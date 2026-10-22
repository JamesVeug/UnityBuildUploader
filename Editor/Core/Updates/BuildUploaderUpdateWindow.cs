using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// What changed in the newer releases (or, for a git checkout, the new commits), a button to update and ways to
    /// stop being told about them.
    /// Opens under the Build Uploader window's update button, or from Window->Build Uploader->Check for Updates.
    /// </summary>
    internal class BuildUploaderUpdateWindow : EditorWindow
    {
        private static readonly Vector2 DropDownSize = new Vector2(480, 540);

        /// <summary>
        /// Opened from Check for Updates, so show every newer release on GitHub even when it is skipped or
        /// not on the Asset Store yet.
        /// </summary>
        [SerializeField] private bool m_userChecked;

        private Vector2 m_scrollPosition;
        private readonly Dictionary<string, bool> m_foldouts = new Dictionary<string, bool>();

        private GUIStyle m_titleStyle;
        private GUIStyle m_wrappedStyle;
        private GUIStyle m_releaseFoldoutStyle;
        private GUIStyle m_sectionFoldoutStyle;
        private GUIStyle m_primaryButtonStyle;
        private GUIStyle m_commitSubjectStyle;
        private GUIStyle m_commitBodyStyle;

        [MenuItem("Window/Build Uploader/Check for Updates")]
        public static void CheckForUpdates()
        {
            BuildUploaderUpdateWindow window = GetWindow<BuildUploaderUpdateWindow>(true, "Build Uploader Updates", true);
            window.m_userChecked = true;
            window.minSize = new Vector2(420, 300);
            window.Show();

            // Failures are kept in UpdateChecker.LastError and shown by the window.
            _ = UpdateChecker.CheckNowAsync();
        }

        public static void ShowDropDown(Rect buttonScreenRect)
        {
            BuildUploaderUpdateWindow window = CreateInstance<BuildUploaderUpdateWindow>();
            window.titleContent = new GUIContent("Build Uploader Updates");
            window.m_userChecked = false;
            window.ShowAsDropDown(buttonScreenRect, DropDownSize);
        }

        private void OnEnable()
        {
            UpdateChecker.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            UpdateChecker.StateChanged -= Repaint;
        }

        private void CreateStyles()
        {
            if (m_titleStyle != null)
            {
                return;
            }

            m_titleStyle = new GUIStyle(EditorStyles.boldLabel);
            m_titleStyle.fontSize = 16;
            m_titleStyle.wordWrap = true;

            m_wrappedStyle = new GUIStyle(EditorStyles.label);
            m_wrappedStyle.wordWrap = true;

            m_releaseFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            m_releaseFoldoutStyle.fontStyle = FontStyle.Bold;
            m_releaseFoldoutStyle.fontSize = 13;

            m_sectionFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            m_sectionFoldoutStyle.fontStyle = FontStyle.Bold;

            m_primaryButtonStyle = new GUIStyle(GUI.skin.button);
            m_primaryButtonStyle.fontStyle = FontStyle.Bold;

            m_commitSubjectStyle = new GUIStyle(EditorStyles.boldLabel);
            m_commitSubjectStyle.wordWrap = true;

            m_commitBodyStyle = new GUIStyle(EditorStyles.miniLabel);
            m_commitBodyStyle.wordWrap = true;
        }

        private void OnGUI()
        {
            CreateStyles();

            PackageInstallSource source = UpdateChecker.InstallSource;
            if (source.ChecksCommits)
            {
                DrawCommitUpdate(source);
                return;
            }

            if (!UpdateChecker.TryGetInstalledVersion(out SemanticVersion installed))
            {
                EditorGUILayout.HelpBox("Could not read the installed version from the Build Uploader's package.json.", MessageType.Error);
                return;
            }

            List<ReleaseInfo> newer = UpdateChecker.NewerReleases(m_userChecked);
            if (newer.Count == 0)
            {
                DrawUpToDate(installed, source);
                return;
            }

            ReleaseInfo latest = newer[0];
            DrawHeader(latest, newer.Count, installed, source);

            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
            for (int i = 0; i < newer.Count; i++)
            {
                DrawRelease(newer[i], i == 0);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            DrawUpdateButton(source, latest);
            DrawIgnoreButtons($"Skip v{latest.Version}", "Only show the update button again for a version newer than this one.",
                () => UpdateChecker.Skip(latest));
        }

        private void DrawUpToDate(SemanticVersion installed, PackageInstallSource source)
        {
            if (!DrawCheckProblem("versions"))
            {
                GUILayout.Label("You're up to date", m_titleStyle);
                GUILayout.Label($"Build Uploader v{installed} is the newest version.", m_wrappedStyle);
            }

            GUILayout.Space(10);
            DrawInstallDetails(source);
            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        /// <summary>
        /// Shows that a check is running or failed instead of the result.
        /// </summary>
        /// <returns>True when something was shown.</returns>
        private bool DrawCheckProblem(string what)
        {
            if (UpdateChecker.IsChecking)
            {
                GUILayout.Label($"Checking GitHub for new {what}...", m_titleStyle);
                return true;
            }

            if (m_userChecked && UpdateChecker.LastError != null)
            {
                GUILayout.Label("Could not check for updates", m_titleStyle);
                EditorGUILayout.HelpBox(Shorten(UpdateChecker.LastError), MessageType.Warning);
                if (GUILayout.Button("Try Again", GUILayout.Width(100)))
                {
                    _ = UpdateChecker.CheckNowAsync();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// A line saying a check is running, or a warning when it failed and the results may be old.
        /// </summary>
        private void DrawCheckStatus()
        {
            if (UpdateChecker.IsChecking)
            {
                GUILayout.Label("Checking GitHub...", EditorStyles.miniLabel);
            }
            else if (m_userChecked && UpdateChecker.LastError != null)
            {
                EditorGUILayout.HelpBox("Could not check for updates, so this may be old: " + Shorten(UpdateChecker.LastError), MessageType.Warning);
            }
        }

        private void DrawHeader(ReleaseInfo latest, int newerCount, SemanticVersion installed, PackageInstallSource source)
        {
            GUILayout.Label($"Build Uploader v{latest.Version} is available", m_titleStyle);

            string behind = newerCount > 1 ? $" {newerCount} new versions have come out since." : "";
            GUILayout.Label($"You have v{installed}.{behind} The newest came out {FormatAge(latest.PublishedUtc)}.", m_wrappedStyle);
            DrawInstallDetails(source);

            if (source.FollowsBranch)
            {
                EditorGUILayout.HelpBox($"You follow the {source.GitRevision} branch, so updating gets the newest commit on " +
                                        $"{source.GitRevision} rather than this release.", MessageType.Info);
            }

            if (source.Kind == PackageInstallKind.LocalCopy)
            {
                EditorGUILayout.HelpBox("This is a local folder. Update it the way you got it.", MessageType.Info);
            }

            if (UpdateNotificationRules.IsWaitingForAssetStore(latest, source, DateTime.UtcNow))
            {
                EditorGUILayout.HelpBox($"This version came out on GitHub {FormatAge(latest.PublishedUtc)}. " +
                                        "The Asset Store can take a few days to get it.", MessageType.Info);
            }

            if (SemanticVersion.TryParse(Preferences.SkippedUpdateVersion, out SemanticVersion skipped) && latest.Version <= skipped)
            {
                EditorGUILayout.HelpBox($"You skipped v{skipped}.", MessageType.None);
            }

            DrawCheckStatus();
            GUILayout.Space(4);
        }

        private void DrawInstallDetails(PackageInstallSource source)
        {
            GUILayout.Label("Installed from: " + source.Describe(), EditorStyles.miniLabel);
        }

        private void DrawRelease(ReleaseInfo release, bool isLatest)
        {
            bool open = GetFoldout(release.Tag, isLatest);
            string title = $"{release.Title}   ({release.PublishedUtc.ToLocalTime():d MMM yyyy})";
            open = EditorGUILayout.Foldout(open, title, true, m_releaseFoldoutStyle);
            m_foldouts[release.Tag] = open;
            if (!open)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawNotes(release);
                if (GUILayout.Button(new GUIContent("View on GitHub", Utils.LinkIcon), GUILayout.ExpandWidth(false)))
                {
                    Application.OpenURL(release.Url);
                }
            }
        }

        /// <summary>
        /// Notes before the first "## " section and the Highlights section are shown. Other sections fold away.
        /// </summary>
        private void DrawNotes(ReleaseInfo release)
        {
            string[] lines = ChangelogMarkdown.SplitLines(release.Notes);
            if (lines.Length == 0)
            {
                GUILayout.Label("No release notes.", EditorStyles.miniLabel);
                return;
            }

            string sectionTitle = null;
            List<string> sectionLines = new List<string>();
            foreach (string line in lines)
            {
                if (line.StartsWith("## "))
                {
                    DrawSection(release, sectionTitle, sectionLines);
                    sectionTitle = line.Substring(3).Trim();
                    sectionLines.Clear();
                    continue;
                }

                sectionLines.Add(line);
            }

            DrawSection(release, sectionTitle, sectionLines);
        }

        private void DrawSection(ReleaseInfo release, string title, List<string> lines)
        {
            string[] trimmed = ChangelogMarkdown.GetEntryLines(lines.ToArray(), 0, lines.Count - 1);
            if (title == null)
            {
                ChangelogMarkdown.Draw(trimmed, true);
                return;
            }

            bool open = GetFoldout(release.Tag + "/" + title, title.Equals("Highlights", StringComparison.OrdinalIgnoreCase));
            open = EditorGUILayout.Foldout(open, title, true, m_sectionFoldoutStyle);
            m_foldouts[release.Tag + "/" + title] = open;
            if (open)
            {
                EditorGUI.indentLevel++;
                ChangelogMarkdown.Draw(trimmed, true);
                EditorGUI.indentLevel--;
            }
        }

        private bool GetFoldout(string key, bool defaultOpen)
        {
            return m_foldouts.TryGetValue(key, out bool open) ? open : defaultOpen;
        }

        /// <summary>
        /// One button that updates the way the package was installed.
        /// </summary>
        private void DrawUpdateButton(PackageInstallSource source, ReleaseInfo latest)
        {
            string text;
            string tooltip;
            Action update;
            switch (source.Kind)
            {
                case PackageInstallKind.GitPinned:
                case PackageInstallKind.GitDefaultBranch:
                case PackageInstallKind.Registry:
                    text = $"Update to v{latest.Version}";
                    tooltip = "Package Manager downloads and installs the new version.";
                    update = () => PackageUpdater.UpdateWithPackageManager(source, latest);
                    break;
                case PackageInstallKind.GitBranch:
                    text = $"Update {source.GitRevision}";
                    tooltip = $"Package Manager downloads the newest commit on {source.GitRevision}.";
                    update = () => PackageUpdater.UpdateWithPackageManager(source, latest);
                    break;
                case PackageInstallKind.UnityPackage when source.FromAssetStore:
                    text = "Update from Asset Store";
                    tooltip = "Opens the Build Uploader in Package Manager's My Assets, where you can update and import it.";
                    update = () =>
                    {
                        PackageUpdater.OpenInAssetStore();
                        CloseIfDropDown();
                    };
                    break;
                case PackageInstallKind.UnityPackage:
                    text = "Update from GitHub";
                    tooltip = string.IsNullOrEmpty(latest.UnityPackageUrl)
                        ? "Opens the release on GitHub."
                        : $"Downloads {latest.UnityPackageName} from GitHub and opens Unity's import window.";
                    update = () => PackageUpdater.DownloadAndImport(latest);
                    break;
                default:
                    text = "Open Release Page";
                    tooltip = "This copy can't be updated from here. See the release on GitHub.";
                    update = () => Application.OpenURL(latest.Url);
                    break;
            }

            string blockedReason = PackageUpdater.BlockedReason();
            if (blockedReason != null)
            {
                EditorGUILayout.HelpBox(blockedReason, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(blockedReason != null))
            {
                if (GUILayout.Button(new GUIContent(text, tooltip), m_primaryButtonStyle, GUILayout.Height(28)))
                {
                    update();
                }
            }
        }

        private void DrawCommitUpdate(PackageInstallSource source)
        {
            CommitUpdate update = UpdateChecker.GetCommitUpdate();
            if (update == null)
            {
                GUILayout.Label("Could not read the git checkout", m_titleStyle);
                GUILayout.Label(source.PackageFolder, m_wrappedStyle);
            }
            else if (update.State == CommitUpdateState.Behind)
            {
                DrawNewCommits(update, source);
                return;
            }
            else if (update.State == CommitUpdateState.NotChecked)
            {
                if (!DrawCheckProblem("commits"))
                {
                    GUILayout.Label("Not checked yet", m_titleStyle);
                    if (GUILayout.Button("Check Now", GUILayout.Width(100)))
                    {
                        _ = UpdateChecker.CheckNowAsync();
                    }
                }
            }
            else if (!DrawCheckProblem("commits"))
            {
                string yours = CommitInfo.ShortenSha(update.YourSha);
                switch (update.State)
                {
                    case CommitUpdateState.UpToDate:
                        GUILayout.Label("You're up to date", m_titleStyle);
                        GUILayout.Label($"{yours} is the newest commit on {update.BranchLabel}.", m_wrappedStyle);
                        break;
                    case CommitUpdateState.LocalBranch:
                        GUILayout.Label("Nothing to compare", m_titleStyle);
                        GUILayout.Label($"You're on {update.Branch}, which isn't on GitHub.", m_wrappedStyle);
                        break;
                    case CommitUpdateState.NotFound:
                        GUILayout.Label("Your commit isn't on GitHub", m_titleStyle);
                        GUILayout.Label($"{yours} isn't in the newest {GitHubFeed.CommitsPerCheck} commits on {update.BranchLabel}. " +
                                        $"It may only be on your machine, or be more than {GitHubFeed.CommitsPerCheck} commits behind.", m_wrappedStyle);
                        break;
                }
            }

            GUILayout.Space(10);
            DrawInstallDetails(source);
            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        private void DrawNewCommits(CommitUpdate update, PackageInstallSource source)
        {
            int count = update.NewCommits.Count;
            GUILayout.Label($"{count} new commit{(count == 1 ? "" : "s")} on {update.BranchLabel}", m_titleStyle);

            string yours = CommitInfo.ShortenSha(update.YourSha);
            string have = update.ComparedWithFetched
                ? $"You have {yours}, the {update.Branch} you last fetched. Your own commits aren't on GitHub yet."
                : $"You have {yours}.";
            GUILayout.Label($"{have} The newest came out {FormatAge(update.NewCommits[0].DateUtc)}.", m_wrappedStyle);
            DrawInstallDetails(source);

            if (string.Equals(Preferences.SkippedUpdateCommit, update.LatestSha, StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox("You skipped these commits.", MessageType.None);
            }

            DrawCheckStatus();
            GUILayout.Space(4);

            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
            foreach (CommitInfo commit in update.NewCommits)
            {
                DrawCommit(commit);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            string command = UpdateCommand(update, source);
            GUILayout.Label("To update, run this in your project folder:", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(command, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            string compareUrl = $"{GitHubFeed.RepositoryUrl}/compare/{update.YourSha}...{update.LatestSha}";
            if (GUILayout.Button(new GUIContent("View Changes on GitHub", "Opens every change between your commit and the newest one."),
                    m_primaryButtonStyle, GUILayout.Height(28)))
            {
                Application.OpenURL(compareUrl);
            }

            DrawIgnoreButtons("Skip These Commits", "Only show the update button again when newer commits come out.",
                () => UpdateChecker.Skip(update));
        }

        private void DrawCommit(CommitInfo commit)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUILayout.Label(commit.Subject, m_commitSubjectStyle);
                string body = commit.Body;
                if (body.Length > 0)
                {
                    GUILayout.Label(body, m_commitBodyStyle);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{commit.ShortSha}  ·  {commit.Author}  ·  {commit.DateUtc.ToLocalTime():d MMM yyyy}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("View", Utils.LinkIcon, "Open this commit on GitHub."), EditorStyles.miniButton))
                    {
                        Application.OpenURL(commit.Url);
                    }
                }
            }
        }

        /// <summary>
        /// The git command that brings the checkout up to date: pull on a branch, or update a detached submodule.
        /// </summary>
        private static string UpdateCommand(CommitUpdate update, PackageInstallSource source)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string folder = Path.GetFullPath(source.PackageFolder);
            if (folder.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                folder = folder.Substring(projectRoot.Length);
            }

            folder = folder.Replace('\\', '/');
            return update.CheckedOutBranch != null
                ? $"git -C \"{folder}\" pull"
                : $"git submodule update --remote \"{folder}\"";
        }

        private void DrawIgnoreButtons(string skipText, string skipTooltip, Action skip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Remind Me Later", "Hide the update button for a while."), FocusType.Passive))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Tomorrow"), false, () => RemindLater(TimeSpan.FromDays(1)));
                    menu.AddItem(new GUIContent("In a week"), false, () => RemindLater(TimeSpan.FromDays(7)));
                    menu.ShowAsContext();
                }

                if (GUILayout.Button(new GUIContent(skipText, skipTooltip)))
                {
                    skip();
                    CloseIfDropDown();
                }

                if (GUILayout.Button(new GUIContent("Don't Notify Me Again", "Stop checking for updates.")))
                {
                    bool stop = EditorUtility.DisplayDialog("Build Uploader Updates",
                        "The Build Uploader will stop checking for new versions.\n\n" +
                        "Check yourself from Window->Build Uploader->Check for Updates, or turn checking back on in " +
                        "Preferences->Build Uploader->General.", "Stop Checking", "Cancel");
                    if (stop)
                    {
                        UpdateChecker.StopChecking();
                        CloseIfDropDown();
                    }
                }
            }

            DrawFooter();
        }

        private void RemindLater(TimeSpan delay)
        {
            UpdateChecker.RemindLater(delay);
            CloseIfDropDown();
        }

        private void DrawFooter()
        {
            DateTime lastCheck = Preferences.LastUpdateCheckUtc;
            string checkedText = lastCheck == DateTime.MinValue ? "Never checked" : $"Checked {FormatAge(lastCheck)}";
            GUILayout.Label(checkedText, EditorStyles.miniLabel);
        }

        private void CloseIfDropDown()
        {
            // A dialog or menu can take focus from a drop-down, which closes it on its own.
            if (this != null && !m_userChecked)
            {
                Close();
            }
        }

        private static string Shorten(string text)
        {
            return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
        }

        internal static string FormatAge(DateTime utc)
        {
            TimeSpan age = DateTime.UtcNow - utc;
            if (age.TotalHours < 1)
            {
                return "just now";
            }

            if (age.TotalDays < 1)
            {
                int hours = (int)age.TotalHours;
                return hours == 1 ? "an hour ago" : $"{hours} hours ago";
            }

            int days = (int)age.TotalDays;
            return days == 1 ? "yesterday" : $"{days} days ago";
        }
    }
}
