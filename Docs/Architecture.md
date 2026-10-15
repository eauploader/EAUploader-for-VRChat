# Architecture

## Startup sequence

`EAUploaderCore` (`Editor/Scripts/EAUploaderCore.cs`) is an `[InitializeOnLoad]` class.

1. **Static constructor**
   - Hooks `Application.logMessageReceived` to `UI.Windows.Logger.OnReceiveLog`.
   - Chooses the log file name (`yyyy-MM-dd-<n>.log`) and writes environment details (version, Unity version, platform, VPM locked packages) to the log.
   - Registers `OnEditorUpdate` on `EditorApplication.update`.
2. **First idle editor update** (not compiling, not entering play mode) runs `PerformInitialization()` once:
   1. `InitializeEAUploader()` creates `Assets/EAUploader/` and `Assets/EAUploader/PrefabManager.json` if missing.
   2. `AvatarUploader.CleanupLeftoverBuildInstances()` removes temporary upload instances left in the scene by an interrupted multi-platform build.
   3. `EAUploaderEditorManager.OnEditorManagerLoad()` resets the edit tool registry (`Assets/EAUploader/EditorsManage.json`).
   4. `ShaderChecker.CheckShaders()` warns about missing / problematic shaders.
   5. `PrefabManager.Initialize()` ensures the managed root, runs the legacy migration and generates missing previews.
   6. `CheckAvailablePackages()` sets `HasVRM`, `HasAAO`, `HasMA`.
   7. Every static method marked with `[EAUPlugin]` in any loaded assembly is invoked (edit tool and plugin registration).
   8. The main window is opened on the next `delayCall`.
3. If initialization throws, a dialog offers to reimport the package.

The menu item **EAUploader/Reload** re-runs steps 3–6 and clears the selection.

## Global state

| Member | Description |
| --- | --- |
| `EAUploaderCore.selectedPrefabPath` | Asset path of the item currently selected in the list. Raises `SelectedPrefabPathChanged` on change. All screens and edit tools act on this item. |
| `EAUploaderCore.HasVRM / HasAAO / HasMA` | Optional package availability. |
| `EAUploaderCore.GetVersion()` | Reads the version from `package.json`. |

## Main window and navigation

`UI.EAUploader` (`Editor/Resources/UI/EAUploader.cs`) is the single `EditorWindow` (menu **EAUploader/Open EAUploader**, default 1280×640, minimum 1080×640).

- `CreateGUI()` loads `UI/styles.uss` and `UI/tailwind.uss`, clones `UI/MainWindow.uxml` (a single `contentRoot`), adds the shared `Modal`, applies the current theme and schedules `LanguageUtility.Localization(root)` every 100 ms.
- Screens are not tabs. One screen at a time is built into `contentRoot`:

| `EAUploaderScreen` | Builder | Purpose |
| --- | --- | --- |
| `AvatarList` | `UI.AvatarList.Main.ShowContent` | Start screen: card grid of all managed items |
| `Setup` | `UI.Setup.Main.ShowContent` | Edit screen for the selected item |
| `Upload` | `UI.Upload.Main.ShowContent` | Upload screen for the selected avatar |

- `EAUploader.Navigate(screen)` opens/focuses the window and switches screens.
- `EAUploader.RefreshCurrentScreen()` rebuilds the current screen (used after duplicate, rename, edit tool changes).
- `EAUploader.modal` is the single in-window modal used by all modals and by the error report.

## Persisted files

| Path | Owner | Contents |
| --- | --- | --- |
| `Assets/EAUploader/settings.json` | `Config` | `language`, `isLibraryOpen`, `showErrorReportWindow`, `managedPrefabsMigrated` |
| `Assets/EAUploader/PrefabManager.json` | `EAUploaderCore` | Legacy placeholder (created empty) |
| `Assets/EAUploader/EditorsManage.json` | `EAUploaderEditorManager` | Registered edit tools (rebuilt every startup) |
| `Assets/EAUploader/tags.json` | `TagManager` | Known tag names and tag groups |
| `Assets/EAUploader/Prefabs/<Item>/<Variant>.prefab` | `ManagedPrefabs` | Managed Prefab Variants (see [PrefabManagement.md](PrefabManagement.md)) |
| `Assets/EAUploader/Prefabs/<Item>/Textures/`, `.../Materials/` | Texture Editor | Generated textures and materials (see [EditTools.md](EditTools.md)) |
| `Assets/EAUploader/PrefabPreviews/` | `PrefabPreview` | Preview PNGs, named by Prefab GUID |
| `EAUploaderLog/` (project root) | `UI.Windows.Logger` | Log files, capped at 100 MB in total |
| EditorPrefs `EAUploader.Theme` | `ThemeUtility` | `white`, `dark` or `pastel` |
| EditorPrefs `EAUploader.CardWidth` | Avatar List | Card width in pixels |

## Logging and error reporting

`UI.Windows.Logger` writes EAUploader log lines (`writeEAULog`) and all Unity log messages to the current log file. When an error or exception is logged and `showErrorReportWindow` is enabled, an error report is shown in the main window modal (guarded against recursion and size-limited to keep UI Toolkit text rendering stable). **Window/Error Report** opens the report manually.

## Asset import hook

`AssetImportProcessor` (`Editor/Scripts/AssetProcessor.cs`) handles every imported `.prefab`:

1. `ShaderChecker.CheckShadersInPrefabs(path)`.
2. `PrefabManager.ImportPrefab(path)` (creates or refreshes a managed Variant).
3. Non-avatar items that still need a genre decision are collected and shown together in one `ClothSettingModal` on the next `delayCall` (only when the main window is open).

Deleting any `.prefab` invalidates the `PrefabManager` cache.
