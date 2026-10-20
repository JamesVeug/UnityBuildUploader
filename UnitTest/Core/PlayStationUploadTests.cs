// BuildUploader-Hosts: OSXUniversal Linux64
// prospero-pub-cmd.exe cannot be faked with a script, and the PlayStation SDK is under NDA, so a fake tool stands in for it.
#if !UNITY_EDITOR_WIN
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// PlayStationUploadDestination uploads one dummy file through a fake publishing tool. Nothing leaves the machine:
    /// the fake records its arguments, the command file and the files it was asked to upload.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class PlayStationUploadTests
    {
        private const string TitleId = "PPSA00000";
        private const string ContentId = "UP0000-PPSA00000_00-BUILDUPLOADERTST";
        private const string Password = "build-uploader-fake-password";

        private string m_previousSdkPath;
        private string m_previousUserName;
        private string m_previousPassword;
        private bool m_previousEnabled;
        private bool m_previousDeleteAuthoringFiles;

        [SetUp]
        public void SetUp()
        {
            m_previousSdkPath = PlayStationSDK.PlayStationSDKPath;
            m_previousUserName = PlayStationSDK.UserName;
            m_previousPassword = PlayStationSDK.Password;
            m_previousEnabled = PlayStationSDK.Enabled;
            m_previousDeleteAuthoringFiles = PlayStationService.DeleteAuthoringFilesDuringCleanup;
        }

        [TearDown]
        public void TearDown()
        {
            PlayStationSDK.PlayStationSDKPath = m_previousSdkPath;
            PlayStationSDK.UserName = m_previousUserName;
            PlayStationSDK.Password = m_previousPassword;
            PlayStationSDK.Enabled = m_previousEnabled;
            PlayStationService.DeleteAuthoringFilesDuringCleanup = m_previousDeleteAuthoringFiles;
            PlayStationSDK.Instance.Initialize();
        }

        [UnityTest]
        public IEnumerator OneDummyFileUploadsThroughPublishingTool()
        {
            string root = TestSupport.OutputDirectory("FakePlayStationSdk");
            string tools = Path.Combine(root, "host_tools", "bin");
            string record = TestSupport.OutputDirectory("FakePlayStationUpload");
            Directory.CreateDirectory(tools);
            File.WriteAllText(Path.Combine(tools, "prospero-pub-cmd"), FakeUploadTool.Script("--command-file", record));

            PlayStationSDK.PlayStationSDKPath = root;
            PlayStationSDK.UserName = "build-uploader-test";
            PlayStationSDK.Password = Password;
            PlayStationSDK.Enabled = true;
            PlayStationService.DeleteAuthoringFilesDuringCleanup = true;
            PlayStationSDK.Instance.Initialize();
            Assert.IsTrue(PlayStationSDK.Instance.IsInitialized);

            var destination = new PlayStationUploadDestination(TitleId, ContentId, "none");
            UploadTask task = DestinationTestSupport.DummyFileTask("PlayStation fake upload",
                DestinationTestSupport.DummyFile("PlayStationSource"), destination);
            yield return TestSupport.Run(task);

            string report = task.Report.GetReport();
            StringAssert.Contains("[PlayStation] Upload successful!", report);
            StringAssert.DoesNotContain(Password, report, "The password is printed by the tool and must be hidden in the report.");
            string arguments = File.ReadAllText(Path.Combine(record, FakeUploadTool.ArgumentsFile));
            StringAssert.StartsWith("submit\n", arguments);
            StringAssert.Contains("--password\n" + Password + "\n", arguments);
            StringAssert.Contains("--content-id\n" + ContentId + "\n", arguments);
            StringAssert.DoesNotContain("--branch", arguments, "Branch 'none' must not be passed to the tool.");
            string commandFile = File.ReadAllText(Path.Combine(record, FakeUploadTool.CommandFile));
            StringAssert.Contains("title_id=" + TitleId, commandFile);
            StringAssert.Contains("content_id=" + ContentId, commandFile);
            FakeUploadTool.AssertUploadedDummyFile(record);
            CollectionAssert.IsEmpty(Directory.GetFiles(Path.Combine(root, "BuildUploaderScripts")),
                "The command file must be deleted during cleanup.");
        }
    }
}
#endif
