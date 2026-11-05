using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public static partial class BatchModeUtil
    {
        private const string ProfileArgument = "-uploadProfile";
        private const string AsyncArgument = "-async";
        private const string DryRunArgument = "-dryRun";
        private const string DescriptionArgument = "-uploadDescription";
        private const string ReportFileArgument = "-uploadReportFile";
        private const string QuitArgument = "-quit";

        /// <summary>
        /// Executes upload tasks and uses the command line arguments to detect GUIDs
        /// Example on how to execute this command with a CLI:
        /// "PATH/TO/UNITY/Unity.exe" -batchmode -projectPath "PATH/TO/PROJECT" -executeMethod Wireframe.BatchModeUtil.Execute -uploadProfile "UPLOAD_PROFILE_GUID"
        /// Don't pass -quit: Unity would quit as soon as this method returns, before the tasks run. In batch mode this
        /// exits Unity itself once the tasks finish, with 0 when they all succeeded and 1 otherwise.
        /// Repeat -uploadProfile to run several profiles one after another. A profile can be a GUID or a name.
        /// Optional arguments:
        /// -dryRun runs each profile as a dry run (UploadTask.SetDryRun): it makes local changes but nothing leaves the local PC
        /// -uploadDescription "TEXT" sets the upload description, eg: what Steamworks shows for the build
        /// -uploadReportFile "PATH" writes each task's report to that file when it finishes
        /// -async starts the tasks together without waiting for them, and doesn't exit Unity when they finish
        /// In batch mode Unity exits with 1 when a profile isn't found or, without -async, when a task fails. Nothing starts if a profile isn't found.
        /// For more see: https://github.com/JamesVeug/UnityBuildUploader/wiki/Starting-a-BuildTask-without-UI
        /// </summary>
        public static void Execute()
        {
            string[] commandLine = Environment.GetCommandLineArgs();
            Arguments arguments = Arguments.Parse(commandLine);
            if (Application.isBatchMode && !arguments.Async && Array.Exists(commandLine, a => a.Equals(QuitArgument, StringComparison.OrdinalIgnoreCase)))
            {
                string error = $"No tasks were started. Remove {QuitArgument}: Unity quits as soon as {nameof(Execute)} returns, before the tasks run. {nameof(BatchModeUtil)} exits Unity itself when they finish.";
                Debug.LogError(error);
                DeleteReport(arguments.ReportFile);
                AppendReport(arguments.ReportFile, error);
                EditorApplication.Exit(1);
                return;
            }

            _ = ExecuteAsync(arguments);
        }

        /// <summary>
        /// Runs the tasks without blocking the main thread, which their awaits continue on, then exits Unity in batch mode
        /// </summary>
        private static async Task ExecuteAsync(Arguments arguments)
        {
            bool successful = false;
            try
            {
                successful = await RunAsync(arguments, FindProfile);
            }
            finally
            {
                if (Application.isBatchMode && (!arguments.Async || !successful))
                {
                    EditorApplication.Exit(successful ? 0 : 1);
                }
            }
        }

        /// <summary>
        /// Starts a task for each profile. Waits for each one unless -async is passed.
        /// Returns false if a profile wasn't found, no task started, or a task it waited for failed.
        /// Never wait for this with GetResult or Wait on the main thread: the tasks continue on it, so that deadlocks.
        /// </summary>
        internal static async Task<bool> RunAsync(Arguments arguments, Func<string, UploadProfile> findProfile)
        {
            DeleteReport(arguments.ReportFile);
            try
            {
                List<UploadTask> tasks = CreateTasks(arguments, findProfile, out string error);
                if (tasks == null)
                {
                    Debug.LogError(error);
                    AppendReport(arguments.ReportFile, error);
                    return false;
                }

                bool successful = true;
                foreach (UploadTask task in tasks)
                {
                    WriteReportWhenComplete(task, arguments.ReportFile);
                    string dryRun = task.DryRun ? " as a dry run" : "";
                    if (arguments.Async)
                    {
                        Debug.Log($"Starting async task with guid {task.GUID} with Upload Profile: {task.UploadName}{dryRun}!");
                        task.Start();
                    }
                    else
                    {
                        Debug.Log($"Starting task with guid {task.GUID} with Upload Profile: {task.UploadName}{dryRun}!");
                        await task.StartAsync(true);
                        successful &= task.IsSuccessful;
                    }
                }

                return successful;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                AppendReport(arguments.ReportFile, e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Makes a task for each profile without starting it. Returns null with the reason in error when a profile
        /// passed with -uploadProfile doesn't exist or none were found, so nothing starts.
        /// </summary>
        internal static List<UploadTask> CreateTasks(Arguments arguments, Func<string, UploadProfile> findProfile, out string error)
        {
            List<UploadTask> tasks = new List<UploadTask>();
            List<string> missing = new List<string>();
            foreach (string profileId in arguments.Profiles)
            {
                UploadProfile profile = findProfile(profileId);
                if (profile == null)
                {
                    if (arguments.ProfilesNamed)
                    {
                        missing.Add(profileId);
                    }

                    continue;
                }

                UploadTask task = new UploadTask(profile);
                if (arguments.Description != null)
                {
                    task.SetBuildDescription(arguments.Description);
                }

                task.SetDryRun(arguments.DryRun);
                tasks.Add(task);
            }

            if (missing.Count > 0)
            {
                error = $"No tasks were started. No profile found with GUID or Name : {string.Join(", ", missing)}";
                return null;
            }

            if (tasks.Count == 0)
            {
                error = $"No tasks were started. Check arguments for {ProfileArgument} \"GUID\"";
                return null;
            }

            error = null;
            return tasks;
        }

        /// <summary>
        /// Appends the task's report to the report file when it finishes
        /// </summary>
        internal static void WriteReportWhenComplete(UploadTask task, string reportFile)
        {
            task.OnComplete += (report) =>
            {
                Debug.Log($"Finished task with guid {report.GUID}!");
                AppendReport(reportFile, report.GetReport(true));
            };
        }

        /// <summary>
        /// Removes the report from an earlier run so the file only holds this run's reports
        /// </summary>
        internal static void DeleteReport(string reportFile)
        {
            if (string.IsNullOrEmpty(reportFile))
            {
                return;
            }

            try
            {
                if (File.Exists(reportFile))
                {
                    File.Delete(reportFile);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void AppendReport(string reportFile, string text)
        {
            if (string.IsNullOrEmpty(reportFile))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportFile)));
                File.AppendAllText(reportFile, text + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static UploadProfile FindProfile(string profileId)
        {
            return UploadProfile.FromGUID(profileId) ?? UploadProfile.FromProfileName(profileId);
        }
    }
}
