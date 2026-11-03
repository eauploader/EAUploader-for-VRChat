# Upload

Upload is available only for items whose genre is `Avatar`. The UI is `UploadForm` (`Editor/Resources/UI/TabContents/Upload/Contents/UploadForm.cs`); the pipeline is `AvatarUploader` (`Editor/Scripts/AvatarUploader.cs`), built on the VRChat SDK builder API (`IVRCSdkAvatarBuilderApi`).

## Upload form

| Section | Behavior |
| --- | --- |
| Login | Shows VRChat SDK login state and whether the account may upload avatars |
| Avatar Info | Name, description, content warnings, release status (public/private). For an avatar already on VRChat, the current values are loaded and can be saved or discarded. |
| Thumbnail | Uses the item preview by default; can be replaced with an image file or the existing VRChat thumbnail |
| VRChat Information | Blueprint ID (copy) and **Unlink from VRChat** (clears the Blueprint ID) |
| Avatar Optimization | With Avatar Optimizer (`HAS_AAO`): toggles a `TraceAndOptimize` component on the avatar |
| Avatar Performance | Performance ratings computed by `PerformanceInfoComputer` for PC and mobile |
| Platform | Windows is always built. Optional: Quest (Android), iOS (only if the account allows it). Platforms without installed build support are skipped with a warning. |
| Build as Test | Local test build |
| Upload | Requires the rights confirmation checkbox; shows progress and **Cancel upload** while running |

## Build instance

Both build paths use `CreateBuildInstance(prefabPath)`:

1. Load the managed Prefab and ensure it has a Blueprint ID (`PipelineManager.AssignId()` saved to the Prefab), so the avatar stays linked across domain reloads caused by platform switches.
2. Instantiate the Prefab in the active scene and **unpack it completely**, so non-destructive tools (Modular Avatar, Avatar Optimizer / NDMF) process it at build time and build-time changes never write back to the Prefab asset.
3. Name it `<name> (EAUploader Upload)`.

The instance is destroyed after the build, except during a VRChat SDK multi-platform build, which keeps using it. Leftover instances are removed at the next startup (`CleanupLeftoverBuildInstances`).

## Build & Test

`BuildAndTestAsync(prefabPath)` opens the SDK control panel, builds the instance with `builder.BuildAndTest` and reports the result with `DialogPro`. Only one build/upload can run at a time.

## Upload

`UploadAvatarAsync(prefabPath, name, description, releaseStatus, tags, thumbnailPath, platforms)`:

1. Resolve build targets (Windows first, then selected platforms with installed support).
2. Create the build instance and fetch the existing `VRCAvatar` by Blueprint ID; create a new record if none exists.
3. Single platform: `builder.BuildAndUpload`. Multiple platforms: set the SDK's multi-platform targets and call `builder.BuildAndUploadMultiPlatform` (Windows → additional platforms → back to the original platform; a domain reload may happen in between).
4. Copy the Blueprint ID assigned by the SDK back to the Prefab asset.
5. Report success, cancellation or failure; raise `OnComplete`.

Progress is forwarded from the SDK (`OnSdkBuildProgress`, `OnSdkUploadProgress`) through `AvatarUploader.ProgressChanged`. `CancelUpload()` cancels the token and the SDK transfer.
