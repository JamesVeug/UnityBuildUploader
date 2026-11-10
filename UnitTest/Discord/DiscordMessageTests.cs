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

        [UnityTest, Category("BuildUploader.DryRun")]
        public IEnumerator WebhookMessageDryRunMasksToken()
        {
            const string token = "secretWebhookToken_abc123";
            const string message = "Webhook test: {taskDescription}";
            string source = TestSupport.FixtureDirectory("DiscordSource");
            string output = TestSupport.OutputDirectory("DiscordWebhookOutput");
            var config = new UploadConfig();
            config.AddSource(new FolderSource(source));
            config.AddDestination(new LocalPathDestination(output));

            var action = new DiscordWebhookMessageAction();
            action.SetWebhookURL("https://discord.com/api/webhooks/123456789/" + token);
            action.SetText(message);
            action.SetUsername("Build Bot");
            action.AddEmbed("Test build", "{taskDescription}", UnityEngine.Color.green);
            action.SetDryRun(true);

            var uploadTask = new UploadTask("Discord webhook UploadTask dry run", config);
            uploadTask.SetBuildDescription("Webhook description");
            uploadTask.AddAction(action, UploadConfig.UploadActionData.UploadCompleteStatus.Always,
                new List<UploadConfig.UploadActionData.UploadTrigger> { UploadConfig.UploadActionData.UploadTrigger.OnTaskFinished });
            yield return TestSupport.Run(uploadTask);

            string report = uploadTask.Report.GetReport();
            StringAssert.Contains("Discord webhook dry run: POST https://discord.com/api/webhooks/xxx/xxx", report);
            StringAssert.DoesNotContain(token, report);
            var logs = uploadTask.Report.StepResults.Values.SelectMany(processes => processes.Values)
                .SelectMany(results => results).SelectMany(result => result.Logs).ToList();
            string payload = logs.Single(log => log.Message.TrimStart().StartsWith("{") && log.Message.Contains("\"content\"")).Message;
            var decoded = JSON.DeserializeObject<Dictionary<string, object>>(payload);
            Assert.AreEqual("Webhook test: Webhook description", decoded["content"]);
            Assert.AreEqual("Build Bot", decoded["username"]);
            Assert.IsFalse(decoded.ContainsKey("avatar_url"));
            Assert.IsTrue(decoded.ContainsKey("embeds"));
        }

        [TestCase("https://discord.com/api/webhooks/123/abcToken", true)]
        [TestCase("https://discord.com/api/webhooks/123/abcToken?thread_id=5", true)]
        [TestCase("https://ptb.discord.com/api/v10/webhooks/123/abcToken", true)]
        [TestCase("http://discord.com/api/webhooks/123/abcToken", false)]
        [TestCase("https://example.com/hook?key=abcToken", false)]
        [TestCase("https://discord.com/api/webhooks/123", false)]
        [TestCase("https://discord.com/api/webhooks/123/", false)]
        public void IsWebhookURLChecksIdAndToken(string url, bool expected)
        {
            Assert.AreEqual(expected, Discord.IsWebhookURL(url));
        }

        [TestCase("not a url", true)]
        [TestCase("https://example.com/avatar.png", false)]
        [TestCase("https://example.com/avatar", true)]
        [TestCase("", false)]
        public void WebhookActionErrorsOnInvalidAvatarURL(string avatarURL, bool expectError)
        {
            var action = new DiscordWebhookMessageAction();
            action.SetWebhookURL("https://discord.com/api/webhooks/123/abcToken");
            action.SetText("Hello");
            action.SetAvatarURL(avatarURL);
            action.SetDryRun(true);

            var errors = new List<UnityEngine.GUIContent>();
            action.TryGetErrors(errors);

            Assert.AreEqual(expectError, errors.Any(error => error.text.StartsWith("Avatar URL")));
        }

        [Test]
        public void WebhookActionRoundTripsSerializedData()
        {
            var action = new DiscordWebhookMessageAction();
            action.SetText("Hello {version}");
            action.SetUsername("Bot");
            action.SetAvatarURL("https://example.com/a.png");
            action.AddEmbed("Title", "Description", UnityEngine.Color.red);

            string json = JSON.SerializeObject(action.Serialize());
            var restored = new DiscordWebhookMessageAction();
            restored.Deserialize(JSON.DeserializeObject<Dictionary<string, object>>(json));

            Assert.AreEqual(json, JSON.SerializeObject(restored.Serialize()));
        }
    }
}
