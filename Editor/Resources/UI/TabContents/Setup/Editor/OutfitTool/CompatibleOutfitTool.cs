using EAUploader.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Core;
using Object = UnityEngine.Object;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#dress-up-compatible-outfits
    internal class CompatibleOutfitTool : EditToolModal
    {
        private const string MENU_NAME = "EAUploader/Edit Tools/Dress Up (Compatible Outfits)";
        private const float TILE_WIDTH = 112f;

        private static readonly (HumanBodyBones? bone, string label)[] ATTACH_TARGETS =
        {
            (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.Neck, "Neck"),
            (HumanBodyBones.Chest, "Chest"),
            (HumanBodyBones.Spine, "Spine"),
            (HumanBodyBones.Hips, "Hips"),
            (HumanBodyBones.LeftHand, "Left hand"),
            (HumanBodyBones.RightHand, "Right hand"),
            (HumanBodyBones.LeftLowerArm, "Left lower arm"),
            (HumanBodyBones.RightLowerArm, "Right lower arm"),
            (HumanBodyBones.LeftFoot, "Left foot"),
            (HumanBodyBones.RightFoot, "Right foot"),
            (null, "Avatar root"),
        };

        private string searchQuery = string.Empty;
        private VisualElement catalog;

        protected override string ToolTitle => T7e.Get("Dress Up (Compatible Outfits)");
        protected override float SidePanelWidth => 400f;

#if HAS_MA
        private static bool UsesModularAvatar => true;
#else
        private static bool UsesModularAvatar => false;
#endif

        #region Registration

        [EAUPlugin]
        private static void Initialize()
        {
            EAUploaderEditorManager.RegisterEditor(new EditorRegistration
            {
                MenuName = MENU_NAME,
                EditorName = "Dress Up (Compatible Outfits)",
                Description = "Dress the selected avatar with outfits and accessories from the list.",
                Version = "1.0.0",
                Author = "USLOG",
                Url = "https://uslog.tech/eauploader",
                Requirement = EditToolUtility.IsAvatar,
                RequirementDescription = "The selected item must be an avatar with a VRCAvatarDescriptor."
            });
        }

        [MenuItem(MENU_NAME)]
        public static void Open()
        {
            ShowTool<CompatibleOutfitTool>();
        }

        #endregion

        #region Side panel

        protected override void BuildSidePanel(VisualElement panel)
        {
            if (Session.Root.GetComponent<VRC.SDKBase.VRC_AvatarDescriptor>() == null)
            {
                panel.Add(EditToolUtility.StatusRow("warning", "warning",
                    T7e.Get("The selected item must be an avatar with a VRCAvatarDescriptor.")));
                return;
            }

            panel.Add(UsesModularAvatar
                ? EditToolUtility.StatusRow("success", "check_circle",
                    T7e.Get("Modular Avatar is installed. Outfits are set up without changing the avatar's bones."))
                : EditToolUtility.StatusRow("warning", "info",
                    T7e.Get("Modular Avatar is not installed. Outfit bones are merged only into the uploaded copy, so the avatar itself is not changed.")));

            BuildWornSection(panel);
            BuildPartsSection(panel);
            BuildCatalogSection(panel);
        }

        protected override void BuildFooterActions(VisualElement footer)
        {
            footer.Add(EditToolUtility.IconButton("person_add", T7e.Get("Save as new avatar"), SaveAsNewAvatar));
        }

        private void BuildWornSection(VisualElement panel)
        {
            var section = CreateSection(panel, T7e.Get("Wearing"));

            var outfits = Session.Root.GetComponentsInChildren<EAUploaderOutfit>(true);
            if (outfits.Length == 0)
            {
                section.Add(EditToolUtility.Note(T7e.Get("No outfits are worn yet. Choose one below.")));
                return;
            }

            foreach (var outfit in outfits)
            {
                var row = CreateToggleRow(outfit.gameObject.name, outfit.gameObject.activeSelf, value =>
                {
                    outfit.gameObject.SetActive(value);
                    MarkChanged();
                });

                var remove = EditToolUtility.IconButton("delete", null, () => RemoveOutfit(outfit));
                remove.tooltip = T7e.Get("Take off");
                row.Add(remove);

                section.Add(row);
            }
        }

        private void BuildPartsSection(VisualElement panel)
        {
            var parts = GetAvatarParts();
            if (parts.Count == 0) return;

            var section = CreateSection(panel, T7e.Get("Avatar parts"));

            foreach (var part in parts)
            {
                section.Add(CreateToggleRow(part.name, part.gameObject.activeSelf, value =>
                {
                    part.gameObject.SetActive(value);
                    MarkChanged();
                }));
            }

            section.Add(EditToolUtility.Note(T7e.Get("Turn off parts such as the original clothes that should not be shown with the outfit.")));
            section.Add(EditToolUtility.IconButton("restart_alt", T7e.Get("Restore original visibility"), () =>
            {
                RestorePartsVisibility(parts);
                MarkChanged();
                RebuildSidePanel();
            }));
        }

        private void BuildCatalogSection(VisualElement panel)
        {
            var section = CreateSection(panel, T7e.Get("Add outfit"));

            var search = new TextFieldPro { placeholder = T7e.Get("Search outfits") };
            search.AddToClassList("mb-2");
            search.RegisterValueChangedCallback(_ =>
            {
                searchQuery = search.GetValue() ?? string.Empty;
                RefreshCatalog();
            });
            section.Add(search);

            catalog = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            section.Add(catalog);

            RefreshCatalog();
        }

        private void RefreshCatalog()
        {
            if (catalog == null) return;
            catalog.Clear();

            var items = PrefabManager.GetAllPrefabsWithPreview()
                .Where(p => p.Genre == EAUploaderMeta.PrefabGenre.Cloth || p.Genre == EAUploaderMeta.PrefabGenre.Accessory)
                .Where(p => p.Path != TargetPrefabPath)
                .Where(p => string.IsNullOrEmpty(searchQuery)
                    || p.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0
                    || p.Tags.Any(t => t.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (items.Count == 0)
            {
                catalog.Add(EditToolUtility.Note(T7e.Get("No outfits or accessories found. Import them from the list screen first.")));
                return;
            }

            foreach (var item in items)
            {
                catalog.Add(CreateCatalogTile(item));
            }
        }

        private VisualElement CreateCatalogTile(PrefabInfo item)
        {
            var tile = new Button
            {
                tooltip = item.Name,
                style =
                {
                    width = TILE_WIDTH,
                    flexDirection = FlexDirection.Column,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingLeft = 2,
                    paddingRight = 2,
                }
            };
            tile.clicked += () => PutOn(item, tile);

            tile.Add(new Image
            {
                image = item.Preview,
                scaleMode = ScaleMode.ScaleAndCrop,
                style = { height = TILE_WIDTH * AvatarList.Main.THUMBNAIL_RATIO }
            });

            var name = new Label(item.Name) { style = { overflow = Overflow.Hidden, unityTextAlign = TextAnchor.MiddleCenter } };
            name.AddToClassList("no-translate");
            name.AddToClassList("text-sm");
            tile.Add(name);

            var genre = new Label(T7e.Get(item.Genre == EAUploaderMeta.PrefabGenre.Cloth ? "Cloth" : "Accessory"))
            {
                style = { unityTextAlign = TextAnchor.MiddleCenter }
            };
            genre.AddToClassList("text-sm");
            tile.Add(genre);

            return tile;
        }

        private static VisualElement CreateSection(VisualElement panel, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("pt-2");
            section.AddToClassList("pb-2");
            section.AddToClassList("mb-2");
            section.AddToClassList("border-b");
            section.AddToClassList("border-zinc-300");
            section.Add(EditToolUtility.Heading(title));
            panel.Add(section);
            return section;
        }

        private static VisualElement CreateToggleRow(string text, bool value, Action<bool> onChanged)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 }
            };

            var toggle = new Toggle { value = value, style = { marginRight = 6 } };
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(toggle);

            var label = new Label(text) { style = { flexGrow = 1, overflow = Overflow.Hidden } };
            label.AddToClassList("no-translate");
            row.Add(label);

            return row;
        }

        private List<Transform> GetAvatarParts()
        {
            var root = Session.Root.transform;
            var hips = OutfitBoneMapper.FindHumanBone(Session.Root, HumanBodyBones.Hips);

            Transform armature = null;
            for (var t = hips; t != null && t != root; t = t.parent)
            {
                if (t.parent == root) armature = t;
            }

            return root.Cast<Transform>()
                .Where(t => t != armature)
                .Where(t => t.GetComponent<EAUploaderOutfit>() == null)
                .Where(t => t.GetComponentsInChildren<Renderer>(true).Any())
                .ToList();
        }

        #endregion

        #region Dressing

        private void PutOn(PrefabInfo item, VisualElement anchor)
        {
            var asset = PrefabManager.GetPrefab(item.Path);
            if (asset == null) return;

            if (OutfitBoneMapper.HasArmature(Session.Root, asset))
            {
                DressWithArmature(item, asset);
                return;
            }

            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent(T7e.Get("Attach to")));
            menu.AddSeparator(string.Empty);
            foreach (var (bone, label) in ATTACH_TARGETS)
            {
                var target = bone;
                menu.AddItem(new GUIContent(T7e.Get(label)), false, () => AttachToBone(item, asset, target));
            }
            menu.DropDown(anchor.worldBound);
        }

        private GameObject CreateOutfitInstance(PrefabInfo item, GameObject asset, Transform parent)
        {
            var instance = Object.Instantiate(asset, parent);
            instance.name = item.Name;
            instance.SetActive(true);

            foreach (var meta in instance.GetComponentsInChildren<EAUploaderMeta>(true))
            {
                Object.DestroyImmediate(meta);
            }

            var outfit = instance.AddComponent<EAUploaderOutfit>();
            outfit.sourcePrefabGuid = AssetDatabase.AssetPathToGUID(item.Path);
            return instance;
        }

        private void DressWithArmature(PrefabInfo item, GameObject asset)
        {
            var instance = CreateOutfitInstance(item, asset, Session.Root.transform);
            var outfit = instance.GetComponent<EAUploaderOutfit>();

            bool success;
            string error;

#if HAS_MA
            outfit.mode = EAUploaderOutfit.SetupMode.ModularAvatar;
            success = OutfitModularAvatar.Setup(Session.Root, instance, out error);
#else
            outfit.mode = EAUploaderOutfit.SetupMode.BoneMerge;
            success = OutfitBoneMapper.CanMerge(Session.Root, instance, out error);
#endif

            if (!success)
            {
                Object.DestroyImmediate(instance);
                EditorUtility.DisplayDialog(T7e.Get("Dress Up (Compatible Outfits)"),
                    T7e.Get("Could not put on this outfit.") + (string.IsNullOrEmpty(error) ? string.Empty : "\n\n" + error),
                    "OK");
                return;
            }

            MarkChanged();
            RebuildSidePanel();
        }

        private void AttachToBone(PrefabInfo item, GameObject asset, HumanBodyBones? bone)
        {
            var parent = bone.HasValue
                ? OutfitBoneMapper.FindHumanBone(Session.Root, bone.Value)
                : Session.Root.transform;

            if (parent == null)
            {
                EditorUtility.DisplayDialog(T7e.Get("Dress Up (Compatible Outfits)"), T7e.Get("This avatar does not have that bone."), "OK");
                return;
            }

            var instance = CreateOutfitInstance(item, asset, Session.Root.transform);
            instance.transform.SetParent(parent, true);
            instance.GetComponent<EAUploaderOutfit>().mode = EAUploaderOutfit.SetupMode.AttachToBone;

            MarkChanged();
            RebuildSidePanel();
        }

        private void RemoveOutfit(EAUploaderOutfit outfit)
        {
            if (outfit == null) return;

            if (!EditorUtility.DisplayDialog(T7e.Get("Dress Up (Compatible Outfits)"),
                    T7e.Get("Take off this outfit?") + "\n\n" + outfit.gameObject.name,
                    T7e.Get("Take off"), T7e.Get("Cancel")))
            {
                return;
            }

            Object.DestroyImmediate(outfit.gameObject);

            MarkChanged();
            RebuildSidePanel();
        }

        private static void RestorePartsVisibility(List<Transform> parts)
        {
            foreach (var part in parts)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(part.gameObject);
                part.gameObject.SetActive(source == null || source.activeSelf);
            }
        }

        private void MarkChanged()
        {
            MarkDirty();
            RefreshPreview();
        }

        #endregion

        #region Saving

        private void SaveAsNewAvatar()
        {
            if (Session == null) return;

            var folder = ManagedPrefabs.GetItemFolder(TargetPrefabPath);
            if (folder == null) return;

            var firstOutfit = Session.Root.GetComponentsInChildren<EAUploaderOutfit>(true).FirstOrDefault();
            var baseName = Path.GetFileNameWithoutExtension(TargetPrefabPath);
            var name = firstOutfit != null ? $"{baseName} {firstOutfit.gameObject.name}" : $"{baseName} {T7e.Get("Outfit")}";
            var newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{EditToolUtility.SanitizeFileName(name)}.prefab");

            var pipeline = Session.Root.GetComponent<PipelineManager>();
            var blueprintId = pipeline != null ? pipeline.blueprintId : null;
            if (pipeline != null) pipeline.blueprintId = string.Empty;

            var meta = Session.Root.GetComponent<EAUploaderMeta>();
            if (meta != null) meta.lastModifiedTicks = DateTime.UtcNow.Ticks;

            if (!Session.SaveTo(newPath))
            {
                if (pipeline != null) pipeline.blueprintId = blueprintId;
                return;
            }

            Retarget(newPath);
            EAUploaderCore.selectedPrefabPath = newPath;

            PrefabManager.InvalidateCache();
            AvatarList.Main.RequestRefresh();

            var window = EAUploader.InstanceOrNull;
            if (window && window.CurrentScreen == EAUploaderScreen.Setup)
            {
                EAUploader.RefreshCurrentScreen();
            }
        }

        #endregion
    }
}
