using System;
using System.IO;

namespace Wireframe
{
    /// <summary>
    /// The commit a git checkout of the package is on, read straight from its .git folder so git doesn't need
    /// to be installed and the repository is never changed.
    /// </summary>
    internal class LocalGitCheckout
    {
        /// <summary>
        /// The commit checked out.
        /// </summary>
        public string HeadSha = "";

        /// <summary>
        /// The branch checked out, or null when HEAD is detached (usual for submodules).
        /// </summary>
        public string Branch;

        /// <summary>
        /// origin's copy of <see cref="Branch"/> from the last fetch, or null when origin doesn't have it.
        /// </summary>
        public string FetchedSha;

        /// <summary>
        /// Null when the folder isn't a git checkout or its HEAD can't be read.
        /// </summary>
        public static LocalGitCheckout Read(string folder)
        {
            try
            {
                string gitDir = FindGitDir(folder);
                if (gitDir == null)
                {
                    return null;
                }

                string commonDir = gitDir;
                string commonDirFile = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonDirFile))
                {
                    // Worktrees keep their refs in the main repository.
                    commonDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonDirFile).Trim()));
                }

                string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                LocalGitCheckout checkout = new LocalGitCheckout();
                if (head.StartsWith("ref:"))
                {
                    string refName = head.Substring(4).Trim();
                    checkout.HeadSha = ResolveRef(gitDir, commonDir, refName);
                    if (refName.StartsWith("refs/heads/"))
                    {
                        checkout.Branch = refName.Substring("refs/heads/".Length);
                        checkout.FetchedSha = ResolveRef(gitDir, commonDir, "refs/remotes/origin/" + checkout.Branch);
                    }
                }
                else if (IsSha(head))
                {
                    checkout.HeadSha = head;
                }

                return string.IsNullOrEmpty(checkout.HeadSha) ? null : checkout;
            }
            catch (Exception)
            {
                // A half-written or unusual .git folder only means updates can't be checked.
                return null;
            }
        }

        /// <summary>
        /// The GitHub branch to compare with: the checkout's own branch, the submodule's configured branch when
        /// HEAD is detached, or "" for GitHub's default branch. Null when the checkout is on a branch origin
        /// doesn't have, so there is nothing to compare with.
        /// </summary>
        public string BranchToCompare(string submoduleBranch)
        {
            if (Branch != null)
            {
                return FetchedSha != null ? Branch : null;
            }

            return submoduleBranch ?? "";
        }

        /// <summary>
        /// The branch .gitmodules sets for the submodule at packageFolder, or null.
        /// </summary>
        public static string SubmoduleBranch(string projectRoot, string packageFolder)
        {
            try
            {
                string gitmodules = Path.Combine(projectRoot, ".gitmodules");
                if (!File.Exists(gitmodules))
                {
                    return null;
                }

                string relativeFolder = Normalize(GetRelativePath(projectRoot, packageFolder));
                bool inMatchingSection = false;
                string path = null;
                string branch = null;
                foreach (string rawLine in File.ReadAllLines(gitmodules))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("["))
                    {
                        if (inMatchingSection && path != null)
                        {
                            break;
                        }

                        path = null;
                        branch = null;
                        inMatchingSection = false;
                        continue;
                    }

                    int equals = line.IndexOf('=');
                    if (equals < 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key == "path")
                    {
                        path = value;
                        inMatchingSection = Normalize(value) == relativeFolder;
                    }
                    else if (key == "branch")
                    {
                        branch = value;
                    }

                    if (inMatchingSection && branch != null)
                    {
                        return branch;
                    }
                }

                return inMatchingSection ? branch : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FindGitDir(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            string git = Path.Combine(folder, ".git");
            if (Directory.Exists(git))
            {
                return git;
            }

            if (!File.Exists(git))
            {
                return null;
            }

            // Submodules and worktrees have a .git file pointing at the real git folder.
            string text = File.ReadAllText(git).Trim();
            if (!text.StartsWith("gitdir:"))
            {
                return null;
            }

            string gitDir = Path.GetFullPath(Path.Combine(folder, text.Substring("gitdir:".Length).Trim()));
            return Directory.Exists(gitDir) ? gitDir : null;
        }

        private static string ResolveRef(string gitDir, string commonDir, string refName)
        {
            foreach (string dir in new[] { gitDir, commonDir })
            {
                string refFile = Path.Combine(dir, refName);
                if (File.Exists(refFile))
                {
                    string sha = File.ReadAllText(refFile).Trim();
                    if (IsSha(sha))
                    {
                        return sha;
                    }
                }
            }

            string packedRefs = Path.Combine(commonDir, "packed-refs");
            if (!File.Exists(packedRefs))
            {
                return null;
            }

            foreach (string line in File.ReadAllLines(packedRefs))
            {
                // "<sha> <ref>", plus "# comments" and "^<sha>" lines for annotated tags.
                int space = line.IndexOf(' ');
                if (space == 40 && line.Substring(space + 1).Trim() == refName && IsSha(line.Substring(0, space)))
                {
                    return line.Substring(0, space);
                }
            }

            return null;
        }

        private static bool IsSha(string text)
        {
            if (text == null || text.Length != 40)
            {
                return false;
            }

            foreach (char c in text)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetRelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(fullRoot.Length) : fullPath;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }
    }
}
