using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadTaskReport
    {
        public partial class StepResult
        {
            public class Log
            {
                public enum LogType
                {
                    Info,
                    Warning,
                    Error,
                    Exception
                }
                
                public LogType Type { get; private set; }
                public string Message { get; private set; }

                public Log(LogType type, string message)
                {
                    Type = type;
                    Message = message;
                }
            }
            
            public bool Successful { get; private set; } = true;
            public bool IsSkipped { get; private set; } = false;
            public bool IsComplete => PercentComplete >= 1f; 
            public float PercentComplete { get; private set; }
            public string FailReason { get; private set; } = "";
            public List<Log> Logs { get; private set; } = new List<Log>();

            private object m_lock = new object();
            private readonly UploadTaskReport m_report;

            public StepResult(UploadTaskReport report)
            {
                m_report = report;
            }
            
            /// <summary>
            /// A copy of the logs, safe to read while other threads add to them
            /// </summary>
            internal List<Log> CopyLogs()
            {
                lock (m_lock)
                {
                    return new List<Log>(Logs);
                }
            }

            public void AddLog(string log)
            {
                lock (m_lock)
                {
                    Logs.Add(new Log(Log.LogType.Info, log));
                }
                m_report.Changed();

                if (m_report.m_invokeDebugLogs)
                {
                    string info = "[INFO] " + log;
                    Debug.Log(info);
                }
            }
            
            public void AddWarning(string warning)
            {
                lock (m_lock)
                {
                    Logs.Add(new Log(Log.LogType.Warning, warning));
                }
                m_report.Changed();

                if (m_report.m_invokeDebugLogs)
                {
                    string warn = "[WARNING] " + warning;
                    Debug.LogWarning(warn);
                }
            }
            
            public void AddError(string error)
            {
                lock (m_lock)
                {
                    Logs.Add(new Log(Log.LogType.Error, error));
                }
                m_report.Changed();

                if (m_report.m_invokeDebugLogs)
                {
                    string err = "[ERROR] " + error;
                    Debug.LogError(err);
                }
            }

            public void AddException(Exception e)
            {
                lock (m_lock)
                {
                    Logs.Add(new Log(Log.LogType.Exception, e.Message + "\n" + e.StackTrace));
                }
                m_report.Changed();

                if (m_report.m_invokeDebugLogs)
                {
                    Debug.LogException(e);
                }
            }

            public void AddException(Exception e, params string[] hideText)
            {
                lock (m_lock)
                {
                    Logs.Add(new Log(Log.LogType.Exception, e.Message.HideText(hideText) + "\n" + e.StackTrace.HideText(hideText)));
                }
                m_report.Changed();

                if (m_report.m_invokeDebugLogs)
                {
                    Debug.LogException(e);
                }
            }
            
            public void SetFailed(string reason)
            {
                lock (m_lock)
                {
                    Successful = false;
                    FailReason = reason;
                    m_report.Successful = false;
                    // Note: We add a log entry in UploadTaskReport.GetReport()
                }
                m_report.Changed();
                
                if (m_report.m_invokeDebugLogs)
                {
                    string err = "[FAILED] " + reason;
                    Debug.LogError(err);
                }
            }

            public void SetSkipped(string reason)
            {
                lock (m_lock)
                {
                    AddLog(reason);
                    IsSkipped = true;
                    SetPercentComplete(1f);
                }
            }
            
            public void SetPercentComplete(float percent)
            {
                lock (m_lock)
                {
                    PercentComplete = Mathf.Clamp01(percent);
                }
            }
        }

        public TimeSpan Duration => EndTime - StartTime;
        public bool Successful { get; private set; } = true;
        public bool Cancelled { get; private set; } = false;
        
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public string GUID { get; private set; }
        public string Name { get; private set; }

        /// <summary>
        /// The task is still running, or it stopped without finishing (for example Unity closed). The report file is
        /// written while a task runs, so the Upload Tasks window shows it as it goes.
        /// </summary>
        public bool InProgress { get; private set; }

        /// <summary>
        /// The step the task was on when the report was last written
        /// </summary>
        public AUploadTask_Step.StepType CurrentStep { get; internal set; }

        /// <summary>
        /// Goes up whenever something is logged or fails, so the report file is only written again when it changed
        /// </summary>
        internal int ChangeCount => m_changeCount;

        private readonly bool m_invokeDebugLogs;
        private AUploadTask_Step.StepProcess m_process;
        private int m_changeCount;

        public UploadTaskReport(string guid, string name, bool invokeDebugLogs = true)
        {
            GUID = guid;
            Name = name;
            m_invokeDebugLogs = invokeDebugLogs;
            StartTime = DateTime.UtcNow;
        }
        
        public Dictionary<AUploadTask_Step.StepType, Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>> StepResults { get; set; } = 
            new Dictionary<AUploadTask_Step.StepType, Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>>();

        public StepResult NewReport(AUploadTask_Step.StepType type)
        {
            if (!StepResults.ContainsKey(type))
            {
                StepResults[type] = new Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>();
            }
            
            if (!StepResults[type].ContainsKey(m_process))
            {
                StepResults[type][m_process] = new List<StepResult>();
            }

            var stepResult = new StepResult(this);
            StepResults[type][m_process].Add(stepResult);
            return stepResult;
        }
        
        public StepResult[] NewReports(AUploadTask_Step.StepType type, int count)
        {
            if (!StepResults.ContainsKey(type))
            {
                StepResults[type] = new Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>();
            }
            
            if (!StepResults[type].ContainsKey(m_process))
            {
                StepResults[type][m_process] = new List<StepResult>();
            }

            var stepResults = new StepResult[count];
            for (int i = 0; i < count; i++)
            {
                var stepResult = new StepResult(this);
                StepResults[type][m_process].Add(stepResult);
                stepResults[i] = stepResult;
            }

            return stepResults;
        }

        public void SetProcess(AUploadTask_Step.StepProcess process)
        {
            m_process = process;
        }

        internal void Start()
        {
            InProgress = true;
        }

        public void Complete()
        {
            EndTime = DateTime.UtcNow;
            InProgress = false;
        }

        private void Changed()
        {
            Interlocked.Increment(ref m_changeCount);
        }

        public void SetCancelled()
        {
            Cancelled = true;
        }

        public string GetReport(bool ignoreEmptySteps = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Upload Task Report");
            sb.AppendLine("GUID: " + GUID);
            sb.AppendLine("Name: " + Name);
            sb.AppendLine("Start Time: " + StartTime + " Local: " + StartTime.ToLocalTime()); // UTC
            sb.AppendLine("In Progress: " + InProgress);
            if (InProgress)
            {
                sb.AppendLine("Current Step: " + CurrentStep);
            }
            else
            {
                sb.AppendLine("End Time: " + EndTime + " Local: " + EndTime.ToLocalTime()); // UTC
                sb.AppendLine("Duration: " + Duration);
            }
            sb.AppendLine("Successful: " + Successful);
            sb.AppendLine("Cancelled: " + Cancelled);
            
            
            StringBuilder stepTypeSb = new StringBuilder();
            foreach (AUploadTask_Step.StepType stepType in Enum.GetValues(typeof(AUploadTask_Step.StepType)))
            {
                if (sb.Length != 0)
                {
                    sb.AppendLine();
                }

                stepTypeSb.Clear();
                GetStepLogs(ignoreEmptySteps, stepType, stepTypeSb);
                if (stepTypeSb.Length > 0)
                {
                    sb.AppendLine($"== -- {stepType} -- ==");
                    sb.AppendLine(stepTypeSb.ToString());
                }
                else if (!ignoreEmptySteps)
                {
                    sb.AppendLine($"== -- {stepType} -- ==");
                    sb.AppendLine("No logs");
                }
            }

            return sb.ToString();
        }

        public void GetStepLogs(bool ignoreEmptySteps, AUploadTask_Step.StepType stepType, StringBuilder sb)
        {
            bool hasSteps = StepResults.TryGetValue(stepType, out var stepResults) && stepResults.Count > 0;
            if (ignoreEmptySteps && !hasSteps)
            {
                return;
            }
                
            if (!hasSteps) // Get Sources
            {
                return;
            }

            foreach (AUploadTask_Step.StepProcess stepProcess in Enum.GetValues(typeof(AUploadTask_Step.StepProcess)))
            {
                if (stepResults.TryGetValue(stepProcess, out var stepResult)) // Post
                {
                    for (var i = 0; i < stepResult.Count; i++)
                    {
                        StepResult result = stepResult[i];
                        List<StepResult.Log> logs = result.CopyLogs();
                        if (logs.Count == 0 && string.IsNullOrEmpty(result.FailReason))
                        {
                            // A failure is written even without logs, or its reason would be lost from the file
                            continue;
                        }

                        sb.AppendLine($"-- {stepProcess} {i + 1} --");
                        if (!string.IsNullOrEmpty(result.FailReason))
                        {
                            sb.AppendLine($"[FAILED] {result.FailReason}");
                        }

                        foreach (StepResult.Log log in logs)
                        {
                            sb.AppendLine($"[{log.Type}] {log.Message}");
                        }
                    }
                }
            }
        }

        public IEnumerable<(AUploadTask_Step.StepType Key, string FailReason)> GetFailReasons()
        {
            foreach (var stepTypeLookup in StepResults)
            {
                foreach (var stepProcessTypeLookup in stepTypeLookup.Value)
                {
                    for (var i = 0; i < stepProcessTypeLookup.Value.Count; i++)
                    {
                        var stepResult = stepProcessTypeLookup.Value[i];
                        if (!stepResult.Successful)
                        {
                            yield return (stepTypeLookup.Key, stepResult.FailReason);
                        }
                    }
                }
            }
        }

        public float GetProgress(AUploadTask_Step.StepType stepType, AUploadTask_Step.StepProcess process)
        {
            if (!StepResults.TryGetValue(stepType, out var stepResults))
            {
                return 0f; // No progress if no results
            }

            float totalProgress = 0f;
            int count = 0;

            if (stepResults.TryGetValue(process, out var results))
            {
                foreach (var result in results)
                {
                    totalProgress += result.PercentComplete;
                    count++;
                }
            }

            return count > 0 ? totalProgress / count : 0f; // Return average progress
        }

        public int CountStepLogs(AUploadTask_Step.StepType stepType)
        {
            if (!StepResults.TryGetValue(stepType, out var stepResults))
            {
                return 0; // No logs if no results
            }
            
            int count = 0;
            foreach (var stepProcess in stepResults.Values)
            {
                foreach (var result in stepProcess)
                {
                    count += result.Logs.Count;
                }
            }
            return count; // Return total log count for the step type
        }

        public static UploadTaskReport FromFilePath(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length == 0 || !lines[0].StartsWith("Upload Task Report", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int endOfMetaDataIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i].Trim()))
                {
                    endOfMetaDataIndex = i;
                    break;
                }
            }

            if (endOfMetaDataIndex == -1 || lines[0] != "Upload Task Report")
            {
                // Malformed file
                return null;
            }
            
            // Read meta data
            // Upload Task Report
            // GUID: gowsehgoewhn
            // Start Time: 4/09/2025 10:27:57 p.m.
            // End Time: 4/09/2025 10:28:35 p.m.
            // Duration: 00:00:37.4792449
            // Successful: True
            UploadTaskReport report = new UploadTaskReport("", "", false);
            for (int i = 1; i < endOfMetaDataIndex; i++)
            {
                string line = lines[i];
                if (line.StartsWith("GUID", StringComparison.OrdinalIgnoreCase))
                {
                    report.GUID = line.Substring("GUID:".Length).Trim();
                }
                else if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
                {
                    report.Name = line.Substring("Name:".Length).Trim();
                }
                else if (line.StartsWith("Start Time", StringComparison.OrdinalIgnoreCase))
                {
                    // Start Time: 4/09/2025 10:27:57 p.m. Local: 4/09/2025 8:27:57 a.m.
                    int localIndex = line.IndexOf("Local:", StringComparison.OrdinalIgnoreCase);
                    if (localIndex != -1)
                    {
                        line = line.Substring(0, localIndex).Trim();
                    }
                    if (DateTime.TryParse(line.Substring("Start Time:".Length).Trim(), out DateTime startTime))
                    {
                        report.StartTime = startTime;
                    }
                }
                else if (line.StartsWith("End Time", StringComparison.OrdinalIgnoreCase))
                {
                    // End Time: 4/09/2025 10:28:35 p.m. Local: 4/09/2025 8:28:35 a.m.
                    int localIndex = line.IndexOf("Local:", StringComparison.OrdinalIgnoreCase);
                    if (localIndex != -1)
                    {
                        line = line.Substring(0, localIndex).Trim();
                    }
                    if (DateTime.TryParse(line.Substring("End Time:".Length).Trim(), out DateTime endTime))
                    {
                        report.EndTime = endTime;
                    }
                }
                else if (line.StartsWith("Duration", StringComparison.OrdinalIgnoreCase))
                {
                    if (TimeSpan.TryParse(line.Substring("Duration:".Length).Trim(), out TimeSpan duration) && duration.Seconds > 0)
                    {
                        report.EndTime = report.StartTime + duration;
                    }
                }
                else if (line.StartsWith("Successful", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(line.Substring("Successful:".Length).Trim(), out bool successful))
                    {
                        report.Successful = successful;
                    }
                }
                else if (line.StartsWith("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(line.Substring("Cancelled:".Length).Trim(), out bool successful))
                    {
                        report.Cancelled = successful;
                    }
                }
                else if (line.StartsWith("In Progress", StringComparison.OrdinalIgnoreCase))
                {
                    // Reports written before tasks saved them as they ran don't have this, and are complete
                    if (bool.TryParse(line.Substring("In Progress:".Length).Trim(), out bool inProgress))
                    {
                        report.InProgress = inProgress;
                    }
                }
                else if (line.StartsWith("Current Step", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse(line.Substring("Current Step:".Length).Trim(), out AUploadTask_Step.StepType currentStep))
                    {
                        report.CurrentStep = currentStep;
                    }
                }
            }

            if (string.IsNullOrEmpty(report.GUID) || string.IsNullOrEmpty(report.Name))
            {
                return null;
            }
            
            
            // Load Steps - each step is -- Intra X --
            Dictionary<AUploadTask_Step.StepType, Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>> results = report.StepResults;
            AUploadTask_Step.StepType currentStepType = AUploadTask_Step.StepType.GetSources;
            AUploadTask_Step.StepProcess currentProcess = AUploadTask_Step.StepProcess.Intra;
            StepResult currentStepResult = null;

            for (int i = endOfMetaDataIndex + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("== -- ") && line.EndsWith(" -- =="))
                {
                    string stepTypeStr = line.Substring(6, line.Length - 11).Trim();
                    if (Enum.TryParse(stepTypeStr, out AUploadTask_Step.StepType stepType))
                    {
                        currentStepType = stepType;
                        currentProcess = AUploadTask_Step.StepProcess.Intra; // Reset process to Intra
                        
                        if (!results.ContainsKey(currentStepType))
                        {
                            results[currentStepType] = new Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>();
                        }
                        
                        if (!results[currentStepType].ContainsKey(currentProcess))
                        {
                            results[currentStepType][currentProcess] = new List<StepResult>();
                        }
                        
                        currentStepResult = new StepResult(report);
                    }
                    else
                    {
                        Debug.LogError("[UploadTaskReport] Failed to parse step type from report: " + stepTypeStr);
                    }
                }
                else if (line.StartsWith("-- ") && line.EndsWith(" --"))
                {
                    string processStr = line.Substring(3, line.Length - 6).Trim();
                    string[] parts = processStr.Split(' ');
                    if (parts.Length >= 1 && Enum.TryParse(parts[0], out AUploadTask_Step.StepProcess process))
                    {
                        currentProcess = process;
                        currentStepResult = new StepResult(report);
                        if (!results.ContainsKey(currentStepType))
                        {
                            results[currentStepType] = new Dictionary<AUploadTask_Step.StepProcess, List<StepResult>>();
                        }

                        if (!results[currentStepType].ContainsKey(currentProcess))
                        {
                            results[currentStepType][currentProcess] = new List<StepResult>();
                        }
                        
                        results[currentStepType][currentProcess].Add(currentStepResult);
                    }
                }
                else
                {
                    // Every line starts with [Info]/[Warning]/[Error]/[Exception]
                    if (line.StartsWith("[Info]", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStepResult.AddLog(line.Substring("[Info]".Length));
                    }
                    else if (line.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStepResult.AddWarning(line.Substring("[Warning]".Length));
                    }
                    else if (line.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStepResult.AddError(line.Substring("[Error]".Length));
                    }
                    else if (line.StartsWith("[Exception]", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStepResult.AddException(new Exception(line.Substring("[Exception]".Length)));
                    }
                    else if (line.StartsWith("[Failed]", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStepResult.SetFailed(line);
                    }
                    else if (line.StartsWith("No Logs", StringComparison.OrdinalIgnoreCase))
                    {
                        // Do nothing
                    }
                    else
                    {
                        currentStepResult.AddLog(line);
                    }
                }
            }
            
            return report;
        }
    }
}