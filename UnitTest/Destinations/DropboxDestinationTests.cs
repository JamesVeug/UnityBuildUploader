using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Dropbox has no dry run, so the dummy file is uploaded to a test folder, downloaded to check it arrived
    /// intact, then deleted. A Dropbox app with "App folder" access keeps this inside Apps/&lt;app name&gt;.
    /// </summary>
    public class DropboxDestinationTests
    {
        private const string Folder = "/BuildUploaderTests";

        private string m_accessToken;
        private string m_uploadedPath;

        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyFileUpload()
        {
            string appKey = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_DROPBOX_APP_KEY");
            string appSecret = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_DROPBOX_APP_SECRET");
            string refreshToken = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_DROPBOX_REFRESH_TOKEN");
            Task<string> login = DestinationTestSupport.RefreshAccessToken(DestinationTestSupport.DropboxTokenUrl,
                appKey, appSecret, refreshToken);
            yield return TestSupport.Await(login, 60);
            m_accessToken = login.Result;

            string runId = DestinationTestSupport.RunId();
            var app = new DropboxConfig.DropboxApp { Name = DestinationTestSupport.AppName };
            string previousToken = app.Token;
            bool previousEnabled = Dropbox.Enabled;
            try
            {
                app.Token = m_accessToken;
                Dropbox.Enabled = true;

                var destination = new DropboxDestination(DestinationTestSupport.AppName, Folder);
                destination.SetFileNameFormat(runId);
                destination.SetZipContents(false);
                UploadTask task = DestinationTestSupport.DummyFileTask("Dropbox destination test",
                    DestinationTestSupport.DummyFile("Dropbox"), destination);
                // Set before running so TearDown deletes the file even if a later assertion fails.
                m_uploadedPath = Folder + "/" + runId + ".txt";
                yield return TestSupport.Execute(task, 300);

                Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
                StringAssert.Contains("Upload Successful: " + m_uploadedPath, task.Report.GetReport());
            }
            finally
            {
                app.Token = previousToken;
                Dropbox.Enabled = previousEnabled;
            }

            RequestWrapper request = RequestWrapper.Post("https://content.dropboxapi.com/2/files/download");
            request.SetRequestHeader("Dropbox-API-Arg", JSON.SerializeObject(new Dictionary<string, object> { { "path", m_uploadedPath } }));
            Task<RequestResult> download = DestinationTestSupport.Send(request, m_accessToken);
            yield return TestSupport.Await(download, 60);
            Assert.IsTrue(download.Result.IsSuccessful, download.Result.Data);
            Assert.AreEqual(DestinationTestSupport.DummyContents, download.Result.Data);
        }

        [UnityTearDown]
        public IEnumerator DeleteUploadedFile()
        {
            if (string.IsNullOrEmpty(m_uploadedPath))
            {
                yield break;
            }

            RequestWrapper request = RequestWrapper.Post("https://api.dropboxapi.com/2/files/delete_v2");
            request.SetJSONData(new Dictionary<string, object> { { "path", m_uploadedPath } });
            Task<RequestResult> delete = DestinationTestSupport.Send(request, m_accessToken);
            yield return TestSupport.Await(delete, 60);
            // A failed upload leaves nothing to delete (path_lookup/not_found).
            Assert.IsTrue(delete.Result.IsSuccessful || delete.Result.Data.Contains("not_found"),
                "Could not delete test file " + m_uploadedPath + " from Dropbox: " + delete.Result.Data);
            m_uploadedPath = null;
        }
    }
}
