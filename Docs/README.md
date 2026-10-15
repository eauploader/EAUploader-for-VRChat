# EAUploader Specification

Specification of **Easy Avatar Uploader for VRChat** (`tech.uslog.eauploader`), describing the implementation as of package version `1.5.1` on the `beta` branch.

| Document | Contents |
| --- | --- |
| [Overview.md](Overview.md) | Purpose, requirements, dependencies, optional integrations, repository layout |
| [Architecture.md](Architecture.md) | Startup sequence, core state, main window and screen navigation, persisted files |
| [PrefabManagement.md](PrefabManagement.md) | Managed Prefab Variants, import flow, metadata, tags, previews, deletion, legacy migration |
| [UI.md](UI.md) | Screens (Avatar List / Edit / Upload), modals, shared components, themes, localization |
| [Upload.md](Upload.md) | Build & Test, upload pipeline, multi-platform builds, Blueprint ID handling |
| [EditTools.md](EditTools.md) | Edit tool framework and the built-in edit tools |
| [PluginAPI.md](PluginAPI.md) | Public API for third-party plugins |
| [Conventions.md](Conventions.md) | Coding and contribution conventions |

All paths in these documents are relative to the package root (`Packages/tech.uslog.eauploader`) unless stated otherwise.
