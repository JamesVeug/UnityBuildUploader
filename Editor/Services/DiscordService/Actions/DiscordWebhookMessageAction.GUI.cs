using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class DiscordWebhookMessageAction
    {
        private bool m_showFormattedText = Preferences.DefaultShowFormattedTextToggle;
        private ReorderableListOfDiscordMessageEmbeds m_embedList;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= DiscordUIUtils.WebhookPopup.DrawPopup(ref m_webhook, m_context, GUILayout.Width(120));

            float width = maxWidth - 125;
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
                GUILayout.Label("Webhook:", GUILayout.Width(120));
                isDirty |= DiscordUIUtils.WebhookPopup.DrawPopup(ref m_webhook, m_context, GUILayout.Width(120));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Username:", "Overrides the webhook's name for this message. Leave empty to use the webhook's name."), GUILayout.Width(120));
                string username = EditorGUILayout.TextField(m_username);
                if (username != m_username)
                {
                    m_username = username;
                    isDirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Avatar URL:", "Overrides the webhook's avatar for this message. Leave empty to use the webhook's avatar."), GUILayout.Width(120));
                string avatarURL = EditorGUILayout.TextField(m_avatarURL);
                if (avatarURL != m_avatarURL)
                {
                    m_avatarURL = avatarURL;
                    isDirty = true;
                }
            }

            GUILayout.Label("Text:", GUILayout.Width(50));
            if (EditorUtils.FormatStringTextArea(ref m_text, ref m_showFormattedText, m_context))
            {
                isDirty = true;
            }

            if (m_embedList == null)
            {
                m_embedList = new ReorderableListOfDiscordMessageEmbeds();
                m_embedList.Initialize(m_embeds, "Embeds", m_embeds.Count <= 1, (_) => { });
            }

            if (m_embedList.OnGUI())
            {
                isDirty = true;
            }
        }
    }
}
