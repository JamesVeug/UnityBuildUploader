# CLI and batch mode

Read this before touching `BatchModeUtil`, `PipelineCommands` or anything that starts tasks without the UI.
The code on `develop` and the planned design differ. Check which one you are changing.

## On develop today

### Batch mode — `Editor/Core/Utils/BatchModeUtil.cs`

```
"PATH/TO/Unity" -batchmode -quit -projectPath "PATH/TO/PROJECT" -logFile - \
    -executeMethod Wireframe.BatchModeUtil.Execute -uploadProfile "PROFILE_GUID_OR_NAME" [-async]
```

- Each `-uploadProfile <GUID or name>` pair starts one `UploadTask(profile)`; repeat the pair for more.
- Blocks with `task.StartAndBlock()` unless `-async` is passed. Blocking deliberately reuses
  `StartAndBlock`: `StartAsync` awaits `Task.Yield()` and posts continuations to the main thread, so a
  home-grown wait loop can deadlock.
- Logs an error if no task started. It does **not** set a process exit code on failure.

### Unity Pipeline CLI — `Editor/Integrations/UnityPipeline/`

`PipelineCommands.cs` exposes one command, `build_uploader`, to the `unity` CLI (`com.unity.pipeline`), which
runs it against an **already-open Editor** over a local HTTP server.

- Own assembly `VeugelJame.BuildUploader.Editor.UnityPipeline`, compiled only when `com.unity.pipeline` is
  installed (`versionDefines` → `BUILD_UPLOADER_PIPELINE`). Core code must never reference it; the package
  has to compile and behave the same without Pipeline.
- Pipeline command names are global and the first registration wins, so the package claims exactly one
  name. Add arguments, never a second `[CliCommand]`.
- Pipeline 0.8 moved `[CliCommand]` / `[CliArg]` into `Unity.Pipeline.Attributes`; the asmdef still
  references only `Unity.Pipeline` (PR #113 was closed pending this).
- The command is dispatched on the main thread the task needs, so it starts tasks without blocking. Callers
  poll `--active_tasks`, then read `--summarize_tasks` / `--open_tasks`.

```bash
unity command build_uploader --profiles true
unity command build_uploader --verify_profiles all --summarize_profiles all
```

Arguments, in group order: **Listing** (`profiles`, `active_tasks`, `source_types`, `modifier_types`,
`destination_types`, `action_types`, `reports`, `cache_summary`) → **Inspection** (`verify_profiles`,
`verify_tasks`, `summarize_profiles`, `summarize_tasks`, `open_tasks` + `errors_only`) → **Running**
(`start_tasks`, `dry_run_tasks`, `cancel_tasks`) → **Mutations** (`clone_profiles` + `new_name`,
`clone_tasks`, `delete_profiles`, `delete_tasks`, `clear_cache`, `export_wiki`) → **Safety** (`confirm`).

- Profiles are addressed by GUID or name; tasks by GUID. `all` (also `true`, `*`) expands per operation.
- `dry_run_tasks` clones the profile in memory and swaps every destination for `NoUploadDestination`.
- `delete_profiles`, `delete_tasks`, `clear_cache` refuse to run without `confirm`. New destructive
  arguments must do the same. A modal dialog is never shown when nobody is at the Editor.
- Result field names are the JSON keys callers see: keep them lower camel case and treat renames as breaking.

## Planned, not on develop yet: one shared command layer

Design notes for moving all logic out of `PipelineCommands.cs` so batch mode reaches the same commands.
Not merged as of 2026-10-19. Do not assume these files exist.

- `Editor/Core/Utils/BuildUploaderCommands*.cs`, one `internal static partial class` in three files:
  `.Args.cs` (name/description `const`s, `CommandArg`, `Args`, `CommandRequest`, `BuildUsageString`),
  `.cs` (`Execute(CommandRequest)` and every operation), `.Results.cs` (result classes).
- `PipelineCommands` and `BatchModeUtil` become thin parsers into `CommandRequest`. The shared files must
  not be gated or reference `Unity.Pipeline`. Everything stays in `VeugelJame.BuildUploader.Editor`
  (`UploadTask.AllTasks` is internal).
- Adding an operation: `NameArg` + `NameDescription` consts (descriptions must be `const` for `[CliArg]`), a
  `CommandArg` in `Args` in the right group, a handler in `Execute` writing its own result key, and a
  `[CliArg]` parameter on `PipelineCommands.Command(...)` using the same consts.
- Batch mode: `-start_tasks` (GUID, name, several, or `all`) replaces `-uploadProfile` (kept as a deprecated
  alias); blocking unless `-async`; exit code 1 (guarded by `Application.isBatchMode`) on a failed task,
  unknown GUID, refused destructive op or no operation; result JSON fenced between
  `[BuildUploader] BEGIN_RESULT` and `[BuildUploader] END_RESULT`.
- Multi-value args arrive as `"a,b"`, `"a b"` or a JSON array; normalise with `SplitArgument`.
  `ExpandTasks` takes `TaskScope.Known` (memory + saved reports), `InMemory` (`clone_tasks`) or
  `Running` (`cancel_tasks`). Live tasks win over saved reports.
- `CLI.md` would be generated from `BuildUploaderCommands.Args` rather than `[CliArg]` attributes, so the
  page is complete without Pipeline installed.
