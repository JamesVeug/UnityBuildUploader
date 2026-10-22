using System.IO;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Reading which commit and branch a git checkout of the package (a submodule or clone) is on, straight from its
    /// .git folder, and which GitHub branch it is compared with.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class LocalGitCheckoutTests
    {
        private const string Head = "1111111111111111111111111111111111111111";
        private const string Fetched = "2222222222222222222222222222222222222222";

        [Test]
        public void CheckoutReadsTheBranchAndItsLooseRefs()
        {
            string folder = FakeGitCheckout.OnBranch("GitLooseRefs", "develop", Head, Fetched);

            LocalGitCheckout checkout = LocalGitCheckout.Read(folder);

            Assert.AreEqual(Head, checkout.HeadSha);
            Assert.AreEqual("develop", checkout.Branch);
            Assert.AreEqual(Fetched, checkout.FetchedSha);
        }

        [Test]
        public void CheckoutReadsPackedRefs()
        {
            string folder = FakeGitCheckout.OnBranch("GitPackedRefs", "feature/updates", Head, Fetched, true);

            LocalGitCheckout checkout = LocalGitCheckout.Read(folder);

            Assert.AreEqual(Head, checkout.HeadSha);
            Assert.AreEqual("feature/updates", checkout.Branch);
            Assert.AreEqual(Fetched, checkout.FetchedSha);
        }

        [Test]
        public void CheckoutReadsADetachedHead()
        {
            LocalGitCheckout checkout = LocalGitCheckout.Read(FakeGitCheckout.Detached("GitDetached", Head));

            Assert.AreEqual(Head, checkout.HeadSha);
            Assert.IsNull(checkout.Branch);
            Assert.IsNull(checkout.FetchedSha);
        }

        [Test]
        public void CheckoutFollowsASubmodulesGitFile()
        {
            LocalGitCheckout checkout = LocalGitCheckout.Read(FakeGitCheckout.Submodule("GitSubmodule", Head));

            Assert.IsNotNull(checkout, "A submodule's .git is a file pointing at .git/modules in the parent repository.");
            Assert.AreEqual(Head, checkout.HeadSha);
            Assert.IsNull(checkout.Branch);
        }

        [Test]
        public void FolderWithoutGitIsNotACheckout()
        {
            Assert.IsNull(LocalGitCheckout.Read(TestSupport.OutputDirectory("GitNone")));
            Assert.IsNull(LocalGitCheckout.Read(""));

            string empty = TestSupport.OutputDirectory("GitEmptyHead");
            Directory.CreateDirectory(Path.Combine(empty, ".git"));
            File.WriteAllText(Path.Combine(empty, ".git", "HEAD"), "ref: refs/heads/main\n");
            Assert.IsNull(LocalGitCheckout.Read(empty), "A new repository with no commits has nothing to compare.");
        }

        [Test]
        public void CheckoutIsComparedWithItsOwnOrItsSubmodulesBranch()
        {
            LocalGitCheckout onPushedBranch = new LocalGitCheckout { HeadSha = Head, Branch = "develop", FetchedSha = Fetched };
            LocalGitCheckout onLocalBranch = new LocalGitCheckout { HeadSha = Head, Branch = "my-work", FetchedSha = null };
            LocalGitCheckout detached = new LocalGitCheckout { HeadSha = Head };

            Assert.AreEqual("develop", onPushedBranch.BranchToCompare("main"));
            Assert.IsNull(onLocalBranch.BranchToCompare("main"), "A branch origin doesn't have has nothing to compare with.");
            Assert.AreEqual("main", detached.BranchToCompare("main"), "Detached submodules use the branch .gitmodules sets.");
            Assert.AreEqual("", detached.BranchToCompare(null), "Otherwise GitHub's default branch.");
        }

        [Test]
        public void SubmoduleBranchComesFromGitmodules()
        {
            string root = TestSupport.OutputDirectory("GitModules");
            File.WriteAllText(Path.Combine(root, ".gitmodules"),
                "[submodule \"Packages/other\"]\n\tpath = Packages/other\n\tbranch = main\n" +
                "[submodule \"Packages/com.veugeljame.builduploader\"]\n\tbranch = develop\n\tpath = Packages/com.veugeljame.builduploader\n" +
                "\turl = https://github.com/JamesVeug/UnitySteamBuildUploader.git\n" +
                "[submodule \"Packages/no-branch\"]\n\tpath = Packages/no-branch\n");

            Assert.AreEqual("develop", LocalGitCheckout.SubmoduleBranch(root, Path.Combine(root, "Packages", "com.veugeljame.builduploader")));
            Assert.AreEqual("main", LocalGitCheckout.SubmoduleBranch(root, Path.Combine(root, "Packages", "other")));
            Assert.IsNull(LocalGitCheckout.SubmoduleBranch(root, Path.Combine(root, "Packages", "no-branch")));
            Assert.IsNull(LocalGitCheckout.SubmoduleBranch(root, Path.Combine(root, "Packages", "missing")));
            Assert.IsNull(LocalGitCheckout.SubmoduleBranch(TestSupport.OutputDirectory("GitNoModules"), root));
        }
    }
}
