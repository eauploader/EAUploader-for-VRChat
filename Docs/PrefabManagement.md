# Prefab Management

## Managed Prefab Variants

EAUploader never modifies the Prefabs a user imports. For every imported Prefab it creates a **Prefab Variant** and manages only that Variant.

```
Assets/EAUploader/Prefabs/<ItemName>/<VariantName>.prefab
```

- `ManagedPrefabs.ROOT` = `Assets/EAUploader/Prefabs`.
- One folder per imported item. Folder and file names are sanitized (invalid file name characters become `_`); duplicates get unique names via `AssetDatabase.GenerateUniqueAssetPath`.
- A Variant is created by instantiating the source Prefab in a temporary preview scene, adding/configuring `EAUploaderMeta`, and saving it with `PrefabUtility.SaveAsPrefabAsset`. The open scene is not touched.
- `EAUploaderMeta` exists only on the Variant.
- Additional assets that EAUploader generates for an item (for example by edit tools) are stored inside the item folder.

### Key operations (`ManagedPrefabs`)

| Method | Behavior |
| --- | --- |
| `IsManaged(path)` | `true` for paths under `ROOT/` |
| `IsInEAUploaderFolder(path)` | `true` for paths under `Assets/EAUploader/` (excluded from import) |
| `CreateVariant(sourcePath, configure)` | Creates a Variant in a new item folder |
| `DuplicateVariant(variantPath)` | Copies the Variant inside the same item folder; the copy is still a Variant of the same source |
| `GetItemFolder(variantPath)` | Item folder of a Variant |
| `GetSourcePath(variantPath)` | Base Prefab of a Variant |
| `GetAllVariantPaths()` / `FindVariantsOf(source)` / `HasVariantOf(source)` | Lookup |
| `DeleteItemFolderIfEmpty(folder)` | Removes an empty item folder |
| `RenameItemFolder(variantPath, name)` | Renames the item folder when it contains only that Prefab |

## Metadata (`EAUploaderMeta`)

Runtime component (`Runtime/Components/EAUploaderMeta.cs`), `[DisallowMultipleComponent]`, derived from `AvatarTagComponent` which implements `VRC.SDKBase.IEditorOnly` so it is stripped from uploads.

| Field | Type | Description |
| --- | --- | --- |
| `status` | `Show`, `Hidden`, `Pinned` | Visibility in the list |
| `type` | `VRChat`, `VRM`, `Other` | Detected from components (`VRC_AvatarDescriptor` → `VRChat`, `VRMMeta` → `VRM`) |
| `genre` | `Avatar`, `Cloth`, `Accessory`, `Other` | User-facing category |
| `lastModifiedTicks` | `long` (UTC ticks) | Time EAUploader imported / refreshed the item. `0` falls back to the file timestamp. |
| `genreConfirmed` | `bool` | Whether the genre was chosen at import |
| `memo` | `string` | Free-text memo |
| `tags` | `List<string>` | Tags attached to the item |

