// BuildUploader-Hosts: OSXUniversal Linux64
// BuildPatchTool.exe cannot be faked with a script, and Windows has no exec bit to restore.
#if !UNITY_EDITOR_WIN
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// EpicGames.Upload restores BuildPatchTool's exec bit before running it (PR #118). A fake BuildPatchTool
    /// stands in for Epic's tool, so no Epic credentials or network are used.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class EpicGamesUploadTests
    {
        // EpicGames.SDKPath is a machine-wide EditorPrefs key, so it is restored exactly.
        private const string SdkPathKey = "epicgames_sdkpath";

        private bool m_hadSdkPath;
        private string m_previousSdkPath;

        [SetUp]
        public void SetUp()
        {
            m_hadSdkPath = EditorPrefs.HasKey(SdkPathKey);
            m_previousSdkPath = EditorPrefs.GetString(SdkPathKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_hadSdkPath) EditorPrefs.SetString(SdkPathKey, m_previousSdkPath);
            else EditorPrefs.DeleteKey(SdkPathKey);
        }

        [UnityTest]
        public IEnumerator UploadMakesBuildPatchToolExecutable()
        {
            string sdk = TestSupport.OutputDirectory("FakeEpicSdk");
            EpicGames.SDKPath = sdk;
            string tool = EpicGames.GetEXEPath();
            Directory.CreateDirectory(Path.GetDirectoryName(tool));
            string argumentsFile = Path.Combine(sdk, "arguments.txt");
            // Written without the exec bit. Records its arguments and reports success like BuildPatchTool.
            File.WriteAllText(tool, "#!/bin/sh\nprintf '%s\\n' \"$@\" > " + TestSupport.Quote(argumentsFile) +
                                    "\necho 'Successfully registered binary in artifact service'\n");
            string build = TestSupport.FixtureDirectory("EpicBuild");
            UploadTaskReport.StepResult step = TestSupport.Report();

            Task<bool> upload = EpicGames.Upload(step, new Context(), build, "org", "product", "artifact", "client",
                TestSupport.OutputDirectory("EpicCloud"), "1.0.0", "Game", EpicGamesProduct.SecretTypes.EnvVar,
                "BUILDUPLOADER_TEST_EPIC_SECRET");
            yield return TestSupport.Await(upload, 60);

            Assert.IsTrue(upload.Result, step.FailReason);
            Assert.IsTrue(ProcessUtilsTests.IsExecutable(tool), "Upload must make BuildPatchTool executable.");
            string arguments = File.ReadAllText(argumentsFile);
            StringAssert.Contains("-mode=UploadBinary", arguments);
            StringAssert.Contains("-ClientSecretEnvVar=BUILDUPLOADER_TEST_EPIC_SECRET", arguments,
                "An environment-variable secret must be passed by name, never by value.");
        }
    }
}
#endif
