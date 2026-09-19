using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class QuickUploadPopup : EditorWindow
    {
        private string m_inputText;
        private bool m_showFormattedDescription;
        private bool m_descriptionFoldoutCollapsed;
            
        private UploadProfileMeta m_profile;
        private UploadTask m_task;
        private static List<UploadProfileMeta> m_unloadedUploadProfiles;

        public static void ShowWindow(UploadProfileMeta profile)
        {
            QuickUploadPopup window = GetWindow<QuickUploadPopup>(true, "Quick Upload", true);
            window.titleContent = new GUIContent("Quick Upload");
            
            int width = 300;
            int height = 300;
            Vector2 center = new Vector2(Screen.currentResolution.width * 0.5f, Screen.currentResolution.height * 0.5f);
            window.position = new Rect(
                center.x - width/2f,
                center.y - height/2f,
                width, height
            );
            window.m_inputText = Preferences.DefaultDescriptionFormat;
            window.m_showFormattedDescription = Preferences.DefaultShowFormattedTextToggle;
            window.m_profile = profile;
            
            UploadProfile uploadProfile = UploadProfile.FromGUID(profile.GUID);
            window.m_task  = new UploadTask(uploadProfile);


            m_unloadedUploadProfiles = UploadProfileMeta.LoadFromProjectSettings();
            
            // window.ShowModalUtility();
        }
        
        void OnEnable()
        {
            Focus();
        }

        void OnGUI()
        {
            FormatStringFieldDropdowns.BeginHost(this);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Upload Profile:");
                if (EditorUtils.DrawUploadProfileDropdown(ref m_profile, m_unloadedUploadProfiles, m_task.Context))
                {
                    UploadProfile uploadProfile = UploadProfile.FromPath(m_profile.FilePath);
                    m_task = new UploadTask(uploadProfile);
                }
            }
            
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (CustomFoldoutButton.OnGUI(m_descriptionFoldoutCollapsed))
                {
                    m_descriptionFoldoutCollapsed = !m_descriptionFoldoutCollapsed;
                }
                
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleLeft;
                GUILayout.Label("Description:", style);
            }

            if (m_descriptionFoldoutCollapsed)
            {
                EditorUtils.FormatStringTextField(ref m_inputText, ref m_showFormattedDescription, m_task.Context);
            }
            else
            {
                EditorUtils.FormatStringTextArea(ref m_inputText, ref m_showFormattedDescription, m_task.Context, null, GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.Space(50);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
                
                if (GUILayout.Button("Run All"))
                {
                    Upload();
                    Close();
                }
            }

            FormatStringFieldDropdowns.EndHost(this);
        }

        public void Upload()
        {
            m_task.SetBuildDescription(m_inputText);
            _ = m_task.StartAsync();
        }
    }
}