using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class ReorderableListOfSlackMessageAttachments : InternalReorderableList<SlackAttachment>
    {
        private Context m_context;
        private Dictionary<SlackAttachment, bool> m_showFormattedTexts = new Dictionary<SlackAttachment, bool>();

        public ReorderableListOfSlackMessageAttachments(Context context)
        {
            m_context = context;
        }

        protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                SlackAttachment element = list[index];

                // Message
                float labelWidth = 60;
                Rect rect1 = new Rect(rect.x, rect.y, labelWidth, rect.height);
                GUI.Label(rect1, new GUIContent("Message", "Attachment text shown in Slack. Supports format tokens and Slack markup."));
                rect1.x += rect1.width;

                rect1.width = 220;
                dirty |= EditorUtils.FormatStringTextArea(rect1, ref element.text, m_showFormattedTexts, element, m_context);
                
                
                
                rect1.x += rect1.width;
                rect1.width = 70;
                
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

        protected override SlackAttachment CreateItem(int index)
        {
            return new SlackAttachment()
            {
                text = ":white_check_mark: #123456 *Successfully* uploaded!",
                fallback = "",
                color = "#009000",
                attachment_type = "",
                callback_id = "",
                actions = new List<SlackAttachment.Action>()
            };
        }
        
        protected override int CompareTo(SlackAttachment a, SlackAttachment b)
        {
            return String.Compare(a.text, b.text, StringComparison.Ordinal);
        }
    }
}