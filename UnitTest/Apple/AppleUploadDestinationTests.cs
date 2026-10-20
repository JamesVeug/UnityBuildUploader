using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Dry run against App Store Connect. altool only accepts a signed .ipa, so a dummy file cannot be uploaded;
    /// the dry run signs a token with the API key and reads the app instead. It skips altool, so it runs on any host.
    /// </summary>
    public class AppleUploadDestinationTests
    {
        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyIpaDryRun()
        {
            string issuerId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_APPLE_ISSUER_ID");
            string keyId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_APPLE_KEY_ID");
            string privateKeyPath = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_APPLE_PRIVATE_KEY_PATH");
            string appId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_APPLE_APP_ID");
            Assert.IsTrue(File.Exists(privateKeyPath), "App Store Connect API key file not found: " + privateKeyPath);

            var apiKey = new AppleConfig.AppleApiKey(0, DestinationTestSupport.AppName)
            {
                IssuerID = issuerId,
                KeyID = keyId
            };
            var app = new AppleConfig.AppleApp(0, DestinationTestSupport.AppName, "")
            {
                AppStoreConnectID = appId
            };
            string previousKeyPath = apiKey.PrivateKeyPath;
            bool previousEnabled = Apple.Enabled;
            try
            {
                apiKey.PrivateKeyPath = privateKeyPath;
                Apple.Enabled = true;

                var destination = new AppleUploadDestination();
                destination.SetApiKey(apiKey);
                destination.SetApp(app);
                destination.SetDryRun(true);
                UploadTask task = DestinationTestSupport.DummyFileTask("Apple destination test",
                    DestinationTestSupport.DummyFile("Apple", ".ipa"), destination);
                yield return TestSupport.Execute(task, 300);

                string report = task.Report.GetReport();
                Assert.IsTrue(task.IsSuccessful, report);
                StringAssert.Contains("Found App Store Connect app " + appId, report);
                StringAssert.Contains("Dry run: would upload " + DestinationTestSupport.DummyFileName + ".ipa", report);
            }
            finally
            {
                apiKey.PrivateKeyPath = previousKeyPath;
                Apple.Enabled = previousEnabled;
            }
        }
    }
}
