using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Wireframe
{
    internal enum PackageInstallKind
    {
        Unknown,

        /// <summary>
        /// Git URL fixed to a tag or commit: ...git#v3.3.0
        /// </summary>
        GitPinned,

        /// <summary>
        /// Git URL following a branch: ...git#develop
        /// </summary>
        GitBranch,

        /// <summary>
        /// Git URL with no revision, so it follows the repository's default branch.
        /// </summary>
        GitDefaultBranch,

        /// <summary>
        /// Files imported from a .unitypackage: the Asset Store or a GitHub release.
        /// </summary>
        UnityPackage,

        /// <summary>
        /// A folder or tarball on disk without git. Whoever owns it updates it, so it is never checked.
        /// </summary>
        LocalCopy,

        /// <summary>
        /// A submodule or clone (the package folder has .git). Checked by commit rather than release.
        /// </summary>
        GitCheckout,

        /// <summary>
        /// A package registry such as OpenUPM.
        /// </summary>
        Registry,
    }

    /// <summary>
    /// How the Build Uploader was added to this project, which decides how it can be updated.
    /// </summary>
    internal class PackageInstallSource
    {
        public const string PackageName = "com.veugeljame.builduploader";
        public const string AssetStoreUrl = "https://assetstore.unity.com/packages/tools/utilities/build-uploader-306907";
        public const string AssetStoreLink = "com.unity3d.kharma:content/306907";
        private const string AssetStorePublisher = "JamesGamesNZ";

        public PackageInstallKind Kind { get; }

        /// <summary>
        /// The Git URL without its #revision.
        /// </summary>
        public string GitUrl { get; }

        /// <summary>
        /// The tag, commit or branch after # in the Git URL.
        /// </summary>
        public string GitRevision { get; }

        /// <summary>
        /// The Asset Store download cache holds the Build Uploader, so a .unitypackage install most likely came from there
        /// and updates through the Asset Store rather than GitHub.
        /// </summary>
        public bool FromAssetStore { get; }

        /// <summary>
        /// The full path of the package folder, or "" when not known.
        /// </summary>
        public string PackageFolder { get; }

        public bool IsGit => Kind == PackageInstallKind.GitPinned || Kind == PackageInstallKind.GitBranch ||
                             Kind == PackageInstallKind.GitDefaultBranch;
        public bool FollowsBranch => Kind == PackageInstallKind.GitBranch;
        public bool ChecksCommits => Kind == PackageInstallKind.GitCheckout;
        public bool NotifiesAboutReleases => Kind != PackageInstallKind.LocalCopy && Kind != PackageInstallKind.Unknown &&
                                             Kind != PackageInstallKind.GitCheckout;

        /// <summary>
        /// The Asset Store reviews each version, so it gets releases days after GitHub does.
        /// GitHub .unitypackage installs look the same, so they wait too.
        /// </summary>
        public bool WaitsForAssetStore => Kind == PackageInstallKind.UnityPackage;

        public PackageInstallSource(PackageInstallKind kind, string gitUrl = "", string gitRevision = "", bool fromAssetStore = false,
            string packageFolder = "")
        {
            Kind = kind;
            GitUrl = gitUrl ?? "";
            GitRevision = gitRevision ?? "";
            FromAssetStore = fromAssetStore;
            PackageFolder = packageFolder ?? "";
        }

        public static PackageInstallSource Detect()
        {
            UnityEditor.PackageManager.PackageInfo info = null;
            try
            {
                info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PackageInstallSource).Assembly);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Build Uploader could not find how it was installed: " + e.Message);
            }

            if (info == null)
            {
                // Copied into Assets by an older .unitypackage.
                return new PackageInstallSource(PackageInstallKind.UnityPackage, fromAssetStore: HasAssetStoreDownload(),
                    packageFolder: Path.GetFullPath(Utils.s_packagePath));
            }

            bool hasGitFolder = !string.IsNullOrEmpty(info.resolvedPath) && Utils.PathExists(Path.Combine(info.resolvedPath, ".git"));
            bool fromAssetStore = info.source == PackageSource.Embedded && !hasGitFolder && HasAssetStoreDownload();
            return FromPackageInfo(info.source, info.packageId, hasGitFolder, fromAssetStore, info.resolvedPath);
        }

        /// <param name="packageId">name@version, or name@url for Git packages</param>
        /// <param name="hasGitFolder">The package folder has .git, so it is a clone or submodule</param>
        internal static PackageInstallSource FromPackageInfo(PackageSource source, string packageId, bool hasGitFolder, bool fromAssetStore,
            string packageFolder = "")
        {
            switch (source)
            {
                case PackageSource.Git:
                    string url = packageId ?? "";
                    int at = url.IndexOf('@');
                    if (at >= 0)
                    {
                        url = url.Substring(at + 1);
                    }

                    string revision = "";
                    int hash = url.IndexOf('#');
                    if (hash >= 0)
                    {
                        revision = url.Substring(hash + 1);
                        url = url.Substring(0, hash);
                    }

                    PackageInstallKind kind;
                    if (revision.Length == 0)
                    {
                        kind = PackageInstallKind.GitDefaultBranch;
                    }
                    else if (IsPinnedRevision(revision))
                    {
                        kind = PackageInstallKind.GitPinned;
                    }
                    else
                    {
                        kind = PackageInstallKind.GitBranch;
                    }

                    return new PackageInstallSource(kind, url, revision, packageFolder: packageFolder);
                case PackageSource.Embedded:
                    return hasGitFolder
                        ? new PackageInstallSource(PackageInstallKind.GitCheckout, packageFolder: packageFolder)
                        : new PackageInstallSource(PackageInstallKind.UnityPackage, fromAssetStore: fromAssetStore, packageFolder: packageFolder);
                case PackageSource.Local:
                    return new PackageInstallSource(hasGitFolder ? PackageInstallKind.GitCheckout : PackageInstallKind.LocalCopy,
                        packageFolder: packageFolder);
                case PackageSource.LocalTarball:
                    return new PackageInstallSource(PackageInstallKind.LocalCopy, packageFolder: packageFolder);
                case PackageSource.Registry:
                    return new PackageInstallSource(PackageInstallKind.Registry, packageFolder: packageFolder);
                default:
                    return new PackageInstallSource(PackageInstallKind.Unknown, packageFolder: packageFolder);
            }
        }

        /// <summary>
        /// A tag (v3.3.0) or a commit hash. Anything else is a branch.
        /// </summary>
        private static bool IsPinnedRevision(string revision)
        {
            if (SemanticVersion.TryParse(revision, out _))
            {
                return true;
            }

            if (revision.Length < 7 || revision.Length > 40)
            {
                return false;
            }

            foreach (char c in revision)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// What to pass to Package Manager's Client.Add to update to the release, or null when Package Manager
        /// can't update this install.
        /// </summary>
        public string PackageManagerIdentifier(ReleaseInfo release)
        {
            switch (Kind)
            {
                case PackageInstallKind.GitPinned:
                    return GitUrl + "#" + release.Tag;
                case PackageInstallKind.GitBranch:
                    // Fetches the newest commit on their branch so they stay on it.
                    return GitUrl + "#" + GitRevision;
                case PackageInstallKind.GitDefaultBranch:
                    return GitUrl;
                case PackageInstallKind.Registry:
                    return PackageName + "@" + release.Version;
                default:
                    return null;
            }
        }

        public string Describe()
        {
            switch (Kind)
            {
                case PackageInstallKind.GitPinned:
                case PackageInstallKind.GitBranch:
                    return $"Git (#{GitRevision})";
                case PackageInstallKind.GitDefaultBranch:
                    return "Git";
                case PackageInstallKind.UnityPackage:
                    return FromAssetStore ? "Asset Store" : ".unitypackage (Asset Store or GitHub)";
                case PackageInstallKind.GitCheckout:
                    return "Git checkout (submodule or clone)";
                case PackageInstallKind.LocalCopy:
                    return "Local folder";
                case PackageInstallKind.Registry:
                    return "Package registry";
                default:
                    return "Unknown";
            }
        }

        private static bool HasAssetStoreDownload()
        {
            return FindAssetStoreDownload() != null;
        }

        /// <summary>
        /// The newest Build Uploader .unitypackage the Asset Store downloaded, or null.
        /// </summary>
        public static string FindAssetStoreDownload()
        {
            string newest = null;
            DateTime newestTime = DateTime.MinValue;
            try
            {
                foreach (string root in AssetStoreCacheFolders())
                {
                    string publisherFolder = Path.Combine(root, AssetStorePublisher);
                    if (!Directory.Exists(publisherFolder))
                    {
                        continue;
                    }

                    foreach (string file in Directory.GetFiles(publisherFolder, "Build Uploader*.unitypackage", SearchOption.AllDirectories))
                    {
                        DateTime written = File.GetLastWriteTimeUtc(file);
                        if (written > newestTime)
                        {
                            newest = file;
                            newestTime = written;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable cache folders only mean we can't tell where the package came from.
            }

            return newest;
        }

        private static IEnumerable<string> AssetStoreCacheFolders()
        {
            string customFolder = Environment.GetEnvironmentVariable("ASSETSTORE_CACHE_PATH");
            if (!string.IsNullOrEmpty(customFolder))
            {
                yield return customFolder;
            }

#if UNITY_EDITOR_WIN
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unity", "Asset Store-5.x");
#elif UNITY_EDITOR_OSX
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Unity", "Asset Store-5.x");
#else
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share", "unity3d", "Asset Store-5.x");
#endif
        }
    }
}
