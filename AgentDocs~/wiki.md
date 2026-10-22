# Wiki export

Read this before editing `[Wiki]` attributes, `Editor/Core/Wiki/`, or any page of the GitHub wiki
(https://github.com/JamesVeug/UnityBuildUploader/wiki).

## Source of truth

- `Editor/Core/Wiki/WikiAttribute.cs` defines `[Wiki(name, subpath?, text, order?)]` and `[WikiEnum(...)]`.
  `SubPath` is one of `"sources"`, `"modifiers"`, `"destinations"`, `"actions"`.
  `TryGetWikiLink(type, out url)` drives the in-editor "?" buttons.
- `Editor/Core/Wiki/Wiki.cs` (only compiled with `BUILD_UPLOADER_WIKI`) adds
  **Tools → Build Uploader → Export Wiki Data** and **Open Wiki Export Folder**.
- Export rewrites `<HostProject>/Wiki/` (`Application.dataPath + "/../Wiki"`), the folder next to the
  host project's `Assets/`.

Decorate every new source, modifier, destination or action and its user-relevant fields with `[Wiki(...)]`.
To change generated text, edit the attribute or the `AddCommand` tooltip, never the generated markdown.

## Generated pages

| Page | Generated from | Preserved |
| --- | --- | --- |
| `Sources.md`, `Modifiers.md`, `Destinations.md`, `Actions.md` | `[Wiki]` attributes grouped by `SubPath` | Everything above `## Sources` / `## Modifiers` / `## Destinations` / `## Actions` |
| `StringFormatter.md` | `Context` commands and their tooltips | Nothing (fully regenerated) |
| `CLI.md` | `[CliCommand]` / `[CliArg]` attributes, by `Editor/Integrations/UnityPipeline/PipelineWiki.cs` via `Wiki.ExportAdditionalPages` | Everything above `## Commands` |

`CLI.md` is only regenerated when both `BUILD_UPLOADER_WIKI` and `BUILD_UPLOADER_PIPELINE` are defined.
`TryGetHandAuthoredPreamble(path, header, defaultPreamble, out preamble)` keeps the hand-written part above
the header; it creates a missing file from the default and leaves a file untouched (returns false) when the
header is missing. New generated pages should use it.

## Hand-authored pages (edit directly)

- `Home.md` — landing page and pipeline semantics not encoded in code.
- `How-to-Install.md` — UPM install (Asset Store or `https://github.com/JamesVeug/UnityBuildUploader.git`;
  `com.unity.sharp-zip-lib` for Unity 2020 and below).
- `How-to-Setup.md` and `How-to-Setup-<Service>.md` — first-run and per-service setup.
- `How-to-Create-a-new-Source.md` / `-Modifier.md` / `-Destination.md` — reference implementations
  (`LastBuildSource`, `CompressModifier`, `LocalPathDestination`). Mirror them when scaffolding.
- `Starting-a-BuildTask-without-UI.md` — batch mode (`-uploadProfile`), generated menu items
  (**Tools → Build Uploader → Quick Upload → Generate Menu Items**, written under `Assets/ThirdParty`)
  and the C# API.
- `Roadmap.md` — points at GitHub issues.

## Workflow

The wiki is a separate git repo, usually cloned as `Wiki/` beside the host project's `Assets/`
(on the maintainer's machine: `C:\GitProjects\BuildUploader_U6000_3\Wiki`). It is often outside the
working session. Confirm write access before editing it, and ask for access rather than writing wiki pages
into the package or a scratch folder.

1. Edit code and `[Wiki]` attributes.
2. Run **Export Wiki Data** in the host project.
3. Review the diff under `Wiki/` and commit it to the wiki repo.
