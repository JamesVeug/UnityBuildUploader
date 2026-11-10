# Build Uploader tests

EditMode tests, one assembly per area. A host project discovers them by adding
`com.veugeljame.builduploader` to the `testables` array in `Packages/manifest.json`.

**Every fix lands with a test.** Put it in `Core/` when it needs no credentials, network or player build.
If a fix cannot be tested without credentials, a live service, an interactive terminal or a person, add a
row to [`UntestedFixes.md`](UntestedFixes.md) instead, saying what would make it testable.

| Assembly | Fixture | Needs | Covers |
| --- | --- | --- | --- |
| `BuildUploader.UnitTest.Core` | `UploadTaskTests` | Nothing | UploadTask end to end on every host: folder/file sources, local path destination, cache and cleanup, validation failure, exclude/compress/decompress, action triggers and status, config post-actions, failing destinations, cancellation, disabled configs, duplicate files, `{key}` formatting, failed actions with and without Stop task if this fails, Invoke Static Method before a build, the progress bar removed when a task finishes (on the main thread, not in a finalizer) |
| | `UploadTaskResumeTests` | Nothing | Tasks resumed from a checkpoint as after a script reload (#107): no repeated numbers, Validation or start actions, finished sources skipped and ones without run state run again, an interrupted Upload fails then cleans up, interrupted after-step actions, the reload limit, checkpoints that no longer load, resuming or discarding tasks from a closed session, report and `BuildConfigSource` round trips, a resumed dry run keeping dry run on, even for types that don't save it |
| | `UploadTaskReportFileTests` | Nothing | The report file is written at the start and follows the task, a resumed task keeps its file, older report files load as complete, the Upload Tasks window's Will resume / Interrupted states |
| | `BatchModeUtilTests` | Nothing | Batch mode arguments: `-uploadProfile` (repeated, or left out as in older versions), `-dryRun`, `-uploadDescription`, `-uploadReportFile` and `-async`; a missing profile starts nothing and writes why to the report file; each task's report is appended when it finishes |
| | `SavedTypeResolutionTests` | Nothing | Actions and destinations from other assemblies reload from a saved profile with their data; the JSON still names types by full name |
| | `InvokeStaticMethodActionTests` | Nothing | Invoke Static Method: public, private, nested and assembly-qualified methods; `bool`, `int`, `Task`, `Task<bool>`, `Task<int>` and `StepResult` shapes; UniTask (through the stand-in in `UnitTestUniTask.cs`) and `IEnumerator` coroutines; return types rejected by validation; exceptions; every path error, including a type in two assemblies |
| | `BuildSourceAddressablesTests` | Nothing | Build Addressables and Clean Addressables are saved, default to off in older JSON, are kept without Addressables, and are validated (uses a stand-in for the integration) |
| | `ProcessUtilsTests` | Nothing | ANSI colour codes stripped from captured output; `EnsureExecutable` restores the exec bit |
| | `SdkInitializationTests` | Nothing | Steam, itch.io, Nintendo and PlayStation `Initialize` on fake SDK folders make their tools executable; Steam login status is cleared and kept per user |
| | `SteamLoginOutputTests` | Nothing | Reading SteamCMD's login output on every host: OK after a login token warning or a Steam Mobile app approval, and the Steam Login window's results (wrong password, a Steam Guard code needed or wrong, the password and code hidden from the message) |
| | `SteamLoginProbeTests` (macOS, Linux) | Nothing | `SteamSDK.ProbeLoginAsync` against a fake `steamcmd.sh`: coloured output, Steam Guard, rate limit, Linux SteamCMD HOME, where the saved login is read |
| | `EpicGamesUploadTests` (macOS, Linux) | Nothing | `EpicGames.Upload` against a fake BuildPatchTool: exec bit and secret passed by name |
| | `SteamDepotTests` | Nothing | Depot Ids never reused, labels with Depot IDs, missing/duplicate Depot ID errors in the app config and the Steam destination (#115, #116) |
| | `AndroidBuildSettingsTests` | Nothing (no Android module needed) | `BuildConfig` App Bundle (`.aab` name) and Android architectures: saved, loaded (also from configs without them), applied; the desktop architecture kept off Android |
| | `ProjectSettingsTests` | Nothing | Build and upload numbers, and the Skip Number Updates setting that keeps `ProjectSettings.json` untouched (#109); build meta data skipped, not failing the build, for targets with no StreamingAssets folder such as an Android `.apk` |
| | `UpdateCheckTests` | Nothing (fake GitHub feed) | Version order (pre-releases, `2.1.1a`), reading the GitHub releases and commits JSON, when the update button shows (skip, remind later, never, pre-releases, the Asset Store delay, local copies), comparing a git checkout's commit (behind, up to date, unpushed commits, not found), Check for Updates, the weekly and monthly checks and their cache, the button's state kept until something changes, splitting the changelog |
| | `PackageInstallSourceTests` | Nothing | How the package was installed (Git tag, commit, branch, no revision, SSH and `?path=` URLs, embedded and local with and without `.git`, registry) and what the update button asks Package Manager for |
| | `UnityPackageCleanupTests` | Nothing (`.unitypackage` files from `FakeUnityPackage`, fake project folders) | Reading a `.unitypackage`'s GUIDs and paths (`./` prefixes, GNU long names), finding the imported file from what Unity passes, deleting files a new version removed and moving moved ones with their `.meta`, keeping files without a `.meta`, ignoring other packages, refusing partial packages and git checkouts |
| | `UploadTabThinLayoutTests` | Nothing | When the Upload tab switches between its wide and thin layouts (with a margin so it doesn't flicker), and the thin layout's Upload Config tab labels |
| | `WelcomeWindowStartupTests` | Nothing | When the Welcome window opens on its own: never in batch mode or test runs (so they don't use up the When updated popup), once per session for On startup, for a new version for When updated, and not when set to Never |
| | `UploadConfigSectionTests` | Nothing | The menu on a section's title: adding an entry of a chosen type or one to choose later, Enable All / Disable All (actions go to Always or Never), Remove Disabled, Clear All, copying a section's entries and pasting them into the same section of another config |
| | `AutoGenerateMenuItemsPathTests` | Nothing | The Quick Upload menu items file is imported with a project-relative `Assets/...` path (Unity 2019 to 2021 reject full paths), for the default folder and one picked as a full path; folders outside the project are not imported |
| | `LocalGitCheckoutTests` | Nothing (fake `.git` folders from `FakeGitCheckout`) | Reading HEAD, the branch and origin's copy from loose refs, `packed-refs`, a detached HEAD and a submodule's `.git` file; which GitHub branch is compared; the submodule branch in `.gitmodules` |
| `BuildUploader.UnitTest.Discord` | `DiscordMessageTests` | Nothing | `DiscordMessageChannelAction` dry run: serialized message and embed, no HTTP |
| `BuildUploader.UnitTest.Itchio` | `ItchioWebGLTests` | Native Butler | `ItchioDestination` `push --dry-run` of an HTML fixture on channel `html5` |
| `BuildUploader.UnitTest.Steam` | `SteamPreviewTests` | SteamCMD + authorized account | `SteamUploadDestination` preview upload, branch `none`; nothing goes live |
| `BuildUploader.UnitTest.Builds` | `DesktopBuildTests` | Desktop build module | One player for the active Win64/macOS/Linux64 target through `BuildProfileSource` (Unity 6) or `BuildConfigSource` (2019/2022) into `LocalPathDestination`. `OneBuildWithAddressablesContent` (compiled only with Addressables 1.19.4+ on 2021.2+, and needs Addressables settings in the project) builds with Build Addressables and checks the player has `StreamingAssets/aa/catalog.*` |
| | `ScriptingDefineBuildTests` | Desktop build module; an editor started without `-batchmode` to cover #107 | A `BuildConfigSource` whose defines differ from the editor's reloads scripts mid-task, and the task resumes, builds a player, saves its report and stays in the Upload Tasks window. Assembly reloads are allowed as they are outside tests. In batch mode (CI) the build happens before the editor recompiles, so it passes there without reloading |
| | `OptionalPackagesTests` | Nothing (starts a second editor, a few minutes) | A new project with only the package and the engine modules (plus SharpZipLib before 2021.1) compiles, and leaves out the assemblies that need Unity Pipeline or Test Framework |
| `BuildUploader.UnitTest.Android` | `AndroidBuildTests` | Android Build Support (SDK, NDK, OpenJDK), `-buildTarget Android` | One `.apk` through `BuildProfileSource` (Unity 6, `BUILDUPLOADER_TEST_BUILD_PROFILE`) or `BuildConfigSource` for the active target (2019/2022), and one App Bundle (`.aab`, never named `.apk`) through `BuildConfigSource`, into `LocalPathDestination`. Its own assembly so desktop runs never list it |
| `BuildUploader.UnitTest.Addressables` | `AddressablesIntegrationTests` | Addressables 1.19.4+ on Unity 2021.2+ (not in CI) | Build With Player is forced for one build and restored, also when the build throws or the settings were saved meanwhile; left alone when already on; a logged content build failure fails the step; the Build Addressables action (builds the project's own content when it has Addressables settings) |
| `BuildUploader.UnitTest.Common` | `TestSupport` | — | Helpers: `Run`, `Execute`, `Await`, `WaitUntil`, `OutputDirectory`, `FixtureDirectory`, `RequiredEnvironment` |

## Configuration

| Variable | Used by |
| --- | --- |
| `BUILDUPLOADER_TEST_BUTLER` | Full path to the native Butler executable |
| `BUILDUPLOADER_TEST_STEAM_SDK` | Steamworks SDK directory containing `tools/ContentBuilder` |
| `BUILDUPLOADER_TEST_STEAM_USER`, `..._APP_ID`, `..._DEPOT_ID` | Dedicated build account, test app and depot |
| `BUILDUPLOADER_TEST_STEAM_CREDENTIALS` | JSON map of SteamCMD credential files (see the host project's `.ci/README.md`) |
| `BUILDUPLOADER_TEST_BUILD_PROFILE` | Unity 6: Build Profile asset for the active target, with an enabled scene |
| `BUILDUPLOADER_TEST_REQUIRE_CONFIGURATION=1` | Missing configuration fails instead of being ignored (CI sets this) |

- Steam authenticates even in preview mode. Use a dedicated depot and never run two uploads against one SDK
  at once.
- Before Unity 6, configure enabled scenes and Player Settings in Build Settings.
- Start the editor with the target already selected (`-buildTarget`). Switching target mid-test reloads assemblies.

## Side effects

- Tests write under `<HostProject>/TestResults/BuildUploader/` and keep the output.
- Tests temporarily change SDK paths, the Steam username and Build Uploader preferences, and restore them in
  `finally` / `TearDown`. A cached Steam login status of *Unknown* is not restored (it is cleared).
- Every task bumps `TotalUploadTasksStarted` and every build bumps `LastBuildNumber` in
  `<HostProject>/BuildUploader/ProjectSettings.json`. Run in a disposable copy or revert that file afterwards.
- Running tasks save checkpoints to `<HostProject>/Library/BuildUploader/InProgressTasks/` so they can resume after
  a script reload. Core fixtures that run tasks point `UploadTaskCheckpoints.FolderPath` at an output folder in
  `SetUp` and restore it in `TearDown`, so a test task that doesn't finish is never resumed in the project. Do the
  same in a new fixture that runs tasks. `ScriptingDefineBuildTests` uses the real folder, because it needs the
  resume after a real reload, and deletes its checkpoint in `TearDown`.

## Writing tests

- Start tasks with `StartAsync(false)` (via `TestSupport`). With debug logs on, any `Debug.LogError` from a
  failure path fails the test.
- Use `TestSupport.Run` when the task must succeed. Use `TestSupport.Execute` and assert on `task.Report`
  for failure paths. Pass a timeout so a hung task fails instead of stalling CI.
- Keep `Core` free of credentials, network and player builds. Fake external tools instead: `FakeSteamSdk` builds
  an SDK folder whose `steamcmd.sh` prints canned output and records its HOME. Pipeline doubles
  (`UnitTestRecordingAction`, `UnitTestFailingAction`, `UnitTestFailingDestination`, `UnitTestGateDestination`,
  `UnitTestGateSource`, `UnitTestCountingSource`) live in
  `Core/UnitTestDoubles.cs`; like every pipeline type they are found by reflection and appear in the host's pickers.
  Methods for Invoke Static Method are in `Core/UnitTestStaticMethods.cs`.
- The host CI runner requires **every** `[Test]`/`[UnityTest]` declared in any `Core/*.cs` file to pass, matched by
  method name, so do not parameterize them (`[Values]`, `[TestCase]`) and keep the fixture the first `public class` in
  its file. A suite also fails if any of its cases is skipped: compile a case that needs an optional package out
  with a version define instead of `Assert.Ignore`. A file
  that only applies to some hosts starts with `// BuildUploader-Hosts: OSXUniversal Linux64` (build target
  names) and wraps its code in a matching `#if`. Otherwise give each host a real assertion rather than skipping.
  Other suites name their required cases in `.ci/run_tests.py`.
- `Core` sees package internals (`InternalsVisibleTo`), so overrides of `protected internal` members keep
  `protected internal`.
- Restore every preference or SDK path a test changes, and never touch the real user's Steam folder.
- Keep sources compiling on 2019.4: C# 7.3, no Unity 6 APIs without `#if UNITY_6000_0_OR_NEWER`.
- Every test asmdef needs the `defineConstraints` `UNITY_INCLUDE_TESTS` and `BUILD_UPLOADER_TEST_FRAMEWORK`, and
  a `versionDefines` entry that sets `BUILD_UPLOADER_TEST_FRAMEWORK` when `com.unity.test-framework` is installed
  (copy one from `Core`). When the package is embedded, Unity defines `UNITY_INCLUDE_TESTS` even without Test
  Framework, and the assembly then fails for want of NUnit. `OptionalPackagesTests` catches a missing constraint.

## Running

From the host project, `.ci/run_tests.py` runs the suites in order (core → services → build) and stops at
the first failure. See the host `.ci/README.md`. To run several editors and targets with your own projects,
copy `matrix.example.json` and run:

```
python UnitTest/run_matrix.py my-matrix.json --output /absolute/path/to/fresh-results
```

`run_matrix.py` runs core and services once per editor and the build once per desktop target. It writes
NUnit XML, editor logs and `matrix.json`, and exits nonzero if the matrix is incomplete.
