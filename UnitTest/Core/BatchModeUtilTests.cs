using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// BatchModeUtil.Execute's arguments: -uploadProfile, profiles passed without it, -dryRun, -uploadDescription,
    /// -uploadReportFile and -async, and that nothing starts when a profile is missing.
    /// The exit code needs a batch mode editor, see UntestedFixes.md.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class BatchModeUtilTests
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

        [Test]
        public void ReadsEveryUploadArgument()
        {
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-batchmode", "-quit", "-projectPath", "C:/Project", "-executeMethod", "Wireframe.BatchModeUtil.Execute",
                "-uploadProfile", "abc123", "-UPLOADPROFILE", "Windows Debug", "-dryrun", "-uploadDescription", "Windows Debug main@1234",
                "-uploadReportFile", "Logs/Report.txt", "-async"
            });

            CollectionAssert.AreEqual(new[] { "abc123", "Windows Debug" }, arguments.Profiles, "Only -uploadProfile values are profiles once it is used.");
            Assert.IsTrue(arguments.ProfilesNamed);
            Assert.IsTrue(arguments.DryRun);
            Assert.IsTrue(arguments.Async);
            Assert.AreEqual("Windows Debug main@1234", arguments.Description);
            Assert.AreEqual("Logs/Report.txt", arguments.ReportFile);
        }

        [Test]
        public void ProfilesWithoutUploadProfileAreTheOtherArguments()
        {
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-batchmode", "-executeMethod", "Wireframe.BatchModeUtil.Execute", "41cf5e", "-uploadDescription", "not a profile"
            });

            CollectionAssert.AreEqual(new[] { "-batchmode", "-executeMethod", "Wireframe.BatchModeUtil.Execute", "41cf5e" }, arguments.Profiles,
                "Without -uploadProfile every argument is tried as a profile, except the executable and other options' values.");
            Assert.IsFalse(arguments.ProfilesNamed);
            Assert.IsFalse(arguments.DryRun);
            Assert.IsFalse(arguments.Async);
            Assert.AreEqual("not a profile", arguments.Description);
            Assert.IsNull(arguments.ReportFile);
        }

        [Test]
        public void TasksGetTheDescriptionAndDryRun()
        {
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-uploadProfile", "abc123", "-uploadProfile", "Second", "-dryRun", "-uploadDescription", "Nightly"
            });

            List<UploadTask> tasks = BatchModeUtil.CreateTasks(arguments, FindProfile, out string error);

            Assert.IsNull(error);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, tasks.Select(a => a.UploadName));
            Assert.IsTrue(tasks.All(a => a.DryRun));
            Assert.IsTrue(tasks.All(a => a.UploadDescription == "Nightly"));

            arguments = BatchModeUtil.Arguments.Parse(new[] { "Unity.exe", "-uploadProfile", "Second" });
            tasks = BatchModeUtil.CreateTasks(arguments, FindProfile, out error);

            Assert.IsNull(error);
            Assert.AreEqual(1, tasks.Count);
            Assert.IsFalse(tasks[0].DryRun);
            Assert.AreEqual("", tasks[0].UploadDescription);
        }

        [Test]
        public void UnknownArgumentsAreSkippedWithoutUploadProfile()
        {
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[] { "Unity.exe", "-batchmode", "abc123", "-quit" });

            List<UploadTask> tasks = BatchModeUtil.CreateTasks(arguments, FindProfile, out string error);

            Assert.IsNull(error);
            CollectionAssert.AreEqual(new[] { "First" }, tasks.Select(a => a.UploadName));

            arguments = BatchModeUtil.Arguments.Parse(new[] { "Unity.exe", "-batchmode", "-quit" });
            Assert.IsNull(BatchModeUtil.CreateTasks(arguments, FindProfile, out error));
            StringAssert.Contains("No tasks were started", error);
        }

        [Test]
        public void AMissingProfileStartsNothing()
        {
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-uploadProfile", "abc123", "-uploadProfile", "Missing"
            });

            Assert.IsNull(BatchModeUtil.CreateTasks(arguments, FindProfile, out string error), "A typo must not run the other profiles.");
            StringAssert.Contains("Missing", error);

            arguments = BatchModeUtil.Arguments.Parse(new[] { "Unity.exe", "-uploadProfile" });
            Assert.IsNull(BatchModeUtil.CreateTasks(arguments, FindProfile, out error));
            StringAssert.Contains("No tasks were started", error);
        }

        [Test]
        public void AMissingProfileFailsAndWritesWhyToTheReportFile()
        {
            string reportFile = Path.Combine(TestSupport.OutputDirectory("BatchModeReport"), "Logs", "Report.txt");
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-uploadProfile", "Missing", "-uploadReportFile", reportFile
            });
            LogAssert.Expect(LogType.Error, "No tasks were started. No profile found with GUID or Name : Missing");

            Assert.IsFalse(BatchModeUtil.Run(arguments, FindProfile));

            StringAssert.Contains("No profile found with GUID or Name : Missing", File.ReadAllText(reportFile));
        }

        [UnityTest]
        public IEnumerator ReportFileGetsEveryTasksReport()
        {
            string reportFile = Path.Combine(TestSupport.OutputDirectory("BatchModeReport"), "Report.txt");
            File.WriteAllText(reportFile, "Report from an earlier run");
            BatchModeUtil.Arguments arguments = BatchModeUtil.Arguments.Parse(new[]
            {
                "Unity.exe", "-uploadProfile", "abc123", "-uploadProfile", "Second", "-uploadReportFile", reportFile
            });
            Dictionary<string, UploadProfile> profiles = new Dictionary<string, UploadProfile>
            {
                { "abc123", LocalProfile("First") },
                { "Second", LocalProfile("Second") },
            };

            List<UploadTask> tasks = BatchModeUtil.CreateTasks(arguments, id => profiles[id], out string error);
            Assert.IsNull(error);
            BatchModeUtil.DeleteReport(arguments.ReportFile);
            Assert.IsFalse(File.Exists(reportFile), "A report from an earlier run must be removed.");
            foreach (UploadTask task in tasks)
            {
                BatchModeUtil.WriteReportWhenComplete(task, arguments.ReportFile);
                yield return TestSupport.Execute(task, Timeout);
                Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
            }

            string written = File.ReadAllText(reportFile);
            foreach (UploadTask task in tasks)
            {
                StringAssert.Contains("GUID: " + task.GUID, written);
                StringAssert.Contains("Name: " + task.UploadName, written);
            }

            StringAssert.Contains("Successful: True", written);
            StringAssert.DoesNotContain("Report from an earlier run", written);
        }

        private static UploadProfile FindProfile(string profileId)
        {
            if (profileId == "abc123")
            {
                return new UploadProfile { GUID = "abc123", ProfileName = "First" };
            }

            if (profileId == "Second")
            {
                return new UploadProfile { GUID = "def456", ProfileName = "Second" };
            }

            return null;
        }

        private static UploadProfile LocalProfile(string name)
        {
            string source = TestSupport.OutputDirectory(name);
            File.WriteAllText(Path.Combine(source, "root.txt"), name);
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            config.AddDestination(new LocalPathDestination(Path.Combine(TestSupport.OutputDirectory(name + "Output"), "upload")));
            var profile = new UploadProfile { GUID = name, ProfileName = name };
            profile.UploadConfigs.Add(config);
            return profile;
        }
    }
}
