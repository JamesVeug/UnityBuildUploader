#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Wireframe
{
    public class BuildProfileWrapper : IBuildConfig, DropdownElement
    {
        private static readonly Type buildProfileType = typeof(BuildProfile);
        private static readonly Type playerSettingsType = typeof(PlayerSettings);
        private static readonly FieldInfo m_PlayerSettingsYamlInfo = buildProfileType.GetField("m_PlayerSettingsYaml", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#if UNITY_6000_3_OR_NEWER
        private static readonly PropertyInfo guidInfo = buildProfileType.GetProperty("platformGuid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#else
        private static readonly PropertyInfo guidInfo = buildProfileType.GetProperty("platformId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#endif
        private static readonly PropertyInfo buildTargetInfo = buildProfileType.GetProperty("buildTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo subtargetInfo = buildProfileType.GetProperty("subtarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo playerSettingsInfo = buildProfileType.GetProperty("playerSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo onEnableInfo = buildProfileType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo hasSerializedPlayerSettingsInfo = buildProfileType.GetMethod("HasSerializedPlayerSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo deserializeFromYAMLStringInfo = playerSettingsType.GetMethod("DeserializeFromYAMLString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        
        private static readonly PropertyInfo platformBuildProfileInfo = buildProfileType.GetProperty("platformBuildProfile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // GetScenesForBuild was added part way through 6000.0 (missing in 6000.0.31, present in 6000.0.77)
        private static readonly MethodInfo getScenesForBuildInfo = buildProfileType.GetMethod("GetScenesForBuild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        private static readonly PropertyInfo overrideGlobalScenesInfo = buildProfileType.GetProperty("overrideGlobalScenes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                                       ?? buildProfileType.GetProperty("overrideGlobalSceneList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static PlayerSettings globalCachedPlayerSettings;
        
        public string GetBuildName => Profile.name;
        public BuildTargetGroup GetTargetPlatform => BuildPipeline.GetBuildTargetGroup(GetTarget);

        public bool TryGetErrors(ref List<GUIContent> errors)
        {
            if (profile == null)
            {
                errors.Add(new GUIContent("Build Profile is null"));
                return false;
            }
            
            return true;
        }

        public List<string> GetSceneGUIDs
        {
            get
            {
                List<string> guids = new List<string>();
                foreach (EditorBuildSettingsScene scene in GetScenesForBuild(Profile))
                {
                    guids.Add(scene.guid.ToString());
                }
                return guids;
            }
        }

        public static EditorBuildSettingsScene[] GetScenesForBuild(BuildProfile profile)
        {
            if (getScenesForBuildInfo != null)
            {
                return (EditorBuildSettingsScene[])getScenesForBuildInfo.Invoke(profile, null);
            }

            // Older 6000.0 versions: same logic as BuildProfile.GetScenesForBuild
            if (overrideGlobalScenesInfo == null)
            {
                Debug.LogWarning("Unable to find BuildProfile overrideGlobalScenes! Using global scene list.");
                return EditorBuildSettings.scenes;
            }

            bool overrideGlobalScenes = (bool)overrideGlobalScenesInfo.GetValue(profile);
            return overrideGlobalScenes ? profile.scenes : EditorBuildSettings.scenes;
        }

        public string GetGUID
        {
            get
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Profile, out string guid, out _))
                {
                    return guid;
                }
                
                // Workaround
                return Profile.name;
            }
        }
        
        public BuildTarget GetTarget
        {
            get
            {
                if (buildTargetInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile target!");
                    return BuildTarget.NoTarget;
                }
                
                BuildTarget platformGuid = (BuildTarget)buildTargetInfo.GetValue(Profile);
                return platformGuid;
            }
        }
        
        public int GetTargetPlatformSubTarget
        {
            get
            {
                if (subtargetInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile subTarget!");
                    return 0;
                }
                
                StandaloneBuildSubtarget subTarget = (StandaloneBuildSubtarget)subtargetInfo.GetValue(Profile);
                return (int)subTarget;
            }
        }
        
        public bool IsDevelopmentBuild
        {
            get
            {
                if (platformBuildProfileInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformBuildProfile!");
                    return false;
                }
                
                object platformSettings = platformBuildProfileInfo.GetValue(Profile);
                if (platformSettings == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformSettings!");
                    return false;
                }

                Type platformSettingsType = platformSettings.GetType();
                PropertyInfo developmentProperty = platformSettingsType.GetProperty("development");
                if (developmentProperty == null)
                {
                    Debug.LogError("Unable to find development property in platformSettings!");
                    return false;
                }

                bool value = (bool)developmentProperty.GetValue(platformSettings);
                return value;
            }
        }
        
        public bool AllowDebugging
        {
            get
            {
                if (platformBuildProfileInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformBuildProfile!");
                    return false;
                }
                
                object platformSettings = platformBuildProfileInfo.GetValue(Profile);
                if (platformSettings == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformSettings!");
                    return false;
                }

                Type platformSettingsType = platformSettings.GetType();
                PropertyInfo allowDebuggingProperty = platformSettingsType.GetProperty("allowDebugging");
                if (allowDebuggingProperty == null)
                {
                    Debug.LogError("Unable to find allowDebugging property in platformSettings!");
                    return false;
                }
                
                return (bool)allowDebuggingProperty.GetValue(platformSettings);
            }
        }
        
        public bool ConnectProfiler
        {
            get
            {
                if (platformBuildProfileInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformBuildProfile!");
                    return false;
                }
                
                object platformSettings = platformBuildProfileInfo.GetValue(Profile);
                if (platformSettings == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformSettings!");
                    return false;
                }

                Type platformSettingsType = platformSettings.GetType();
                PropertyInfo connectProfilerProperty = platformSettingsType.GetProperty("connectProfiler");
                if (connectProfilerProperty == null)
                {
                    Debug.LogError("Unable to find connectProfiler property in platformSettings!");
                    return false;
                }
                
                return (bool)connectProfilerProperty.GetValue(platformSettings);
            }
        }
        
        public bool EnableDeepProfilingSupport
        {
            get
            {
                if (platformBuildProfileInfo == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformBuildProfile!");
                    return false;
                }
                
                object platformSettings = platformBuildProfileInfo.GetValue(Profile);
                if (platformSettings == null)
                {
                    Debug.LogError("Unable to find BuildProfile platformSettings!");
                    return false;
                }

                Type platformSettingsType = platformSettings.GetType();
                PropertyInfo buildWithDeepProfilingSupportProperty = platformSettingsType.GetProperty("buildWithDeepProfilingSupport");
                if (buildWithDeepProfilingSupportProperty == null)
                {
                    Debug.LogError("Unable to find buildWithDeepProfilingSupport property in platformSettings!");
                    return false;
                }
                
                return (bool)buildWithDeepProfilingSupportProperty.GetValue(platformSettings);
            }
        }

        private PlayerSettings GetBuildProfilePlayerSettings()
        {
            PlayerSettings ps = (PlayerSettings)playerSettingsInfo.GetValue(Profile);
            if (ps != null)
            {
                return ps;
            }

            if (tempCachedPlayerSettings != null)
            {
                return tempCachedPlayerSettings;
            }
            
            bool hasSerializedPlayerSettings = (bool)hasSerializedPlayerSettingsInfo.Invoke(Profile, null);
            if (hasSerializedPlayerSettings)
            {
                object playerSettingsYaml = m_PlayerSettingsYamlInfo.GetValue(Profile);
                
                tempCachedPlayerSettings = (PlayerSettings)deserializeFromYAMLStringInfo.Invoke(null, new []{playerSettingsYaml});
                return tempCachedPlayerSettings;
            }

            // Get settings overall
            if (globalCachedPlayerSettings == null)
            {
                globalCachedPlayerSettings = AssetDatabase.LoadAssetAtPath<PlayerSettings>("ProjectSettings/ProjectSettings.asset");
            }
            
            return globalCachedPlayerSettings;
        }
        
        public string GetProductName => PlayerSettings.productName; // TODO: Actually read it
        public BuildUtils.Architecture GetTargetArchitecture => BuildUtils.Architecture.x64; // TODO: Actually read it
        public ScriptingImplementation GetScriptingBackend => BuildUtils.CurrentScriptingBackend(); // TODO: Actually read it
        public bool GetSwitchTargetPlatform => true; // TODO: Is this needed?

        public int Id => id;
        public string DisplayName => profile.name;
        public BuildProfile Profile => profile;

        private int id;
        private BuildProfile profile;
        private PlayerSettings tempCachedPlayerSettings;

        public BuildProfileWrapper(BuildProfile profile, int id = -1)
        {
            this.profile = profile;
            this.id = id;
        }

        public BuildOptions GetBuildOptions()
        {
            BuildOptions buildOptions = BuildOptions.None;

            if (IsDevelopmentBuild)
            {
                buildOptions |= BuildOptions.Development;

                if (AllowDebugging) buildOptions |= BuildOptions.AllowDebugging;
                if (ConnectProfiler) buildOptions |= BuildOptions.ConnectWithProfiler;
                if (EnableDeepProfilingSupport) buildOptions |= BuildOptions.EnableDeepProfilingSupport;
            }

            // if (BuildScriptsOnly) // TODO
            //     buildOptions |= BuildOptions.BuildScriptsOnly;

            return buildOptions;
        }

        public string GetFormattedProductName(Context ctx)
        {
            return Application.productName; // BuildProfile has no override
        }

        public bool ApplySettings(bool switchPlatform, Context context, UploadTaskReport.StepResult stepResult = null)
        {
            stepResult?.AddLog($"Applying settings");
            try
            {
                BuildProfile.SetActiveBuildProfile(Profile);
                // onEnableInfo.Invoke(Profile, null);
                
                BuildProfile activeBuildProfile = BuildProfile.GetActiveBuildProfile();
                if(activeBuildProfile != null && GetGUID == new BuildProfileWrapper(activeBuildProfile, 1).GetGUID)
                {
                    stepResult?.AddLog($"Build Profile " + GetBuildName + " applied");
                    return true;
                }
                else
                {
                    stepResult?.SetFailed($"Build Profile " + GetBuildName + " failed to apply");
                    return false;
                }
                
            }
            catch (Exception e)
            {
                stepResult?.SetFailed("Failed to apply Build Profile. Check the console for more details.");
                stepResult?.AddException(e);
                return false;
            }
        }

        public string GetProductExtension()
        {
            bool isAndroidBundle = false;
            if (GetTarget == BuildTarget.Android)
            {
                if (platformBuildProfileInfo != null)
                {
                    object platformSettings = platformBuildProfileInfo.GetValue(Profile);
                    if (platformSettings != null)
                    {
                        Type platformSettingsType = platformSettings.GetType();
                        PropertyInfo buildAppBundleProp = platformSettingsType.GetProperty("buildAppBundle");
                        if (buildAppBundleProp != null)
                        {
                            isAndroidBundle = (bool)buildAppBundleProp.GetValue(platformSettings);
                        }
                    }
                }
            }
            return BuildUtils.GetPlatformExtension(GetTargetPlatform, GetTarget, isAndroidBundle);
        }
    }
    
}
#endif