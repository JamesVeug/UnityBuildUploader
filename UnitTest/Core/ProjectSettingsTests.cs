using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// The Skip Number Updates project setting keeps BuildUploader/ProjectSettings.json unchanged when uploads
    /// and builds start (#109).
    /// </summary>
    [Category("BuildUploader.Core")]
    public class ProjectSettingsTests
    {
        private static string SettingsPath => Path.GetFullPath(Application.dataPath + "/../BuildUploader/ProjectSettings.json");

        private bool m_previousSkip;
        private int m_previousBuildNumber;
        private int m_previousUploadNumber;

        [SetUp]
        public void SetUp()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            m_previousSkip = settings.SkipBuildAndUploadNumberUpdates;
            m_previousBuildNumber = settings.LastBuildNumber;
            m_previousUploadNumber = settings.TotalUploadTasksStarted;
        }

        [TearDown]
        public void TearDown()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            settings.SkipBuildAndUploadNumberUpdates = m_previousSkip;
            settings.LastBuildNumber = m_previousBuildNumber;
            settings.TotalUploadTasksStarted = m_previousUploadNumber;
            BuildUploaderProjectSettings.Save();
        }

        [Test]
        public void NumbersUpdateByDefault()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            settings.SkipBuildAndUploadNumberUpdates = false;
            int build = settings.LastBuildNumber;
            int upload = settings.TotalUploadTasksStarted;

            BuildUploaderProjectSettings.BumpUploadNumber();
            BuildUploaderProjectSettings.BumpBuildNumber();

            Assert.AreEqual(build + 1, settings.LastBuildNumber);
            Assert.AreEqual(upload + 1, settings.TotalUploadTasksStarted);
            StringAssert.Contains("\"TotalUploadTasksStarted\":" + (upload + 1), File.ReadAllText(SettingsPath).Replace(" ", ""));
        }

        [Test]
        public void SkippedNumbersLeaveTheSettingsFileUntouched()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            settings.SkipBuildAndUploadNumberUpdates = true;
            BuildUploaderProjectSettings.Save();
            string before = File.ReadAllText(SettingsPath);
            DateTime written = File.GetLastWriteTimeUtc(SettingsPath);
            int build = settings.LastBuildNumber;
            int upload = settings.TotalUploadTasksStarted;

            BuildUploaderProjectSettings.BumpUploadNumber();
            BuildUploaderProjectSettings.BumpBuildNumber();

            Assert.AreEqual(build, settings.LastBuildNumber);
            Assert.AreEqual(upload, settings.TotalUploadTasksStarted);
            Assert.AreEqual(before, File.ReadAllText(SettingsPath));
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(SettingsPath), "ProjectSettings.json must not be rewritten.");
        }

        [UnityTest]
        public IEnumerator SkippedNumbersStayUnchangedAcrossAnUpload()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            settings.SkipBuildAndUploadNumberUpdates = true;
            BuildUploaderProjectSettings.Save();
            string before = File.ReadAllText(SettingsPath);
            int upload = settings.TotalUploadTasksStarted;
            var config = new UploadConfig();
            config.AddSource(new FolderSource(TestSupport.FixtureDirectory("SkipNumbers")));
            config.AddDestination(new NoUploadDestination());

            yield return TestSupport.Run(new UploadTask("Core skip numbers", config));

            Assert.AreEqual(upload, settings.TotalUploadTasksStarted);
            Assert.AreEqual(before, File.ReadAllText(SettingsPath));
        }

        [Test]
        public void SettingDefaultsOffForOlderFiles()
        {
            var old = JSON.DeserializeObject<BuildUploaderProjectSettings>("{\"Version\":1,\"LastBuildNumber\":5,\"TotalUploadTasksStarted\":9}");
            Assert.IsFalse(old.SkipBuildAndUploadNumberUpdates);
            Assert.AreEqual(5, old.LastBuildNumber);

            var saved = JSON.DeserializeObject<BuildUploaderProjectSettings>(
                JSON.SerializeObject(new BuildUploaderProjectSettings { SkipBuildAndUploadNumberUpdates = true }));
            Assert.IsTrue(saved.SkipBuildAndUploadNumberUpdates);
        }
    }
}
