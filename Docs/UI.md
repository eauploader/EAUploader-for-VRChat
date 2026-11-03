# User Interface

All UI is built with UI Toolkit (UXML + USS) inside the single `UI.EAUploader` window. Styles come from `UI/styles.uss` (project styles and themes) and `UI/tailwind.uss` (utility classes such as `flex-row`, `p-4`, `text-xl`).

## Screen flow

```
Avatar List ──(card click / Edit)──▶ Edit ──(Upload, avatars only)──▶ Upload
     ▲                                │ ▲                                  │
     └────────────(Back to list)──────┘ └──────────────(Edit)──────────────┘
```

Selecting a card sets `EAUploaderCore.selectedPrefabPath`; the Edit and Upload screens always act on that item.

## Avatar List screen

`TabContents/AvatarList/AvatarList.cs`, `AvatarList.uxml`, `AvatarCard.uxml`.

| Element | Behavior |
| --- | --- |
| Title | Centered; the help button sits next to it without shifting the title (`screen-header__title`) |
| Header buttons | **Import** (opens `ImportModal`), **Settings** (opens `SettingsModal`), **Exit Unity** |
| Search | Filters as you type by name and tag (Enter / Search button also run it). **Tag Settings** follows the Search button with an 8 px gap. |
| Category | `Avatar`, `Cloth`, `Items and Others` (`Accessory` + `Other`) |
| Sort | Last modified (desc/asc), name (desc/asc). Pinned items are always first. |
| Tag filter | `TagFilterModal`; items must have **all** selected tags (AND). The button shows the selected count. |
| Tag Settings | `TagSettingsModal` for creating/deleting tags and groups |
| Show hidden | Includes `Hidden` items |
| Card size | Slider, 160–600 px (default 360), saved in EditorPrefs |
| Drag & drop | `.prefab`, `.unitypackage`, and `.vrm` (with `HAS_VRM`) files are imported |

Cards (`AvatarCard`) show the preview (1200:900 ratio), name and last modified date. Hovering shows **Edit**, **Upload** (avatars only; otherwise "Needs conversion" / "Can't be uploaded") and **Delete**. Cards are added asynchronously and the operation is cancelled when a new search starts. `AvatarList.Main.RequestRefresh()` rebuilds the cards only while the list is visible.

## Edit screen

`TabContents/Setup/Setup.cs`, `Setup.uxml`. Three columns plus a bottom bar.

### Left: edit tools

- **Status** of the selected item:
  - VRM avatar: warning and **Convert to VRChat Avatar** (opens `VRMImporterWindow`).
  - Missing `VRCAvatarDescriptor` or shaders: warnings.
  - Otherwise "Ready to upload" (plus a note for converted VRM avatars about the viewpoint).
- **Edit tool list**: one entry per registered tool (`EAUploaderEditorManager.GetRegisteredEditors()`). The main button runs the tool's menu item; the expand button shows description, version, author and link. Tools whose `Requirement` fails are disabled and show `RequirementDescription`. See [EditTools.md](EditTools.md).
- **Import extentions** (imports a `.unitypackage`) and **Find extentions** (opens the plugin site) are temporarily hidden (`extension_buttons` has the `hidden` class); the code is kept.
- Tools open as a modal inside this window (see [EditTools.md](EditTools.md#tool-modal)).

### Center: preview

`Components/Preview` renders the Prefab with the built-in GameObject preview; drag to rotate/move, wheel to zoom, reset button.

### Right: item info

Thumbnail (regenerate / replace from file), name (applied on blur or Enter; also renames the item folder when it only holds this Prefab), genre, tags (input with suggestions, existing-tag dropdown, removable chips), memo (saved on blur), **Pin** and **Hide from list** (mutually exclusive), details (last modified, height from the viewpoint, Blueprint ID with copy, path), **Duplicate** (creates another Variant and selects it) and **Delete**.

### Bottom bar

`footer-nav`: the footer is split into two halves and each half is one full-size button: **Back to list** (left, secondary style) and **Upload** (right, primary style; shown only when the genre is `Avatar`, otherwise **Back to list** fills the footer).

## Upload screen

`TabContents/Upload/Upload.cs`, `Contents/UploadForm.cs`. Left: selected avatar name and preview. Right: upload form (see [Upload.md](Upload.md)). Footer (`footer-nav`, same as the Edit screen): **Back to list**, **Edit**.

## Modals

All modals render into `EAUploader.modal` (`Components/Modal`: title, content, footer action buttons).

| Modal | Purpose |
| --- | --- |
| `ImportModal` | Import `.prefab`/`.unitypackage`, import a whole folder, import `.vrm`, import shader packages, list existing shaders |
| `SettingsModal` | Language, theme (experimental), error report window toggle, version, open log report, VRChat SDK reload |
| `ClothSettingModal` | Genre selection (`Cloth`, `Accessory`, `Other`) for newly imported non-avatar items |
| `TagFilterModal` | Tag selection grouped by tag group |
| `TagSettingsModal` | Tag and tag group management |
| `PrefabPreviewModal` | Large preview with regenerate |
| `AvatarSettingsModal` (`EAUploaderMetaSetting.cs`) | Older metadata settings modal |

## Shared components (`UI/Components`)

| Component | Description |
| --- | --- |
| `ShadowButton`, `Shadow` | Button with drop shadow |
| `MaterialIcon` | Material Icons glyph (`MaterialIcons-Regular.ttf`, names in `codepoints.txt`) |
| `HelpButton` | Opens help message `Resources/Message/<lang>/<msg-id>.txt` in the modal |
| `Link` | Clickable URL label |
| `SlideToggle` | Switch-style toggle |
| `TextFieldPro` | Text field with placeholder (`GetValue()` returns `null` while showing the placeholder) |
| `PrefabItemButton` | Prefab button with thumbnail |
| `ContentWarningsField` | VRChat content warning selector |
| `ArticleRenderer` | Renders EA Library articles |
| `Preview` | Interactive Prefab preview |

Other windows: `DialogPro` (custom dialog), `WelcomeWindow`, `DiscordWebhookSender` (feedback), `VRMImporterWindow`.

## Themes

`ThemeUtility` stores the theme in EditorPrefs (`EAUploader.Theme`) and applies one of the USS classes `white`, `dark`, `pastel` to the window root.

## Localization

- English strings are the keys. Translations live in `Editor/Resources/Localization/<lang>.json` (`{ "name": "<display name>", "items": [ { "key", "value" } ] }`). Available: `ja`, `ko`.
- `T7e.Get(key, args)` returns the translation (with `string.Format`) or the key.
- `LanguageUtility.Localization(root)` translates every `TextElement` in a tree. The main window runs it every 100 ms. Elements with the USS class `no-translate` (user content such as names and tags) and text input contents are skipped.
- Tools may carry their own files and use `LanguageUtility.LocalizationFromJsonFile` / `T7eFromJsonFile`.
- The current language is stored in `settings.json`.
