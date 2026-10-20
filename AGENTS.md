# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex and others) working in this package. Agents load this
file into every session, so it holds only the rules and a map. `CLAUDE.md` just imports it.

Topic detail lives in `AgentDocs~/`: plain markdown written for agents, and readable by people too. The
`~` suffix stops Unity importing the folder, so it needs no `.meta` files and never reaches the editor.
Before editing, read the file that matches your task.

## What this is

- **Build Uploader** (`com.veugeljame.builduploader`, v3.3.0, namespace `Wireframe`): an Editor-only UPM
  package that runs upload pipelines — Sources → Modifiers → Destinations → Actions — through `UploadTask`.
- Supports Unity **2019.4+** (`package.json`). Unity 6 features (Build Profiles) are behind
  `UNITY_6000_0_OR_NEWER`; zip uses SharpZipLib before `UNITY_2021_1_OR_NEWER`. Must work on Windows,
  macOS and Linux editors.
- Branches: `develop` stages work and takes PRs; `main` is the release (v3.3.0 at `c90f59e`).
- Usually checked out as a git submodule at `Packages/com.veugeljame.builduploader` inside the host test
  project (JamesVeug/UnityBuildUploaderProject). Commit here first, then bump the gitlink there.
  Current branch state, CI results and open bugs with root causes are tracked in that project's
  `AgentDocs~/status.md`.

## Rules that are easy to break

1. **Never rename** serialized class names or `Serialize()`/`Deserialize()` keys. They are stored in
   `<ProjectRoot>/BuildUploader/*.json` (including the `AUploadModifer` spelling).
2. Run pipelines **only through `UploadTask`** (`StartAsync` / `Start` / `StartAndBlock`).
3. JSON only through the package `JSON` class; HTTP only through `RequestWrapper`.
4. **Secrets** never go in URLs (reports log them) or in JSON/serialized assets. Use `EditorPrefs` or
   `ProjectEditorPrefs`.
5. Config checks go in `TryGetErrors` / `TryGetWarnings` (run in Validation), not in `Upload()`. On a runtime
   failure call `stepResult.SetFailed(reason)` before returning `false`.
6. Types register by reflection: subclass plus `[UploadSource]`/`[UploadModifier]`/`[UploadDestination]`/
   `[UploadAction]`. Every non-abstract subclass in any loaded assembly is picked up; never register manually.
7. Keep optional integrations isolated. `com.unity.pipeline` code lives only in
   `Editor/Integrations/UnityPipeline` (own asmdef, `BUILD_UPLOADER_PIPELINE`); the wiki exporter needs
   `BUILD_UPLOADER_WIKI`. Core must compile without either.
8. Platform and process code goes through `BuildUtils` and `ProcessUtils`. Unity-internal `BuildProfile` APIs
   go through the reflection helpers in `BuildProfileWrapper`.
9. Split non-trivial classes into `.cs`, `.GUI.cs` and `.StringContextModifier.cs`. Call `FormatString` on
   every user string. Add `[Wiki(...)]` to user-facing types and fields.

## Map

| Area | Path |
| --- | --- |
| Pipeline core | `Editor/Core/UploadTask/` — `UploadTask.cs`, `Steps/`, `DownloadSources/`, `UploadModifiers/`, `UploadDestinations/`, `Actions/` |
| Utilities | `Editor/Core/Utils/` — `BuildUtils`, `ProcessUtils`, `IOUtils`, `ZipUtils`, `JSON/`, `StringFormatting/`, `BatchModeUtil` |
| Settings | `Editor/Core/Data/` — `Preferences`, `BuildUploaderProjectSettings`, settings providers |
| Services | `Editor/Services/<Name>Service/` (Slack is the reference) |
| Integrations | `Editor/Integrations/UnityPipeline/` |
| Wiki export | `Editor/Core/Wiki/` |
| Tests | `UnitTest/` — see `UnitTest/README.md` |

## Read before you change...

| Change | Read |
| --- | --- |
| `UploadTask`, steps, sources, modifiers, destinations, actions, context keys | `AgentDocs~/pipeline.md` |
| `BuildConfig`, Build Profiles, `ABuildSource`, platforms | `AgentDocs~/build-configs-and-platforms.md` |
| A service, settings UI, prefs/storage, HTTP, JSON | `AgentDocs~/services.md` |
| Batch mode or the `unity` CLI command | `AgentDocs~/cli-and-batch-mode.md` |
| `[Wiki]` attributes or wiki pages | `AgentDocs~/wiki.md` |
| Tests | `UnitTest/README.md` |

## Verifying a change

- Compile against the oldest and newest editors you touched behaviour for (2019.4 and Unity 6 at minimum).
- Run the credential-free core suite `BuildUploader.UnitTest.Core` (EditMode, a few seconds). From the host
  project use `python .ci/run_tests.py --suites core ...`; see the host project's `.ci/README.md`.
- **Every fix lands with a test**, in `UnitTest/Core/` when it needs no credentials (fake external tools as
  `FakeSteamSdk` does). If it needs credentials, a live service, an interactive terminal or a person, add a
  row to `UnitTest/UntestedFixes.md` saying what would make it testable.

## Commit messages

One line, with no body and no trailers.

- A fix says what it fixes and briefly how the bug happens, e.g. `Fixed Windows paths in saved JSON reloading
  wrong (backslashes were written unescaped, so C:\new_build loaded with a newline)`.
- A feature says what is added and what it is for, e.g. `Added Upload Number project setting to track how
  many uploads have been done`.
