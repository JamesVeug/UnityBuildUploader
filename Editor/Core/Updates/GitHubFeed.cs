using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Where the update check downloads releases and commits from. Tests swap in their own.
    /// </summary>
    internal interface IGitHubFeed
    {
        /// <summary>
        /// The GitHub releases JSON.
        /// </summary>
        Task<RequestResult> DownloadReleasesAsync();

        /// <summary>
        /// The GitHub JSON for the newest commits on a branch.
        /// </summary>
        /// <param name="branch">The branch, or "" for the repository's default branch.</param>
        Task<RequestResult> DownloadCommitsAsync(string branch);
    }

    internal class GitHubFeed : IGitHubFeed
    {
        public const string RepositoryUrl = "https://github.com/JamesVeug/UnityBuildUploader";

        /// <summary>
        /// How many commits a git checkout is compared against.
        /// </summary>
        public const int CommitsPerCheck = 100;

        // By repository id rather than name so the check keeps working if the repository is renamed again.
        private const string ApiUrl = "https://api.github.com/repositories/778195997";

        public Task<RequestResult> DownloadReleasesAsync()
        {
            return DownloadAsync(ApiUrl + "/releases?per_page=20");
        }

        public Task<RequestResult> DownloadCommitsAsync(string branch)
        {
            string url = ApiUrl + "/commits?per_page=" + CommitsPerCheck;
            if (!string.IsNullOrEmpty(branch))
            {
                url += "&sha=" + Uri.EscapeDataString(branch);
            }

            return DownloadAsync(url);
        }

        private static async Task<RequestResult> DownloadAsync(string url)
        {
            using (RequestWrapper www = RequestWrapper.Get(url))
            {
                www.SetRequestHeader("Accept", "application/vnd.github+json");
                return await www.SendAsync(null);
            }
        }

        /// <summary>
        /// Releases newest first. Drafts and tags that are not versions are left out.
        /// </summary>
        public static List<ReleaseInfo> ParseReleases(string json)
        {
            List<ReleaseInfo> releases = new List<ReleaseInfo>();
            foreach (Dictionary<string, object> entry in ParseList(json))
            {
                if (GetBool(entry, "draft"))
                {
                    continue;
                }

                string tag = GetString(entry, "tag_name");
                if (!SemanticVersion.TryParse(tag, out SemanticVersion version))
                {
                    continue;
                }

                ReleaseInfo release = new ReleaseInfo
                {
                    Tag = tag,
                    Version = version,
                    Name = GetString(entry, "name"),
                    Notes = GetString(entry, "body"),
                    IsPreRelease = GetBool(entry, "prerelease") || version.IsPreRelease,
                    PublishedUtc = GetDate(entry, "published_at"),
                    Url = GetString(entry, "html_url"),
                };

                if (entry.TryGetValue("assets", out object assets) && assets is List<object> assetList)
                {
                    foreach (object asset in assetList)
                    {
                        if (asset is Dictionary<string, object> assetData)
                        {
                            string name = GetString(assetData, "name");
                            if (name.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                            {
                                release.UnityPackageName = name;
                                release.UnityPackageUrl = GetString(assetData, "browser_download_url");
                                break;
                            }
                        }
                    }
                }

                releases.Add(release);
            }

            releases.Sort((a, b) => b.Version.CompareTo(a.Version));
            return releases;
        }

        /// <summary>
        /// Commits newest first, as GitHub lists them.
        /// </summary>
        public static List<CommitInfo> ParseCommits(string json)
        {
            List<CommitInfo> commits = new List<CommitInfo>();
            foreach (Dictionary<string, object> entry in ParseList(json))
            {
                string sha = GetString(entry, "sha");
                if (sha.Length == 0)
                {
                    continue;
                }

                CommitInfo commit = new CommitInfo
                {
                    Sha = sha,
                    Url = GetString(entry, "html_url"),
                };

                if (entry.TryGetValue("commit", out object details) && details is Dictionary<string, object> detailData)
                {
                    commit.Message = GetString(detailData, "message");
                    if (detailData.TryGetValue("author", out object author) && author is Dictionary<string, object> authorData)
                    {
                        commit.Author = GetString(authorData, "name");
                        commit.DateUtc = GetDate(authorData, "date");
                    }
                }

                commits.Add(commit);
            }

            return commits;
        }

        private static List<Dictionary<string, object>> ParseList(string json)
        {
            if (string.IsNullOrEmpty(json) || json.TrimStart().StartsWith("{"))
            {
                // GitHub answers errors with an object such as {"message": "API rate limit exceeded"}.
                return new List<Dictionary<string, object>>();
            }

            List<Dictionary<string, object>> entries = JSON.DeserializeObject<List<Dictionary<string, object>>>(json);
            if (entries == null)
            {
                return new List<Dictionary<string, object>>();
            }

            entries.RemoveAll(a => a == null);
            return entries;
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value != null ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value is bool b && b;
        }

        private static DateTime GetDate(Dictionary<string, object> data, string key)
        {
            DateTime.TryParse(GetString(data, key), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date);
            return date;
        }
    }
}
