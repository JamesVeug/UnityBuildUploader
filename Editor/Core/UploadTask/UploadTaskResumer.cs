using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Carries on upload tasks that a script reload interrupted, such as one whose BuildConfig changed the scripting
    /// defines. Tasks that were running when Unity closed wait in the Upload Tasks window until the user resumes or
    /// discards them. Running tasks save themselves with UploadTaskCheckpoints between steps.
    /// </summary>
    [InitializeOnLoad]
    internal static class UploadTaskResumer
    {
        /// <summary>
        /// SessionState survives script reloads but not closing Unity. A checkpoint saved with this session's ID was
        /// interrupted by a script reload; any other ID means Unity closed while the task ran.
        /// </summary>
        private const string SessionIdKey = "BuildUploader_EditorSessionId";

        /// <summary>
        /// Tasks that were running when Unity closed, shown in the Upload Tasks window with Resume and Discard buttons
        /// </summary>
        internal static readonly List<PendingTask> Pending = new List<PendingTask>();

        private static string[] s_checkpointsAtLoad;

        internal enum Decision
        {
            Resume, // A script reload interrupted it in this editor session
            Skip, // It is still running
            Wait, // Unity was closed while it ran, so it waits for the user in the Upload Tasks window
        }

        internal class PendingTask
        {
            public readonly string Path;
            public readonly Dictionary<string, object> Data;

            public PendingTask(string path, Dictionary<string, object> data)
            {
                Path = path;
                Data = data;
            }

            public string GUID => (string)Data["guid"];
            public string Name => Data["name"] as string;
            public string Step => Data["nextStep"] as string;
            public DateTime StartTime => new DateTime(Convert.ToInt64(((Dictionary<string, object>)Data["report"])["startTicks"]), DateTimeKind.Utc);
        }

        static UploadTaskResumer()
        {
            // Listed now, so a task started before the editor is idle is never mistaken for an interrupted one
            s_checkpointsAtLoad = UploadTaskCheckpoints.List(UploadTaskCheckpoints.FolderPath);
            if (s_checkpointsAtLoad.Length > 0)
            {
                EditorApplication.update += WaitForEditor;
            }
        }

        internal static string SessionId
        {
            get
            {
                string id = SessionState.GetString(SessionIdKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    SessionState.SetString(SessionIdKey, id);
                }

                return id;
            }
        }

        private static void WaitForEditor()
        {
            // Polled rather than delayCall, which waits for a repaint that doesn't come while Unity opens in the background
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= WaitForEditor;
            string[] paths = s_checkpointsAtLoad;
            s_checkpointsAtLoad = null;
            ResumeOrHold(paths);
        }

        internal static Decision Decide(string savedSessionId, string taskGUID)
        {
            if (UploadTask.IsRunning(taskGUID) || UploadTask.AllTasks.Any(a => a.GUID == taskGUID && !a.IsComplete))
            {
                return Decision.Skip;
            }

            return savedSessionId == SessionId ? Decision.Resume : Decision.Wait;
        }

        /// <summary>
        /// Resumes the tasks a script reload interrupted. Tasks that were running when Unity closed are held in
        /// Pending for the Upload Tasks window, rather than asking with a dialog while Unity opens.
        /// </summary>
        /// <returns>The resumed tasks</returns>
        internal static List<UploadTask> ResumeOrHold(string[] paths)
        {
            List<UploadTask> resumed = new List<UploadTask>();
            int held = 0;
            foreach (string path in paths)
            {
                Dictionary<string, object> data = ReadOrSetAside(path);
                if (data == null)
                {
                    continue;
                }

                switch (Decide(data["sessionId"] as string, (string)data["guid"]))
                {
                    case Decision.Resume:
                        UploadTask task = Resume(path, data, "after a script reload");
                        if (task != null)
                        {
                            resumed.Add(task);
                        }
                        break;
                    case Decision.Wait:
                        if (!Pending.Any(a => a.Path == path))
                        {
                            Pending.Add(new PendingTask(path, data));
                            held++;
                        }
                        break;
                }
            }

            if (held > 0)
            {
                Debug.Log($"[BuildUploader] {held} upload task(s) were running when Unity closed. Resume or discard them in Tools > Build Uploader > Open Upload Tasks Window.");
            }

            return resumed;
        }

        /// <summary>
        /// The Upload Tasks window's Resume button: carries on a task that was running when Unity closed
        /// </summary>
        /// <returns>The resumed task, or null if it could not be resumed or was already handled</returns>
        internal static UploadTask Resume(PendingTask pending)
        {
            if (!Pending.Remove(pending))
            {
                return null;
            }

            return Resume(pending.Path, pending.Data, "after Unity was reopened");
        }

        /// <summary>
        /// The Upload Tasks window's Discard button
        /// </summary>
        internal static void Discard(PendingTask pending)
        {
            if (Pending.Remove(pending))
            {
                Discard(pending.Path, pending.Data, "Not resumed after Unity closed");
            }
        }

        private static Dictionary<string, object> ReadOrSetAside(string path)
        {
            try
            {
                Dictionary<string, object> data = UploadTaskCheckpoints.Read(path);
                if (Convert.ToInt32(data["version"]) > UploadTaskCheckpoints.Version)
                {
                    throw new InvalidOperationException("It was saved by a newer Build Uploader.");
                }

                return data;
            }
            catch (Exception e)
            {
                // Renamed so it isn't read on every launch
                Debug.LogWarning($"[BuildUploader] Could not read saved upload task {path}: {e.Message} It has been renamed to .invalid.");
                try
                {
                    UploadTaskCheckpoints.Delete(path + ".invalid");
                    File.Move(path, path + ".invalid");
                }
                catch (Exception moveException)
                {
                    Debug.LogWarning($"[BuildUploader] Could not rename {path}: {moveException.Message}");
                }

                return null;
            }
        }

        private static UploadTask Resume(string path, Dictionary<string, object> data, string when)
        {
            UploadTask task;
            try
            {
                task = UploadTask.FromCheckpoint(data, Path.GetDirectoryName(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildUploader] Could not resume upload task '{data["name"]}' {when}: {e.Message}");
                Discard(path, data, $"Could not resume {when}: {e.Message}");
                return null;
            }

            UploadTask.AllTasks.Add(task);
            if (task.StartedFromWindow)
            {
                // The window's own wait for the result didn't survive the reload
                WindowUploadTab.ShowResultWhenComplete(task);
                if (Preferences.AutoFocusNewUploadTask)
                {
                    BuildUploaderTaskWindow.FocusTask(task);
                }
            }

            Debug.Log($"[BuildUploader] Resumed upload task '{task.UploadName}' {when}");
            task.ResumeAsync();
            return task;
        }

        /// <summary>
        /// Deletes a saved task and the cache folders it made, never a Local Path destination's folder, and saves its
        /// report as failed so the Upload Tasks window shows what happened
        /// </summary>
        internal static void Discard(string path, Dictionary<string, object> data, string reason)
        {
            try
            {
                List<object> locations = (List<object>)data["cachedLocations"];
                List<object> needsCleaning = (List<object>)data["cachedLocationNeedsCleaning"];
                for (int i = 0; i < locations.Count && i < needsCleaning.Count; i++)
                {
                    if ((bool)needsCleaning[i] && locations[i] is string location && Directory.Exists(location))
                    {
                        Directory.Delete(location, true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildUploader] Could not delete the cached files of upload task '{data["name"]}': {e.Message}");
            }

            try
            {
                UploadTaskReport report = UploadTaskReport.Deserialize((Dictionary<string, object>)data["report"]);
                Enum.TryParse(data["nextStep"] as string, out AUploadTask_Step.StepType step);
                UploadTaskReport.StepResult result = report.NewReport(step);
                result.SetFailed(reason);
                result.SetPercentComplete(1f);
                report.Complete();

                UploadTask discarded = new UploadTask();
                discarded.SetReport(report);
                UploadTask.AllTasks.Add(discarded);

                IEnumerable<string> configGUIDs = ((List<object>)data["configs"]).Select(a => ((Dictionary<string, object>)a)["guid"] as string);
                _ = UploadTask.SaveReport(report, configGUIDs.ToList());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildUploader] Could not save the report of upload task '{data["name"]}': {e.Message}");
            }

            UploadTaskCheckpoints.Delete(path);
        }
    }
}
