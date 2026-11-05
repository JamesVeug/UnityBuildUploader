using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using StepType = Wireframe.AUploadTask_Step.StepType;
using Status = Wireframe.UploadConfig.UploadActionData.UploadCompleteStatus;
using Trigger = Wireframe.UploadConfig.UploadActionData.UploadTrigger;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// UploadTask.SetDryRun: a dry run upload will make local changes but nothing will leave the local PC. Destinations and
    /// actions with a dry run run with it forced on, local ones run as normal, the rest are skipped, and the configs and
    /// actions the task was given never change.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadTaskDryRunTests
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

            Preferences.CacheFolderPath = TestSupport.OutputDirectory("Cache");
            Preferences.UseLocalDestinationIfAvailable = false;
            Preferences.DeleteCacheAfterUpload = true;
            Preferences.AutoSaveReportToCacheFolder = false;
            UploadTaskCheckpoints.FolderPath = TestSupport.OutputDirectory("Checkpoints");
        }

        [TearDown]
        public void TearDown()
        {
            UploadTaskCheckpoints.FolderPath = m_previousCheckpointFolder;
            Preferences.CacheFolderPath = m_previousCacheFolder;
            Preferences.UseLocalDestinationIfAvailable = m_previousUseLocalDestination;
            Preferences.DeleteCacheAfterUpload = m_previousDeleteCache;
            Preferences.AutoSaveReportToCacheFolder = m_previousSaveReport;
        }

        [UnityTest]
        public IEnumerator DryRunForcesDryRunsOnAndSkipsWhatHasNone()
        {
            string source = Fixture("DryRun", "root.txt", "root");
            string output = Path.Combine(TestSupport.OutputDirectory("DryRunOutput"), "upload");
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            config.AddDestination(new LocalPathDestination(output));
            var dryRunDestination = new UnitTestDryRunDestination();
            config.AddDestination(dryRunDestination);
            var recordingDestination = new UnitTestRecordingDestination();
            config.AddDestination(recordingDestination);
            var configAction = new UnitTestDryRunAction();
            config.AddPostAction(configAction);
            var task = new UploadTask("Core dry run", config);
            var taskDryRunAction = new UploadConfig.UploadActionData(new UnitTestDryRunAction(), Status.Always, Trigger.OnTaskFinished);
            var taskRecordingAction = new UploadConfig.UploadActionData(new UnitTestRecordingAction(), Status.Always, Trigger.OnTaskFinished);
            task.AddAction(taskDryRunAction);
            task.AddAction(taskRecordingAction);
            task.SetDryRun(true);

            yield return Run(task);

            Assert.IsTrue(task.DryRun);
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")), "Local destinations run as normal.");

            List<UploadConfig.DestinationData> destinations = task.UploadConfigs[0].Destinations;
            var ranDestination = (UnitTestDryRunDestination)destinations[1].Destination;
            Assert.AreEqual(1, ranDestination.UploadCount);
            Assert.IsTrue(ranDestination.UploadedAsDryRun, "A destination with a dry run must upload with it on.");
            var skippedDestination = (UnitTestRecordingDestination)destinations[2].Destination;
            Assert.IsFalse(destinations[2].Enabled);
            Assert.AreEqual(0, skippedDestination.UploadCount, "A destination without a dry run must be skipped.");
            Assert.AreEqual(0, skippedDestination.CleanUpCount);

            var ranConfigAction = (UnitTestDryRunAction)task.UploadConfigs[0].PostActions[0].UploadAction;
            Assert.AreEqual(1, ranConfigAction.ExecuteCount);
            Assert.IsTrue(ranConfigAction.ExecutedAsDryRun, "A config action with a dry run must execute with it on.");
            var ranTaskAction = (UnitTestDryRunAction)task.Actions[0].UploadAction;
            Assert.AreEqual(1, ranTaskAction.ExecuteCount);
            Assert.IsTrue(ranTaskAction.ExecutedAsDryRun, "A task action with a dry run must execute with it on.");
            var skippedTaskAction = (UnitTestRecordingAction)task.Actions[1].UploadAction;
            Assert.AreEqual(0, skippedTaskAction.ExecuteCount, "An action without a dry run must be skipped.");

            CollectionAssert.Contains(Logs(task, StepType.Validation), "Config 1 destination [UnitTestRecordingDestination] is skipped because it would send something off the local PC");

            Assert.AreNotSame(config, task.UploadConfigs[0], "A dry run must run a copy of the config.");
            Assert.IsFalse(dryRunDestination.DryRun, "The given destination must not change.");
            Assert.AreEqual(0, dryRunDestination.UploadCount);
            Assert.IsTrue(config.Destinations[2].Enabled, "The given config must not change.");
            Assert.IsFalse(configAction.DryRun, "The given config action must not change.");
            Assert.IsFalse(((UnitTestDryRunAction)taskDryRunAction.UploadAction).DryRun, "The given task action must not change.");
            Assert.AreEqual(Status.Always, taskRecordingAction.WhenToExecute, "The given task action must not change.");
        }

        [UnityTest]
        public IEnumerator NormalTaskLeavesDryRunsOff()
        {
            string source = Fixture("NotDryRun", "root.txt", "root");
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            var dryRunDestination = new UnitTestDryRunDestination();
            config.AddDestination(dryRunDestination);
            var recordingDestination = new UnitTestRecordingDestination();
            config.AddDestination(recordingDestination);
            var task = new UploadTask("Core not dry run", config);

            yield return Run(task);

            Assert.IsFalse(task.DryRun);
            Assert.AreSame(config, task.UploadConfigs[0], "A normal task runs the config it was given.");
            Assert.AreEqual(1, dryRunDestination.UploadCount);
            Assert.IsFalse(dryRunDestination.UploadedAsDryRun);
            Assert.AreEqual(1, recordingDestination.UploadCount);
        }

        [Test]
        public void SteamDryRunIsAPreviewUpload()
        {
            var steam = new SteamUploadDestination();
            Assert.IsFalse(steam.SkippedDuringDryRun, "Create AppFile is on by default, so Steam can preview.");
            Assert.AreEqual(false, steam.Serialize()["m_previewUpload"]);

            steam.SetDryRun(true);

            Assert.IsTrue(steam.DryRun);
            Assert.AreEqual(true, steam.Serialize()["m_previewUpload"]);
        }

        [Test]
        public void SteamWithoutCreateAppFileIsSkippedDuringDryRun()
        {
            var steam = new SteamUploadDestination();
            Dictionary<string, object> data = steam.Serialize();
            data["m_createAppFile"] = false;

            steam.Deserialize(data);

            Assert.IsTrue(steam.SkippedDuringDryRun, "A preview upload needs the app file Steam creates.");
        }

        [Test]
        public void OnlyStepsThatKeepEverythingLocalRunDuringDryRun()
        {
            Assert.IsFalse(new LocalPathDestination().SkippedDuringDryRun, "Local Path only copies on this PC.");
            Assert.IsFalse(new NoUploadDestination().SkippedDuringDryRun, "Nowhere sends nothing.");
            Assert.IsFalse(new InvokeStaticMethodAction().SkippedDuringDryRun, "Invoke Static Method runs project code on this PC.");
            Assert.IsFalse(new ItchioDestination().SkippedDuringDryRun, "Itch's dry run uploads nothing.");
            Assert.IsFalse(new GooglePlayDestination().SkippedDuringDryRun, "Google Play's dry run doesn't upload the binary.");
            Assert.IsFalse(new DiscordMessageChannelAction().SkippedDuringDryRun, "Discord's dry run sends no message.");
            Assert.IsTrue(new XboxUploadDestination().SkippedDuringDryRun, "Xbox's dry run still uploads the package.");
        }

        private static IEnumerator Run(UploadTask task)
        {
            task.SetBuildDescription("Build Uploader core dry run test");
            yield return TestSupport.Execute(task, Timeout);
            Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
        }

        private static List<string> Logs(UploadTask task, StepType step)
        {
            return task.Report.StepResults[step].Values.SelectMany(a => a).SelectMany(a => a.Logs).Select(a => a.Message).ToList();
        }

        /// <summary>Creates a directory from alternating relative-path and contents arguments.</summary>
        private static string Fixture(string name, params string[] pathsAndContents)
        {
            string directory = TestSupport.OutputDirectory(name);
            for (int i = 0; i < pathsAndContents.Length; i += 2)
            {
                string path = Path.Combine(directory, pathsAndContents[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, pathsAndContents[i + 1]);
            }
            return directory;
        }
    }
}
