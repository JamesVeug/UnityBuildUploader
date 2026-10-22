using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Uploads all cached files to all enabled upload destinations
    /// All uploads for all configs are done in parallel
    /// </summary>
    public class UploadTaskStep_Upload : AUploadTask_Step
    {
        public UploadTaskStep_Upload(Context context) : base(context)
        {
            
        }

        public override StepType Type => StepType.Upload;
        
        public override async Task<bool> Run(UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            List<UploadConfig> buildConfigs = uploadTask.UploadConfigs;

            var uploads = new List<Tuple<List<UploadConfig.DestinationData>, Task<bool>>>();
            for (int i = 0; i < buildConfigs.Count; i++)
            {
                if (!buildConfigs[i].Enabled)
                {
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    UploadTaskReport.StepResult cancelled = report.NewReport(Type);
                    cancelled.SetSkipped($"Skipping upload of Config {i + 1} because the task was cancelled");
                    continue;
                }

                List<UploadConfig.DestinationData> destinations = buildConfigs[i].Destinations.Where(a=>a.Enabled).ToList();
                List<UploadConfig.UploadActionData> actions = buildConfigs[i].PostActions.Where(a=>a.WhenToExecute != UploadConfig.UploadActionData.UploadCompleteStatus.Never).ToList();

                Task<bool> upload = Upload(uploadTask, i, destinations, actions, report, token);
                uploads.Add(new Tuple<List<UploadConfig.DestinationData>, Task<bool>>(destinations, upload));
            }
            
            bool allSuccessful = true;
            while (true)
            {
                bool done = true;
                for (int j = 0; j < uploads.Count; j++)
                {
                    var tuple = uploads[j];
                    if (!tuple.Item2.IsCompleted)
                    {
                        done = false;
                    }
                    else
                    {
                        allSuccessful &= tuple.Item2.Result;
                    }
                }

                if (done)
                {
                    break;
                }

                await Task.Yield();
            }

            return allSuccessful;
        }

        private async Task<bool> Upload(UploadTask uploadTask, int configIndex,
            List<UploadConfig.DestinationData> destinations,
            List<UploadConfig.UploadActionData> actions,
            UploadTaskReport report,
            CancellationTokenSource token)
        {
            bool success = await ExecuteDestinations(uploadTask, configIndex, destinations, report, token);

            bool actionsSuccessful = await uploadTask.ExecuteActions(success, null, actions, $"Post Config {configIndex} Action");

            return success && actionsSuccessful;
        }
        
        private async Task<bool> ExecuteDestinations(UploadTask uploadTask, int configIndex,
            List<UploadConfig.DestinationData> destinations,
            UploadTaskReport report,
            CancellationTokenSource token)
        {
            UploadConfig config = uploadTask.UploadConfigs[configIndex];
            int uploadID = ProgressUtils.Start(Type.ToString(), $"Uploading Config {configIndex + 1}");

            List<Task<bool>> uploadTasks = new List<Task<bool>>();
            UploadTaskReport.StepResult[] reports = report.NewReports(Type, destinations.Count);
            m_stateResults.Add(new StateResult(config, reports, (index) =>
            {
                string displayName = destinations[index].DestinationType.DisplayName;
                string summary = destinations[index].Destination.Summary();
                return $"[{displayName}] {summary}";
            }));
            
            for (var i = 0; i < destinations.Count; i++)
            {
                UploadConfig.DestinationData destinationData = destinations[i];
                UploadTaskReport.StepResult result = reports[i];
                if (!destinationData.Enabled)
                {
                    result.SetSkipped("Skipping upload because it's disabled");
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    result.SetSkipped("Skipping upload because the task was cancelled");
                    continue;
                }

                Task<bool> task = UploadDestinationWrapper(destinationData.Destination, result, config);
                uploadTasks.Add(task);
            }

            bool allSuccessful = true;
            while (true)
            {
                bool done = true;
                float completionAmount = 0.0f;
                for (int j = 0; j < uploadTasks.Count; j++)
                {
                    Task<bool> tuple = uploadTasks[j];
                    if (!tuple.IsCompleted)
                    {
                        done = false;
                    }
                    else
                    {
                        allSuccessful &= tuple.Result;
                        completionAmount++;
                    }
                }

                if (done)
                {
                    break;
                }

                float progress = completionAmount / destinations.Count;
                ProgressUtils.Report(uploadID, progress, "Waiting for all to be uploaded...");
                await Task.Yield();
            }
            
            ProgressUtils.Remove(uploadID);
            return allSuccessful;
        }

        private async Task<bool> UploadDestinationWrapper(AUploadDestination destinationDataDestination, UploadTaskReport.StepResult result, UploadConfig config)
        {
            try
            {
                return await destinationDataDestination.Upload(result);
            }
            catch (Exception e)
            {
                result.AddException(e);
                result.SetFailed("Upload failed: " + e.Message);
                return false;
            }
            finally
            {
                result.SetPercentComplete(1f);
            }
        }

        public override async Task<bool> PostRunResult(UploadTask uploadTask, UploadTaskReport report,
            bool allStepsSuccessful)
        {
            bool success = true;
            foreach (UploadConfig config in uploadTask.UploadConfigs)
            {
                if (!config.Enabled)
                {
                    continue;
                }

                UploadTaskReport.StepResult[] reports = report.NewReports(Type, config.Destinations.Count);
                for (var i = 0; i < config.Destinations.Count; i++)
                {
                    var destination = config.Destinations[i];
                    var result = reports[i];
                    if (!destination.Enabled)
                    {
                        result.SetSkipped("Skipping upload because it's disabled");
                        continue;
                    }

                    if (uploadTask.IsCancelled)
                    {
                        result.SetSkipped("Skipping post upload because the task was cancelled");
                        continue;
                    }

                    AUploadDestination uploadDestination = destination.Destination;
                    try
                    {
                        success &= await uploadDestination.PostUpload(result);
                    }
                    catch (Exception e)
                    {
                        result.AddException(e);
                        result.SetFailed("Post upload failed: " + e.Message);
                        success = false;
                    }
                    finally
                    {
                        result.SetPercentComplete(1f);
                    }
                }
            }
            
            return success;
        }
    }
}