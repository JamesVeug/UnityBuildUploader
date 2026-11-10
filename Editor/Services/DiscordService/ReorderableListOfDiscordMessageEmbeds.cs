using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class ReorderableListOfDiscordMessageEmbeds : InternalReorderableList<DiscordMessageChannelAction.Embed>
    {
        private Context m_context;
        private Dictionary<DiscordMessageChannelAction.Embed, bool> m_showFormattedTitles = new Dictionary<DiscordMessageChannelAction.Embed, bool>();
        private Dictionary<DiscordMessageChannelAction.Embed, bool> m_showFormattedDescriptions = new Dictionary<DiscordMessageChannelAction.Embed, bool>();

        public ReorderableListOfDiscordMessageEmbeds(Context context)
        {
            m_context = context;
        }

        protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DiscordMessageChannelAction.Embed element = list[index];

                // Title
                float labelWidth = 40;
                float toggleWidth = 20;
                float width = Mathf.Min(100, rect.width / 2);
                Rect rect1 = new Rect(rect.x, rect.y, labelWidth, rect.height);
                GUI.Label(rect1, new GUIContent("Title", "Embed title. Supports format tokens like {version}."));
                rect1.x += rect1.width;
                rect1.width = toggleWidth + width;
                dirty |= EditorUtils.FormatStringTextField(rect1, ref element.title, m_showFormattedTitles, element, m_context, "e.g. v{version}");

                // Description
                rect1.x += rect1.width;
                rect1.width = 40;
                GUI.Label(rect1, new GUIContent("Desc", "Embed description / body text. Supports format tokens."));
                rect1.x += rect1.width;
                rect1.width = toggleWidth + 200;
                dirty |= EditorUtils.FormatStringTextArea(rect1, ref element.description, m_showFormattedDescriptions, element, m_context);
                
                
                
                rect1.x += rect1.width;
                rect1.width = 40;
                GUI.Label(rect1, new GUIContent("Color", "Embed side-bar color as a hex value, e.g. #009000."));
                rect1.x += rect1.width;
                rect1.width = 70;

                string c = GUI.TextArea(rect1, element.color);
                if (c != element.color)
                {
                    element.color = c;
                    dirty = true;
                }
                
                Color newColor = Color.white;
                try
                {
                    string tC = element.color;
                    if (tC.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        tC = "#" + tC.Substring(2);
                    }
                    else if (!tC.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    {
                        tC = "#" + tC;
                    }
                    
                    if (!ColorUtility.TryParseHtmlString(tC, out newColor))
                    {
                        newColor = Color.white;
                    }
                }
                catch
                {
                    // ignored
                }

                rect1.x += rect1.width;
                rect1.width = 20;
                int pictureSize = (int)rect1.width * 10;
                Texture2D t2 = new Texture2D(pictureSize, pictureSize);
                for (int i = 0; i < pictureSize; i++)
                {
                    for (int j = 0; j < pictureSize; j++)
                    {
                        t2.SetPixel(i, j, newColor);
                    }
                }
                t2.Apply();
                if (GUI.Button(rect1, t2, GUI.skin.button))
                {
                    Application.OpenURL("https://www.w3schools.com/colors/colors_hexadecimal.asp");
                }
            }
        }

        protected override DiscordMessageChannelAction.Embed CreateItem(int index)
        {
            return new DiscordMessageChannelAction.Embed()
            {
                title = "v{version}",
                description = "Bug Fixes",
                color = "#009000"
            };
        }
        
        protected override int CompareTo(DiscordMessageChannelAction.Embed a, DiscordMessageChannelAction.Embed b)
        {
            return String.Compare(a.title, b.title, StringComparison.Ordinal);
        }
    }
}