using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Dry run against the Play Developer API. Play only accepts a signed bundle with a new versionCode, so a dummy
    /// file cannot be uploaded; the dry run opens an edit to check the credentials and package name, then deletes it.
    /// </summary>
    public class GooglePlayDestinationTests
    {
        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyBundleDryRun()
        {
            string packageName = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_PLAY_PACKAGE");
            string clientId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_CLIENT_ID");
            string clientSecret = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_CLIENT_SECRET");
            string refreshToken = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_REFRESH_TOKEN");
            Task<string> login = DestinationTestSupport.RefreshAccessToken(DestinationTestSupport.GoogleTokenUrl,
                clientId, clientSecret, refreshToken);
            yield return TestSupport.Await(login, 60);

            var app = new GoogleConfig.GoogleApp(0, DestinationTestSupport.AppName);
            string previousToken = app.Token;
            bool previousEnabled = Google.Enabled;
            try
            {
                app.Token = login.Result;
                Google.Enabled = true;

                var destination = new GooglePlayDestination(DestinationTestSupport.AppName, packageName, GooglePlayTrack.Internal);
                destination.SetDryRun(true);
                UploadTask task = DestinationTestSupport.DummyFileTask("Google Play destination test",
                    DestinationTestSupport.DummyFile("GooglePlay", ".aab"), destination);
                yield return TestSupport.Execute(task, 300);

                string report = task.Report.GetReport();
                Assert.IsTrue(task.IsSuccessful, report);
                StringAssert.Contains("Dry run: would upload " + DestinationTestSupport.DummyFileName + ".aab", report);
                StringAssert.Contains("without committing it", report);
            }
            finally
            {
                app.Token = previousToken;
                Google.Enabled = previousEnabled;
            }
        }
    }
}
