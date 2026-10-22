# Build configs, Build Profiles and platforms

Read this before touching `BuildConfig`, `BuildProfileWrapper`, `ABuildSource`, `BuildConfigSource`,
`BuildProfileSource` or `BuildUtils`.

Two interchangeable ways describe "how to build the player". Both implement `IBuildConfig`
(`GetBuildName`, `GetGUID`, `GetTargetPlatform`, `GetTarget`, `GetTargetPlatformSubTarget`,
`GetTargetArchitecture`, `GetSceneGUIDs`, `GetProductName`, `GetScriptingBackend`, `GetBuildOptions()`,
`GetProductExtension()`, `GetFormattedProductName(ctx)`, `ApplySettings(switchPlatform, ctx, stepResult)`),
so `ABuildSource<T>`, validation and the pipeline do not care which one the user picked.

## `BuildConfig` — the package's own config (all Unity versions)

`Editor/Core/UploadTask/BuildConfig.cs`. A serializable POCO authored in
**Project Settings → Build Uploader → Build Configs** (`ProjectSettings_BuildConfigs`), persisted in
`<ProjectRoot>/BuildUploader/BuildConfigs.json`. Load via `BuildConfigsUIUtils.GetBuildConfigs()`,
`BuildConfig.FromGUID(guid)` or `BuildConfig.FromBuildName(name)`. "Debugging Build" and "Release Build"
are written the first time a project opens the settings page.

Fields: scenes (`SceneGUIDs`), product name, `ExtraScriptingDefines`, dev-build flags (`IsDevelopmentBuild`,
`BuildScriptsOnly`, `AllowDebugging`, `ConnectProfiler`, `EnableDeepProfilingSupport`), platform
(`SwitchTargetPlatform`, `TargetPlatform`, `TargetPlatformSubTarget`, `Target`, `TargetArchitecture`) and
player settings (`StackTraceLogTypes`, `StrippingLevel`, `ScriptingBackend`, `CompressionMethod`).
`ApplySettings()` writes all of them to `PlayerSettings` / `EditorUserBuildSettings` /
`EditorBuildSettings.scenes` and optionally switches platform via `BuildUtils.TrySwitchPlatform`.

