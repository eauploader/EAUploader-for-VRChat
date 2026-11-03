# Changelog

## 1.6.0

- Redesigned the avatar list and edit screen, with a 3D studio view, floating tool panels and an integrated upload sheet.
- Added managed Prefab Variants, tags and tag groups, memos, shape-key and texture editing, and compatible outfit setup.
- Added an in-window Prefab deletion confirmation with an animated, read-only list of the files being deleted. Shared assets are retained, dependency searches are cached, and changed deletion targets must be confirmed again.
- Improved Japanese font rendering, input placeholders, tag entry, segmented controls and clipboard notifications. Metadata and tag writes are debounced.
- Restore original Prefab data when migration fails, and retry incomplete migrations instead of marking them complete.
- Ignore stale upload-information responses after switching Prefabs, and dispose completed thumbnail requests.
- Added `PrefabAPI.ShowDeletePrefabDialog(path, completed)` for asynchronous UI Toolkit confirmation. The obsolete one-argument overload retains its synchronous native confirmation and returns true only after deletion completes.
- Package archives now contain only the Unity package and documentation, excluding local developer settings and release tooling.

## 1.5.1

Code quality release based on the DeepSource C# analysis of v1.5.0. No functional changes.

- One public type per file: split the public Prefab API, `HelpButton` / `EAUploaderMessageWindow`, the Viewpoint Position Editor classes, `TagGroupInfo`, `LanguageInfo` and `ThemeInfo` into their own files.
- `AvatarUploader.GetVRCAvatar` and `AvatarUploader.UpdateVRCAvatar` are renamed to `GetVRCAvatarAsync` and `UpdateVRCAvatarAsync`. The old names remain as `[Obsolete]` wrappers; `UploadAPI` is unchanged.
- The Avatar List card refresh no longer uses `async void`; exceptions are logged.
- Simplified null checks, `is not` patterns, target-typed `new()`, array initializers and expression-bodied lambdas. Null checks on Unity objects keep Unity's null semantics (implicit bool conversion instead of `?.` / `??`).
- Added explicit `default` branches to switch statements and replaced two-branch switches with `if` / `else`.
- Marked fields that are never reassigned as `readonly`.
- Removed an unused local in the avatar optimization toggle when Avatar Optimizer is not installed.
- Error report timestamps use `DateTime.UtcNow` converted to local time (same output).
