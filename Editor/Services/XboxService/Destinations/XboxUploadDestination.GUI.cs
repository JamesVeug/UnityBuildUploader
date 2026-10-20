using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class XboxUploadDestination
    {
        private bool m_showFormattedSubmissionIdKey = Preferences.DefaultShowFormattedTextToggle;

        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= XboxUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(160));
        }

        protected internal override void OnGUIExpanded(ref bool isDirty)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("App:", GUILayout.Width(120));
                isDirty |= XboxUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(200));
            }

            if (m_submissionIdFormat != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Submission ID Key:",
                        submissionIdTooltip),
                        GUILayout.Width(120));

                    isDirty |= ContextGUI.DrawKey(m_submissionIdFormat, ref m_showFormattedSubmissionIdKey, m_context);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Wait for Cert:",
                    "Wait for a submission status after committing. Adds time to the pipeline run but " +
                    "confirms the package was accepted before proceeding."),
                    GUILayout.Width(120));

                bool newWait = EditorGUILayout.Toggle(m_waitForCertification);
                if (newWait != m_waitForCertification)
                {
                    m_waitForCertification = newWait;
                    isDirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Dry Run:",
                    "Upload the package, then delete the submission instead of committing it."),
                    GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_dryRun);
            }

            EditorGUILayout.HelpBox(
                "Requires an Azure AD app registration with the \"Microsoft Store\" API permission. " +
                "See Partner Center → Account settings → User management → Add Azure AD applications.",
                MessageType.Info);
        }
    }
}
