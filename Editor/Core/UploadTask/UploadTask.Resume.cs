using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// A script reload (for example a BuildConfig changing the scripting defines) destroys every running task.
    /// A running task saves a checkpoint before each step and after each source it gets, and UploadTaskResumer
    /// rebuilds it from that checkpoint once scripts have reloaded.
    /// </summary>
    public partial class UploadTask
    {
        /// <summary>
        /// A step interrupted this many times fails instead of running again, so a package that keeps changing the
        /// scripting defines can't reload scripts forever
        /// </summary>
        internal const int MaxInterruptions = 3;

        // Tasks running in this domain, including ones never added to AllTasks
        private static readonly HashSet<string> s_liveTasks = new HashSet<string>();

        internal enum CheckpointPhase
        {
            Running, // The step is running
            Actions, // The step finished and its AfterEachStepCompletes actions are running
        }

        /// <summary>
        /// The running task, so a task resumed after a script reload can be awaited too
        /// </summary>
        internal Task RunTask { get; private set; }

        /// <summary>
        /// Started from the Upload window, so its completion dialog is shown again after resuming
        /// </summary>
        internal bool StartedFromWindow { get; set; }

        private string m_checkpointFolder;
        private AUploadTask_Step.StepType m_checkpointStep;
        private CheckpointPhase m_checkpointPhase;
        private int m_interruptions;
        private readonly HashSet<string> m_completedSources = new HashSet<string>();
        private bool m_debugForceStopTask;

        internal static bool IsRunning(string taskGUID)
        {
            return s_liveTasks.Contains(taskGUID);
        }

        internal bool IsSourceCompleted(int configIndex, int sourceIndex)
        {
            return m_completedSources.Contains(SourceKey(configIndex, sourceIndex));
        }

        /// <summary>
        /// Saves that a source finished, so it isn't fetched or built again if scripts reload later in GetSources
        /// </summary>
        internal void SourceCompleted(int configIndex, int sourceIndex)
        {
            m_completedSources.Add(SourceKey(configIndex, sourceIndex));
            WriteCheckpoint();
        }

        private static string SourceKey(int configIndex, int sourceIndex)
        {
            return configIndex + ":" + sourceIndex;
        }

        /// <summary>
        /// For tests: stops the task where it is without finishing it, as a script reload would.
        /// A report write already in progress still finishes; wait for ReportWrite before writing the same file.
        /// </summary>
        internal void Abandon()
        {
            m_debugForceStopTask = true;
            s_liveTasks.Remove(m_guid);
        }

        internal Task ReportWrite => m_reportWrite;

        private void Checkpoint(AUploadTask_Step.StepType step, CheckpointPhase phase)
        {
            if (step != m_checkpointStep)
            {
                // Kept while the same step runs again after a resume
                m_checkpointStep = step;
                m_interruptions = 0;
                m_completedSources.Clear();
            }

            m_checkpointPhase = phase;
            WriteCheckpoint();
        }

        private void WriteCheckpoint()
        {
            if (m_debugForceStopTask)
            {
                return;
            }

            try
            {
                UploadTaskCheckpoints.Write(m_checkpointFolder, m_guid, CreateCheckpoint());
            }
            catch (Exception e)
            {
                // A warning, not an error: the task carries on, it just can't resume after a script reload
                Debug.LogWarning($"[BuildUploader] Could not save upload task '{m_uploadName}' to resume it after a script reload: {e.Message}");
            }

            WriteReportFile();
        }

        private Dictionary<string, object> CreateCheckpoint()
        {
            List<object> configs = new List<object>();
            List<object> configStates = new List<object>();
            foreach (UploadConfig config in m_uploadConfigs)
            {
                configs.Add(config.Serialize());
                configStates.Add(new Dictionary<string, object>
                {
                    ["context"] = config.Context.GetCachedValues(),
                    ["sources"] = config.Sources.Select(a => ComponentState(a.Source?.Context, a.Source?.CaptureRunState())).ToList(),
                    ["modifiers"] = config.Modifiers.Select(a => ComponentState(a.Modifier?.Context, null)).ToList(),
                    ["destinations"] = config.Destinations.Select(a => ComponentState(a.Destination?.Context, a.Destination?.CaptureRunState())).ToList(),
                    ["actions"] = config.PostActions.Select(a => ComponentState(a.UploadAction?.Context, a.UploadAction?.CaptureRunState())).ToList(),
                });
            }

            return new Dictionary<string, object>
            {
                ["version"] = UploadTaskCheckpoints.Version,
                ["sessionId"] = UploadTaskResumer.SessionId,
                ["guid"] = m_guid,
                ["name"] = m_uploadName,
                ["description"] = m_uploadDescription,
                ["startedFromWindow"] = StartedFromWindow,
                ["nextStep"] = m_checkpointStep.ToString(),
                ["stepPhase"] = m_checkpointPhase.ToString(),
                ["interruptions"] = m_interruptions,
                ["completedSources"] = m_completedSources.ToList(),
                ["isSuccessful"] = IsSuccessful,
                ["isCancelled"] = IsCancelled,
                ["cachedLocations"] = m_cachedLocations,
                ["cachedLocationNeedsCleaning"] = m_cachedLocationNeedsCleaning,
                ["configs"] = configs,
                ["actions"] = m_actions.Select(a => (object)a.Serialize()).ToList(),
                ["configStates"] = configStates,
                ["actionStates"] = m_actions.Select(a => ComponentState(a.UploadAction?.Context, a.UploadAction?.CaptureRunState())).ToList(),
                ["context"] = m_context.GetCachedValues(),
                ["report"] = m_report.Serialize(),
            };
        }

        private static object ComponentState(Context context, Dictionary<string, object> runState)
        {
            return new Dictionary<string, object>
            {
                ["context"] = context?.GetCachedValues(),
                ["runState"] = runState,
            };
        }

        /// <summary>
        /// Rebuilds a task saved by a running task. Throws if the configs no longer load the same, for example when a
        /// source's script was deleted, rather than resume with part of the pipeline missing.
        /// </summary>
        internal static UploadTask FromCheckpoint(Dictionary<string, object> data, string checkpointFolder)
        {
            List<UploadConfig> configs = new List<UploadConfig>();
            foreach (Dictionary<string, object> configData in (List<object>)data["configs"])
            {
                UploadConfig config = new UploadConfig();
                config.Deserialize(configData);
                ThrowIfMissing(configData, "sources", "sourceType", config.Sources.Select(a => (object)a.Source).ToList());
                ThrowIfMissing(configData, "allModifiers", "modifierType", config.Modifiers.Select(a => (object)a.Modifier).ToList());
                ThrowIfMissing(configData, "destinations", "destinationType", config.Destinations.Select(a => (object)a.Destination).ToList());
                ThrowIfMissing(configData, "actions", "actionType", config.PostActions.Select(a => (object)a.UploadAction).ToList());
                configs.Add(config);
            }

            List<UploadConfig.UploadActionData> actions = new List<UploadConfig.UploadActionData>();
            foreach (Dictionary<string, object> actionData in (List<object>)data["actions"])
            {
                UploadConfig.UploadActionData action = new UploadConfig.UploadActionData();
                action.Deserialize(actionData);
                actions.Add(action);
            }
            ThrowIfMissing(data, "actions", "actionType", actions.Select(a => (object)a.UploadAction).ToList());

            UploadTask task = new UploadTask((string)data["name"], configs, actions);
            task.m_guid = (string)data["guid"];
            task.m_uploadDescription = data["description"] as string ?? "";
            task.StartedFromWindow = (bool)data["startedFromWindow"];
            task.m_checkpointFolder = checkpointFolder;
            task.m_report = UploadTaskReport.Deserialize((Dictionary<string, object>)data["report"]);
            task.m_cachedLocations = ((List<object>)data["cachedLocations"]).Select(a => a as string).ToArray();
            task.m_cachedLocationNeedsCleaning = ((List<object>)data["cachedLocationNeedsCleaning"]).Select(a => (bool)a).ToArray();
            task.IsSuccessful = (bool)data["isSuccessful"];
            task.IsCancelled = (bool)data["isCancelled"];
            task.m_checkpointStep = (AUploadTask_Step.StepType)Enum.Parse(typeof(AUploadTask_Step.StepType), (string)data["nextStep"]);
            task.m_checkpointPhase = (CheckpointPhase)Enum.Parse(typeof(CheckpointPhase), (string)data["stepPhase"]);
            task.m_interruptions = Convert.ToInt32(data["interruptions"]);
            task.CurrentStepType = task.m_checkpointStep;
            foreach (object key in (List<object>)data["completedSources"])
            {
                task.m_completedSources.Add((string)key);
            }

            task.RestoreState(data);
            return task;
        }

        private static void ThrowIfMissing(Dictionary<string, object> data, string listKey, string typeKey, List<object> loaded)
        {
            List<object> saved = data.TryGetValue(listKey, out object list) ? list as List<object> : null;
            int savedCount = saved?.Count ?? 0;
            if (savedCount != loaded.Count)
            {
                throw new InvalidOperationException($"Expected {savedCount} {listKey} but {loaded.Count} loaded.");
            }

            for (int i = 0; i < savedCount; i++)
            {
                string typeName = ((Dictionary<string, object>)saved[i])[typeKey] as string;
                if (!string.IsNullOrEmpty(typeName) && loaded[i]?.GetType().FullName != typeName)
                {
                    throw new InvalidOperationException($"Could not load {typeName}. Was its script deleted or renamed?");
                }
            }
        }

        private void RestoreState(Dictionary<string, object> data)
        {
            // Parent every context the way Prepare does, then put back what it cached
            m_context.RestoreCachedValues((Dictionary<string, object>)data["context"]);
            List<object> configStates = (List<object>)data["configStates"];
            for (int i = 0; i < m_uploadConfigs.Count; i++)
            {
                UploadConfig config = m_uploadConfigs[i];
                config.SetContext(m_context);

                Dictionary<string, object> state = (Dictionary<string, object>)configStates[i];
                config.Context.RestoreCachedValues((Dictionary<string, object>)state["context"]);

                List<object> sources = (List<object>)state["sources"];
                for (int j = 0; j < config.Sources.Count; j++)
                {
                    AUploadSource source = config.Sources[j].Source;
                    Dictionary<string, object> runState = RestoreComponent(source?.Context, sources[j]);
                    if (runState != null)
                    {
                        source.RestoreRunState(runState);
                    }
                    else
                    {
                        // Without its run state, a source that already finished has to run again
                        m_completedSources.Remove(SourceKey(i, j));
                    }
                }

                List<object> modifiers = (List<object>)state["modifiers"];
                for (int j = 0; j < config.Modifiers.Count; j++)
                {
                    RestoreComponent(config.Modifiers[j].Modifier?.Context, modifiers[j]);
                }

                List<object> destinations = (List<object>)state["destinations"];
                for (int j = 0; j < config.Destinations.Count; j++)
                {
                    Dictionary<string, object> runState = RestoreComponent(config.Destinations[j].Destination?.Context, destinations[j]);
                    if (runState != null)
                    {
                        config.Destinations[j].Destination.RestoreRunState(runState);
                    }
                }

                List<object> actions = (List<object>)state["actions"];
                for (int j = 0; j < config.PostActions.Count; j++)
                {
                    Dictionary<string, object> runState = RestoreComponent(config.PostActions[j].UploadAction?.Context, actions[j]);
                    if (runState != null)
                    {
                        config.PostActions[j].UploadAction.RestoreRunState(runState);
                    }
                }
            }

            List<object> actionStates = (List<object>)data["actionStates"];
            for (int i = 0; i < m_actions.Count; i++)
            {
                AUploadAction action = m_actions[i].UploadAction;
                action?.Context.SetParent(m_context);
                Dictionary<string, object> runState = RestoreComponent(action?.Context, actionStates[i]);
                if (runState != null)
                {
                    action.RestoreRunState(runState);
                }
            }
        }

        /// <summary>
        /// Restores a component's cached context values and returns its run state, if it saved one
        /// </summary>
        private static Dictionary<string, object> RestoreComponent(Context context, object saved)
        {
            Dictionary<string, object> state = saved as Dictionary<string, object>;
            if (context == null || state == null)
            {
                return null;
            }

            if (state["context"] is Dictionary<string, object> cachedValues)
            {
                context.RestoreCachedValues(cachedValues);
            }

            return state["runState"] as Dictionary<string, object>;
        }

        /// <summary>
        /// Carries on a task rebuilt by FromCheckpoint from the step a script reload interrupted
        /// </summary>
        internal Task ResumeAsync()
        {
            RunTask = ResumeFromCheckpointAsync();
            return RunTask;
        }

        private async Task ResumeFromCheckpointAsync()
        {
            AUploadTask_Step[] steps = CreateSteps();
            int index = Array.FindIndex(steps, a => a.Type == m_checkpointStep);
            AUploadTask_Step step = steps[index];
            m_report.SetProcess(AUploadTask_Step.StepProcess.Intra);
            CloseInterruptedResults(step.Type);
            m_interruptions++;

            int startStep = index;
            if (m_checkpointPhase == CheckpointPhase.Actions)
            {
                m_report.NewReport(step.Type).AddWarning($"A script reload interrupted the actions that run after {step.Type}. They won't run again.");
                startStep = index + 1;
            }
            else if (m_interruptions >= MaxInterruptions)
            {
                FailInterruptedStep(step.Type, $"Scripts reloaded {m_interruptions} times during {step.Type}, so the task stopped. Another package may keep changing the scripting defines.");
                startStep = index + 1;
            }
            else if (!step.CanRerunAfterReload)
            {
                FailInterruptedStep(step.Type, $"A script reload interrupted {step.Type}. It can't be re-run safely, so the task stopped.");
                startStep = index + 1;
            }
            else
            {
                m_report.NewReport(step.Type).AddLog($"Scripts reloaded during {step.Type}, so it is running again.");
            }

            // Saved now so the next reload counts this one
            WriteCheckpoint();
            await RunAsync(steps, startStep);
        }

        private void FailInterruptedStep(AUploadTask_Step.StepType stepType, string reason)
        {
            UploadTaskReport.StepResult result = m_report.NewReport(stepType);
            result.SetFailed(reason);
            result.SetPercentComplete(1f);
            IsSuccessful = false;
        }

        /// <summary>
        /// Results the reload left unfinished would otherwise hold the step's progress below 100%
        /// </summary>
        private void CloseInterruptedResults(AUploadTask_Step.StepType stepType)
        {
            if (!m_report.StepResults.TryGetValue(stepType, out var processes))
            {
                return;
            }

            foreach (List<UploadTaskReport.StepResult> results in processes.Values)
            {
                foreach (UploadTaskReport.StepResult result in results)
                {
                    if (!result.IsComplete)
                    {
                        result.AddLog("Interrupted by a script reload");
                        result.SetPercentComplete(1f);
                    }
                }
            }
        }
    }
}
