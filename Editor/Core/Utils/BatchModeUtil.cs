using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Executes upload tasks and uses the command line arguments to detect GUIDs
        /// Example on how to execute this command with a CLI:
        /// "PATH/TO/UNITY/Unity.exe" -batchmode -quit -projectPath "PATH/TO/PROJECT" -executeMethod Wireframe.BatchModeUtil.Execute -uploadProfile "UPLOAD_PROFILE_GUID"
        /// Repeat -uploadProfile to run several profiles one after another. A profile can be a GUID or a name.
        /// Optional arguments:
        /// -dryRun runs each profile as a dry run (UploadTask.SetDryRun): it makes local changes but nothing leaves the local PC
        /// -uploadDescription "TEXT" sets the upload description, eg: what Steamworks shows for the build
        /// -uploadReportFile "PATH" writes each task's report to that file when it finishes
        /// -async starts the tasks without waiting for them. If the terminal never exits then add -async and the terminal should persist until exiting but complete the task
        /// In batch mode Unity exits with 1 when a profile isn't found or, without -async, when a task fails. Nothing starts if a profile isn't found.
        /// For more see: https://github.com/JamesVeug/UnityBuildUploader/wiki/Starting-a-BuildTask-without-UI
        /// </summary>
        public static void Execute()
        {
            bool successful = Run(Arguments.Parse(Environment.GetCommandLineArgs()), FindProfile);
            if (!successful && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Starts a task for each profile. Waits for each one unless -async is passed.
        /// Returns false if a profile wasn't found, no task started, or a task it waited for failed.
        /// </summary>
        internal static bool Run(Arguments arguments, Func<string, UploadProfile> findProfile)
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
                        task.StartAndBlock(true);
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
