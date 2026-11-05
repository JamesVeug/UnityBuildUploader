using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Partial class for GUI content
    /// TODO: Move to UIToolkit
    /// </summary>
    public partial class UploadConfig
    {
        internal const float StopTaskIfFailedToggleWidth = 150;
        private const float ThinCollapsedTypeWidth = 120;
        private const float ThinSectionSpacing = 10;
        // Lines a Task Action's settings up with its label, after its foldout and settings buttons
        internal const float ThinEntryIndent = 48;
        // The foldout in front of each entry of a thin section
        private const float ThinEntryFoldoutWidth = 24;
        private const float LogIndent = 15;
        
        internal bool Collapsed { get; set; } = true;
        
        private GUIStyle m_titleStyle;
        private GUIStyle m_thinSectionLabelStyle;
        private GUIStyle m_thinAddButtonStyle;
        private bool m_deferredIsDirty;
        private readonly HashSet<object> m_thinExpandedEntries = new HashSet<object>();

        /// <summary>
        /// Indents what's drawn inside it, e.g. so a thin section's entries start under its label instead of under
        /// its foldout.
        /// </summary>
        internal class ThinEntryIndentScope : GUI.Scope
        {
            public ThinEntryIndentScope(float indent)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indent);
                GUILayout.BeginVertical();
            }

            protected override void CloseScope()
            {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        public void SetupDefaults()
        {
            AddSource(new SourceData()
            {
                Enabled = true,
                Source = null,
                SourceType = null
            });
            
            AddDestination(new DestinationData()
            {
                Enabled = true,
                Destination = null,
                DestinationType = null
            });
            
            AddDefaultModifiers();
        }

        public void AddDefaultModifiers()
        {
            // All Unity builds include a X_BurstDebugInformation_DoNotShip folder
            // This isn't needed so add it as a default modifier
            ExcludeFoldersModifier regexBuildModifier = new ExcludeFoldersModifier(AExcludePathsByRegex_UploadModifier.WhenToExclude.DeleteFromCache);
            regexBuildModifier.Add(".*DoNotShip", true, false);
            regexBuildModifier.Add(".*ButDontShipItWithYourGame", true, false);
            AddModifier(new ModifierData(regexBuildModifier, true));
        }

        private void SetupGUI()
        {
            if (m_titleStyle == null)
            {
                m_titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            if (m_thinAddButtonStyle == null)
            {
                m_thinAddButtonStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (m_thinSectionLabelStyle == null)
            {
                m_thinSectionLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
        }

        internal void OnGUI(float windowWidth, ref bool isDirty)
        {
            SetupGUI();

            if (m_deferredIsDirty)
            {
                isDirty = true;
                m_deferredIsDirty = false;
            }

            using (new EditorGUI.DisabledScope(!Enabled))
            {
                if (Collapsed)
                {
                    OnGUICollapsed(windowWidth, ref isDirty);
                }
                else
                {
                    OnGUIExpanded(windowWidth, ref isDirty);
                }
            }
        }

        private void OnGUICollapsed(float windowWidth, ref bool isDirty)
        {
            float splitWidth = 100;
            float maxWidth = windowWidth - splitWidth - 120;
            float parts = maxWidth / 2 - splitWidth;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (SourceData source in m_buildSources)
                    {
                        if (!source.Enabled)
                        {
                            continue;
                        }
                        
                        // Draw the build but on one line
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Source Type
                            isDirty |= DrawSourceTypePopup(source, GUILayout.MaxWidth(120));

                            // Source
                            float sourceWidth = parts;
                            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(sourceWidth)))
                            {
                                if (source.Source != null)
                                {
                                    source.Source.OnGUICollapsed(ref isDirty, sourceWidth);
                                }
                            }
                        }
                    }

                    DrawSourceLogs();
                }

                // Progress / Modifiers
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string progressText = "->";
                        GUILayout.Label(progressText, m_titleStyle, GUILayout.Width(splitWidth));
                    }

                    DrawModifierLogs();
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (DestinationData destinationData in m_buildDestinations)
                    {
                        if (!destinationData.Enabled)
                        {
                            continue;
                        }
                        
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Destination Type
                            isDirty |= DrawDestinationTypePopup(destinationData);

                            // Destination
                            float destinationWidth = parts;
                            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(destinationWidth)))
                            {
                                if (destinationData.Destination != null)
                                {
                                    destinationData.Destination.OnPreGUI(ref isDirty, m_context);
                                    destinationData.Destination.OnGUICollapsed(ref isDirty, parts);
                                }
                            }
                        }

                        DrawCollapsedDestinationLogs(destinationData);
                    }
                }

                if (Enabled && m_postActions.Count > 0)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawPostActionLogs();
                    }
                }
            }
        }

        private void DrawSourceLogs()
        {
            if (!Enabled)
            {
                return;
            }

            foreach (GUIContent error in GetSourceErrors())
            {
                DrawError(error);
            }

            foreach (GUIContent warning in GetSourceWarnings())
            {
                DrawWarning(warning);
            }
        }

        private void DrawModifierLogs()
        {
            if (!Enabled)
            {
                return;
            }

            foreach (GUIContent error in GetModifierErrors())
            {
                DrawError(error);
            }

            foreach (GUIContent warning in GetModifierWarnings())
            {
                DrawWarning(warning);
            }
        }

        private void DrawCollapsedDestinationLogs(DestinationData destinationData)
        {
            if (destinationData.Destination == null)
            {
                return;
            }

            List<GUIContent> logs = new List<GUIContent>();
            destinationData.Destination.TryGetErrors(logs);
            foreach (GUIContent error in logs)
            {
                DrawError(error);
            }

            logs.Clear();
            destinationData.Destination.TryGetWarnings(logs, m_context);
            foreach (GUIContent warning in logs)
            {
                DrawWarning(warning);
            }
        }

        private void DrawPostActionLogs()
        {
            if (!Enabled)
            {
                return;
            }

            foreach (GUIContent error in GetPostActionErrors())
            {
                DrawError(error);
            }

            foreach (GUIContent warning in GetPostActionWarnings())
            {
                DrawWarning(warning);
            }
        }

        /// <returns>True if another type was chosen</returns>
        private bool DrawSourceTypePopup(SourceData source, params GUILayoutOption[] options)
        {
            if (!UIHelpers.SourcesPopup.DrawPopup(ref source.SourceType, m_context, options))
            {
                return false;
            }

            Utils.CreateInstance(source.SourceType?.Type, out source.Source);
            if (source.Source != null)
            {
                source.Source.Context.SetParent(m_context);
            }
            return true;
        }

        /// <returns>True if another type was chosen</returns>
        private bool DrawModifierTypePopup(ModifierData modifiers, params GUILayoutOption[] options)
        {
            if (!UIHelpers.ModifiersPopup.DrawPopup(ref modifiers.ModifierType, m_context, options))
            {
                return false;
            }

            Utils.CreateInstance(modifiers.ModifierType?.Type, out modifiers.Modifier);
            if (modifiers.Modifier != null)
            {
                modifiers.Modifier.Context.SetParent(m_context);
            }
            return true;
        }

        /// <returns>True if another type was chosen</returns>
        private bool DrawDestinationTypePopup(DestinationData destinationData, params GUILayoutOption[] options)
        {
            if (!UIHelpers.DestinationsPopup.DrawPopup(ref destinationData.DestinationType, m_context, options))
            {
                return false;
            }

            Utils.CreateInstance(destinationData.DestinationType?.Type, out destinationData.Destination);
            if (destinationData.Destination != null)
            {
                destinationData.Destination.Context.SetParent(m_context);
            }
            return true;
        }

        /// <returns>True if another type was chosen</returns>
        private bool DrawPostActionTypePopup(UploadActionData actionData, params GUILayoutOption[] options)
        {
            if (!UIHelpers.ActionsPopup.DrawPopup(ref actionData.ActionType, m_context, options))
            {
                return false;
            }

            Utils.CreateInstance(actionData.ActionType?.Type, out actionData.UploadAction);
            if (actionData.UploadAction != null)
            {
                actionData.UploadAction.Context.SetParent(m_context);
                actionData.StopTaskIfFailed = actionData.UploadAction.DefaultStopTaskIfFailed;
            }
            return true;
        }

        public static void DrawWarning(GUIContent content)
        {
            Color color = GUI.color;
            GUI.color = Color.yellow;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(LogIndent);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUIContent iconContent = new GUIContent(Utils.WarningIcon, "Warning: " + content.tooltip);
                    GUILayout.Label(iconContent, EditorStyles.label, GUILayout.Width(15), GUILayout.Height(15));
                    GUILayout.Label(content, EditorStyles.wordWrappedLabel);
                    DrawSettingsLinkButton(content, color);
                }
            }
            GUI.color = color;
        }

        public static void DrawError(GUIContent content)
        {
            Color color = GUI.color;
            GUI.color = Color.red;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(LogIndent);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUIContent iconContent = new GUIContent(Utils.ErrorIcon, "Error: " + content.tooltip);
                    GUILayout.Label(iconContent, EditorStyles.label, GUILayout.Width(15), GUILayout.Height(15));
                    GUILayout.Label(content, EditorStyles.wordWrappedLabel);
                    DrawSettingsLinkButton(content, color);
                }
            }
            GUI.color = color;
        }

        private static void DrawSettingsLinkButton(GUIContent content, Color originalColor)
        {
            if (content is SettingsLinkGUIContent settingsLink)
            {
                GUILayout.FlexibleSpace();
                Color tint = GUI.color;
                GUI.color = originalColor;

                // Logging in is usually the whole fix, so offer it here rather than making the user go
                // to Preferences to press the same button.
                AuthStatusButton.Draw(settingsLink.AuthService);

                if (GUILayout.Button(settingsLink.ButtonText, GUILayout.ExpandWidth(false)))
                {
                    settingsLink.OpenSettings();
                }
                GUI.color = tint;
            }
        }

        private void OnGUIExpanded(float windowWidth, ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                float maxWidth = windowWidth / 4;
                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawSectionTitle(Section.Sources);
                    DrawExpandedSources(ref isDirty);
                }

                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawSectionTitle(Section.Modifiers);
                    DrawExpandedModifiers(ref isDirty);
                }

                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawSectionTitle(Section.Destinations);
                    DrawExpandedDestinations(ref isDirty);
                }
                
                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawSectionTitle(Section.Actions);
                    DrawExpandedActions(ref isDirty);
                }
            }
        }

        /// <summary>
        /// Draws the sections stacked instead of side by side, for windows too thin for OnGUI. Each entry has its own
        /// foldout; a folded entry shows its type dropdown and one-line settings, as a collapsed Upload Config does.
        /// </summary>
        /// <param name="width">The width a section's contents can fill</param>
        internal void OnGUIThin(float width, ref bool isDirty)
        {
            SetupGUI();

            if (m_deferredIsDirty)
            {
                isDirty = true;
                m_deferredIsDirty = false;
            }

            float settingsWidth = width - ThinEntryFoldoutWidth - ThinCollapsedTypeWidth;

            GUILayout.Space(ThinSectionSpacing);

            // Only the contents are disabled with the config, so its entries can still be folded
            using (new GUILayout.VerticalScope("box"))
            {
                DrawThinSectionHeader(Section.Sources);
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    for (int i = 0; i < m_buildSources.Count; i++)
                    {
                        SourceData source = m_buildSources[i];
                        using (new GUILayout.HorizontalScope())
                        {
                            bool expanded = DrawThinEntryFoldout(source);
                            using (new GUILayout.VerticalScope())
                            {
                                if (expanded)
                                {
                                    DrawExpandedSource(i, ref isDirty);
                                }
                                else
                                {
                                    DrawThinCollapsedSource(source, settingsWidth, ref isDirty);
                                }
                            }
                        }
                    }

                    DrawSourceLogs();
                }
            }

            GUILayout.Space(ThinSectionSpacing);

            using (new GUILayout.VerticalScope("box"))
            {
                DrawThinSectionHeader(Section.Modifiers);
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    for (int i = 0; i < m_modifiers.Count; i++)
                    {
                        ModifierData modifier = m_modifiers[i];
                        using (new GUILayout.HorizontalScope())
                        {
                            bool expanded = DrawThinEntryFoldout(modifier);
                            using (new GUILayout.VerticalScope())
                            {
                                if (expanded)
                                {
                                    DrawExpandedModifier(i, ref isDirty);
                                }
                                else
                                {
                                    DrawThinCollapsedModifier(modifier, settingsWidth, ref isDirty);
                                }
                            }
                        }
                    }

                    DrawModifierLogs();
                }
            }

            GUILayout.Space(ThinSectionSpacing);

            using (new GUILayout.VerticalScope("box"))
            {
                DrawThinSectionHeader(Section.Destinations);
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    for (int i = 0; i < m_buildDestinations.Count; i++)
                    {
                        DestinationData destinationData = m_buildDestinations[i];
                        using (new GUILayout.HorizontalScope())
                        {
                            bool expanded = DrawThinEntryFoldout(destinationData);
                            using (new GUILayout.VerticalScope())
                            {
                                if (expanded)
                                {
                                    DrawExpandedDestination(i, ref isDirty);
                                }
                                else
                                {
                                    DrawThinCollapsedDestination(destinationData, settingsWidth, ref isDirty);
                                }
                            }
                        }

                        DrawCollapsedDestinationLogs(destinationData);
                    }

                }
            }

            GUILayout.Space(ThinSectionSpacing);

            using (new GUILayout.VerticalScope("box"))
            {
                DrawThinSectionHeader(Section.Actions);
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    for (int i = 0; i < m_postActions.Count; i++)
                    {
                        UploadActionData actionData = m_postActions[i];
                        using (new GUILayout.HorizontalScope())
                        {
                            bool expanded = DrawThinEntryFoldout(actionData);
                            using (new GUILayout.VerticalScope())
                            {
                                if (expanded)
                                {
                                    DrawExpandedAction(i, ref isDirty, true);
                                }
                                else
                                {
                                    DrawThinCollapsedAction(actionData, settingsWidth, ref isDirty);
                                }
                            }
                        }
                    }

                    DrawPostActionLogs();
                }
            }
        }

        /// <summary>
        /// The settings button that opens the section's menu, the label, which also opens it when right-clicked. An empty
        /// section shows its count and a + button that picks the type of a new entry.
        /// </summary>
        private void DrawThinSectionHeader(Section section)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (CustomSettingsIcon.OnGUI())
                {
                    ShowSectionMenu(section);
                }

                bool empty = GetEntryCount(section) == 0;
                GUIContent content = new GUIContent(empty ? $"{section} (0)" : section.ToString(), GetSectionMenuTooltip(section));
                Rect rect = GUILayoutUtility.GetRect(content, m_thinSectionLabelStyle, GUILayout.Height(20), GUILayout.ExpandWidth(true));
                if (EditorUtils.IsRightClicked(rect))
                {
                    ShowSectionMenu(section);
                }

                GUI.Label(rect, content, m_thinSectionLabelStyle);

                if (!empty)
                {
                    return;
                }

                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    GUIContent addContent = new GUIContent("+", "Add New " + GetEntryName(section));
                    if (GUILayout.Button(addContent, m_thinAddButtonStyle, GUILayout.Width(20), GUILayout.Height(20)))
                    {
                        GenericMenu menu = new GenericMenu();
                        AddNewEntryMenuItems(menu, section, "");
                        menu.ShowAsContext();
                    }
                }
            }
        }

        /// <summary>
        /// The foldout in front of an entry of a thin section. Entries start folded.
        /// </summary>
        /// <returns>True if the entry is expanded</returns>
        private bool DrawThinEntryFoldout(object entry)
        {
            bool expanded = m_thinExpandedEntries.Contains(entry);
            if (CustomFoldoutButton.OnGUI(!expanded))
            {
                expanded = !expanded;
                if (expanded)
                {
                    m_thinExpandedEntries.Add(entry);
                }
                else
                {
                    m_thinExpandedEntries.Remove(entry);
                }
            }

            return expanded;
        }

        /// <summary>
        /// The title above a section in the wide layout, which opens the section's menu when right-clicked.
        /// </summary>
        private void DrawSectionTitle(Section section)
        {
            GUIContent content = new GUIContent(section.ToString(), GetSectionMenuTooltip(section));
            Rect rect = GUILayoutUtility.GetRect(content, m_titleStyle);
            if (EditorUtils.IsRightClicked(rect))
            {
                ShowSectionMenu(section);
            }

            GUI.Label(rect, content, m_titleStyle);
        }

        private static string GetSectionMenuTooltip(Section section)
        {
            return $"Right-click to add, enable, copy or clear {section}";
        }

        /// <summary>
        /// Opened by right-clicking a section's title.
        /// </summary>
        private void ShowSectionMenu(Section section)
        {
            GenericMenu menu = new GenericMenu();
            AddSectionMenuItems(menu, section, "");
            menu.ShowAsContext();
        }

        /// <summary>
        /// The section menu's items, which the Upload Config's settings menu also shows in a submenu per section.
        /// </summary>
        /// <param name="path">The submenu to put them in, e.g. "Sources/", or "" for none</param>
        internal void AddSectionMenuItems(GenericMenu menu, Section section, string path)
        {
            string title = section.ToString();
            string entryName = GetEntryName(section);
            int enabledCount = GetEntryCount(section, true);
            int disabledCount = GetEntryCount(section, false);

            AddNewEntryMenuItems(menu, section, path + "Add New " + entryName + "/");

            if (section == Section.Modifiers)
            {
                menu.AddMenuItem(path + "Add Default Modifiers", false, () => EditSection(AddDefaultModifiers), !Enabled);
            }

            menu.AddSeparator(path);

            menu.AddMenuItem(path + "Enable All " + title, false, () => EditSection(() => SetAllEnabled(section, true)), !Enabled || disabledCount == 0);
            menu.AddMenuItem(path + "Disable All " + title, false, () => EditSection(() => SetAllEnabled(section, false)), !Enabled || enabledCount == 0);
            menu.AddMenuItem(path + "Remove Disabled " + title, false, () =>
            {
                if (EditorUtility.DisplayDialog("Remove Disabled " + title,
                        $"Are you sure you want to remove the {disabledCount} disabled {title}?",
                        "Yes", "Oops No!"))
                {
                    EditSection(() => RemoveDisabledEntries(section));
                }
            }, !Enabled || disabledCount == 0);

            menu.AddSeparator(path);

            menu.AddMenuItem(path + "Copy " + title, false, () => EditorGUIUtility.systemCopyBuffer = CopyEntries(section), GetEntryCount(section) == 0);
            menu.AddMenuItem(path + "Paste " + title, false, () => EditSection(() => PasteEntries(section, EditorGUIUtility.systemCopyBuffer)),
                !Enabled || !CanPasteEntries(section, EditorGUIUtility.systemCopyBuffer));

            menu.AddSeparator(path);

            menu.AddMenuItem(path + "Clear All " + title, false, () =>
            {
                if (EditorUtility.DisplayDialog("Clear All " + title,
                        "Are you sure you want to remove all " + title + "?",
                        "Yes", "Oops No!"))
                {
                    EditSection(() => ClearEntries(section));
                }
            }, !Enabled || GetEntryCount(section) == 0);
        }

        /// <summary>
        /// Choose Later then one item per entry type, each adding a new entry to the section.
        /// </summary>
        private void AddNewEntryMenuItems(GenericMenu menu, Section section, string path)
        {
            menu.AddMenuItem(path + "Choose Later", false, () => EditSection(() => AddEntry(section, null)), !Enabled);
            menu.AddSeparator(path);
            foreach (KeyValuePair<string, Type> entryType in GetEntryTypes(section))
            {
                Type type = entryType.Value;
                menu.AddMenuItem(path + entryType.Key, false, () => EditSection(() => AddEntry(section, type)), !Enabled);
            }
        }

        /// <summary>
        /// Move To Top, Move Up, Move Down and Move To Bottom for the settings menu on one entry of a section.
        /// </summary>
        private void AddEntryMoveMenuItems(GenericMenu menu, Section section, object entry)
        {
            int index = GetEntryIndex(section, entry);
            bool isFirst = index <= 0;
            bool isLast = index < 0 || index >= GetEntryCount(section) - 1;

            menu.AddMenuItem("Move To Top", false, () => EditSection(() => MoveEntry(section, entry, 0)), isFirst);
            menu.AddMenuItem("Move Up", false, () => EditSection(() => MoveEntry(section, entry, GetEntryIndex(section, entry) - 1)), isFirst);
            menu.AddMenuItem("Move Down", false, () => EditSection(() => MoveEntry(section, entry, GetEntryIndex(section, entry) + 1)), isLast);
            menu.AddMenuItem("Move To Bottom", false, () => EditSection(() => MoveEntry(section, entry, GetEntryCount(section) - 1)), isLast);
        }

        /// <summary>
        /// Menu items run outside OnGUI, so the change is saved on the next one.
        /// </summary>
        private void EditSection(Action edit)
        {
            edit();
            m_deferredIsDirty = true;
        }

        // A folded entry shows on one row, its type then its settings, as OnGUICollapsed does. Disabled entries are
        // greyed out.
        private void DrawThinCollapsedSource(SourceData source, float settingsWidth, ref bool isDirty)
        {
            using (new EditorGUI.DisabledScope(!source.Enabled))
            using (new EditorGUILayout.HorizontalScope())
            {
                isDirty |= DrawSourceTypePopup(source, GUILayout.Width(ThinCollapsedTypeWidth));
                if (source.Source != null)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(settingsWidth)))
                    {
                        source.Source.OnGUICollapsed(ref isDirty, settingsWidth);
                    }
                }
            }
        }

        private void DrawThinCollapsedModifier(ModifierData modifier, float settingsWidth, ref bool isDirty)
        {
            using (new EditorGUI.DisabledScope(!modifier.Enabled))
            using (new EditorGUILayout.HorizontalScope())
            {
                isDirty |= DrawModifierTypePopup(modifier, GUILayout.Width(ThinCollapsedTypeWidth));
                if (modifier.Modifier != null)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(settingsWidth)))
                    {
                        modifier.Modifier.OnGUICollapsed(ref isDirty, settingsWidth, m_context);
                    }
                }
            }
        }

        private void DrawThinCollapsedDestination(DestinationData destinationData, float settingsWidth, ref bool isDirty)
        {
            using (new EditorGUI.DisabledScope(!destinationData.Enabled))
            using (new EditorGUILayout.HorizontalScope())
            {
                isDirty |= DrawDestinationTypePopup(destinationData, GUILayout.Width(ThinCollapsedTypeWidth));
                if (destinationData.Destination != null)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(settingsWidth)))
                    {
                        destinationData.Destination.OnPreGUI(ref isDirty, m_context);
                        destinationData.Destination.OnGUICollapsed(ref isDirty, settingsWidth);
                    }
                }
            }
        }

        private void DrawThinCollapsedAction(UploadActionData actionData, float settingsWidth, ref bool isDirty)
        {
            using (new EditorGUI.DisabledScope(actionData.WhenToExecute == UploadActionData.UploadCompleteStatus.Never))
            using (new EditorGUILayout.HorizontalScope())
            {
                isDirty |= DrawPostActionTypePopup(actionData, GUILayout.Width(ThinCollapsedTypeWidth));
                if (actionData.UploadAction != null)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(settingsWidth)))
                    {
                        actionData.UploadAction.OnGUICollapsed(ref isDirty, settingsWidth);
                    }
                }
            }
        }

        private void DrawExpandedSources(ref bool isDirty)
        {
            for (var i = 0; i < m_buildSources.Count; i++)
            {
                DrawExpandedSource(i, ref isDirty);
            }

            DrawAddNewSourceButton(ref isDirty);
        }

        private void DrawExpandedSource(int i, ref bool isDirty)
        {
            var source = m_buildSources[i];
            using (new GUILayout.HorizontalScope())
            {
                isDirty |= CustomToggle.DrawToggle(ref source.Enabled, GUILayout.Width(20));

                using (new EditorGUI.DisabledScope(!source.Enabled))
                {
                    GUILayout.Label("Source Type: ", GUILayout.Width(100));
                    isDirty |= DrawSourceTypePopup(source);
                }

                if (source.SourceType != null)
                {
                    if (source.SourceType.Type.TryGetWikiLink(out string sourceURL))
                    {
                        if (GUILayout.Button("?", GUILayout.Width(20)))
                        {
                            Application.OpenURL(sourceURL);
                        }
                    }
                }
                        
                if (CustomSettingsIcon.OnGUI())
                {
                    GenericMenu menu = new GenericMenu();
                    AddEntryMoveMenuItems(menu, Section.Sources, source);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Remove Source"), false, () =>
                    {
                        if (EditorUtility.DisplayDialog("Remove Source",
                                "Are you sure you want to remove this source?",
                                "Yes", "Oops No!"))
                        {
                            m_buildSources.Remove(source);
                            m_deferredIsDirty = true;
                        }
                    });
                    menu.ShowAsContext();
                }
            }

            if (source.Source != null)
            {
                using (new EditorGUI.DisabledScope(!source.Enabled))
                {
                    source.Source.OnGUIExpanded(ref isDirty, source);

                    using (new GUILayout.HorizontalScope())
                    {
                        GUIContent subFolderContent = new GUIContent("Sub Folder: ", 
                            "A sub-path in the cached directory of which this source will be saved to before being modified and uploaded. Leave empty to save to the root folder.");
                        GUILayout.Label(subFolderContent, GUILayout.Width(120));
                        if (EditorUtils.FormatStringTextField(ref source.SubFolder, ref source.ShowFormattedExportFolder, m_context))
                        {
                            isDirty = true;
                        }
                    }

                    if (source.Source.CanCacheContents)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUIContent doNotCacheLabel = new GUIContent("Do Not Cache: ",
                                "If enabled, all files will be saved directly to the folder for the upload task and not cached for future reference. This will speed up uploading in some cases at the cost of having to re-get the source contents every upload." +
                                "\nExample: If using BuildConfigSource it will make a full new build every upload");
                            GUILayout.Label(doNotCacheLabel, GUILayout.Width(120));
                            isDirty |= CustomToggle.DrawToggle(ref source.DoNotCache, GUILayout.Width(20));
                        }
                    }

                    foreach (ModifierData modifer in m_modifiers)
                    {
                        if (modifer.Modifier == null || !modifer.Enabled)
                        {
                            continue;
                        }
                                
                        List<GUIContent> errors = new List<GUIContent>();
                        modifer.Modifier.TryGetErrors(source.Source, errors);
                        foreach (GUIContent warning in errors)
                        {
                            DrawError(warning);
                        }
                                
                        List<GUIContent> warnings = new List<GUIContent>();
                        modifer.Modifier.TryGetWarnings(source.Source, warnings);
                        foreach (GUIContent warning in warnings)
                        {
                            DrawWarning(warning);
                        }
                    }
                }
            }

            if (i > 0)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Duplicate Files: ", GUILayout.Width(120));
                    var newHandler = (Utils.FileExistHandling)EditorGUILayout.EnumPopup(source.DuplicateFileHandling);
                    if (source.DuplicateFileHandling != newHandler)
                    {
                        source.DuplicateFileHandling = newHandler;
                        isDirty = true;
                    }
                }
                    
            }
                    
            if (source.Enabled)
            {
                if (source.Source != null)
                {
                    List<GUIContent> errors = new List<GUIContent>();
                    source.Source.TryGetErrors(errors);
                    foreach (GUIContent error in errors)
                    {
                        DrawError(error);
                    }
                        
                    List<GUIContent> warnings = new List<GUIContent>();
                    source.Source.TryGetWarnings(warnings);
                    foreach (GUIContent warning in warnings)
                    {
                        DrawWarning(warning);
                    }
                }
                else
                {
                    DrawError(new GUIContent("No source selected"));
                }
            }
                    
            GUILayout.Space(10);
        }

        private void DrawAddNewSourceButton(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Source"))
                {
                    AddSource(new SourceData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        private void DrawExpandedModifiers(ref bool isDirty)
        {
            for (var i = 0; i < m_modifiers.Count; i++)
            {
                DrawExpandedModifier(i, ref isDirty);
            }

            DrawAddNewModifierButton(ref isDirty);
        }

        private void DrawExpandedModifier(int i, ref bool isDirty)
        {
            ModifierData modifiers = m_modifiers[i];
            using (new GUILayout.HorizontalScope())
            {
                isDirty |= CustomToggle.DrawToggle(ref modifiers.Enabled, GUILayout.Width(20));

                using (new EditorGUI.DisabledScope(!modifiers.Enabled))
                {
                    GUILayout.Label("Modifier Type: ", GUILayout.Width(100));
                    isDirty |= DrawModifierTypePopup(modifiers);
                }
                        
                if (modifiers.ModifierType != null)
                {
                    if (modifiers.ModifierType.Type.TryGetWikiLink(out string sourceURL))
                    {
                        if (GUILayout.Button("?", GUILayout.Width(20)))
                        {
                            Application.OpenURL(sourceURL);
                        }
                    }
                }

                if (CustomSettingsIcon.OnGUI())
                {
                    GenericMenu menu = new GenericMenu();
                    AddEntryMoveMenuItems(menu, Section.Modifiers, modifiers);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Remove Modifier"), false, () =>
                    {
                        if (EditorUtility.DisplayDialog("Remove Modifier",
                                "Are you sure you want to remove this Modifier?",
                                "Yes", "Oops No!"))
                        {
                            m_modifiers.Remove(modifiers);
                            m_deferredIsDirty = true;
                        }
                    });
                    menu.ShowAsContext();
                }
            }
                    
            if (modifiers.Modifier != null)
            {
                using (new EditorGUI.DisabledScope(!modifiers.Enabled))
                {
                    modifiers.Modifier.OnGUIExpanded(ref isDirty, m_context);
                }


                if (modifiers.Enabled)
                {
                    List<GUIContent> errors = new List<GUIContent>();
                    modifiers.Modifier.TryGetErrors(this, errors);
                    foreach (GUIContent error in errors)
                    {
                        DrawError(error);
                    }

                    List<GUIContent> warnings = new List<GUIContent>();
                    modifiers.Modifier.TryGetWarnings(this, warnings);
                    foreach (GUIContent warning in warnings)
                    {
                        DrawWarning(warning);
                    }
                }
            }
            else if (modifiers.Enabled)
            {
                DrawError(new GUIContent("No modifier selected"));
            }
                    
            GUILayout.Space(10);
        }

        private void DrawAddNewModifierButton(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Modifier"))
                {
                    AddModifier(new ModifierData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        private void DrawExpandedDestinations(ref bool isDirty)
        {
            for (var i = 0; i < m_buildDestinations.Count; i++)
            {
                DrawExpandedDestination(i, ref isDirty);
            }

            DrawAddNewDestinationButton(ref isDirty);
        }

        private void DrawExpandedDestination(int i, ref bool isDirty)
        {
            var destinationData = m_buildDestinations[i];
            using (new GUILayout.HorizontalScope())
            {
                isDirty |= CustomToggle.DrawToggle(ref destinationData.Enabled, GUILayout.Width(20));

                GUILayout.Label("Destination Type: ", GUILayout.Width(120));
                using (new EditorGUI.DisabledScope(!destinationData.Enabled))
                {
                    isDirty |= DrawDestinationTypePopup(destinationData);
                }
                        
                if (destinationData.DestinationType != null)
                {
                    if (destinationData.DestinationType.Type.TryGetWikiLink(out string sourceURL))
                    {
                        if (GUILayout.Button("?", GUILayout.Width(20)))
                        {
                            Application.OpenURL(sourceURL);
                        }
                    }
                }
                        
                        
                if (CustomSettingsIcon.OnGUI())
                {
                    GenericMenu menu = new GenericMenu();
                    AddEntryMoveMenuItems(menu, Section.Destinations, destinationData);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Remove Destination"), false, () =>
                    {
                        if(EditorUtility.DisplayDialog("Remove Destination",
                               "Are you sure you want to remove this destination?", 
                               "Yes", "Oops No!"))
                        {
                            m_buildDestinations.Remove(destinationData);
                            m_deferredIsDirty = true;
                        }
                    });
                    menu.ShowAsContext();
                }
            }

            if (destinationData.Destination != null)
            {
                using (new EditorGUI.DisabledScope(!destinationData.Enabled))
                {
                    destinationData.Destination.OnPreGUI(ref isDirty, m_context);
                    destinationData.Destination.OnGUIExpanded(ref isDirty);

                    if (destinationData.Enabled)
                    {
                        List<GUIContent> errors = new List<GUIContent>();
                        destinationData.Destination.TryGetErrors(errors);
                        foreach (GUIContent error in errors)
                        {
                            DrawError(error);
                        }

                        List<GUIContent> warnings = new List<GUIContent>();
                        destinationData.Destination.TryGetWarnings(warnings, m_context);
                        foreach (ModifierData modifier in m_modifiers)
                        {
                            if (modifier.ModifierType == null || !modifier.Enabled)
                            {
                                continue;
                            }

                            modifier.Modifier.TryGetWarnings(destinationData.Destination, warnings);
                        }

                        foreach (GUIContent warning in warnings)
                        {
                            DrawWarning(warning);
                        }
                    }
                }
            }
            else if(destinationData.Enabled)
            {
                DrawError(new GUIContent("No destination selected"));
            }
                    
            GUILayout.Space(10);
        }

        private void DrawAddNewDestinationButton(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Destination"))
                {
                    AddDestination(new DestinationData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Draws the "Stop task if this fails" toggle for an action entry.
        /// </summary>
        /// <returns>True if the value changed</returns>
        internal static bool DrawStopTaskIfFailedToggle(UploadActionData actionData)
        {
            GUIContent content = new GUIContent("Stop task if this fails",
                "If this action fails, the task fails and skips its remaining build and upload steps. " +
                "Cleanup and actions that run when the task finishes still run.");
            bool stop = GUILayout.Toggle(actionData.StopTaskIfFailed, content, GUILayout.Width(StopTaskIfFailedToggleWidth));
            if (stop == actionData.StopTaskIfFailed)
            {
                return false;
            }

            actionData.StopTaskIfFailed = stop;
            return true;
        }

        private void DrawExpandedActions(ref bool isDirty)
        {
            for (var i = 0; i < m_postActions.Count; i++)
            {
                DrawExpandedAction(i, ref isDirty, false);
            }

            DrawAddNewActionButton(ref isDirty);
        }

        /// <param name="thin">Leaves out the label and moves the Stop task toggle to its own line so the row fits a thin window</param>
        private void DrawExpandedAction(int i, ref bool isDirty, bool thin)
        {
            var actionData = m_postActions[i];
            using (new GUILayout.HorizontalScope())
            {
                if (!thin)
                {
                    GUILayout.Label("Action Type: ", GUILayout.Width(120));
                }

                var status =
                    (UploadActionData.UploadCompleteStatus)EditorGUILayout.EnumPopup(actionData.WhenToExecute,
                        GUILayout.Width(100));
                if (status != actionData.WhenToExecute)
                {
                    actionData.WhenToExecute = status;
                    isDirty = true;
                }

                if (!thin)
                {
                    isDirty |= DrawStopTaskIfFailedToggle(actionData);
                }

                bool disabled = actionData.WhenToExecute == UploadActionData.UploadCompleteStatus.Never;
                using (new EditorGUI.DisabledScope(disabled))
                {
                    GUILayoutOption typeWidth = thin ? GUILayout.MinWidth(80) : GUILayout.Width(200);
                    isDirty |= DrawPostActionTypePopup(actionData, typeWidth);
                }
                
                if (CustomSettingsIcon.OnGUI())
                {
                    GenericMenu menu = new GenericMenu();
                    AddEntryMoveMenuItems(menu, Section.Actions, actionData);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Remove Action"), false, () =>
                    {
                        if(EditorUtility.DisplayDialog("Remove Action",
                               "Are you sure you want to remove this Post Action?", 
                               "Yes", "Oops No!"))
                        {
                            m_postActions.Remove(actionData);
                            m_deferredIsDirty = true;
                        }
                    });
                    menu.ShowAsContext();
                }
            }

            if (thin)
            {
                isDirty |= DrawStopTaskIfFailedToggle(actionData);
            }

            using (new GUILayout.VerticalScope())
            {
                if (actionData.ActionType != null)
                {
                    using (new EditorGUI.DisabledScope(actionData.WhenToExecute == UploadActionData.UploadCompleteStatus.Never))
                    {
                        actionData.UploadAction.OnGUIExpanded(ref isDirty);

                        if (actionData.WhenToExecute != UploadActionData.UploadCompleteStatus.Never)
                        {
                            List<GUIContent> errors = new List<GUIContent>();
                            actionData.UploadAction.TryGetErrors(errors);
                            foreach (GUIContent error in errors)
                            {
                                DrawError(error);
                            }

                            List<GUIContent> warnings = new List<GUIContent>();
                            actionData.UploadAction.TryGetWarnings(warnings);
                            foreach (GUIContent warning in warnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }
            }
        }

        private void DrawAddNewActionButton(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Action"))
                {
                    AddPostAction(new UploadActionData()
                    {
                        WhenToExecute = UploadActionData.UploadCompleteStatus.Always
                    });
                    isDirty = true;
                }
            }
        }
    }
}