using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class SlackUpdateMessageChannelAction
    {
        private const string tsFormatTooltip = "When a message to Slack message is sent we receive the id or TS of the message to reference it. If a formatName is provided then that TS can be used elsewhere in the Upload Task. eg: editing it as a post action. eg: SlackMessageID (NOTE: Do not include curly braces)";
        
        private bool m_showFormattedText = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showMessageTimeStamp = Preferences.DefaultShowFormattedTextToggle;
        private ReorderableListOfSlackMessageAttachments m_attachmentList;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= SlackUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
            isDirty |= SlackUIUtils.ServerPopup.DrawPopup(ref m_server, m_context, GUILayout.Width(120));
            isDirty |= SlackUIUtils.ChannelPopup.DrawPopup(m_server, ref m_channel, m_context, GUILayout.Width(120));

            float width = maxWidth - (120 * 3);
            using (new EditorGUI.DisabledScope(true))
            {
                bool alwaysFormatted = true;
                EditorUtils.FormatStringTextArea(ref m_text, ref alwaysFormatted, m_context, null, GUILayout.Width(width));
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
            
            var tsFormat = new GUIContent("Message ID Format:", tsFormatTooltip);
            GUILayout.Label(tsFormat, GUILayout.Width(200));
            if (EditorUtils.FormatStringTextArea(ref m_messageTimeStamp, ref m_showMessageTimeStamp, m_context))
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
            
            GUILayout.Label("Text:", GUILayout.Width(50));
            if (EditorUtils.FormatStringTextArea(ref m_text, ref m_showFormattedText, m_context))
            {
                isDirty = true;
            }

            if (m_attachmentList.OnGUI())
            {
                isDirty = true;
            }
            
            
        }
    }
}