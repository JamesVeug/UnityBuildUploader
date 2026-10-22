using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// How the Build Uploader was added to a project, and what its update button asks Package Manager for.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class PackageInstallSourceTests
    {
        private const string Name = "com.veugeljame.builduploader@";
        private const string GitUrl = "https://github.com/JamesVeug/UnitySteamBuildUploader.git";

        private static readonly ReleaseInfo Release = new ReleaseInfo
        {
            Tag = "v3.4.0",
            Version = new SemanticVersion(3, 4, 0),
        };

        private static PackageInstallSource Git(string packageId)
        {
            return PackageInstallSource.FromPackageInfo(PackageSource.Git, packageId, false, false);
        }

        [Test]
        public void GitTagInstallUpdatesToTheNewTag()
        {
            PackageInstallSource source = Git(Name + GitUrl + "#v3.3.0");

            Assert.AreEqual(PackageInstallKind.GitPinned, source.Kind);
            Assert.AreEqual(GitUrl, source.GitUrl);
            Assert.AreEqual("v3.3.0", source.GitRevision);
            Assert.AreEqual(GitUrl + "#v3.4.0", source.PackageManagerIdentifier(Release), "Keeps the URL the user added, even the old repository name.");
            Assert.IsFalse(source.FollowsBranch);
            Assert.IsTrue(source.NotifiesAboutReleases);
            Assert.IsFalse(source.WaitsForAssetStore);
        }

        [Test]
        public void GitCommitInstallUpdatesToTheNewTag()
        {
            PackageInstallSource source = Git(Name + GitUrl + "#e1dfc2a");

            Assert.AreEqual(PackageInstallKind.GitPinned, source.Kind);
            Assert.AreEqual(GitUrl + "#v3.4.0", source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void GitDevelopInstallStaysOnDevelop()
        {
            PackageInstallSource source = Git(Name + GitUrl + "#develop");

            Assert.AreEqual(PackageInstallKind.GitBranch, source.Kind);
            Assert.IsTrue(source.FollowsBranch, "Branch followers get a different update icon.");
            Assert.IsTrue(source.NotifiesAboutReleases);
            Assert.AreEqual(GitUrl + "#develop", source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void GitInstallWithoutRevisionIsAddedAgain()
        {
            PackageInstallSource source = Git(Name + GitUrl);

            Assert.AreEqual(PackageInstallKind.GitDefaultBranch, source.Kind);
            Assert.IsFalse(source.FollowsBranch);
            Assert.AreEqual(GitUrl, source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void GitInstallKeepsItsPathQuery()
        {
            PackageInstallSource source = Git(Name + "https://github.com/someone/monorepo.git?path=/Packages/builduploader#v3.3.0");

            Assert.AreEqual("https://github.com/someone/monorepo.git?path=/Packages/builduploader#v3.4.0", source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void SshGitInstallKeepsItsUrl()
        {
            PackageInstallSource source = Git(Name + "git@github.com:JamesVeug/UnityBuildUploader.git#v3.3.0");

            Assert.AreEqual(PackageInstallKind.GitPinned, source.Kind);
            Assert.AreEqual("git@github.com:JamesVeug/UnityBuildUploader.git#v3.4.0", source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void EmbeddedCopyWithGitIsAGitCheckout()
        {
            PackageInstallSource source = PackageInstallSource.FromPackageInfo(PackageSource.Embedded, Name + "3.3.0", true, false,
                "C:/Project/Packages/com.veugeljame.builduploader");

            Assert.AreEqual(PackageInstallKind.GitCheckout, source.Kind);
            Assert.IsTrue(source.ChecksCommits, "Submodules and clones compare commits.");
            Assert.IsFalse(source.NotifiesAboutReleases);
            Assert.AreEqual("C:/Project/Packages/com.veugeljame.builduploader", source.PackageFolder);
            Assert.IsNull(source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void EmbeddedCopyWithoutGitCameFromAUnityPackage()
        {
            PackageInstallSource fromAssetStore = PackageInstallSource.FromPackageInfo(PackageSource.Embedded, Name + "3.3.0", false, true);
            PackageInstallSource fromGitHub = PackageInstallSource.FromPackageInfo(PackageSource.Embedded, Name + "3.3.0", false, false);

            Assert.AreEqual(PackageInstallKind.UnityPackage, fromAssetStore.Kind);
            Assert.IsTrue(fromAssetStore.FromAssetStore);
            Assert.IsTrue(fromAssetStore.WaitsForAssetStore);
            Assert.IsTrue(fromAssetStore.NotifiesAboutReleases);
            Assert.IsNull(fromAssetStore.PackageManagerIdentifier(Release), "Package Manager can't update copied files.");

            Assert.AreEqual(PackageInstallKind.UnityPackage, fromGitHub.Kind);
            Assert.IsFalse(fromGitHub.FromAssetStore);
            Assert.IsTrue(fromGitHub.WaitsForAssetStore, "A GitHub .unitypackage looks the same as an Asset Store one.");
        }

        [Test]
        public void LocalFolderIsALocalCopyUnlessItIsAClone()
        {
            PackageInstallSource folder = PackageInstallSource.FromPackageInfo(PackageSource.Local, Name + "file:../../builduploader", false, false);
            Assert.AreEqual(PackageInstallKind.LocalCopy, folder.Kind);
            Assert.IsFalse(folder.NotifiesAboutReleases);
            Assert.IsFalse(folder.ChecksCommits);

            Assert.AreEqual(PackageInstallKind.GitCheckout,
                PackageInstallSource.FromPackageInfo(PackageSource.Local, Name + "file:../../builduploader", true, false).Kind);
            Assert.AreEqual(PackageInstallKind.LocalCopy,
                PackageInstallSource.FromPackageInfo(PackageSource.LocalTarball, Name + "file:builduploader.tgz", false, false).Kind);
        }

        [Test]
        public void RegistryInstallUpdatesByVersion()
        {
            PackageInstallSource source = PackageInstallSource.FromPackageInfo(PackageSource.Registry, Name + "3.3.0", false, false);

            Assert.AreEqual(PackageInstallKind.Registry, source.Kind);
            Assert.AreEqual("com.veugeljame.builduploader@3.4.0", source.PackageManagerIdentifier(Release));
        }

        [Test]
        public void ThisProjectsInstallIsDetected()
        {
            PackageInstallSource source = PackageInstallSource.Detect();

            // The host project embeds the package: a submodule locally, a plain copy in CI test projects.
            Assert.IsTrue(source.Kind == PackageInstallKind.GitCheckout || source.Kind == PackageInstallKind.UnityPackage,
                "Detected " + source.Kind);
            Assert.IsTrue(Directory.Exists(source.PackageFolder), source.PackageFolder);
        }
    }
}
