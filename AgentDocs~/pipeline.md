# Upload pipeline

Read this before changing `UploadTask`, its steps, or any source, modifier, destination or action.

## Shape

An `UploadConfig` holds four ordered lists, each serialized to JSON in
`<ProjectRoot>/BuildUploader/UploadProfiles/*.json`:

```
Sources       what to upload (folder, file, URL, a fresh player build, the last build)
Modifiers     transform the cached copy (exclude files/folders, compress, decompress)
Destinations  where it goes (local path, Steam, itch.io, Dropbox, ...). All run in parallel.
PostActions   per-config actions after its upload (Discord/Slack/Google Chat message, ...)
```

An `UploadProfile` is a named list of configs plus task-level actions.
**Class names and serialization keys are written to JSON — never rename them.**

## `UploadTask` — the only orchestrator

`Editor/Core/UploadTask/UploadTask.cs` owns execution: step sequence, `UploadTaskStringFormatterContext`
wiring into every config/action, the upload-number bump, `ProgressUtils` progress, and the `UploadTaskReport`.
Never reimplement this elsewhere.

```csharp
UploadTask task = new UploadTask(profile);                 // preferred: picks up task actions too
UploadTask task = new UploadTask("My Upload", configs, actions);
task.AddConfig(extraConfig);
task.AddAction(action, UploadCompleteStatus.IfFailed, new List<UploadTrigger> { UploadTrigger.OnTaskFinished });
task.SetBuildDescription("...");
task.OnComplete += report => { /* report.Successful, report.GetReport() */ };

task.Start();             // fire and forget
await task.StartAsync();  // continuations need the main-thread sync context
task.StartAndBlock();     // batch mode only; blocks the calling thread
```

Load profiles with `UploadProfile.FromGUID(guid)`, `UploadProfile.FromProfileName(name)` or
`UploadProfile.FromPath(path)`.

## Steps

`AUploadTask_Step.StepType`, executed in order (renaming or reordering breaks saved reports):

| Step | Runs after an earlier failure? | Notes |
| --- | --- | --- |
| Validation | yes | Creates each config's cache folder, then `config.GetAllErrors()` / warnings |
| PreUploadActions | yes | Task actions with trigger `OnTaskStarted`. Fails, so the "no" steps are skipped, when an action with `StopTaskIfFailed` fails |
| PrepareSources | no | |
| GetSources | no | Builds happen here (`ABuildSource`) |
| CacheSources | no | Copies sources into the cache; `IgnoreFileDuringCacheSource` filters |
| ModifyCacheSources | no | `AUploadModifer.ModifyBuildAtPath` on the cache |
| PrepareDestinations | no | `Prepare(..., taskContentsFolder, ...)` gets the cache path |
| Upload | no | Destinations in parallel, then each config's `PostActions` with the upload result |
| PostUploadActions | yes | Task actions with trigger `OnTaskFinished` |
| Cleanup | yes | Deletes cache (if preferred), then `CleanUp` on every enabled source, destination and action |

Steps are global across configs: a failure in any config before Upload skips the remaining
"no" steps for every config. A failed destination does **not** stop sibling destinations.
Task actions with `AfterEachStepCompletes` fire after every step that runs, except Validation and the
two action steps. If one with `StopTaskIfFailed` fails, the remaining "no" steps are skipped.

## Result, failure and cancellation

- `StepResult.SetFailed(reason)` marks the report failed; `IsSuccessful` is taken from the report.
  A step returning `false` without a reason gets a generic one, but always give a real reason.
- `Cancel()` is cooperative: steps check the token between operations, an in-flight upload finishes,
  then the task ends with `IsCancelled`, `Report.Cancelled` and `IsSuccessful == false`. Cleanup and
  `IfFailed` actions still run. Cancelling a completed task does nothing.
- Action filters: `UploadCompleteStatus` (`Always`, `Never`, `IfSuccessful`, `IfFailed`) combined with
  `UploadTrigger` (`OnTaskStarted`, `AfterEachStepCompletes`, `OnTaskFinished`). `CleanUp` runs for every
  action that is not `Never` and has a trigger, whatever the result.
- An action fails when `Prepare` or `Execute` returns false or throws, or its `StepResult` is failed.
  By default that only logs (and fails the report if the action called `SetFailed`); later steps still run.
  With `UploadActionData.StopTaskIfFailed` (JSON `stopTaskIfFailed`, missing means false) `UploadTask.ExecuteActions`
  fails the result and returns false: `OnTaskStarted` stops the build and upload, `AfterEachStepCompletes`
  skips the remaining "no" steps, and a config post-action fails the Upload step. Cleanup and `OnTaskFinished`
  actions still run. Picking an action in the GUI (or `new UploadActionData(action, ...)`) copies
  `AUploadAction.DefaultStopTaskIfFailed`, which actions that later steps depend on override to true.
- Past runs appear in **Window → Build Uploader → Open Upload Tasks Window**. There is no completion dialog
  outside the GUI. To dry-run sources and modifiers without touching a service, use `NoUploadDestination`
  ("Nowhere") and turn off cache deletion.
- The report is saved (if `Preferences.AutoSaveReportToCacheFolder`) only after all steps finish.
  A domain reload mid-task (e.g. a BuildConfig changing scripting defines) loses the task with no
  report — open issue #107.

## Preferences that change behaviour

