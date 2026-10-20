# Build Uploader tests

EditMode tests, one assembly per area. A host project discovers them by adding
`com.veugeljame.builduploader` to the `testables` array in `Packages/manifest.json`.

**Every fix lands with a test.** Put it in `Core/` when it needs no credentials, network or player build.
If a fix cannot be tested without credentials, a live service, an interactive terminal or a person, add a
row to [`UntestedFixes.md`](UntestedFixes.md) instead, saying what would make it testable.

| Assembly | Fixture | Needs | Covers |
| --- | --- | --- | --- |
| `BuildUploader.UnitTest.Core` | `UploadTaskTests` | Nothing | UploadTask end to end on every host: folder/file sources, local path destination, cache and cleanup, validation failure, exclude/compress/decompress, action triggers and status, config post-actions, failing destinations, cancellation, disabled configs, duplicate files, `{key}` formatting |
| | `ProcessUtilsTests` | Nothing | ANSI colour codes stripped from captured output; `EnsureExecutable` restores the exec bit |
| | `SdkInitializationTests` | Nothing | Steam, itch.io, Nintendo and PlayStation `Initialize` on fake SDK folders make their tools executable; Steam login status is cleared and kept per user |
| | `SteamLoginProbeTests` (macOS, Linux) | Nothing | `SteamSDK.ProbeLoginAsync` against a fake `steamcmd.sh`: coloured output, Steam Guard, rate limit, Linux SteamCMD HOME, where the saved login is read |
| | `EpicGamesUploadTests` (macOS, Linux) | Nothing | `EpicGames.Upload` against a fake BuildPatchTool: exec bit and secret passed by name |
| | `SteamDepotTests` | Nothing | Depot Ids never reused, labels with Depot IDs, missing/duplicate Depot ID errors in the app config and the Steam destination (#115, #116) |
| | `ProjectSettingsTests` | Nothing | Build and upload numbers, and the Skip Number Updates setting that keeps `ProjectSettings.json` untouched (#109) |
| `BuildUploader.UnitTest.Discord` | `DiscordMessageTests` | Nothing | `DiscordMessageChannelAction` dry run: serialized message and embed, no HTTP |
| `BuildUploader.UnitTest.Itchio` | `ItchioWebGLTests` | Native Butler | `ItchioDestination` `push --dry-run` of an HTML fixture on channel `html5` |
| `BuildUploader.UnitTest.Steam` | `SteamPreviewTests` | SteamCMD + authorized account | `SteamUploadDestination` preview upload, branch `none`; nothing goes live |
| `BuildUploader.UnitTest.Builds` | `DesktopBuildTests` | Desktop build module | One player for the active Win64/macOS/Linux64 target through `BuildProfileSource` (Unity 6) or `BuildConfigSource` (2019/2022) into `LocalPathDestination` |
| | `OptionalPackagesTests` | Nothing (starts a second editor, a few minutes) | A new project with only the package and the engine modules (plus SharpZipLib before 2021.1) compiles, and leaves out the assemblies that need Unity Pipeline or Test Framework |
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

## Writing tests

- Start tasks with `StartAsync(false)` (via `TestSupport`). With debug logs on, any `Debug.LogError` from a
  failure path fails the test.
- Use `TestSupport.Run` when the task must succeed. Use `TestSupport.Execute` and assert on `task.Report`
  for failure paths. Pass a timeout so a hung task fails instead of stalling CI.
- Keep `Core` free of credentials, network and player builds. Fake external tools instead: `FakeSteamSdk` builds
  an SDK folder whose `steamcmd.sh` prints canned output and records its HOME. Pipeline doubles
  (`UnitTestRecordingAction`, `UnitTestFailingDestination`, `UnitTestGateDestination`) live in
  `Core/UnitTestDoubles.cs`; like every pipeline type they are found by reflection and appear in the host's pickers.
- The host CI runner requires **every** `[Test]`/`[UnityTest]` declared in any `Core/*.cs` file to pass. A file
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
