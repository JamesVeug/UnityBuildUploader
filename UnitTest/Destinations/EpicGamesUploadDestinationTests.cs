using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// BuildPatchTool has no dry run. UploadBinary only registers a binary with the artifact; players see nothing
    /// until a binary is labeled, which this test never does. Each run uses a new Build Version because Epic
    /// rejects a repeated one, so every run leaves one unlabeled binary in the artifact.
    /// </summary>
    public class EpicGamesUploadDestinationTests
    {
        // BuildPatchTool reads the secret from this variable itself; the test never reads or passes its value.
        private const string ClientSecretVariable = "BUILDUPLOADER_TEST_EPIC_CLIENT_SECRET";
        // EpicGames.SDKPath is a machine-wide EditorPrefs key, so it is restored exactly.
        private const string SdkPathKey = "epicgames_sdkpath";

        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyFileUnlabeledBinary()
        {
            string sdk = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_EPIC_SDK");
            string organization = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_EPIC_ORGANIZATION_ID");
            string product = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_EPIC_PRODUCT_ID");
            string artifact = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_EPIC_ARTIFACT_ID");
            string client = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_EPIC_CLIENT_ID");
            TestSupport.RequiredEnvironment(ClientSecretVariable);

            bool hadSdkPath = EditorPrefs.HasKey(SdkPathKey);
            string previousSdkPath = EditorPrefs.GetString(SdkPathKey);
            bool previousEnabled = EpicGames.Enabled;
            try
            {
                EpicGames.SDKPath = sdk;
                EpicGames.Enabled = true;
                Assert.IsTrue(File.Exists(EpicGames.GetEXEPath()), "BuildPatchTool not found at " + EpicGames.GetEXEPath());

                string runId = DestinationTestSupport.RunId();
                var destination = new EpicGamesUploadDestination(organization, product, artifact, client,
                    ClientSecretVariable, DestinationTestSupport.DummyFileName + ".txt");
                destination.SetBuildVersion(runId);
                UploadTask task = DestinationTestSupport.DummyFileTask("Epic Games destination test",
                    DestinationTestSupport.DummyFile("EpicGames"), destination);
                yield return TestSupport.Execute(task, 600);

                string report = task.Report.GetReport();
                Assert.IsTrue(task.IsSuccessful, report);
                StringAssert.Contains("Successfully registered binary in artifact service", report);
            }
            finally
            {
                if (hadSdkPath) EditorPrefs.SetString(SdkPathKey, previousSdkPath);
                else EditorPrefs.DeleteKey(SdkPathKey);
                EpicGames.Enabled = previousEnabled;
            }
        }
    }
}
