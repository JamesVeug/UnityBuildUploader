using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

#if UNITY_2021_1_OR_NEWER
using UnityEditor.Build;
#endif

namespace Wireframe
{
    public static class BuildUtils
    {
        public enum Architecture
        {
            /// <summary>
            ///   <para>Supported for MacOS, Windows and Linux.</para>
            /// </summary>
            x64,
            /// <summary>
            ///   <para>Supported for MacOS and Windows.</para>
            /// </summary>
            ARM64,
            /// <summary>
            ///   <para>Supported for MacOS.</para>
            /// </summary>
            x64ARM64,
            /// <summary>
            ///   <para>Supported for Windows.</para>
            /// </summary>
            x86,
        }
        
        public enum Compression // From BuildProfilePlatformSettingsBase.cs
        {
            Default = 0,
            Lz4 = 2,
            Lz4HC = 3,
        }
            
        internal static SemaphoreSlim m_lock = new SemaphoreSlim(1);
        internal static int m_totalBuildsInProgress;

        public static Task WaitForTurnToBuild()
        {
            return m_lock.WaitAsync();
        }
        
        public static void ReleaseBuildLock()
        {
            m_lock.Release();
        }
        
        public static int ChangeRunningBuilds(bool increment)
        {
            if (increment)
                m_totalBuildsInProgress++;
            else
                m_totalBuildsInProgress--;
            return m_totalBuildsInProgress;
        }
        
        public static int GetIntValue(this Architecture architecture)
        {
#if UNITY_2021_1_OR_NEWER
            return (int)architecture;
#else
            // 0 - None, 1 - ARM64, 2 - Universal
            // https://docs.unity3d.com/2020.3/Documentation/ScriptReference/PlayerSettings.GetArchitecture.html
            int architectureValue = 2;
            switch (architecture)
            {
                case Architecture.x86:
                    architectureValue = 1;
                    break;
            }
            return architectureValue;
#endif
        }

        public static ManagedStrippingLevel CurrentStrippingLevel()
        {
#if UNITY_2021_1_OR_NEWER
            return PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(BuildTargetToPlatform()));
#else
            return PlayerSettings.GetManagedStrippingLevel(BuildTargetToPlatform());
#endif
        }

        public static ScriptingImplementation CurrentScriptingBackend()
        {
#if UNITY_2021_1_OR_NEWER
            return PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(BuildTargetToPlatform()));
#else
            return PlayerSettings.GetScriptingBackend(BuildTargetToPlatform());
#endif
        }

        public static Dictionary<LogType, StackTraceLogType> CurrentStackTraceLogTypes()
        {
            Dictionary<LogType, StackTraceLogType> stackTraceLogTypes = new Dictionary<LogType, StackTraceLogType>();
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
            {
                stackTraceLogTypes[logType] = PlayerSettings.GetStackTraceLogType(logType);
            }

            return stackTraceLogTypes;
        }

        public static Architecture CurrentTargetArchitecture()
        {
#if UNITY_2021_1_OR_NEWER
            BuildTarget value = BuildUtils.CurrentTargetPlatform();
            string buildTargetName = BuildPipeline.GetBuildTargetName(value);
            string lower = EditorUserBuildSettings.GetPlatformSettings(buildTargetName, "Architecture").ToLower();
            if (Enum.TryParse(lower, out Architecture result))
            {
                return result;
            }
#endif

            // Assume always 64 bit
            return Architecture.x64;
        }

        public static BuildTarget CurrentTargetPlatform()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                    target = BuildTarget.StandaloneWindows64;
                    break;
            }
            return target;
        }

        public static BuildTargetGroup BuildTargetToPlatform()
        {
            return BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        }

        public static List<string> GetDefaultScriptingDefines()
        {
            List<string> defines = new List<string>();

            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            string[] scriptingDefines = null;
#if UNITY_2021_1_OR_NEWER
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out scriptingDefines);
#else
            string value = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            scriptingDefines = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