| Preference | Effect |
| --- | --- |
| `UseLocalDestinationIfAvailable` (default on) | The first non-zipping `LocalPathDestination` becomes the cache; it is wiped in Validation if it exists and is not cleaned up afterwards |
| `DeleteCacheAfterUpload` (default on) | Cleanup deletes task cache folders it created |
| `CacheFolderPath` | Cache root; default `Application.persistentDataPath/BuildUploader/CachedBuilds` |
| `AutoDecompressZippedSourceFiles` (default on) | A `.zip` file source is extracted into the cache |
| `AutoSaveReportToCacheFolder` (default off) | Writes the report for the Upload Tasks window |

Per-source `DuplicateFileHandling` defaults to `Error`: two sources writing the same file fail CacheSources.
Every task start bumps `TotalUploadTasksStarted` and every build bumps `LastBuildNumber` in
`<ProjectRoot>/BuildUploader/ProjectSettings.json`, unless **Skip Number Updates**
(`BuildUploaderProjectSettings.SkipBuildAndUploadNumberUpdates`) is on (#109).

## Base classes

- **`AUploadSource`** — `Summary()`, `Prepare()`, `GetSource(doNotCache, ...)`, `SourceFilePath()`,
  `CleanUp()`, `TryGetErrors`/`TryGetWarnings`, `Serialize`/`Deserialize`, `PrepareContextForCaching()`.
  Tag with `[UploadSource("Display Name")]`.
- **`AUploadModifer`** — historical single-`i` spelling, written to JSON. `ModifyBuildAtPath(cachedFolderPath,
  uploadConfig, configIndex, stepResult)`, optional `IgnoreFileDuringCacheSource(...)`, three
  `TryGetErrors`/`TryGetWarnings` overloads (config, source, destination). Tag with `[UploadModifier(...)]`.
- **`AUploadDestination`** — `Summary`, `Prepare()`, `Upload()`, `PostUpload()`, `CleanUp()`,
  `Serialize`/`Deserialize`. Tag with `[UploadDestination(...)]`.
- **`AUploadAction`** — `Prepare()`, `Execute()`, `CleanUp()`, `TryGetErrors`, `TryGetWarnings`,
  `Serialize`/`Deserialize`. Tag with `[UploadAction(...)]`.

Built-in actions with no service: `InvokeStaticMethodAction` (core) calls a project's static method by path
(`Namespace.Type.Method`, `+` for nested types, `Namespace.Type, Assembly.Method` to pick an assembly; private
methods allowed; `()` or `(UploadTaskReport.StepResult)`; returns `void`, `bool` or `int`, a `Task` or UniTask of
one, or an `IEnumerator` run as a coroutine one step per editor update; false or an int other than 1 fails).
UniTask is optional: it is recognised by name and awaited through its own `UniTaskExtensions.AsTask`, so core
never references it. It resolves in `TryGetErrors` and caches the `MethodInfo` per formatted path until a domain
reload. `BuildAddressablesAction` (`Editor/Integrations/Addressables`) builds Addressables content for the
active build target; for content in a player use the source toggle (see `build-configs-and-platforms.md`).
Both default to `StopTaskIfFailed`.

Copy-paste reference implementations live in the wiki: `How-to-Create-a-new-Source.md` (`LastBuildSource`),
`How-to-Create-a-new-Modifier.md` (`CompressModifier`), `How-to-Create-a-new-Destination.md` (`LocalPathDestination`).

## Destination upload contract

Write `Upload()`, `Prepare`, `PostUpload` and `CleanUp` as if every precondition holds. Anything that can
be checked from the serialized config alone (missing token, empty channel ID, unresolvable `{key}`,
invalid path) belongs in `TryGetErrors(List<GUIContent>)` (blocks the task) or
`TryGetWarnings(List<GUIContent>, Context)`; both run in Validation before anything is fetched.
Only failures that need the remote service stay in `Upload` and are reported through `stepResult`.

## Registration by reflection

`InternalUtils.FetchAllTypes()` scans every loaded assembly and registers **every non-abstract subclass**
of `AUploadSource`, `AUploadModifer`, `AUploadDestination`, `AUploadAction` and `AService`. The attribute
only supplies the display name. Never register types manually. Test doubles in `UnitTest/` are
discovered too, so they appear in pickers of projects that list the package in `testables`.

Saved profiles name each type by `Type.FullName` (no assembly). Load it with `InternalUtils.FindType`, never
`Type.GetType`, which only searches the core assembly and mscorlib: `FindType` also searches every loaded assembly
(the same `AssemblyUtils.GetAssemblies()` registration scans), so integrations, test doubles and users' own types reload.

## Partial class conventions

| Suffix | Purpose |
| --- | --- |
| `.cs` | Fields, Execute/Upload, TryGetErrors, Serialize, Deserialize |
| `.GUI.cs` | `OnGUICollapsed` and `OnGUIExpanded` |
| `.StringContextModifier.cs` | Registers `{key}` values the type produces for later consumers |

## Context / format strings

`Context` resolves `{key}` tokens in user strings. Keys are constants in
`Editor/Core/Utils/StringFormatting/Context.Static.cs`.

- `context.AddCommand(key, getter, tooltip)` registers a key; tooltips feed the generated `StringFormatter.md`.
- Call `m_context.FormatString(str)` on every user-authored string before use.
- Contexts chain: source/modifier/destination/action -> config -> task. `UploadTask.StartAsync` caches
  values once at the start (`CacheCallbacks`), so `{taskProfileName}` etc. are stable for a run.
