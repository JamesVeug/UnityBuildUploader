using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Starts a new build using a BuildConfig.
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    public abstract partial class ABuildSource<T> : AUploadSource, IBuildSource where T : IBuildConfig
    {
        public T BuildConfig => m_BuildConfig;

        [Wiki("BuildConfig", "Which config to use when creating a build")]
        protected T m_BuildConfig;
        
        [Wiki("Clean Build", "If enabled, the build folder will be deleted before building. This ensures a fresh build but may increase build time.")]
        protected bool m_CleanBuild;
        
        private string m_filePath = "";
        private bool m_appliedSettings;
        protected BuildMetaData m_buildMetaData;
        protected T m_buildConfigToApply;
        
        // Lock to 1 build at a time regardless of how many tasks/configs are running
        internal static T m_editorSettingsBeforeUpload;

        public ABuildSource()
        {
            // Required for reflection
        }
        
        public ABuildSource(T buildConfig, bool cleanBuild = false)
        {
            m_BuildConfig = buildConfig;
            m_CleanBuild = cleanBuild;
        }

        public override void PrepareContextForCaching()
        {
            base.PrepareContextForCaching();
            BuildUploaderProjectState.BumpBuildNumber();
        }

        public override Task<bool> Prepare(string taskContentsFolder, UploadTaskReport.StepResult stepResult, CancellationTokenSource token)
        {
            base.Prepare(taskContentsFolder, stepResult, token);
            
            // Create a new config since we make different changes at different times and this keeps it consistent
            m_buildConfigToApply = GetBuildConfigToApply();

            return Task.FromResult(true);
        }

        public abstract T GetBuildConfigToApply();
        public abstract bool CompareBuildConfig(IBuildSource other);

        public override async Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig,
            UploadTaskReport.StepResult stepResult,
            CancellationTokenSource token)
        {
            // Start build
            if (m_buildConfigToApply == null)
            {
                stepResult.SetFailed("No Build selected.");
                token.Cancel();
                return false;
            }
            
            // Ensure only 1 build happens at a time to avoid applying settings over each other
            await BuildUtils.WaitForTurnToBuild();
            if (token.IsCancellationRequested)
            {
                BuildUtils.ReleaseBuildLock();
                stepResult.AddLog("Build cancelled by user.");
                return false;
            }
            
            BuildReport report = null;
            try
            {
                m_buildMetaData = BuildUploaderProjectSettings.CreateFromContext(m_context);
                stepResult.AddLog("Build Number: " + m_buildMetaData.BuildNumber);

                m_filePath = GetBuiltDirectory(stepResult);
                if (string.IsNullOrEmpty(m_filePath))
                {
                    stepResult.SetFailed("Could not get built directory. Possible invalid build config or platform. Check you have the modules installed for the selected platform.");
                    token.Cancel();
                    return false;
                }

                if (m_CleanBuild && Directory.Exists(m_filePath))
                {
                    // Clear the directory if it exists
                    stepResult.AddLog($"Clean build set and build already exists so deleting to make a fresh build: {m_filePath}");
                    if (!await IOUtils.DeleteDirectory(m_filePath, true, stepResult))
                    {
                        stepResult.SetFailed("Failed to clear build directory. Check the console for more details.");
                        token.Cancel();
                        return false;
                    }
                }

                if (token.IsCancellationRequested)
                {
                    stepResult.AddLog("Build cancelled by user.");
                    token.Cancel();
                    return false;
                }

                if (!Directory.Exists(m_filePath))
                {
                    Directory.CreateDirectory(m_filePath);
                }

                // Cache current editor settings to restore later
                BuildUtils.ChangeRunningBuilds(true);
                m_appliedSettings = true;

                // Cache stuff to restore later
                PreApplyBuildConfig();

                if (!ApplyBuildConfig(m_buildConfigToApply, stepResult))
                {
                    token.Cancel();
                    return false;
                }

                // Wait for us to stop compiling
                if (!Application.isBatchMode)
                {
                    await Task.Yield();
                    
                    stepResult.AddLog("Waiting for compilation to finish...");
                    while (EditorApplication.isCompiling)
                    {
                        await Task.Yield();
                    }
                }


                // Get all enabled scenes in build settings
                BuildOptions buildOptions = m_buildConfigToApply.GetBuildOptions();
#if UNITY_2021_2_OR_NEWER
                buildOptions |= BuildOptions.DetailedBuildReport;
                if (m_CleanBuild)
                {
                    buildOptions |= BuildOptions.CleanBuildCache;
                }
#endif

                string productName = m_buildConfigToApply.GetFormattedProductName(m_context);
                string extension = m_buildConfigToApply.GetProductExtension();

                List<string> sceneGUIDs = m_buildConfigToApply.GetSceneGUIDs;
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = sceneGUIDs.Select(SceneUIUtils.GetPathFromGUID).Where(a=>!string.IsNullOrEmpty(a)).ToArray(),
                    locationPathName = Path.Combine(m_filePath, productName + extension),
                    targetGroup = m_buildConfigToApply.GetTargetPlatform,
                    target = m_buildConfigToApply.GetTarget,
                    options = buildOptions,
                };
            
                stepResult.AddLog("Starting build with the following options:");
                stepResult.AddLog($"Scenes: {string.Join(", ", options.scenes)}");
                stepResult.AddLog($"Location: {options.locationPathName}");
                stepResult.AddLog($"Target Group: {options.targetGroup}");
                stepResult.AddLog($"Target: {options.target}");
                stepResult.AddLog($"Build Options: {options.options}");

                // Ensure we are outside the player loop (e.g., OnGUI or Update) before building
                stepResult.AddLog("Starting build...");
                var tcs = new TaskCompletionSource<BuildReport>();
                if (Application.isBatchMode)
                {
                    try
                    {
                        tcs.SetResult(MakeBuild(options, stepResult));
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                }
                else
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            tcs.SetResult(MakeBuild(options, stepResult));
                        }
                        catch (Exception e)
                        {
                            tcs.SetException(e);
                        }
                    };
                }

                // Build the player
                Stopwatch stopwatch = Stopwatch.StartNew();
                report = await tcs.Task;
                stopwatch.Stop();
                stepResult.AddLog($"Build completed in {stopwatch.ElapsedMilliseconds} ms ({report.summary.result})");
                if (report.summary.result == BuildResult.Succeeded)
                {
                    LastBuildUtil.SetLastBuild(m_filePath, m_context.FormatString(Context.BUILD_NAME_KEY));
                    if (BuildUploaderProjectSettings.Instance.IncludeBuildMetaDataInStreamingDataFolder)
                    {
                        stepResult.AddLog("Saving build meta data to StreamingAssets folder");
                        BuildUploaderProjectSettings.SaveToStreamingAssets(m_buildMetaData, options, report.summary.outputPath);
                    }
                }
            }
            catch (Exception e)
            {
                stepResult.AddException(e);
                stepResult.SetFailed("Build failed - " + e.Message);
                token.Cancel();
                return false;
            }
            finally
            {
                BuildUtils.ReleaseBuildLock();
                stepResult.SetPercentComplete(1f);
            }

            
            foreach (var step in report.steps)
            {
                stepResult.AddLog($"Step: {step.name}");
                stepResult.AddLog($"Duration: {step.duration}");
                foreach (BuildStepMessage message in step.messages)
                {
                    switch (message.type)
                    {
                        case LogType.Error:
                            stepResult.AddError(message.content);
                            break;
                        case LogType.Assert:
                            stepResult.AddLog(message.content);
                            break;
                        case LogType.Warning:
                            stepResult.AddWarning(message.content);
                            break;
                        case LogType.Log:
                            stepResult.AddLog(message.content);
                            break;
                        case LogType.Exception:
                            stepResult.AddError(message.content);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            
            if (report.summary.result == BuildResult.Succeeded)
            {
                stepResult.AddLog($"Build succeeded: {report.summary.totalSize} bytes");
                stepResult.AddLog($"Build path: {report.summary.outputPath}");
                return true;
            }

            string summarizeErrors = Utils.SummarizeErrors(report);
            stepResult.AddError(summarizeErrors);
            stepResult.SetFailed(summarizeErrors);
            token.Cancel();
            return false;
        }

        protected virtual void PreApplyBuildConfig()
        {
            
        }

        protected string GetBuiltDirectory(UploadTaskReport.StepResult stepResult = null)
        {
            stepResult?.AddLog($"Building to {m_taskContentsFolder}");
            return m_taskContentsFolder;
        }

        public virtual bool ApplyBuildConfig(T config, UploadTaskReport.StepResult stepResult)
        {
            stepResult?.AddLog($"Applying settings");
            if (!config.ApplySettings(config.GetSwitchTargetPlatform, m_context, stepResult))
            {
                stepResult?.SetFailed("Failed to apply build settings. Check the console for more details.");
                return false;
            }
            
            stepResult?.AddLog($"Build settings applied");
            return true;
        }

        protected virtual BuildReport MakeBuild(BuildPlayerOptions options, UploadTaskReport.StepResult stepResult)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            return report;
        }

        public override string SourceFilePath()
        {
            return m_filePath;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            if (ValidConfig(ref errors))
            {
                // Stop the user from starting multiple tasks with the same config
                // This is because paths can be shared between configs and builds and can can break an active task
                foreach (UploadTask a in UploadTask.AllTasks)
                {
                    if (a.IsComplete) continue;
                    if (a.CurrentStepType == AUploadTask_Step.StepType.Validation) continue;
                    
                    foreach (UploadConfig b in a.UploadConfigs)
                    {
                        if(!b.Enabled) continue;
                        
                        foreach (UploadConfig.SourceData s in b.Sources)
                        {
                            if (!s.Enabled) continue;
                            if (!(s.SourceType.Type != GetType())) continue;
                            if (s.Source == this) continue;
                            if (!(s.Source is IBuildSource otherBuildSource)) continue;
                            
                            if (otherBuildSource.CompareBuildConfig(this))
                            {
                                errors.Add(new GUIContent($"Build '{m_BuildConfig.GetBuildName}' is already used in another active Upload Task."));
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool ValidConfig(ref List<GUIContent> errors)
        {
            T config = m_BuildConfig;
            if(config == null)
            {
                errors.Add(new GUIContent("No BuildConfig selected."));
                return false;
            }
            
            if (!config.TryGetErrors(ref errors))
            {
                return false;
            }

            // Check scene files exist
            if (config.GetSceneGUIDs.Count == 0)
            {
                errors.Add(new GUIContent("No scenes selected"));
                return false;
            }
            else
            {
                string[] sceneGUIDs = SceneUIUtils.GetSceneGUIDS();
                List<string> invalidScenes = new List<string>();
                foreach (string scene in config.GetSceneGUIDs)
                {
                    if (Array.IndexOf(sceneGUIDs, scene) == -1)
                    {
                        invalidScenes.Add($"'{scene}' not found.");
                    }
                }

                if (invalidScenes.Count > 0)
                {
                    errors.Add(new GUIContent($"Invalid scene guids: {string.Join(", ", invalidScenes)}"));
                    return false;
                }
            }

            BuildTarget target = ResultingTarget();
            BuildTargetGroup targetGroup = ResultingTargetGroup();
            int subTarget = ResultingTargetPlatformSubTarget();

            BuildPlatform buildPlatform = BuildUtils.GetBuildPlatform(targetGroup, target, subTarget);
            if (buildPlatform == null)
            {
                errors.Add(new GUIContent($"The selected target platform {target} ({targetGroup}) is not valid."));
                return false;
            }
            
            if (!buildPlatform.installed)
            {
                errors.Add(new GUIContent($"The selected target platform {buildPlatform.DisplayName} is not installed. Use Unity Hub to install the module."));
                return false;
            }
            
            if (!buildPlatform.supported)
            {
                errors.Add(new GUIContent($"The selected target platform {buildPlatform.DisplayName} is not supported by Unity. Not installed or deprecated in this version of Unity?\nUse Unity Hub to install the module."));
                return false;
            }
            
            return true;
        }

        public sealed override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "CleanBuild", m_CleanBuild },
            };
            SerializeBuildConfig(data);
            return data;
        }

        public abstract void SerializeBuildConfig(Dictionary<string, object> data);
        public abstract void DeserializeBuildConfig(Dictionary<string, object> data);

        public sealed override void Deserialize(Dictionary<string, object> data)
        {
            DeserializeBuildConfig(data);
            
            if (data.TryGetValue("CleanBuild", out var cleanBuildObj) && cleanBuildObj is bool cleanBuild)
            {
                m_CleanBuild = cleanBuild;
            }
            else
            {
                m_CleanBuild = false;
            }
        }

        public override async Task CleanUp(int configIndex, UploadTaskReport.StepResult stepResult)
        {
            await base.CleanUp(configIndex, stepResult);

            m_buildConfigToApply = default;
            m_buildMetaData = null;
            if (!m_appliedSettings)
            {
                stepResult.AddLog("No settings were applied so no need to restore previous settings.");
                return;
            }

            // Wait in case anything else is trying to apply settings or make a build
            await BuildUtils.WaitForTurnToBuild();
            
            try
            {
                m_appliedSettings = false;
                int remaining = BuildUtils.ChangeRunningBuilds(false);
                if (remaining > 0)
                {
                    // Another build or task is active and hasn't been cleaned up yet
                    stepResult.AddLog("Another build is still in progress so not restoring settings yet.");
                    return;
                }
                
                if (m_editorSettingsBeforeUpload == null)
                {
                    stepResult.AddLog("No previous editor settings to restore.");
                    return;
                }
                
                T buildConfig = m_editorSettingsBeforeUpload;
                m_editorSettingsBeforeUpload = default;

                stepResult.AddLog($"Restoring previous editor settings... {buildConfig.GetTargetPlatform} ({buildConfig.GetTargetPlatformSubTarget}) {buildConfig.GetTarget} {buildConfig.GetTargetArchitecture}");
                bool successful = buildConfig.ApplySettings(true, m_context);
                if (!successful)
                {
                    stepResult.AddError("Failed to restore previous build settings!");
                }
                else
                {
                    stepResult.AddLog("Previous editor settings restored.");
                }
            }
            finally
            {
                BuildUtils.ReleaseBuildLock();
            }
        }

        public virtual BuildTarget ResultingTarget()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTarget;
            if (m_BuildConfig != null && m_BuildConfig.GetSwitchTargetPlatform)
                return m_BuildConfig.GetTarget;
            return BuildUtils.CurrentTargetPlatform();
        }

        public virtual BuildUtils.Architecture ResultingArchitecture()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetArchitecture;
            if (m_BuildConfig != null && m_BuildConfig.GetSwitchTargetPlatform)
                return m_BuildConfig.GetTargetArchitecture;
            return BuildUtils.CurrentTargetArchitecture();
        }

        public virtual int ResultingTargetPlatformSubTarget()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetPlatformSubTarget;
            if (m_BuildConfig != null && m_BuildConfig.GetSwitchTargetPlatform)
                return m_BuildConfig.GetTargetPlatformSubTarget;
            return BuildUtils.CurrentSubTarget();
        }

        public virtual BuildTargetGroup ResultingTargetGroup()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetTargetPlatform;
            if (m_BuildConfig != null && m_BuildConfig.GetSwitchTargetPlatform)
                return m_BuildConfig.GetTargetPlatform;
            return BuildUtils.BuildTargetToPlatform();
        }

        public BuildPlatform ResultingPlatform()
        {
            BuildTargetGroup group = ResultingTargetGroup();
            BuildTarget target = ResultingTarget();
            int subTarget = ResultingTargetPlatformSubTarget();
            return BuildUtils.GetBuildPlatform(group, target, subTarget);
        }

        public string ResultingBuildName()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetBuildName;
            
            if (m_BuildConfig != null)
                return m_BuildConfig.GetBuildName;
            
            return "";
        }

        public string ResultingGUID()
        {
            if (m_buildConfigToApply != null)
                return m_buildConfigToApply.GetGUID;
            
            if (m_BuildConfig != null)
                return m_BuildConfig.GetGUID;
            
            return "";
        }

    }
}
