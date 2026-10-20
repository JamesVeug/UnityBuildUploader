using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Shared setup for destination tests: the single dummy file every destination uploads, a name unique to each
    /// run, and HTTP helpers for refreshing tokens and checking or deleting what was uploaded.
    /// </summary>
    public static class DestinationTestSupport
    {
        /// <summary>Name of the temporary app entry whose token or secret a test writes to EditorPrefs.</summary>
        public const string AppName = "BuildUploaderUnitTest";
        public const string DummyFileName = "BuildUploaderDummy";
        public const string DummyContents = "Build Uploader destination test file. Safe to delete.\n";
        public const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
        public const string DropboxTokenUrl = "https://api.dropboxapi.com/oauth2/token";

        /// <summary>Unique per run, so leftovers from a failed run never collide. Only letters, digits and '-'.</summary>
        public static string RunId()
        {
            return "build-uploader-test-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>Writes the single file that is uploaded and returns its path.</summary>
        public static string DummyFile(string name, string extension = ".txt")
        {
            string path = Path.Combine(TestSupport.OutputDirectory(name), DummyFileName + extension);
            File.WriteAllText(path, DummyContents);
            return path;
        }

        public static UploadTask DummyFileTask(string name, string file, AUploadDestination destination)
        {
            var config = new UploadConfig();
            config.AddSource(new FileSource(file));
            config.AddDestination(destination);
            var task = new UploadTask(name, config);
            task.SetBuildDescription("Build Uploader destination test");
            return task;
        }

        /// <summary>Exchanges an OAuth refresh token for a short-lived access token (Google and Dropbox).</summary>
        public static async Task<string> RefreshAccessToken(string tokenUrl, string clientId, string clientSecret, string refreshToken)
        {
            string form = "grant_type=refresh_token" +
                          "&refresh_token=" + Uri.EscapeDataString(refreshToken) +
                          "&client_id=" + Uri.EscapeDataString(clientId) +
                          "&client_secret=" + Uri.EscapeDataString(clientSecret);
            using (RequestWrapper www = RequestWrapper.Post(tokenUrl))
            {
                www.SetFormData(form);
                RequestResult response = await www.SendAsync(null);
                // Error responses name the problem (e.g. invalid_grant) and never echo the secrets.
                Assert.IsTrue(response.IsSuccessful, "Could not refresh the access token at " + tokenUrl + ": " + response.Data);

                Dictionary<string, object> json = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                object token = null;
                if (json != null)
                {
                    json.TryGetValue("access_token", out token);
                }

                Assert.IsFalse(string.IsNullOrEmpty(token as string), "The token response from " + tokenUrl + " has no access_token.");
                return (string)token;
            }
        }

        /// <summary>Sends a request with a bearer token and disposes it. Nothing is logged, so the token never reaches a report.</summary>
        public static async Task<RequestResult> Send(RequestWrapper www, string accessToken)
        {
            using (www)
            {
                www.SetRequestHeader("Authorization", "Bearer " + accessToken);
                return await www.SendAsync(null);
            }
        }
    }
}
