using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using StepType = Wireframe.AUploadTask_Step.StepType;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// The report file is written when a task starts and kept up to date while it runs, so the Upload Tasks window
    /// shows running tasks and ones that stopped without finishing.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadTaskReportFileTests
    {
        private const double Timeout = 120;

        private string m_previousCacheFolder;
        private bool m_previousUseLocalDestination;
        private bool m_previousDeleteCache;
        private bool m_previousSaveReport;
        private string m_previousCheckpointFolder;

        [SetUp]
        public void SetUp()
        {
            m_previousCacheFolder = Preferences.CacheFolderPath;
            m_previousUseLocalDestination = Preferences.UseLocalDestinationIfAvailable;
            m_previousDeleteCache = Preferences.DeleteCacheAfterUpload;
            m_previousSaveReport = Preferences.AutoSaveReportToCacheFolder;
            m_previousCheckpointFolder = UploadTaskCheckpoints.FolderPath;

            // The report folder is inside the cache folder
            Preferences.CacheFolderPath = TestSupport.OutputDirectory("ReportCache");
            Preferences.UseLocalDestinationIfAvailable = false;
            Preferences.DeleteCacheAfterUpload = true;
            Preferences.AutoSaveReportToCacheFolder = true;
            UploadTaskCheckpoints.FolderPath = TestSupport.OutputDirectory("Checkpoints");
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.CacheFolderPath = m_previousCacheFolder;
            Preferences.UseLocalDestinationIfAvailable = m_previousUseLocalDestination;
            Preferences.DeleteCacheAfterUpload = m_previousDeleteCache;
            Preferences.AutoSaveReportToCacheFolder = m_previousSaveReport;
            UploadTaskCheckpoints.FolderPath = m_previousCheckpointFolder;
        }

        [UnityTest]
        public IEnumerator ReportIsWrittenWhenTheTaskStartsAndFollowsIt()
        {
            var gate = new UnitTestGateSource(Fixture("LiveReport"));
            var task = new UploadTask("Core live report", Config(gate));

            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => Read(task).Contains("Current Step: GetSources"), Timeout, "The report was not written while the task ran");

            string running = Read(task);
            StringAssert.Contains("In Progress: True", running);
            StringAssert.DoesNotContain("End Time:", running);
            StringAssert.Contains(Logs(task, StepType.Validation).First(), running, "Finished steps must be in the report while the task runs.");
            UploadTaskReport parsed = UploadTaskReport.FromFilePath(ReportPath(task));
            Assert.IsTrue(parsed.InProgress);
            Assert.AreEqual(StepType.GetSources, parsed.CurrentStep);

            gate.Release();
            yield return TestSupport.Await(task.RunTask, Timeout);

            string finished = Read(task);
            StringAssert.Contains("In Progress: False", finished);
            StringAssert.Contains("End Time:", finished);
            Assert.IsFalse(UploadTaskReport.FromFilePath(ReportPath(task)).InProgress);
            CollectionAssert.IsEmpty(Directory.GetFiles(WindowUploadTab.UploadReportSaveDirectory, "*.tmp"), "No temporary report file may be left behind.");
        }

        [UnityTest]
        public IEnumerator ResumedTaskWritesToTheSameReport()
        {
            var gate = new UnitTestGateSource(Fixture("ResumedReport"));
            var task = new UploadTask("Core resumed report", Config(gate));
            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => gate.HasEntered && Read(task).Contains("Current Step: GetSources"), Timeout, "The task did not reach the gate");
            Dictionary<string, object> checkpoint = UploadTaskCheckpoints.Read(UploadTaskCheckpoints.FilePath(UploadTaskCheckpoints.FolderPath, task.GUID));
            task.Abandon();
            yield return TestSupport.Await(task.ReportWrite, Timeout); // A real reload ends it with the old scripts

            UploadTask resumed = UploadTask.FromCheckpoint(checkpoint, UploadTaskCheckpoints.FolderPath);
            _ = resumed.ResumeAsync();
            var resumedGate = (UnitTestGateSource)resumed.UploadConfigs[0].Sources[0].Source;
            yield return TestSupport.WaitUntil(() => resumedGate.HasEntered, Timeout, "The resumed task did not reach the gate");
            resumedGate.Release();
            yield return TestSupport.Await(resumed.RunTask, Timeout);

            Assert.AreEqual(ReportPath(task), ReportPath(resumed));
            string finished = Read(resumed);
            StringAssert.Contains("In Progress: False", finished);
            StringAssert.Contains("Scripts reloaded during GetSources", finished);
            Assert.AreEqual(1, Directory.GetFiles(WindowUploadTab.UploadReportSaveDirectory, "*.txt").Length, "The resumed task must not start a second report.");
        }

        [Test]
        public void ReportSavedBeforeLiveReportsLoadsAsComplete()
        {
            string path = Path.Combine(TestSupport.OutputDirectory("OldReport"), "UploadReport_abcde_2025-09-04_10-27-57.txt");
            File.WriteAllText(path, "Upload Task Report\nGUID: abc123\nName: Old report\n" +
                                    "Start Time: 4/09/2025 10:27:57 PM Local: 5/09/2025 10:27:57 AM\n" +
                                    "End Time: 4/09/2025 10:28:35 PM Local: 5/09/2025 10:28:35 AM\n" +
                                    "Duration: 00:00:37.4792449\nSuccessful: True\nCancelled: False\n\n" +
                                    "== -- Validation -- ==\n-- Intra 1 --\n[Info] No errors found\n");

            UploadTaskReport report = UploadTaskReport.FromFilePath(path);
            var task = new UploadTask();
            task.SetReport(report);

            Assert.IsFalse(report.InProgress);
            Assert.IsTrue(task.IsComplete);
            Assert.AreEqual(1f, task.PercentComplete);
            Assert.AreEqual("Success", BuildUploaderTaskWindow.GetState(task, false, false, out _));
        }

        [Test]
        public void WindowShowsWhetherAReportedTaskCanStillFinish()
        {
            var report = new UploadTaskReport("abc123", "Core window state", false);
            report.Start();
            report.CurrentStep = StepType.GetSources;
            var task = new UploadTask();
            task.SetReport(UploadTaskReport.FromFilePath(Write(report)));

            Assert.IsFalse(task.IsComplete, "A report saved while its task ran is not complete.");
            Assert.AreEqual(StepType.GetSources, task.CurrentStepType);
            Assert.AreEqual(BuildUploaderTaskWindow.WillResumeState, BuildUploaderTaskWindow.GetState(task, false, true, out _), "A saved task will resume.");
            Assert.AreEqual(BuildUploaderTaskWindow.InterruptedState, BuildUploaderTaskWindow.GetState(task, false, false, out _), "Nothing will finish it.");
            Assert.AreEqual("In Progress", BuildUploaderTaskWindow.GetState(task, true, false, out _), "A task running in this editor is in progress.");

            report.Complete();
            task.SetReport(UploadTaskReport.FromFilePath(Write(report)));
            Assert.AreEqual("Success", BuildUploaderTaskWindow.GetState(task, false, false, out _));
        }

        private static string Write(UploadTaskReport report)
        {
            string path = Path.Combine(TestSupport.OutputDirectory("WindowState"), "report.txt");
            File.WriteAllText(path, report.GetReport());
            return path;
        }

        private static string ReportPath(UploadTask task)
        {
            return UploadTask.ReportPath(task.UploadConfigs.Select(a => a.GUID), task.Report.StartTime);
        }

        /// <summary>Empty while the file is missing, including the moment it is being replaced.</summary>
        private static string Read(UploadTask task)
        {
            try
            {
                return task.Report == null ? "" : File.ReadAllText(ReportPath(task));
            }
            catch (IOException)
            {
                return "";
            }
        }

        private static List<string> Logs(UploadTask task, StepType step)
        {
            return task.Report.StepResults[step].Values.SelectMany(a => a).SelectMany(a => a.Logs).Select(a => a.Message).ToList();
        }

        private static UploadConfig Config(AUploadSource source)
        {
            var config = new UploadConfig();
            config.AddSource(source);
            config.AddDestination(new NoUploadDestination());
            return config;
        }

        private static string Fixture(string name)
        {
            string directory = TestSupport.OutputDirectory(name);
            File.WriteAllText(Path.Combine(directory, name + ".txt"), name);
            return directory;
        }
    }
}
