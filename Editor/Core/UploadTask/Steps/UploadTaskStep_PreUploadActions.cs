using System.Threading;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Execute actions before we begin the upload progress
    /// Each action is executed in order
    /// </summary>
    public class UploadTaskStep_PreUploadActions : AUploadTask_Step
    {
        public UploadTaskStep_PreUploadActions(Context context) : base(context)
        {
            
        }

        public override StepType Type => StepType.PreUploadActions;
        public override bool RequiresEverythingBeforeToSucceed => false;
        public override bool FireActions => false;

        public override async Task<bool> Run(UploadTask uploadTask, UploadTaskReport report,
            CancellationTokenSource token)
        {
            int preActionID = ProgressUtils.Start("Pre Upload Actions", "Executing Pre Upload Actions...");

            // False when an action set to stop the task failed, so the build and upload steps are skipped
            bool successful = await uploadTask.ExecuteTaskActions(uploadTask.IsSuccessful, UploadConfig.UploadActionData.UploadTrigger.OnTaskStarted);
            
            ProgressUtils.Remove(preActionID);
            return successful;
        }

        public override Task<bool> PostRunResult(UploadTask uploadTask, UploadTaskReport report, bool allStepsSuccessful)
        {
            return Task.FromResult(true);
        }
    }
}