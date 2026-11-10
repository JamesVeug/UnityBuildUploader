using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class DiscordMessageChannelAction
    {
        private bool m_showFormattedText = Preferences.DefaultShowFormattedTextToggle;
        private ReorderableListOfDiscordMessageEmbeds m_embedList;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= DiscordUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
            isDirty |= DiscordUIUtils.ServerPopup.DrawPopup(ref m_server, m_context, GUILayout.Width(120));
            isDirty |= DiscordUIUtils.ChannelPopup.DrawPopup(m_server, ref m_channel, m_context, GUILayout.Width(120));

            float width = maxWidth - 375;
            string formattedText = m_context.FormatString(m_text);
            string truncated = Utils.TruncateText(formattedText, width, "");
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.TextField(truncated, GUILayout.Width(width));
            }
        }

        public override void OnGUIExpanded(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Dry Run:", GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_dryRun);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("App:", GUILayout.Width(120));
                isDirty |= DiscordUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Server:", GUILayout.Width(120));
                isDirty |= DiscordUIUtils.ServerPopup.DrawPopup(ref m_server, m_context, GUILayout.Width(120));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Channel:", GUILayout.Width(120));
                // Draw the channel popup
                if (m_server == null)
                {
                    GUILayout.Label("No server selected", GUILayout.Width(120));
                }
                else
                {
                    isDirty |= DiscordUIUtils.ChannelPopup.DrawPopup(m_server, ref m_channel, m_context, GUILayout.Width(120));
                }
            }

            
            GUILayout.Label("Text:", GUILayout.Width(50));
            if(EditorUtils.FormatStringTextArea(ref m_text, ref m_showFormattedText, m_context))
            {
                isDirty = true;
            }

            if (m_embedList == null)
            {
                m_embedList = new ReorderableListOfDiscordMessageEmbeds(m_context);
                m_embedList.Initialize(m_embeds, "Embeds", m_embeds.Count <= 1, (_) =>
                {
                    
                });
            }

            if (m_embedList.OnGUI())
            {
                isDirty = true;
            }
            
            
        }
    }
}
