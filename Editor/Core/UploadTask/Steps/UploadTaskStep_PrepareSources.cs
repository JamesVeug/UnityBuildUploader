using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Prepares all sources for upload so we can cache anything before the editor or any files change
    /// If any source fails to prepare then the entire upload task is aborted and we skip to cleanup
    /// </summary>
    public class UploadTaskStep_PrepareSources : AUploadTask_Step
    {
        public UploadTaskStep_PrepareSources(Context context) : base(context)
        {
            
        }

        public override StepType Type => StepType.PrepareSources;
        public override bool CanRerunAfterReload => true;
        
        public override async Task<bool> Run(UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            int progressId = ProgressUtils.Start(Type.ToString(), "Setting up...");
            List<UploadConfig> buildConfigs = uploadTask.UploadConfigs;
            
            var tasks = new List<Tuple<List<UploadConfig.SourceData>, Task<bool>>>();
            for (int j = 0; j < buildConfigs.Count; j++)
            {
                if (!buildConfigs[j].Enabled)
                {
                    continue;
                }

                Task<bool> task = PrepareSource(j, uploadTask, report, token);
                List<UploadConfig.SourceData> destinations = buildConfigs[j].Sources.Where(a => a.Enabled).ToList();
                tasks.Add(new Tuple<List<UploadConfig.SourceData>, Task<bool>>(destinations, task));
            }

            bool allSuccessful = true;
            while (true)
            {
                bool done = true;
                float completionAmount = 0.0f;
                int totalSources = 0;
                for (int j = 0; j < tasks.Count; j++)
                {
                    Tuple<List<UploadConfig.SourceData>, Task<bool>> tuple = tasks[j];
                    if (!tuple.Item2.IsCompleted)
                    {
                        done = false;
                        totalSources += tuple.Item1.Count;
                    }
                    else
                    {
                        allSuccessful &= tuple.Item2.Result;
                        completionAmount += tuple.Item1.Count;
                    }
                }

                if (done)
                {
                    break;
                }

                float progress = completionAmount / totalSources;
                ProgressUtils.Report(progressId, progress, "Waiting for sources to prepare...");
                await Task.Yield();
            }
            
            ProgressUtils.Remove(progressId);
            return allSuccessful;
        }
        
        private async Task<bool> PrepareSource(int configIndex, UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            UploadConfig uploadConfig = uploadTask.UploadConfigs[configIndex];
            UploadTaskReport.StepResult[] reports = report.NewReports(Type, uploadConfig.Sources.Count);
            m_stateResults.Add(new StateResult(uploadConfig, reports, (index) => uploadConfig.Sources[index].SourceType.DisplayName));
            
            for (var i = 0; i < uploadConfig.Sources.Count; i++)
            {
                var sourceData = uploadConfig.Sources[i];
                var result = reports[i];
                if (!sourceData.Enabled)
                {
                    result.AddLog("Skipping destination because it's disabled");
                    continue;
                }

                string cacheFolder = uploadTask.CachedLocations[configIndex];
                string taskContentsFolder = string.IsNullOrEmpty(sourceData.SubFolder) ? cacheFolder : Path.Combine(cacheFolder, sourceData.SubFolder);
                
                AUploadSource source = sourceData.Source;
                try
                {
                    bool success = await source.Prepare(taskContentsFolder, result, token);
                    if (!success)
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    result.AddException(e);
                    result.SetFailed("Failed to prepare destination: " + e.Message);
                    return false;
                }
                finally
                {
                    result.SetPercentComplete(1f);
                }
            }

            return true;
        }

        public override Task<bool> PostRunResult(UploadTask uploadTask, UploadTaskReport report,
            bool allStepsSuccessful)
        {
            // Do nothing
            return Task.FromResult(true);
        }
    }
}