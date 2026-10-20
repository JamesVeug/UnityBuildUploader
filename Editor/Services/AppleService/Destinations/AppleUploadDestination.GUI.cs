using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class AppleUploadDestination
    {
        private bool m_showFormattedBuildVersion = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedBuildNumber = Preferences.DefaultShowFormattedTextToggle;

        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= AppleUIUtils.ApiKeyPopup.DrawPopup(ref m_apiKey, m_context, GUILayout.Width(150));
            isDirty |= AppleUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(150));

            string platformText = m_app != null ? m_app.Platform.ToString() : "";
            GUILayout.Label(platformText, GUILayout.Width(80));
        }

        protected internal override void OnGUIExpanded(ref bool isDirty)
        {
            if (GUILayout.Button("?", GUILayout.Width(20)))
            {
                Application.OpenURL("https://developer.apple.com/documentation/appstoreconnectapi");
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Dry Run:",
                        "Check the API Key and App with App Store Connect without uploading the .ipa."),
                    GUILayout.Width(140));
                isDirty |= CustomToggle.DrawToggle(ref m_dryRun);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("API Key:", GUILayout.Width(140));
                isDirty |= AppleUIUtils.ApiKeyPopup.DrawPopup(ref m_apiKey, m_context, GUILayout.Width(200));
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("App:", GUILayout.Width(140));
                isDirty |= AppleUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(200));
            }

            if (m_app != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Platform:", GUILayout.Width(140));
                        EditorGUILayout.EnumPopup(m_app.Platform, GUILayout.Width(200));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Bundle ID:", GUILayout.Width(140));
                        EditorGUILayout.TextField(m_app.BundleID, GUILayout.Width(300));
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Build Version:",
                        "CFBundleShortVersionString used to match the uploaded build via App Store Connect REST."),
                    GUILayout.Width(140));
                isDirty |= EditorUtils.FormatStringTextArea(ref m_buildVersionFormat, ref m_showFormattedBuildVersion, m_context);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Build Number:",
                        "CFBundleVersion used to match the uploaded build via App Store Connect REST."),
                    GUILayout.Width(140));
                isDirty |= EditorUtils.FormatStringTextArea(ref m_buildNumberFormat, ref m_showFormattedBuildNumber, m_context);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Find Build Timeout (s):",
                        "How long to wait for the build to appear in App Store Connect after altool completes."),
                    GUILayout.Width(160));
                int newTimeout = EditorGUILayout.IntField(m_findBuildTimeoutSeconds, GUILayout.Width(100));
                if (newTimeout != m_findBuildTimeoutSeconds)
                {
                    m_findBuildTimeoutSeconds = newTimeout;
                    isDirty = true;
                }
            }
        }
    }
}
