using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Cleanup step to remove any cached files or data to avoid dirtying up the user's computer
    /// This step always runs as a last step - even if previous steps failed
    /// </summary>
    public class UploadTaskStep_Validate : AUploadTask_Step
    {
        public UploadTaskStep_Validate(Context ctx) : base(ctx)
        {
        }

        public override StepType Type => StepType.Validation;
        public override bool CanRerunAfterReload => true;
        public override bool RequiresEverythingBeforeToSucceed => false;
        public override bool FireActions => false;

        public override async Task<bool> Run(UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            report.SetProcess(StepProcess.Intra);

            bool valid = true;
            
            UploadTaskReport.StepResult[] reports = report.NewReports(StepType.Validation, uploadTask.UploadConfigs.Count);
            for (var i = 0; i < uploadTask.UploadConfigs.Count; i++)
            {
                UploadTaskReport.StepResult result = reports[i];
                UploadConfig config = uploadTask.UploadConfigs[i];
                if (!config.Enabled)
                {
                    continue;
                }
                
                // Create Cached location
                string cacheFolderPath = "";
                if (Preferences.UseLocalDestinationIfAvailable && GetFirstLocalDestination(config, out LocalPathDestination destination))
                {
                    cacheFolderPath = destination.FullPath();
                }
                else
                {
                    string sanitisedName = InternalEditorUtility.RemoveInvalidCharsFromFileName(uploadTask.UploadName, false);
                    cacheFolderPath = Path.Combine(Preferences.CacheFolderPath, "UploadTasks", $"{sanitisedName} ({uploadTask.GUID})", config.GUID);
                    uploadTask.CachedLocationNeedsCleaning[i] = true;
                }
                
                bool pathAlreadyExists = Directory.Exists(cacheFolderPath);
                if (pathAlreadyExists)
                {
                    result.AddWarning($"Cached folder already exists: {cacheFolderPath}." +
                                      $"\nLikely it wasn't cleaned up properly in an older build." +
                                      $"\nDeleting now to avoid accidentally uploading the same build!");
                    if (!await IOUtils.DeleteDirectory(cacheFolderPath, true, result))
                    {
                        result.SetFailed($"Unable to clear cached folder: {cacheFolderPath}");
                        valid = false;
                    }
                }

                Directory.CreateDirectory(cacheFolderPath);
                uploadTask.CachedLocations[i] = cacheFolderPath;
                
                List<GUIContent> errors = config.GetAllErrors();
                if (errors.Count > 0)
                {
                    foreach (GUIContent error in errors)
                    {
                        result.SetFailed(string.IsNullOrEmpty(error.tooltip) ? error.text : error.text + ": " + error.tooltip);
                        valid = false;
                    }
                }
                else
                {
                    result.AddLog("No errors found in config: " + config.GUID);
                }
                
                List<GUIContent> warnings = config.GetAllWarnings();
                if (warnings.Count > 0)
                {
                    foreach (GUIContent warning in warnings)
                    {
                        result.AddWarning(warning.text + ": " + warning.tooltip);
                    }
                }
            }
            
            reports = report.NewReports(StepType.Validation, uploadTask.Actions.Count);
            for (var i = 0; i < uploadTask.Actions.Count; i++)
            {
                var action = uploadTask.Actions[i];
                if (action.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never)
                {
                    continue;
                }

                UploadTaskReport.StepResult result = reports[i];
                if (action.UploadAction == null)
                {
                    result.SetFailed($"No pre upload action specified at index {i}");
                    valid = false;
                    continue;
                }

                List<GUIContent> errors = new List<GUIContent>();
                action.UploadAction.TryGetErrors(errors);
                foreach (GUIContent error in errors)
                {
                    result.SetFailed(string.IsNullOrEmpty(error.tooltip) ? error.text : error.text + ": " + error.tooltip);
                    valid = false;
                }
            }

            return valid;
        }

        private bool GetFirstLocalDestination(UploadConfig config, out LocalPathDestination destination)
        {
            foreach (UploadConfig.DestinationData destinationData in config.Destinations)
            {
                if (destinationData.Destination is LocalPathDestination localPathDestination)
                {
                    if (!localPathDestination.IsZippingContents)
                    {
                        destination = destinationData.Destination as LocalPathDestination;
                        return true;
                    }
                }
            }
            
            destination = null;
            return false;
        }

        public override Task<bool> PostRunResult(UploadTask uploadTask, UploadTaskReport report, bool allStepsSuccessful)
        {
            return Task.FromResult(true);
        }
    }
}