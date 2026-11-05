using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Typing '{' opens the dropdown of all string format options
    ///
    /// call OnGUI(...) where the text area should appear,
    /// call DrawDropdown() once at the end of OnGUI
    /// </summary>
    public class FormatStringAutocompleteTextArea
    {
        private const int MaxSuggestions = 12;
        private const float RowHeight = 18f;
        private const float Padding = 3f;

        private static FieldInfo s_recycledEditorField;
        private static PropertyInfo s_recycledEditorProperty;
        private static bool s_recycledEditorLookedUp;

        private readonly string m_controlName;

        private List<Command> m_suggestions = new List<Command>();
        private int m_anchorIndex = -1;
        private int m_tokenEndIndex;
        private int m_selectedIndex;
        private Command m_pendingSelection;
        private Rect m_dropdownScreenRect;
        private Rect m_dropdownTopLevelRect;
        private bool m_swallowMouseUp;
        private bool m_swallowSubmitCharacter;
        private EditorWindow m_ownerWindow;

        private bool IsOpen => m_anchorIndex >= 0 && m_suggestions.Count > 0;
        public bool IsDropdownOpen => IsOpen;

        public FormatStringAutocompleteTextArea(string controlName)
        {
            m_controlName = controlName;
        }

        public string OnGUI(string text, Context ctx, params GUILayoutOption[] options)
        {
            return OnGUI(text, ctx, false, true, null, options);
        }

        /// <param name="singleLine">Draw a single-line TextField instead of a multi-line TextArea.</param>
        /// <param name="armed">When false the field is drawn as a plain editable field with no
        /// dropdown (used when the host doesn't pump HandleOverlayInput/DrawDropdown).</param>
        public string OnGUI(string text, Context ctx, bool singleLine, bool armed, GUIStyle style, GUILayoutOption[] options)
        {
            if (armed)
            {
                TrackOwnerWindow();
                HandleNavigationKeys();
            }

            GUI.SetNextControlName(m_controlName);
            string newText = DrawField(text, singleLine, style, options);
            Rect textAreaRect = GUILayoutUtility.GetLastRect();

            if (!armed)
            {
                if (IsOpen)
                {
                    Close();
                }

                return newText;
            }

            if (m_pendingSelection != null)
            {
                newText = InsertSelection(newText, m_pendingSelection);
                m_pendingSelection = null;
                Close();
                return newText;
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (GUI.GetNameOfFocusedControl() == m_controlName)
                {
                    UpdateSuggestions(newText, ctx, textAreaRect);
                }
                else
                {
                    Close();
                }
            }

            return newText;
        }

        private static string DrawField(string text, bool singleLine, GUIStyle style, GUILayoutOption[] options)
        {
            options = options ?? Array.Empty<GUILayoutOption>();
            if (style != null)
            {
                return singleLine
                    ? EditorGUILayout.TextField(text, style, options)
                    : EditorGUILayout.TextArea(text, style, options);
            }

            return singleLine
                ? EditorGUILayout.TextField(text, options)
                : EditorGUILayout.TextArea(text, options);
        }

        public void DrawDropdown()
        {
            if (!IsOpen || m_dropdownScreenRect.width <= 0f)
            {
                return;
            }

            Vector2 local = GUIUtility.ScreenToGUIPoint(new Vector2(m_dropdownScreenRect.x, m_dropdownScreenRect.y));
            Rect windowRect = new Rect(local.x, local.y, m_dropdownScreenRect.width, m_dropdownScreenRect.height);

            m_dropdownTopLevelRect = windowRect;

            bool pro = EditorGUIUtility.isProSkin;
            Color background = pro ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.87f, 0.87f, 0.87f);
            Color border = pro ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.5f, 0.5f, 0.5f);
            Color highlight = new Color(0.24f, 0.48f, 0.90f, 0.6f);

            EditorGUI.DrawRect(windowRect, background);
            DrawBorder(windowRect, border);

            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < m_suggestions.Count; i++)
            {
                Command command = m_suggestions[i];
                Rect rowRect = new Rect(windowRect.x + 1f, windowRect.y + Padding + i * RowHeight,
                    windowRect.width - 2f, RowHeight);

                if (i == m_selectedIndex || rowRect.Contains(mouse))
                {
                    EditorGUI.DrawRect(rowRect, highlight);
                }

                Rect labelRect = new Rect(rowRect.x + 4f, rowRect.y, rowRect.width - 4f, rowRect.height);
                GUI.Label(labelRect, new GUIContent(command.Key, command.Tooltip), EditorStyles.label);
            }
        }

        private static void DrawBorder(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
        }

        /// <summary>
        /// Keyboard events only ever reach the focused window, so a key event arriving while this
        /// field holds focus identifies the window the dropdown belongs to. Once known, the dropdown
        /// closes the moment focus moves to another window - tabbing away leaves the field's own
        /// focus check unchanged, so nothing else would ever take the overlay down.
        /// </summary>
        private void TrackOwnerWindow()
        {
            if (Event.current.isKey && GUI.GetNameOfFocusedControl() == m_controlName)
            {
                m_ownerWindow = EditorWindow.focusedWindow;
            }

            if (m_ownerWindow != null && EditorWindow.focusedWindow != m_ownerWindow)
            {
                m_ownerWindow = null;
                Close();
            }
        }

        /// <summary>
        /// IMGUI sends Enter and Tab as two KeyDown events: one with the keyCode, then one with only
        /// the character. Picking a suggestion uses the first, so the second is swallowed too, or the
        /// field reads it as Enter/Tab and ends editing, drops focus or inserts a newline.
        /// </summary>
        private void HandleNavigationKeys()
        {
            Event e = Event.current;
            if (m_swallowSubmitCharacter && e.type == EventType.KeyDown)
            {
                m_swallowSubmitCharacter = false;
                if (e.keyCode == KeyCode.None && IsSubmitCharacter(e.character))
                {
                    e.Use();
                    return;
                }
            }

            if (!IsOpen || GUI.GetNameOfFocusedControl() != m_controlName)
            {
                return;
            }

            if (e.type != EventType.KeyDown)
            {
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    m_selectedIndex = (m_selectedIndex + 1) % m_suggestions.Count;
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    m_selectedIndex = (m_selectedIndex - 1 + m_suggestions.Count) % m_suggestions.Count;
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Tab:
                    m_pendingSelection = m_suggestions[m_selectedIndex];
                    m_swallowSubmitCharacter = true;
                    e.Use();
                    break;
                case KeyCode.Escape:
                    Close();
                    e.Use();
                    break;
            }
        }

        /// <summary>
        /// Handles mouse interaction with the dropdown. Call once at the start of OnGUI
        /// </summary>
        public void HandleOverlayInput()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseUp && m_swallowMouseUp)
            {
                m_swallowMouseUp = false;
                e.Use();
                return;
            }

            if (!IsOpen || m_dropdownTopLevelRect.width <= 0f)
            {
                return;
            }

            if (e.type != EventType.MouseDown)
            {
                return;
            }

            if (!m_dropdownTopLevelRect.Contains(e.mousePosition))
            {
                // Clicking anywhere else dismisses the dropdown. The event is left unused so the
                // click still lands on whatever control it was aimed at, and a click back inside the
                // text area simply reopens the dropdown on the next repaint.
                Close();
                return;
            }

            if (e.button != 0)
            {
                return;
            }

            float relY = e.mousePosition.y - (m_dropdownTopLevelRect.y + Padding);
            int row = Mathf.FloorToInt(relY / RowHeight);
            if (row >= 0 && row < m_suggestions.Count)
            {
                m_pendingSelection = m_suggestions[row];
                m_selectedIndex = row;
            }

            m_swallowMouseUp = true;
            e.Use();
        }

        private void UpdateSuggestions(string text, Context ctx, Rect textAreaRect)
        {
            if (string.IsNullOrEmpty(text))
            {
                // No text (fields can hold a null string) means no '{' token to complete.
                Close();
                return;
            }

            TextEditor editor = GetActiveTextEditor();
            int cursor = editor != null ? Mathf.Clamp(editor.cursorIndex, 0, text.Length) : text.Length;

            int anchor = FindTokenAnchor(text, cursor);
            if (anchor < 0)
            {
                Close();
                return;
            }

            string rawSearch = text.Substring(anchor, cursor - anchor);   // includes leading '{', e.g. "{commit"
            string term = rawSearch.Length > 1 ? rawSearch.Substring(1) : "";

            List<Command> matches = EditorUtils.GetAllCommands(ctx)
                .Where(c => term.Length == 0 || c.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(c => c.Key.StartsWith(rawSearch, StringComparison.OrdinalIgnoreCase))
                .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Take(MaxSuggestions)
                .ToList();

            if (matches.Count == 0)
            {
                Close();
                return;
            }

            m_suggestions = matches;
            m_anchorIndex = anchor;
            m_tokenEndIndex = cursor;
            if (m_selectedIndex >= m_suggestions.Count)
            {
                m_selectedIndex = 0;
            }

            float width = Mathf.Max(240f, textAreaRect.width);
            float height = RowHeight * m_suggestions.Count + Padding * 2f;
            Vector2 screenPos = GUIUtility.GUIToScreenPoint(new Vector2(textAreaRect.x, textAreaRect.yMax));
            m_dropdownScreenRect = new Rect(screenPos.x, screenPos.y, width, height);
        }

        private static bool IsSubmitCharacter(char c)
        {
            return c == '\n' || c == '\r' || c == (char)3 || c == '\t';
        }

        private static int FindTokenAnchor(string text, int cursor)
        {
            int i = cursor - 1;
            while (i >= 0 && char.IsLetterOrDigit(text[i]))
            {
                i--;
            }

            if (i >= 0 && text[i] == '{')
            {
                return i;
            }

            return -1;
        }

        /// <summary>
        /// EditorGUI.s_RecycledEditor is a field in older editors and a property in Unity 6.
        /// </summary>
        internal static TextEditor GetActiveTextEditor()
        {
            if (!s_recycledEditorLookedUp)
            {
                s_recycledEditorLookedUp = true;
                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                s_recycledEditorField = typeof(EditorGUI).GetField("s_RecycledEditor", flags);
                if (s_recycledEditorField == null)
                {
                    s_recycledEditorProperty = typeof(EditorGUI).GetProperty("s_RecycledEditor", flags);
                }
            }

            if (s_recycledEditorField != null)
            {
                return s_recycledEditorField.GetValue(null) as TextEditor;
            }

            return s_recycledEditorProperty?.GetValue(null, null) as TextEditor;
        }

        private string InsertSelection(string text, Command command)
        {
            int anchor = Mathf.Clamp(m_anchorIndex, 0, text.Length);
            int end = Mathf.Clamp(m_tokenEndIndex, anchor, text.Length);
            string result = text.Substring(0, anchor) + command.Key + text.Substring(end);

            TextEditor editor = GetActiveTextEditor();
            if (editor != null)
            {
                int newCursor = anchor + command.Key.Length;
                editor.text = result;
                editor.cursorIndex = newCursor;
                editor.selectIndex = newCursor;
            }

            // No GUI.FocusControl here: a selection is applied during the event that picked it, which
            // is already used, and the name lookup fails then, so it set focus to nothing.
            return result;
        }

        /// <summary>
        /// Dismisses the dropdown from outside the widget, for when the field that owns it is no
        /// longer being drawn and so can no longer close itself.
        /// </summary>
        public void ForceClose()
        {
            m_ownerWindow = null;
            Close();
        }

        private void Close()
        {
            m_anchorIndex = -1;
            m_suggestions.Clear();
            m_selectedIndex = 0;
        }
    }
}
