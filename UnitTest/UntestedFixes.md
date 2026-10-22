# Fixes without automated tests

Every fix lands with a test in `UnitTest/`: in `Core/` when it needs no credentials, network or player build,
otherwise in the matching service assembly. When a fix cannot be tested without credentials, a live service,
an interactive terminal or a person, record it here with what would make it testable. Remove the row when a
test exists.

Last reviewed: 2026-10-19 against `develop` 181232e plus the uncommitted fixes for #109, #113, #115 and #116.

| Fix | Change | Why there is no test | Check by hand | What would make it testable |
| --- | --- | --- | --- | --- |
| Steam login button clears the cached "requires login" status before opening SteamCMD | `SteamworksService.StartAuthentication` (a40160d, PR #121) | It opens an interactive SteamCMD console for the password and Steam Guard | Set the status to *Requires login*, press the login button: the status resets and a console opens with `+login <user>` | A seam for `ProcessUtils.ShowConsole` (e.g. an injectable launcher) so a test can assert the call without opening a terminal. `InvalidateLoginStatus` itself is covered by `SdkInitializationTests` |
| Linux SteamCMD console uses the same HOME as uploads (`/usr/bin/env HOME=<builder_linux>/home steamcmd.sh +login`) | `SteamSDK.ShowSteamCMDConsole` (7efa179, PR #120) | It opens a terminal emulator | On Linux log in through the button, then check `<builder_linux>/home/Steam/config/config.vdf` contains the account | Same console seam as above |
| Linux uploads and DRM wrapping run SteamCMD with its own HOME | `SteamSDK` upload / DRM (7efa179) | A real upload needs a Steam account | Covered by `SteamPreviewTests` on Linux CI (needs Steam secrets) | Already covered with credentials. The HOME handover is also covered credential-free by `SteamLoginProbeTests.SteamCMDRunsWithItsOwnHomeOnLinux` |
| "Upload All" renamed to "Run All" | `WindowUploadTab` (PR #111, issue #110) | IMGUI label text | Open **Window → Build Uploader** with several configs | Not worth automating |
| Pipeline integration references `Unity.Pipeline.Attributes` (PR #113) | `VeugelJame.BuildUploader.Editor.UnityPipeline.asmdef` | CI removes Unity Pipeline from its test copies, and this project uses Pipeline 0.3.1 | Checked 2026-10-19 in throwaway projects. Unity 6000.6.0f1 with Pipeline 0.8.0-exp.1 compiles with the reference and fails with `CS0246` at `PipelineCommands.cs:28,31` without it. Unity 6000.3.21f1 with Pipeline 0.3.1-exp.1 compiles with it | A compile-only CI job that keeps `com.unity.pipeline` installed at the newest version |
| Steam depot editing UI: Depot ID field can be cleared, its label turns red while empty, and errors and warnings show under the depot list (#116, #115) | `ReorderableListOfDepots`, `SteamworksService_ProjectSettings.DrawDepots` | IMGUI drawing and keyboard focus | **Project Settings → Build Uploader → Steamworks**: add a depot. The Depot ID field is empty with a placeholder, and an error says it has no Depot ID. Type, select with Shift+arrows, delete. Give two depots the same name or ID and check the messages | The parsing and the messages are covered by `SteamDepotTests`; the drawing itself is not automatable here |
| Depot popup shows "Name (Depot ID)" (#115) | `EditorUtils.DrawPopup` label overload, `SteamUploadDestination.GUI` | `GenericMenu` only exists in an interactive editor | In an upload config with a Steam destination, open **Choose Depots** with two same-named depots: both appear, with their IDs | Labels covered by `SteamDepotTests.PopupLabelsTellSameNamedDepotsApart` |
| **Stop task if this fails** toggle on task and config actions; picking an action applies its `DefaultStopTaskIfFailed` | `UploadConfig.DrawStopTaskIfFailedToggle`, `WindowUploadTab`, `UploadConfig.GUI` | IMGUI | In **Window → Build Uploader**, add an action and tick the toggle. Save and reopen: it is kept. Pick an action that defaults it on: it turns on | Behaviour covered by `UploadTaskTests.StartAction*` and `FailedConfigActionSetToStopTaskFailsTask` |
| **Skip Number Updates** checkbox (#109) | `ProjectSettings_General` | IMGUI | **Project Settings → Build Uploader**: toggle it, start an upload, and check `BuildUploader/ProjectSettings.json` is unchanged | Behaviour covered by `ProjectSettingsTests` |

## Covered by tests (for reference)

| Fix | Test |
| --- | --- |
| A step failing without `SetFailed` still fails the task | `UploadTaskTests.FailedDestinationWithoutReasonStillFailsTask` |
| An embedded copy of the package no longer fails to compile its test assemblies in a project without Test Framework (NUnit missing) | `OptionalPackagesTests.CompilesWithoutOptionalPackages` |
| ANSI colour codes stripped from process output (59ce393) | `ProcessUtilsTests.CapturedOutputHasAnsiCodesStripped`, `...TaskOutput...`, `SteamLoginProbeTests.ColouredLoginOutputIsAuthorized` |
| SDK tools get their exec bit (5ef4383) | `ProcessUtilsTests.EnsureExecutable*`, `SdkInitializationTests.*Initialize*`, `EpicGamesUploadTests.UploadMakesBuildPatchToolExecutable` |
| Linux SteamCMD gets its own HOME, and its login is read from there (7efa179, 516c138) | `SteamLoginProbeTests.SteamCMDRunsWithItsOwnHomeOnLinux`, `...CredentialCheckReadsWhereSteamCMDSavesItsLogin` |
| Login status is cleared and kept per user (a40160d, 669b0c7) | `SdkInitializationTests.InvalidateLoginStatusForgetsCachedResult`, `...CachedLoginStatusBelongsToOneUser` |
| New list entries never reuse an Id still in use; copies keep their Id; same-named depots get distinct popup labels (#115) | `SteamDepotTests.NewListItemsNeverReuseAnIdStillInUse`, `...CopiesKeepTheirIds`, `...PopupLabelsTellSameNamedDepotsApart`, `...AppReportsDepotsThatBlockUploads`, `...AppWarnsAboutSameNamedDepots` |
| Depot ID starts unset, can be cleared, and a missing or repeated ID blocks the upload (#116) | `SteamDepotTests.NewDepotsStartWithoutADepotId`, `...DepotIdFieldCanBeClearedAndIgnoresJunk`, `...DestinationBlocksMissingAndRepeatedDepotIds` |
| Skip Number Updates leaves `ProjectSettings.json` untouched (#109) | `ProjectSettingsTests.*` |
