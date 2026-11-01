using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// #107: a BuildConfig whose scripting defines differ from the editor's makes Unity recompile scripts, and the
    /// domain reload destroys the running UploadTask. The task must resume from its checkpoint, build the player,
    /// save its report and stay in the Upload Tasks window. Outside batch mode only: batch mode builds before the
    /// editor recompiles, so it passes there either way.
    /// </summary>
    public class ScriptingDefineBuildTests
    {
        private const string AddedDefine = "BUILD_UPLOADER_TEST_CHANGED_DEFINES";

        private static Task s_run;

        // The test runner keeps serialized fields across a domain reload; locals and statics are reset
        [SerializeField] private bool m_changedEditor;
        [SerializeField] private BuildTarget m_target;
        [SerializeField] private string m_root;
        [SerializeField] private string m_taskGuid;
        [SerializeField] private string[] m_editorDefines;
        [SerializeField] private bool m_previousSaveReport;

        [UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        [Timeout(20 * 60 * 1000)] // The default 180 s also counts the recompile, the reload and the build after it
        public IEnumerator BuildThatChangesScriptingDefinesFinishes()
        {
            m_target = DesktopBuildTests.ActiveDesktopTarget();
            m_root = TestSupport.OutputDirectory("Builds/" + m_target + "-ChangedDefines");
            m_editorDefines = BuildUtils.GetDefaultScriptingDefines().ToArray();
            Assert.IsFalse(m_editorDefines.Contains(AddedDefine),
                "The editor already has " + AddedDefine + " from an interrupted run. Remove it from the Player Settings scripting defines.");

            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = "Build Uploader changed scripting defines build";
            buildConfig.ExtraScriptingDefines.Add(AddedDefine);
            Assert.IsNotEmpty(buildConfig.SceneGUIDs, "Configure enabled scenes in Build Settings.");

            var config = new UploadConfig();
            config.AddSource(new BuildConfigSource(buildConfig));
            config.AddDestination(new LocalPathDestination(m_root));
            var uploadTask = new UploadTask("Changed scripting defines build " + m_target, config);
            uploadTask.SetBuildDescription("UploadTask build that changes the editor's scripting defines");
            m_taskGuid = uploadTask.GUID;

            m_previousSaveReport = Preferences.AutoSaveReportToCacheFolder;
            Preferences.AutoSaveReportToCacheFolder = true;
            m_changedEditor = true;

            // Started the way the Upload window starts it, so the Upload Tasks window lists it
            UploadTask.AllTasks.Add(uploadTask);
            s_run = uploadTask.StartAsync(false);
            yield return new WaitForTaskOrDomainReload(s_run, 15 * 60);

            if (s_run == null)
            {
                // Scripts reloaded, so UploadTaskResumer carries the task on from its checkpoint once the editor is idle
                yield return TestSupport.WaitUntil(() => UploadTask.AllTasks.Any(task => task.GUID == m_taskGuid), 5 * 60,
                    "The task was not resumed after scripts reloaded");
                UploadTask resumed = UploadTask.AllTasks.First(task => task.GUID == m_taskGuid);
                if (resumed.RunTask != null) // Null when the checkpoint could not be resumed, which the checks below report
                {
                    yield return TestSupport.Await(resumed.RunTask, 15 * 60);
                }
            }
            else
            {
                s_run.GetAwaiter().GetResult();
            }

            var problems = new List<string>();
            UploadTask finished = UploadTask.AllTasks.FirstOrDefault(task => task.GUID == m_taskGuid);
            if (finished != null && !finished.IsSuccessful)
            {
                problems.Add("The task failed:\n" + finished.Report.GetReport());
            }

            if (!DesktopBuildTests.HasPlayer(m_root, m_target))
            {
                problems.Add("No player was built under " + m_root);
            }

            string[] reports = SavedReports(m_taskGuid);
            if (reports.Length == 0)
            {
                problems.Add("No report was saved to " + WindowUploadTab.UploadReportSaveDirectory);
            }

            if (finished == null && reports.Length == 0)
            {
                problems.Add("The Upload Tasks window does not list the task: it is not in UploadTask.AllTasks and has no saved report.");
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [TearDown]
        public void TearDown()
        {
            if (!m_changedEditor)
            {
                return;
            }

            m_changedEditor = false;
            s_run = null;
            Preferences.AutoSaveReportToCacheFolder = m_previousSaveReport;

            // Stop a task that is still running, or the reload after restoring the defines would resume it
            foreach (UploadTask task in UploadTask.AllTasks.Where(task => task.GUID == m_taskGuid && !task.IsComplete))
            {
                task.Cancel();
                task.Abandon();
            }
            UploadTaskCheckpoints.Delete(UploadTaskCheckpoints.FilePath(UploadTaskCheckpoints.FolderPath, m_taskGuid));
            UploadTask.AllTasks.RemoveAll(task => task.GUID == m_taskGuid);
            foreach (string report in SavedReports(m_taskGuid))
            {
                File.Delete(report);
            }

            // ABuildSource never restores editor settings after a build, so the BuildConfig's defines are still applied
            BuildTargetGroup group = BuildUtils.BuildTargetToPlatform();
#if UNITY_2021_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group), m_editorDefines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", m_editorDefines));
#endif
        }

        private static string[] SavedReports(string taskGuid)
        {
            string directory = WindowUploadTab.UploadReportSaveDirectory;
            if (string.IsNullOrEmpty(taskGuid) || !Directory.Exists(directory))
            {
                return new string[0];
            }

            return Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories)
                .Where(path => UploadTaskReport.FromFilePath(path)?.GUID == taskGuid)
                .ToArray();
        }

        /// <summary>
        /// Waits for the task with assembly reloads allowed, as they are outside tests. If scripts reload, the test
        /// resumes after this instruction in the new domain.
        /// </summary>
        private class WaitForTaskOrDomainReload : IEditModeTestYieldInstruction
        {
            public bool ExpectDomainReload => true;
            public bool ExpectedPlaymodeState => false;

            // The StartAsync task, not UploadTask.IsComplete: that is set before the report is saved, and blocking on
            // the rest of StartAsync from the main thread deadlocks
            private readonly Task m_run;
            private readonly double m_timeoutSeconds;

            public WaitForTaskOrDomainReload(Task run, double timeoutSeconds)
            {
                m_run = run;
                m_timeoutSeconds = timeoutSeconds;
            }

            public IEnumerator Perform()
            {
                // The test runner locks reloads while tests run
                EditorApplication.UnlockReloadAssemblies();
                DateTime deadline = DateTime.UtcNow.AddSeconds(m_timeoutSeconds);
                while (!m_run.IsCompleted && DateTime.UtcNow < deadline)
                {
                    yield return null;
                }

                // Only reached when scripts did not reload
                EditorApplication.LockReloadAssemblies();
                if (!m_run.IsCompleted)
                {
                    throw new TimeoutException("UploadTask did not complete within " + m_timeoutSeconds + " seconds.");
                }
            }
        }
    }
}
