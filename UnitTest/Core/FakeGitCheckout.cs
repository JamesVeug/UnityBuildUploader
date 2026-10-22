using System.IO;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Folders with just enough of a .git folder for <see cref="LocalGitCheckout"/> to read: HEAD, branch refs
    /// (loose or packed) and origin's copy of the branch. Nothing runs git.
    /// </summary>
    internal static class FakeGitCheckout
    {
        /// <param name="fetchedSha">origin's copy of the branch, or null when origin doesn't have it.</param>
        /// <param name="packed">Keep the refs in packed-refs like a fresh clone does.</param>
        public static string OnBranch(string name, string branch, string sha, string fetchedSha, bool packed = false)
        {
            string folder = TestSupport.OutputDirectory(name);
            string gitDir = Path.Combine(folder, ".git");
            Directory.CreateDirectory(gitDir);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/" + branch + "\n");
            if (packed)
            {
                string packedRefs = "# pack-refs with: peeled fully-peeled sorted \n" + sha + " refs/heads/" + branch + "\n";
                if (fetchedSha != null)
                {
                    packedRefs += fetchedSha + " refs/remotes/origin/" + branch + "\n";
                }

                File.WriteAllText(Path.Combine(gitDir, "packed-refs"), packedRefs);
            }
            else
            {
                SetRef(gitDir, "refs/heads/" + branch, sha);
                if (fetchedSha != null)
                {
                    SetRef(gitDir, "refs/remotes/origin/" + branch, fetchedSha);
                }
            }

            return folder;
        }

        public static string Detached(string name, string sha)
        {
            string folder = TestSupport.OutputDirectory(name);
            string gitDir = Path.Combine(folder, ".git");
            Directory.CreateDirectory(gitDir);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), sha + "\n");
            return folder;
        }

        /// <summary>
        /// Like a submodule: the folder's .git is a file pointing at the git folder in the parent's .git/modules.
        /// </summary>
        public static string Submodule(string name, string sha)
        {
            string root = TestSupport.OutputDirectory(name);
            string gitDir = Path.Combine(root, ".git", "modules", "Packages", "builduploader");
            string folder = Path.Combine(root, "Packages", "builduploader");
            Directory.CreateDirectory(gitDir);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), sha + "\n");
            File.WriteAllText(Path.Combine(folder, ".git"), "gitdir: ../../.git/modules/Packages/builduploader\n");
            return folder;
        }

        /// <summary>
        /// Moves the checked out branch, as a pull does.
        /// </summary>
        public static void MoveBranch(string folder, string branch, string sha)
        {
            SetRef(Path.Combine(folder, ".git"), "refs/heads/" + branch, sha);
        }

        private static void SetRef(string gitDir, string refName, string sha)
        {
            string path = Path.Combine(gitDir, refName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sha + "\n");
        }
    }
}
