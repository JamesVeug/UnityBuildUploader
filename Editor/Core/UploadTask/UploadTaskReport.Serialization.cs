using System;
using System.Collections.Generic;

namespace Wireframe
{
    /// <summary>
    /// Saves everything in a report so an in-progress task can be resumed after a script reload.
    /// Unlike GetReport and FromFilePath, Deserialize gives back exactly what was saved.
    /// </summary>
    public partial class UploadTaskReport
    {
        public partial class StepResult
        {
            internal Dictionary<string, object> Serialize(AUploadTask_Step.StepType step, AUploadTask_Step.StepProcess process)
            {
                // Logs can be added from other threads while a task runs
                lock (m_lock)
                {
                    List<object> logs = new List<object>(Logs.Count);
                    foreach (Log log in Logs)
                    {
                        logs.Add(new Dictionary<string, object>
                        {
                            ["type"] = log.Type.ToString(),
                            ["message"] = log.Message,
                        });
                    }

                    return new Dictionary<string, object>
                    {
                        ["step"] = step.ToString(),
                        ["process"] = process.ToString(),
                        ["successful"] = Successful,
                        ["skipped"] = IsSkipped,
                        ["percent"] = PercentComplete,
                        ["failReason"] = FailReason,
                        ["logs"] = logs,
                    };
                }
            }

            /// <summary>
            /// Restores what Serialize saved without logging it to the Console again
            /// </summary>
            internal void Deserialize(Dictionary<string, object> data)
            {
                Successful = (bool)data["successful"];
                IsSkipped = (bool)data["skipped"];
                PercentComplete = Convert.ToSingle(data["percent"]);
                FailReason = data["failReason"] as string ?? "";
                foreach (Dictionary<string, object> log in (List<object>)data["logs"])
                {
                    Enum.TryParse((string)log["type"], out Log.LogType type);
                    Logs.Add(new Log(type, log["message"] as string ?? ""));
                }
            }
        }

        internal Dictionary<string, object> Serialize()
        {
            List<object> results = new List<object>();
            foreach (var step in StepResults)
            {
                foreach (var process in step.Value)
                {
                    foreach (StepResult result in process.Value)
                    {
                        results.Add(result.Serialize(step.Key, process.Key));
                    }
                }
            }

            return new Dictionary<string, object>
            {
                ["guid"] = GUID,
                ["name"] = Name,
                ["startTicks"] = StartTime.Ticks,
                ["successful"] = Successful,
                ["cancelled"] = Cancelled,
                ["inProgress"] = InProgress,
                ["currentStep"] = CurrentStep.ToString(),
                ["invokeDebugLogs"] = m_invokeDebugLogs,
                ["results"] = results,
            };
        }

        internal static UploadTaskReport Deserialize(Dictionary<string, object> data)
        {
            UploadTaskReport report = new UploadTaskReport((string)data["guid"], (string)data["name"], (bool)data["invokeDebugLogs"]);
            report.StartTime = new DateTime(Convert.ToInt64(data["startTicks"]), DateTimeKind.Utc);
            report.Successful = (bool)data["successful"];
            report.Cancelled = (bool)data["cancelled"];
            report.InProgress = data.TryGetValue("inProgress", out object inProgress) && inProgress is bool running && running;
            if (data.TryGetValue("currentStep", out object currentStep) && Enum.TryParse(currentStep as string, out AUploadTask_Step.StepType currentStepType))
            {
                report.CurrentStep = currentStepType;
            }

            foreach (Dictionary<string, object> result in (List<object>)data["results"])
            {
                if (!Enum.TryParse((string)result["step"], out AUploadTask_Step.StepType step) ||
                    !Enum.TryParse((string)result["process"], out AUploadTask_Step.StepProcess process))
                {
                    continue;
                }

                report.SetProcess(process);
                report.NewReport(step).Deserialize(result);
            }

            report.SetProcess(AUploadTask_Step.StepProcess.Intra);
            return report;
        }
    }
}
