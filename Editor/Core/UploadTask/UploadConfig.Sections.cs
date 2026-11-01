using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Wireframe
{
    /// <summary>
    /// Edits a whole section of entries at once (all the Sources, Modifiers, Destinations or Actions), for the menu on a
    /// section's title in the Upload tab.
    /// </summary>
    public partial class UploadConfig
    {
        // Marks text on the clipboard as entries copied from a section
        private const string CopiedSectionKey = "buildUploaderSection";
        private const string CopiedEntriesKey = "entries";

        internal enum Section
        {
            Sources,
            Modifiers,
            Destinations,
            Actions,
        }

        internal static string GetEntryName(Section section)
        {
            switch (section)
            {
                case Section.Sources:
                    return "Source";
                case Section.Modifiers:
                    return "Modifier";
                case Section.Destinations:
                    return "Destination";
                default:
                    return "Action";
            }
        }

        internal int GetEntryCount(Section section)
        {
            return GetEntries(section).Count;
        }

        /// <param name="enabled">Count the enabled entries, or the disabled ones. Actions set to Never count as disabled</param>
        internal int GetEntryCount(Section section, bool enabled)
        {
            int count = 0;
            foreach (object entry in GetEntries(section))
            {
                if (IsEnabled(entry) == enabled)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// The types an entry can be, named as the section's type dropdown names them.
        /// </summary>
        internal List<KeyValuePair<string, Type>> GetEntryTypes(Section section)
        {
            IEnumerable<KeyValuePair<string, Type>> types;
            switch (section)
            {
                case Section.Sources:
                    types = UIHelpers.SourcesPopup.Values.Select(a => GetEntryType(a, a.Type));
                    break;
                case Section.Modifiers:
                    types = UIHelpers.ModifiersPopup.Values.Select(a => GetEntryType(a, a.Type));
                    break;
                case Section.Destinations:
                    types = UIHelpers.DestinationsPopup.Values.Select(a => GetEntryType(a, a.Type));
                    break;
                default:
                    types = UIHelpers.ActionsPopup.Values.Select(a => GetEntryType(a, a.Type));
                    break;
            }
            return types.ToList();
        }

        private KeyValuePair<string, Type> GetEntryType(DropdownElement element, Type type)
        {
            return new KeyValuePair<string, Type>(element.Id + ". " + m_context.FormatString(element.DisplayName), type);
        }

        /// <param name="type">The type of the new entry, or null to choose it later</param>
        internal void AddEntry(Section section, Type type)
        {
            switch (section)
            {
                case Section.Sources:
                    if (Utils.CreateInstance(type, out AUploadSource source))
                    {
                        AddSource(source);
                    }
                    else
                    {
                        AddSource(new SourceData() { Enabled = true });
                    }
                    break;
                case Section.Modifiers:
                    if (Utils.CreateInstance(type, out AUploadModifer modifier))
                    {
                        AddModifier(modifier);
                    }
                    else
                    {
                        AddModifier(new ModifierData() { Enabled = true });
                    }
                    break;
                case Section.Destinations:
                    if (Utils.CreateInstance(type, out AUploadDestination destination))
                    {
                        AddDestination(destination);
                    }
                    else
                    {
                        AddDestination(new DestinationData() { Enabled = true });
                    }
                    break;
                default:
                    if (Utils.CreateInstance(type, out AUploadAction action))
                    {
                        AddPostAction(action);
                    }
                    else
                    {
                        AddPostAction(new UploadActionData() { WhenToExecute = UploadActionData.UploadCompleteStatus.Always });
                    }
                    break;
            }
        }

        /// <summary>
        /// Actions have no Enabled box: enabling sets the ones that never run to Always, and disabling sets them all to
        /// Never.
        /// </summary>
        internal void SetAllEnabled(Section section, bool enabled)
        {
            foreach (object entry in GetEntries(section))
            {
                switch (entry)
                {
                    case SourceData source:
                        source.Enabled = enabled;
                        break;
                    case ModifierData modifier:
                        modifier.Enabled = enabled;
                        break;
                    case DestinationData destination:
                        destination.Enabled = enabled;
                        break;
                    case UploadActionData action:
                        if (!enabled)
                        {
                            action.WhenToExecute = UploadActionData.UploadCompleteStatus.Never;
                        }
                        else if (action.WhenToExecute == UploadActionData.UploadCompleteStatus.Never)
                        {
                            action.WhenToExecute = UploadActionData.UploadCompleteStatus.Always;
                        }
                        break;
                }
            }
        }

        internal void RemoveDisabledEntries(Section section)
        {
            IList entries = GetEntries(section);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!IsEnabled(entries[i]))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        internal void ClearEntries(Section section)
        {
            GetEntries(section).Clear();
        }

        /// <returns>JSON to put on the clipboard, which PasteEntries reads back into the same section of any config</returns>
        internal string CopyEntries(Section section)
        {
            List<Dictionary<string, object>> entries;
            switch (section)
            {
                case Section.Sources:
                    entries = m_buildSources.Select(a => a.Serialize()).ToList();
                    break;
                case Section.Modifiers:
                    entries = m_modifiers.Select(a => a.Serialize()).ToList();
                    break;
                case Section.Destinations:
                    entries = m_buildDestinations.Select(a => a.Serialize()).ToList();
                    break;
                default:
                    entries = m_postActions.Select(a => a.Serialize()).ToList();
                    break;
            }

            Dictionary<string, object> data = new Dictionary<string, object>
            {
                [CopiedSectionKey] = section.ToString(),
                [CopiedEntriesKey] = entries
            };
            return JSON.SerializeObject(data);
        }

        internal static bool CanPasteEntries(Section section, string text)
        {
            return ReadCopiedEntries(section, text) != null;
        }

        /// <summary>
        /// Adds copies of the entries in the text after the section's own entries.
        /// </summary>
        /// <returns>How many entries were added. None if the text wasn't copied from this section</returns>
        internal int PasteEntries(Section section, string text)
        {
            List<object> entries = ReadCopiedEntries(section, text);
            if (entries == null)
            {
                return 0;
            }

            foreach (object entry in entries)
            {
                Dictionary<string, object> entryData = (Dictionary<string, object>)entry;
                switch (section)
                {
                    case Section.Sources:
                        SourceData source = new SourceData();
                        source.Deserialize(entryData);
                        AddSource(source);
                        break;
                    case Section.Modifiers:
                        ModifierData modifier = new ModifierData();
                        modifier.Deserialize(entryData);
                        AddModifier(modifier);
                        break;
                    case Section.Destinations:
                        DestinationData destination = new DestinationData();
                        destination.Deserialize(entryData);
                        AddDestination(destination);
                        break;
                    default:
                        UploadActionData action = new UploadActionData();
                        action.Deserialize(entryData);
                        AddPostAction(action);
                        break;
                }
            }
            return entries.Count;
        }

        private static List<object> ReadCopiedEntries(Section section, string text)
        {
            string json = text?.Trim();
            if (string.IsNullOrEmpty(json) || json[0] != '{' || !json.Contains(CopiedSectionKey))
            {
                return null;
            }

            Dictionary<string, object> data;
            try
            {
                data = JSON.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch (Exception)
            {
                // Some other text that happens to contain the key
                return null;
            }

            if (data == null ||
                !data.TryGetValue(CopiedSectionKey, out object copiedSection) ||
                copiedSection as string != section.ToString() ||
                !data.TryGetValue(CopiedEntriesKey, out object entries))
            {
                return null;
            }
            return entries as List<object>;
        }

        private IList GetEntries(Section section)
        {
            switch (section)
            {
                case Section.Sources:
                    return m_buildSources;
                case Section.Modifiers:
                    return m_modifiers;
                case Section.Destinations:
                    return m_buildDestinations;
                default:
                    return m_postActions;
            }
        }

        private static bool IsEnabled(object entry)
        {
            switch (entry)
            {
                case SourceData source:
                    return source.Enabled;
                case ModifierData modifier:
                    return modifier.Enabled;
                case DestinationData destination:
                    return destination.Enabled;
                case UploadActionData action:
                    return action.WhenToExecute != UploadActionData.UploadCompleteStatus.Never;
                default:
                    return false;
            }
        }
    }
}
