using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Wireframe
{
    public partial class BuildConfig : IBuildConfig
    {
        public string GetBuildName => BuildName;
        public string GetGUID => GUID;
        public List<string> GetSceneGUIDs => SceneGUIDs;
        public BuildTargetGroup GetTargetPlatform => TargetPlatform;
        public int GetTargetPlatformSubTarget => TargetPlatformSubTarget;
        public BuildTarget GetTarget => Target;
        public BuildUtils.Architecture GetTargetArchitecture => TargetArchitecture;
        public bool GetSwitchTargetPlatform => SwitchTargetPlatform;
        public ScriptingImplementation GetScriptingBackend => ScriptingBackend;
        public string GetProductName => ProductName;

        public string GUID;
        public string BuildName;
        public string ProductName;
        public List<string> SceneGUIDs = new List<string>();
        public List<string> ExtraScriptingDefines = new List<string>();
        
        // Options
        public bool IsDevelopmentBuild;
        public bool BuildScriptsOnly;
        public bool AllowDebugging;
        public bool ConnectProfiler;
        public bool EnableDeepProfilingSupport;
        
        // Platform-specific settings
        public bool SwitchTargetPlatform;
        public BuildTargetGroup TargetPlatform;
        public int TargetPlatformSubTarget;
        public BuildTarget Target;
        public BuildUtils.Architecture TargetArchitecture;
        public Dictionary<LogType, StackTraceLogType> StackTraceLogTypes = new Dictionary<LogType, StackTraceLogType>();
        public ManagedStrippingLevel StrippingLevel = ManagedStrippingLevel.Disabled;
        public ScriptingImplementation ScriptingBackend = ScriptingImplementation.Mono2x;
        public BuildUtils.Compression CompressionMethod = BuildUtils.Compression.Default;
        
        // Android
        public bool AndroidBuildAppBundle;
        public AndroidArchitecture AndroidTargetArchitectures = AndroidArchitecture.ARM64;

        public BuildConfig()
        {
            
        }
        
        public bool TryGetErrors(ref List<GUIContent> errors)
        {
            return true;
        }

        public void Clear()
        {
            BuildName = "";
            ProductName = "";
            SceneGUIDs = new List<string>();
            ExtraScriptingDefines = new List<string>();
            
            IsDevelopmentBuild = false;
            BuildScriptsOnly = false;
            AllowDebugging = false;
            ConnectProfiler = false;
            EnableDeepProfilingSupport = false;
            
            SwitchTargetPlatform = false;
            TargetPlatform = BuildTargetGroup.Unknown;
            TargetArchitecture = BuildUtils.Architecture.x64;
            StrippingLevel = ManagedStrippingLevel.Disabled;
            ScriptingBackend = ScriptingImplementation.Mono2x;
            CompressionMethod = BuildUtils.Compression.Default;
            AndroidBuildAppBundle = false;
            AndroidTargetArchitectures = AndroidArchitecture.ARM64;
            StackTraceLogTypes = new Dictionary<LogType, StackTraceLogType>();
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
            {
                StackTraceLogTypes[logType] = StackTraceLogType.ScriptOnly;
            }
        }

        public void SetEditorSettings()
        {
            NewGUID();
            BuildName = "New Build";
            ProductName = BuildUtils.GetDefaultProductName();
            SceneGUIDs = BuildUtils.GetCurrentScenesGUIDs();
            ExtraScriptingDefines = BuildUtils.GetDefaultScriptingDefines();
            
            IsDevelopmentBuild = EditorUserBuildSettings.development;
            BuildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly; 
            AllowDebugging = EditorUserBuildSettings.allowDebugging;
            ConnectProfiler = EditorUserBuildSettings.connectProfiler;
            EnableDeepProfilingSupport = EditorUserBuildSettings.buildWithDeepProfilingSupport;
            
            TargetPlatform = BuildUtils.BuildTargetToPlatform();
            Target = BuildUtils.CurrentTargetPlatform();
            TargetPlatformSubTarget = BuildUtils.CurrentSubTarget();
            TargetArchitecture = BuildUtils.CurrentTargetArchitecture();
            StackTraceLogTypes = BuildUtils.CurrentStackTraceLogTypes();
            StrippingLevel = BuildUtils.CurrentStrippingLevel();
            ScriptingBackend = BuildUtils.CurrentScriptingBackend();
            CompressionMethod = BuildUtils.CurrentCompressionMethod(TargetPlatform);
            AndroidBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            AndroidTargetArchitectures = PlayerSettings.Android.targetArchitectures;
        }

        public void SetDebuggingOn(bool on)
        {
            AllowDebugging = on;
            IsDevelopmentBuild = on;
            ConnectProfiler = on;

            StackTraceLogTypes[LogType.Log] = on ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;
            StackTraceLogTypes[LogType.Warning] = on ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;
            StackTraceLogTypes[LogType.Assert] = on ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;
            StackTraceLogTypes[LogType.Error] = StackTraceLogType.ScriptOnly;
            StackTraceLogTypes[LogType.Exception] = StackTraceLogType.ScriptOnly;
        }

        public void NewGUID()
        {
            GUID = Guid.NewGuid().ToString().Substring(0, 6);
        }

        public Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                { "GUID", GUID },
                { "BuildName", BuildName },
                { "ProductName", ProductName },
                { "ExtraScriptingDefines", ExtraScriptingDefines?.ToList() ?? new List<string>() },
                { "Scenes", SceneGUIDs?.ToList() ?? new List<string>() },
                { "IsDevelopmentBuild", IsDevelopmentBuild },
                { "BuildScriptsOnly", BuildScriptsOnly },
                { "AllowDebugging", AllowDebugging },
                { "ConnectProfiler", ConnectProfiler },
                { "EnableDeepProfilingSupport", EnableDeepProfilingSupport },
                { "SwitchTargetPlatform", SwitchTargetPlatform },
                { "TargetPlatform", TargetPlatform.ToString() },
                { "TargetPlatformSubTarget", TargetPlatformSubTarget },
                { "Target", Target.ToString() },
                { "TargetArchitecture", TargetArchitecture.ToString() },
                { "StackTraceLogTypes", StackTraceLogTypes.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.ToString()) },
                { "StrippingLevel", StrippingLevel.ToString() },
                { "ScriptingBackend", ScriptingBackend.ToString() },
                { "CompressionMethod", CompressionMethod.ToString() },
                { "AndroidBuildAppBundle", AndroidBuildAppBundle },
                { "AndroidTargetArchitectures", (int)AndroidTargetArchitectures }
            };
            return dict;
        }
        
        public void Deserialize(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("GUID", out var guidData) && guidData is string guid)
            {
                GUID = guid;
            }
            else
            {
                GUID = Guid.NewGuid().ToString().Substring(0, 6);
            }
            
            if (dict.TryGetValue("BuildName", out var buildNameData) && buildNameData is string buildName)
            {
                BuildName = buildName;
            }
            else
            {
                BuildName = "New Build";
            }
            
            if (dict.TryGetValue("ProductName", out var productNameData) && productNameData is string productName)
            {
                ProductName = productName;
            }
            else
            {
                ProductName = BuildUtils.GetDefaultProductName();
            }
            
            if (dict.TryGetValue("ExtraScriptingDefines", out var extraDefinesData) && extraDefinesData != null)
            {
                if (extraDefinesData.GetType().GenericTypeArguments[0] == typeof(string))
                {
                    // Deserialize then Serialize
                    ExtraScriptingDefines = (List<string>)extraDefinesData; 
                }
                else
                {
                    // Deserialize, TO JSON then from JSON
                    ExtraScriptingDefines = ((List<object>)extraDefinesData).Cast<string>().ToList();
                }
            }
            else
            {
                ExtraScriptingDefines = BuildUtils.GetDefaultScriptingDefines();
            }
            
            if (dict.TryGetValue("Scenes", out var scenesData) && scenesData != null)
            {
                if (scenesData.GetType().GenericTypeArguments[0] == typeof(string))
                {
                    // Deserialize then Serialize
                    SceneGUIDs = (List<string>)scenesData; 
                }
                else
                {
                    // Deserialize, TO JSON then from JSON
                    SceneGUIDs = ((List<object>)scenesData).Cast<string>().Distinct().ToList();
                }
            }
            else
            {
                SceneGUIDs = BuildUtils.GetCurrentScenesGUIDs();
            }
            
            if (dict.TryGetValue("IsDevelopmentBuild", out var isDevBuildData) && isDevBuildData is bool isDevBuild)
            {
                IsDevelopmentBuild = isDevBuild;
            }
            else
            {
                IsDevelopmentBuild = false;
            }
            
            if (dict.TryGetValue("BuildScriptsOnly", out var buildScriptsOnlyData) && buildScriptsOnlyData is bool buildScriptsOnly)
            {
                BuildScriptsOnly = buildScriptsOnly;
            }
            else
            {
                BuildScriptsOnly = false;
            }
            
            if (dict.TryGetValue("AllowDebugging", out var allowDebuggingData) && allowDebuggingData is bool allowDebugging)
            {
                AllowDebugging = allowDebugging;
            }
            else
            {
                AllowDebugging = false;
            }
            
            if (dict.TryGetValue("ConnectProfiler", out var connectProfilerData) && connectProfilerData is bool connectProfiler)
            {
                ConnectProfiler = connectProfiler;
            }
            else
            {
                ConnectProfiler = false;
            }
            
            if (dict.TryGetValue("EnableDeepProfilingSupport", out var enableDeepProfilingData) && enableDeepProfilingData is bool enableDeepProfiling)
            {
                EnableDeepProfilingSupport = enableDeepProfiling;
            }
            else
            {
                EnableDeepProfilingSupport = false;
            }
            
            if (dict.TryGetValue("SwitchTargetPlatform", out var switchTargetPlatformData) && switchTargetPlatformData is bool switchTargetPlatform)
            {
                SwitchTargetPlatform = switchTargetPlatform;
            }
            else
            {
                SwitchTargetPlatform = false;
            }
            
            if (dict.TryGetValue("TargetPlatform", out var targetPlatformData) && targetPlatformData is string targetPlatformStr)
            {
                if (Enum.TryParse(targetPlatformStr, out BuildTargetGroup targetPlatform))
                {
                    TargetPlatform = targetPlatform;
                }
                else
                {
                    Debug.LogWarning($"Invalid TargetPlatform value: {targetPlatformStr}. Defaulting to current platform.");
                    TargetPlatform = BuildUtils.BuildTargetToPlatform();
                }
            }
            else
            {
                TargetPlatform = BuildUtils.BuildTargetToPlatform();
            }
            
            if (dict.TryGetValue("TargetPlatformSubTarget", out var targetPlatformSubTargetData) && targetPlatformSubTargetData is long targetPlatformSubTargetLong)
            {
                TargetPlatformSubTarget = (int)targetPlatformSubTargetLong;
            }
            else
            {
                if (TargetPlatform == BuildTargetGroup.Standalone)
                {
#if UNITY_2021_1_OR_NEWER
                    TargetPlatformSubTarget = (int)StandaloneBuildSubtarget.Player;
#else
                    TargetPlatformSubTarget = 0; // Player?
#endif
                }
                else
                {
                    TargetPlatformSubTarget = 0;
                }
            }
            
            if (dict.TryGetValue("Target", out var targetData) && targetData is string targetStr)
            {
                if (Enum.TryParse(targetStr, out BuildTarget target))
                {
                    Target = target;
                }
                else
                {
                    Debug.LogWarning($"Invalid Target value: {targetStr}. Defaulting to current target.");
                    Target = CalculateTarget();
                }
            }
            else
            {
                Target = CalculateTarget();
            }
            
            if (dict.TryGetValue("TargetArchitecture", out var targetArchitectureData) && targetArchitectureData is string targetArchitectureStr)
            {
                if (Enum.TryParse(targetArchitectureStr, out BuildUtils.Architecture targetArchitecture))
                {
                    TargetArchitecture = targetArchitecture;
                }
                else
                {
                    Debug.LogWarning($"Invalid TargetArchitecture value: {targetArchitectureStr}. Defaulting to Unknown.");
                    TargetArchitecture = BuildUtils.Architecture.x64;
                }
            }
            else
            {
                TargetArchitecture = BuildUtils.CurrentTargetArchitecture();
            }
            
            if (dict.TryGetValue("StackTraceLogTypes", out var stackTraceLogTypesData))
            {
                if (stackTraceLogTypesData is Dictionary<string, string> stackTraceLogTypesDict)
                {
                    StackTraceLogTypes = stackTraceLogTypesDict.ToDictionary(
                        kvp => (LogType)Enum.Parse(typeof(LogType), kvp.Key),
                        kvp => (StackTraceLogType)Enum.Parse(typeof(StackTraceLogType), kvp.Value));
                }
                else if (stackTraceLogTypesData is Dictionary<string, object> stackTraceLogTypesObjDict)
                {
                    // Deserialize, TO JSON then from JSON
                    StackTraceLogTypes = stackTraceLogTypesObjDict.ToDictionary(
                        kvp => (LogType)Enum.Parse(typeof(LogType), kvp.Key.ToString()),
                        kvp => (StackTraceLogType)Enum.Parse(typeof(StackTraceLogType), kvp.Value.ToString()));
                }
                else
                {
                    StackTraceLogTypes = BuildUtils.CurrentStackTraceLogTypes();
                }
            }
            else
            {
                StackTraceLogTypes = BuildUtils.CurrentStackTraceLogTypes();
            }
            
            if (dict.TryGetValue("StrippingLevel", out var strippingLevelData) && strippingLevelData is string strippingLevelStr)
            {
                if (Enum.TryParse(strippingLevelStr, out ManagedStrippingLevel strippingLevel))
                {
                    StrippingLevel = strippingLevel;
                }
                else
                {
                    Debug.LogWarning($"Invalid StrippingLevel value: {strippingLevelStr}. Defaulting to Disabled.");
                    StrippingLevel = ManagedStrippingLevel.Disabled;
                }
            }
            else
            {
                StrippingLevel = BuildUtils.CurrentStrippingLevel();
            }
            
            if (dict.TryGetValue("ScriptingBackend", out var scriptingBackendData) && scriptingBackendData is string scriptingBackendStr)
            {
                if (Enum.TryParse(scriptingBackendStr, out ScriptingImplementation scriptingBackend))
                {
                    ScriptingBackend = scriptingBackend;
                }
                else
                {
                    Debug.LogWarning($"Invalid ScriptingBackend value: {scriptingBackendStr}. Defaulting to Mono2x.");
                    ScriptingBackend = ScriptingImplementation.Mono2x;
                }
            }
            else
            {
                ScriptingBackend = BuildUtils.CurrentScriptingBackend();
            }
            
            if (dict.TryGetValue("CompressionMethod", out var compressionMethodData) && compressionMethodData is string compressionMethodStr)
            {
                if (Enum.TryParse(compressionMethodStr, out BuildUtils.Compression compress))
                {
                    CompressionMethod = compress;
                }
                else
                {
                    Debug.LogWarning($"Invalid Compression value: {compressionMethodStr}. Defaulting to Default.");
                    CompressionMethod = BuildUtils.Compression.Default;
                }
            }
            else
            {
                CompressionMethod = BuildUtils.CurrentCompressionMethod(TargetPlatform);
            }
            
            // Configs saved before these existed built with the editor's own settings, so keep doing that
            if (dict.TryGetValue("AndroidBuildAppBundle", out var buildAppBundleData) && buildAppBundleData is bool buildAppBundle)
            {
                AndroidBuildAppBundle = buildAppBundle;
            }
            else
            {
                AndroidBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            }
            
            // Saved as a number because the flags differ between Unity versions. A long from JSON, an int when
            // copied straight from Serialize()
            if (dict.TryGetValue("AndroidTargetArchitectures", out var architecturesData) && (architecturesData is long || architecturesData is int))
            {
                AndroidTargetArchitectures = (AndroidArchitecture)Convert.ToInt32(architecturesData);
            }
            else
            {
                AndroidTargetArchitectures = PlayerSettings.Android.targetArchitectures;
            }
        }

        public string GetProductExtension()
        {
            return BuildUtils.GetPlatformExtension(TargetPlatform, Target, TargetPlatform == BuildTargetGroup.Android && AndroidBuildAppBundle);
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

            if (BuildScriptsOnly)
                buildOptions |= BuildOptions.BuildScriptsOnly;

            return buildOptions;
        }

        public bool ApplySettings(bool switchPlatform, Context context, UploadTaskReport.StepResult stepResult = null)
        {
            // Switch to the build target if necessary
            if (switchPlatform && SwitchTargetPlatform)
            {
                if (!BuildUtils.TrySwitchPlatform(TargetPlatform, TargetPlatformSubTarget, Target, TargetArchitecture, stepResult))
                {
                    return false;
                }
            }

            PlayerSettings.productName = context.FormatString(ProductName);
            string[] defines = ExtraScriptingDefines.Select(context.FormatString).ToArray();
#if UNITY_2021_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(TargetPlatform), defines);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(TargetPlatform), StrippingLevel);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(TargetPlatform), ScriptingBackend);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(TargetPlatform, string.Join(";", defines));
            PlayerSettings.SetManagedStrippingLevel(TargetPlatform, StrippingLevel);
            PlayerSettings.SetScriptingBackend(TargetPlatform, ScriptingBackend);
#endif
            void ApplyLogType(LogType type, Dictionary<LogType, StackTraceLogType> dict)
            {
                if (dict.TryGetValue(type, out StackTraceLogType logType))
                {
                    PlayerSettings.SetStackTraceLogType(type, logType);
                }
            }
            
            ApplyLogType(LogType.Error, StackTraceLogTypes);
            ApplyLogType(LogType.Assert, StackTraceLogTypes);
            ApplyLogType(LogType.Warning, StackTraceLogTypes);
            ApplyLogType(LogType.Log, StackTraceLogTypes);
            ApplyLogType(LogType.Exception, StackTraceLogTypes);
            
            EditorUserBuildSettings.development = IsDevelopmentBuild;
            EditorUserBuildSettings.connectProfiler = ConnectProfiler;
            EditorUserBuildSettings.allowDebugging = AllowDebugging;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = EnableDeepProfilingSupport;
            EditorUserBuildSettings.buildScriptsOnly = BuildScriptsOnly;
            BuildUtils.SetCompressionType(TargetPlatform, CompressionMethod);
            if (TargetPlatform == BuildTargetGroup.Android)
            {
                ApplyAndroidSettings();
            }
            
            // Scene list
            if (SceneGUIDs == null || SceneGUIDs.Count == 0)
            {
                EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            }
            else
            {
                EditorBuildSettings.scenes = SceneGUIDs.Select(guid => new EditorBuildSettingsScene(new GUID(guid), true)).ToArray();
            }

            return true;
        }

        /// <summary>
        /// Build an App Bundle or APK, and for which CPU architectures. Architectures left empty keep the project's.
        /// </summary>
        internal void ApplyAndroidSettings()
        {
            EditorUserBuildSettings.buildAppBundle = AndroidBuildAppBundle;
            if (AndroidTargetArchitectures != AndroidArchitecture.None)
            {
                PlayerSettings.Android.targetArchitectures = AndroidTargetArchitectures;
            }
        }

        public BuildTarget CalculateTarget()
        {
            List<BuildTarget> targets = BuildUtils.ValidTargetsForPlatform(TargetPlatform);
            if (targets.Count > 0)
            {
                return targets[0];
            }

            return BuildTarget.NoTarget;
        }

        public string GetFormattedProductName(Context ctx)
        {
            return ctx.FormatString(ProductName);
        }
        
        /// <summary>
        /// Loads all Build Configs and returns one with same GUID that exists in Project/BuildUploader/BuildConfigs.json
        /// </summary>
        public static BuildConfig FromGUID(string guid)
        {
            if (BuildConfigsUIUtils.TryLoadFromGUID(guid, out BuildConfig buildConfig))
            {
                return buildConfig;
            }
            
            return null;
        }
        
        /// <summary>
        /// Loads all Build Configs and returns one with same BuildName that exists in Project/BuildUploader/BuildConfigs.json
        /// </summary>
        public static BuildConfig FromBuildName(string name)
        {
            if (BuildConfigsUIUtils.TryLoadFromBuildName(name, out BuildConfig buildConfig))
            {
                return buildConfig;
            }
            
            return null;
        }
    }
}