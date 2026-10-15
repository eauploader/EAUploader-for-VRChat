# Plugin API

Public, stable entry points for third-party plugins live in `Editor/Public EAUploader API/`. Plugins should use these classes instead of internal types.

## Registering an edit tool

```csharp
using EAUploader.Editor.Plugin.API;
using UnityEditor;

public static class MyTool
{
    [PluginAPI.EAUPlugin]
    private static void Register()
    {
        PluginAPI.RegisterEditor(new PluginAPI.EditorRegistration
        {
            MenuName = "MyTool/Open",
            EditorName = "My Tool",
            Description = "Does something to the selected avatar.",
            Version = "1.0.0",
            Author = "Me",
            Url = "https://example.com",
            Requirement = path => path != null,
            RequirementDescription = "Select an avatar first."
        });
    }

    [MenuItem("MyTool/Open")]
    private static void Open() { /* open an EditorWindow working on CoreAPI.SelectedPrefabPath */ }
}
```

| Field | Description |
| --- | --- |
| `MenuName` | Menu path executed with `EditorApplication.ExecuteMenuItem` when the tool button is clicked |
| `EditorName` | Button label |
| `Description`, `Version`, `Author`, `Url` | Shown in the expandable details |
| `Requirement` | `Func<string, bool>` called with the selected Prefab path; `false` disables the button |
| `RequirementDescription` | Warning shown when the requirement fails |

Static methods marked with `[EAUPlugin]` (either `EAUploader.EAUPluginAttribute` or `PluginAPI.EAUPluginAttribute`) are invoked once during EAUploader initialization. See [EditTools.md](EditTools.md) for how tools are expected to behave.

## API classes

### `EAUploader.Editor.API` (`EAUploader.Editor.API.cs`)

| Class | Members |
| --- | --- |
| `CoreAPI` | `SelectedPrefabPath`, `SelectedPrefabPathChanged`, `HasVRM`, `HasAvatarOptimizer`, `GetVersion(noText)`, `Reload()` |
| `LanguageAPI` | `GetCurrentLanguage()`, `Localize(root)`, `LocalizeFromJsonFile(root, folder)`, `GetLocalizedTextFromJsonFile(key, folder)`, `ChangeLanguage(lang)`, `GetAvailableLanguages()` |
| `UploadAPI` | `OnUploadComplete`, `UploadProgressChanged`, `BuildAndTestAsync(path)`, `GetVRCAvatar(path)`, `UploadAvatarAsync(...)`, `UpdateVRCAvatar(...)` |

### `EAUploader.Editor.Prefab.API` (`EAUploader.Editor.Prefab.API.cs`)

| Class | Members |
| --- | --- |
| `PrefabAPI` | `UpdatePrefabInfo()`, `ImportPrefab(path)`, `GetPrefab(path)`, `ShowDeletePrefabDialog(path)`, `DeletePrefabPreview(path)`, `PinPrefab(path)`, `GetAvatarDescriptor(path)`, `IsPinned(path)`, `SavePrefab(prefab, path)` |
| `PrefabPreviewAPI` | `GeneratePreview`, `GenerateAndSaveAllPrefabPreviews`, `SavePrefabPreview`, `LoadTextureFromFile`, `SaveTextureToFile`, `GetPreviewImagePath`, `GetPrefabPreview` |
| `ShaderAPI` | `CheckShaders`, `GetExistingShaders`, `CheckShadersInPrefabs`, `CheckAvatarHasShader` |
| `VRMAPI` | `ImportVRM(path)` |
| `PrefabUtilityAPI` | `GetAvatarHeight`, `CheckAvatarHasVRCAvatarDescriptor`, `CheckAvatarIsVRM`, `CheckAvatarHasTandO`, `GetAvatarDescriptor`, `GetPipelineManager` |

### `EAUploader.Editor.Resource.API` (`EAUploader.Editor.Resource.API.cs`)

| Class | Members |
| --- | --- |
| `EAUResourceAPI` | `EAUStyles`, `EAUTailwind` (style sheets to add to a tool window) |
| `HelpButtonAPI` | `ShowMessage(msgNum)` |
| `LinkAPI` | `CreateLink(text, url)` |
| `MaterialIconAPI` | `CreateMaterialIcon(name)` |
| `PrefabItemButtonAPI` | `CreatePrefabItemButton(prefabInfo, onClick, disabled)` |
| `ShadowButtonAPI` | `CreateShadowButton(text, onClick)` |
| `PreviewAPI` | `CreatePreview(root, path)`, `ShowContent`, `UpdatePreview`, `ResetPreview` |
