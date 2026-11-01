#if BUILD_UPLOADER_PIPELINE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// https://docs.unity.com/en-us/unity-cli/unity-cli
    /// unity command build_uploader --profiles true
    ///
    /// Everything is exposed through the single "build_uploader" command. Command names live in one flat
    /// global namespace and the first registration of a name wins, so the package claims exactly one name
    /// and puts each operation behind its own argument.
    /// </summary>
    public static class PipelineCommands
    {
        public const string COMMAND = "build_uploader";
        public const string COMMAND_DESCRIPTION = "Build Uploader: inspect, verify, run and maintain upload profiles and tasks";

        [CliCommand(COMMAND, COMMAND_DESCRIPTION)]
        public static object Command(
            // Listing
            [CliArg("profiles", "Lists every UploadProfile asset (name and GUID)")] bool showProfiles = false,
            [CliArg("active_tasks", "Lists currently running or queued UploadTask instances")] bool showActiveTasks = false,
            [CliArg("source_types", "Lists all registered AUploadSource subclasses (download sources)")] bool showSourceTypes = false,
            [CliArg("modifier_types", "Lists all registered AUploadModifer subclasses")] bool showModifierTypes = false,
            [CliArg("destination_types", "Lists all registered AUploadDestination subclasses")] bool showDestinationTypes = false,
            [CliArg("action_types", "Lists all registered AUploadAction subclasses (pre/post-upload actions)")] bool showActionTypes = false,
            [CliArg("reports", "Lists saved UploadTaskReport files. 'all' for every report, or UploadProfile GUIDs/names to filter. Comma- or space-separated.")] string reports = null,
            [CliArg("cache_summary", "Summarizes the Build Uploader cache folder: full path, size, cached builds and saved reports")] bool cacheSummary = false,

            // Inspection
            [CliArg("verify_profiles", "UploadProfile GUIDs or names to verify, or 'all'. Comma- or space-separated.")] string verifyProfiles = null,
            [CliArg("verify_tasks", "UploadTask GUIDs to verify, or 'all' for every known task. Comma- or space-separated.")] string verifyTasks = null,
            [CliArg("summarize_profiles", "UploadProfile GUIDs or names to summarize, or 'all'. Comma- or space-separated.")] string summarizeProfiles = null,
            [CliArg("summarize_tasks", "UploadTask GUIDs to summarize, or 'all' for every known task. Comma- or space-separated.")] string summarizeTasks = null,
            [CliArg("open_tasks", "UploadTask GUIDs whose report to print, or 'all' for every known task. Comma- or space-separated.")] string openTasks = null,
            [CliArg("errors_only", "With --open_tasks: only include failed steps in the output")] bool errorsOnly = false,

            // Running
            [CliArg("start_tasks", "UploadProfile GUIDs or names to run the full pipeline for, or 'all'. Starts asynchronously - poll --active_tasks. Comma- or space-separated.")] string startTasks = null,
            [CliArg("dry_run_tasks", "UploadProfile GUIDs or names to run with every destination swapped for Nowhere so nothing is uploaded, or 'all'. Comma- or space-separated.")] string dryRunTasks = null,
            [CliArg("cancel_tasks", "UploadTask GUIDs to cancel, or 'all' for every running task. Comma- or space-separated.")] string cancelTasks = null,

            // Mutations
            [CliArg("clone_profiles", "UploadProfile GUIDs or names to clone, or 'all'. Comma- or space-separated.")] string cloneProfiles = null,
            [CliArg("new_name", "With --clone_profiles: name for the cloned profile. Defaults to '<name> (Copy)'.")] string newName = null,
            [CliArg("clone_tasks", "GUIDs of tasks still in memory to re-run as a fresh task instance, or 'all' for every task in memory. Comma- or space-separated.")] string cloneTasks = null,
            [CliArg("delete_profiles", "UploadProfile GUIDs or names to delete, or 'all'. Comma- or space-separated. Requires --confirm.")] string deleteProfiles = null,
            [CliArg("delete_tasks", "UploadTask GUIDs whose saved report and cache folder to delete, or 'all' for every known task. Comma- or space-separated. Requires --confirm.")] string deleteTasks = null,
            [CliArg("clear_cache", "Deletes the contents of the Build Uploader cache folder, keeping saved reports. Requires --confirm.")] bool clearCache = false,
            [CliArg("export_wiki", "Regenerates package documentation via the Editor/Core/Wiki exporter. Requires BUILD_UPLOADER_WIKI.")] bool exportWiki = false,

            // Safety
            [CliArg("confirm", "Apply destructive operations (--delete_profiles, --delete_tasks, --clear_cache). Without it they are refused.")] bool confirm = false)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            // Listing
            if (showProfiles)
            {
                result["profiles"] = ListProfiles();
            }

            if (showActiveTasks)
            {
                result["active_tasks"] = ListActiveTasks();
            }

            if (showSourceTypes)
            {
                result["source_types"] = ListTypes(UIHelpers.SourcesPopup.Values.Select(a => new KeyValuePair<string, Type>(a.DisplayName, a.Type)));
            }

            if (showModifierTypes)
            {
                result["modifier_types"] = ListTypes(UIHelpers.ModifiersPopup.Values.Select(a => new KeyValuePair<string, Type>(a.DisplayName, a.Type)));
            }

            if (showDestinationTypes)
            {
                result["destination_types"] = ListTypes(UIHelpers.DestinationsPopup.Values.Select(a => new KeyValuePair<string, Type>(a.DisplayName, a.Type)));
            }

            if (showActionTypes)
            {
                result["action_types"] = ListTypes(UIHelpers.ActionsPopup.Values.Select(a => new KeyValuePair<string, Type>(a.DisplayName, a.Type)));
            }

            List<string> profilesToReport = SplitArgument(reports);
            if (profilesToReport.Count > 0)
            {
                result["reports"] = ListReports(profilesToReport);
            }

            if (cacheSummary)
            {
                result["cache_summary"] = CacheSummary();
            }

            // Inspection
            List<string> profilesToVerify = ExpandProfiles(SplitArgument(verifyProfiles));
            if (profilesToVerify.Count > 0)
            {
                result["verified_profiles"] = VerifyProfiles(profilesToVerify);
            }

            List<string> tasksToVerify = ExpandTasks(SplitArgument(verifyTasks), TaskScope.Known);
            if (tasksToVerify.Count > 0)
            {
                result["verified_tasks"] = VerifyTasks(tasksToVerify);
            }

            List<string> profilesToSummarize = ExpandProfiles(SplitArgument(summarizeProfiles));
            if (profilesToSummarize.Count > 0)
            {
                result["summarized_profiles"] = SummarizeProfiles(profilesToSummarize);
            }

            List<string> tasksToSummarize = ExpandTasks(SplitArgument(summarizeTasks), TaskScope.Known);
            if (tasksToSummarize.Count > 0)
            {
                result["summarized_tasks"] = SummarizeTasks(tasksToSummarize);
            }

            List<string> tasksToOpen = ExpandTasks(SplitArgument(openTasks), TaskScope.Known);
            if (tasksToOpen.Count > 0)
            {
                result["opened_tasks"] = OpenTasks(tasksToOpen, errorsOnly);
            }

            // Running
            List<string> profilesToStart = ExpandProfiles(SplitArgument(startTasks));
            if (profilesToStart.Count > 0)
            {
                result["started_tasks"] = StartTasks(profilesToStart);
            }

            List<string> profilesToDryRun = ExpandProfiles(SplitArgument(dryRunTasks));
            if (profilesToDryRun.Count > 0)
            {
                result["dry_run_tasks"] = DryRunTasks(profilesToDryRun);
            }

            // 'all' here means every task still running - cancelling a finished task is a no-op.
            List<string> tasksToCancel = ExpandTasks(SplitArgument(cancelTasks), TaskScope.Running);
            if (tasksToCancel.Count > 0)
            {
                result["cancelled_tasks"] = CancelTasks(tasksToCancel);
            }

            // Mutations
            List<string> profilesToClone = ExpandProfiles(SplitArgument(cloneProfiles));
            if (profilesToClone.Count > 0)
            {
                result["cloned_profiles"] = CloneProfiles(profilesToClone, newName);
            }

            // Only tasks in memory keep their config, so 'all' cannot include saved reports here.
            List<string> tasksToClone = ExpandTasks(SplitArgument(cloneTasks), TaskScope.InMemory);
            if (tasksToClone.Count > 0)
            {
                result["cloned_tasks"] = CloneTasks(tasksToClone);
            }

            List<string> profilesToDelete = ExpandProfiles(SplitArgument(deleteProfiles));
            if (profilesToDelete.Count > 0)
            {
                if (!confirm)
                {
                    RequireConfirmation("Delete Profiles", profilesToDelete);
                }

                result["deleted_profiles"] = DeleteProfiles(profilesToDelete);
            }

            List<string> tasksToDelete = ExpandTasks(SplitArgument(deleteTasks), TaskScope.Known);
            if (tasksToDelete.Count > 0)
            {
                if (!confirm)
                {
                    RequireConfirmation("Delete Tasks", tasksToDelete);
                }

                result["deleted_tasks"] = DeleteTasks(tasksToDelete);
            }

            if (clearCache)
            {
                if (!confirm)
                {
                    RequireConfirmation("Clear Cache", new List<string>());
                }

                result["cleared_cache"] = ClearCache();
            }

            if (exportWiki)
            {
                result["exported_wiki"] = ExportWiki();
            }

            if (result.Count == 0)
            {
                result["usage"] = "No operation requested. Pass one of: --profiles, --active_tasks, --source_types, " +
                                  "--modifier_types, --destination_types, --action_types, --reports, --cache_summary, --verify_profiles, " +
                                  "--verify_tasks, --summarize_profiles, --summarize_tasks, --open_tasks, --start_tasks, " +
                                  "--dry_run_tasks, --cancel_tasks, --clone_profiles, --clone_tasks, " +
                                  "--delete_profiles, --delete_tasks, --clear_cache, --export_wiki";
            }

            return result;
        }

        /// <summary>
        /// Multi-value CLI args arrive as one string ("a,b" or "a b"), or as a JSON array when the caller
        /// quotes one. Returns an empty list when the arg was omitted - never null.
        /// </summary>
        private static List<string> SplitArgument(string value)
        {
            List<string> values = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return values;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Replace("\"", "");
            }

            foreach (string part in trimmed.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                values.Add(part.Trim());
            }

            return values;
        }

        /// <summary>Which tasks 'all' means, since not every operation can act on every kind of task.</summary>
        private enum TaskScope
        {
            /// <summary>Tasks in memory plus every saved report.</summary>
            Known,

            /// <summary>Tasks in memory only - the only ones that still hold their config.</summary>
            InMemory,

            /// <summary>Tasks in memory that have not finished.</summary>
            Running
        }

        /// <summary>
        /// True when the caller asked for everything rather than naming ids. 'true' is accepted because
        /// callers reach for it out of habit on the flag arguments.
        /// </summary>
        private static bool IsAllArgument(List<string> values)
        {
            return values.Exists(a =>
                string.Equals(a, "all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "true", StringComparison.OrdinalIgnoreCase) ||
                a == "*");
        }

        /// <summary>Turns 'all' into every UploadProfile GUID. Anything else is passed through untouched.</summary>
        private static List<string> ExpandProfiles(List<string> values)
        {
            if (!IsAllArgument(values))
            {
                return values;
            }

            List<string> profileIds = new List<string>();
            foreach (UploadProfileMeta meta in UploadProfileMeta.LoadFromProjectSettings())
            {
                profileIds.Add(meta.GUID);
            }

            return profileIds;
        }

        /// <summary>Turns 'all' into every task GUID in scope. Anything else is passed through untouched.</summary>
        private static List<string> ExpandTasks(List<string> values, TaskScope scope)
        {
            if (!IsAllArgument(values))
            {
                return values;
            }

            List<string> taskIds = new List<string>();
            foreach (UploadTask task in UploadTask.AllTasks)
            {
                if (scope == TaskScope.Running && task.IsComplete)
                {
                    continue;
                }

                if (!taskIds.Contains(task.GUID))
                {
                    taskIds.Add(task.GUID);
                }
            }

            if (scope == TaskScope.Known)
            {
                foreach (KeyValuePair<string, UploadTaskReport> saved in LoadSavedReports())
                {
                    if (!taskIds.Contains(saved.Value.GUID))
                    {
                        taskIds.Add(saved.Value.GUID);
                    }
                }
            }

            return taskIds;
        }

        /// <summary>
        /// Destructive operations refuse to run without --confirm. When a human is at the editor they get a
        /// dialog instead; headless callers (CI, agents) have no UI so they must pass --confirm explicitly.
        /// </summary>
        private static void RequireConfirmation(string operation, List<string> items)
        {
            string summary = items.Count > 0 ? string.Join("\n", items.ToArray()) : operation;
            string message = string.Format("Are you sure you want to {0}?\n\n{1}", operation.ToLower(), summary);
            if (ConfirmWithUser("Build Uploader - " + operation, message))
            {
                return;
            }

            throw new ArgumentException(string.Format("Refusing to {0}. Pass --confirm to apply.", operation.ToLower()));
        }

        private static bool ConfirmWithUser(string title, string message)
        {
            // Nobody to ask headless, and a modal dialog blocks the main thread the server runs commands on.
            if (Application.isBatchMode || !InternalEditorUtility.isHumanControllingUs)
            {
                return false;
            }

            return EditorUtility.DisplayDialog(title, message, "Yes", "Cancel");
        }

        /// <summary>Resolves a GUID or profile name to the profile's metadata (which holds its file path).</summary>
        private static UploadProfileMeta ResolveProfileMeta(string profileId)
        {
            foreach (UploadProfileMeta meta in UploadProfileMeta.LoadFromProjectSettings())
            {
                if (meta.GUID == profileId || meta.ProfileName == profileId)
                {
                    return meta;
                }
            }

            throw new ArgumentException($"No UploadProfile matched the GUID or name '{profileId}'.");
        }

        /// <summary>Resolves a GUID or profile name to the fully deserialized profile.</summary>
        private static UploadProfile ResolveProfile(string profileId)
        {
            UploadProfileMeta meta = ResolveProfileMeta(profileId);
            UploadProfile profile = UploadProfile.FromPath(meta.FilePath);
            if (profile == null)
            {
                throw new InvalidOperationException($"Failed to load the UploadProfile at '{meta.FilePath}'.");
            }

            return profile;
        }

        /// <summary>A task that is still in memory this editor session - the only kind that holds live state.</summary>
        private static UploadTask FindLiveTask(string taskId)
        {
            foreach (UploadTask task in UploadTask.AllTasks)
            {
                if (task.GUID == taskId)
                {
                    return task;
                }
            }

            return null;
        }

        /// <summary>Every report written to the cache folder, paired with the file it came from.</summary>
        private static List<KeyValuePair<string, UploadTaskReport>> LoadSavedReports()
        {
            List<KeyValuePair<string, UploadTaskReport>> loaded = new List<KeyValuePair<string, UploadTaskReport>>();
            if (!Directory.Exists(WindowUploadTab.UploadReportSaveDirectory))
            {
                return loaded;
            }

            string[] filePaths = Directory.GetFiles(WindowUploadTab.UploadReportSaveDirectory, "*.txt", SearchOption.AllDirectories);
            foreach (string filePath in filePaths)
            {
                UploadTaskReport report;
                try
                {
                    report = UploadTaskReport.FromFilePath(filePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BuildUploader] Failed to read upload report '{filePath}': {e.Message}");
                    continue;
                }

                if (report != null)
                {
                    loaded.Add(new KeyValuePair<string, UploadTaskReport>(filePath, report));
                }
            }

            return loaded;
        }

        /// <summary>Live tasks win over saved reports - they have progress the file does not.</summary>
        private static UploadTaskReport ResolveReport(string taskId, out bool isLive)
        {
            UploadTask liveTask = FindLiveTask(taskId);
            if (liveTask != null && liveTask.Report != null)
            {
                isLive = true;
                return liveTask.Report;
            }

            isLive = false;
            foreach (KeyValuePair<string, UploadTaskReport> saved in LoadSavedReports())
            {
                if (saved.Value.GUID == taskId)
                {
                    return saved.Value;
                }
            }

            throw new ArgumentException($"No UploadTask or saved report matched the GUID '{taskId}'.");
        }

        private static Dictionary<string, string> ListProfiles()
        {
            Dictionary<string, string> profileLookup = new Dictionary<string, string>();
            foreach (UploadProfileMeta meta in UploadProfileMeta.LoadFromProjectSettings())
            {
                profileLookup[meta.GUID] = meta.ProfileName;
            }

            return profileLookup;
        }

        private static List<ActiveTaskResult> ListActiveTasks()
        {
            List<ActiveTaskResult> tasks = new List<ActiveTaskResult>();
            foreach (UploadTask task in UploadTask.AllTasks)
            {
                if (task.IsComplete)
                {
                    continue;
                }

                ActiveTaskResult activeTask = new ActiveTaskResult();
                activeTask.guid = task.GUID;
                activeTask.name = task.UploadName;
                activeTask.description = task.UploadDescription;
                activeTask.started = task.HasStarted;
                activeTask.step = task.CurrentStepType.ToString();
                activeTask.percentComplete = task.PercentComplete;
                tasks.Add(activeTask);
            }

            return tasks;
        }

        private static List<TypeResult> ListTypes(IEnumerable<KeyValuePair<string, Type>> types)
        {
            List<TypeResult> listed = new List<TypeResult>();
            foreach (KeyValuePair<string, Type> type in types)
            {
                TypeResult result = new TypeResult();
                result.name = type.Key;
                result.type = type.Value?.FullName;
                result.description = type.Value?.GetCustomAttribute<WikiAttribute>()?.Text;
                listed.Add(result);
            }

            return listed;
        }

        /// <summary>
        /// Saved reports record the profile they ran as their name, so filtering resolves each id to a
        /// profile name first. 'all' lists every report, including ones whose profile has since been
        /// deleted - which is why this does not go through ExpandProfiles.
        /// </summary>
        private static List<ReportResult> ListReports(List<string> profileIds)
        {
            bool listAll = IsAllArgument(profileIds);

            List<string> profileNames = new List<string>();
            if (!listAll)
            {
                foreach (string profileId in profileIds)
                {
                    profileNames.Add(ResolveProfileMeta(profileId).ProfileName);
                }
            }

            List<ReportResult> found = new List<ReportResult>();
            foreach (KeyValuePair<string, UploadTaskReport> saved in LoadSavedReports())
            {
                UploadTaskReport report = saved.Value;
                if (!listAll && !profileNames.Contains(report.Name))
                {
                    continue;
                }

                ReportResult result = new ReportResult();
                result.task = report.GUID;
                result.name = report.Name;
                result.file = saved.Key;
                result.startTime = report.StartTime.ToString("u");
                result.duration = report.Duration.ToString();
                result.successful = report.Successful;
                found.Add(result);
            }

            return found;
        }

        /// <summary>
        /// Walks the cache folder to report what is actually sitting on disk. A cached build is one config's
        /// staged output, so a task that ran three configs leaves three of them behind. The report counts
        /// come from parsing each file, so a malformed report is counted in 'reports' but not in the
        /// successful/failed split.
        /// </summary>
        private static CacheSummaryResult CacheSummary()
        {
            CacheSummaryResult result = new CacheSummaryResult();
            result.path = Path.GetFullPath(Preferences.CacheFolderPath);
            result.reportsPath = Path.GetFullPath(WindowUploadTab.UploadReportSaveDirectory);
            result.defaultPath = Path.GetFullPath(Preferences.DefaultCacheFolder);
            result.isDefaultPath = string.Equals(result.path, result.defaultPath, StringComparison.OrdinalIgnoreCase);
            result.size = EditorUtility.FormatBytes(0);

            // A build that dies at the cache step is usually out of disk, so the free space matters more
            // than the size already used.
            string cacheRoot = Path.GetPathRoot(result.path);
            try
            {
                DriveInfo drive = new DriveInfo(cacheRoot);
                result.driveFreeSpaceBytes = drive.AvailableFreeSpace;
                result.driveFreeSpace = EditorUtility.FormatBytes(result.driveFreeSpaceBytes);
            }
            catch (Exception e)
            {
                result.driveFreeSpace = $"<unavailable: {e.GetType().Name}>";
            }

            string projectRoot = Path.GetPathRoot(Path.GetFullPath(Application.dataPath));
            result.sameDriveAsProject = string.Equals(cacheRoot, projectRoot, StringComparison.OrdinalIgnoreCase);

            result.exists = Directory.Exists(result.path);
            if (result.exists)
            {
                foreach (string file in Directory.EnumerateFiles(result.path, "*", SearchOption.AllDirectories))
                {
                    result.sizeBytes += new FileInfo(file).Length;
                    result.files++;
                }

                result.size = EditorUtility.FormatBytes(result.sizeBytes);

                string uploadTasksPath = Path.Combine(result.path, "UploadTasks");
                if (Directory.Exists(uploadTasksPath))
                {
                    DateTime oldestBuild = DateTime.MaxValue;
                    DateTime newestBuild = DateTime.MinValue;

                    string[] taskFolders = Directory.GetDirectories(uploadTasksPath);
                    result.cachedTasks = taskFolders.Length;
                    foreach (string taskFolder in taskFolders)
                    {
                        foreach (string buildFolder in Directory.GetDirectories(taskFolder))
                        {
                            result.builds++;

                            DateTime written = Directory.GetLastWriteTimeUtc(buildFolder);
                            if (written < oldestBuild)
                            {
                                oldestBuild = written;
                            }

                            if (written > newestBuild)
                            {
                                newestBuild = written;
                            }
                        }
                    }

                    if (result.builds > 0)
                    {
                        result.oldestBuild = oldestBuild.ToString("u");
                        result.newestBuild = newestBuild.ToString("u");
                    }
                }
            }

            if (Directory.Exists(result.reportsPath))
            {
                result.reports = Directory.GetFiles(result.reportsPath, "*.txt", SearchOption.AllDirectories).Length;
            }

            DateTime oldestReport = DateTime.MaxValue;
            DateTime newestReport = DateTime.MinValue;
            foreach (KeyValuePair<string, UploadTaskReport> saved in LoadSavedReports())
            {
                if (saved.Value.Successful)
                {
                    result.reportsSuccessful++;
                }
                else
                {
                    result.reportsFailed++;
                }

                if (saved.Value.StartTime < oldestReport)
                {
                    oldestReport = saved.Value.StartTime;
                }

                if (saved.Value.StartTime > newestReport)
                {
                    newestReport = saved.Value.StartTime;
                }
            }

            if (result.reportsSuccessful + result.reportsFailed > 0)
            {
                result.oldestReport = oldestReport.ToString("u");
                result.newestReport = newestReport.ToString("u");
            }

            return result;
        }

        /// <summary>
        /// Runs the same validation the Validation step runs, without fetching a source or touching a
        /// remote service - everything that can be answered from the serialized config alone.
        /// </summary>
        private static List<VerifiedProfileResult> VerifyProfiles(List<string> profileIds)
        {
            List<VerifiedProfileResult> verified = new List<VerifiedProfileResult>();
            foreach (string profileId in profileIds)
            {
                UploadProfile profile = ResolveProfile(profileId);

                VerifiedProfileResult result = new VerifiedProfileResult();
                result.guid = profile.GUID;
                result.name = profile.ProfileName;
                result.valid = profile.UploadConfigs.Count > 0;

                foreach (UploadConfig config in profile.UploadConfigs)
                {
                    VerifiedConfigResult configResult = new VerifiedConfigResult();
                    configResult.guid = config.GUID;
                    configResult.enabled = config.Enabled;
                    configResult.valid = config.CanStartBuild(out string reason);
                    configResult.reason = reason;
                    configResult.errors = config.GetAllErrors().ConvertAll(e => e.text);
                    configResult.warnings = config.GetAllWarnings().ConvertAll(w => w.text);
                    result.configs.Add(configResult);

                    if (config.Enabled && !configResult.valid)
                    {
                        result.valid = false;
                    }
                }

                foreach (UploadConfig.UploadActionData action in profile.Actions)
                {
                    if (action.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never)
                    {
                        continue;
                    }

                    if (action.UploadAction == null)
                    {
                        result.actionErrors.Add("Action is not setup");
                        continue;
                    }

                    List<GUIContent> errors = new List<GUIContent>();
                    action.UploadAction.TryGetErrors(errors);
                    result.actionErrors.AddRange(errors.ConvertAll(e => e.text));
                }

                if (result.actionErrors.Count > 0)
                {
                    result.valid = false;
                }

                verified.Add(result);
            }

            return verified;
        }

        private static List<VerifiedTaskResult> VerifyTasks(List<string> taskIds)
        {
            List<VerifiedTaskResult> verified = new List<VerifiedTaskResult>();
            foreach (string taskId in taskIds)
            {
                UploadTaskReport report = ResolveReport(taskId, out bool isLive);
                UploadTask liveTask = isLive ? FindLiveTask(taskId) : null;

                VerifiedTaskResult result = new VerifiedTaskResult();
                result.guid = taskId;
                result.name = report.Name;
                result.live = isLive;
                result.complete = liveTask == null || liveTask.IsComplete;
                foreach ((AUploadTask_Step.StepType Key, string FailReason) reason in report.GetFailReasons())
                {
                    result.failReasons.Add($"{reason.Key}: {reason.FailReason}");
                }

                result.valid = report.Successful && result.failReasons.Count == 0;
                verified.Add(result);
            }

            return verified;
        }

        private static List<ProfileSummaryResult> SummarizeProfiles(List<string> profileIds)
        {
            List<ProfileSummaryResult> summarized = new List<ProfileSummaryResult>();
            foreach (string profileId in profileIds)
            {
                UploadProfile profile = ResolveProfile(profileId);

                ProfileSummaryResult result = new ProfileSummaryResult();
                result.guid = profile.GUID;
                result.name = profile.ProfileName;
                result.actions = SummarizeActions(profile.Actions);

                foreach (UploadConfig config in profile.UploadConfigs)
                {
                    ConfigSummaryResult configResult = new ConfigSummaryResult();
                    configResult.guid = config.GUID;
                    configResult.enabled = config.Enabled;
                    configResult.postActions = SummarizeActions(config.PostActions);

                    foreach (UploadConfig.SourceData source in config.Sources)
                    {
                        configResult.sources.Add(SummarizeItem(source.Enabled, source.Source,
                            UIHelpers.SourcesPopup.GetDisplayNameFromType(source.Source?.GetType())));
                    }

                    foreach (UploadConfig.ModifierData modifier in config.Modifiers)
                    {
                        configResult.modifiers.Add(SummarizeItem(modifier.Enabled, modifier.Modifier,
                            UIHelpers.ModifiersPopup.GetDisplayNameFromType(modifier.Modifier?.GetType())));
                    }

                    foreach (UploadConfig.DestinationData destination in config.Destinations)
                    {
                        configResult.destinations.Add(SummarizeItem(destination.Enabled, destination.Destination,
                            UIHelpers.DestinationsPopup.GetDisplayNameFromType(destination.Destination?.GetType())));
                    }

                    result.configs.Add(configResult);
                }

                summarized.Add(result);
            }

            return summarized;
        }

        private static ItemSummaryResult SummarizeItem(bool enabled, object item, string typeName)
        {
            ItemSummaryResult result = new ItemSummaryResult();
            result.enabled = enabled;
            result.type = typeName;
            result.description = item?.GetType().GetCustomAttribute<WikiAttribute>()?.Text;

            // Summary() is GUI code that assumes a live context. A profile read straight off disk has never
            // been attached to a task, so a source can throw while building its label - the label is a
            // nicety, not a reason to fail the whole command.
            try
            {
                if (item is AUploadSource source)
                {
                    result.summary = source.Summary();
                }
                else if (item is AUploadModifer modifier)
                {
                    result.summary = modifier.Summary();
                }
                else if (item is AUploadDestination destination)
                {
                    result.summary = destination.Summary();
                }
            }
            catch (Exception e)
            {
                result.summary = $"<unavailable: {e.GetType().Name}>";
            }

            return result;
        }

        private static List<ActionSummaryResult> SummarizeActions(List<UploadConfig.UploadActionData> actions)
        {
            List<ActionSummaryResult> summarized = new List<ActionSummaryResult>();
            foreach (UploadConfig.UploadActionData action in actions)
            {
                ActionSummaryResult result = new ActionSummaryResult();
                result.type = UIHelpers.ActionsPopup.GetDisplayNameFromType(action.UploadAction?.GetType());
                result.description = action.UploadAction?.GetType().GetCustomAttribute<WikiAttribute>()?.Text;
                result.whenToExecute = action.WhenToExecute.ToString();
                result.triggers = action.Triggers.ConvertAll(t => t.ToString());
                summarized.Add(result);
            }

            return summarized;
        }

        private static List<TaskSummaryResult> SummarizeTasks(List<string> taskIds)
        {
            List<TaskSummaryResult> summarized = new List<TaskSummaryResult>();
            foreach (string taskId in taskIds)
            {
                UploadTaskReport report = ResolveReport(taskId, out bool isLive);
                UploadTask liveTask = isLive ? FindLiveTask(taskId) : null;

                TaskSummaryResult result = new TaskSummaryResult();
                result.guid = report.GUID;
                result.name = report.Name;
                result.live = isLive;
                result.complete = liveTask == null || liveTask.IsComplete;
                result.successful = report.Successful;
                result.startTime = report.StartTime.ToString("u");
                result.endTime = report.EndTime.ToString("u");
                result.duration = report.Duration.ToString();

                foreach (AUploadTask_Step.StepType stepType in Enum.GetValues(typeof(AUploadTask_Step.StepType)))
                {
                    if (!report.StepResults.TryGetValue(stepType, out var processes))
                    {
                        continue;
                    }

                    StepSummaryResult stepResult = new StepSummaryResult();
                    stepResult.step = stepType.ToString();
                    stepResult.logs = report.CountStepLogs(stepType);

                    float progress = 0f;
                    foreach (List<UploadTaskReport.StepResult> results in processes.Values)
                    {
                        foreach (UploadTaskReport.StepResult stepProcessResult in results)
                        {
                            stepResult.results++;
                            progress += stepProcessResult.PercentComplete;
                            if (!stepProcessResult.Successful)
                            {
                                stepResult.failed++;
                            }

                            if (stepProcessResult.IsSkipped)
                            {
                                stepResult.skipped++;
                            }
                        }
                    }

                    stepResult.percentComplete = stepResult.results > 0 ? progress / stepResult.results : 0f;
                    result.steps.Add(stepResult);
                }

                summarized.Add(result);
            }

            return summarized;
        }

        private static List<OpenedTaskResult> OpenTasks(List<string> taskIds, bool errorsOnly)
        {
            List<OpenedTaskResult> opened = new List<OpenedTaskResult>();
            foreach (string taskId in taskIds)
            {
                UploadTaskReport report = ResolveReport(taskId, out bool isLive);

                OpenedTaskResult result = new OpenedTaskResult();
                result.guid = report.GUID;
                result.name = report.Name;
                result.live = isLive;
                result.successful = report.Successful;
                result.report = errorsOnly ? GetFailedStepsReport(report) : report.GetReport(true);
                opened.Add(result);
            }

            return opened;
        }

        /// <summary>The failed steps only, in the same shape GetReport uses for a whole task.</summary>
        private static string GetFailedStepsReport(UploadTaskReport report)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var stepTypeLookup in report.StepResults)
            {
                foreach (var processLookup in stepTypeLookup.Value)
                {
                    for (int i = 0; i < processLookup.Value.Count; i++)
                    {
                        UploadTaskReport.StepResult stepResult = processLookup.Value[i];
                        if (stepResult.Successful)
                        {
                            continue;
                        }

                        sb.AppendLine($"== -- {stepTypeLookup.Key} -- ==");
                        sb.AppendLine($"-- {processLookup.Key} {i + 1} --");
                        sb.AppendLine($"[FAILED] {stepResult.FailReason}");
                        foreach (UploadTaskReport.StepResult.Log log in stepResult.Logs)
                        {
                            sb.AppendLine($"[{log.Type}] {log.Message}");
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : "No failed steps.";
        }

        /// <summary>
        /// Starts each profile asynchronously. Blocking would hold the main thread the pipeline server
        /// runs commands on, so the caller polls --active_tasks instead.
        /// </summary>
        private static List<StartedTaskResult> StartTasks(List<string> profileIds)
        {
            List<StartedTaskResult> started = new List<StartedTaskResult>();
            foreach (string profileId in profileIds)
            {
                UploadProfile profile = ResolveProfile(profileId);
                started.Add(StartProfile(profile, $"Started from the CLI ({profileId})"));
            }

            return started;
        }

        /// <summary>
        /// Swaps every destination for a NoUploadDestination so sources and modifiers run but nothing is
        /// uploaded. The profile on disk is untouched - the swap happens on a deserialized copy.
        /// </summary>
        private static List<StartedTaskResult> DryRunTasks(List<string> profileIds)
        {
            List<StartedTaskResult> started = new List<StartedTaskResult>();
            foreach (string profileId in profileIds)
            {
                UploadProfile profile = CloneProfileInMemory(ResolveProfile(profileId));
                foreach (UploadConfig config in profile.UploadConfigs)
                {
                    config.Destinations.Clear();
                    config.AddDestination(new NoUploadDestination());
                }

                started.Add(StartProfile(profile, $"Dry run from the CLI ({profileId})"));
            }

            return started;
        }

        private static StartedTaskResult StartProfile(UploadProfile profile, string description)
        {
            UploadTask task = new UploadTask(profile);
            task.SetBuildDescription(description);
            UploadTask.AllTasks.Add(task);
            task.Start();

            StartedTaskResult result = new StartedTaskResult();
            result.task = task.GUID;
            result.profile = profile.GUID;
            result.name = profile.ProfileName;
            result.status = "started";
            return result;
        }

        private static List<CancelledTaskResult> CancelTasks(List<string> taskIds)
        {
            List<CancelledTaskResult> cancelled = new List<CancelledTaskResult>();
            foreach (string taskId in taskIds)
            {
                UploadTask task = FindLiveTask(taskId);
                if (task == null)
                {
                    throw new ArgumentException($"No running UploadTask matched the GUID '{taskId}'. " +
                                                "Only tasks started in this editor session can be cancelled.");
                }

                CancelledTaskResult result = new CancelledTaskResult();
                result.task = taskId;
                result.step = task.CurrentStepType.ToString();

                if (task.IsComplete)
                {
                    result.status = "already_complete";
                }
                else
                {
                    task.Cancel();
                    result.status = "cancelling";
                }

                cancelled.Add(result);
            }

            return cancelled;
        }

        /// <summary>Round-trips the profile through its saved-data form, the same way the GUI duplicates one.</summary>
        private static UploadProfile CloneProfileInMemory(UploadProfile profile)
        {
            UploadProfileSavedData data = UploadProfileSavedData.FromUploadProfile(profile);
            string json = JSON.SerializeObject(data);
            return UploadProfileSavedData.FromJSON(json).ToUploadProfile();
        }

        private static List<ClonedProfileResult> CloneProfiles(List<string> profileIds, string newName)
        {
            List<ClonedProfileResult> cloned = new List<ClonedProfileResult>();
            for (int i = 0; i < profileIds.Count; i++)
            {
                UploadProfile source = ResolveProfile(profileIds[i]);
                UploadProfile clone = CloneProfileInMemory(source);
                clone.GUID = Guid.NewGuid().ToString().Substring(0, 6);

                if (string.IsNullOrWhiteSpace(newName))
                {
                    clone.ProfileName = source.ProfileName + " (Copy)";
                }
                else
                {
                    // One name for many clones would collide, so number them after the first.
                    clone.ProfileName = profileIds.Count > 1 ? $"{newName} ({i + 1})" : newName;
                }

                string filePath = Path.Combine(WindowUploadTab.UploadProfilePath, $"{clone.GUID}.json");
                if (!Directory.Exists(WindowUploadTab.UploadProfilePath))
                {
                    Directory.CreateDirectory(WindowUploadTab.UploadProfilePath);
                }

                File.WriteAllText(filePath, JSON.SerializeObject(UploadProfileSavedData.FromUploadProfile(clone)));

                ClonedProfileResult result = new ClonedProfileResult();
                result.source = source.GUID;
                result.guid = clone.GUID;
                result.name = clone.ProfileName;
                result.file = filePath;
                cloned.Add(result);
            }

            return cloned;
        }

        /// <summary>
        /// Re-runs a task's exact config. Only tasks still in memory hold their configs - saved reports are
        /// text, so a task from a previous session cannot be cloned.
        /// </summary>
        private static List<ClonedTaskResult> CloneTasks(List<string> taskIds)
        {
            List<ClonedTaskResult> cloned = new List<ClonedTaskResult>();
            foreach (string taskId in taskIds)
            {
                UploadTask source = FindLiveTask(taskId);
                if (source == null)
                {
                    throw new ArgumentException($"No UploadTask matched the GUID '{taskId}'. Only tasks from this " +
                                                "editor session keep their config - saved reports cannot be re-run.");
                }

                UploadTask clone = new UploadTask(source.UploadName, source.UploadConfigs, source.Actions);
                clone.SetBuildDescription($"Cloned from task {taskId}");
                UploadTask.AllTasks.Add(clone);
                clone.Start();

                ClonedTaskResult result = new ClonedTaskResult();
                result.source = taskId;
                result.task = clone.GUID;
                result.name = clone.UploadName;
                result.status = "started";
                cloned.Add(result);
            }

            return cloned;
        }

        private static List<DeletedProfileResult> DeleteProfiles(List<string> profileIds)
        {
            List<DeletedProfileResult> deleted = new List<DeletedProfileResult>();
            foreach (string profileId in profileIds)
            {
                UploadProfileMeta meta = ResolveProfileMeta(profileId);
                if (!File.Exists(meta.FilePath))
                {
                    throw new InvalidOperationException($"UploadProfile '{meta.ProfileName}' has no file at '{meta.FilePath}'.");
                }

                File.Delete(meta.FilePath);

                DeletedProfileResult result = new DeletedProfileResult();
                result.guid = meta.GUID;
                result.name = meta.ProfileName;
                result.file = meta.FilePath;
                deleted.Add(result);
            }

            return deleted;
        }

        /// <summary>Deletes a task's saved report file and the cache folder its sources were staged in.</summary>
        private static List<DeletedTaskResult> DeleteTasks(List<string> taskIds)
        {
            List<KeyValuePair<string, UploadTaskReport>> savedReports = LoadSavedReports();

            List<DeletedTaskResult> deleted = new List<DeletedTaskResult>();
            foreach (string taskId in taskIds)
            {
                if (UploadTask.IsRunning(taskId))
                {
                    throw new ArgumentException($"UploadTask '{taskId}' is still running. Cancel it first.");
                }

                DeletedTaskResult result = new DeletedTaskResult();
                result.task = taskId;

                // Or it would be resumed after the next script reload
                string checkpoint = UploadTaskCheckpoints.FilePath(UploadTaskCheckpoints.FolderPath, taskId);
                if (File.Exists(checkpoint))
                {
                    UploadTaskCheckpoints.Delete(checkpoint);
                    result.deleted.Add(checkpoint);
                }

                foreach (KeyValuePair<string, UploadTaskReport> saved in savedReports)
                {
                    if (saved.Value.GUID != taskId || !File.Exists(saved.Key))
                    {
                        continue;
                    }

                    File.Delete(saved.Key);
                    result.deleted.Add(saved.Key);
                }

                // Cache folders are named "<task name> (<guid>)" - see UploadTaskStep_Validate.
                string uploadTasksPath = Path.Combine(Preferences.CacheFolderPath, "UploadTasks");
                if (Directory.Exists(uploadTasksPath))
                {
                    foreach (string directory in Directory.GetDirectories(uploadTasksPath, $"*({taskId})", SearchOption.TopDirectoryOnly))
                    {
                        Directory.Delete(directory, true);
                        result.deleted.Add(directory);
                    }
                }

                if (result.deleted.Count == 0)
                {
                    throw new ArgumentException($"No saved report or cache folder matched the UploadTask GUID '{taskId}'.");
                }

                deleted.Add(result);
            }

            return deleted;
        }

        /// <summary>
        /// Empties the cache folder but keeps the saved reports that live inside it - those are the record of
        /// past uploads, and --delete_tasks is the way to remove them.
        /// </summary>
        private static ClearCacheResult ClearCache()
        {
            ClearCacheResult result = new ClearCacheResult();
            result.path = Preferences.CacheFolderPath;
            result.keptReports = WindowUploadTab.UploadReportSaveDirectory;

            if (!Directory.Exists(result.path))
            {
                result.status = "nothing_to_clear";
                return result;
            }

            string reportsPath = Path.GetFullPath(WindowUploadTab.UploadReportSaveDirectory);
            foreach (string directory in Directory.GetDirectories(result.path))
            {
                if (string.Equals(Path.GetFullPath(directory), reportsPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Directory.Delete(directory, true);
                result.deleted.Add(directory);
            }

            foreach (string file in Directory.GetFiles(result.path))
            {
                File.Delete(file);
                result.deleted.Add(file);
            }

            result.status = "cleared";
            return result;
        }

        private static ExportWikiResult ExportWiki()
        {
#if BUILD_UPLOADER_WIKI
            Wiki.ExportWikiData();

            ExportWikiResult result = new ExportWikiResult();
            result.status = "exported";
            result.path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Wiki"));
            return result;
#else
            throw new InvalidOperationException("Exporting the wiki requires the BUILD_UPLOADER_WIKI scripting define.");
#endif
        }

        // Results returned to the CLI. Field names are the JSON keys the caller sees, so they are named the
        // way the rest of the pipeline commands name theirs rather than in the usual C# casing.

        private class ActiveTaskResult
        {
            public string guid;
            public string name;
            public string description;
            public bool started;
            public string step;
            public float percentComplete;
        }

        private class TypeResult
        {
            public string name;
            public string type;
            public string description;
        }

        private class CacheSummaryResult
        {
            public string path;
            public bool exists;
            public bool isDefaultPath;
            public string defaultPath;
            public string size;
            public long sizeBytes;
            public int files;
            public int builds;
            public int cachedTasks;
            public string oldestBuild;
            public string newestBuild;
            public string driveFreeSpace;
            public long driveFreeSpaceBytes;
            public bool sameDriveAsProject;
            public string reportsPath;
            public int reports;
            public int reportsSuccessful;
            public int reportsFailed;
            public string oldestReport;
            public string newestReport;
        }

        private class ReportResult
        {
            public string task;
            public string name;
            public string file;
            public string startTime;
            public string duration;
            public bool successful;
        }

        private class VerifiedProfileResult
        {
            public string guid;
            public string name;
            public bool valid;
            public List<VerifiedConfigResult> configs = new List<VerifiedConfigResult>();
            public List<string> actionErrors = new List<string>();
        }

        private class VerifiedConfigResult
        {
            public string guid;
            public bool enabled;
            public bool valid;
            public string reason;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        private class VerifiedTaskResult
        {
            public string guid;
            public string name;
            public bool live;
            public bool complete;
            public bool valid;
            public List<string> failReasons = new List<string>();
        }

        private class ProfileSummaryResult
        {
            public string guid;
            public string name;
            public List<ConfigSummaryResult> configs = new List<ConfigSummaryResult>();
            public List<ActionSummaryResult> actions = new List<ActionSummaryResult>();
        }

        private class ConfigSummaryResult
        {
            public string guid;
            public bool enabled;
            public List<ItemSummaryResult> sources = new List<ItemSummaryResult>();
            public List<ItemSummaryResult> modifiers = new List<ItemSummaryResult>();
            public List<ItemSummaryResult> destinations = new List<ItemSummaryResult>();
            public List<ActionSummaryResult> postActions = new List<ActionSummaryResult>();
        }

        private class ItemSummaryResult
        {
            public bool enabled;
            public string type;
            public string description;
            public string summary;
        }

        private class ActionSummaryResult
        {
            public string type;
            public string description;
            public string whenToExecute;
            public List<string> triggers = new List<string>();
        }

        private class TaskSummaryResult
        {
            public string guid;
            public string name;
            public bool live;
            public bool complete;
            public bool successful;
            public string startTime;
            public string endTime;
            public string duration;
            public List<StepSummaryResult> steps = new List<StepSummaryResult>();
        }

        private class StepSummaryResult
        {
            public string step;
            public int results;
            public int failed;
            public int skipped;
            public float percentComplete;
            public int logs;
        }

        private class OpenedTaskResult
        {
            public string guid;
            public string name;
            public bool live;
            public bool successful;
            public string report;
        }

        private class StartedTaskResult
        {
            public string task;
            public string profile;
            public string name;
            public string status;
        }

        private class CancelledTaskResult
        {
            public string task;
            public string status;
            public string step;
        }

        private class ClonedProfileResult
        {
            public string source;
            public string guid;
            public string name;
            public string file;
        }

        private class ClonedTaskResult
        {
            public string source;
            public string task;
            public string name;
            public string status;
        }

        private class DeletedProfileResult
        {
            public string guid;
            public string name;
            public string file;
        }

        private class DeletedTaskResult
        {
            public string task;
            public List<string> deleted = new List<string>();
        }

        private class ClearCacheResult
        {
            public string path;
            public string status;
            public string keptReports;
            public List<string> deleted = new List<string>();
        }

        private class ExportWikiResult
        {
            public string status;
            public string path;
        }
    }
}
#endif
