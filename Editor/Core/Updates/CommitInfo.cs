using System;
using System.Collections.Generic;

namespace Wireframe
{
    /// <summary>
    /// One commit on GitHub.
    /// </summary>
    internal class CommitInfo
    {
        public string Sha = "";
        public string Message = "";
        public string Author = "";
        public DateTime DateUtc;
        public string Url = "";

        public string ShortSha => ShortenSha(Sha);

        /// <summary>
        /// The first line of the message.
        /// </summary>
        public string Subject
        {
            get
            {
                int newLine = Message.IndexOf('\n');
                return (newLine >= 0 ? Message.Substring(0, newLine) : Message).Trim();
            }
        }

        /// <summary>
        /// The message after its first line, or "".
        /// </summary>
        public string Body
        {
            get
            {
                int newLine = Message.IndexOf('\n');
                return newLine >= 0 ? Message.Substring(newLine + 1).Trim() : "";
            }
        }

        public static string ShortenSha(string sha)
        {
            return sha != null && sha.Length > 7 ? sha.Substring(0, 7) : sha ?? "";
        }
    }

    internal enum CommitUpdateState
    {
        /// <summary>
        /// GitHub hasn't been asked about this branch yet.
        /// </summary>
        NotChecked,

        /// <summary>
        /// The checkout is on a branch that isn't on GitHub, so there is nothing to compare with.
        /// </summary>
        LocalBranch,

        /// <summary>
        /// The commit isn't in the newest commits on GitHub: it only exists locally, or is very far behind.
        /// </summary>
        NotFound,
        UpToDate,
        Behind,
    }

    /// <summary>
    /// Where a git checkout of the package (a submodule or clone) stands against the newest commits on its
    /// branch on GitHub.
    /// </summary>
    internal class CommitUpdate
    {
        public CommitUpdateState State;

        /// <summary>
        /// The GitHub branch compared with, or "" for the repository's default branch.
        /// </summary>
        public string Branch = "";

        /// <summary>
        /// The branch checked out, or null when HEAD is detached (usual for submodules).
        /// </summary>
        public string CheckedOutBranch;

        /// <summary>
        /// The commit compared: HEAD, or the last fetched branch when HEAD has commits GitHub doesn't.
        /// </summary>
        public string YourSha = "";

        /// <summary>
        /// HEAD has commits that aren't on GitHub, so <see cref="YourSha"/> is the branch as it was last fetched.
        /// </summary>
        public bool ComparedWithFetched;

        /// <summary>
        /// Commits on GitHub that the checkout doesn't have, newest first.
        /// </summary>
        public List<CommitInfo> NewCommits = new List<CommitInfo>();

        public string LatestSha => NewCommits.Count > 0 ? NewCommits[0].Sha : YourSha;
        public string BranchLabel => string.IsNullOrEmpty(Branch) ? "the default branch" : Branch;

        /// <param name="branchCommits">The newest commits on the branch on GitHub, newest first.</param>
        /// <param name="branch">The branch they came from, or "" for the default branch.</param>
        public static CommitUpdate Compare(List<CommitInfo> branchCommits, LocalGitCheckout checkout, string branch)
        {
            CommitUpdate update = new CommitUpdate
            {
                Branch = branch ?? "",
                CheckedOutBranch = checkout.Branch,
                YourSha = checkout.HeadSha,
            };

            int index = IndexOf(branchCommits, checkout.HeadSha);
            if (index < 0 && !string.IsNullOrEmpty(checkout.FetchedSha))
            {
                index = IndexOf(branchCommits, checkout.FetchedSha);
                if (index >= 0)
                {
                    update.YourSha = checkout.FetchedSha;
                    update.ComparedWithFetched = true;
                }
            }

            if (index < 0)
            {
                update.State = CommitUpdateState.NotFound;
                return update;
            }

            update.NewCommits = branchCommits.GetRange(0, index);
            update.State = index == 0 ? CommitUpdateState.UpToDate : CommitUpdateState.Behind;
            return update;
        }

        private static int IndexOf(List<CommitInfo> commits, string sha)
        {
            if (string.IsNullOrEmpty(sha) || commits == null)
            {
                return -1;
            }

            return commits.FindIndex(a => string.Equals(a.Sha, sha, StringComparison.OrdinalIgnoreCase));
        }
    }
}
