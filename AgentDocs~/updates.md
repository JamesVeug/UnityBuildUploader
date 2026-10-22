# Update notifications

Read this before changing `Editor/Core/Updates/`, the update button in `BuildUploaderWindow`, or the Updates
section of **Preferences → Build Uploader → General**.

## What it does

- When Unity opens (the first script load of the session, tracked in `SessionState`; not script reloads, not the
  window, not batch mode) and the last check is a week old (a month with Monthly), the editor asks GitHub
  (`api.github.com/repositories/778195997`, by repository id so a rename doesn't break it) whether there is
  something newer. Unauthenticated: 60 requests per hour per IP. A failed check waits an hour before retrying.
  The session is only marked once the check ends, so a script reload mid-download checks again. It starts from
  `EditorApplication.update` once scripts have compiled, not `delayCall`, which waits for a repaint that never
  comes when Unity opens in the background.
- Nothing else checks on its own. **Check for Updates** checks right away.
- Disclosed in the README's Security section, a line in the Welcome window and the Updates section of
  Preferences. Keep them in step if what is sent changes.
  - Most installs download `/releases` and compare release tags with the `version` in `package.json`. Kept in
    `Library/BuildUploader/Releases.json`.
  - Git checkouts (a submodule or clone: the package folder has `.git`) download `/commits?sha=<branch>`
    (the newest 100) and look for the checked out commit in them. Kept in `Library/BuildUploader/Commits.json`.
- When there is something newer, `BuildUploaderWindow.DrawUpdateButton` draws a small button. The Upload tab
  draws it right of its settings icon in the Upload Configs header; other tabs get it at the end of the tab row.
  It never adds a row of its own. It shows `Utils.DownloadIcon` (`Assets/DownloadIcon.png`, `DownloadIconLight.png`
  on the light skin) with `vX.Y.Z`, or `N commits` for a git checkout. Every install type uses the same icon.
- The button opens `BuildUploaderUpdateWindow` as a drop-down:
  - Releases: notes for every newer release (Highlights open, other `## ` sections folded) and one update button
    for the install type.
  - Git checkouts: each new commit's message, sha, author and date, the git command that updates the checkout,
    and **View Changes on GitHub** (the compare page). Nothing runs git.
  - Remind Me Later / Skip / Don't Notify Me Again.
- **Window → Build Uploader → Check for Updates** (also in the window's ⋮ menu and Preferences) opens the same
  window as a utility window and checks now, ignoring Skip, Remind Me Later, Never and the Asset Store delay.

## Install types (`PackageInstallSource`)

| Kind | Detected from | Update button |
| --- | --- | --- |
| `GitPinned` | `PackageSource.Git`, revision is a version or commit hash | `Client.Add(url#<new tag>)` |
| `GitBranch` | `PackageSource.Git`, any other revision | `Client.Add` with the same URL (newest commit on the branch) |
| `GitDefaultBranch` | `PackageSource.Git`, no revision | `Client.Add` with the same URL |
| `UnityPackage` | `PackageSource.Embedded` without `.git`, or not a package at all | With `FromAssetStore`: **Update from Asset Store** (`PackageManagerWindow.OpenURL` by reflection with `com.unity3d.kharma:content/306907`, else the web page). Otherwise **Update from GitHub** (downloads the release's `.unitypackage`, then `AssetDatabase.ImportPackage(path, true)`) |
| `GitCheckout` | `Embedded` or `Local` with `.git` (submodule or clone) | **View Changes on GitHub**, plus `git -C "<folder>" pull` on a branch or `git submodule update --remote "<folder>"` when detached |
| `LocalCopy` | `Local` without `.git`, `LocalTarball` | **Open Release Page** only, from Check for Updates; never notified |
| `Registry` | `PackageSource.Registry` | `Client.Add(name@version)` |

- The Asset Store `.unitypackage` installs into `Packages/com.veugeljame.builduploader`, so Asset Store and
  GitHub `.unitypackage` installs look the same. `FromAssetStore` is a hint: the Asset Store download cache
  (`ASSETSTORE_CACHE_PATH` or the per-OS default) has `JamesGamesNZ/**/Build Uploader*.unitypackage`. It only
  decides whether the update button goes to the Asset Store or GitHub.
- `UnityPackage` installs are not notified until a release is 3 days old
  (`UpdateNotificationRules.AssetStoreDelay`), while the Asset Store reviews it.
- Never `Application.OpenURL` a `com.unity3d.kharma:` link: the operating system hands it to whichever editor
  registered the protocol last and starts another editor.
## Tidying up after a `.unitypackage` update (`UnityPackageCleanup`)

Unity's importer (used by the Asset Store's Import and the GitHub update button) adds and updates files but never
deletes files a newer version removed, and a file that moved folder keeps its old path (same GUID) with the new
content. Checked 2026-10-22 on 2019.4, 2022.3 and 6000.3. Real cases: 3.1.1 → 3.2.0 deleted
`UploadTaskStringFormatterContext.cs` and recreated the class elsewhere (the update stops compiling, CS0101);
`PipelineCommands.cs` moving into `Integrations/UnityPipeline` on develop would stay in the core assembly, where
`BUILD_UPLOADER_PIPELINE` isn't defined, so the `unity` CLI command would silently go.

- `[InitializeOnLoad]` hooks `AssetDatabase.importPackageCompleted`. Unity passes the imported file's full path
  (or only its name) without `.unitypackage`. The file is that path, the GitHub button's download
  (`ExpectImport`, kept in `SessionState`), the newest Asset Store download, or `~/Downloads/<name>.unitypackage`.
- `UnityPackageFile.ReadContents` reads the tar.gz's `<guid>/pathname` entries (GUID → path) without importing.
- The plan compares that with every `.meta` in the package folder (skipping `.git` and `~` folders): GUIDs the
  package doesn't have are deleted with their `.meta`; GUIDs at another path in the package are moved with their
  `.meta`. Folders are only removed once empty, so files without a `.meta` stay. Each change is logged.
- Nothing happens when the package's `package.json` GUID isn't the installed one (another asset), when it would
  delete more than half the files (a partial package), or when the package folder has `.git`.
- The cleanup runs in whichever code handles the import callback. Checked end to end on all three editors with a
  stale duplicate class (the compile fails, so the old domain handles it) and a moved file: both fixed and the
  project compiles. Updating from 3.3.0 or older (no cleanup code) still leaves files behind.

## Git checkouts (`LocalGitCheckout`, `CommitUpdate`)

- HEAD, the branch and `refs/remotes/origin/<branch>` are read from the `.git` folder (or the folder a
  submodule's `.git` file points at, loose refs or `packed-refs`, `commondir` for worktrees). Git doesn't have to
  be installed and the repository is never changed.
- The branch compared with GitHub: the checked out branch when origin has it; a detached HEAD uses the
  submodule's `branch` in the project's `.gitmodules`, else GitHub's default branch. A branch origin doesn't
  have is never checked (`LocalBranch`).
- States: `NotChecked`, `LocalBranch`, `NotFound` (HEAD isn't in the newest 100: unpushed or far behind),
  `UpToDate`, `Behind`. When HEAD has unpushed commits, the last fetched `origin/<branch>` is compared instead.
- What the button shows (and HEAD) is worked out once and again after a check or a preference change, not on
  every repaint. A pull changes the package's scripts, which reloads them, so the button updates without another
  request. Switching branch makes the next start-up check due.

## Preferences

| Key | Store | Meaning |
| --- | --- | --- |
| `BuildUploader_CheckForUpdates` | `EditorPrefs` | `Weekly` (0), `Never` (1) or `Monthly` (2); new options get the next number |
| `BuildUploader_SkippedUpdateVersion` | `EditorPrefs` | Hide the button up to this version |
| `BuildUploader_SkippedUpdateCommit` | `EditorPrefs` | Git checkouts: hide the button while this is the newest commit |
| `BuildUploader_UpdateRemindAfter` | `EditorPrefs` | UTC ticks; hide the button until then |
| `BuildUploader_LastUpdateCheck`, `BuildUploader_LastFailedUpdateCheck` | `ProjectEditorPrefs` | UTC ticks |
| `BuildUploader_UpdateCommitsBranch` | `ProjectEditorPrefs` | The branch `Commits.json` came from |

## Rules

- Versions compare with `SemanticVersion` (pre-releases sort first; `2.1.1a` is a pre-release). Do not use
  `Utils.ToSemantic` for this: it turns `3.4.0-rc1` into `3.4.01`.
- Pre-releases and drafts are never offered.
- Decisions live in `UpdateNotificationRules` (no state, covered by `UpdateCheckTests`); install handling in
  `PackageInstallSource` (`PackageInstallSourceTests`); reading `.git` in `LocalGitCheckout`
  (`LocalGitCheckoutTests`, with fake checkouts from `FakeGitCheckout`); the `.unitypackage` tidy-up in
  `UnityPackageCleanup` (`UnityPackageCleanupTests`, with packages from `FakeUnityPackage`). The GUI and the
  Package Manager calls are manual checks in `UnitTest/UntestedFixes.md`.
- Release notes are drawn with `ChangelogMarkdown`, shared with the Welcome window.