- `ExtraScriptingDefines` **replaces** the target's define list (`PlayerSettings.SetScriptingDefineSymbols`),
  despite the name. A changed list triggers a recompile and domain reload, which destroys the running
  in-memory `UploadTask` (issues #107/#108).
- `BuildConfig` implements `DropdownElement`: `Id` is popup order and is rewritten on reorder; `GUID` is the
  stable 6-char identifier serialized into `BuildConfigSource`. **Never reuse a deleted GUID.**
- `BuildConfigSource` serializes an optional platform override (`m_OverrideSwitchTargetPlatform` +
  `m_Target` / `m_TargetPlatform` / `m_TargetPlatformSubTarget` / `m_TargetArchitecture`) that beats the
  config's own `SwitchTargetPlatform`. `GetBuildConfigToApply()` deep-clones so the saved config is never mutated.

## `BuildProfile` — Unity's asset (Unity 6000.0+ only)

Wrapped read-only by `BuildProfileWrapper : IBuildConfig, DropdownElement` (gated on
`UNITY_6000_0_OR_NEWER`). The package never authors or persists `BuildProfile` assets; it references them
by `GUID`.

- Discovery: `BuildUtils.GetAllCustomBuildProfiles()` version-gates between `BuildProfile.GetAllBuildProfiles()`
  (6000.5+), `BuildProfileModuleUtil.GetAllBuildProfiles` (6000.3+) and `BuildProfileDataSource.FindAllBuildProfiles`
  (earlier 6000.x).
- The wrapper reads `playerSettings`, `platformBuildProfile`, `buildTarget`, `subtarget` and dev-build flags by
  reflection. These internals move between 6000.x point releases, so new access must go through the
  existing reflection helpers in `BuildProfileWrapper`.
- `BuildProfileUIUtils` caches wrappers, refreshes on `[DidReloadScripts]`, and subscribes to
  `BuildProfile.AddOnBuildProfileCreated` by reflection so the dropdown follows new profiles.
- `BuildProfileSource.ApplyBuildConfig` is a no-op (Unity owns settings); `MakeBuild` calls
  `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)`, so the platform switch happens inside `BuildPlayer`.
  Override behaviour (platform, product name, scenes) belongs in `BuildConfigSource`, not the profile path.

## `ABuildSource<T>` — shared base

`Editor/Core/UploadTask/DownloadSources/ABuildSource.cs` owns the build loop, the global single-build lock
(`BuildUtils.WaitForTurnToBuild` / `ReleaseBuildLock`, `m_lock` semaphore + `ChangeRunningBuilds`),
pre/post editor-settings caching, `BuildOptions.DetailedBuildReport` / `CleanBuildCache` plumbing, and
forwarding `report.steps` into `stepResult`. Subclasses implement `GetBuildConfigToApply()`,
`CompareBuildConfig(IBuildSource)`, `SerializeBuildConfig` / `DeserializeBuildConfig`, and optionally
override `ApplyBuildConfig` and `MakeBuild`.

`TryGetErrors` (Validation) blocks a build when the config is null, scenes are missing or unresolvable,
`BuildUtils.GetBuildPlatform()` returns null, the platform is not installed or not supported, or another
active `UploadTask` is building the same `IBuildConfig` (via `CompareBuildConfig`).

Known gap: `PreApplyBuildConfig` never snapshots editor settings, so the post-build restore does not run
and a BuildConfig's defines stay applied after the build.

### Build Addressables

`ABuildSource` serializes `BuildAddressables` and `CleanAddressables` (missing keys mean false; kept when
Addressables is not installed). Core cannot reference Addressables, so it talks to the optional
`Editor/Integrations/Addressables` assembly through `IAddressablesSupport` / `AddressablesSupport.Implementation`,
which that assembly sets from an `[InitializeOnLoad]` constructor (also in batch mode). The integration needs
Addressables 1.19.4+ (first with `AddressableAssetSettings.PlayerBuildOption`) and Unity 2021.2+ (Addressables
only builds content inside a player build from there, through a `BuildPlayerProcessor`).

With the toggle on, `MakeBuildWithAddressables` optionally cleans content, then wraps `MakeBuild` in
`ForceBuildWithPlayer`: it sets the project's "Build Addressables on Player Build" to `BuildWithPlayer` and restores
it afterwards. It does **not** build the content itself before `MakeBuild`, because:

- Addressables only adds `Library/com.unity.addressables/aa/<Platform>` to a player if it exists when the build starts.
- A Build Profile only switches platform inside `BuildPlayer`, so content built before `MakeBuild` would be for the
  wrong platform. Inside the build, Addressables 2.x builds for the `BuildPlayerOptions` actually used, with no
  extra platform switch or domain reload, and honours `AddressablesPlayerBuildProcessor.BuildAddressablesOverride`.

The setter does not mark the settings dirty, so the file normally stays untouched; if the build saved the settings,
the scope saves them again with the original value. A failed content build throws `BuildFailedException` in
Addressables 2.x+ but is only logged in 1.x, so the scope watches the log for "Failed to build Addressables content"
and fails the step; `GetSource` then fails even when the player build succeeded.

## Platforms

There is no hardcoded platform list. `BuildUtils.ValidPlatforms` is built lazily by reflection over
`BuildPlatforms.instance.buildPlatforms`, so every `BuildTargetGroup` with an installed module is
available. Route platform-specific behaviour through `BuildUtils`, not `switch` ladders in services or sources.

- **Standalone is split per OS.** `GetValidPlatforms()` clones Standalone into `StandaloneWindows64`,
  `StandaloneOSX`, `StandaloneLinux64`, each with its own `installed` / `supported` flags.
  `GetPlatformExtension` maps them to `.exe`, `.app`, `.x86_64`.
- **Architecture is Standalone-only.** `BuildUtils.Architecture` is `x64 | ARM64 | x64ARM64 | x86`
  (`x64ARM64` macOS-only, `x86` Windows-only). `DrawArchitecturePopup` returns the input unchanged for
  other groups.
- **Android** extension is `.aab` with the bundle flag, else `.apk`. `BuildProfileWrapper` reads
  `buildAppBundle` by reflection; `BuildConfig.GetProductExtension` hardcodes `androidBundle: false`.
- **Module checks.** `IsTargetGroupInstalled` / `IsTargetGroupSupported` are the canonical probes; on
  6000.0+ `IsTargetGroupSupported` calls `BuildPipeline.IsBuildPlatformSupported` by reflection.
  `TrySwitchPlatform` requires a non-zero `TargetPlatformSubTarget` for Standalone (2021.1+), non-`-1` on 6000.0+.
- **Host OS.** External tools (SteamCMD, Butler, ...) run through `ProcessUtils`, which works on Windows,
  macOS and Linux and makes SDK tools executable on Unix. Zip uses `System.IO.Compression` on 2021.1+ and
  `com.unity.sharp-zip-lib` before that (`ZipUtils`).
