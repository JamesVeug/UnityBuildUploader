using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Wireframe
{
    /// <summary>
    /// Download a build from UnityCloud
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(UnityCloudSource), "sources", "Downloads a zipped build from Unity Cloud")]
    [UploadSource("UnityCloud", "Choose Unity Cloud Build...", true)]
    public partial class UnityCloudSource : AUploadSource
    {
        [Wiki("Target", "Which Build Target to select a build from off Unity Cloud. eg: Windows/Mac")]
        private UnityCloudTarget sourceTarget;
        
        [Wiki("Build", "Which Build to download.")]
        private UnityCloudBuild sourceBuild;

        private Vector2 buildScrollPosition;

        private string downloadedFilePath;
        private string sourceFilePath;

        public UnityCloudSource()
        {
            // Required for reflection
        }
        
        public UnityCloudSource(UnityCloudTarget target, UnityCloudBuild build)
        {
            sourceTarget = target;
            sourceBuild = build;
        }

        public override async Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig,
            UploadTaskReport.StepResult stepResult, CancellationTokenSource token)
        {
            // Preparing
            string buildName = sourceBuild.platform + "-" + sourceBuild.buildtargetid + "-" + sourceBuild.build;
            string directoryPath = doNotCache ? m_taskContentsFolder : Path.Combine(Preferences.CacheFolderPath, "UnityCloudBuilds");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            downloadedFilePath = Path.Combine(directoryPath, buildName + ".zip");

            // Only download if we don't have it
            if (!File.Exists(downloadedFilePath))
            {
                string downloadUrl = sourceBuild.GetGameArtifactDownloadUrl();
                if (downloadUrl == null)
                {
                    downloadUrl = sourceBuild.GetAddressableArtifactDownloadUrl();
                    if (downloadUrl == null)
                    {
                        stepResult.SetFailed("Could not download UnityCloudBuild. No artifacts in build!");
                        return false;
                    }
                }

                stepResult.AddLog("Downloading from: " + RequestWrapper.MaskQuery(downloadUrl));

                using (RequestWrapper request = RequestWrapper.Get(downloadUrl, RequestWrapper.MaskQuery(downloadUrl)))
                {
                    var response = await request.SendAsync(stepResult, true);
                    if (!response.IsSuccessful)
                    {
                        stepResult.SetFailed("Failed to download UnityCloudBuild");
                        return false;
                    }

                    try
                    {
                        // Save as a file
                        await IOUtils.WriteAllBytesAsync(downloadedFilePath, response.Bytes);
                    }
                    catch (Exception e)
                    {
                        stepResult.SetFailed("Failed to save downloaded file: " + downloadedFilePath + "\n" +
                                             e.Message);
                        return false;
                    }
                }
            }
            else
            {
                stepResult.AddLog("Skipping downloading form UnityCloud since it already exists: " + downloadedFilePath);
            }

            stepResult.AddLog("Retrieved UnityCloud Build: " + downloadedFilePath);

            // Record where the game is saved to
            sourceFilePath = downloadedFilePath;
            return true;
        }

        public override Task CleanUp(int configIndex, UploadTaskReport.StepResult stepResult)
        {
            base.CleanUp(configIndex, stepResult);
            if (File.Exists(downloadedFilePath) && !Utils.ComparePaths(m_taskContentsFolder, downloadedFilePath))
            {
                try
                {
                    stepResult.AddLog("Deleting cached file: " + downloadedFilePath);
                    File.Delete(downloadedFilePath);
                }
                catch (IOException e)
                {
                    stepResult.AddError("Failed to delete file: " + downloadedFilePath + "\n" + e.Message);
                }
            }
            
            return Task.CompletedTask;
        }

        public override string SourceFilePath()
        {
            return sourceFilePath;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            UnityCloudService service = InternalUtils.GetService<UnityCloudService>();
            if (!service.IsReadyToStartBuild(out GUIContent reason))
            {
                errors.Add(reason);
            }

            if (sourceBuild == null)
            {
                errors.Add(new GUIContent("No build selected"));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["sourceTarget"] = sourceTarget?.name,
                ["sourceBuild"] = sourceBuild?.Id
            };

            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            string sourceTargetName = (string)data["sourceTarget"];

            List<UnityCloudTarget> buildTargets = UnityCloudAPI.CloudBuildTargets;
            if (buildTargets != null)
            {
                for (int i = 0; i < buildTargets.Count; i++)
                {
                    if (buildTargets[i].name == sourceTargetName)
                    {
                        sourceTarget = buildTargets[i];
                        List<UnityCloudBuild> builds = UnityCloudAPI.GetBuildsForTarget(sourceTarget);
                        if (builds != null)
                        {
                            for (int j = 0; j < builds.Count; j++)
                            {
                                if (builds[j].Id == (long)data["sourceBuild"])
                                {
                                    sourceBuild = builds[j];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            Dictionary<string, object> state = NewRunState();
            state["downloadedFilePath"] = downloadedFilePath;
            state["sourceFilePath"] = sourceFilePath;
            return state;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            base.RestoreRunState(state);
            downloadedFilePath = state["downloadedFilePath"] as string;
            sourceFilePath = state["sourceFilePath"] as string;
        }
    }
}