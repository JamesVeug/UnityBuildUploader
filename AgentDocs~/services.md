# Services, settings storage, HTTP and JSON

Read this before adding or changing a service (Slack, Discord, Steam, itch.io, ...) or anything that stores
settings, makes HTTP calls or serializes JSON. **Slack is the reference implementation.**

## Folder layout per service

```
Editor/Services/<Name>Service/
  <Name>Service.cs                          AService: ServiceName, SearchKeywords, IsReadyToStartBuild, IsProjectSettingsSetup
  <Name>Service.Preferences.cs              Edit > Preferences > Build Uploader > Services > <Name>
  <Name>Service.ProjectSettings.cs          Project Settings > Build Uploader > Services > <Name>
  <Name>Service_Preferences_Provider.cs     SettingsProvider (SettingsScope.User)
  <Name>Service_ProjectSettings_Provider.cs SettingsProvider (SettingsScope.Project)
  Data/<Name>Config.cs                      Serializable root (servers/spaces/channels)
  Data/<Name>UIUtils.cs                     GetConfig(), Save(), popup helpers
  Data/<Name><Entity>.cs                    Each entity implements DropdownElement
  API/<Name>.cs                             Static partial class, HTTP calls only, no UI
  Actions/<Name><Verb>ChannelAction(.GUI|.StringContextModifier).cs
  ReorderableListOf<Name><Entities>.cs      InternalReorderableList<T> subclasses
```

### Adding a service

1. Create the layout above. Subclass `AService` — discovered automatically, no registration.
2. `<Name>Config` with entities implementing `DropdownElement`; `<Name>UIUtils` with `GetConfig()`, `Save()`, popups.
3. API class using `RequestWrapper` only.
4. Actions/destinations with `[UploadAction("...")]` / `[UploadDestination("...")]`.
5. Two `SettingsProvider` classes (Preferences and Project Settings).
6. `[Wiki(...)]` on user-facing types and fields (see `wiki.md`).

## `DropdownElement`

```csharp
public interface DropdownElement { int Id { get; } string DisplayName { get; } }
```

- `Id` is stable and used for serialization and equality. `InternalReorderableList` passes
  `NextItemIndex(list)` (highest existing `Id` + 1) to `CreateItem`, which uses it as the new `Id`.
  **Never reuse an `Id`**: destinations look entities up by `Id`, so duplicates collapse into one (#115).
  `NextItemIndex` can still reuse the `Id` of a deleted last entry.
- `DisplayName` is UI only and can change. `GenericMenu` cannot show two identical items, so when names may
  repeat pass a unique label to `EditorUtils.DrawPopup(..., menuLabel, ...)` (depots use
  `SteamDepot.DisplayNameWithDepotID`).
- Popups: `CustomDropdown<T>` (single) or `CustomMultiDropdown<TParent,TChild>` (hierarchical);
  `DrawPopup(ref T value, Context ctx, GUILayout.Width(n))` returns `isDirty`.

## `InternalReorderableList<T>`

Every service lists its entities through a subclass of `InternalReorderableList<T>`
(`Editor/Core/Utils/InternalReorderableList.cs`), which standardises foldout header, add/remove/reorder,
the right-click menu (Clear, Sort) and the `dirty` flag. Never use `ReorderableList` directly.

```csharp
public class ReorderableListOfSlackChannels : InternalReorderableList<SlackConfig.SlackChannel>
{
    protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused) { ... } // set dirty = true on change
    protected override SlackConfig.SlackChannel CreateItem(int index) => new SlackConfig.SlackChannel(index, "BotTestChannel", "");
    protected override int CompareTo(SlackConfig.SlackChannel a, SlackConfig.SlackChannel b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
}

m_channelsList = new ReorderableListOfSlackChannels();
m_channelsList.Initialize(config.Channels, "Channels", foldoutStartsOpen: true);
if (m_channelsList.OnGUI()) SlackUIUtils.Save();   // true when dirty
```

- One subclass per entity type; if Preferences and Project Settings render it differently (e.g. masking
  secrets) make two (`ReorderableListOfSlackAppsPreferences` vs `...ProjectSettings`).
- Rows are rect-based (`GUI.*`); `EditorGUILayout` only inside the header `HorizontalScope`.

## Where settings live

| Tier | Where | For |
| --- | --- | --- |
| `EditorPrefs` | Per machine | Secrets shared by every project on the machine (tokens, API keys, passwords) |
| `ProjectEditorPrefs` | Per machine, keys prefixed with the project UUID in `<ProjectRoot>/BuildUploader/ProjectID.txt` | Secrets and flags for *this* project on *this* machine |
| JSON config | `<ProjectRoot>/BuildUploader/<ServiceName>Config.json` | Non-secret, shareable config (server names, channel IDs) |

`ProjectEditorPrefs` mirrors `EditorPrefs` (`Get/SetBool|Int|String|Float`, `DeleteKey`, `HasKey`) plus
`MigrateFromEditorPrefs(key, PrefType)`. **Tokens and API keys never go in JSON config or serialized assets.**
Prefer `ProjectEditorPrefs` unless the credential really is machine-wide.

## HTTP — always `RequestWrapper`

Never use `HttpClient` or `UnityWebRequest` directly.

```csharp
using (RequestWrapper www = RequestWrapper.Post(url))   // also .Get, .Delete, .Patch
{
    www.SetJSONData(body);                              // Dictionary<string, object>
    www.SetRequestHeader("Authorization", $"Bearer {token}");
    RequestResult response = await www.SendAsync(result, true);
    if (!response.IsSuccessful) { /* handle */ }
    // response.Data -> JSON string; response.Bytes -> byte[]
}
```

**Never put secrets in the URL.** `RequestWrapper` logs the request line into the `UploadTaskReport`, and
reports get pasted into issues. If the API forces a secret into the URL (webhooks, signed URLs), mask that
segment as `xxx-xxx-xxx` before it reaches `AddLog`. Prefer headers; they are not logged.

## JSON — always the package `JSON` class

`Editor/Core/Utils/JSON/`. Never `JsonUtility` (cannot handle `Dictionary<string, object>` or polymorphic
lists) or Newtonsoft (the package has no dependencies).

```csharp
string json = JSON.SerializeObject(data);
MyType data = JSON.DeserializeObject<MyType>(json);
object data = JSON.DeserializeObject(json, type);
```

Used by every `Serialize()` / `Deserialize()`, the `<ProjectRoot>/BuildUploader/*.json` files and
`RequestWrapper.SetJSONData`. Numbers deserialize as `long`/`double` — cast accordingly (`(Enum)(long)value`).
