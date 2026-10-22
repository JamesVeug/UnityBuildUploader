using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public static class EditorUtils
    {
        /// <summary>
        /// Rect-based text field that paints greyed-out example text when the field is empty
        /// and not currently focused, so the user can see what to type. Mirrors GUI.TextField's
        /// signature/return so it can drop into rect-based ReorderableList rows.
        /// </summary>
        public static string PlaceholderTextField(Rect rect, string text, string placeholder)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Keyboard, rect);
            string controlName = "PlaceholderTextField" + controlId;
            GUI.SetNextControlName(controlName);
            string newText = GUI.TextField(rect, text);

            if (string.IsNullOrEmpty(text) &&
                !string.IsNullOrEmpty(placeholder) &&
                GUI.GetNameOfFocusedControl() != controlName)
            {
                GUIStyle style = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.75f) }
                };

                Rect labelRect = rect;
                labelRect.x += 2;
                labelRect.width -= 2;
                GUI.Label(labelRect, placeholder, style);
            }

            return newText;
        }

        public static IEnumerable<Command> GetAllCommands(Context ctx)
        {
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Command command in ctx.LocalCommands.Concat(Context.FormatToCommand.Values).OrderBy(a => a.Key))
            {
                if (IsResolvableKey(command.Key) && seenKeys.Add(command.Key))
                {
                    yield return command;
                }
            }
        }

        /// <summary>
        /// True when the key is a single '{name}' token that <see cref="Context.FormatString"/> can
        /// actually resolve. Local commands take their key from a field the user is still typing
        /// into, so they can be empty or hold nested braces (eg: "{#{buildNumber}}") - offering
        /// those as autocomplete suggestions lets the user pick a key that can never resolve.
        /// </summary>
        public static bool IsResolvableKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length <= 2)
            {
                return false;
            }

            if (key[0] != '{' || key[key.Length - 1] != '}')
            {
                return false;
            }

            return key.IndexOf('{', 1) < 0 && key.IndexOf('}', 0, key.Length - 1) < 0;
        }

        public static string GetFormatStringTextFieldTooltip(Context ctx)
        {
            StringBuilder tooltipBuilder = new StringBuilder();
            tooltipBuilder.AppendLine("Show the text as it will appear with formats:");

            const int maximum = 30;
            int ignored = 0;
            List<string> commandKeys = new List<string>(maximum);
            List<string> commandValues = new List<string>(maximum);
            foreach (Command command in GetAllCommands(ctx))
            {
                string key = command.Key;
                if (commandKeys.Count >= maximum)
                {
                    ignored++;
                    continue;
                }
                
                string value = ctx.FormatString(key);
                if (Preferences.ToolTipsHideBlackValuesToggle && (string.IsNullOrEmpty(value) || value == "???"))
                {
                    ignored++;
                    continue;
                }
                
                commandKeys.Add(key);
                commandValues.Add(value);
            }


            for (int i = 0; i < commandKeys.Count; i++)
            {
                string Key = commandKeys[i];
                string Value = commandValues[i];
                tooltipBuilder.Append(Key);
                tooltipBuilder.Append(" - ");
                tooltipBuilder.AppendLine(SingleLinePreview(Value));
            }

            if (commandValues.Count >= maximum)
            {
                tooltipBuilder.AppendLine($"+{ignored} more...");
                tooltipBuilder.AppendLine();
                tooltipBuilder.AppendLine("For all format see the Wiki:\nTools->Build Uploader->Welcome->Documentation");
            }
            
            return tooltipBuilder.ToString();
        }

        private static string SingleLinePreview(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            const int maximumLength = 60;

            StringBuilder builder = new StringBuilder(Math.Min(value.Length, maximumLength));
            bool lastWasWhitespace = false;
            foreach (char c in value)
            {
                bool isWhitespace = char.IsWhiteSpace(c);
                if (isWhitespace && lastWasWhitespace)
                {
                    continue;
                }

                builder.Append(isWhitespace ? ' ' : c);
                lastWasWhitespace = isWhitespace;

                if (builder.Length >= maximumLength)
                {
                    builder.Append("...");
                    break;
                }
            }

            return builder.ToString();
        }

        public static bool FormatStringTextField(ref string text, ref bool pressed, Context ctx, GUILayoutOption textFieldOption)
        {
            return FormatStringTextField(ref text, ref pressed, ctx, null, textFieldOption);
        }
        
        public static bool FormatStringTextField(ref string text, ref bool pressed, Context ctx, GUIStyle style = null, GUILayoutOption textFieldOption = null)
        {
            return FormatStringText(ref text, ref pressed, style, textFieldOption, true, ctx);
        }

        public static bool FormatStringTextArea(ref string text, ref bool pressed, GUILayoutOption textFieldOption)
        {
            return FormatStringTextField(ref text, ref pressed, null, textFieldOption);
        }
        
        public static bool FormatStringTextArea(ref string text, ref bool pressed, Context ctx, GUIStyle style = null, GUILayoutOption textFieldOption = null)
        {
            return FormatStringText(ref text, ref pressed, style, textFieldOption, false, ctx);
        }
        
        private static bool FormatStringText(ref string text, ref bool pressed, GUIStyle style, GUILayoutOption textFieldOption, bool textField, Context ctx)
        {
            if (style == null)
            {
                style = EditorStyles.textField;
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent content = new GUIContent("F", GetFormatStringTextFieldTooltip(ctx));

                GUIStyle guiStyle = "ToolbarButton";
                var newPressed = GUILayout.Toggle(pressed, content, guiStyle, GUILayout.Width(20), GUILayout.Height(20));
                if (newPressed != pressed)
                {
                    pressed = newPressed;
                    GUI.FocusControl(null); // Deselect the text field so we can see the formatted text
                }

                if (pressed)
                {
                    // Disabled formatted-preview: show the resolved value, not editable, no dropdown.
                    using (new EditorGUI.DisabledScope(true))
                    {
                        string displayText = ctx.FormatString(text);
                        if (textFieldOption == null)
                        {
                            if (textField)
                                EditorGUILayout.TextField(displayText, style);
                            else
                                EditorGUILayout.TextArea(displayText, style);
                        }
                        else
                        {
                            if (textField)
                                EditorGUILayout.TextField(displayText, style, textFieldOption);
                            else
                                EditorGUILayout.TextArea(displayText, style, textFieldOption);
                        }
                    }
                }
                else
                {
                    // Editable: route through the autocomplete dropdown pool.
                    int id = GUIUtility.GetControlID(FocusType.Keyboard);
                    string newText = FormatStringFieldDropdowns.Draw(id, text, ctx, textField, style,
                        textFieldOption == null ? Array.Empty<GUILayoutOption>() : new[] { textFieldOption });

                    if (newText != text)
                    {
                        text = newText;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool DrawUploadProfileDropdown(ref UploadProfileMeta selectedProfile, List<UploadProfileMeta> profiles, Context ctx)
        {
            List<string> profileNames = new List<string>();
            profileNames.Add("-- Select Upload Profile --");
                    
            profileNames.AddRange(profiles.Select(p => ctx.FormatString(p.ProfileName)));
            for (int i = 1; i < profileNames.Count; i++)
            {
                profileNames[i] = $"{i}. {profileNames[i]}";
            }

            int selectedIndex = 0;
            if (selectedProfile != null)
            {
                string guid = selectedProfile.GUID;
                selectedIndex = profiles.FindIndex(a => a.GUID == guid);
                if (selectedIndex != -1)
                {
                    selectedIndex++;
                }
            }

            var newSelectedIndex = EditorGUILayout.Popup(selectedIndex, profileNames.ToArray(), GUILayout.Width(150));
            if (newSelectedIndex == selectedIndex)
            {
                return false;
            }

            if (newSelectedIndex <= 0)
            {
                selectedProfile = null;
            }
            else
            {
                selectedProfile = profiles[newSelectedIndex - 1];
            }

            return true;
        }

        public static void DrawPopup<T>(List<T> selected, List<T> allOptions, string emptySelection, Action<List<T>> callback, params GUILayoutOption[] options) where T : DropdownElement
        {
            DrawPopup(selected, allOptions, emptySelection, a => a.Id + ". " + a.DisplayName, callback, options);
        }

        public static void DrawPopup<T>(List<T> selected, List<T> allOptions, string emptySelection, Func<T, string> menuLabel, Action<List<T>> callback, params GUILayoutOption[] options) where T : DropdownElement
        {
            // TODO: Replace this with the actual popup with more lists/array shit?
            string buttonText = selected.Count == 0 ? emptySelection : string.Join(",", selected.Select(menuLabel));
            GUIStyle style = new GUIStyle(EditorStyles.popup);
            Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), style, options);
            if (GUI.Button(buttonRect, buttonText, style)) 
            {
                List<T> m_channels = new List<T>(selected);
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Clear"), selected.Count == 0, () =>
                {
                    m_channels.Clear();
                    callback(m_channels);
                });
                
                menu.AddSeparator(string.Empty);

                if (allOptions != null)
                {
                    foreach (T channel in allOptions.OrderBy(a => a.DisplayName))
                    {
                        bool isSelected = selected.Contains(channel);
                        menu.AddItem(new GUIContent(menuLabel(channel)), isSelected, () =>
                        {
                            if (isSelected)
                            {
                                m_channels.Remove(channel);
                            }
                            else
                            {
                                m_channels.Add(channel);
                                m_channels.Sort((a, b) => a.DisplayName.CompareTo(b.DisplayName));
                            }

                            callback(m_channels);
                        });
                    }
                }

                Rect rect = buttonRect;
                // rect.y += rect.height;
                menu.DropDown(rect);
            }
        }

        public static void DrawEnumPopup<T>(List<T> selected, string emptySelection, Action<List<T>> callback, params GUILayoutOption[] options) where T : Enum
        {
            string buttonText = selected.Count == 0 ? emptySelection : string.Join(",", selected.Select(a=>a.ToString()));
            GUIStyle style = new GUIStyle(EditorStyles.popup);
            Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), style, options);
            if (GUI.Button(buttonRect, buttonText, style)) 
            {
                List<T> m_channels = new List<T>(selected);
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Clear"), selected.Count == 0, () =>
                {
                    m_channels.Clear();
                    callback(m_channels);
                });
                
                menu.AddSeparator(string.Empty);

                var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
                foreach (T channel in values)
                {
                    bool isSelected = selected.Contains(channel);
                    menu.AddItem(new GUIContent(channel.ToString()), isSelected, () =>
                    {
                        if (isSelected)
                        {
                            m_channels.Remove(channel);
                        }
                        else
                        {
                            m_channels.Add(channel);
                            m_channels.Sort((a, b) => Array.IndexOf(values, a) - Array.IndexOf(values, b));
                        }

                        callback(m_channels);
                    });
                }
                
                Rect rect = buttonRect;
                // rect.y += rect.height;
                menu.DropDown(rect);
            }
        }
    }
}
