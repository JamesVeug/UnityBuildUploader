using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// GitHub has no dry run, so the dummy file is attached to a draft prerelease. Drafts are only visible to
    /// people with push access and create no tag. The draft is deleted afterwards.
    /// </summary>
    public class GithubNewReleaseDestinationTests
    {
        private string m_token;
        private string m_repository;
        private string m_tag;

        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyFileDraftRelease()
        {
            m_token = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GITHUB_TOKEN");
            m_repository = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_GITHUB_REPOSITORY");
            string[] ownerAndName = m_repository.Split('/');
            Assert.AreEqual(2, ownerAndName.Length, "BUILDUPLOADER_TEST_GITHUB_REPOSITORY must look like owner/name.");

            // A draft still needs a branch to point at, so use the repository's default branch.
            Task<RequestResult> repository = DestinationTestSupport.Send(Api(RequestWrapper.Get(RepositoryUrl)), m_token);
            yield return TestSupport.Await(repository, 60);
            Assert.IsTrue(repository.Result.IsSuccessful, "Cannot read " + m_repository + ": " + repository.Result.Data);
            string defaultBranch = (string)JSON.DeserializeObject<Dictionary<string, object>>(repository.Result.Data)["default_branch"];

            string runId = DestinationTestSupport.RunId();
            string previousToken = Github.Token;
            bool previousEnabled = Github.Enabled;
            try
            {
                Github.Token = m_token;
                Github.Enabled = true;

                var destination = new GithubNewReleaseDestination(ownerAndName[0], ownerAndName[1],
                    "Build Uploader test " + runId, runId, defaultBranch, draft: true, prerelease: true, zipContents: false);
                UploadTask task = DestinationTestSupport.DummyFileTask("GitHub destination test",
                    DestinationTestSupport.DummyFile("Github"), destination);
                // Set before running so TearDown deletes the draft even if a later assertion fails.
                m_tag = runId;
                yield return TestSupport.Execute(task, 300);

                Assert.IsTrue(task.IsSuccessful, task.Report.GetReport());
                StringAssert.Contains("Asset " + DestinationTestSupport.DummyFileName + ".txt uploaded successfully.", task.Report.GetReport());
            }
            finally
            {
                Github.Token = previousToken;
                Github.Enabled = previousEnabled;
            }

            Task<Dictionary<string, object>> find = FindRelease();
            yield return TestSupport.Await(find, 60);
            Dictionary<string, object> release = find.Result;
            Assert.IsNotNull(release, "No release with tag " + m_tag + " in " + m_repository);
            Assert.AreEqual(true, release["draft"], "The release must stay a draft.");
            List<object> assets = (List<object>)release["assets"];
            Assert.AreEqual(1, assets.Count);
            Dictionary<string, object> asset = (Dictionary<string, object>)assets[0];
            Assert.AreEqual(DestinationTestSupport.DummyFileName + ".txt", asset["name"]);
            Assert.AreEqual((long)Encoding.UTF8.GetByteCount(DestinationTestSupport.DummyContents), asset["size"]);
        }

        [UnityTearDown]
        public IEnumerator DeleteDraftRelease()
        {
            if (string.IsNullOrEmpty(m_tag))
            {
                yield break;
            }

            Task<Dictionary<string, object>> find = FindRelease();
            yield return TestSupport.Await(find, 60);
            if (find.Result != null)
            {
                string url = ReleasesUrl + "/" + find.Result["id"];
                Task<RequestResult> delete = DestinationTestSupport.Send(Api(RequestWrapper.Delete(url)), m_token);
                yield return TestSupport.Await(delete, 60);
                Assert.IsTrue(delete.Result.IsSuccessful, "Could not delete draft release " + m_tag + ": " + delete.Result.Data);
            }

            m_tag = null;
        }

        private string RepositoryUrl => "https://api.github.com/repos/" + m_repository;

        private string ReleasesUrl => RepositoryUrl + "/releases";

        /// <summary>Drafts have no tag yet, so /releases/tags/{tag} cannot find them; search the newest releases.</summary>
        private async Task<Dictionary<string, object>> FindRelease()
        {
            RequestResult response = await DestinationTestSupport.Send(Api(RequestWrapper.Get(ReleasesUrl + "?per_page=100")), m_token);
            Assert.IsTrue(response.IsSuccessful, "Cannot list releases of " + m_repository + ": " + response.Data);
            foreach (object item in JSON.DeserializeObject<List<object>>(response.Data))
            {
                Dictionary<string, object> release = (Dictionary<string, object>)item;
                if ((string)release["tag_name"] == m_tag)
                {
                    return release;
                }
            }

            return null;
        }

        private static RequestWrapper Api(RequestWrapper www)
        {
            www.SetRequestHeader("Accept", "application/vnd.github+json");
            www.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            www.SetRequestHeader("User-Agent", "Unity-GitHub-Client");
            return www;
        }
    }
}
