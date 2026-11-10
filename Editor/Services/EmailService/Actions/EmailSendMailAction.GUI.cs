using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class EmailSendMailAction
    {
        private bool m_showFormattedTo = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedSubject = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedBody = Preferences.DefaultShowFormattedTextToggle;

        private ReorderableListOfEmailRecipients m_ccList;
        private ReorderableListOfEmailRecipients m_bccList;
        private ReorderableListOfEmailAttachments m_attachmentList;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= EmailUIUtils.AccountPopup.DrawPopup(ref m_account, m_context, GUILayout.Width(120));

            float remaining = Mathf.Max(60f, (maxWidth - 120f - 20f) / 3f);
            using (new EditorGUI.DisabledScope(true))
            {
                bool alwaysFormatted = true;
                EditorUtils.FormatStringTextArea(ref m_to, ref alwaysFormatted, m_context, null, GUILayout.Width(remaining));
                EditorUtils.FormatStringTextArea(ref m_subject, ref alwaysFormatted, m_context, null, GUILayout.Width(remaining));
                EditorUtils.FormatStringTextArea(ref m_body, ref alwaysFormatted, m_context, null, GUILayout.Width(remaining));
            }
        }

        public override void OnGUIExpanded(ref bool isDirty)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Account:", GUILayout.Width(60));
                isDirty |= EmailUIUtils.AccountPopup.DrawPopup(ref m_account, m_context, GUILayout.Width(160));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("To:", GUILayout.Width(60));
                if (EditorUtils.FormatStringTextArea(ref m_to, ref m_showFormattedTo, m_context))
                {
                    isDirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Subject:", GUILayout.Width(60));
                if (EditorUtils.FormatStringTextArea(ref m_subject, ref m_showFormattedSubject, m_context))
                {
                    isDirty = true;
                }
            }

            GUILayout.Label("Body:");
            if (EditorUtils.FormatStringTextArea(ref m_body, ref m_showFormattedBody, m_context))
            {
                isDirty = true;
            }

            if (m_ccList == null)
            {
                m_ccList = new ReorderableListOfEmailRecipients(m_context);
                m_ccList.Initialize(m_ccEmails, "CC", m_ccEmails.Count > 0);
            }
            if (m_ccList.OnGUI())
            {
                isDirty = true;
            }

            if (m_bccList == null)
            {
                m_bccList = new ReorderableListOfEmailRecipients(m_context);
                m_bccList.Initialize(m_bccEmails, "BCC", m_bccEmails.Count > 0);
            }
            if (m_bccList.OnGUI())
            {
                isDirty = true;
            }

            if (m_attachmentList == null)
            {
                m_attachmentList = new ReorderableListOfEmailAttachments(m_context);
                m_attachmentList.Initialize(m_attachments, "Attachments", m_attachments.Count > 0);
            }
            if (m_attachmentList.OnGUI())
            {
                isDirty = true;
            }
        }
    }
}
