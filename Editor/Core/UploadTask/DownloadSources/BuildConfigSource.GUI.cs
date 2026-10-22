using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class BuildConfigSource
    {
        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            isDirty |= BuildConfigsUIUtils.BuildConfigsPopup.DrawPopup(ref m_BuildConfig, m_context);

            bool useSourceSettings = m_OverrideSwitchTargetPlatform;
            using (new EditorGUI.DisabledScope(!useSourceSettings))
            {
                (BuildTargetGroup newTargetGroup, int newSubTarget, BuildTarget newTarget) result = BuildUtils.DrawPlatformPopup(ResultingTargetGroup(), ResultingTargetPlatformSubTarget(), ResultingTarget());
                if (useSourceSettings)
                {
                    if (result.newTargetGroup != m_TargetPlatform || result.newSubTarget != m_TargetPlatformSubTarget || result.newTarget != m_Target)
                    {
                        m_TargetPlatform = result.newTargetGroup;
                        m_TargetPlatformSubTarget = result.newSubTarget;
                        m_Target = result.newTarget;
                        isDirty = true;
                    }
                }
                
                BuildUtils.Architecture newArchitecture = BuildUtils.DrawArchitecturePopup(ResultingTargetGroup(), ResultingTarget(), ResultingArchitecture());
                if (useSourceSettings && newArchitecture != m_TargetArchitecture)
                {
                    m_TargetArchitecture = newArchitecture;
                    isDirty = true;
                }
            }

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
                GUILayout.Label("Build Config:", GUILayout.Width(120));
                isDirty |= BuildConfigsUIUtils.BuildConfigsPopup.DrawPopup(ref m_BuildConfig, m_context);
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Override Target Platform", "If enabled, the target platform specified below will be used instead of the one from the build config.");
                GUILayout.Label(label, GUILayout.Width(150));
                
                bool newOverride = GUILayout.Toggle(m_OverrideSwitchTargetPlatform, "");
                if (newOverride != m_OverrideSwitchTargetPlatform)
                {
                    m_OverrideSwitchTargetPlatform = newOverride;
                    isDirty = true;
                }
            }
            
            using (new EditorGUI.DisabledScope(!m_OverrideSwitchTargetPlatform))
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIContent label = new GUIContent("Target Platform:", "The platform to build for. This will be used if 'Override Platform' is enabled.");
                    EditorGUILayout.LabelField(label, GUILayout.Width(150));

                    (BuildTargetGroup buildTargetGroup, int newSubTarget, BuildTarget newTarget) = BuildUtils.DrawPlatformPopup(m_TargetPlatform, m_TargetPlatformSubTarget, m_Target);
                    if (buildTargetGroup != m_TargetPlatform || newSubTarget != m_TargetPlatformSubTarget || newTarget != m_Target)
                    {
                        m_TargetPlatform = buildTargetGroup;
                        m_TargetPlatformSubTarget = newSubTarget;
                        m_Target = newTarget;
                        isDirty = true;
                    }
                }

                if(m_TargetPlatform == BuildTargetGroup.Standalone){
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Target Architecture:", "The Architecture version to build for. This will be used if 'Override Platform' is enabled.");
                        EditorGUILayout.LabelField(label, GUILayout.Width(150));
                        BuildUtils.Architecture newArchitecture = BuildUtils.DrawArchitecturePopup(m_TargetPlatform, m_Target, m_TargetArchitecture);
                        if (newArchitecture != m_TargetArchitecture)
                        {
                            m_TargetArchitecture = newArchitecture;
                            isDirty = true;
                        }
                    }
                }
                EditorGUI.indentLevel--;
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
            if (m_BuildConfig.SwitchTargetPlatform)
            {
                target = m_BuildConfig.Target;
                architecture = m_BuildConfig.TargetArchitecture;
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
