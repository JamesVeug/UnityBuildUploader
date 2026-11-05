using System.Collections.Generic;

namespace Wireframe
{
    public partial class UploadTask
    {
        /// <summary>
        /// True when the task is a dry run. See SetDryRun.
        /// </summary>
        public bool DryRun { get; private set; }

        /// <summary>
        /// Makes the task a dry run. A dry run upload will make local changes but nothing will leave the local PC.
        /// Local changes: sources and modifiers run as normal, so builds are still made, and destinations and actions that only
        /// work locally (eg: Local Path) run as normal.
        /// Nothing leaves: destinations and actions with their own dry run (eg: Steam's Preview Upload) run with it forced on, so
        /// they only check the upload with their service. SkippedDuringDryRun skips the rest.
        /// Call before starting the task. The configs and actions it was given are never changed.
        /// </summary>
        public void SetDryRun(bool dryRun)
        {
            DryRun = dryRun;
        }

        /// <summary>
        /// Swaps every config and action for a copy and forces dry run on the copies.
        /// The copies are what checkpoints save, so skipped ones stay skipped when the task resumes.
        /// </summary>
        private void ApplyDryRun()
        {
            UploadTaskReport.StepResult log = m_report.NewReport(AUploadTask_Step.StepType.Validation);
            log.AddLog("Dry run: this makes local changes but nothing will leave the local PC. Destinations and actions with " +
                       "their own dry run run with it forced on and only check the upload. Local ones run as normal. The rest are skipped.");

            for (int i = 0; i < m_uploadConfigs.Count; i++)
            {
                UploadConfig copy = new UploadConfig();
                copy.Deserialize(Copy(m_uploadConfigs[i].Serialize()));
                copy.Context.SetParent(m_context);
                m_uploadConfigs[i] = copy;
            }

            for (int i = 0; i < m_actions.Count; i++)
            {
                UploadConfig.UploadActionData copy = new UploadConfig.UploadActionData();
                copy.Deserialize(Copy(m_actions[i].Serialize()));
                m_actions[i] = copy;
            }

            ForceDryRun(log);
            log.SetPercentComplete(1f);
        }

        /// <summary>
        /// Forces dry run on again for a task rebuilt from a checkpoint. A destination or action only keeps its Dry Run
        /// setting through a reload if its own Serialize saves it, and not all do, so it is set again here.
        /// </summary>
        private void ReapplyDryRun()
        {
            ForceDryRun(null);
        }

        /// <summary>
        /// Turns on dry run for each destination and action that has one or only works locally, and skips the rest.
        /// Logs what it does to log, unless log is null.
        /// </summary>
        private void ForceDryRun(UploadTaskReport.StepResult log)
        {
            for (int i = 0; i < m_uploadConfigs.Count; i++)
            {
                string configName = $"Config {i + 1}";
                foreach (UploadConfig.DestinationData destination in m_uploadConfigs[i].Destinations)
                {
                    ApplyDryRun(destination, configName, log);
                }

                foreach (UploadConfig.UploadActionData action in m_uploadConfigs[i].PostActions)
                {
                    ApplyDryRun(action, configName, log);
                }
            }

            foreach (UploadConfig.UploadActionData action in m_actions)
            {
                ApplyDryRun(action, "Task", log);
            }
        }

        private static void ApplyDryRun(UploadConfig.DestinationData destination, string owner, UploadTaskReport.StepResult log)
        {
            if (!destination.Enabled || destination.Destination == null)
            {
                return;
            }

            string name = destination.DestinationType?.DisplayName ?? destination.Destination.GetType().Name;
            if (!destination.Destination.SkippedDuringDryRun)
            {
                destination.Destination.SetDryRun(true);
                log?.AddLog($"{owner} destination [{name}] runs: it has a dry run or only works locally");
            }
            else
            {
                destination.Enabled = false;
                log?.AddLog($"{owner} destination [{name}] is skipped because it would send something off the local PC");
            }
        }

        private static void ApplyDryRun(UploadConfig.UploadActionData action, string owner, UploadTaskReport.StepResult log)
        {
            if (action.UploadAction == null || action.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never)
            {
                return;
            }

            string name = action.ActionType?.DisplayName ?? action.UploadAction.GetType().Name;
            if (!action.UploadAction.SkippedDuringDryRun)
            {
                action.UploadAction.SetDryRun(true);
                log?.AddLog($"{owner} action [{name}] runs: it has a dry run or only works locally");
            }
            else
            {
                action.WhenToExecute = UploadConfig.UploadActionData.UploadCompleteStatus.Never;
                log?.AddLog($"{owner} action [{name}] is skipped because it would send something off the local PC");
            }
        }

        /// <summary>
        /// Round trips saved data through JSON, as a saved profile or checkpoint loads, so Deserialize gets the types it
        /// expects (eg: whole numbers as long)
        /// </summary>
        private static Dictionary<string, object> Copy(Dictionary<string, object> data)
        {
            return JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(data));
        }
    }
}
