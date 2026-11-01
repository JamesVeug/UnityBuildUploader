using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
    /// <summary>
    /// Starts a new build using a BuildConfig.
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(BuildConfigSource), "sources", "Chooses a BuildConfig to start a new build when uploading")]
    [UploadSource("BuildConfig", "Build Config", false)]
    public partial class BuildConfigSource : ABuildSource<BuildConfig>
    {
        [Wiki("Override Target Platform", "If enabled, the target platform and architecture specified below will be used instead of the one in the BuildConfig")]
        protected bool m_OverrideSwitchTargetPlatform;
        
        [WikiEnum("Target Platform", "The target platform to switch to before building. Only used if 'Override Switch Target Platform' is enabled.", false)]
        protected BuildTarget m_Target;
        
        [Wiki("Target Architecture", "The target architecture to build for. Only used if 'Override Switch Target Platform' is enabled and the target platform supports multiple architectures.")]
        protected BuildUtils.Architecture m_TargetArchitecture;
        
        
        // Also serialized but not exposed to WIKI
        protected BuildTargetGroup m_TargetPlatform;
        protected int m_TargetPlatformSubTarget;
        
        public BuildConfigSource()
        {
            // Required for reflection
        }
        
        public BuildConfigSource(BuildConfig buildConfig, bool cleanBuild = false)
        {
            m_BuildConfig = buildConfig;
            m_CleanBuild = cleanBuild;
        }
        
        public void SetPlatformOverride(BuildTargetGroup targetPlatform, int targetPlatformSubTarget, BuildTarget target, BuildUtils.Architecture architecture)
        {
            m_OverrideSwitchTargetPlatform = true;
            m_TargetPlatform = targetPlatform;
            m_TargetPlatformSubTarget = targetPlatformSubTarget;
            m_Target = target;
            m_TargetArchitecture = architecture;
        }
        
        public override BuildConfig GetBuildConfigToApply()
        {
            var config = new BuildConfig();
            config.Deserialize(m_BuildConfig.Serialize());

            if (m_OverrideSwitchTargetPlatform)
            {
                config.Target = m_Target;
                config.TargetArchitecture = m_TargetArchitecture;
                config.TargetPlatform = m_TargetPlatform;
                config.TargetPlatformSubTarget = m_TargetPlatformSubTarget;
                config.SwitchTargetPlatform = true;
            }
            else if (!config.SwitchTargetPlatform)
            {
                config.Target = BuildUtils.CurrentTargetPlatform();
                config.TargetArchitecture = BuildUtils.CurrentTargetArchitecture();
                config.TargetPlatform = BuildUtils.BuildTargetToPlatform();
                config.TargetPlatformSubTarget = BuildUtils.CurrentSubTarget();
                config.SwitchTargetPlatform = true;
            }
            
            return config;
        }

        public override void SerializeBuildConfig(Dictionary<string, object> data)
        {
            data["BuildConfig"] = m_BuildConfig != null ? m_BuildConfig.GUID : "";
            data["OverrideSwitchTargetPlatform"] = m_OverrideSwitchTargetPlatform;
            data["TargetPlatform"] = m_TargetPlatform.ToString();
            data["TargetPlatformSubTarget"] = m_TargetPlatformSubTarget;
            data["Target"] = m_Target.ToString();
            data["TargetArchitecture"] = (int)m_TargetArchitecture;
        }

        public override void DeserializeBuildConfig(Dictionary<string, object> data)
        {
            if (data.TryGetValue("BuildConfig", out var buildConfigGuidObj) && buildConfigGuidObj is string guid)
            {
                if (!string.IsNullOrEmpty(guid))
                {
                    m_BuildConfig = BuildConfigsUIUtils.GetBuildConfigs().FirstOrDefault(a => a.GUID == guid);
                    if (m_BuildConfig == null)
                    {
                        Debug.LogWarning($"BuildConfig with GUID {guid} not found.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("BuildConfig GUID not found in serialized data.");
            }
            
            
            
            if (data.TryGetValue("OverrideSwitchTargetPlatform", out var overrideSwitchTargetPlatformObj) && overrideSwitchTargetPlatformObj is bool overrideSwitchTargetPlatform)
            {
                m_OverrideSwitchTargetPlatform = overrideSwitchTargetPlatform;
            }
            else
            {
                m_OverrideSwitchTargetPlatform = false;
            }
            
            if (data.TryGetValue("TargetPlatform", out var targetPlatformObj) && targetPlatformObj is string targetPlatformStr && Enum.TryParse<BuildTargetGroup>(targetPlatformStr, out var targetPlatform))
            {
                m_TargetPlatform = targetPlatform;
            }
            else
            {
                m_TargetPlatform = BuildTargetGroup.Standalone;
            }
            
            if (data.TryGetValue("TargetPlatformSubTarget", out var targetPlatformSubTargetObj) && (targetPlatformSubTargetObj is long || targetPlatformSubTargetObj is int))
            {
                m_TargetPlatformSubTarget = Convert.ToInt32(targetPlatformSubTargetObj);
            }
            else
            {
#if UNITY_2021_1_OR_NEWER
                m_TargetPlatformSubTarget = (int)StandaloneBuildSubtarget.Player;
#else
                m_TargetPlatformSubTarget = 0; // Player?
#endif
            }
            
            if (data.TryGetValue("Target", out var targetObj) && targetObj is string targetStr && Enum.TryParse<BuildTarget>(targetStr, out var target))
            {
                m_Target = target;
            }
            else
            {
                m_Target = BuildTarget.StandaloneWindows64;
            }
            
            // Saved as an int but read back from JSON as a long
            if (data.TryGetValue("TargetArchitecture", out var targetArchitectureObj) && (targetArchitectureObj is long || targetArchitectureObj is int))
            {
                m_TargetArchitecture = (BuildUtils.Architecture)Convert.ToInt32(targetArchitectureObj);
            }
            else
            {
                m_TargetArchitecture = BuildUtils.Architecture.x64;
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            Dictionary<string, object> state = base.CaptureRunState();

            // A config made in code, or one deleted since, can't be found by its GUID after a script reload
            state["buildConfig"] = m_BuildConfig?.Serialize();
            return state;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            if (m_BuildConfig == null && state.TryGetValue("buildConfig", out object buildConfig) && buildConfig is Dictionary<string, object> buildConfigData)
            {
                m_BuildConfig = new BuildConfig();
                m_BuildConfig.Deserialize(buildConfigData);
            }

            base.RestoreRunState(state);
        }

        protected override object SerializeBuildConfigToApply(BuildConfig config)
        {
            return config.Serialize();
        }

        protected override BuildConfig DeserializeBuildConfigToApply(object data)
        {
            BuildConfig config = new BuildConfig();
            config.Deserialize((Dictionary<string, object>)data);
            return config;
        }

        public override BuildTarget ResultingTarget()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTarget;
            
            if (m_OverrideSwitchTargetPlatform)
                return m_Target;
            
            return base.ResultingTarget();
        }

        public override BuildUtils.Architecture ResultingArchitecture()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetArchitecture;
            
            if (m_OverrideSwitchTargetPlatform)
                return m_TargetArchitecture;
            
            return base.ResultingArchitecture();
        }

        public override int ResultingTargetPlatformSubTarget()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetPlatformSubTarget;
            
            if (m_OverrideSwitchTargetPlatform)
                return m_TargetPlatformSubTarget;
            
            return base.ResultingTargetPlatformSubTarget();
        }

        public override BuildTargetGroup ResultingTargetGroup()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetPlatform;
            
            if (m_OverrideSwitchTargetPlatform)
                return m_TargetPlatform;
            
            return base.ResultingTargetGroup();
        }

        public override bool CompareBuildConfig(IBuildSource other)
        {
            if(other is BuildConfigSource otherSource)
                return otherSource.m_BuildConfig == m_BuildConfig;
            
            return false;
        }
    }
}