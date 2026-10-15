# Overview

## Purpose

EAUploader is a Unity Editor extension that lets users who are new to Unity manage avatars, outfits and accessories and upload avatars to VRChat from a single, simplified window. It hides most of the Unity Editor workflow (scenes, hierarchy, inspectors) behind a card-based list, an edit screen and an upload screen.

## Package

| Item | Value |
| --- | --- |
| Package name | `tech.uslog.eauploader` |
| Display name | Easy Avatar Uploader for VRChat |
| Version | `1.4.6` |
| Unity | `2022.3` |
| Distribution | VPM (VRChat Creator Companion / ALCOM) |
| License | See `LICENSE` |

### Required VPM dependencies

| Package | Version |
| --- | --- |
| `com.vrchat.base` | `^3.7.0` |
| `com.vrchat.avatars` | `^3.7.0` |

### Optional integrations

Optional packages are detected in two ways: compile-time defines from `versionDefines` in `Editor/EAUploader.Editor.asmdef`, and run-time flags read from `Packages/packages-lock.json` by `EAUploaderCore.CheckAvailablePackages()`.

| Package | Define | Runtime flag | Used for |
| --- | --- | --- | --- |
| `jp.pokemori.vrm-converter-for-vrchat` (+ `com.vrmc.univrm`) | `HAS_VRM` | `EAUploaderCore.HasVRM` | Importing `.vrm` files and converting VRM avatars to VRChat avatars |
| `com.anatawa12.avatar-optimizer` | `HAS_AAO` | `EAUploaderCore.HasAAO` | "Automatic avatar optimizing" toggle (adds/removes `TraceAndOptimize`), AAO component information for `EAUploaderMeta` |
| `nadena.dev.modular-avatar` | `HAS_MA` | `EAUploaderCore.HasMA` | Outfit setup in Dress Up (Compatible Outfits) (see [EditTools.md](EditTools.md)) |

## Assemblies

| Assembly | Folder | Platforms | Notes |
| --- | --- | --- | --- |
| `EAUploader` | `Runtime/` | All | Runtime components (`EAUploaderMeta`, `AvatarTagComponent`). Defines `VRC_SDK3A` when the avatars SDK is present. |
| `EAUploader.Editor` | `Editor/` | Editor | All editor code, UI and public API. |

## Repository layout

```
Docs/                         Specification (this folder)
Runtime/
  Components/                 EAUploaderMeta, EAUploaderOutfit, AvatarTagComponent
Editor/
  Public EAUploader API/      Public API for plugins (see PluginAPI.md)
  Scripts/
    EAUploaderCore.cs         Startup and global state
    EAUploaderEditorManager.cs  Edit tool registry
    EAUPluginAttribute.cs     [EAUPlugin] attribute
    AvatarUploader.cs         Build / upload pipeline
    AssetProcessor.cs         AssetPostprocessor (import hook)
    Config.cs                 settings.json
    LanguageUtility.cs, T7e.cs  Localization
    ThemeUtility.cs           Themes
    CustomPrefabUtility/      Managed prefabs, tags, previews, shaders, VRM import, migration
    Coexistence/              Avatar Optimizer integration
    VRCSDK/                   Performance rating helpers
    Feedback/, SubWindow/     Feedback sender, welcome window
  Resources/
    Localization/             ja.json, ko.json (English is the key language)
    Message/<lang>/           Help messages shown by HelpButton (by id)
    Shaders/                  AlwaysShow.shader (preview markers), TextureAdjust.shader (Texture Editor)
    UI/                       Main window, styles, components, modals, screens
      TabContents/Setup/Editor/  Built-in edit tools and their shared base (see EditTools.md)
.vpm/, .github/               VPM listing build and CI
```
