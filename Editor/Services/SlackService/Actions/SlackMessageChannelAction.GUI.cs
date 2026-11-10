using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class SlackMessageChannelAction
    {
        private bool m_showFormattedText = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedTSFormat = Preferences.DefaultShowFormattedTextToggle;
        private ReorderableListOfSlackMessageAttachments m_attachmentList;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= SlackUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
            isDirty |= SlackUIUtils.ServerPopup.DrawPopup(ref m_server, m_context, GUILayout.Width(120));
            isDirty |= SlackUIUtils.ChannelPopup.DrawPopup(m_server, ref m_channel, m_context, GUILayout.Width(120));

            float width = maxWidth - (120 * 3);
            string formattedText = m_context.FormatString(m_text);
            string truncated = Utils.TruncateText(formattedText, width, "");
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.TextField(truncated, GUILayout.Width(width));
            }
        }

        public override void OnGUIExpanded(ref bool isDirty)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("App:", GUILayout.Width(60));
                isDirty |= SlackUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
                
                GUILayout.Label("Server:", GUILayout.Width(60));
                isDirty |= SlackUIUtils.ServerPopup.DrawPopup(ref m_server, m_context, GUILayout.Width(120));
                
                GUILayout.Label("Channel:", GUILayout.Width(60));
                // Draw the channel popup
                if (m_server == null)
                {
                    GUILayout.Label("No server selected", GUILayout.Width(120));
                }
                else
                {
                    isDirty |= SlackUIUtils.ChannelPopup.DrawPopup(m_server, ref m_channel, m_context, GUILayout.Width(120));
                }
            }
            var tsFormat = new GUIContent("Message ID Format", tsFormatTooltip);
            GUILayout.Label(tsFormat, GUILayout.Width(200));
            isDirty |= ContextGUI.DrawKey(m_responseTSFormat, ref m_showFormattedTSFormat, m_context);

            
            GUILayout.Label("Text:", GUILayout.Width(50));
            if (EditorUtils.FormatStringTextArea(ref m_text, ref m_showFormattedText, m_context))
            {
                isDirty = true;
            }

            if (m_attachmentList == null)
            {
                m_attachmentList = new ReorderableListOfSlackMessageAttachments(m_context);
                m_attachmentList.Initialize(m_attachments, "Attachments", m_attachments.Count <= 1, (_) =>
                {
                    
                });
            }

            if (m_attachmentList.OnGUI())
            {
                isDirty = true;
            }
            
            
        }
    }
}