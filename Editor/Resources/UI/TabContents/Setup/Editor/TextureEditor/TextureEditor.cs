using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#texture-editor
    internal class TextureEditor : EditToolModal
    {
        private const string MENU_NAME = "EAUploader/Edit Tools/Texture Editor";
        private const string TEXTURE_FOLDER = "Textures";
        private const string MATERIAL_FOLDER = "Materials";

        private class MaterialEdit
        {
            public Material Current;
            public Material Original;
            public TextureEditRecord Record;
            public string Property;
            public Texture Source;
            public TextureAdjustment Adjustment = new TextureAdjustment();
            public Material PreviewMaterial;
            public RenderTexture PreviewTexture;
            public bool Changed;
        }

        private GameObject materialsRoot;
        private List<Material> materials = new List<Material>();
        private readonly Dictionary<Material, MaterialEdit> edits = new Dictionary<Material, MaterialEdit>();
        private Material selected;

        private VisualElement materialList;
        private VisualElement detailPanel;

        protected override string ToolTitle => T7e.Get("Texture Editor");

        #region Registration

        [EAUPlugin]
        private static void Initialize()
        {
            EAUploaderEditorManager.RegisterEditor(new EditorRegistration
            {
                MenuName = MENU_NAME,
                EditorName = "Texture Editor",
                Description = "Change the colors of the textures used by the selected item, or replace them with other images.",
                Version = "1.0.0",
                Author = "USLOG",
                Url = "https://uslog.tech/eauploader",
                Requirement = HasTextures,
                RequirementDescription = "The selected item must have a material with a texture."
            });
        }

        [MenuItem(MENU_NAME)]
        public static void Open()
        {
            ShowTool<TextureEditor>();
        }

        private static bool HasTextures(string prefabPath)
        {
            var prefab = EditToolUtility.LoadPrefab(prefabPath);
            return prefab != null && prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && GetTextureProperties(m).Count > 0);
        }

        #endregion

        #region Side panel

        protected override void BuildSidePanel(VisualElement panel)
        {
            if (materialsRoot != Session.Root)
            {
                ClearEdits();
                materialsRoot = Session.Root;
                materials = CollectMaterials();
                selected = materials.FirstOrDefault();
            }

            if (materials.Count == 0)
            {
                panel.Add(EditToolUtility.StatusRow("warning", "warning",
                    T7e.Get("The selected item must have a material with a texture.")));
                return;
            }

            panel.Add(EditToolUtility.Heading(T7e.Get("Materials")));

            materialList = new VisualElement();
            materialList.AddToClassList("pb-2");
            materialList.AddToClassList("mb-2");
            materialList.AddToClassList("border-b");
            materialList.AddToClassList("border-zinc-300");
            panel.Add(materialList);

            detailPanel = new VisualElement();
            panel.Add(detailPanel);

            RefreshMaterialList();
            RefreshDetail();
        }

        private List<Material> CollectMaterials()
        {
            return Session.Root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && GetTextureProperties(m).Count > 0)
                .Distinct()
                .ToList();
        }

        private void RefreshMaterialList()
        {
            materialList.Clear();

            foreach (var material in materials)
            {
                var edit = edits.TryGetValue(material, out var existing) ? existing : null;

                var row = new Button(() =>
                {
                    selected = material;
                    RefreshMaterialList();
                    RefreshDetail();
                })
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4, height = 44 }
                };
                row.EnableInClassList("selected", material == selected);

                var thumbnail = new Image
                {
                    image = edit?.PreviewTexture != null ? (Texture)edit.PreviewTexture : Thumbnail(material),
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 36, height = 36, marginRight = 8 }
                };
                row.Add(thumbnail);

                var name = new Label(material.name) { style = { flexGrow = 1, overflow = Overflow.Hidden } };
                name.AddToClassList("no-translate");
                row.Add(name);

                if (edit != null && edit.Changed)
                {
                    row.Add(new Label(T7e.Get("Changed")) { style = { marginLeft = 4 } });
                }
                else if (TextureEditRecord.Read(material) != null)
                {
                    row.Add(new Label(T7e.Get("Edited")) { style = { marginLeft = 4 } });
                }

                materialList.Add(row);
            }
        }

        #endregion

        #region Detail

        private void RefreshDetail()
        {
            detailPanel.Clear();
            if (selected == null) return;

            var edit = GetEdit(selected);

            var title = EditToolUtility.Heading(T7e.Get("Adjust colors"));
            detailPanel.Add(title);

            var properties = GetTextureProperties(edit.Current);
            var propertyDropdown = new DropdownField(T7e.Get("Texture"),
                properties.Select(p => DescribeProperty(edit.Current, p)).ToList(),
                Mathf.Max(0, properties.IndexOf(edit.Property)));
            propertyDropdown.AddToClassList("mb-2");
            propertyDropdown.RegisterValueChangedCallback(_ =>
            {
                ChangeProperty(edit, properties[propertyDropdown.index]);
                RefreshDetail();
            });
            detailPanel.Add(propertyDropdown);

            var texturePreview = new Image
            {
                image = edit.PreviewTexture != null ? (Texture)edit.PreviewTexture : edit.Source,
                scaleMode = ScaleMode.ScaleToFit,
                style = { height = 180, marginBottom = 8 }
            };
            detailPanel.Add(texturePreview);

            if (edit.Source == null)
            {
                detailPanel.Add(EditToolUtility.StatusRow("warning", "warning",
                    T7e.Get("This material has no texture in the selected slot.")));
                return;
            }

            var sourceLabel = new Label(edit.Source.name + $"  ({edit.Source.width}x{edit.Source.height})");
            sourceLabel.AddToClassList("no-translate");
            sourceLabel.AddToClassList("text-sm");
            sourceLabel.AddToClassList("pb-2");
            detailPanel.Add(sourceLabel);

            detailPanel.Add(CreateSlider(T7e.Get("Hue"), -180f, 180f, edit.Adjustment.hue, v => edit.Adjustment.hue = v, edit, texturePreview));
            detailPanel.Add(CreateSlider(T7e.Get("Saturation (%)"), 0f, 200f, edit.Adjustment.saturation * 100f, v => edit.Adjustment.saturation = v / 100f, edit, texturePreview));
            detailPanel.Add(CreateSlider(T7e.Get("Brightness (%)"), 0f, 200f, edit.Adjustment.brightness * 100f, v => edit.Adjustment.brightness = v / 100f, edit, texturePreview));
            detailPanel.Add(CreateSlider(T7e.Get("Contrast (%)"), 0f, 200f, edit.Adjustment.contrast * 100f, v => edit.Adjustment.contrast = v / 100f, edit, texturePreview));

            var tintField = new ColorField(T7e.Get("Tint")) { value = edit.Adjustment.tint, showAlpha = false, hdr = false };
            tintField.AddToClassList("mb-2");
            tintField.RegisterValueChangedCallback(evt =>
            {
                edit.Adjustment.tint = evt.newValue;
                UpdatePreview(edit, texturePreview);
            });
            detailPanel.Add(tintField);

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            actions.Add(EditToolUtility.IconButton("restart_alt", T7e.Get("Reset adjustments"), () =>
            {
                edit.Adjustment = new TextureAdjustment();
                UpdatePreview(edit, texturePreview);
                RefreshDetail();
            }));
            actions.Add(EditToolUtility.IconButton("image", T7e.Get("Replace image..."), () =>
            {
                ReplaceSource(edit);
                RefreshDetail();
            }));
            if (edit.Record != null && edit.Original != null && edit.Original != edit.Current)
            {
                actions.Add(EditToolUtility.IconButton("history", T7e.Get("Restore original material"), () => RestoreOriginal(edit)));
            }
            detailPanel.Add(actions);

            detailPanel.Add(EditToolUtility.Note(T7e.Get("The original textures and materials are not changed. Edited copies are saved in the item folder when you apply.")));
        }

        private VisualElement CreateSlider(string label, float min, float max, float value,
            System.Action<float> setter, MaterialEdit edit, Image texturePreview)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.AddToClassList("mb-1");
            slider.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                UpdatePreview(edit, texturePreview);
            });
            return slider;
        }

        private static readonly Dictionary<string, string> PROPERTY_LABELS = new Dictionary<string, string>
        {
            { "_MainTex", "Main texture" },
            { "_BaseMap", "Main texture" },
            { "_Main2ndTex", "2nd texture" },
            { "_Main3rdTex", "3rd texture" },
            { "_ShadowColorTex", "Shadow color" },
            { "_Shadow2ndColorTex", "2nd shadow color" },
            { "_Shadow3rdColorTex", "3rd shadow color" },
            { "_EmissionMap", "Emission" },
            { "_Emission2ndMap", "2nd emission" },
            { "_OutlineTex", "Outline" },
            { "_MatCapTex", "MatCap" },
            { "_MatCap2ndTex", "2nd MatCap" },
            { "_RimColorTex", "Rim light" },
        };

        private static string DescribeProperty(Material material, string property)
        {
            string label;
            if (PROPERTY_LABELS.TryGetValue(property, out var key))
            {
                label = T7e.Get(key);
            }
            else
            {
                var index = material.shader.FindPropertyIndex(property);
                var description = index >= 0 ? material.shader.GetPropertyDescription(index) : null;
                label = string.IsNullOrWhiteSpace(description) || !description.Contains(" ")
                    ? property.TrimStart('_')
                    : description;
            }

            var texture = material.GetTexture(property);
            return texture != null ? $"{label} ({texture.name})" : label;
        }

        #endregion

        #region Editing

        private MaterialEdit GetEdit(Material material)
        {
            if (edits.TryGetValue(material, out var edit)) return edit;

            var record = TextureEditRecord.Read(material);
            var original = record != null ? LoadByGuid<Material>(record.originalMaterial) : null;

            edit = new MaterialEdit
            {
                Current = material,
                Original = original != null ? original : material,
                Record = record,
            };

            var properties = GetTextureProperties(material);
            var property = record != null && properties.Contains(record.property)
                ? record.property
                : DefaultProperty(material, properties);

            SetProperty(edit, property);
            edits[material] = edit;
            return edit;
        }

        private static void SetProperty(MaterialEdit edit, string property)
        {
            edit.Property = property;

            if (edit.Record != null && edit.Record.property == property)
            {
                var source = LoadByGuid<Texture>(edit.Record.sourceTexture);
                edit.Source = source != null ? source : edit.Current.GetTexture(property);
                edit.Adjustment = edit.Record.adjustment != null ? edit.Record.adjustment.Clone() : new TextureAdjustment();
            }
            else
            {
                edit.Source = edit.Current.GetTexture(property);
                edit.Adjustment = new TextureAdjustment();
            }
        }

        private void ChangeProperty(MaterialEdit edit, string property)
        {
            if (edit.Property == property) return;

            if (edit.PreviewMaterial != null)
            {
                edit.PreviewMaterial.SetTexture(edit.Property, edit.Current.GetTexture(edit.Property));
                ReleasePreviewTexture(edit);
            }

            SetProperty(edit, property);
            if (edit.Changed) UpdatePreview(edit, null);
        }

        private void UpdatePreview(MaterialEdit edit, Image texturePreview)
        {
            if (edit.Source == null) return;

            if (edit.PreviewMaterial == null)
            {
                edit.PreviewMaterial = new Material(edit.Current)
                {
                    name = edit.Current.name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                ReplaceMaterial(edit.Current, edit.PreviewMaterial);
            }

            if (edit.PreviewTexture == null)
            {
                edit.PreviewTexture = TextureAdjuster.CreatePreviewTexture(edit.Source);
                edit.PreviewMaterial.SetTexture(edit.Property, edit.PreviewTexture);
            }

            TextureAdjuster.Apply(edit.Source, edit.PreviewTexture, edit.Adjustment);

            if (texturePreview != null)
            {
                texturePreview.image = edit.PreviewTexture;
                texturePreview.MarkDirtyRepaint();
            }

            if (!edit.Changed)
            {
                edit.Changed = true;
                RefreshMaterialList();
            }

            MarkDirty();
        }

        private void ReplaceSource(MaterialEdit edit)
        {
            var path = EditorUtility.OpenFilePanel(T7e.Get("Replace image..."), "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;

            var folder = EditToolUtility.GetGeneratedFolder(TargetPrefabPath, TEXTURE_FOLDER);
            var settingsSource = edit.Source != null ? edit.Source : edit.Current.GetTexture(edit.Property);
            var texture = TextureAdjuster.ImportImageFile(path, folder, settingsSource);
            if (texture == null) return;

            ReleasePreviewTexture(edit);
            edit.Source = texture;
            UpdatePreview(edit, null);
        }

        private void RestoreOriginal(MaterialEdit edit)
        {
            var assigned = edit.PreviewMaterial != null ? edit.PreviewMaterial : edit.Current;
            ReplaceMaterial(assigned, edit.Original);
            DisposeEdit(edit);
            edits.Remove(edit.Current);

            var index = materials.IndexOf(edit.Current);
            if (index >= 0)
            {
                if (materials.Contains(edit.Original)) materials.RemoveAt(index);
                else materials[index] = edit.Original;
            }

            selected = edit.Original;
            MarkDirty();
            RefreshMaterialList();
            RefreshDetail();
        }

        private void ReplaceMaterial(Material from, Material to)
        {
            foreach (var renderer in Session.Root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                var replaced = false;

                for (var i = 0; i < shared.Length; i++)
                {
                    if (shared[i] != from) continue;
                    shared[i] = to;
                    replaced = true;
                }

                if (replaced) renderer.sharedMaterials = shared;
            }

            RefreshPreview();
        }

        #endregion

        #region Saving

        protected override void OnBeforeSave()
        {
            foreach (var edit in edits.Values.Where(e => e.Changed && e.PreviewMaterial != null).ToList())
            {
                Commit(edit);
            }

            AssetDatabase.SaveAssets();
        }

        private void Commit(MaterialEdit edit)
        {
            var originalTexture = edit.Original.HasProperty(edit.Property) ? edit.Original.GetTexture(edit.Property) : null;

            if (edit.Adjustment.IsIdentity && edit.Source == originalTexture)
            {
                ReplaceMaterial(edit.PreviewMaterial, edit.Original);
                ReplaceKey(edit, edit.Original);
                return;
            }

            var textureFolder = EditToolUtility.GetGeneratedFolder(TargetPrefabPath, TEXTURE_FOLDER);
            var materialFolder = EditToolUtility.GetGeneratedFolder(TargetPrefabPath, MATERIAL_FOLDER);
            var baseName = EditToolUtility.SanitizeFileName(edit.Original.name);

            var texturePath = edit.Record != null && edit.Record.property == edit.Property
                ? AssetDatabase.GUIDToAssetPath(edit.Record.generatedTexture)
                : null;
            if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith(textureFolder + "/")
                || IsUsedByOtherPrefabs(texturePath))
            {
                texturePath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{textureFolder}/{baseName}_{edit.Property.TrimStart('_')}.png");
            }

            var png = TextureAdjuster.BakeToPng(edit.Source, edit.Adjustment);
            var generatedTexture = TextureAdjuster.WriteTexture(texturePath, png, edit.Source);

            Material materialAsset;
            var currentPath = AssetDatabase.GetAssetPath(edit.Current);
            if (edit.Record != null && currentPath.StartsWith(materialFolder + "/") && !IsUsedByOtherPrefabs(currentPath))
            {
                materialAsset = edit.Current;
            }
            else
            {
                var materialPath = AssetDatabase.GenerateUniqueAssetPath($"{materialFolder}/{baseName} (Edited).mat");
                materialAsset = new Material(edit.Current) { name = Path.GetFileNameWithoutExtension(materialPath) };
                AssetDatabase.CreateAsset(materialAsset, materialPath);
            }

            materialAsset.SetTexture(edit.Property, generatedTexture);
            EditorUtility.SetDirty(materialAsset);
            AssetDatabase.SaveAssets();

            new TextureEditRecord
            {
                originalMaterial = GuidOf(edit.Original),
                sourceTexture = GuidOf(edit.Source),
                generatedTexture = GuidOf(generatedTexture),
                property = edit.Property,
                adjustment = edit.Adjustment.Clone(),
            }.Write(materialAsset);

            ReplaceMaterial(edit.PreviewMaterial, materialAsset);
            ReplaceKey(edit, materialAsset);
        }

        private void ReplaceKey(MaterialEdit edit, Material newMaterial)
        {
            var index = materials.IndexOf(edit.Current);
            if (index >= 0)
            {
                if (newMaterial != edit.Current && materials.Contains(newMaterial)) materials.RemoveAt(index);
                else materials[index] = newMaterial;
            }

            if (selected == edit.Current) selected = newMaterial;

            DisposeEdit(edit);
            edits.Remove(edit.Current);
        }

        protected override void OnAfterSave()
        {
            RebuildSidePanel();
        }

        #endregion

        #region Helpers

        private static List<string> GetTextureProperties(Material material)
        {
            var shader = material.shader;
            var result = new List<string>();
            if (shader == null) return result;

            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                if (shader.GetPropertyTextureDimension(i) != TextureDimension.Tex2D) continue;

                var name = shader.GetPropertyName(i);
                var texture = material.GetTexture(name) as Texture2D;
                if (texture == null || IsNormalMap(texture)) continue;

                result.Add(name);
            }

            return result;
        }

        private static string DefaultProperty(Material material, List<string> properties)
        {
            if (properties.Contains("_MainTex")) return "_MainTex";
            if (properties.Contains("_BaseMap")) return "_BaseMap";
            return properties.FirstOrDefault();
        }

        private static Texture Thumbnail(Material material)
        {
            var properties = GetTextureProperties(material);
            var property = DefaultProperty(material, properties);
            return property != null ? material.GetTexture(property) : null;
        }

        private bool IsUsedByOtherPrefabs(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            return CustomPrefabUtility.ManagedPrefabs.GetAllVariantPaths()
                .Where(p => p != TargetPrefabPath)
                .Any(p => AssetDatabase.GetDependencies(p, true).Contains(assetPath));
        }

        private static bool IsNormalMap(Texture texture)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return importer != null && importer.textureType == TextureImporterType.NormalMap;
        }

        private static T LoadByGuid<T>(string guid) where T : Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static string GuidOf(Object asset)
        {
            return asset == null ? null : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }

        private static void ReleasePreviewTexture(MaterialEdit edit)
        {
            if (edit.PreviewTexture == null) return;

            edit.PreviewTexture.Release();
            Object.DestroyImmediate(edit.PreviewTexture);
            edit.PreviewTexture = null;
        }

        private static void DisposeEdit(MaterialEdit edit)
        {
            ReleasePreviewTexture(edit);
            if (edit.PreviewMaterial != null) Object.DestroyImmediate(edit.PreviewMaterial);
            edit.PreviewMaterial = null;
        }

        private void ClearEdits()
        {
            foreach (var edit in edits.Values) DisposeEdit(edit);
            edits.Clear();
        }

        #endregion

        protected override void OnClose()
        {
            ClearEdits();
            materialsRoot = null;
        }
    }
}
