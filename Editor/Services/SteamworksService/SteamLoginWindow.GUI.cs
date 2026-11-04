using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal partial class SteamLoginWindow
    {
        private void OnGUI()
        {
            // Text fields keep the Return key, so check for it before drawing them
            Event current = Event.current;
            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && CanLogIn)
            {
                current.Use();
                LogIn();
            }

            EditorGUILayout.LabelField("Steam Username", MaskUserName(SteamSDK.UserName));
            EditorGUILayout.HelpBox("SteamSDK will remember your login for future sessions.", MessageType.None);

            using (new EditorGUI.DisabledScope(m_loggingIn))
            {
                GUI.SetNextControlName(PasswordControl);
                m_password = EditorGUILayout.PasswordField("Password", m_password);

                GUI.SetNextControlName(SteamGuardCodeControl);
                m_steamGuardCode = EditorGUILayout.TextField(new GUIContent("Steam Guard Code",
                    "Only needed when Steam asks for one. It's in the email Steam sends, or in the Steam Mobile app."), m_steamGuardCode);
            }

            if (m_focusControl != null && current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(m_focusControl);
                m_focusControl = null;
            }

            if (!string.IsNullOrEmpty(m_message))
            {
                EditorGUILayout.HelpBox(m_message, m_messageType);
            }

            GUILayout.FlexibleSpace();
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    Close();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(!CanLogIn))
                {
                    if (GUILayout.Button(m_loggingIn ? "Logging In..." : "Log In", GUILayout.Width(100)))
                    {
                        LogIn();
                    }
                }
            }
        }
    }
}
