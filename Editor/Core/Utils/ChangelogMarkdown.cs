using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Reads and draws the markdown used by CHANGELOG.md and the GitHub release notes.
    /// </summary>
    internal static class ChangelogMarkdown
    {
        internal class Entry
        {
            public string Title;
            public bool FoldoutOpen;
            public string[] Lines;

            public Entry(string title, string[] lines)
            {
                Title = title;
                Lines = lines;
                FoldoutOpen = false;
            }
        }

        private static GUIStyle s_headerStyle;
        private static GUIStyle s_lineStyle;
        private static GUIStyle s_subHeaderStyle;
        private static GUIStyle s_wrappedLineStyle;
        private static GUIStyle s_wrappedSubHeaderStyle;
        private static bool s_stylesDarkMode;

        /// <summary>
        /// One entry per "# version" header, in the order they are written.
        /// </summary>
        public static List<Entry> Parse(string markdown)
        {
            string[] lines = (markdown ?? "").Split('\n');

            // group by any that start with '# '
            List<Entry> entries = new List<Entry>();
            int headerIndex = -1;
            for (int i = 0; i <= lines.Length; i++)
            {
                // Going one past the last line so the final version in the file is added too.
                bool endOfFile = i == lines.Length;
                if (!endOfFile && !lines[i].StartsWith("# "))
                {
                    continue;
                }

                if (headerIndex >= 0)
                {
                    string title = "v" + lines[headerIndex].Substring(1).Trim();
                    entries.Add(new Entry(title, GetEntryLines(lines, headerIndex + 1, i - 1)));
                }

                headerIndex = i;
            }

            return entries;
        }

        /// <summary>
        /// The lines of a release's notes, with Windows line endings removed.
        /// </summary>
        public static string[] SplitLines(string markdown)
        {
            string[] lines = (markdown ?? "").Replace("\r\n", "\n").Split('\n');
            return GetEntryLines(lines, 0, lines.Length - 1);
        }

        /// <summary>
        /// All lines of a version with any blank lines padding the start and end removed.
        /// </summary>
        public static string[] GetEntryLines(string[] lines, int start, int end)
        {
            while (start <= end && lines[start].Trim().Length == 0)
            {
                start++;
            }

            while (end >= start && lines[end].Trim().Length == 0)
            {
                end--;
            }

            List<string> entryLines = new List<string>();
            for (int i = start; i <= end; i++)
            {
                entryLines.Add(lines[i]);
            }

            return entryLines.ToArray();
        }

        private static void CreateStyles()
        {
            if (s_lineStyle != null && s_stylesDarkMode == Utils.IsDarkMode)
            {
                return;
            }

            s_stylesDarkMode = Utils.IsDarkMode;

            s_headerStyle = new GUIStyle(GUI.skin.label);
            s_headerStyle.wordWrap = true;
            s_headerStyle.alignment = TextAnchor.MiddleCenter;
            s_headerStyle.fontStyle = FontStyle.Bold;
            s_headerStyle.fontSize = 24;

            s_lineStyle = new GUIStyle(GUI.skin.label);
            s_lineStyle.richText = true;

            s_subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            s_subHeaderStyle.richText = true;
            s_subHeaderStyle.fontSize = 16;

            s_wrappedLineStyle = new GUIStyle(s_lineStyle);
            s_wrappedLineStyle.wordWrap = true;

            s_wrappedSubHeaderStyle = new GUIStyle(s_subHeaderStyle);
            s_wrappedSubHeaderStyle.wordWrap = true;
        }

        /// <param name="wordWrap">Wrap long lines, for narrow windows. Leaves room for scroll bars and boxes when
        /// working out each line's height.</param>
        public static void Draw(string[] lines, bool wordWrap = false)
        {
            CreateStyles();
            GUIStyle lineStyle = wordWrap ? s_wrappedLineStyle : s_lineStyle;
            GUIStyle subHeaderStyle = wordWrap ? s_wrappedSubHeaderStyle : s_subHeaderStyle;
            float width = wordWrap ? EditorGUIUtility.currentViewWidth - 60 : EditorGUIUtility.currentViewWidth;

            // Draw allText as markdown
            // # is header
            // - bullet point
            for (int i = 0; i < lines.Length; i++)
            {
                GUIStyle style = lineStyle;

                // Get text and style based on the line content
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    line = "";
                }
                else if (line.StartsWith("##"))
                {
                    // Sub-Header
                    line = line.Substring(2).Trim();
                    style = subHeaderStyle;
                }
                else if (line.StartsWith("#"))
                {
                    // Header
                    line = line.Substring(1).Trim();
                    style = s_headerStyle;
                }
                else if (line.Trim().StartsWith("-"))
                {
                    // Bullet point
                    int indents = Mathf.CeilToInt(line.IndexOf('-') / 2f);

                    int artificialIndent = (indents + 1) * 10;
                    line = new string(' ', artificialIndent) + line.Trim().Substring(1).Trim();
                    // line = line.Substring(1).Trim();
                    // GUILayout.Label($"- {bulletText}");
                }

                // replace **XXXX** with <b>XXXX</b>
                int boldStartIndex = line.IndexOf("**");
                while (boldStartIndex != -1)
                {
                    int boldEndIndex = line.IndexOf("**", boldStartIndex + 2);
                    if (boldEndIndex == -1)
                    {
                        break; // No closing bold found
                    }

                    string boldText = line.Substring(boldStartIndex + 2, boldEndIndex - boldStartIndex - 2);
                    string coloredBoldText = $"<b>{boldText}</b>";
                    line = line.Replace($"**{boldText}**", coloredBoldText);

                    boldStartIndex = line.IndexOf("**", boldEndIndex + 2);
                }


                // replace `XXXX` with colorization
                int startIndex = line.IndexOf('`');
                while (startIndex != -1)
                {
                    int endIndex = line.IndexOf('`', startIndex + 1);
                    if (endIndex == -1)
                    {
                        break; // No closing backtick found
                    }

                    string codeSnippet = line.Substring(startIndex + 1, endIndex - startIndex - 1);
                    string color = Utils.IsDarkMode ? "#FFFB00" : "#7F7900";
                    string coloredSnippet = $"<color={color}>{codeSnippet}</color>";
                    line = line.Replace($"`{codeSnippet}`", coloredSnippet);

                    startIndex = line.IndexOf('`', endIndex + 1);
                }

                // replace [docs](XXX) with a button
                int linkStartIndex = line.IndexOf('[');
                if (linkStartIndex != -1)
                {
                    int linkEndIndex = line.IndexOf(']', linkStartIndex + 1);
                    if (linkEndIndex > -1)
                    {

                        int urlStartIndex = line.IndexOf('(', linkEndIndex + 1);
                        if (urlStartIndex > -1)
                        {

                            int urlEndIndex = line.IndexOf(')', urlStartIndex + 1);
                            if (urlEndIndex > -1)
                            {

                                string linkText = line.Substring(linkStartIndex + 1, linkEndIndex - linkStartIndex - 1);
                                string url = line.Substring(urlStartIndex + 1, urlEndIndex - urlStartIndex - 1);

                                line = line.Replace($"[{linkText}]({url})",
                                    $"<b><color=blue><u>{linkText}</u></color></b>");
                                if (GUILayout.Button(line, style))
                                {
                                    Application.OpenURL(url);
                                }
                                continue;
                            }
                        }
                    }
                }


                // Selectable so it can be copied but not editable like a text field looks.
                float height = style.CalcHeight(new GUIContent(line), width);
                EditorGUILayout.SelectableLabel(line, style, GUILayout.Height(height));
            }
        }
    }
}
