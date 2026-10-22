#if UNITY_6000_0_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class BuildProfileSource
    {
        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= BuildProfileUIUtils.BuildProfilesPopup.DrawPopup(ref m_BuildConfig, m_context);

            bool newCleanBuild = GUILayout.Toggle(m_CleanBuild, "Clean Build");
            if (newCleanBuild != m_CleanBuild)
            {
                m_CleanBuild = newCleanBuild;
                isDirty = true;
            }

            DrawAddressablesCollapsed();
        }

        public override void OnGUIExpanded(ref bool isDirty, UploadConfig.SourceData data)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Build Profile:", GUILayout.Width(120));
                isDirty |= BuildProfileUIUtils.BuildProfilesPopup.DrawPopup(ref m_BuildConfig, m_context);
            }
            
            if(m_BuildConfig != null){
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.indentLevel++;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Target Platform:", "The platform to build for. Cannot be changed at this time.");
                        EditorGUILayout.LabelField(label, GUILayout.Width(150));

                        BuildUtils.DrawPlatformPopup(m_BuildConfig.GetTargetPlatform, m_BuildConfig.GetTargetPlatformSubTarget, m_BuildConfig.GetTarget);
                    }

                    if (m_BuildConfig.GetTargetPlatform == BuildTargetGroup.Standalone){
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Target Architecture:", "The Architecture version to build for. Cannot be changed at this time.");
                            EditorGUILayout.LabelField(label, GUILayout.Width(150));
                            BuildUtils.DrawArchitecturePopup(m_BuildConfig.GetTargetPlatform, m_BuildConfig.GetTarget, m_BuildConfig.GetTargetArchitecture);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                GUIContent cleanBuildContent = new GUIContent("Clean Build:", "If enabled, the build folder will be deleted before building. This ensures a fresh build but may increase build time.");
                GUILayout.Label(cleanBuildContent, GUILayout.Width(120));
                bool newCleanBuild = GUILayout.Toggle(m_CleanBuild, "");
                if (newCleanBuild != m_CleanBuild)
                {
                    m_CleanBuild = newCleanBuild;
                    isDirty = true;
                }
                
                string path = GetBuiltDirectory();
                using (new EditorGUI.DisabledScope(!System.IO.Directory.Exists(path)))
                {
                    if (GUILayout.Button("Open Build Folder", GUILayout.Width(120)))
                    {
                        EditorUtility.RevealInFinder(path);
                    }
                }
            }

            DrawAddressablesToggles(ref isDirty);
            
            if (GUILayout.Button("Apply to Editor", GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("Apply to Editor",
                        "Are you sure you want to apply settings to the editor?\n" +
                        "This will change your Player settings and Editor settings",
                        "Apply", "Cancel"))
                {
                    ApplyBuildConfig(GetBuildConfigToApply(), null);
                }
            }
        }

        public override string Summary()
        {
            string displayName = m_BuildConfig.DisplayName;
            
            BuildTarget target;
            BuildUtils.Architecture architecture;
            if (m_BuildConfig.GetSwitchTargetPlatform)
            {
                target = m_BuildConfig.GetTarget;
                architecture = m_BuildConfig.GetTargetArchitecture;
            }
            else
            {
                target = BuildUtils.CurrentTargetPlatform();
                architecture = BuildUtils.CurrentTargetArchitecture();
            }

            // Release Build (Windows x64)
            return $"{displayName} ({target} {architecture})";
        }
    }
}
#endif