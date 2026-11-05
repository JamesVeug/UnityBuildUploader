using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    public class DiscordMessageTests
    {
        private bool m_previousOpenBuildFolders;

        [SetUp]
        public void SetUp()
        {
            m_previousOpenBuildFolders = Preferences.OpenBuildFoldersAfterUpload;
            Preferences.OpenBuildFoldersAfterUpload = false;
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.OpenBuildFoldersAfterUpload = m_previousOpenBuildFolders;
        }

        [UnityTest, Category("BuildUploader.DryRun")]
        public IEnumerator OneMessageDryRun()
        {
            const string message = "Build Uploader test: \"quoted\" text\nUnicode: ✓";
            string source = TestSupport.FixtureDirectory("DiscordSource");
            string output = TestSupport.OutputDirectory("DiscordOutput");
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            config.AddDestination(new LocalPathDestination(output));

            var action = new DiscordMessageChannelAction();
            action.SetChannel(123456789);
            action.SetText(message);
            action.AddEmbed("Test build", "{taskDescription}", UnityEngine.Color.blue);
            action.SetDryRun(true);

            var uploadTask = new UploadTask("Discord UploadTask dry run", config);
            uploadTask.SetBuildDescription("Discord description");
            uploadTask.AddAction(action, UploadConfig.UploadActionData.UploadCompleteStatus.Always,
                new System.Collections.Generic.List<UploadConfig.UploadActionData.UploadTrigger> { UploadConfig.UploadActionData.UploadTrigger.OnTaskFinished });
            yield return TestSupport.Run(uploadTask);

            string report = uploadTask.Report.GetReport();
            StringAssert.Contains("Discord dry run: POST https://discord.com/api/v10/channels/123456789/messages", report);
            StringAssert.Contains("Unicode:", report);
            StringAssert.Contains("Test build", report);
            StringAssert.Contains("Discord description", report);
            var logs = uploadTask.Report.StepResults.Values.SelectMany(processes => processes.Values)
                .SelectMany(results => results).SelectMany(result => result.Logs).ToList();
            Assert.AreEqual(1, logs.Count(log => log.Message.StartsWith("Discord dry run: POST ")));
            string payload = logs.Single(log => log.Message.TrimStart().StartsWith("{") && log.Message.Contains("\"content\"")).Message;
            var decoded = JSON.DeserializeObject<Dictionary<string, object>>(payload);
            Assert.AreEqual(message, decoded["content"]);
            Assert.IsTrue(Directory.Exists(output));
        }
    }
}
