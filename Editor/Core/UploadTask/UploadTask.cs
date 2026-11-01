using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// A Wrapper for a single upload
    /// Each task can contain multiple upload configs that will be executed in parallel
    /// When started, a Report is created that contains all live information about the upload
    /// Listen to OnComplete to get the report when the upload is done.
    /// </summary>
    public partial class UploadTask
    {
        internal static List<UploadTask> AllTasks = new List<UploadTask>();
        private static readonly TimeSpan ReportWriteInterval = TimeSpan.FromSeconds(5);
        
        public event Action<UploadTaskReport> OnComplete = delegate { };
        
        public string GUID => m_guid;
        public List<UploadConfig> UploadConfigs => m_uploadConfigs;
        public List<UploadConfig.UploadActionData> Actions => m_actions;
        public string UploadDescription => m_uploadDescription;
        public string UploadName => m_uploadName;
        public string[] CachedLocations => m_cachedLocations;
        public bool[] CachedLocationNeedsCleaning => m_cachedLocationNeedsCleaning;
        public UploadTaskReport Report => m_report;
        
        public bool IsComplete { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsSuccessful { get; private set; }
        public bool IsCancelled { get; private set; }
        public float PercentComplete { get; private set; }
        public AUploadTask_Step.StepType CurrentStepType { get; private set; }
        public AUploadTask_Step CurrentStep { get; private set; }
        public AUploadTask_Step[] CurrentSteps => m_currentSteps;
        
        public UploadTaskStringFormatterContext Context => m_context;

        private UploadTaskReport m_report;
        private List<UploadConfig> m_uploadConfigs;
        private List<UploadConfig.UploadActionData> m_actions;
        private UploadTaskStringFormatterContext m_context;
        private string[] m_cachedLocations;
        private bool[] m_cachedLocationNeedsCleaning;
        private int m_progressId;
        private int m_totalSteps;
        
        private string m_uploadName;
        private string m_uploadDescription;
        private string m_guid;
        private AUploadTask_Step[] m_currentSteps;
        private CancellationTokenSource m_cancellationToken;

        private Task m_reportWrite = Task.CompletedTask;
        private bool m_reportWritePending;
        private int m_reportChangesWritten = -1;
        private DateTime m_reportWrittenAt;

        public UploadTask(UploadProfile uploadProfile) : 
            this(uploadProfile.ProfileName, uploadProfile.UploadConfigs, uploadProfile.Actions)
        {
            
        }
        
        public UploadTask(string name, UploadConfig config) : this()
        {
            m_uploadName = name;

            AddConfig(config);
        }
        
        public UploadTask(string name, List<UploadConfig> uploadConfigs, List<UploadConfig.UploadActionData> actions = null) 
            : this()
        {
            m_uploadName = name;

            AddActions(actions);

            foreach (UploadConfig config in uploadConfigs)
            {
                AddConfig(config);
            }
        }

        public UploadTask(List<UploadConfig> uploadConfigs, List<UploadConfig.UploadActionData> actions = null)
            : this("No Name Specified", uploadConfigs, actions)
        {
            
        }
        
        public UploadTask()
        {
            m_guid = Guid.NewGuid().ToString().Substring(0, 6);
            m_uploadDescription = "";
            m_uploadConfigs = new List<UploadConfig>();
            m_actions = new List<UploadConfig.UploadActionData>();
            
            m_context = new UploadTaskStringFormatterContext(this);
        }

        ~UploadTask()
        {
            if (ProgressUtils.Exists(m_progressId))
            {
                ProgressUtils.Remove(m_progressId);
            }
        }

        /// <summary>
        /// Start the upload task asynchronously.
        /// Listen to OnComplete to get the report when the upload is done.
        /// </summary>
        /// <param name="invokeDebugLogs">When a log,warning,error occurs during the upload should this log to Unity Console? logs can be found in the report at the end.</param>
        public void Start(bool invokeDebugLogs = true)
        {
            _ = StartAsync(invokeDebugLogs);
        }

        /// <summary>
        /// Start the upload task synchronously.
        /// </summary>
        /// <param name="invokeDebugLogs">When a log,warning,error occurs during the upload should this log to Unity Console? logs can be found in the report at the end.</param>
        public void StartAndBlock(bool invokeDebugLogs = true)
        {
            StartAsync(invokeDebugLogs).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Start the upload task asynchronously.
        /// Listen to OnComplete to get the report when the upload is done. 
        /// </summary>
        /// <param name="invokeDebugLogs">When a log,warning,error occurs during the upload should this log to Unity Console? logs can be found in the report at the end.</param>
        /// <returns>Report regarding if the upload was successful or not aswell as all the logs</returns>
        public async Task StartAsync(bool invokeDebugLogs=true)
        {
            Prepare(invokeDebugLogs);
            RunTask = RunAsync(CreateSteps(), 0);
            await RunTask;
        }

        /// <summary>
        /// Everything a task does once before its first step. A resumed task skips this so its upload and build
        /// numbers aren't bumped again.
        /// </summary>
        private void Prepare(bool invokeDebugLogs)
        {
            m_report = new UploadTaskReport(m_guid, UploadName, invokeDebugLogs);
            m_report.Start();
            m_cachedLocations = new string[m_uploadConfigs.Count];
            m_cachedLocationNeedsCleaning = new bool[m_uploadConfigs.Count];
            IsSuccessful = false;
            CurrentStepType = AUploadTask_Step.StepType.Validation;
            m_checkpointFolder = UploadTaskCheckpoints.FolderPath;
            m_checkpointStep = AUploadTask_Step.StepType.Validation;
            m_interruptions = 0;
            m_completedSources.Clear();
            BuildUploaderProjectSettings.BumpUploadNumber();

            for (var i = 0; i < m_uploadConfigs.Count; i++)
            {
                m_uploadConfigs[i].SetContextAndCacheCallbacks(m_context);
            }
            m_context.CacheCallbacks();

            for (var i = 0; i < m_actions.Count; i++)
            {
                if (m_actions[i].UploadAction != null)
                {
                    m_actions[i].UploadAction.Context.SetParent(m_context);
                    m_actions[i].UploadAction.Context.CacheCallbacks();
                }
            }

            IsSuccessful = true;
        }

        private AUploadTask_Step[] CreateSteps()
        {
            // Executed in order
            return new AUploadTask_Step[]
            {
                // Setup - Always executed
                new UploadTaskStep_Validate(m_context), // Make sure the sources are ready before we retrieve them
                new UploadTaskStep_PreUploadActions(m_context), // Execute actions before we begin the upload progress
                
                // Do the upload sequence - Only executed if all previous steps succeeded
                new UploadTaskStep_PrepareSources(m_context), // Make sure the sources are ready before we retrieve them
                new UploadTaskStep_GetSources(m_context), // Download content from services or get local folder
                new UploadTaskStep_CacheSources(m_context), // Cache the content in Utils.CachePath
                new UploadTaskStep_ModifyCachedSources(m_context), // Modify the build so it's ready to be uploaded (Remove/add files)
                new UploadTaskStep_PrepareDestinations(m_context), // Make sure the destination is ready to receive the content
                new UploadTaskStep_Upload(m_context), // Upload cached content,
                
                // Cleanup - Always executed
                new UploadTaskStep_PostUploadActions(m_context), // Execute actions after we finish the upload progress
                new UploadTaskStep_Cleanup(m_context), // Clean up the task, delete cached files, etc.
            };
        }

        /// <summary>
        /// Runs the steps from startStep on, then finishes the task. Starting a task runs every step; a task resumed
        /// after a script reload starts at the step it was on.
        /// </summary>
        private async Task RunAsync(AUploadTask_Step[] steps, int startStep)
        {
            s_liveTasks.Add(m_guid);
            try
            {
                m_progressId = ProgressUtils.Start("Build Uploader Window", "Setting up...");
                IsComplete = false;
                PercentComplete = (float)startStep / steps.Length;
                HasStarted = true;
                m_currentSteps = steps;
                m_totalSteps = steps.Length;

                // Set up Context to display task-specific strings
                SetupContext(m_report);
                WriteReportFile();

                // Run the Build Uploader
                await Execute(steps, startStep);
                if (m_debugForceStopTask)
                {
                    ProgressUtils.Remove(m_progressId);
                    return;
                }

                IsComplete = true;
                PercentComplete = 1f;
                IsSuccessful = m_report.Successful && !IsCancelled;

                ProgressUtils.Remove(m_progressId);
                m_report.Complete();
                WriteReportFile();
                await m_reportWrite;
                if (Preferences.AutoSaveReportToCacheFolder)
                {
                    Debug.Log($"[BuildUploader] Wrote upload task report to {ReportPath(m_uploadConfigs.Select(x => x.GUID), m_report.StartTime)}");
                }
                UploadTaskCheckpoints.Delete(UploadTaskCheckpoints.FilePath(m_checkpointFolder, m_guid));
            }
            finally
            {
                if (!m_debugForceStopTask)
                {
                    s_liveTasks.Remove(m_guid);
                }
            }

            OnComplete?.Invoke(m_report);
        }

        /// <summary>
        /// Writes the report next to the cached builds so it outlives the editor session. Every entry point -
        /// the Upload window, the CLI, batch mode - goes through RunAsync, so saving here is what makes a run
        /// show up in the Upload Tasks window no matter how it was started. The file is written when the task
        /// starts and kept up to date while it runs; a request while a write is in progress writes again after it.
        /// </summary>
        private void WriteReportFile()
        {
            if (!Preferences.AutoSaveReportToCacheFolder || m_debugForceStopTask)
            {
                return;
            }

            if (!m_reportWrite.IsCompleted)
            {
                m_reportWritePending = true;
                return;
            }

            m_reportWrite = WriteReportFileAsync();
        }

        private async Task WriteReportFileAsync()
        {
            do
            {
                m_reportWritePending = false;
                m_reportChangesWritten = m_report.ChangeCount;
                m_reportWrittenAt = DateTime.UtcNow;
                string reportPath = ReportPath(m_uploadConfigs.Select(x => x.GUID), m_report.StartTime);
                await WriteReportText(reportPath, m_report.GetReport());
            }
            while (m_reportWritePending && !m_debugForceStopTask);
        }

        /// <summary>
        /// Called while a step runs, so a long step's logs reach the file every few seconds
        /// </summary>
        private void WriteReportFileIfChanged()
        {
            if (m_report.ChangeCount != m_reportChangesWritten && DateTime.UtcNow - m_reportWrittenAt >= ReportWriteInterval)
            {
                WriteReportFile();
            }
        }

        internal static async Task SaveReport(UploadTaskReport report, List<string> configGUIDs)
        {
            if (!Preferences.AutoSaveReportToCacheFolder)
            {
                return;
            }

            await WriteReportText(ReportPath(configGUIDs, report.StartTime), report.GetReport());
        }

        /// <summary>
        /// Writes beside the report and then moves it into place, so the Upload Tasks window never reads half a file
        /// </summary>
        private static async Task WriteReportText(string reportPath, string text)
        {
            string temporaryPath = reportPath + ".tmp";
            try
            {
                // Made every time: Delete All Complete Reports deletes the folder
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                await IOUtils.WriteAllTextAsync(temporaryPath, text);
                if (File.Exists(reportPath))
                {
                    File.Delete(reportPath);
                }
                File.Move(temporaryPath, reportPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildUploader] Could not write the upload task report to {reportPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Where a task's report is saved. It depends only on the configs and the start time, so a resumed task
        /// writes to the same file.
        /// </summary>
        internal static string ReportPath(IEnumerable<string> configGUIDs, DateTime startTime)
        {
            string guids = string.Join("_", configGUIDs);
            string fileName = $"UploadReport_{guids}_{startTime:yyyy-MM-dd_HH-mm-ss}.txt";
            return Path.Combine(WindowUploadTab.UploadReportSaveDirectory, fileName);
        }

        private T GetStep<T>()
        {
            foreach (AUploadTask_Step step in m_currentSteps)
            {
                if (step is T t)
                {
                    return t;
                }
            }
            
            return default(T);
        }

        /// <summary>
        /// Request the task stop. Steps check the token between operations, so an upload already in flight
        /// finishes before the task gives up.
        /// </summary>
        public void Cancel()
        {
            if (IsComplete)
            {
                return;
            }
            
            m_cancellationToken?.Cancel(); // Calls OnCancelled which knock-on updates everything
        }

        private async Task Execute(AUploadTask_Step[] steps, int startStep)
        {
            // Do upload steps. IsSuccessful is not reset here: a resumed task keeps an earlier failure.
            CancellationTokenSource token = m_cancellationToken = new CancellationTokenSource();
            token.Token.Register(OnCancelled);
            if (IsCancelled)
            {
                token.Cancel();
            }

            for (int i = startStep; i < steps.Length && !m_debugForceStopTask; i++)
            {
                AUploadTask_Step step = steps[i];
                CurrentStep = step;
                CurrentStepType = step.Type;
                if (!IsSuccessful && step.RequiresEverythingBeforeToSucceed)
                {
                    continue;
                }

                // Saved before every step so a script reload can resume the task from here
                m_report.CurrentStep = step.Type;
                Checkpoint(step.Type, CheckpointPhase.Running);

                // Perform the Step (GetSources, CacheSources, etc.)
                m_report.SetProcess(AUploadTask_Step.StepProcess.Intra);
                Task<bool> intraTask;
                try
                {
                    intraTask = step.Run(this, m_report, token);
                }
                catch (Exception ex)
                {
                    UploadTaskReport.StepResult result = m_report.NewReport(step.Type);
                    result.SetFailed($"Failed to complete step {step.Type}. An exception occured!\n\n{ex}");
                    result.AddException(ex);
                    intraTask = Task.FromResult(false);
                }

                while (!intraTask.IsCompleted)
                {
                    float progress = m_report.GetProgress(step.Type, AUploadTask_Step.StepProcess.Intra);
                    SetProgress(i, progress, CurrentStepType.ToString());
                    WriteReportFileIfChanged();
                    await Task.Yield();
                    if (m_debugForceStopTask)
                    {
                        return;
                    }
                }
                bool stepSuccessful = intraTask.Result;
                SetProgress(i, 1f, CurrentStepType.ToString());
                
                // Post-step logic mainly for logging
                m_report.SetProcess(AUploadTask_Step.StepProcess.Post);
                bool postStepSuccessful = false;
                try
                {
                    postStepSuccessful = await step.PostRunResult(this, m_report, stepSuccessful && IsSuccessful);
                }
                catch (Exception ex)
                {
                    UploadTaskReport.StepResult result = m_report.NewReport(step.Type);
                    result.AddException(ex);
                }

                if (!stepSuccessful || !postStepSuccessful || token.IsCancellationRequested)
                {
                    IsSuccessful = false;

                    // The task result comes from the report, so a step that failed without SetFailed must still fail it
                    if (m_report.Successful && !token.IsCancellationRequested)
                    {
                        m_report.NewReport(step.Type).SetFailed($"{step.Type} step failed without giving a reason.");
                    }
                }

                if (step.FireActions)
                {
                    Checkpoint(step.Type, CheckpointPhase.Actions);
                    bool actionsSuccessful = await ExecuteTaskActions(stepSuccessful, UploadConfig.UploadActionData.UploadTrigger.AfterEachStepCompletes);
                    if (!actionsSuccessful)
                    {
                        // An action set to stop the task failed, so skip the remaining steps that need success
                        IsSuccessful = false;
                    }
                }
            }
        }
        
        private void OnCancelled()
        {
            if (IsCancelled)
                return;
            
            IsCancelled = true;
            m_report.SetCancelled();
        }

        /// <summary>
        /// Runs the task's actions that match the trigger.
        /// </summary>
        /// <returns>False if an action set to stop the task failed</returns>
        public async Task<bool> ExecuteTaskActions(bool valid,
            UploadConfig.UploadActionData.UploadTrigger trigger)
        {
            return await ExecuteActions(valid, trigger, m_actions, "Post Upload Action");
        }

        /// <summary>
        /// Runs the actions that match the trigger and the task's success so far.
        /// An action fails when Prepare or Execute returns false or throws, or it marks its result failed.
        /// </summary>
        /// <returns>False if an action with StopTaskIfFailed failed</returns>
        public async Task<bool> ExecuteActions(bool valid, 
            UploadConfig.UploadActionData.UploadTrigger? trigger,
            List<UploadConfig.UploadActionData> allActions, string title)
        {
            UploadTaskReport.StepResult startLog = m_report.NewReport(CurrentStepType);
            if (trigger.HasValue)
            {
                startLog.AddLog($"Execute {title} with trigger {trigger.Value} successful {valid}");
            }
            else
            {
                startLog.AddLog($"Execute {title} with successful {valid}");
            }
            startLog.SetPercentComplete(1f);
            
            UploadTaskReport.StepResult[] results = m_report.NewReports(CurrentStepType, allActions.Count);
            
            int postActionID = ProgressUtils.Start(title, $"Executing {title}...");
            int actionsRun = 0;
            bool stopTask = false;
            
            for (int i = 0; i < allActions.Count; i++)
            {
                UploadTaskReport.StepResult result = results[i];
                UploadConfig.UploadActionData actionData = allActions[i];
                if (actionData == null || actionData.UploadAction == null)
                {
                    result.SetSkipped($"Skipping {title} {i+1} because it's null");
                    continue;
                }

                if (trigger.HasValue && !actionData.Triggers.Contains(trigger.Value))
                {
                    result.SetSkipped($"Skipping {title} {i+1} because it doesn't match the trigger. Trigger: {trigger.Value}");
                    continue;
                }

                UploadConfig.UploadActionData.UploadCompleteStatus status = actionData.WhenToExecute;
                if (status == UploadConfig.UploadActionData.UploadCompleteStatus.Never ||
                    (status == UploadConfig.UploadActionData.UploadCompleteStatus.IfSuccessful && !valid) ||
                    (status == UploadConfig.UploadActionData.UploadCompleteStatus.IfFailed && valid))
                {
                    result.SetSkipped($"Skipping {title} {i+1} because it doesn't match the current status. Status: {status}. Successful: {valid}");
                    continue;
                }

                result.AddLog($"Executing {title}: {i+1}");

                actionsRun++;
                string actionName = actionData.UploadAction.GetType().Name;
                bool succeeded = false;
                try
                {
                    if (await actionData.UploadAction.Prepare(result))
                    {
                        succeeded = await actionData.UploadAction.Execute(result);
                    }
                    else
                    {
                        result.AddError($"Failed to prepare {title}: {actionName}");
                    }
                }
                catch (Exception e)
                {
                    result.AddException(e);
                }
                finally
                {
                    result.SetPercentComplete(1f);
                }

                if ((!succeeded || !result.Successful) && actionData.StopTaskIfFailed)
                {
                    if (result.Successful)
                    {
                        result.SetFailed($"{title} {i+1} ({actionName}) failed and is set to stop the task if it fails.");
                    }
                    stopTask = true;
                }
            }

            if (actionsRun == 0)
            {
                UploadTaskReport.StepResult result = m_report.NewReport(CurrentStepType);
                result.SetSkipped("No post upload actions to run under trigger " + trigger + " and successful " + valid);
            }
            
            ProgressUtils.Remove(postActionID);
            return !stopTask;
        }

        private void SetProgress(int step, float percent, string message)
        {
            float mainPercent = (float)step / m_totalSteps;
            float subPercent = (1f / m_totalSteps) * Mathf.Clamp01(percent);
            
            PercentComplete = mainPercent + subPercent;
            ProgressUtils.Report(m_progressId, percent, message);
        }

        private void SetupContext(UploadTaskReport report)
        {
            m_context.AddCommand(Wireframe.Context.TASK_FAILED_REASONS_KEY, ()=>
            {
                if (report.Successful)
                {
                    return "Upload task did not fail.";
                }
                
                var failReasons = report.GetFailReasons();
                if (!failReasons.Any())
                {
                    return "No specific failure reasons provided.";
                }

                string reasonText = "";
                foreach ((AUploadTask_Step.StepType Key, string FailReason) reason in failReasons)
                {
                    if (string.IsNullOrEmpty(reasonText))
                    {
                        reasonText += $"{reason.Key}: {reason.FailReason}";
                    }
                    else
                    {
                        reasonText += $"\n{reason.Key}: {reason.FailReason}";
                    }
                }
                return reasonText;
            });
        }

        public void AddConfig(UploadConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.Context.SetParent(m_context);
            
            m_uploadConfigs.Add(config);
        }

        public void AddAction(AUploadAction action, 
            UploadConfig.UploadActionData.UploadCompleteStatus whenToExecute = UploadConfig.UploadActionData.UploadCompleteStatus.Always,
            List<UploadConfig.UploadActionData.UploadTrigger> triggers = null)
        {
            if (action == null)
            {
                return;
            }
            
            AddAction(new UploadConfig.UploadActionData(action, whenToExecute, triggers));
        }
        
        public void AddAction(UploadConfig.UploadActionData action)
        {
            if (action == null)
            {
                return;
            }

            m_actions.Add(action);
        }
        
        public void AddActions(List<UploadConfig.UploadActionData> actions)
        {
            if (actions == null)
            {
                return;
            }

            foreach (UploadConfig.UploadActionData action in actions)
            {
                AddAction(action);
            }
        }
        
        public void SetBuildDescription(string description)
        {
            m_uploadDescription = description;
        }
        
        internal void SetReport(UploadTaskReport report)
        {
            m_report = report;
            m_guid = report.GUID;
            m_uploadName = report.Name;
            IsSuccessful = report.Successful;
            IsCancelled = report.Cancelled;

            // A report saved while its task ran shows where the task got to
            IsComplete = !report.InProgress;
            CurrentStepType = report.CurrentStep;
            PercentComplete = report.InProgress ? (float)report.CurrentStep / Enum.GetValues(typeof(AUploadTask_Step.StepType)).Length : 1f;
        }
    }
}