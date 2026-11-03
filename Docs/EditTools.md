# Edit Tools

Edit tools are listed in the left column of the Edit screen and always operate on the selected item (`EAUploaderCore.selectedPrefabPath`).

## Framework

### Registry

`EAUploaderEditorManager` (`Editor/Scripts/EAUploaderEditorManager.cs`) keeps the registered tools in memory and mirrors them to `Assets/EAUploader/EditorsManage.json`. The JSON is cleared on every initialization; tools re-register through `[EAUPlugin]` methods.

`EditorRegistration` fields: `MenuName`, `EditorName`, `Description`, `Version`, `Author`, `Url`, `Requirement` (`Func<string, bool>` on the selected Prefab path) and `RequirementDescription`. Third-party tools use the same mechanism through `PluginAPI` (see [PluginAPI.md](PluginAPI.md)).

### Built-in tool base

Built-in tools added in this version share a common base in `Editor/Resources/UI/TabContents/Setup/Editor/Common/` (namespace `EAUploader.UI.Setup.EditTools`):

| Type | Responsibility |
| --- | --- |
| `EditToolSession` | Opens the managed Prefab with `PrefabUtility.LoadPrefabContents` into an isolated preview scene, tracks the dirty state, saves with `PrefabUtility.SaveAsPrefabAsset` (a Variant stays a Variant), reloads (revert) and can retarget to another path. |
| `EditToolModal` | Abstract base of the built-in tools. Shows the tool as a modal inside the EAUploader window (see [Tool modal](#tool-modal)) with the common layout: header (tool name, item name, "Unsaved changes" badge, **Back**), side panel (tool specific), live preview, footer (tool actions, **Revert**, **Apply**). Handles opening, selection changes, unsaved-change prompts and closing. Only one tool is open at a time. |
| `ToolModalFrame` | Modal frame shared by all tools, including the Viewpoint Position Editor: backdrop, panel, header with the title and a **Back** button at the top right, body. |
| `EditToolUtility` | Generated-asset folders, file name sanitizing, bounds, post-save refresh, shared UI helpers, requirement helpers. |

### Tool modal

Edit tools are not separate windows. Running a tool (from the Edit screen or the menu) opens the EAUploader window if needed and places a full-size modal over its content:

- The modal is inserted into the window root just below the shared `EAUploader.modal`, so help messages and error reports still appear above it.
- The header shows the tool name on the left and a **Back** button at the top right. **Back** closes the tool; with unsaved changes it asks Save / Cancel / Discard.
- The modal uses the main window's styles and theme (`.edit-tool-modal` with dark and pastel variants). Buttons inside tools get a 6 px right and 4 px bottom margin (`.edit-tool-modal Button`).
- Opening another tool closes the current one first (asking about unsaved changes).
- While a tool has unsaved changes, the EAUploader window reports `hasUnsavedChanges`, so closing the window offers to save them (`EAUploader.SaveChanges` saves the active tool).
- Closing the window or a script reload closes the tool and discards unsaved changes.

### Preview

Tools use the shared `UI/Components/Preview` component (the same one as the Edit and Upload screens) in **live mode**: `new Preview(container, session.Root)`.

- Asset mode (`new Preview(container, prefabPath)`) is unchanged and renders a Prefab asset with the built-in GameObject inspector preview.
- Live mode renders an in-memory object that is not an asset. It instantiates a copy of the target into a `PreviewRenderUtility` scene and draws it in the same IMGUI container, with the same theme background, help button and reset button.
- Controls: left drag orbits, right drag pans, wheel zooms, reset frames the whole item.
- `Refresh()` re-creates the copy after structural changes (materials replaced, outfits added or removed, visibility changed). `Repaint()` redraws without re-creating. `FindInPreview(component)` returns the matching component on the copy so lightweight changes (blend shape weights) can be mirrored without re-creating it. `Frame(center, radius)` focuses the camera. `SetLiveTarget` switches the target after a revert; `Dispose()` releases the preview scene.

Behavior shared by all built-in tools:

- Nothing is written until **Apply**. **Revert** reloads the saved Prefab.
- After saving, `EditToolUtility.OnPrefabSaved` invalidates the `PrefabManager` cache, regenerates the item preview, refreshes the Avatar List and rebuilds the Edit screen when it shows the same item.
- Changing the selection in the main window while a tool is open retargets the tool; unsaved changes trigger a Save / Discard / Cancel prompt.
- Generated assets are stored in the item folder: `Assets/EAUploader/Prefabs/<Item>/<SubFolder>/` (`Assets/EAUploader/Generated/<SubFolder>/` for unmanaged paths).

### Localization

- Tools register English keys as `EditorName`, `Description` and `RequirementDescription`; the Edit screen translates them with `T7e.Get` when it builds the tool list, so the list follows language changes. Already translated text (for example from the Viewpoint Position Editor) passes through unchanged.
- All tool UI text uses `T7e.Get` with the main localization files (`Editor/Resources/Localization/ja.json`, `ko.json`).
- `T7e` caches translations; the cache is cleared whenever a file in `Resources/Localization/` is reimported (`AssetImportProcessor`), so edited translations apply without a domain reload.

Menu items live under **EAUploader/Edit Tools/**.

## Viewpoint Position Editor

`Setup/Editor/ViewpointPositionEditor/` · menu `EAUploader/ViewpointPositionEditor` · requirement: `VRCAvatarDescriptor`.

Opens in the same tool modal (`ToolModalFrame`) with its own layout. Side view with a red marker for the new viewpoint and a green marker for the applied one. Horizontal and vertical sliders move the viewpoint within the avatar bounds; the right panel shows the first-person view in 1:1 (VR) or 16:9 (PC). **Apply** opens the selected Prefab with `PrefabUtility.LoadPrefabContents`, sets `VRCAvatarDescriptor.ViewPosition`, saves it with `PrefabUtility.SaveAsPrefabAsset` (stored as an override on the managed Variant), moves the green marker to the applied position and calls `EditToolUtility.OnPrefabSaved`. The tool does not use `EditToolSession`, so it has no unsaved-changes state: **Back** closes it without a prompt and slider changes that were not applied are discarded. It predates the shared base and uses its own localization files in `Localization/`.

## Shape Key Editor

`Setup/Editor/ShapeKeyEditor/ShapeKeyEditor.cs` · menu `EAUploader/Edit Tools/Shape Key Editor` · requirement: at least one `SkinnedMeshRenderer` with blend shapes.

Sets the default blend shape weights saved in the item (for example body shape, face shape or shrinking body parts hidden by clothes).

| Element | Behavior |
| --- | --- |
| Mesh | Dropdown of all skinned meshes with blend shapes (`path (count)`). Defaults to the lip sync mesh of the `VRCAvatarDescriptor`, otherwise the mesh with the most blend shapes. |
| Focus on face / Whole body | Frames the head bone (humanoid mapping, then a `Head` transform, then the top of the bounds) or the whole item. |
| Search / Show changed only | Filters the list by name and by weights that differ from the saved value. |
| Shape key list | Virtualized list. Each row has a slider (0 to the weight of the last blend shape frame, usually 100), a numeric field and a reset button. |
| Usage badges | Keys used as visemes (`VisemeBlendShapes` on the viseme mesh with `VisemeBlendShape` lip sync) or as eyelids (`customEyeLookSettings.eyelidsBlendshapes`) are marked, because VRChat drives them at runtime. |
| Reset this mesh / Set all to 0 | Bulk actions for the selected mesh. |

Changes are mirrored to the preview copy immediately (`Preview.FindInPreview`) and saved as `m_BlendShapeWeights` overrides on the managed Variant. When the search finds nothing, the list shows "No matching shape keys."

## Texture Editor

`Setup/Editor/TextureEditor/` · menu `EAUploader/Edit Tools/Texture Editor` · requirement: a material with at least one 2D texture.

Recolors textures or replaces them with other images without modifying the original assets.

### Usage

1. Choose a material. The list contains every distinct material on the item's renderers that has a 2D texture (normal maps excluded). "Edited" marks materials created by this tool; "Changed" marks pending edits.
2. Choose the texture slot (`_MainTex`, then `_BaseMap`, then the first texture property by default). Common slots (main, 2nd/3rd, shadow colors, emission, outline, MatCap, rim light; lilToon and Standard names) are shown with translated names; other slots use the shader description, or the property name when the description is not readable text.
3. Adjust **Hue** (−180°…180°), **Saturation**, **Brightness**, **Contrast** (0…200 %) and **Tint** (multiplied color), or use **Replace image...** to use an image file (`.png`, `.jpg`) as the source.
4. **Apply** to bake the result.

### Processing

- `TextureAdjuster` applies the hidden shader `Resources/Shaders/TextureAdjust.shader` with `Graphics.Blit`, so source textures do not need to be readable or uncompressed. Adjustments run in gamma space for sRGB textures (linear projects convert before and after); alpha is preserved.
- The preview uses a temporary material copy and a render texture of at most 1024 px, assigned to every slot that uses the material.
- On **Apply**:
  - The full-resolution result is baked to `<Item>/Textures/<Material>_<Property>.png`. Import settings (texture settings, max size, compression, platform overrides) are copied from the source texture; `isReadable` is off and streaming mipmaps are on.
  - A material copy `<Item>/Materials/<Material> (Edited).mat` is created with the new texture and assigned to all slots that used the material in this item.
  - The edit is recorded as JSON in the generated material's importer `userData` (`TextureEditRecord`: original material GUID, source texture GUID, generated texture GUID, property, adjustment).
- Re-editing a generated material starts from the recorded source and adjustment values, so edits do not accumulate. The generated material and texture are overwritten in place unless another managed Prefab also uses them; then new copies are created.
- An identity adjustment on the original texture restores the original material instead of generating assets.
- **Restore original material** reassigns the recorded original material.
- **Replace image...** copies the chosen file into `<Item>/Textures/` immediately (it remains there even if the edit is reverted).


## Dress Up (Compatible Outfits)

`Setup/Editor/OutfitTool/` (`CompatibleOutfitTool`) · menu `EAUploader/Edit Tools/Dress Up (Compatible Outfits)` · Japanese name 着せ替え（対応衣装） · requirement: genre `Avatar` with a `VRCAvatarDescriptor`.

Dresses the avatar with managed items of genre `Cloth` or `Accessory` that were made for this avatar (same armature and rest pose). Outfits for other avatars are out of scope for this tool.

### Side panel

| Section | Behavior |
| --- | --- |
| Status | Whether Modular Avatar is available (`HAS_MA`) and how bones will be merged |
| Wearing | Every child with `EAUploaderOutfit`: visibility toggle and **Take off** (with confirmation) |
| Avatar parts | Direct children of the avatar root that contain renderers, excluding the armature and outfits: visibility toggles (for hiding the original clothes) and **Restore original visibility** |
| Add outfit | Search (name and tags) and a tile grid of Cloth / Accessory items with previews |

Footer: **Save as new avatar**, **Revert**, **Apply**.

### Putting on an outfit

The item is instantiated as a plain copy (`Object.Instantiate`, no Prefab link) under the avatar root (or under a bone for accessories). Its `EAUploaderMeta` components are removed and an `EAUploaderOutfit` component is added (`sourcePrefabGuid`, `mode`). `EAUploaderOutfit` derives from `AvatarTagComponent` (`IEditorOnly`) and is stripped from uploads; Avatar Optimizer receives component information for it.

| Mode | When | Edit time | Build time |
| --- | --- | --- | --- |
| `ModularAvatar` | The outfit has an armature and Modular Avatar is installed | Rebinds the animators and runs Modular Avatar's `SetupOutfit.SetupOutfitUI` on the outfit. If that is not possible or adds no `ModularAvatarMergeArmature`, a `ModularAvatarMergeArmature` targeting the avatar armature is added to the outfit armature (`BaseToMerge`, inferred prefix/suffix). | Modular Avatar merges the armature. |
| `BoneMerge` | The outfit has an armature and Modular Avatar is not installed | Only checks that both hips bones can be found. The outfit keeps its own armature, which overlaps the avatar in the rest pose. | `OutfitBuildHook` (`IVRCSDKPreprocessAvatarCallback`, order −12000, before NDMF) runs `OutfitBoneMapper.Merge` on the avatar copy that the VRChat SDK builds. |
| `AttachToBone` | No armature (accessories) | A menu asks for the target (head, neck, chest, spine, hips, hands, lower arms, feet, avatar root); the item keeps its world pose and becomes a child of that bone. | Nothing extra. |

`OutfitBoneMapper.Merge` maps outfit bones to avatar bones: hips and armature first, then the humanoid mapping from both `Avatar` definitions, then children by normalized name. It rebinds `SkinnedMeshRenderer.bones` / `rootBone`, moves unmatched bones (for example skirt or hair bones, with their PhysBones) under the matched avatar bone keeping their world pose, removes the outfit armature (renamed `... (Merged)` and kept if matched bones carry components) and removes the outfit root `Animator`.

Hips detection uses the humanoid `Avatar` of the `Animator` (by bone name, without requiring a bound animator), then common names (`Hips`, `Hip`, `Pelvis`, `J_Bip_C_Hips`). For outfits without a humanoid definition, a transform with the avatar's hips name is used.

### Non-destructive design

| Guarantee | How |
| --- | --- |
| The imported avatar Prefab is never modified | All edits go to the managed Variant through `EditToolSession`; the Variant only stores overrides on top of the imported Prefab. |
| The imported outfit is never modified | The outfit is copied without a Prefab link; deleting the outfit item later does not break the avatar (its materials stay in use, so the delete dialog keeps them). |
| The avatar's own hierarchy is not restructured | Outfits are added as child objects only. Bone merging happens at build time (Modular Avatar or `OutfitBuildHook`) on the copy that is uploaded. Modular Avatar's Setup Outfit only changes objects inside the outfit. |
| Every change can be undone | **Take off** removes an outfit completely (one object, no leftovers). Part visibility can be toggled back or restored to the imported Prefab's state with **Restore original visibility**. Unsaved changes are discarded with **Revert**. |
| The original avatar can be kept as is | **Save as new avatar** saves the dressed state as a new Variant in the same item folder (`<Avatar> <Outfit>.prefab`) with a cleared Blueprint ID, so it uploads as a separate avatar; the selected Variant stays unchanged. **Apply** writes to the selected Variant. |

Known limitations:

- Part visibility is stored as an `m_IsActive` override on the Variant (reversible, but it is a change to that Variant).
- In `BoneMerge` mode, tools that run in Play mode without the VRChat SDK build callbacks show the outfit unmerged.
- Modular Avatar's Setup Outfit records Undo entries for objects in the temporary editing scene; they become no-ops after the tool closes.
