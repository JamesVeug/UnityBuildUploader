using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class GooglePlayDestination
    {
        private bool m_showFormattedReleaseStatus = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedReleaseName = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedReleaseNotes = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedBinaryFileName = Preferences.DefaultShowFormattedTextToggle;

        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= GoogleUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(120));
            isDirty |= GoogleUIUtils.PlayAppPopup.DrawPopup(ref m_playApp, m_context, GUILayout.Width(120));

            GooglePlayTrack newTrack = (GooglePlayTrack)EditorGUILayout.EnumPopup(m_track, GUILayout.Width(100));
            if (newTrack != m_track)
            {
                m_track = newTrack;
                isDirty = true;
            }

            float remaining = maxWidth - (120 * 2) - 100;
            using (new EditorGUI.DisabledScope(true))
            {
                bool alwaysFormatted = true;
                EditorUtils.FormatStringTextArea(ref m_releaseNameFormat, ref alwaysFormatted, m_context, null, GUILayout.Width(remaining));
            }
        }

        protected internal override void OnGUIExpanded(ref bool isDirty)
        {
            if (GUILayout.Button("?", GUILayout.Width(20)))
            {
                Application.OpenURL("https://developers.google.com/android-publisher/api-ref/rest");
            }

            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Dry Run:",
                    "Open an edit to check the credentials and package name, then delete it without uploading.");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_dryRun);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("App:", GUILayout.Width(120));
                isDirty |= GoogleUIUtils.AppPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(200));
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Play App:", GUILayout.Width(120));
                isDirty |= GoogleUIUtils.PlayAppPopup.DrawPopup(ref m_playApp, m_context, GUILayout.Width(200));
                if (m_playApp != null && !string.IsNullOrEmpty(m_playApp.PackageName))
                {
                    GUILayout.Label($"({m_playApp.PackageName})", EditorStyles.miniLabel);
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Track:", GUILayout.Width(120));
                GooglePlayTrack newTrack = (GooglePlayTrack)EditorGUILayout.EnumPopup(m_track, GUILayout.Width(200));
                if (newTrack != m_track)
                {
                    m_track = newTrack;
                    isDirty = true;
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Release Status:", "completed / draft / halted / inProgress");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextField(ref m_releaseStatusFormat, ref m_showFormattedReleaseStatus, m_context);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Release Name:", GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextField(ref m_releaseNameFormat, ref m_showFormattedReleaseName, m_context);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Release Notes:", GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextArea(ref m_releaseNotesFormat, ref m_showFormattedReleaseNotes, m_context);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Binary File Name:",
                    "When the source is a folder, name the .aab/.apk to upload. Leave empty to auto-detect.");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextField(ref m_binaryFileName, ref m_showFormattedBinaryFileName, m_context);
            }
        }

        public override string Summary()
        {
            string app = m_playApp != null ? m_playApp.PackageName : "<no app>";
            return $"Google Play: {app} → {GooglePlay.TrackName(m_track)}";
        }
    }
}
