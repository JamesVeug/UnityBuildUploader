# Optional Addressables integration

This editor assembly is enabled only when `com.unity.addressables` 1.19.4 or newer is installed on Unity 2021.2 or
newer. Its package version define (`BUILD_UPLOADER_ADDRESSABLES`) and assembly constraints keep the core Build
Uploader usable without Addressables. Earlier versions cannot build Addressables content inside a player build.

`AddressablesIntegration` sets `AddressablesSupport.Implementation` in core when scripts load, which enables the
**Build Addressables** toggle on the Build Config and Build Profile sources. With the toggle on, the source sets
Addressables' own "Build Addressables on Player Build" to *Build With Player* for that one build and restores it
afterwards, so Addressables builds the content inside `BuildPipeline.BuildPlayer` for the target being built.

`BuildAddressablesAction` builds content for the editor's active build target, for content-only uploads.

The core editor assembly must not reference this assembly or Addressables. Installing or removing Addressables
enables or disables this integration after Unity recompiles.
