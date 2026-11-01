using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Base class for a step in the upload task process
    /// Each step is executed in order and can specify what happens if the step fails
    /// Steps can be things like validation, preparing sources, uploading, post upload actions, etc
    /// </summary>
    public abstract class AUploadTask_Step
    {
        protected class StateResult
        {
            public UploadConfig uploadConfig { get; private set; }
            public Func<int, string> labelGetter { get; private set; }
            public UploadTaskReport.StepResult[] reports { get; private set; }

            public StateResult(UploadConfig config, IEnumerable<UploadTaskReport.StepResult> reports, Func<int, string> labelGetter)
            {
                this.uploadConfig = config;
                this.labelGetter = labelGetter;
                this.reports = reports.ToArray();
            }
            
            public StateResult(UploadConfig config, UploadTaskReport.StepResult report, Func<int, string> labelGetter)
            {
                this.uploadConfig = config;
                this.labelGetter = labelGetter;
                this.reports = new[]{report};
            }
        }

        /// <summary>
        /// Step which the task is at while in the process of uploading
        /// NOTE: Changing this at all can break report logs. TODO: Make it not
        /// </summary>
        public enum StepType
        {
            Validation,
            PreUploadActions,
            PrepareSources,
            GetSources,
            CacheSources, // TODO: Rename to CopySources
            ModifyCacheSources,
            PrepareDestinations,
            Upload,
            PostUploadActions,
            Cleanup,
        }

        public enum StepProcess
        {
            Pre,
            Intra,
            Post
        }
        
        public abstract StepType Type { get; }
        public virtual bool RequiresEverythingBeforeToSucceed => true;
        public virtual bool FireActions => true;

        /// <summary>
        /// Whether a task resumed after a script reload interrupted this step may run it again from the start.
        /// Steps with effects outside the editor (uploads, messages) or that change files in place must not.
        /// </summary>
        public virtual bool CanRerunAfterReload => false;
        public abstract Task<bool> Run(UploadTask uploadTask, UploadTaskReport report, CancellationTokenSource token);
        public abstract Task<bool> PostRunResult(UploadTask uploadTask, UploadTaskReport report, bool allStepsSuccessful);
        
        protected List<StateResult> m_stateResults = new List<StateResult>(); 
        protected bool m_completed;
        protected bool m_successful;
        protected readonly Context m_context;

        public AUploadTask_Step(Context ctx)
        {
            m_context = ctx;
        }
        
        protected void ReportCachedFiles(UploadTask uploadTask, UploadTaskReport report)
        {
            List<UploadConfig> buildConfigs = uploadTask.UploadConfigs;
            UploadTaskReport.StepResult[] results = report.NewReports(Type, buildConfigs.Count);
            for (var i = 0; i < buildConfigs.Count; i++)
            {
                var config = buildConfigs[i];
                var result = results[i];
                if (!config.Enabled)
                {
                    continue;
                }
                
                string cachePath = uploadTask.CachedLocations[i];
                if (string.IsNullOrEmpty(cachePath))
                {
                    result.AddLog($"Config {i+1} has cached files. Something went wrong.");
                    return;
                }
                
                ReportFilesAtPath(cachePath, $"Config {i+1} cached files:", result);
            }
        }

        protected void ReportFilesAtPath(string cachePath, string prefix, UploadTaskReport.StepResult result)
        {
            if (string.IsNullOrEmpty(cachePath))
            {
                return;
            }
            
            List<string> allFiles = Utils.GetSortedFilesAndDirectories(cachePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(prefix);
            foreach (string file in allFiles)
            {
                sb.AppendLine("\t-" + file);
            }
            result.AddLog(sb.ToString());
        }

        public virtual string GetStateSummary()
        {
            StringBuilder summary = new StringBuilder();
            foreach (StateResult stateResult in m_stateResults)
            {
                for (var i = 0; i < stateResult.reports.Length; i++)
                {
                    UploadTaskReport.StepResult stepResult = stateResult.reports[i];
                    if (stepResult.IsSkipped)
                    {
                        continue;
                    }
                    
                    string labelGetter = stateResult.labelGetter(i);
                    string icon = !stepResult.IsComplete ? "⌛" : stepResult.Successful ? "✅" : "❌";
                    
                    summary.Append("- ");
                    summary.Append(icon);
                    summary.Append(labelGetter);
                    if (stepResult.IsComplete)
                    {
                        if (!stepResult.Successful)
                        {
                            summary.Append(": ");   
                            summary.AppendLine(stepResult.FailReason);
                        }
                        else
                        {
                            summary.AppendLine();
                        }
                    }
                    else if (stepResult.Logs.Count > 0)
                    {
                        summary.Append(": ");   
                        summary.AppendLine(stepResult.Logs[stepResult.Logs.Count - 1].Message);
                        if (!string.IsNullOrEmpty(stepResult.FailReason))
                        {
                            summary.AppendLine(stepResult.FailReason);
                        }
                    }
                    else
                    {
                        summary.Append(": ");
                        summary.AppendLine("Waiting to start...");
                    }
                }
            }
            
            return summary.ToString();
        }
    }
}