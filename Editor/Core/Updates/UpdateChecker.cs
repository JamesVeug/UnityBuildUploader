using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// When Unity opens, asks GitHub (at most weekly or monthly, as set in Preferences) whether the Build Uploader has
    /// a new version so the Build Uploader window can offer it. Script reloads and the window never check, to keep the editor's overhead down.
    /// Installs from Git URLs, the Asset Store or a .unitypackage compare releases; a submodule or clone of the
    /// package compares its commit with the newest commits on its branch.
    /// What GitHub returned is kept in Library so the button survives script reloads and editor restarts.
    /// </summary>
    [InitializeOnLoad]
    internal static class UpdateChecker
    {
        private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

        /// <summary>
        /// Survives script reloads but not restarting Unity, so only the first load after opening Unity checks.
        /// </summary>
        private const string StartupCheckKey = "BuildUploader_UpdateCheckedThisSession";

        /// <summary>
        /// Raised when a check starts or finishes and when the user hides or skips an update.
        /// </summary>
        public static event Action StateChanged = delegate { };

        internal static IGitHubFeed Feed = new GitHubFeed();
        internal static string CachePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "BuildUploader", "Releases.json"));
        internal static string CommitsCachePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "BuildUploader", "Commits.json"));

        public static bool IsChecking { get; private set; }

        /// <summary>
        /// Why the last check failed, or null when it worked.
        /// </summary>
        public static string LastError { get; private set; }

        private static List<ReleaseInfo> s_releases;
        private static List<CommitInfo> s_commits;
        private static PackageInstallSource s_installSource;
        private static ReleaseInfo s_availableUpdate;
        private static CommitUpdate s_availableCommits;
        private static bool s_notificationKnown;
        private static CommitUpdate s_commitUpdate;
        private static bool s_commitUpdateKnown;

        static UpdateChecker()
        {
            // Batch mode is CI or the unity CLI: nobody sees the button and the request would only slow them down.
            if (Application.isBatchMode || SessionState.GetBool(StartupCheckKey, false))
            {
                return;
            }

            // Not delayCall: that waits for a repaint, which never comes while Unity opens in the background.
            EditorApplication.update += WaitToCheckOnStartup;
        }

        private static void WaitToCheckOnStartup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= WaitToCheckOnStartup;
            CheckOnStartup();
        }

        private static async void CheckOnStartup()
        {
            if (IsDue())
            {
                // Failures are kept in LastError.
                await CheckNowAsync();
            }

            // Only marked once the check is over, so a script reload while it downloads checks again.
            SessionState.SetBool(StartupCheckKey, true);
        }

        /// <summary>
        /// Every release from the last successful check, newest first.
        /// </summary>
        public static List<ReleaseInfo> Releases
        {
            get
            {
                if (s_releases == null)
                {
                    s_releases = GitHubFeed.ParseReleases(LoadCache(CachePath));
                }

                return s_releases;
            }
        }

        /// <summary>
        /// The newest commits on the checkout's branch from the last successful check, newest first.
        /// </summary>
        public static List<CommitInfo> BranchCommits
        {
            get
            {
                if (s_commits == null)
                {
                    s_commits = GitHubFeed.ParseCommits(LoadCache(CommitsCachePath));
                }

                return s_commits;
            }
        }

        public static PackageInstallSource InstallSource
        {
            get
            {
                if (s_installSource == null)
                {
                    s_installSource = PackageInstallSource.Detect();
                }

                return s_installSource;
            }
        }

        /// <summary>
        /// Lets tests pretend the package was installed another way. Cleared by <see cref="Reload"/>.
        /// </summary>
        internal static void SetInstallSource(PackageInstallSource source)
        {
            s_installSource = source;
            NotifyChanged();
        }

        public static bool TryGetInstalledVersion(out SemanticVersion version)
        {
            return SemanticVersion.TryParse(Utils.PackageVersion, out version);
        }

        /// <summary>
        /// The release the Build Uploader window offers with its update button, or null.
        /// </summary>
        public static ReleaseInfo AvailableUpdate
        {
            get
            {
                RefreshNotification();
                return s_availableUpdate;
            }
        }

        /// <summary>
        /// For a git checkout: the new commits the Build Uploader window offers with its update button, or null.
        /// </summary>
        public static CommitUpdate AvailableCommits
        {
            get
            {
                RefreshNotification();
                return s_availableCommits;
            }
        }

        private static void RefreshNotification()
        {
            // OnGUI asks on every repaint, so work it out once and again only after a check or a preference change.
            // A git pull changes the package's scripts, which reloads them and starts over.
            if (s_notificationKnown)
            {
                return;
            }

            s_notificationKnown = true;
            s_availableUpdate = null;
            s_availableCommits = null;
            UpdateNotificationRules.Settings settings = UpdateNotificationRules.Settings.FromPreferences();
            if (InstallSource.ChecksCommits)
            {
                s_availableCommits = UpdateNotificationRules.CommitsToNotify(GetCommitUpdate(), settings, DateTime.UtcNow);
            }
            else if (TryGetInstalledVersion(out SemanticVersion installed))
            {
                s_availableUpdate = UpdateNotificationRules.ReleaseToNotify(installed, Releases, InstallSource, settings, DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Every release newer than the installed one that the user could update to now, newest first.
        /// </summary>
        public static List<ReleaseInfo> NewerReleases(bool ignoreAssetStoreDelay)
        {
            if (!TryGetInstalledVersion(out SemanticVersion installed))
            {
                return new List<ReleaseInfo>();
            }

            return UpdateNotificationRules.NewerReleases(installed, Releases, InstallSource, DateTime.UtcNow, ignoreAssetStoreDelay);
        }

        /// <summary>
        /// For a git checkout: how its commit compares with the newest commits on its branch, read from its .git
        /// folder once and again after a check or a preference change. Null when the package isn't a git checkout
        /// or its .git folder can't be read.
        /// </summary>
        public static CommitUpdate GetCommitUpdate()
        {
            if (!s_commitUpdateKnown)
            {
                s_commitUpdate = ReadCommitUpdate();
                s_commitUpdateKnown = true;
            }

            return s_commitUpdate;
        }

        private static CommitUpdate ReadCommitUpdate()
        {
            if (!InstallSource.ChecksCommits)
            {
                return null;
            }

            LocalGitCheckout checkout = LocalGitCheckout.Read(InstallSource.PackageFolder);
            if (checkout == null)
            {
                return null;
            }

            string branch = BranchToCompare(checkout);
            if (branch == null)
            {
                return new CommitUpdate
                {
                    State = CommitUpdateState.LocalBranch, Branch = checkout.Branch, CheckedOutBranch = checkout.Branch, YourSha = checkout.HeadSha,
                };
            }

            if (BranchCommits.Count == 0 || Preferences.UpdateCommitsBranch != branch)
            {
                return new CommitUpdate
                {
                    State = CommitUpdateState.NotChecked, Branch = branch, CheckedOutBranch = checkout.Branch, YourSha = checkout.HeadSha,
                };
            }

            return CommitUpdate.Compare(BranchCommits, checkout, branch);
        }

        private static string BranchToCompare(LocalGitCheckout checkout)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return checkout.BranchToCompare(LocalGitCheckout.SubmoduleBranch(projectRoot, InstallSource.PackageFolder));
        }

        /// <summary>
        /// Checks GitHub if <see cref="IsDue"/>. Unity opening does this; nothing else checks on its own.
        /// </summary>
        public static void CheckIfDue()
        {
            if (IsDue())
            {
                // Failures are kept in LastError.
                _ = CheckNowAsync();
            }
        }

        /// <summary>
        /// How long after a check the next one is due, or null when checks are off.
        /// </summary>
        internal static TimeSpan? CheckInterval(Preferences.UpdateCheck mode)
        {
            switch (mode)
            {
                case Preferences.UpdateCheck.Weekly:
                    return TimeSpan.FromDays(7);
                case Preferences.UpdateCheck.Monthly:
                    return TimeSpan.FromDays(30);
                default:
                    return null;
            }
        }

        /// <summary>
        /// True when checks are on and the last check is a week (or month) old, or a git checkout moved to another branch.
        /// </summary>
        private static bool IsDue()
        {
            TimeSpan? interval = CheckInterval(Preferences.CheckForUpdates);
            if (interval == null || IsChecking)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;
            if (now - Preferences.LastFailedUpdateCheckUtc < RetryAfterFailure)
            {
                return false;
            }

            bool due = now - Preferences.LastUpdateCheckUtc >= interval.Value;
            CommitUpdate commits = GetCommitUpdate();
            if (commits != null)
            {
                if (commits.State == CommitUpdateState.LocalBranch)
                {
                    // Nothing on GitHub to compare with.
                    return false;
                }

                due |= commits.State == CommitUpdateState.NotChecked;
            }

            return due;
        }

        /// <summary>
        /// Downloads the releases (or a git checkout's branch commits) now, whatever the check settings are.
        /// </summary>
        /// <returns>False if they could not be downloaded. See <see cref="LastError"/>.</returns>
        public static async Task<bool> CheckNowAsync()
        {
            if (IsChecking)
            {
                return false;
            }

            IsChecking = true;
            LastError = null;
            NotifyChanged();
            try
            {
                return InstallSource.ChecksCommits ? await CheckCommitsAsync() : await CheckReleasesAsync();
            }
            catch (Exception e)
            {
                return Failed(e.Message);
            }
            finally
            {
                IsChecking = false;
                NotifyChanged();
            }
        }

        private static async Task<bool> CheckReleasesAsync()
        {
            RequestResult result = await Feed.DownloadReleasesAsync();
            List<ReleaseInfo> releases = result.IsSuccessful ? GitHubFeed.ParseReleases(result.Data) : null;
            if (releases == null || releases.Count == 0)
            {
                return Failed(result.IsSuccessful ? "GitHub did not list any releases." : result.Data);
            }

            SaveCache(CachePath, result.Data);
            s_releases = releases;
            Preferences.LastUpdateCheckUtc = DateTime.UtcNow;
            return true;
        }

        private static async Task<bool> CheckCommitsAsync()
        {
            LocalGitCheckout checkout = LocalGitCheckout.Read(InstallSource.PackageFolder);
            if (checkout == null)
            {
                return Failed("Could not read the git checkout in " + InstallSource.PackageFolder);
            }

            string branch = BranchToCompare(checkout);
            if (branch == null)
            {
                // A branch that was never pushed: nothing to compare with, which isn't a failure.
                Preferences.LastUpdateCheckUtc = DateTime.UtcNow;
                return true;
            }

            RequestResult result = await Feed.DownloadCommitsAsync(branch);
            List<CommitInfo> commits = result.IsSuccessful ? GitHubFeed.ParseCommits(result.Data) : null;
            if (commits == null || commits.Count == 0)
            {
                string branchLabel = branch.Length == 0 ? "the default branch" : branch;
                return Failed(result.IsSuccessful ? $"GitHub did not list any commits on {branchLabel}." : result.Data);
            }

            SaveCache(CommitsCachePath, result.Data);
            s_commits = commits;
            Preferences.UpdateCommitsBranch = branch;
            Preferences.LastUpdateCheckUtc = DateTime.UtcNow;
            return true;
        }

        private static bool Failed(string error)
        {
            LastError = error;
            Preferences.LastFailedUpdateCheckUtc = DateTime.UtcNow;
            return false;
        }

        public static void RemindLater(TimeSpan delay)
        {
            Preferences.UpdateRemindAfterUtc = DateTime.UtcNow + delay;
            NotifyChanged();
        }

        public static void Skip(ReleaseInfo release)
        {
            Preferences.SkippedUpdateVersion = release.Version.ToString();
            NotifyChanged();
        }

        public static void Skip(CommitUpdate commits)
        {
            Preferences.SkippedUpdateCommit = commits.LatestSha;
            NotifyChanged();
        }

        public static void StopChecking()
        {
            Preferences.CheckForUpdates = Preferences.UpdateCheck.Never;
            NotifyChanged();
        }

        /// <summary>
        /// Call after changing an update preference so the window shows or hides its button straight away.
        /// </summary>
        public static void NotifyChanged()
        {
            s_notificationKnown = false;
            s_commitUpdateKnown = false;
            StateChanged();
        }

        /// <summary>
        /// Forgets the releases, commits and install source in memory, and reads them again when next used.
        /// </summary>
        internal static void Reload()
        {
            s_releases = null;
            s_commits = null;
            s_installSource = null;
            NotifyChanged();
        }

        private static string LoadCache(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Build Uploader could not read its update check at {path}: {e.Message}");
            }

            return "";
        }

        private static void SaveCache(string path, string json)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Build Uploader could not save its update check to {path}: {e.Message}");
            }
        }
    }
}
