using System;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// WindowsBuildProfileExtension.cs for reference
    /// </summary>
    public partial class BuildConfig : DropdownElement
    {
        internal bool Collapsed { get; set; } = true;
        public int Id { get; set; }
        public string DisplayName => BuildName;
        
        private ReorderableListOfScenes m_sceneList = new ReorderableListOfScenes();
        private ReorderableListOfScriptDefines m_definesList = new ReorderableListOfScriptDefines();
        
        private GUIStyle m_titleStyle;
        private bool m_showFormattedBuildName;
        private bool m_showFormattedProductName;
        
        private void SetupGUI()
        {
            if (m_titleStyle == null)
            {
                m_sceneList.Initialize(SceneGUIDs, "Scenes", SceneGUIDs.Count <= 6, _ => { Save(); });
                m_definesList.Initialize(ExtraScriptingDefines, "Extra Script Defines", ExtraScriptingDefines.Count <= 6, _ => { Save(); });
                
                m_titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        public void OnGUI(float width, ref bool dirty, Context context)
        {
            SetupGUI();
            
            if (Collapsed)
            {
                OnGUICollapsed(width, ref dirty, context);
            }
            else
            {
                OnGUIExpanded(width, ref dirty, context);
            }
        }

        public void OnGUICollapsed(float width, ref bool dirty, Context context)
        {
            // Draw the collapsed view of the BuildConfig
            string formattedBuildName = context.FormatString(BuildName);
            GUILayout.Label(formattedBuildName, GUILayout.Width(width - 50));
        }

        public void OnGUIExpanded(float width, ref bool dirty, Context context)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(width)))
                {
                    // Draw the title of the BuildConfig
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Config Name:", "Name of this build config for visual and debugging purposes.");
                        GUILayout.Label(label, GUILayout.Width(150));
                        if (EditorUtils.FormatStringTextField(ref BuildName, ref m_showFormattedBuildName, context))
                        {
                            dirty = true;
                        }
                        
                        GUILayout.FlexibleSpace();
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label("GUID:");
                            GUILayout.Label(GUID);
                        }

                        if (GUILayout.Button("Apply to Editor", GUILayout.Width(120)))
                        {
                            if (EditorUtility.DisplayDialog("Apply to Editor",
                                    "Are you sure you want to apply settings to the editor?\n" +
                                    "This will change your Player settings and Editor settings", 
                                    "Apply", "Cancel"))
                            {
                                ApplySettings(true, context);
                            }
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Product Name:", "The name of the build for example: MySuperScaryGame");
                        GUILayout.Label(label, GUILayout.Width(150));
                        if (EditorUtils.FormatStringTextField(ref ProductName, ref m_showFormattedProductName, context))
                        {
                            dirty = true;
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Build Scripts Only:", "When enabled, the build will only include scripts and no assets. This is useful for testing scripts without building the entire game.");
                        GUILayout.Label(label, GUILayout.Width(150));
                        bool newDevelopmentBuild = EditorGUILayout.Toggle(BuildScriptsOnly);
                        if (newDevelopmentBuild != BuildScriptsOnly)
                        {
                            BuildScriptsOnly = newDevelopmentBuild;
                            dirty = true;
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Development Build:", "When enabled, the build will enable development features such as script debugging, profiler support, and more. This is useful for testing and debugging the game during development.");
                        GUILayout.Label(label, GUILayout.Width(150));
                        bool newDevelopmentBuild = EditorGUILayout.Toggle(IsDevelopmentBuild);
                        if (newDevelopmentBuild != IsDevelopmentBuild)
                        {
                            IsDevelopmentBuild = newDevelopmentBuild;
                            dirty = true;
                        }
                    }
                    
                    using (new EditorGUI.DisabledScope(!IsDevelopmentBuild))
                    {
                    
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Script Debugging:", "When enabled, your chosen coding IDE will be able to attach to the build to inspect code.");
                            GUILayout.Label(label, GUILayout.Width(150));
                            bool newDevelopmentBuild = EditorGUILayout.Toggle(AllowDebugging);
                            if (newDevelopmentBuild != AllowDebugging)
                            {
                                AllowDebugging = newDevelopmentBuild;
                                dirty = true;
                            }
                        }
                        
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Autoconnect Profiler:",
                                "When enabled, the Unity Profiler will automatically connect to the build when it starts. This is useful for debugging and profiling the game.");
                            GUILayout.Label(label, GUILayout.Width(150));
                            bool newDevelopmentBuild = EditorGUILayout.Toggle(ConnectProfiler);
                            if (newDevelopmentBuild != ConnectProfiler)
                            {
                                ConnectProfiler = newDevelopmentBuild;
                                dirty = true;
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Deep Profiling Support:",
                                "When profiling the game with the Unity Profiler, this enables deep profiling support. This will increase the performance overhead of the profiler but will provide more detailed profiling information.");
                            GUILayout.Label(label, GUILayout.Width(150));
                            bool newDevelopmentBuild = EditorGUILayout.Toggle(EnableDeepProfilingSupport);
                            if (newDevelopmentBuild != EnableDeepProfilingSupport)
                            {
                                EnableDeepProfilingSupport = newDevelopmentBuild;
                                dirty = true;
                            }
                        }
                    }

                    EditorGUILayout.Space();
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Stripping Level:", StrippingLevelToolTip());
                        GUILayout.Label(label, GUILayout.Width(150));
                        ManagedStrippingLevel newStrippingLevel = (ManagedStrippingLevel)EditorGUILayout.EnumPopup(StrippingLevel);
                        if (newStrippingLevel != StrippingLevel)
                        {
                            StrippingLevel = newStrippingLevel;
                            dirty = true;
                        }
                    }
                    
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Scripting Backend:", "The scripting backend to use for the build. This will determine how your code is compiled and executed.");
                        GUILayout.Label(label, GUILayout.Width(150));
                        ScriptingImplementation newScriptingBackend = (ScriptingImplementation)EditorGUILayout.EnumPopup(ScriptingBackend);
                        if (newScriptingBackend != ScriptingBackend)
                        {
                            ScriptingBackend = newScriptingBackend;
                            dirty = true;
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Compression:", 
                            "Compression applied to Player data (scenes and resources).\n" +
                            "Default - none or default platform compression.\n" +
                            "LZ4 - fast compression suitable for Development Builds.\n" +
                            "LZ4HC - high compression rate variance of LZ4, causes longer build Times. Works best for Release Builds");
                        GUILayout.Label(label, GUILayout.Width(150));
                        BuildUtils.Compression compression = (BuildUtils.Compression)EditorGUILayout.EnumPopup(CompressionMethod);
                        if (compression != CompressionMethod)
                        {
                            CompressionMethod = compression;
                            dirty = true;
                        }
                    }
                    
                    EditorGUILayout.Space();

                    if (m_sceneList.OnGUI())
                    {
                        Save();
                    }
                    
                    EditorGUILayout.Space();

                    if (m_definesList.OnGUI())
                    {
                        Save();
                    }
                    
                    // Stacktrace level
                    GUIContent stlLabel = new GUIContent("Stacktrace Logging:", "Set the logging level for stack traces in the build. This can help with debugging issues in the build.");
                    GUILayout.Label(stlLabel, GUILayout.Width(150));
                    dirty |= DrawLogLevel(LogType.Log);
                    dirty |= DrawLogLevel(LogType.Warning);
                    dirty |= DrawLogLevel(LogType.Assert);
                    dirty |= DrawLogLevel(LogType.Error);
                    dirty |= DrawLogLevel(LogType.Exception);
                    
                    EditorGUILayout.Space();
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Override Platform:", "When enabled switch to this platform before starting the build. If disabled, the current platform will be used.");
                        GUILayout.Label(label, GUILayout.Width(150));
                        bool switchTarget = EditorGUILayout.Toggle(SwitchTargetPlatform);
                        if (switchTarget != SwitchTargetPlatform)
                        {
                            SwitchTargetPlatform = switchTarget;
                            dirty = true;
                        }
                    }
                    
                    using (new EditorGUI.DisabledScope(!SwitchTargetPlatform))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Target Platform:", "The platform to build for. This will be used if 'Override Platform' is enabled.");
                            GUILayout.Label(label, GUILayout.Width(150));

                            (BuildTargetGroup buildTargetGroup, int newSubTarget, BuildTarget newTarget) = BuildUtils.DrawPlatformPopup(TargetPlatform, TargetPlatformSubTarget, Target);
                            if (buildTargetGroup != TargetPlatform || newSubTarget != TargetPlatformSubTarget || newTarget != Target)
                            {
                                TargetPlatform = buildTargetGroup;
                                TargetPlatformSubTarget = newSubTarget;
                                Target = newTarget;
                                dirty = true;
                            }
                        }

                        if(TargetPlatform == BuildTargetGroup.Standalone){
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUIContent label = new GUIContent("Target Architecture:", "The Architecture version to build for. This will be used if 'Override Platform' is enabled.");
                                GUILayout.Label(label, GUILayout.Width(150));
                                BuildUtils.Architecture newArchitecture = BuildUtils.DrawArchitecturePopup(TargetPlatform, Target, TargetArchitecture);
                                if (newArchitecture != TargetArchitecture)
                                {
                                    TargetArchitecture = newArchitecture;
                                    dirty = true;
                                }
                            }
                        }
                    }

                    if (TargetPlatform == BuildTargetGroup.Android)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Build App Bundle:", "Build an Android App Bundle (.aab) for Google Play instead of an APK.");
                            GUILayout.Label(label, GUILayout.Width(150));
                            bool newBuildAppBundle = EditorGUILayout.Toggle(AndroidBuildAppBundle);
                            if (newBuildAppBundle != AndroidBuildAppBundle)
                            {
                                AndroidBuildAppBundle = newBuildAppBundle;
                                dirty = true;
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUIContent label = new GUIContent("Target Architectures:", "The CPU architectures the Android build includes. ARM64 needs the IL2CPP scripting backend.");
                            GUILayout.Label(label, GUILayout.Width(150));
                            AndroidArchitecture newArchitectures = (AndroidArchitecture)EditorGUILayout.EnumFlagsField(AndroidTargetArchitectures);
                            if (newArchitectures != AndroidTargetArchitectures)
                            {
                                AndroidTargetArchitectures = newArchitectures;
                                dirty = true;
                            }
                        }
                    }
                }
            }
        }

        private bool DrawLogLevel(LogType log)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(log.ToString(), GUILayout.Width(70));
                
                StackTraceLogType enabled = StackTraceLogTypes[log];

                // Show toggles for each log type
                foreach (StackTraceLogType trace in Enum.GetValues(typeof(StackTraceLogType)))
                {
                    bool isEnabled = (enabled == trace);
                    string label = trace.ToString();
                    bool newEnabled = GUILayout.Toggle(isEnabled, label, "Button", GUILayout.MaxWidth(80));
                    if (newEnabled != isEnabled && newEnabled)
                    {
                        StackTraceLogTypes[log] = trace;
                        return true;
                    }
                }
            }

            return false;
        }

        private string StrippingLevelToolTip()
        {
            string text = "How much code to remove when making this build.\n\n";
            foreach (ManagedStrippingLevel level in Enum.GetValues(typeof(ManagedStrippingLevel)))
            {
                text += $"{level}\n{GetManagedStrippingComment(level)}\n\n";
            }
            
            return text;
        }

        private void Save()
        {
            BuildConfigsUIUtils.Save();
        }

        private string GetManagedStrippingComment(ManagedStrippingLevel level)
        {
            switch (level)
            {
                case ManagedStrippingLevel.Disabled:
                    return "Do not strip any code.";
                case ManagedStrippingLevel.Low:
                    return "Remove unreachable managed code to reduce build size and Mono/IL2CPP build times.";
                case ManagedStrippingLevel.Medium:
                    return "Run UnityLinker in a less conservative mode than Low. This will further reduce code size beyond what Low can achieve. However, this additional reduction may come with tradeoffs. Possible side effects may include, having to maintain a custom link.xml file, and some reflection code paths may not behave the same.";
                case ManagedStrippingLevel.High:
                    return
                        "UnityLinker will strip as much as possible. This will further reduce code size beyond what Medium can achieve. However, this additional reduction may come with tradeoffs. Possible side effects may include, managed code debugging of some methods may no longer work. You may need to maintain a custom link.xml file, and some reflection code paths may not behave the same.";
#if UNITY_2021_1_OR_NEWER
                case ManagedStrippingLevel.Minimal:
                    return
                        "The class libraries, UnityEngine, and Windows Runtime assemblies will be stripped. All other assemblies are copied.";
#endif
                default:
                    return "Unknown managed code stripping level.";
            }
        }
    }
}