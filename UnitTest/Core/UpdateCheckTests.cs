using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Checking GitHub for new Build Uploader releases: version order, reading the release list, when the
    /// Build Uploader window shows its update button, and the weekly check. Git checkouts (submodules and clones)
    /// compare commits instead. No network: the feed is faked.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UpdateCheckTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        private static readonly PackageInstallSource GitTag = new PackageInstallSource(PackageInstallKind.GitPinned,
            "https://github.com/JamesVeug/UnityBuildUploader.git", "v3.3.0");
        private static readonly PackageInstallSource UnityPackage = new PackageInstallSource(PackageInstallKind.UnityPackage);
        private static readonly PackageInstallSource LocalCopy = new PackageInstallSource(PackageInstallKind.LocalCopy);

        private const string Commit1 = "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1";
        private const string Commit2 = "c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2";
        private const string Commit3 = "c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3";
        private const string UnpushedCommit = "dddddddddddddddddddddddddddddddddddddddd";

        // Shaped like api.github.com/repos/.../commits: newest first, the message, author and date nested in "commit".
        private const string CommitsJson = @"[
  {
    ""sha"": ""c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3"",
    ""html_url"": ""https://github.com/JamesVeug/UnityBuildUploader/commit/c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3"",
    ""commit"": {
      ""author"": { ""name"": ""James"", ""email"": ""james@example.com"", ""date"": ""2026-10-21T08:30:00Z"" },
      ""committer"": { ""name"": ""GitHub"", ""date"": ""2026-10-21T09:00:00Z"" },
      ""message"": ""Added update notifications\n\nA button in the window and \""Check for Updates\"".""
    },
    ""author"": { ""login"": ""JamesVeug"" },
    ""parents"": [ { ""sha"": ""c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2"" } ]
  },
  {
    ""sha"": ""c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2"",
    ""html_url"": ""https://github.com/JamesVeug/UnityBuildUploader/commit/c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2"",
    ""commit"": { ""author"": { ""name"": ""James"", ""date"": ""2026-10-20T08:30:00Z"" }, ""message"": ""Fixed paths (C:\\Builds)"" },
    ""author"": null,
    ""parents"": []
  },
  {
    ""sha"": ""c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1"",
    ""html_url"": ""https://github.com/JamesVeug/UnityBuildUploader/commit/c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1"",
    ""commit"": { ""author"": { ""name"": ""Someone"", ""date"": ""2026-10-01T08:30:00Z"" }, ""message"": ""Older commit"" },
    ""parents"": []
  }
]";

        // Shaped like api.github.com/repos/.../releases: nested objects, braces and escapes inside strings, a
        // draft, a pre-release and a tag that is not a version.
        private const string ReleasesJson = @"[
  {
    ""url"": ""https://api.github.com/repos/JamesVeug/UnityBuildUploader/releases/3"",
    ""html_url"": ""https://github.com/JamesVeug/UnityBuildUploader/releases/tag/v3.4.0"",
    ""upload_url"": ""https://uploads.github.com/repos/JamesVeug/UnityBuildUploader/releases/3/assets{?name,label}"",
    ""id"": 3,
    ""author"": { ""login"": ""JamesVeug"", ""id"": 7586886, ""site_admin"": false },
    ""tag_name"": ""v3.4.0"",
    ""target_commitish"": ""main"",
    ""name"": ""v3.4.0 Updates \u0026 \""more\"""",
    ""draft"": false,
    ""prerelease"": false,
    ""created_at"": ""2026-10-20T10:00:00Z"",
    ""published_at"": ""2026-10-20T11:03:01Z"",
    ""assets"": [
      { ""name"": ""notes.txt"", ""browser_download_url"": ""https://example.com/notes.txt"", ""size"": 12 },
      { ""name"": ""BuildUploader-v3_4_0.unitypackage"", ""browser_download_url"": ""https://github.com/JamesVeug/UnityBuildUploader/releases/download/v3.4.0/BuildUploader-v3_4_0.unitypackage"", ""uploader"": { ""login"": ""JamesVeug"" } }
    ],
    ""body"": ""## Highlights\r\n- Update button in the window\r\n- `Check for Updates` menu item\r\n\r\n## What's Fixed\r\n- Paths like C:\\Builds\\new""
  },
  {
    ""tag_name"": ""v3.5.0"",
    ""name"": ""Not published yet"",
    ""draft"": true,
    ""prerelease"": false,
    ""published_at"": null,
    ""assets"": [],
    ""body"": null
  },
  {
    ""tag_name"": ""v3.5.0-rc.1"",
    ""name"": ""Release candidate"",
    ""draft"": false,
    ""prerelease"": true,
    ""published_at"": ""2026-10-21T09:00:00Z"",
    ""assets"": [],
    ""body"": """"
  },
  {
    ""tag_name"": ""nightly"",
    ""name"": ""Nightly"",
    ""draft"": false,
    ""prerelease"": false,
    ""published_at"": ""2026-10-21T09:00:00Z"",
    ""assets"": [],
    ""body"": """"
  },
  {
    ""tag_name"": ""v3.3.0"",
    ""name"": ""v3.3.0 More Services"",
    ""draft"": false,
    ""prerelease"": false,
    ""published_at"": ""2026-08-23T11:03:01Z"",
    ""html_url"": ""https://github.com/JamesVeug/UnityBuildUploader/releases/tag/v3.3.0"",
    ""assets"": [],
    ""body"": ""## Highlights\n- More services""
  }
]";

        private IGitHubFeed m_previousFeed;
        private string m_previousCachePath;
        private string m_previousCommitsCachePath;
        private Preferences.UpdateCheck m_previousMode;
        private string m_previousSkipped;
        private string m_previousSkippedCommit;
        private string m_previousCommitsBranch;
        private DateTime m_previousRemindAfter;
        private DateTime m_previousLastCheck;
        private DateTime m_previousLastFailedCheck;

        private class FakeGitHubFeed : IGitHubFeed
        {
            public RequestResult Result;
            public RequestResult CommitsResult;
            public int Downloads;
            public readonly List<string> CommitBranches = new List<string>();

            public Task<RequestResult> DownloadReleasesAsync()
            {
                Downloads++;
                return Task.FromResult(Result);
            }

            public Task<RequestResult> DownloadCommitsAsync(string branch)
            {
                Downloads++;
                CommitBranches.Add(branch);
                return Task.FromResult(CommitsResult);
            }
        }

        [SetUp]
        public void SetUp()
        {
            m_previousFeed = UpdateChecker.Feed;
            m_previousCachePath = UpdateChecker.CachePath;
            m_previousCommitsCachePath = UpdateChecker.CommitsCachePath;
            m_previousMode = Preferences.CheckForUpdates;
            m_previousSkipped = Preferences.SkippedUpdateVersion;
            m_previousSkippedCommit = Preferences.SkippedUpdateCommit;
            m_previousCommitsBranch = Preferences.UpdateCommitsBranch;
            m_previousRemindAfter = Preferences.UpdateRemindAfterUtc;
            m_previousLastCheck = Preferences.LastUpdateCheckUtc;
            m_previousLastFailedCheck = Preferences.LastFailedUpdateCheckUtc;

            string output = TestSupport.OutputDirectory("UpdateCheck");
            UpdateChecker.CachePath = Path.Combine(output, "Releases.json");
            UpdateChecker.CommitsCachePath = Path.Combine(output, "Commits.json");
            UpdateChecker.Reload();

            // The host project's own install differs locally (a submodule) and in CI (a copy), so pick one.
            UpdateChecker.SetInstallSource(GitTag);
        }

        [TearDown]
        public void TearDown()
        {
            UpdateChecker.Feed = m_previousFeed;
            UpdateChecker.CachePath = m_previousCachePath;
            UpdateChecker.CommitsCachePath = m_previousCommitsCachePath;
            Preferences.CheckForUpdates = m_previousMode;
            Preferences.SkippedUpdateVersion = m_previousSkipped;
            Preferences.SkippedUpdateCommit = m_previousSkippedCommit;
            Preferences.UpdateCommitsBranch = m_previousCommitsBranch;
            Preferences.UpdateRemindAfterUtc = m_previousRemindAfter;
            Preferences.LastUpdateCheckUtc = m_previousLastCheck;
            Preferences.LastFailedUpdateCheckUtc = m_previousLastFailedCheck;
            UpdateChecker.Reload();
        }

        /// <summary>
        /// Notifications on, nothing skipped or put off.
        /// </summary>
        private static void ResetNotificationPreferences()
        {
            Preferences.CheckForUpdates = Preferences.UpdateCheck.Weekly;
            Preferences.SkippedUpdateVersion = "";
            Preferences.SkippedUpdateCommit = "";
            Preferences.UpdateRemindAfterUtc = DateTime.MinValue;
            Preferences.LastFailedUpdateCheckUtc = DateTime.MinValue;
        }

        private static SemanticVersion SemVer(string text)
        {
            Assert.IsTrue(SemanticVersion.TryParse(text, out SemanticVersion version), $"{text} should be a version");
            return version;
        }

        private static ReleaseInfo Release(string tag, double daysAgo, bool preRelease = false)
        {
            return new ReleaseInfo
            {
                Tag = tag,
                Name = tag,
                Version = SemVer(tag),
                PublishedUtc = Now.AddDays(-daysAgo),
                IsPreRelease = preRelease,
            };
        }

        private static UpdateNotificationRules.Settings Weekly()
        {
            return new UpdateNotificationRules.Settings
            {
                Mode = Preferences.UpdateCheck.Weekly,
                SkippedVersion = "",
                RemindAfterUtc = DateTime.MinValue,
            };
        }

        private static List<ReleaseInfo> Releases()
        {
            return new List<ReleaseInfo>
            {
                Release("v3.4.0", 10),
                Release("v3.3.1", 20),
                Release("v3.3.0", 60),
            };
        }

        [Test]
        public void VersionsCompareByNumberNotText()
        {
            Assert.IsTrue(SemVer("3.10.0") > SemVer("3.9.0"));
            Assert.IsTrue(SemVer("v3.4.0") == SemVer("3.4.0"));
            Assert.IsTrue(SemVer("3.4") == SemVer("3.4.0"));
            Assert.IsTrue(SemVer("4.0.0") > SemVer("3.99.99"));
            Assert.AreEqual("3.4.0", SemVer("v3.4.0").ToString());
        }

        [Test]
        public void PreReleaseVersionsSortBeforeTheirRelease()
        {
            Assert.IsTrue(SemVer("3.4.0-rc.1") < SemVer("3.4.0"));
            Assert.IsTrue(SemVer("3.4.0-rc.1") > SemVer("3.3.0"));
            Assert.IsTrue(SemVer("3.4.0-rc.2") < SemVer("3.4.0-rc.10"));
            Assert.IsTrue(SemVer("3.4.0-alpha") < SemVer("3.4.0-beta"));
            Assert.IsTrue(SemVer("2.1.1a") < SemVer("2.1.1"));
            Assert.IsTrue(SemVer("2.1.1a").IsPreRelease);

            // ToSemantic, used before, read 3.4.0-rc1 as 3.4.01, newer than 3.4.0.
            Assert.IsTrue(SemVer("3.4.0-rc1") < SemVer("3.4.0"));
        }

        [Test]
        public void BranchNamesAndCommitsAreNotVersions()
        {
            foreach (string text in new[] { "develop", "main", "e1dfc2a", "1234567", "", null, "3", "1.2.3.4", "v", "3.4.0-", "1.x.0" })
            {
                Assert.IsFalse(SemanticVersion.TryParse(text, out _), $"'{text}' is not a version");
            }
        }

        [Test]
        public void ReleaseFeedSkipsDraftsAndTagsThatAreNotVersions()
        {
            List<ReleaseInfo> releases = GitHubFeed.ParseReleases(ReleasesJson);

            CollectionAssert.AreEqual(new[] { "v3.5.0-rc.1", "v3.4.0", "v3.3.0" }, releases.ConvertAll(a => a.Tag),
                "Newest first, without the draft or the nightly tag.");
            Assert.IsTrue(releases[0].IsPreRelease);
            Assert.IsFalse(releases[1].IsPreRelease);
        }

        [Test]
        public void ReleaseFeedReadsNotesDatesAndTheUnityPackage()
        {
            ReleaseInfo release = GitHubFeed.ParseReleases(ReleasesJson).Find(a => a.Tag == "v3.4.0");

            Assert.AreEqual(SemVer("3.4.0"), release.Version);
            Assert.AreEqual("v3.4.0 Updates & \"more\"", release.Name);
            Assert.AreEqual(new DateTime(2026, 10, 20, 11, 3, 1, DateTimeKind.Utc), release.PublishedUtc);
            Assert.AreEqual(DateTimeKind.Utc, release.PublishedUtc.Kind);
            Assert.AreEqual("https://github.com/JamesVeug/UnityBuildUploader/releases/tag/v3.4.0", release.Url);
            Assert.AreEqual("BuildUploader-v3_4_0.unitypackage", release.UnityPackageName);
            StringAssert.EndsWith("/download/v3.4.0/BuildUploader-v3_4_0.unitypackage", release.UnityPackageUrl);

            string[] lines = ChangelogMarkdown.SplitLines(release.Notes);
            CollectionAssert.AreEqual(new[]
            {
                "## Highlights",
                "- Update button in the window",
                "- `Check for Updates` menu item",
                "",
                "## What's Fixed",
                "- Paths like C:\\Builds\\new",
            }, lines);
        }

        [Test]
        public void ReleaseFeedErrorsGiveNoReleases()
        {
            CollectionAssert.IsEmpty(GitHubFeed.ParseReleases(""));
            CollectionAssert.IsEmpty(GitHubFeed.ParseReleases("{\"message\": \"API rate limit exceeded for 1.2.3.4.\"}"));
            CollectionAssert.IsEmpty(GitHubFeed.ParseReleases("[]"));
        }

        [Test]
        public void UpdateIsOfferedForTheNewestRelease()
        {
            ReleaseInfo update = UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), Releases(), GitTag, Weekly(), Now);

            Assert.IsNotNull(update);
            Assert.AreEqual("v3.4.0", update.Tag);
        }

        [Test]
        public void NoUpdateIsOfferedWhenOnTheNewestRelease()
        {
            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.4.0"), Releases(), GitTag, Weekly(), Now));

            // A develop branch already numbered ahead of the newest release.
            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.5.0"), Releases(), GitTag, Weekly(), Now));
        }

        [Test]
        public void PreReleasesAreNeverOffered()
        {
            List<ReleaseInfo> releases = Releases();
            releases.Add(Release("v3.5.0-rc.1", 1, true));

            ReleaseInfo update = UpdateNotificationRules.ReleaseToNotify(SemVer("3.4.0"), releases, GitTag, Weekly(), Now);

            Assert.IsNull(update);
            CollectionAssert.IsEmpty(UpdateNotificationRules.NewerReleases(SemVer("3.4.0"), releases, GitTag, Now, true));
        }

        [Test]
        public void SkippedVersionHidesTheUpdateUntilANewerRelease()
        {
            UpdateNotificationRules.Settings settings = Weekly();
            settings.SkippedVersion = "3.4.0";
            List<ReleaseInfo> releases = Releases();

            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), releases, GitTag, settings, Now));

            releases.Add(Release("v3.4.1", 1));
            ReleaseInfo update = UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), releases, GitTag, settings, Now);
            Assert.AreEqual("v3.4.1", update.Tag);
        }

        [Test]
        public void RemindMeLaterHidesTheUpdateUntilItExpires()
        {
            UpdateNotificationRules.Settings settings = Weekly();
            settings.RemindAfterUtc = Now.AddDays(1);

            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), Releases(), GitTag, settings, Now));
            Assert.IsNotNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), Releases(), GitTag, settings, Now.AddDays(1).AddMinutes(1)));
        }

        [Test]
        public void NeverCheckingHidesTheUpdate()
        {
            UpdateNotificationRules.Settings settings = Weekly();
            settings.Mode = Preferences.UpdateCheck.Never;

            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), Releases(), GitTag, settings, Now));
        }

        [Test]
        public void UnityPackageInstallsWaitForTheAssetStore()
        {
            List<ReleaseInfo> releases = Releases();
            releases.Add(Release("v3.5.0", 1));

            Assert.AreEqual("v3.5.0", UpdateNotificationRules.ReleaseToNotify(SemVer("3.4.0"), releases, GitTag, Weekly(), Now).Tag,
                "Git installs get the release straight away.");
            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.4.0"), releases, UnityPackage, Weekly(), Now),
                "Asset Store installs wait while the Asset Store reviews the release.");
            Assert.AreEqual("v3.5.0", UpdateNotificationRules.ReleaseToNotify(SemVer("3.4.0"), releases, UnityPackage, Weekly(), Now.AddDays(3)).Tag);

            // Older releases are already on the Asset Store.
            Assert.AreEqual("v3.4.0", UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), releases, UnityPackage, Weekly(), Now).Tag);
        }

        [Test]
        public void LocalCopiesAreNotOfferedUpdates()
        {
            Assert.IsNull(UpdateNotificationRules.ReleaseToNotify(SemVer("3.3.0"), Releases(), LocalCopy, Weekly(), Now));
        }

        [Test]
        public void CheckForUpdatesListsEveryNewerReleaseOnGitHub()
        {
            List<ReleaseInfo> releases = Releases();
            releases.Add(Release("v3.5.0", 1));

            List<ReleaseInfo> newer = UpdateNotificationRules.NewerReleases(SemVer("3.3.0"), releases, UnityPackage, Now, true);

            CollectionAssert.AreEqual(new[] { "v3.5.0", "v3.4.0", "v3.3.1" }, newer.ConvertAll(a => a.Tag));
            Assert.IsTrue(UpdateNotificationRules.IsWaitingForAssetStore(newer[0], UnityPackage, Now));
            Assert.IsFalse(UpdateNotificationRules.IsWaitingForAssetStore(newer[0], GitTag, Now));
        }

        [Test]
        public void CheckForUpdatesNowKeepsTheReleasesFromGitHub()
        {
            FakeGitHubFeed feed = new FakeGitHubFeed { Result = RequestResult.Successful(ReleasesJson, null) };
            UpdateChecker.Feed = feed;
            DateTime before = DateTime.UtcNow.AddSeconds(-1);

            Task<bool> check = UpdateChecker.CheckNowAsync();

            Assert.IsTrue(check.IsCompleted, "The fake feed answers straight away.");
            Assert.IsTrue(check.Result, UpdateChecker.LastError);
            Assert.IsNull(UpdateChecker.LastError);
            Assert.IsFalse(UpdateChecker.IsChecking);
            Assert.AreEqual(1, feed.Downloads);
            Assert.AreEqual(3, UpdateChecker.Releases.Count);
            Assert.GreaterOrEqual(Preferences.LastUpdateCheckUtc, before);
            Assert.AreEqual(ReleasesJson, File.ReadAllText(UpdateChecker.CachePath));

            // A script reload reads the releases back from the cache.
            UpdateChecker.Reload();
            Assert.AreEqual(3, UpdateChecker.Releases.Count);
        }

        [Test]
        public void FailedUpdateCheckKeepsTheLastReleases()
        {
            FakeGitHubFeed feed = new FakeGitHubFeed { Result = RequestResult.Successful(ReleasesJson, null) };
            UpdateChecker.Feed = feed;
            Assert.IsTrue(UpdateChecker.CheckNowAsync().Result);

            feed.Result = RequestResult.Failed("Request Failed: 403 - API rate limit exceeded");
            DateTime before = DateTime.UtcNow.AddSeconds(-1);
            Task<bool> check = UpdateChecker.CheckNowAsync();

            Assert.IsFalse(check.Result);
            StringAssert.Contains("403", UpdateChecker.LastError);
            Assert.AreEqual(3, UpdateChecker.Releases.Count);
            Assert.GreaterOrEqual(Preferences.LastFailedUpdateCheckUtc, before);
            Assert.AreEqual(ReleasesJson, File.ReadAllText(UpdateChecker.CachePath));
        }

        [Test]
        public void WeeklyUpdateCheckWaitsAWeek()
        {
            FakeGitHubFeed feed = new FakeGitHubFeed { Result = RequestResult.Successful(ReleasesJson, null) };
            UpdateChecker.Feed = feed;
            ResetNotificationPreferences();

            Preferences.LastUpdateCheckUtc = DateTime.UtcNow.AddDays(-6);
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(0, feed.Downloads, "Checked less than a week ago.");

            Preferences.LastUpdateCheckUtc = DateTime.UtcNow.AddDays(-8);
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(1, feed.Downloads, "Checked more than a week ago.");

            Preferences.LastUpdateCheckUtc = DateTime.MinValue;
            Preferences.CheckForUpdates = Preferences.UpdateCheck.Never;
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(1, feed.Downloads, "Checking is turned off.");

            Preferences.CheckForUpdates = Preferences.UpdateCheck.Weekly;
            Preferences.LastFailedUpdateCheckUtc = DateTime.UtcNow.AddMinutes(-10);
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(1, feed.Downloads, "Failed less than an hour ago, so being offline doesn't retry on every reload.");
        }

        [Test]
        public void MonthlyUpdateCheckWaitsAMonth()
        {
            FakeGitHubFeed feed = new FakeGitHubFeed { Result = RequestResult.Successful(ReleasesJson, null) };
            UpdateChecker.Feed = feed;
            ResetNotificationPreferences();
            Preferences.CheckForUpdates = Preferences.UpdateCheck.Monthly;

            Preferences.LastUpdateCheckUtc = DateTime.UtcNow.AddDays(-29);
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(0, feed.Downloads, "Checked less than a month ago.");

            Preferences.LastUpdateCheckUtc = DateTime.UtcNow.AddDays(-31);
            UpdateChecker.CheckIfDue();
            Assert.AreEqual(1, feed.Downloads, "Checked more than a month ago.");

            // EditorPrefs stores the number, so a saved Weekly or Never keeps its meaning.
            Assert.AreEqual(0, (int)Preferences.UpdateCheck.Weekly);
            Assert.AreEqual(1, (int)Preferences.UpdateCheck.Never);
            Assert.AreEqual(Preferences.UpdateCheckOptions.Length, Preferences.UpdateCheckOptionValues.Length);
            Assert.IsNull(UpdateChecker.CheckInterval(Preferences.UpdateCheck.Never));
        }

        [Test]
        public void UpdateButtonStateIsKeptUntilSomethingChanges()
        {
            ResetNotificationPreferences();
            const string futureRelease = "[{ \"tag_name\": \"v999.0.0\", \"draft\": false, \"prerelease\": false, \"published_at\": \"2026-01-01T00:00:00Z\" }]";
            UpdateChecker.Feed = new FakeGitHubFeed { Result = RequestResult.Successful(futureRelease, null) };
            Assert.IsTrue(UpdateChecker.CheckNowAsync().Result, UpdateChecker.LastError);
            Assert.AreEqual("v999.0.0", UpdateChecker.AvailableUpdate.Tag);

            // Changed without telling the checker: the window keeps what it worked out instead of re-reading every repaint.
            Preferences.SkippedUpdateVersion = "999.0.0";
            Assert.IsNotNull(UpdateChecker.AvailableUpdate);

            UpdateChecker.NotifyChanged();
            Assert.IsNull(UpdateChecker.AvailableUpdate);
        }

        private static LocalGitCheckout Checkout(string headSha, string fetchedSha)
        {
            return new LocalGitCheckout { HeadSha = headSha, Branch = "develop", FetchedSha = fetchedSha };
        }

        [Test]
        public void CommitFeedReadsMessagesAuthorsAndDates()
        {
            List<CommitInfo> commits = GitHubFeed.ParseCommits(CommitsJson);

            CollectionAssert.AreEqual(new[] { Commit3, Commit2, Commit1 }, commits.ConvertAll(a => a.Sha), "Newest first, as GitHub lists them.");
            Assert.AreEqual("Added update notifications", commits[0].Subject);
            Assert.AreEqual("A button in the window and \"Check for Updates\".", commits[0].Body);
            Assert.AreEqual("James", commits[0].Author);
            Assert.AreEqual(new DateTime(2026, 10, 21, 8, 30, 0, DateTimeKind.Utc), commits[0].DateUtc);
            Assert.AreEqual("https://github.com/JamesVeug/UnityBuildUploader/commit/" + Commit3, commits[0].Url);
            Assert.AreEqual("c3c3c3c", commits[0].ShortSha);
            Assert.AreEqual("Fixed paths (C:\\Builds)", commits[1].Subject);
            Assert.AreEqual("", commits[1].Body);
            CollectionAssert.IsEmpty(GitHubFeed.ParseCommits("{\"message\": \"No commit found for SHA: develop\"}"));
        }

        [Test]
        public void CommitUpdateListsTheCommitsNewerThanYours()
        {
            CommitUpdate update = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(Commit1, Commit1), "develop");

            Assert.AreEqual(CommitUpdateState.Behind, update.State);
            CollectionAssert.AreEqual(new[] { Commit3, Commit2 }, update.NewCommits.ConvertAll(a => a.Sha));
            Assert.AreEqual(Commit1, update.YourSha);
            Assert.AreEqual(Commit3, update.LatestSha);
            Assert.AreEqual("develop", update.CheckedOutBranch);
            Assert.IsFalse(update.ComparedWithFetched);
        }

        [Test]
        public void CommitUpdateIsUpToDateOnTheNewestCommit()
        {
            CommitUpdate update = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(Commit3, Commit3), "develop");

            Assert.AreEqual(CommitUpdateState.UpToDate, update.State);
            CollectionAssert.IsEmpty(update.NewCommits);
        }

        [Test]
        public void CommitUpdateUsesTheFetchedBranchForUnpushedCommits()
        {
            CommitUpdate update = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(UnpushedCommit, Commit2), "develop");

            Assert.AreEqual(CommitUpdateState.Behind, update.State);
            Assert.IsTrue(update.ComparedWithFetched);
            Assert.AreEqual(Commit2, update.YourSha);
            CollectionAssert.AreEqual(new[] { Commit3 }, update.NewCommits.ConvertAll(a => a.Sha));
        }

        [Test]
        public void CommitUpdateCantFindAnUnknownCommit()
        {
            CommitUpdate update = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(UnpushedCommit, null), "develop");

            Assert.AreEqual(CommitUpdateState.NotFound, update.State);
            CollectionAssert.IsEmpty(update.NewCommits);
        }

        [Test]
        public void NewCommitsFollowSkipRemindLaterAndNever()
        {
            CommitUpdate behind = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(Commit1, Commit1), "develop");
            CommitUpdate upToDate = CommitUpdate.Compare(GitHubFeed.ParseCommits(CommitsJson), Checkout(Commit3, Commit3), "develop");

            Assert.AreSame(behind, UpdateNotificationRules.CommitsToNotify(behind, Weekly(), Now));
            Assert.IsNull(UpdateNotificationRules.CommitsToNotify(upToDate, Weekly(), Now));
            Assert.IsNull(UpdateNotificationRules.CommitsToNotify(null, Weekly(), Now));

            UpdateNotificationRules.Settings skipped = Weekly();
            skipped.SkippedCommit = Commit3;
            Assert.IsNull(UpdateNotificationRules.CommitsToNotify(behind, skipped, Now), "Skipped up to the newest commit.");
            skipped.SkippedCommit = Commit2;
            Assert.AreSame(behind, UpdateNotificationRules.CommitsToNotify(behind, skipped, Now), "A newer commit came out after skipping.");

            UpdateNotificationRules.Settings remind = Weekly();
            remind.RemindAfterUtc = Now.AddDays(1);
            Assert.IsNull(UpdateNotificationRules.CommitsToNotify(behind, remind, Now));

            UpdateNotificationRules.Settings never = Weekly();
            never.Mode = Preferences.UpdateCheck.Never;
            Assert.IsNull(UpdateNotificationRules.CommitsToNotify(behind, never, Now));
        }

        [Test]
        public void CheckForUpdatesNowComparesAGitCheckoutsCommits()
        {
            ResetNotificationPreferences();
            string folder = FakeGitCheckout.OnBranch("UpdateCheckGit", "develop", Commit1, Commit1);
            UpdateChecker.SetInstallSource(new PackageInstallSource(PackageInstallKind.GitCheckout, packageFolder: folder));
            FakeGitHubFeed feed = new FakeGitHubFeed { CommitsResult = RequestResult.Successful(CommitsJson, null) };
            UpdateChecker.Feed = feed;
            Assert.AreEqual(CommitUpdateState.NotChecked, UpdateChecker.GetCommitUpdate().State);

            Task<bool> check = UpdateChecker.CheckNowAsync();

            Assert.IsTrue(check.Result, UpdateChecker.LastError);
            CollectionAssert.AreEqual(new[] { "develop" }, feed.CommitBranches, "Only commits are downloaded, for the checked out branch.");
            Assert.AreEqual("develop", Preferences.UpdateCommitsBranch);
            Assert.AreEqual(CommitsJson, File.ReadAllText(UpdateChecker.CommitsCachePath));
            CommitUpdate update = UpdateChecker.AvailableCommits;
            Assert.IsNotNull(update, "The window shows the update button.");
            Assert.AreEqual(2, update.NewCommits.Count);
            Assert.IsNull(UpdateChecker.AvailableUpdate, "Git checkouts aren't offered releases.");

            // Pulling moves HEAD to the newest commit, which is enough to hide the button without asking GitHub again.
            FakeGitCheckout.MoveBranch(folder, "develop", Commit3);
            UpdateChecker.NotifyChanged();
            Assert.AreEqual(CommitUpdateState.UpToDate, UpdateChecker.GetCommitUpdate().State);
            Assert.IsNull(UpdateChecker.AvailableCommits);
            Assert.AreEqual(1, feed.Downloads);
        }

        [Test]
        public void GitCheckoutOnAnUnpushedBranchIsNotChecked()
        {
            ResetNotificationPreferences();
            Preferences.LastUpdateCheckUtc = DateTime.MinValue;
            string folder = FakeGitCheckout.OnBranch("UpdateCheckLocalBranch", "my-work", UnpushedCommit, null);
            UpdateChecker.SetInstallSource(new PackageInstallSource(PackageInstallKind.GitCheckout, packageFolder: folder));
            FakeGitHubFeed feed = new FakeGitHubFeed { CommitsResult = RequestResult.Successful(CommitsJson, null) };
            UpdateChecker.Feed = feed;

            UpdateChecker.CheckIfDue();

            Assert.AreEqual(0, feed.Downloads, "origin doesn't have the branch, so there is nothing on GitHub to compare with.");
            Assert.AreEqual(CommitUpdateState.LocalBranch, UpdateChecker.GetCommitUpdate().State);
            Assert.IsNull(UpdateChecker.AvailableCommits);
        }

        [Test]
        public void ChangelogSplitsIntoOneEntryPerVersion()
        {
            const string changelog = "# 3.3.0\n\n## Highlights\n- One\n\n# 3.2.2\n## What's Fixed\n- Two\n\n";

            List<ChangelogMarkdown.Entry> entries = ChangelogMarkdown.Parse(changelog);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("v3.3.0", entries[0].Title);
            CollectionAssert.AreEqual(new[] { "## Highlights", "- One" }, entries[0].Lines);
            Assert.AreEqual("v3.2.2", entries[1].Title);
            CollectionAssert.AreEqual(new[] { "## What's Fixed", "- Two" }, entries[1].Lines);
            Assert.IsFalse(entries[0].FoldoutOpen);
        }
    }
}
