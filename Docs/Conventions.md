# Conventions

## Documentation

- Specifications are kept in `Docs/` as Markdown, written in English.
- Update the relevant document in the same change that alters behavior.

## Code comments

- Comments in new code are limited to headings, feature names and references (for example `#region Shape keys`, `// Bone mapping`, `// See Docs/EditTools.md`).
- Explanations of behavior and design belong in `Docs/`, not in code comments.

## Code quality

- The project compiles with C# 9 (Unity 2022.3). Do not use C# 10+ features such as file-scoped namespaces; DeepSource rule CS-R1062 is therefore not applicable and should be ignored in the DeepSource dashboard.
- One public type per file.
- Unity objects (`UnityEngine.Object` and derived types) must not be null-checked with `?.`, `??`, `??=` or `is {}` / `is null` patterns, because these bypass Unity's destroyed-object check. Use `== null`, `!= null` or the implicit bool conversion (`if (obj && obj.x)`).
- Async methods return `Task` and end with `Async`; fire-and-forget calls go through a wrapper that logs exceptions.
- Switch statements have a `default` branch; two-branch switches are written as `if` / `else`.
- Intentionally empty virtual hooks are marked with `// skipcq: CS-R1093`.

## UI text

- Write UI strings in English; they are the localization keys.
- Add translations to `Editor/Resources/Localization/ja.json` and `ko.json`.
- Mark user-provided text (names, tags, file names) with the USS class `no-translate`.

## Assets created by EAUploader

- Never modify user-imported source Prefabs; edit the managed Variant instead.
- Store generated assets (textures, meshes, Variants) inside the item folder under `Assets/EAUploader/Prefabs/<Item>/`.
- Edits must be undoable or reversible from the tool that made them where practical.

## Repository

- Pushing to GitHub is done by maintainers manually.
