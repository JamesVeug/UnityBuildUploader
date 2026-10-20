// BuildUploader-Hosts: OSXUniversal Linux64
// AuthoringTool.exe cannot be faked with a script, and the Nintendo SDK is under NDA, so a fake tool stands in for it.
#if !UNITY_EDITOR_WIN
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// NintendoUploadDestination uploads one dummy file through a fake AuthoringTool. Nothing leaves the machine:
    /// the fake records its arguments, the command file and the files it was asked to upload.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class NintendoUploadTests
    {
        private const string TitleId = "0100000000010000";
        private const string ApplicationId = "0100000000010001";

        private string m_previousSdkPath;
        private string m_previousUserName;
        private bool m_previousEnabled;
        private bool m_previousDeleteAuthoringFiles;

        [SetUp]
        public void SetUp()
        {
            m_previousSdkPath = NintendoSDK.NintendoSDKPath;
            m_previousUserName = NintendoSDK.UserName;
            m_previousEnabled = NintendoSDK.Enabled;
            m_previousDeleteAuthoringFiles = NintendoService.DeleteAuthoringFilesDuringCleanup;
        }

        [TearDown]
        public void TearDown()
        {
            NintendoSDK.NintendoSDKPath = m_previousSdkPath;
            NintendoSDK.UserName = m_previousUserName;
            NintendoSDK.Enabled = m_previousEnabled;
            NintendoService.DeleteAuthoringFilesDuringCleanup = m_previousDeleteAuthoringFiles;
            NintendoSDK.Instance.Initialize();
        }

        [UnityTest]
        public IEnumerator OneDummyFileUploadsThroughAuthoringTool()
        {
            string root = TestSupport.OutputDirectory("FakeNintendoSdk");
            string tools = Path.Combine(root, "NintendoSDK", "Tools");
            string record = TestSupport.OutputDirectory("FakeNintendoUpload");
            Directory.CreateDirectory(tools);
            File.WriteAllText(Path.Combine(tools, "AuthoringTool"), FakeUploadTool.Script("--command", record));

            NintendoSDK.NintendoSDKPath = root;
            NintendoSDK.UserName = "build-uploader-test";
            NintendoSDK.Enabled = true;
            NintendoService.DeleteAuthoringFilesDuringCleanup = true;
            NintendoSDK.Instance.Initialize();
            Assert.IsTrue(NintendoSDK.Instance.IsInitialized);

            var destination = new NintendoUploadDestination(TitleId, "none", ApplicationId);
            UploadTask task = DestinationTestSupport.DummyFileTask("Nintendo fake upload",
                DestinationTestSupport.DummyFile("NintendoSource"), destination);
            yield return TestSupport.Run(task);

            StringAssert.Contains("[Nintendo] Upload successful!", task.Report.GetReport());
            string arguments = File.ReadAllText(Path.Combine(record, FakeUploadTool.ArgumentsFile));
            StringAssert.Contains("--user\nbuild-uploader-test\n", arguments);
            StringAssert.Contains("--upload", arguments);
            string commandFile = File.ReadAllText(Path.Combine(record, FakeUploadTool.CommandFile));
            StringAssert.Contains("title_id=" + TitleId, commandFile);
            StringAssert.Contains("application_id=" + ApplicationId, commandFile);
            StringAssert.Contains("description=Build Uploader destination test", commandFile);
            FakeUploadTool.AssertUploadedDummyFile(record);
            CollectionAssert.IsEmpty(Directory.GetFiles(Path.Combine(root, "BuildUploaderScripts")),
                "The command file must be deleted during cleanup.");
        }
    }
}
#endif
