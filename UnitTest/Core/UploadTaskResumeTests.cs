using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Decision = Wireframe.UploadTaskResumer.Decision;
using StepType = Wireframe.AUploadTask_Step.StepType;
using Status = Wireframe.UploadConfig.UploadActionData.UploadCompleteStatus;
using Trigger = Wireframe.UploadConfig.UploadActionData.UploadTrigger;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Running tasks save a checkpoint between steps so they carry on after a script reload (#107). A Core test
    /// can't reload scripts, so each test stops a task at a gate, abandons it as the reload would, and resumes a
    /// new task from its checkpoint.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadTaskResumeTests
    {
        private const double Timeout = 120;

        private string m_previousCacheFolder;
        private bool m_previousUseLocalDestination;
        private bool m_previousDeleteCache;
        private bool m_previousSaveReport;
        private string m_previousCheckpointFolder;
        private List<UploadTask> m_tasksBefore;
        private List<UploadTaskResumer.PendingTask> m_pendingBefore;

        [SetUp]
        public void SetUp()
        {
            m_previousCacheFolder = Preferences.CacheFolderPath;
            m_previousUseLocalDestination = Preferences.UseLocalDestinationIfAvailable;
            m_previousDeleteCache = Preferences.DeleteCacheAfterUpload;
            m_previousSaveReport = Preferences.AutoSaveReportToCacheFolder;
            m_previousCheckpointFolder = UploadTaskCheckpoints.FolderPath;
            m_tasksBefore = UploadTask.AllTasks.ToList();
            m_pendingBefore = UploadTaskResumer.Pending.ToList();

            Preferences.CacheFolderPath = TestSupport.OutputDirectory("ResumeCache");
            Preferences.UseLocalDestinationIfAvailable = false;
            Preferences.DeleteCacheAfterUpload = true;
            Preferences.AutoSaveReportToCacheFolder = false;
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
            UploadTask.AllTasks.RemoveAll(task => !m_tasksBefore.Contains(task));
            UploadTaskResumer.Pending.RemoveAll(task => !m_pendingBefore.Contains(task));
        }

        [UnityTest]
        public IEnumerator ResumedTaskCarriesOnWithoutRepeatingFinishedWork()
        {
            string folder = Fixture("ResumeFolder", "folder.txt", "folder");
            string counted = Fixture("ResumeCounted", "counted.txt", "counted");
            string gated = Fixture("ResumeGated", "gated.txt", "gated");
            string output = Output("Resume");
            string countId = Guid.NewGuid().ToString("N");
            var gate = new UnitTestGateSource(gated);
            var config = new UploadConfig();
            config.AddSource(new FolderSource(folder));
            config.AddSource(new UnitTestCountingSource(countId, counted));
            config.AddSource(gate);
            config.AddDestination(new LocalPathDestination(output));
            var task = new UploadTask("Core resume", config);
            task.AddAction(new UploadConfig.UploadActionData(new UnitTestRecordingAction(), Status.Always, Trigger.OnTaskStarted));

            yield return StartUntil(task, gate);
            string uploadNumber = task.Context.FormatString(Context.UPLOAD_NUMBER_KEY);
            int uploadsStarted = BuildUploaderProjectSettings.Instance.TotalUploadTasksStarted;
            File.WriteAllText(Path.Combine(task.CachedLocations[0], "marker.txt"), "Validation must not run again");
            UploadTask resumed = Resume(Abandon(task));
            var startedAction = (UnitTestRecordingAction)resumed.Actions[0].UploadAction;
            yield return ReleaseAndFinish(resumed);

            Assert.IsTrue(resumed.IsSuccessful, resumed.Report.GetReport());
            Assert.AreEqual(task.GUID, resumed.GUID);
            Assert.AreEqual(uploadNumber, resumed.Context.FormatString(Context.UPLOAD_NUMBER_KEY), "The resumed task must keep its upload number.");
            Assert.AreEqual(uploadsStarted, BuildUploaderProjectSettings.Instance.TotalUploadTasksStarted, "Resuming must not count another upload.");
            Assert.AreEqual(0, startedAction.ExecuteCount, "OnTaskStarted actions must not run again.");
            Assert.AreEqual(2, UnitTestCountingSource.Runs(countId), "A source without run state must run again.");
            CollectionAssert.Contains(Logs(resumed, StepType.GetSources), "Already got this source before scripts reloaded");
            Assert.IsTrue(resumed.Report.StepResults.ContainsKey(StepType.Validation), "The report must keep the steps from before the reload.");
            foreach (string file in new[] { "folder.txt", "counted.txt", "gated.txt", "marker.txt" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(output, file)), "Missing " + file + " in " + output);
            }
            Assert.IsFalse(File.Exists(CheckpointPath(task)), "The checkpoint must be deleted when the task finishes.");
        }

        [UnityTest]
        public IEnumerator ResumedDryRunStaysADryRun()
        {
            string gated = Fixture("ResumeDryRunGated", "gated.txt", "gated");
            var config = new UploadConfig();
            config.AddSource(new UnitTestGateSource(gated));
            config.AddDestination(new UnitTestDryRunDestination());
            config.AddDestination(new UnitTestRecordingDestination());
            var task = new UploadTask("Core resume dry run", config);
            task.SetDryRun(true);

            // A dry run runs a copy of the config, so wait on the copy's gate
            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => Gate(task).HasEntered, Timeout, "The task did not reach the gate");
            UploadTask resumed = Resume(Abandon(task));
            yield return ReleaseAndFinish(resumed);

            Assert.IsTrue(resumed.IsSuccessful, resumed.Report.GetReport());
            Assert.IsTrue(resumed.DryRun, "A resumed dry run must stay a dry run.");
            List<UploadConfig.DestinationData> destinations = resumed.UploadConfigs[0].Destinations;
            Assert.IsTrue(((UnitTestDryRunDestination)destinations[0].Destination).UploadedAsDryRun, "The forced dry run must survive the reload.");
            Assert.IsFalse(destinations[1].Enabled, "A destination without a dry run must stay skipped.");
            Assert.AreEqual(0, ((UnitTestRecordingDestination)destinations[1].Destination).UploadCount);
        }

        [UnityTest]
        public IEnumerator ResumedDryRunForcesDryRunWhereItIsNotSaved()
        {
            string gated = Fixture("ResumeUnsavedDryRunGated", "gated.txt", "gated");
            var config = new UploadConfig();
            config.AddSource(new UnitTestGateSource(gated));
            config.AddDestination(new UnitTestUnsavedDryRunDestination());
            config.AddPostAction(new UnitTestUnsavedDryRunAction());
            var task = new UploadTask("Core resume unsaved dry run", config);
            task.AddAction(new UploadConfig.UploadActionData(new UnitTestUnsavedDryRunAction(), Status.Always, Trigger.OnTaskFinished));
            task.SetDryRun(true);

            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => Gate(task).HasEntered, Timeout, "The task did not reach the gate");
            UploadTask resumed = Resume(Abandon(task));
            yield return ReleaseAndFinish(resumed);

            Assert.IsTrue(resumed.IsSuccessful, resumed.Report.GetReport());
            var destination = (UnitTestUnsavedDryRunDestination)resumed.UploadConfigs[0].Destinations[0].Destination;
            Assert.AreEqual(1, destination.UploadCount);
            Assert.IsTrue(destination.UploadedAsDryRun, "A destination that doesn't save its dry run must still upload as one after a reload.");
            var configAction = (UnitTestUnsavedDryRunAction)resumed.UploadConfigs[0].PostActions[0].UploadAction;
            Assert.AreEqual(1, configAction.ExecuteCount);
            Assert.IsTrue(configAction.ExecutedAsDryRun, "A config action that doesn't save its dry run must still execute as one after a reload.");
            var taskAction = (UnitTestUnsavedDryRunAction)resumed.Actions[0].UploadAction;
            Assert.AreEqual(1, taskAction.ExecuteCount);
            Assert.IsTrue(taskAction.ExecutedAsDryRun, "A task action that doesn't save its dry run must still execute as one after a reload.");
        }

        [UnityTest]
        public IEnumerator InterruptedUploadFailsThenCleansUp()
        {
            string source = Fixture("ResumeUpload", "root.txt", "root");
            var gate = new UnitTestGateDestination();
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            config.AddDestination(gate);
            var task = new UploadTask("Core interrupted upload", config);
            task.AddAction(new UploadConfig.UploadActionData(new UnitTestRecordingAction(), Status.IfFailed, Trigger.OnTaskFinished));
            task.AddAction(new UploadConfig.UploadActionData(new UnitTestRecordingAction(), Status.IfSuccessful, Trigger.OnTaskFinished));

            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => gate.HasEntered, Timeout, "The task did not reach Upload");
            Dictionary<string, object> checkpoint = Abandon(task);
            Assert.AreEqual(StepType.Upload.ToString(), checkpoint["nextStep"]);
            UploadTask resumed = Resume(checkpoint);
            yield return TestSupport.Await(resumed.ResumeAsync(), Timeout);

            Assert.IsFalse(resumed.IsSuccessful);
            AssertFailedAt(resumed, StepType.Upload, "can't be re-run safely");
            Assert.AreEqual(1, ((UnitTestRecordingAction)resumed.Actions[0].UploadAction).ExecuteCount, "IfFailed actions must run after the interrupted upload.");
            Assert.AreEqual(0, ((UnitTestRecordingAction)resumed.Actions[1].UploadAction).ExecuteCount, "IfSuccessful actions must not run.");
            var resumedGate = (UnitTestGateDestination)resumed.UploadConfigs[0].Destinations[0].Destination;
            Assert.IsFalse(resumedGate.HasEntered, "Upload must not run again.");
            Assert.AreEqual(1, resumedGate.CleanUpCount, "Cleanup must still run.");
            Assert.IsFalse(Directory.Exists(resumed.CachedLocations[0]), "Cleanup must still delete the cache.");
        }

        [UnityTest]
        public IEnumerator InterruptedActionsAfterAStepDoNotRunTheStepAgain()
        {
            string gated = Fixture("ResumeActions", "gated.txt", "gated");
            string output = Output("ResumeActions");
            var gate = new UnitTestGateSource(gated);
            var task = new UploadTask("Core interrupted actions", Config(gate, new LocalPathDestination(output)));

            yield return StartUntil(task, gate);
            Dictionary<string, object> checkpoint = Abandon(task);
            checkpoint["stepPhase"] = "Actions";
            UploadTask resumed = Resume(checkpoint);
            yield return TestSupport.Await(resumed.ResumeAsync(), Timeout);

            Assert.IsTrue(resumed.IsSuccessful, resumed.Report.GetReport());
            Assert.IsFalse(Gate(resumed).HasEntered, "GetSources must not run again.");
            Assert.IsTrue(Logs(resumed, StepType.GetSources).Any(log => log.Contains("won't run again")), resumed.Report.GetReport());
            Assert.IsTrue(File.Exists(Path.Combine(output, "gated.txt")));
        }

        [UnityTest]
        public IEnumerator ReloadsAreCountedUntilTheStepFails()
        {
            string folder = Fixture("ResumeCountFolder", "folder.txt", "folder");
            string gated = Fixture("ResumeCountGated", "gated.txt", "gated");
            var gate = new UnitTestGateSource(gated);
            var config = new UploadConfig();
            config.AddSource(new FolderSource(folder));
            config.AddSource(gate);
            config.AddDestination(new NoUploadDestination());
            var task = new UploadTask("Core reload loop", config);

            yield return StartUntil(task, gate);
            Dictionary<string, object> checkpoint = Abandon(task);
            for (int reloads = 1; reloads < UploadTask.MaxInterruptions; reloads++)
            {
                UploadTask resumed = Resume(checkpoint);
                _ = resumed.ResumeAsync();
                yield return TestSupport.WaitUntil(() => Gate(resumed).HasEntered, Timeout, "The resumed task did not reach the gate");
                checkpoint = Abandon(resumed);
                Assert.AreEqual(reloads, Convert.ToInt32(checkpoint["interruptions"]), "Each resume of the same step must be counted.");
                CollectionAssert.Contains((List<object>)checkpoint["completedSources"], "0:0", "Finished sources must be kept across resumes.");
            }

            UploadTask last = Resume(checkpoint);
            yield return TestSupport.Await(last.ResumeAsync(), Timeout);

            Assert.IsFalse(last.IsSuccessful);
            AssertFailedAt(last, StepType.GetSources, "Scripts reloaded " + UploadTask.MaxInterruptions + " times");
            Assert.IsFalse(Gate(last).HasEntered, "The step must not run a third time.");
        }

        [UnityTest]
        public IEnumerator CheckpointThatNoLongerLoadsIsFailedNotResumed()
        {
            var gate = new UnitTestGateSource(Fixture("ResumeMissing", "gated.txt", "gated"));
            var task = new UploadTask("Core missing type", Config(gate, new NoUploadDestination()));

            yield return StartUntil(task, gate);
            Dictionary<string, object> checkpoint = Abandon(task);
            var sources = (List<object>)((Dictionary<string, object>)((List<object>)checkpoint["configs"])[0])["sources"];
            ((Dictionary<string, object>)sources[0])["sourceType"] = "Wireframe.UnitTest.DeletedSource";
            string path = CheckpointPath(task);
            UploadTaskCheckpoints.Write(UploadTaskCheckpoints.FolderPath, task.GUID, checkpoint);

            LogAssert.Expect(LogType.Error, new Regex("Cannot find type 'Wireframe.UnitTest.DeletedSource'"));
            List<UploadTask> resumed = UploadTaskResumer.ResumeOrHold(new[] { path });

            CollectionAssert.IsEmpty(resumed);
            Assert.IsFalse(File.Exists(path), "A checkpoint that can't resume must be deleted.");
            UploadTask failed = UploadTask.AllTasks.Last(a => a.GUID == task.GUID);
            Assert.IsTrue(failed.IsComplete);
            Assert.IsFalse(failed.IsSuccessful);
            AssertFailedAt(failed, StepType.GetSources, "Could not resume");
        }

        [Test]
        public void ResumerDecidesBySession()
        {
            string session = UploadTaskResumer.SessionId;
            Assert.AreEqual(Decision.Resume, UploadTaskResumer.Decide(session, "unknown"), "A reload in this session resumes without asking.");
            Assert.AreEqual(Decision.Wait, UploadTaskResumer.Decide("closed session", "unknown"), "A task from before Unity closed waits for the user.");

            var running = new UploadTask();
            UploadTask.AllTasks.Add(running);
            Assert.AreEqual(Decision.Skip, UploadTaskResumer.Decide(session, running.GUID), "A task still running must not start twice.");
        }

        [UnityTest]
        public IEnumerator TaskFromAClosedSessionWaitsUntilResumedFromTheWindow()
        {
            var gate = new UnitTestGateSource(Fixture("ResumeYes", "gated.txt", "gated"));
            var task = new UploadTask("Core closed session", Config(gate, new NoUploadDestination()));
            yield return StartUntil(task, gate);
            Dictionary<string, object> checkpoint = Abandon(task);
            checkpoint["sessionId"] = "closed session";
            UploadTaskCheckpoints.Write(UploadTaskCheckpoints.FolderPath, task.GUID, checkpoint);

            List<UploadTask> resumedAtLoad = UploadTaskResumer.ResumeOrHold(new[] { CheckpointPath(task) });
            UploadTaskResumer.ResumeOrHold(new[] { CheckpointPath(task) });

            CollectionAssert.IsEmpty(resumedAtLoad, "A task from before Unity closed must wait for the user.");
            UploadTaskResumer.PendingTask pending = UploadTaskResumer.Pending.Single(a => a.GUID == task.GUID);
            Assert.AreEqual("Core closed session", pending.Name);
            Assert.AreEqual(StepType.GetSources.ToString(), pending.Step);
            Assert.AreEqual(task.Report.StartTime.Ticks, pending.StartTime.Ticks);
            Assert.IsTrue(File.Exists(CheckpointPath(task)), "The checkpoint must be kept until the user decides.");

            UploadTask resumed = UploadTaskResumer.Resume(pending);

            Assert.IsNotNull(resumed);
            CollectionAssert.DoesNotContain(UploadTaskResumer.Pending, pending);
            Assert.IsNull(UploadTaskResumer.Resume(pending), "Pressing Resume twice must not start the task twice.");
            CollectionAssert.Contains(UploadTask.AllTasks, resumed);
            yield return TestSupport.WaitUntil(() => Gate(resumed).HasEntered, Timeout, "The resumed task did not reach the gate");
            Assert.AreEqual(UploadTaskResumer.SessionId, UploadTaskCheckpoints.Read(CheckpointPath(task))["sessionId"], "A resumed task belongs to this session.");
            Gate(resumed).Release();
            yield return TestSupport.Await(resumed.RunTask, Timeout);
            Assert.IsTrue(resumed.IsSuccessful, resumed.Report.GetReport());
        }

        [UnityTest]
        public IEnumerator TaskFromAClosedSessionCanBeDiscardedFromTheWindow()
        {
            Preferences.UseLocalDestinationIfAvailable = true;
            Preferences.AutoSaveReportToCacheFolder = true;
            string localOutput = Output("DiscardLocal");
            UploadConfig local = Config(new FolderSource(Fixture("DiscardFolder", "folder.txt", "folder")), new LocalPathDestination(localOutput));
            var gate = new UnitTestGateSource(Fixture("DiscardGated", "gated.txt", "gated"));
            UploadConfig generated = Config(gate, new NoUploadDestination());
            var task = new UploadTask("Core discard", new List<UploadConfig> { local, generated });
            yield return StartUntil(task, gate);
            Dictionary<string, object> checkpoint = Abandon(task);
            yield return TestSupport.Await(task.ReportWrite, Timeout); // A real reload ends it with the old scripts
            checkpoint["sessionId"] = "closed session";
            UploadTaskCheckpoints.Write(UploadTaskCheckpoints.FolderPath, task.GUID, checkpoint);

            CollectionAssert.IsEmpty(UploadTaskResumer.ResumeOrHold(new[] { CheckpointPath(task) }));
            UploadTaskResumer.Discard(UploadTaskResumer.Pending.Single(a => a.GUID == task.GUID));

            Assert.IsFalse(UploadTaskResumer.Pending.Any(a => a.GUID == task.GUID));
            Assert.IsFalse(File.Exists(CheckpointPath(task)), "Discarding must delete the checkpoint.");
            Assert.IsTrue(Directory.Exists(localOutput), "A Local Path destination's folder must never be deleted.");
            Assert.IsFalse(Directory.Exists(task.CachedLocations[1]), "The cache folder the task made must be deleted.");
            UploadTask discarded = UploadTask.AllTasks.Last(a => a.GUID == task.GUID);
            Assert.IsFalse(discarded.IsSuccessful);
            AssertFailedAt(discarded, StepType.GetSources, "Not resumed after Unity closed");

            // The report was written while the task ran, so wait for it to be written again as failed
            string report = UploadTask.ReportPath(new List<string> { local.GUID, generated.GUID }, task.Report.StartTime);
            yield return TestSupport.WaitUntil(() => ReadOrEmpty(report).Contains("Not resumed after Unity closed"), Timeout,
                "The discarded task's report was not saved as failed");
            StringAssert.Contains("Successful: False", ReadOrEmpty(report));
            StringAssert.Contains("In Progress: False", ReadOrEmpty(report));
        }

        [Test]
        public void ReportSurvivesSerialization()
        {
            var report = new UploadTaskReport("abc123", "Core report", false);
            report.SetProcess(AUploadTask_Step.StepProcess.Intra);
            UploadTaskReport.StepResult logged = report.NewReport(StepType.GetSources);
            logged.AddLog("line one\nline two");
            logged.AddWarning("C:\\new_build \"quoted\"");
            logged.AddError("an error");
            logged.SetPercentComplete(0.5f);
            report.NewReport(StepType.Upload).SetFailed("failed without logs");
            report.SetProcess(AUploadTask_Step.StepProcess.Post);
            report.NewReport(StepType.Upload).SetSkipped("skipped");

            UploadTaskReport restored = UploadTaskReport.Deserialize(RoundTrip(report.Serialize()));

            Assert.AreEqual(report.GetReport(), restored.GetReport());
            Assert.AreEqual(report.StartTime, restored.StartTime);
            Assert.AreEqual(DateTimeKind.Utc, restored.StartTime.Kind);
            Assert.IsFalse(restored.Successful);
            UploadTaskReport.StepResult restoredLogged = restored.StepResults[StepType.GetSources][AUploadTask_Step.StepProcess.Intra][0];
            Assert.AreEqual(0.5f, restoredLogged.PercentComplete);
            CollectionAssert.AreEqual(logged.Logs.Select(a => a.Type + a.Message), restoredLogged.Logs.Select(a => a.Type + a.Message));
            Assert.AreEqual("failed without logs", restored.StepResults[StepType.Upload][AUploadTask_Step.StepProcess.Intra][0].FailReason);
            Assert.IsTrue(restored.StepResults[StepType.Upload][AUploadTask_Step.StepProcess.Post][0].IsSkipped);
        }

        [Test]
        public void BuildConfigSourceKeepsItsConfigAndArchitecture()
        {
            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = "Core code-built config";
            buildConfig.ExtraScriptingDefines.Add("BUILD_UPLOADER_CORE_TEST");
            var source = new BuildConfigSource(buildConfig);
            source.SetPlatformOverride(BuildTargetGroup.Standalone, 0, BuildTarget.StandaloneOSX, BuildUtils.Architecture.ARM64);
            source.Prepare(TestSupport.OutputDirectory("BuildConfigRunState"), TestSupport.Report(), new CancellationTokenSource());

            var reloaded = new BuildConfigSource();
            reloaded.Deserialize(RoundTrip(source.Serialize()));

            Assert.AreEqual(BuildUtils.Architecture.ARM64, reloaded.ResultingArchitecture(), "The architecture must survive JSON.");
            Assert.IsNull(reloaded.BuildConfig, "A code-built config can't be found by its GUID.");

            reloaded.RestoreRunState(RoundTrip(source.CaptureRunState()));

            Assert.IsNotNull(reloaded.BuildConfig, "The run state must bring back a config that can't be found by GUID.");
            Assert.AreEqual("Core code-built config", reloaded.BuildConfig.BuildName);
            CollectionAssert.Contains(reloaded.BuildConfig.ExtraScriptingDefines, "BUILD_UPLOADER_CORE_TEST");
            Assert.AreEqual(BuildTarget.StandaloneOSX, reloaded.ResultingTarget(), "The config being applied must be restored.");
            Assert.AreEqual("Core code-built config", reloaded.ResultingBuildName());
        }

        private static IEnumerator StartUntil(UploadTask task, UnitTestGateSource gate)
        {
            _ = task.StartAsync(false);
            yield return TestSupport.WaitUntil(() => gate.HasEntered, Timeout, "The task did not reach the gate");
        }

        /// <summary>Reads the task's checkpoint and stops the task, as a script reload would.</summary>
        private static Dictionary<string, object> Abandon(UploadTask task)
        {
            Dictionary<string, object> checkpoint = UploadTaskCheckpoints.Read(CheckpointPath(task));
            task.Abandon();
            return checkpoint;
        }

        private static UploadTask Resume(Dictionary<string, object> checkpoint)
        {
            return UploadTask.FromCheckpoint(RoundTrip(checkpoint), UploadTaskCheckpoints.FolderPath);
        }

        private static IEnumerator ReleaseAndFinish(UploadTask resumed)
        {
            Task run = resumed.ResumeAsync();
            UnitTestGateSource gate = Gate(resumed);
            yield return TestSupport.WaitUntil(() => gate.HasEntered || run.IsCompleted, Timeout, "The resumed task did not reach the gate");
            gate.Release();
            yield return TestSupport.Await(run, Timeout);
        }

        private static UnitTestGateSource Gate(UploadTask task)
        {
            return task.UploadConfigs.SelectMany(a => a.Sources).Select(a => a.Source).OfType<UnitTestGateSource>().First();
        }

        private static string CheckpointPath(UploadTask task)
        {
            return UploadTaskCheckpoints.FilePath(UploadTaskCheckpoints.FolderPath, task.GUID);
        }

        private static Dictionary<string, object> RoundTrip(Dictionary<string, object> data)
        {
            return JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(data));
        }

        /// <summary>Empty while the file is missing, including the moment it is being replaced.</summary>
        private static string ReadOrEmpty(string path)
        {
            try
            {
                return File.ReadAllText(path);
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

        private static UploadConfig Config(AUploadSource source, AUploadDestination destination)
        {
            var config = new UploadConfig();
            config.AddSource(source);
            config.AddDestination(destination);
            return config;
        }

        private static string Fixture(string name, string file, string contents)
        {
            string directory = TestSupport.OutputDirectory(name);
            File.WriteAllText(Path.Combine(directory, file), contents);
            return directory;
        }

        /// <summary>A destination folder that does not exist yet, so the task has to create it.</summary>
        private static string Output(string name)
        {
            return Path.Combine(TestSupport.OutputDirectory(name + "Output"), "upload");
        }

        private static void AssertFailedAt(UploadTask task, StepType step, string reason)
        {
            Assert.IsTrue(task.Report.GetFailReasons().Any(failure => failure.Key == step && failure.FailReason.Contains(reason)),
                "Expected a " + step + " failure containing '" + reason + "'.\n" + task.Report.GetReport());
        }
    }
}