#endif
            defines.AddRange(scriptingDefines);

            return defines;
        }

        public static List<string> GetCurrentScenesGUIDs()
        {
            List<string> defaultScenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string guid = scene.guid.ToString();
                if (scene.enabled && !defaultScenes.Contains(guid))
                {
                    defaultScenes.Add(guid);
                }
            }

            if (defaultScenes.Count == 0)
            {
                if (EditorSceneManager.GetActiveScene().IsValid())
                {
                    string path = EditorSceneManager.GetActiveScene().path;
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (!string.IsNullOrEmpty(guid) && !defaultScenes.Contains(guid))
                    {
                        defaultScenes.Add(guid);
                    }
                }
            }

            return defaultScenes;
        }

        public static string GetDefaultProductName()
        {
            return Application.productName;
        }
    

        private static List<BuildPlatform> BuildPlatforms;

        public static List<BuildPlatform> ValidPlatforms
        {
            get
            {
                if (BuildPlatforms == null)
                {
                    BuildPlatforms = GetValidPlatforms();
                }

                return BuildPlatforms;
            }
        }
        
        public static (BuildTargetGroup newTargetGroup, int newSubTarget, BuildTarget target) DrawPlatformPopup(BuildTargetGroup group, int subTarget, BuildTarget target)
        {
            List<BuildPlatform> platforms = GetValidPlatforms();
            int selectedIndex = Mathf.Max(0, platforms.FindIndex(a=>a.targetGroup == group && a.subTarget == subTarget && a.defaultTarget == target));
            
            // Draw list of platforms
            string[] names = new string[platforms.Count];
            for (int i = 0; i < platforms.Count; i++)
            {
                names[i] = platforms[i].DisplayName;
            }
            int newIndex = EditorGUILayout.Popup(selectedIndex, names);
            BuildTargetGroup newTargetGroup = platforms[newIndex].targetGroup;
            int newTargetGroupSubTarget = platforms[newIndex].subTarget;
            BuildTarget newTarget = platforms[newIndex].defaultTarget;
            
            return (newTargetGroup, newTargetGroupSubTarget, newTarget);
        }
        
        private static Type BuildPlatformsType = Type.GetType("UnityEditor.Build.BuildPlatforms, UnityEditor");
        private static PropertyInfo BuildPlatformsTypeInstanceProp = BuildPlatformsType.GetProperty("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static List<BuildPlatform> GetValidPlatforms()
        {
            object instance = BuildPlatformsTypeInstanceProp.GetValue(null);
            FieldInfo buildPlatformsField = BuildPlatformsType.GetField("buildPlatforms", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object result = buildPlatformsField.GetValue(instance);
            
            List<BuildPlatform> validPlatforms = new List<BuildPlatform>();
            foreach (var platform in (IList)result)
            {
                BuildPlatform buildPlatform = BuildPlatform.ToBuildPlatform(platform);
                if (buildPlatform != null)
                {
                    validPlatforms.Add(buildPlatform);

                    if (buildPlatform.targetGroup == BuildTargetGroup.Standalone)
                    {
                        buildPlatform.defaultTarget = BuildTarget.StandaloneWindows64;
                        
                        BuildPlatform macBuildPlatform = BuildPlatform.ToBuildPlatform(platform);
                        macBuildPlatform.defaultTarget = BuildTarget.StandaloneOSX;
                        macBuildPlatform.supported = IsTargetGroupSupported(macBuildPlatform.targetGroup, macBuildPlatform.defaultTarget);
                        macBuildPlatform.installed = IsTargetGroupInstalled(macBuildPlatform.targetGroup, macBuildPlatform.defaultTarget);
                        validPlatforms.Add(macBuildPlatform);
                        
                        BuildPlatform linuxBuildPlatform = BuildPlatform.ToBuildPlatform(platform);
                        linuxBuildPlatform.defaultTarget = BuildTarget.StandaloneLinux64;
                        linuxBuildPlatform.supported = IsTargetGroupSupported(linuxBuildPlatform.targetGroup, linuxBuildPlatform.defaultTarget);
                        linuxBuildPlatform.installed = IsTargetGroupInstalled(linuxBuildPlatform.targetGroup, linuxBuildPlatform.defaultTarget);
                        validPlatforms.Add(linuxBuildPlatform);
                    }
                }
            }
            
            return validPlatforms;
        }

        public static bool TrySwitchPlatform(BuildTargetGroup TargetPlatform, int TargetPlatformSubTarget, BuildTarget Target, Architecture architecture, UploadTaskReport.StepResult stepResult)
        {
            if ((int)TargetPlatform == 0)
            {
                stepResult?.AddError("No target platform selected");
                stepResult?.SetFailed("No target platform selected");
                return false;
            }

            
#if UNITY_2021_1_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
            if (TargetPlatform == BuildTargetGroup.Standalone && TargetPlatformSubTarget == 0)
    #else
            if (TargetPlatform == BuildTargetGroup.Standalone && TargetPlatformSubTarget == -1)
    #endif
            {
                stepResult?.AddError("No target platform sub-target selected for Standalone platform");
                stepResult?.SetFailed("No target platform sub-target selected for Standalone platform");
                return false;
            }
#else
            // No sub-targets before Unity 2021.1 for standalone
#endif
            
            if ((int)Target == 0)
            {
                stepResult?.AddError("No target selected");
                stepResult?.SetFailed("No target selected");
                return false;
            }
            
            if (!IsEditorSetToTarget(TargetPlatform, TargetPlatformSubTarget, Target))
            {
                stepResult?.AddLog($"Switching build target to {TargetPlatform} subTarget {TargetPlatformSubTarget} target {Target}");
                bool switched = SwitchToBuildTarget(TargetPlatform, TargetPlatformSubTarget, Target, architecture);
                if (!switched)
                {
                    stepResult?.AddError($"Failed to switch build target to {Target}");
                    stepResult?.SetFailed("Failed to switch build target. Check the console for more details.");
                    return false;
                }

                if (EditorUserBuildSettings.activeBuildTarget != Target)
                {
                    stepResult?.AddError($"Failed to switch build target to {Target}. Current target is {EditorUserBuildSettings.activeBuildTarget}");
                    stepResult?.SetFailed("Failed to switch build target. Check the console for more details.");
                    return false;
                }

                stepResult?.AddLog($"Switched build target to {TargetPlatform}");
                return true;
            }

            stepResult?.AddLog($"Build target is already set to {TargetPlatform} - {Target} - {TargetPlatformSubTarget}");
            return true;
        }

        public static Architecture DrawArchitecturePopup(BuildTargetGroup targetPlatform, BuildTarget target, Architecture architecture)
        {
            if (targetPlatform != BuildTargetGroup.Standalone)
            {
                return architecture;
            }
            
            List<Architecture> options = new List<Architecture>();
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    options.Add(Architecture.x86);
                    options.Add(Architecture.x64);
                    options.Add(Architecture.ARM64);
                    break;
                case BuildTarget.StandaloneOSX:
                    options.Add(Architecture.x64);
                    options.Add(Architecture.ARM64);
                    options.Add(Architecture.x64ARM64);
                    break;
                case BuildTarget.StandaloneLinux64:
                    options.Add(Architecture.x64);
                    break;
            }

            if (options.Count > 0)
            {
                string[] names = new string[options.Count];
                for (int i = 0; i < options.Count; i++)
                {
                    names[i] = options[i].ToString();
                }

                int selectedIndex = Mathf.Max(0, options.IndexOf(architecture));
                int newIndex = EditorGUILayout.Popup(selectedIndex, names);
                return options[newIndex];
            }
            
            return architecture;
        }

        public static int CurrentSubTarget()
        {
#if UNITY_2021_1_OR_NEWER
            MethodInfo methodInfo = typeof(EditorUserBuildSettings).GetMethod("GetActiveSubtargetFor", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            int subTarget = (int)methodInfo.Invoke(null, new object[] { EditorUserBuildSettings.activeBuildTarget });
            return subTarget;
#else
            return 0; // Player
#endif
        }
        
        public static bool SwitchToBuildTarget(BuildTargetGroup targetPlatform, int subTarget, BuildTarget target, Architecture architecture)
        {
            if (IsEditorSetToTarget(targetPlatform, subTarget, target))
            {
                return true;
            }

            try
            {
                switch (targetPlatform)
                {
                    case BuildTargetGroup.Standalone:
#if UNITY_2021_1_OR_NEWER
                        EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)subTarget;
#endif
                        break;
                    case BuildTargetGroup.PS4:
                        EditorUserBuildSettings.ps4BuildSubtarget = (PS4BuildSubtarget)subTarget;
                        break;
                }

#if UNITY_6000_0_OR_NEWER
                MethodInfo methodInfo = typeof(EditorUserBuildSettings).GetMethod("SwitchActiveBuildTargetAndSubtarget",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                bool successful = (bool)methodInfo.Invoke(null, new object[] { target, subTarget });
#elif UNITY_2021_1_OR_NEWER
                MethodInfo methodInfo = typeof(EditorUserBuildSettings).GetMethod("SwitchActiveBuildTargetAndSubtarget",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                bool successful = (bool)methodInfo.Invoke(null, new object[] { targetPlatform, target, subTarget });
#else
                // Subtargets not supported before Unity 2021.1
                bool successful = EditorUserBuildSettings.SwitchActiveBuildTarget(targetPlatform, target);
#endif
                if (!successful)
                {
                    Debug.LogError($"Failed to switch build target to {targetPlatform} - {target} - {subTarget}");
                    return false;
                }
                
                if (UsesTargetArchitecture(targetPlatform))
                {
                    int architectureValue = architecture.GetIntValue();
#if UNITY_2021_1_OR_NEWER
                    PlayerSettings.SetArchitecture(NamedBuildTarget.FromBuildTargetGroup(targetPlatform), architectureValue);
#else
                    PlayerSettings.SetArchitecture(targetPlatform, architectureValue);
#endif
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to switch build target to {targetPlatform} - {target} - {subTarget}: {e}");
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// The desktop Target Architecture means nothing on Android, whose architectures are a separate
        /// BuildConfig setting (PlayerSettings.Android.targetArchitectures).
        /// </summary>
        internal static bool UsesTargetArchitecture(BuildTargetGroup targetPlatform)
        {
            return targetPlatform != BuildTargetGroup.Android;
        }

        public static bool IsEditorSetToTarget(BuildTargetGroup targetPlatform, int targetPlatformSubTarget, BuildTarget target)
        {
            if(EditorUserBuildSettings.selectedBuildTargetGroup != targetPlatform)
            {
                return false;
            }
            
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                return false;
            } 
            
            int currentSubTarget = CurrentSubTarget();
            if (currentSubTarget != targetPlatformSubTarget)
            {
                return false;
            }

            return true;
        }

        public static List<BuildTarget> ValidTargetsForPlatform(BuildTargetGroup buildTargetGroup)
        {
            List<BuildTarget> targets = new List<BuildTarget>();
            foreach (var platform in ValidPlatforms)
            {
                if (platform.targetGroup != buildTargetGroup)
                {
                    continue;
                }
                
                if (!targets.Contains(platform.defaultTarget)) 
                    targets.Add(platform.defaultTarget);
            }

            return targets;
        }

        public static BuildPlatform GetBuildPlatform(BuildTargetGroup targetGroup, BuildTarget target, int subTarget)
        {
            foreach (var platform in ValidPlatforms)
            {
                if (platform.targetGroup == targetGroup && platform.defaultTarget == target && platform.subTarget == subTarget)
                {
                    return platform;
                }
            }

            return null;
        }
        
        public static string ScriptingBackendDisplayName(ScriptingImplementation script)
        {
            switch (script)
            {
                case ScriptingImplementation.IL2CPP:
                    return "IL2CPP";
                case ScriptingImplementation.Mono2x:
                    return "Mono";
                case ScriptingImplementation.WinRTDotNET:
                    return "DotNet";
#if UNITY_6000_0_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
                case ScriptingImplementation.CoreCLR:
#pragma warning restore CS0618 // Type or member is obsolete
                    return "CoreCLR";
#endif
                default:
                    return script.ToString();
            }
        }
        
        public static bool IsTargetGroupInstalled(BuildTargetGroup targetGroup, BuildTarget target)
        {
            // Copied from BuildPlayerWindow.cs
#if UNITY_2021_1_OR_NEWER
            return targetGroup == BuildTargetGroup.Standalone || BuildPipeline.GetPlaybackEngineDirectory(target, BuildOptions.None, false) != string.Empty;
#else
            return targetGroup == BuildTargetGroup.Standalone || BuildPipeline.GetPlaybackEngineDirectory(targetGroup, target, BuildOptions.None) != string.Empty;
#endif
        }

        public static bool IsTargetGroupSupported(BuildTargetGroup targetGroup, BuildTarget target)
        {
#if UNITY_6000_0_OR_NEWER
            MethodInfo IsBuildPlatformSupportedMethod = typeof(BuildPipeline).GetMethod("IsBuildPlatformSupported", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (IsBuildPlatformSupportedMethod != null) return (bool)IsBuildPlatformSupportedMethod.Invoke(null, new object[] { target });

            Debug.LogError("Could not find BuildPipeline.IsBuildPlatformSupported method via reflection.");
            return false;
#else
            return BuildPipeline.IsBuildTargetSupported(targetGroup, target);
#endif
        }

        public static void SetCompressionType(BuildTargetGroup targetGroup, Compression compression)
        {
            // EditorUserBuildSettings.SetCompressionType(namedBuildTarget.ToBuildTargetGroup(), BuildPlayerWindow.styles.compressionTypes[index]);
            MethodInfo SetCompressionTypeMethod = typeof(EditorUserBuildSettings).GetMethod("SetCompressionType", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (SetCompressionTypeMethod != null)
            {
                SetCompressionTypeMethod.Invoke(null, new object[] { targetGroup, (int)compression });
            }
            else
            {
                Debug.LogError("Could not find EditorUserBuildSettings.SetCompressionType method via reflection.");
            }
        }

        public static Compression CurrentCompressionMethod(BuildTargetGroup targetGroup)
        {
            // EditorUserBuildSettings.GetCompressionType(namedBuildTarget.ToBuildTargetGroup())
            MethodInfo GetCompressionMethod = typeof(EditorUserBuildSettings).GetMethod("GetCompressionType", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (GetCompressionMethod != null) return (Compression)GetCompressionMethod.Invoke(null, new object[] { targetGroup });

            Debug.LogError("Could not find EditorUserBuildSettings.GetCompressionType method via reflection.");
            return Compression.Default;
        }

        public static string GetPlatformExtension(BuildTargetGroup targetGroup, BuildTarget target, bool androidBundle)
        {
            if (targetGroup == BuildTargetGroup.Standalone)
            {
                if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64)
                {
                    return ".exe";
                }
                else if (target == BuildTarget.StandaloneOSX)
                {
                    return ".app";
                }
                else if (target == BuildTarget.StandaloneLinux64)
                {
                    return ".x86_64";
                }
            }
            else if (targetGroup == BuildTargetGroup.Android)
            {
                if (androidBundle)
                {
                    return ".aab";
                }
                else
                {
                    return ".apk";
                }
            }
            
            
            return "";
        }

#if UNITY_6000_0_OR_NEWER
        public static List<BuildProfile> GetAllCustomBuildProfiles()
        {
            // Default: BuildProfileContext.instance.classicPlatformProfiles
            List<BuildProfile> profiles = null;

#if UNITY_6000_5_OR_NEWER
            profiles = new List<BuildProfile>(BuildProfile.GetAllBuildProfiles());
#elif UNITY_6000_3_OR_NEWER
            Type type = Type.GetType("UnityEditor.Build.Profile.BuildProfileModuleUtil, UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            MethodInfo getProfiles = type.GetMethod("GetAllBuildProfiles", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            profiles = (List<BuildProfile>)getProfiles.Invoke(null, null);
#else
            Type type = Type.GetType("UnityEditor.Build.Profile.Handlers.BuildProfileDataSource, UnityEditor.BuildProfileModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            MethodInfo getProfiles = type.GetMethod("FindAllBuildProfiles", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            profiles = (List<BuildProfile>)getProfiles.Invoke(null, null);
#endif
            
            return profiles;
        }
#endif
    }
}