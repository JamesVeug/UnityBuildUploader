using System;
using System.Collections.Generic;

namespace Wireframe
{
    /// <summary>
    /// Decides which releases a user is told about. Has no state so the rules can be tested on their own.
    /// </summary>
    internal static class UpdateNotificationRules
    {
        /// <summary>
        /// How long installs that may have come from the Asset Store wait after a GitHub release, while the
        /// Asset Store reviews the new version.
        /// </summary>
        public static readonly TimeSpan AssetStoreDelay = TimeSpan.FromDays(3);

        internal struct Settings
        {
            public Preferences.UpdateCheck Mode;
            public string SkippedVersion;
            public string SkippedCommit;
            public DateTime RemindAfterUtc;

            public static Settings FromPreferences()
            {
                return new Settings
                {
                    Mode = Preferences.CheckForUpdates,
                    SkippedVersion = Preferences.SkippedUpdateVersion,
                    SkippedCommit = Preferences.SkippedUpdateCommit,
                    RemindAfterUtc = Preferences.UpdateRemindAfterUtc,
                };
            }
        }

        /// <summary>
        /// The commits the Build Uploader window's update button offers to a git checkout, or null when it is
        /// up to date, can't be compared, or the user asked not to be told.
        /// </summary>
        public static CommitUpdate CommitsToNotify(CommitUpdate update, Settings settings, DateTime utcNow)
        {
            if (update == null || update.State != CommitUpdateState.Behind || settings.Mode == Preferences.UpdateCheck.Never ||
                utcNow < settings.RemindAfterUtc)
            {
                return null;
            }

            // Skipping hides these commits until a newer one comes out.
            if (!string.IsNullOrEmpty(settings.SkippedCommit) &&
                string.Equals(update.LatestSha, settings.SkippedCommit, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return update;
        }

        /// <summary>
        /// Releases newer than the installed version, newest first. Pre-releases are left out.
        /// </summary>
        /// <param name="ignoreAssetStoreDelay">True when the user asked to check, so they see everything on GitHub.</param>
        public static List<ReleaseInfo> NewerReleases(SemanticVersion installed, IEnumerable<ReleaseInfo> releases,
            PackageInstallSource source, DateTime utcNow, bool ignoreAssetStoreDelay)
        {
            List<ReleaseInfo> newer = new List<ReleaseInfo>();
            if (releases == null)
            {
                return newer;
            }

            foreach (ReleaseInfo release in releases)
            {
                if (release.IsPreRelease || release.Version <= installed)
                {
                    continue;
                }

                if (!ignoreAssetStoreDelay && IsWaitingForAssetStore(release, source, utcNow))
                {
                    continue;
                }

                newer.Add(release);
            }

            newer.Sort((a, b) => b.Version.CompareTo(a.Version));
            return newer;
        }

        public static bool IsWaitingForAssetStore(ReleaseInfo release, PackageInstallSource source, DateTime utcNow)
        {
            return source.WaitsForAssetStore && utcNow - release.PublishedUtc < AssetStoreDelay;
        }

        /// <summary>
        /// The release the Build Uploader window's update button offers, or null when there is none or the user
        /// asked not to be told about it.
        /// </summary>
        public static ReleaseInfo ReleaseToNotify(SemanticVersion installed, IEnumerable<ReleaseInfo> releases,
            PackageInstallSource source, Settings settings, DateTime utcNow)
        {
            if (settings.Mode == Preferences.UpdateCheck.Never || !source.NotifiesAboutReleases || utcNow < settings.RemindAfterUtc)
            {
                return null;
            }

            List<ReleaseInfo> newer = NewerReleases(installed, releases, source, utcNow, false);
            if (newer.Count == 0)
            {
                return null;
            }

            ReleaseInfo latest = newer[0];
            if (SemanticVersion.TryParse(settings.SkippedVersion, out SemanticVersion skipped) && latest.Version <= skipped)
            {
                return null;
            }

            return latest;
        }
    }
}
