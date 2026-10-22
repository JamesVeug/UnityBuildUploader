using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using StepType = Wireframe.AUploadTask_Step.StepType;
using Status = Wireframe.UploadConfig.UploadActionData.UploadCompleteStatus;
using Trigger = Wireframe.UploadConfig.UploadActionData.UploadTrigger;
using WhenToExclude = Wireframe.AExcludePathsByRegex_UploadModifier.WhenToExclude;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Credential-free UploadTask coverage. Needs no SDK, network or player build, so it runs on every
    /// editor version and host OS in the matrix.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadTaskTests
    {
        private const double Timeout = 120;

        private string m_previousCacheFolder;
        private bool m_previousUseLocalDestination;
        private bool m_previousDeleteCache;
        private bool m_previousSaveReport;
        private bool m_previousDecompress;

        [SetUp]
        public void SetUp()
        {
            m_previousCacheFolder = Preferences.CacheFolderPath;
            m_previousUseLocalDestination = Preferences.UseLocalDestinationIfAvailable;
            m_previousDeleteCache = Preferences.DeleteCacheAfterUpload;
            m_previousSaveReport = Preferences.AutoSaveReportToCacheFolder;
            m_previousDecompress = Preferences.AutoDecompressZippedSourceFiles;

            // Isolate the task cache so cleanup can be asserted, and keep the source -> cache -> destination
            // copy explicit unless a test opts into the local-destination shortcut.
            Preferences.CacheFolderPath = TestSupport.OutputDirectory("Cache");
            Preferences.UseLocalDestinationIfAvailable = false;
            Preferences.DeleteCacheAfterUpload = true;
            Preferences.AutoSaveReportToCacheFolder = false;
            Preferences.AutoDecompressZippedSourceFiles = true;
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.CacheFolderPath = m_previousCacheFolder;
            Preferences.UseLocalDestinationIfAvailable = m_previousUseLocalDestination;
            Preferences.DeleteCacheAfterUpload = m_previousDeleteCache;
            Preferences.AutoSaveReportToCacheFolder = m_previousSaveReport;
            Preferences.AutoDecompressZippedSourceFiles = m_previousDecompress;
        }

        [UnityTest]
        public IEnumerator FolderSourceCopiesTreeToLocalPath()
        {
            string source = Fixture("Folder", "root.txt", "root", "nested/child.txt", "child");
            string output = Output("Folder");
            var task = new UploadTask("Core folder copy", Config(new FolderSource(source), new LocalPathDestination(output)));
            UploadTaskReport completed = null;
            task.OnComplete += report => completed = report;

            yield return Run(task);

            Assert.AreSame(task.Report, completed, "OnComplete must receive the task report.");
            Assert.IsTrue(task.HasStarted);
            Assert.IsFalse(task.IsCancelled);
            Assert.AreEqual(1f, task.PercentComplete);
            CollectionAssert.IsEmpty(task.Report.GetFailReasons());
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")));
            Assert.AreEqual("child", File.ReadAllText(Path.Combine(output, "nested", "child.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(source, "nested", "child.txt")), "The source must not be modified.");
            Assert.IsFalse(Directory.Exists(task.CachedLocations[0]), "The task cache must be deleted after the upload.");
        }

        [UnityTest]
        public IEnumerator FileSourceCopiesOnlyThatFile()
        {
            string directory = Fixture("File", "notes.txt", "notes", "other.txt", "other");
            string output = Output("File");
            var task = new UploadTask("Core file copy",
                Config(new FileSource(Path.Combine(directory, "notes.txt")), new LocalPathDestination(output)));

            yield return Run(task);

            Assert.AreEqual("notes", File.ReadAllText(Path.Combine(output, "notes.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(output, "other.txt")));
        }

        [UnityTest]
        public IEnumerator LocalPathDestinationIsUsedAsCacheWhenPreferred()
        {
            Preferences.UseLocalDestinationIfAvailable = true;
            string source = Fixture("LocalCache", "root.txt", "root");
            string output = Output("LocalCache");
            var task = new UploadTask("Core local cache", Config(new FolderSource(source), new LocalPathDestination(output)));

            yield return Run(task);

            Assert.IsTrue(Utils.ComparePaths(output, task.CachedLocations[0]), "Expected the destination to be the cache: " + task.CachedLocations[0]);
            Assert.IsFalse(task.CachedLocationNeedsCleaning[0], "A destination used as the cache must not be cleaned up.");
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")));
        }

        [UnityTest]
        public IEnumerator MissingSourceFailsValidationAndUploadsNothing()
        {
            string missing = Path.Combine(TestSupport.OutputDirectory("Missing"), "does-not-exist");
            string output = Output("Missing");
            var task = new UploadTask("Core missing source", Config(new FolderSource(missing), new LocalPathDestination(output)));
            var finished = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(finished, Status.Always, Trigger.OnTaskFinished));

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            Assert.IsFalse(task.Report.Successful);
            AssertFailedAt(task, StepType.Validation, "Path does not exist");
            Assert.IsFalse(task.Report.StepResults.ContainsKey(StepType.Upload), "Upload must be skipped after a validation failure.");
            Assert.IsTrue(task.Report.StepResults.ContainsKey(StepType.Cleanup), "Cleanup must always run.");
            Assert.IsFalse(Directory.Exists(output), "Nothing may be uploaded after a validation failure.");
            Assert.IsFalse(Directory.Exists(task.CachedLocations[0]), "The task cache must be deleted after a failure.");
            Assert.AreEqual(1, finished.ExecuteCount, "OnTaskFinished actions must still run after a failure.");
        }

        [UnityTest]
        public IEnumerator ExcludeFilesSkipsMatchesWhileCaching()
        {
            yield return ExcludeFiles(WhenToExclude.DoNotCopyFromSource);
        }

        [UnityTest]
        public IEnumerator ExcludeFilesDeletesMatchesFromCache()
        {
            yield return ExcludeFiles(WhenToExclude.DeleteFromCache);
        }

        private IEnumerator ExcludeFiles(WhenToExclude when)
        {
            string source = Fixture("Exclude" + when, "game.txt", "game", "debug.pdb", "symbols");
            string output = Output("Exclude" + when);
            UploadConfig config = Config(new FolderSource(source), new LocalPathDestination(output));
            config.AddModifier(new ExcludeFilesModifier(when, @"\.pdb$"));

            yield return Run(new UploadTask("Core exclude " + when, config));

            Assert.IsTrue(File.Exists(Path.Combine(output, "game.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(output, "debug.pdb")), "Excluded files must not be uploaded.");
            Assert.IsTrue(File.Exists(Path.Combine(source, "debug.pdb")), "The source must not be modified.");
        }

        [UnityTest]
        public IEnumerator CompressedUploadDecompressesAsSource()
        {
            string source = Fixture("Zip", "root.txt", "root", "nested/child.txt", "child");
            string zipped = Output("Zip");
            // ZipUtils stages self-contained zips under the shared temp folder by file name, so keep it unique.
            string archiveName = "bundle-" + System.Guid.NewGuid().ToString("N");
            UploadConfig compress = Config(new FolderSource(source), new LocalPathDestination(zipped));
            compress.AddModifier(new CompressModifier(archiveName, ""));

            yield return Run(new UploadTask("Core compress", compress));

            string archive = Path.Combine(zipped, archiveName + ".zip");
            Assert.IsTrue(File.Exists(archive), "CompressModifier did not produce " + archive);
            CollectionAssert.AreEquivalent(new[] { archive }, Directory.GetFileSystemEntries(zipped),
                "Compressed content should replace the original files.");

            // A .zip source is decompressed into the cache, completing the round trip through ZipUtils.
            string unzipped = Output("Unzip");
            yield return Run(new UploadTask("Core decompress", Config(new FileSource(archive), new LocalPathDestination(unzipped))));

            Assert.AreEqual("root", File.ReadAllText(Path.Combine(unzipped, "root.txt")));
            Assert.AreEqual("child", File.ReadAllText(Path.Combine(unzipped, "nested", "child.txt")));
        }

        [UnityTest]
        public IEnumerator TaskActionsFollowTriggerAndStatusOnSuccess()
        {
            string source = Fixture("ActionsOk", "root.txt", "root");
            var task = new UploadTask("Core actions success", Config(new FolderSource(source), new NoUploadDestination()));
            ActionSet actions = ActionSet.AddTo(task);

            yield return Run(task);

            Assert.AreEqual(1, actions.Started.ExecuteCount, "OnTaskStarted");
            Assert.AreEqual(1, actions.IfSuccessful.ExecuteCount, "IfSuccessful");
            Assert.AreEqual(0, actions.IfFailed.ExecuteCount, "IfFailed");
            Assert.AreEqual(0, actions.Never.ExecuteCount, "Never");
            Assert.Greater(actions.EachStep.ExecuteCount, 1, "AfterEachStepCompletes");
            Assert.AreEqual(1, actions.IfFailed.CleanUpCount, "Cleanup runs for every triggered action, whatever the status.");
            Assert.AreEqual(0, actions.Never.CleanUpCount, "Never actions are not cleaned up.");
        }

        [UnityTest]
        public IEnumerator TaskActionsFollowTriggerAndStatusOnFailure()
        {
            string missing = Path.Combine(TestSupport.OutputDirectory("ActionsFail"), "does-not-exist");
            var task = new UploadTask("Core actions failure", Config(new FolderSource(missing), new NoUploadDestination()));
            ActionSet actions = ActionSet.AddTo(task);

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            Assert.AreEqual(1, actions.Started.ExecuteCount, "OnTaskStarted");
            Assert.AreEqual(0, actions.IfSuccessful.ExecuteCount, "IfSuccessful");
            Assert.AreEqual(1, actions.IfFailed.ExecuteCount, "IfFailed");
            Assert.AreEqual(0, actions.Never.ExecuteCount, "Never");
        }

        [UnityTest]
        public IEnumerator StartActionReturningFalseStopsTaskWhenSet()
        {
            yield return FailedStartActionSetToStopTaskSkipsBuildAndUpload(UnitTestFailingAction.FailureMode.ExecuteReturnsFalse);
        }

        [UnityTest]
        public IEnumerator StartActionThrowingStopsTaskWhenSet()
        {
            yield return FailedStartActionSetToStopTaskSkipsBuildAndUpload(UnitTestFailingAction.FailureMode.ExecuteThrows);
        }

        [UnityTest]
        public IEnumerator StartActionFailingToPrepareStopsTaskWhenSet()
        {
            yield return FailedStartActionSetToStopTaskSkipsBuildAndUpload(UnitTestFailingAction.FailureMode.PrepareReturnsFalse);
        }

        private IEnumerator FailedStartActionSetToStopTaskSkipsBuildAndUpload(UnitTestFailingAction.FailureMode mode)
        {
            string source = Fixture("StopTask" + mode, "root.txt", "root");
            string output = Output("StopTask" + mode);
            var destination = new UnitTestGateDestination();
            UploadConfig config = Config(new FolderSource(source), new LocalPathDestination(output));
            config.AddDestination(destination);
            var task = new UploadTask("Core stop task " + mode, config);
            var failing = new UnitTestFailingAction(mode);
            task.AddAction(new UploadConfig.UploadActionData(failing, Status.Always, Trigger.OnTaskStarted) { StopTaskIfFailed = true });
            var ifFailed = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(ifFailed, Status.IfFailed, Trigger.OnTaskFinished));
            destination.Release(true); // Never reached, but must not block the test if it were

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            AssertFailedAt(task, StepType.PreUploadActions, "UnitTestFailingAction");
            Assert.IsFalse(task.Report.StepResults.ContainsKey(StepType.GetSources), "Sources must not be fetched or built after a failed start action.\n" + task.Report.GetReport());
            Assert.IsFalse(task.Report.StepResults.ContainsKey(StepType.Upload), "Nothing may be uploaded after a failed start action.");
            Assert.IsFalse(destination.HasEntered);
            Assert.IsFalse(Directory.Exists(output), "Nothing may be written to the destination.");
            Assert.IsTrue(task.Report.StepResults.ContainsKey(StepType.Cleanup), "Cleanup must always run.");
            Assert.AreEqual(1, destination.CleanUpCount, "Destinations are cleaned up after the task stops.");
            Assert.AreEqual(1, failing.CleanUpCount, "The failed action is cleaned up.");
            Assert.AreEqual(1, ifFailed.ExecuteCount, "IfFailed actions run when an action stops the task.");
        }

        [UnityTest]
        public IEnumerator StartActionReturningFalseDoesNotStopTaskByDefault()
        {
            yield return FailedStartActionNotSetToStopTaskStillUploads(UnitTestFailingAction.FailureMode.ExecuteReturnsFalse);
        }

        [UnityTest]
        public IEnumerator StartActionThrowingDoesNotStopTaskByDefault()
        {
            yield return FailedStartActionNotSetToStopTaskStillUploads(UnitTestFailingAction.FailureMode.ExecuteThrows);
        }

        private IEnumerator FailedStartActionNotSetToStopTaskStillUploads(UnitTestFailingAction.FailureMode mode)
        {
            string source = Fixture("KeepGoing" + mode, "root.txt", "root");
            string output = Output("KeepGoing" + mode);
            var task = new UploadTask("Core keep going " + mode, Config(new FolderSource(source), new LocalPathDestination(output)));
            var failing = new UnitTestFailingAction(mode);
            var actionData = new UploadConfig.UploadActionData(failing, Status.Always, Trigger.OnTaskStarted);
            Assert.IsFalse(actionData.StopTaskIfFailed, "Actions do not stop the task unless they ask to.");
            task.AddAction(actionData);

            yield return Run(task);

            Assert.AreEqual(1, failing.ExecuteCount);
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")), "Without the flag a failed action changes nothing.");
        }

        [UnityTest]
        public IEnumerator InvokedMethodReturningFalseBlocksTheBuild()
        {
            UnitTestStaticMethods.Calls = 0;
            string source = Fixture("InvokeFalse", "root.txt", "root");
            string output = Output("InvokeFalse");
            var task = new UploadTask("Core invoke false", Config(new FolderSource(source), new LocalPathDestination(output)));
            // Added the way the window adds it, so Stop task if this fails comes from the action's default
            task.AddAction(new InvokeStaticMethodAction(UnitTestStaticMethods.Path + "ReturnsFalse"), Status.Always,
                new System.Collections.Generic.List<Trigger> { Trigger.OnTaskStarted });

            yield return Execute(task);

            Assert.AreEqual(1, UnitTestStaticMethods.Calls);
            Assert.IsFalse(task.IsSuccessful);
            AssertFailedAt(task, StepType.PreUploadActions, "ReturnsFalse returned false");
            Assert.IsFalse(task.Report.StepResults.ContainsKey(StepType.GetSources), "Sources must not be fetched or built.\n" + task.Report.GetReport());
            Assert.IsFalse(Directory.Exists(output), "Nothing may be uploaded.");
        }

        [UnityTest]
        public IEnumerator InvokedMethodRunsBeforeTheUpload()
        {
            UnitTestStaticMethods.Calls = 0;
            string source = Fixture("InvokeTrue", "root.txt", "root");
            string output = Output("InvokeTrue");
            var task = new UploadTask("Core invoke true", Config(new FolderSource(source), new LocalPathDestination(output)));
            task.AddAction(new InvokeStaticMethodAction(UnitTestStaticMethods.Path + "ReturnsTrue"), Status.Always,
                new System.Collections.Generic.List<Trigger> { Trigger.OnTaskStarted });

            yield return Run(task);

            Assert.AreEqual(1, UnitTestStaticMethods.Calls);
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")));
        }

        [UnityTest]
        public IEnumerator FailedEachStepActionSetToStopTaskSkipsRemainingSteps()
        {
            string source = Fixture("StopEachStep", "root.txt", "root");
            string output = Output("StopEachStep");
            var task = new UploadTask("Core stop after step", Config(new FolderSource(source), new LocalPathDestination(output)));
            var failing = new UnitTestFailingAction(UnitTestFailingAction.FailureMode.ExecuteReturnsFalse);
            task.AddAction(new UploadConfig.UploadActionData(failing, Status.Always, Trigger.AfterEachStepCompletes) { StopTaskIfFailed = true });

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            Assert.IsFalse(task.Report.StepResults.ContainsKey(StepType.Upload), "The steps after the first failed action must be skipped.\n" + task.Report.GetReport());
            Assert.IsFalse(Directory.Exists(output), "Nothing may be uploaded.");
            Assert.IsTrue(task.Report.StepResults.ContainsKey(StepType.Cleanup), "Cleanup must always run.");
        }

        [UnityTest]
        public IEnumerator FailedConfigActionSetToStopTaskFailsTask()
        {
            string source = Fixture("StopConfigAction", "root.txt", "root");
            UploadConfig config = Config(new FolderSource(source), new NoUploadDestination());
            config.AddPostAction(new UploadConfig.UploadActionData(new UnitTestFailingAction(), Status.Always) { StopTaskIfFailed = true });
            var task = new UploadTask("Core stop config action", config);
            var ifSuccessful = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(ifSuccessful, Status.IfSuccessful, Trigger.OnTaskFinished));

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            AssertFailedAt(task, StepType.Upload, "UnitTestFailingAction");
            Assert.AreEqual(0, ifSuccessful.ExecuteCount, "IfSuccessful actions must not run after a failed config action.");
        }

        [UnityTest]
        public IEnumerator ConfigPostActionsRunWithUploadResult()
        {
            string source = Fixture("ConfigActions", "root.txt", "root");
            UploadConfig config = Config(new FolderSource(source), new NoUploadDestination());
            var ifSuccessful = new UnitTestRecordingAction();
            var ifFailed = new UnitTestRecordingAction();
            config.AddPostAction(ifSuccessful, Status.IfSuccessful);
            config.AddPostAction(ifFailed, Status.IfFailed);

            yield return Run(new UploadTask("Core config actions", config));

            Assert.AreEqual(1, ifSuccessful.ExecuteCount);
            Assert.AreEqual(0, ifFailed.ExecuteCount);
            Assert.AreEqual(1, ifFailed.CleanUpCount, "Config actions are cleaned up whatever the status.");
        }

        [UnityTest]
        public IEnumerator FailedDestinationFailsTaskWithoutStoppingSiblings()
        {
            string source = Fixture("DestinationFails", "root.txt", "root");
            string output = Output("DestinationFails");
            var failing = new UnitTestFailingDestination();
            UploadConfig config = Config(new FolderSource(source), new LocalPathDestination(output));
            config.AddDestination(failing);
            var configIfFailed = new UnitTestRecordingAction();
            config.AddPostAction(configIfFailed, Status.IfFailed);
            var task = new UploadTask("Core failing destination", config);
            var taskIfFailed = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(taskIfFailed, Status.IfFailed, Trigger.OnTaskFinished));

            yield return Execute(task);

            Assert.IsFalse(task.IsSuccessful);
            AssertFailedAt(task, StepType.Upload, UnitTestFailingDestination.FailReason);
            Assert.AreEqual(1, failing.UploadCount);
            Assert.AreEqual(1, failing.CleanUpCount, "Destinations are cleaned up after a failure.");
            Assert.AreEqual("root", File.ReadAllText(Path.Combine(output, "root.txt")),
                "A failed upload must not cancel the other destinations.");
            Assert.AreEqual(1, configIfFailed.ExecuteCount, "Config IfFailed action");
            Assert.AreEqual(1, taskIfFailed.ExecuteCount, "Task IfFailed action");
            Assert.IsFalse(Directory.Exists(task.CachedLocations[0]), "The task cache must be deleted after a failure.");
        }

        [UnityTest]
        public IEnumerator FailedDestinationWithoutReasonStillFailsTask()
        {
            string source = Fixture("SilentFailure", "root.txt", "root");
            var task = new UploadTask("Core silent failure",
                Config(new FolderSource(source), new UnitTestFailingDestination(setFailReason: false)));
            var taskIfFailed = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(taskIfFailed, Status.IfFailed, Trigger.OnTaskFinished));

            yield return Execute(task);

            Assert.AreEqual(1, taskIfFailed.ExecuteCount, "The task treated the failed upload as a failure when running actions.");
            Assert.IsFalse(task.IsSuccessful, "A destination returning false must fail the task even without SetFailed.\n" + task.Report.GetReport());
            Assert.IsFalse(task.Report.Successful, "The saved report must agree with the task result.");
            Assert.IsTrue(task.Report.GetFailReasons().Any(failure => failure.Key == StepType.Upload), task.Report.GetReport());
        }

        [UnityTest]
        public IEnumerator CancelDuringUploadStopsTask()
        {
            string source = Fixture("Cancel", "root.txt", "root");
            var gate = new UnitTestGateDestination();
            var task = new UploadTask("Core cancel", Config(new FolderSource(source), gate));
            var taskIfFailed = new UnitTestRecordingAction();
            task.AddAction(new UploadConfig.UploadActionData(taskIfFailed, Status.IfFailed, Trigger.OnTaskFinished));

            Task operation = task.StartAsync(false);
            try
            {
                yield return TestSupport.WaitUntil(() => gate.HasEntered || operation.IsCompleted, Timeout,
                    "Upload did not reach the gate destination");
                Assert.IsFalse(operation.IsCompleted, "Task ended before the upload: " + task.Report.GetReport());
                task.Cancel();
            }
            finally
            {
                // Uploads already in flight finish before the task stops.
                gate.Release(true);
            }
            yield return TestSupport.Await(operation, Timeout);

            Assert.IsTrue(task.IsComplete);
            Assert.IsTrue(task.IsCancelled);
            Assert.IsTrue(task.Report.Cancelled);
            Assert.IsFalse(task.IsSuccessful);
            Assert.AreEqual(1, gate.CleanUpCount, "Cleanup must run after cancellation.");
            Assert.AreEqual(1, taskIfFailed.ExecuteCount, "A cancelled task runs IfFailed actions.");

            task.Cancel(); // Cancelling a completed task is a no-op.
            Assert.IsTrue(task.IsComplete);
        }

        [UnityTest]
        public IEnumerator DisabledConfigsAndDestinationsAreSkipped()
        {
            string source = Fixture("Disabled", "root.txt", "root");
            string enabledOutput = Output("DisabledEnabled");
            string disabledDestinationOutput = Output("DisabledDestination");
            string disabledConfigOutput = Output("DisabledConfig");
            UploadConfig enabled = Config(new FolderSource(source), new LocalPathDestination(enabledOutput));
            enabled.AddDestination(new UploadConfig.DestinationData(new LocalPathDestination(disabledDestinationOutput), false));
            UploadConfig disabled = Config(new FolderSource(source), new LocalPathDestination(disabledConfigOutput));
            disabled.Enabled = false;
            var task = new UploadTask("Core disabled", new System.Collections.Generic.List<UploadConfig> { enabled, disabled });

            yield return Run(task);

            Assert.IsTrue(File.Exists(Path.Combine(enabledOutput, "root.txt")));
            Assert.IsFalse(Directory.Exists(disabledDestinationOutput), "A disabled destination must not receive files.");
            Assert.IsFalse(Directory.Exists(disabledConfigOutput), "A disabled config must not run.");
        }

        [UnityTest]
        public IEnumerator DuplicateSourceFilesFollowDuplicateHandling()
        {
            string first = Fixture("DuplicateFirst", "same.txt", "first");
            string second = Fixture("DuplicateSecond", "same.txt", "second");

            string errorOutput = Output("DuplicateError");
            UploadConfig error = Config(new FolderSource(first), new LocalPathDestination(errorOutput));
            error.AddSource(new FolderSource(second)); // Defaults to FileExistHandling.Error
            var errorTask = new UploadTask("Core duplicate error", error);
            yield return Execute(errorTask);

            Assert.IsFalse(errorTask.IsSuccessful);
            AssertFailedAt(errorTask, StepType.CacheSources, "File already exists");
            Assert.IsFalse(Directory.Exists(errorOutput), "Nothing may be uploaded after a caching failure.");

            string overwriteOutput = Output("DuplicateOverwrite");
            UploadConfig overwrite = Config(new FolderSource(first), new LocalPathDestination(overwriteOutput));
            overwrite.AddSource(new FolderSource(second));
            overwrite.Sources[1].DuplicateFileHandling = Utils.FileExistHandling.Overwrite;
            yield return Run(new UploadTask("Core duplicate overwrite", overwrite));

            Assert.AreEqual("second", File.ReadAllText(Path.Combine(overwriteOutput, "same.txt")));
        }

        [UnityTest]
        public IEnumerator TaskNameFormatsIntoPathsAndSubFolders()
        {
            string source = Fixture("Format", "root.txt", "root");
            string root = TestSupport.OutputDirectory("Format");
            UploadConfig config = Config(new FolderSource(source),
                new LocalPathDestination(Path.Combine(root, Context.TASK_PROFILE_NAME_KEY)));
            config.Sources[0].SubFolder = Context.TASK_PROFILE_NAME_KEY + "-sub";

            yield return Run(new UploadTask("CoreFormat", config));

            Assert.AreEqual("root", File.ReadAllText(Path.Combine(root, "CoreFormat", "CoreFormat-sub", "root.txt")));
        }

        private static IEnumerator Run(UploadTask task)
        {
            yield return Execute(task);
            Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
        }

        private static IEnumerator Execute(UploadTask task)
        {
            task.SetBuildDescription("Build Uploader core UploadTask test");
            yield return TestSupport.Execute(task, Timeout);
        }

        private static UploadConfig Config(AUploadSource source, AUploadDestination destination)
        {
            var config = new UploadConfig();
            config.AddSource(source);
            config.AddDestination(destination);
            return config;
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

        private class ActionSet
        {
            public UnitTestRecordingAction Started, IfSuccessful, IfFailed, Never, EachStep;

            public static ActionSet AddTo(UploadTask task)
            {
                var set = new ActionSet
                {
                    Started = new UnitTestRecordingAction(),
                    IfSuccessful = new UnitTestRecordingAction(),
                    IfFailed = new UnitTestRecordingAction(),
                    Never = new UnitTestRecordingAction(),
                    EachStep = new UnitTestRecordingAction(),
                };
                task.AddAction(new UploadConfig.UploadActionData(set.Started, Status.Always, Trigger.OnTaskStarted));
                task.AddAction(new UploadConfig.UploadActionData(set.IfSuccessful, Status.IfSuccessful, Trigger.OnTaskFinished));
                task.AddAction(new UploadConfig.UploadActionData(set.IfFailed, Status.IfFailed, Trigger.OnTaskFinished));
                task.AddAction(new UploadConfig.UploadActionData(set.Never, Status.Never, Trigger.OnTaskFinished));
                task.AddAction(new UploadConfig.UploadActionData(set.EachStep, Status.Always, Trigger.AfterEachStepCompletes));
                return set;
            }
        }
    }
}