`EAUploaderMeta` is only read from the root of a managed Prefab. Outfits put on by Dress Up (Compatible Outfits) carry `EAUploaderOutfit` instead (see [EditTools.md](EditTools.md#dress-up-compatible-outfits)).

`PrefabInfo` (`CustomPrefabUtility.cs`) is the cached, UI-facing snapshot of a managed Prefab (path, name, last modified, type, status, genre, tags, memo, preview texture).

## Import flow

`PrefabManager.ImportPrefab(path)`:

- **Unmanaged Prefab** (`ImportSourcePrefab`), skipped when `SuppressAutoVariantCreation` is set, when the path is inside `Assets/EAUploader/` or outside `Assets/`, or when a Variant of that source already exists:
  - Creates a Variant with `type`, `lastModifiedTicks = now`.
  - Avatars (`VRC_AvatarDescriptor`) get `genre = Avatar`, `genreConfirmed = true`.
  - Everything else gets `genre = Cloth`, `genreConfirmed = false` and is returned for the genre selection modal.
  - Saves a preview and refreshes the list.
- **Managed Prefab** (`RefreshManagedPrefab`):
  - Re-detects `type`; avatars are forced to `genre = Avatar`.
  - Updates `lastModifiedTicks` only if the recorded time is older than 5 seconds, so the save it triggers does not loop through the postprocessor again.
  - Saves a preview when anything changed.

Import entry points: drag & drop onto the Avatar List, the Import modal (`.prefab`, `.unitypackage`, folder, `.vrm`), or any asset import in the project.

## Listing

`PrefabManager.GetAllPrefabs()` scans `ManagedPrefabs.GetAllVariantPaths()` (only Prefabs that already carry `EAUploaderMeta`) and caches the result for 2 seconds. `InvalidateCache()` forces a rescan.

- `GetAllPrefabsWithPreview()` excludes `Hidden` items; `GetAllPrefabsIncludingHidden()` includes them. Both sort pinned first, then newest first.
- `GetPrefabInfo(path)`, `GetPrefab(path)`, `GetPrefabGenre(path)`.

## Editing metadata

`PrefabManager` setters write to `EAUploaderMeta`, call `SetDirty` + `SaveAssets` and invalidate the cache: `SetPrefabGenre`, `SetPrefabType`, `MarkGenreConfirmed`, `SetMemo`, `PinPrefab`, `RenamePrefab`.

## Tags (`TagManager`)

- Tag names and groups are project-wide in `Assets/EAUploader/tags.json`; attachment is stored per Prefab in `EAUploaderMeta.tags`.
- Comparison is case-insensitive; a new tag that differs only in case reuses the existing spelling.
- Maximum length of tag and group names: 32 characters (`MAX_TAG_LENGTH`).
- A tag belongs to at most one group. Deleting a tag removes it from all Prefabs. Deleting a group keeps its tags ungrouped.
- `GetAllTags()` also includes tags used by Prefabs but missing from the JSON.

## Previews (`PrefabPreview`)

- Resolution 1200×900 (`PREVIEW_WIDTH` × `PREVIEW_HEIGHT`); the list cards use the same aspect ratio.
- Stored as PNG in `Assets/EAUploader/PrefabPreviews/`, file name = Prefab GUID, so renames do not move previews.
- Users can regenerate a preview or replace it with an image file from the Edit screen.

## Deletion

`PrefabManager.ShowDeletePrefabDialog(path)`:

1. Collects the Variant and, if no other Variant uses it, its source Prefab.
2. Finds materials and textures referenced only by the deleted assets (`FindAssetsOnlyUsedBy`) by scanning dependencies of all other `.prefab/.unity/.controller/.overridecontroller/.anim/.asset/.mat` assets.
3. Shows a confirmation dialog listing everything that will be deleted.
4. Deletes the preview, the assets, and the item folder if it became empty, then clears the selection if needed.

## Legacy migration (`LegacyPrefabMigration`)

Older versions attached `EAUploaderMeta` directly to imported Prefabs. On startup, if `managedPrefabsMigrated` is not set and such Prefabs exist outside the managed root, the user is asked to migrate:

1. Read the legacy metadata from the source Prefab.
2. Remove `EAUploaderMeta` from the source Prefab (before creating the Variant, so it is not inherited).
3. Create a Variant carrying the metadata, and move the old name-based preview to the GUID-based path.

Automatic Variant creation is suppressed during the migration. Declining asks again on the next startup.

## Shader checks (`ShaderChecker`)

Detects renderers whose materials use missing or error shaders and shows a message box per Prefab. `CheckAvatarHasShader(avatar)` drives the "No Shader" status on the Edit screen.

## VRM import (`VRMImporter`, `HAS_VRM` only)

`ImportVRM(path)` imports a `.vrm` (0.x) file and waits for the generated Prefab. `VRMImporterWindow` converts a VRM Prefab to a VRChat avatar using VRM Converter for VRChat.
