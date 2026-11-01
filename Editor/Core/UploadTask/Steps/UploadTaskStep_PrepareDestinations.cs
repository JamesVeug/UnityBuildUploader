using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Prepares all destinations for upload
    /// If any destination fails to prepare, the entire upload task fails and we skip to cleanup
    /// </summary>
    public class UploadTaskStep_PrepareDestinations : AUploadTask_Step
    {
        public UploadTaskStep_PrepareDestinations(Context context) : base(context)
        {
            
        }

        public override StepType Type => StepType.PrepareDestinations;
        public override bool CanRerunAfterReload => true;
        
        public override async Task<bool> Run(UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            int progressId = ProgressUtils.Start(Type.ToString(), "Setting up...");
            List<UploadConfig> buildConfigs = uploadTask.UploadConfigs;
            
            var tasks = new List<Tuple<List<UploadConfig.DestinationData>, Task<bool>>>();
            for (int j = 0; j < buildConfigs.Count; j++)
            {
                if (!buildConfigs[j].Enabled)
                {
                    continue;
                }

                Task<bool> task = PrepareDestination(j, uploadTask, report);
                List<UploadConfig.DestinationData> destinations = buildConfigs[j].Destinations.Where(a => a.Enabled).ToList();
                tasks.Add(new Tuple<List<UploadConfig.DestinationData>, Task<bool>>(destinations, task));
            }

            bool allSuccessful = true;
            while (true)
            {
                bool done = true;
                float completionAmount = 0.0f;
                int totalDestinations = 0;
                for (int j = 0; j < tasks.Count; j++)
                {
                    Tuple<List<UploadConfig.DestinationData>, Task<bool>> tuple = tasks[j];
                    if (!tuple.Item2.IsCompleted)
                    {
                        done = false;
                        totalDestinations += tuple.Item1.Count;
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

                float progress = completionAmount / totalDestinations;
                ProgressUtils.Report(progressId, progress, "Waiting for destinations to prepare...");
                await Task.Yield();
            }
            
            ProgressUtils.Remove(progressId);
            return allSuccessful;
        }
        
        private async Task<bool> PrepareDestination(int configIndex, UploadTask uploadTask, UploadTaskReport report)
        {
            UploadConfig uploadConfig = uploadTask.UploadConfigs[configIndex];
            string cachePath = uploadTask.CachedLocations[configIndex];
            UploadTaskReport.StepResult[] reports = report.NewReports(Type, uploadConfig.Destinations.Count);
            m_stateResults.Add(new StateResult(uploadConfig, reports, (index) => uploadConfig.Destinations[index].DestinationType.DisplayName));
            
            for (var i = 0; i < uploadConfig.Destinations.Count; i++)
            {
                var destination = uploadConfig.Destinations[i];
                var result = reports[i];
                if (!destination.Enabled)
                {
                    result.SetSkipped("Skipping destination because it's disabled");
                    continue;
                }

                
                AUploadDestination uploadDestination = destination.Destination;
                try
                {
                    bool success = await uploadDestination.Prepare(uploadTask.GUID, configIndex, i, cachePath, result);
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