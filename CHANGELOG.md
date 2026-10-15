# Changelog

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
