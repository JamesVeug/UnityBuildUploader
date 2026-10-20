using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Google Drive has no dry run, so the dummy file is uploaded to the root of My Drive, downloaded to check it
    /// arrived intact, then deleted.
    /// </summary>
    public class GoogleDriveDestinationTests
    {
        private string m_accessToken;
        private string m_fileId;

        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyFileUpload()
        {
            string clientId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_CLIENT_ID");
            string clientSecret = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_CLIENT_SECRET");
            string refreshToken = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GOOGLE_REFRESH_TOKEN");
            Task<string> login = DestinationTestSupport.RefreshAccessToken(DestinationTestSupport.GoogleTokenUrl,
                clientId, clientSecret, refreshToken);
            yield return TestSupport.Await(login, 60);
            m_accessToken = login.Result;

            string runId = DestinationTestSupport.RunId();
            var app = new GoogleConfig.GoogleApp(0, DestinationTestSupport.AppName);
            string previousToken = app.Token;
            bool previousEnabled = Google.Enabled;
            try
            {
                app.Token = m_accessToken;
                Google.Enabled = true;

                var destination = new GoogleDriveDestination(DestinationTestSupport.AppName, "");
                destination.SetFileNameFormat(runId);
                destination.SetZipContents(false);
                UploadTask task = DestinationTestSupport.DummyFileTask("Google Drive destination test",
                    DestinationTestSupport.DummyFile("GoogleDrive"), destination);
                yield return TestSupport.Execute(task, 300);

                Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
                m_fileId = destination.Context.FormatString(Context.GOOGLE_DRIVE_FOLDER_FILE_ID_KEY);
                StringAssert.Contains("Upload Successful: " + runId + ".txt (id: " + m_fileId + ")", task.Report.GetReport());
            }
            finally
            {
                app.Token = previousToken;
                Google.Enabled = previousEnabled;
            }

            Task<RequestResult> download = DestinationTestSupport.Send(RequestWrapper.Get(FileUrl(m_fileId) + "?alt=media"), m_accessToken);
            yield return TestSupport.Await(download, 60);
            Assert.IsTrue(download.Result.IsSuccessful, download.Result.Data);
            Assert.AreEqual(DestinationTestSupport.DummyContents, download.Result.Data);
        }

        [UnityTearDown]
        public IEnumerator DeleteUploadedFile()
        {
            if (string.IsNullOrEmpty(m_fileId))
            {
                yield break;
            }

            Task<RequestResult> delete = DestinationTestSupport.Send(RequestWrapper.Delete(FileUrl(m_fileId)), m_accessToken);
            yield return TestSupport.Await(delete, 60);
            Assert.IsTrue(delete.Result.IsSuccessful, "Could not delete test file " + m_fileId + " from Google Drive: " + delete.Result.Data);
            m_fileId = null;
        }

        private static string FileUrl(string fileId)
        {
            return "https://www.googleapis.com/drive/v3/files/" + fileId;
        }
    }
}
