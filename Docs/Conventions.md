# Conventions

## Documentation

- Specifications are kept in `Docs/` as Markdown, written in English.
- Update the relevant document in the same change that alters behavior.

## Code comments

- Comments in new code are limited to headings, feature names and references (for example `#region Shape keys`, `// Bone mapping`, `// See Docs/EditTools.md`).
- Explanations of behavior and design belong in `Docs/`, not in code comments.

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
