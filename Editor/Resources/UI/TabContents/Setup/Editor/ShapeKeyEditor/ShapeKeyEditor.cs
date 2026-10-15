using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#shape-key-editor
    internal class ShapeKeyEditor : EditToolModal
    {
        private const string MENU_NAME = "EAUploader/Edit Tools/Shape Key Editor";
        private const float ROW_HEIGHT = 28f;

        private class ShapeKeyEntry
        {
            public int Index;
            public string Name;
            public float Max;
            public float Original;
            public string Usage;
        }

        private List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
        private SkinnedMeshRenderer currentRenderer;
        private readonly Dictionary<SkinnedMeshRenderer, float[]> originalWeights = new Dictionary<SkinnedMeshRenderer, float[]>();
        private List<ShapeKeyEntry> allEntries = new List<ShapeKeyEntry>();
        private readonly List<ShapeKeyEntry> visibleEntries = new List<ShapeKeyEntry>();

        private ListView listView;
        private Label countLabel;
        private string searchQuery = string.Empty;
        private bool onlyChanged;

        protected override string ToolTitle => T7e.Get("Shape Key Editor");

        #region Registration

        [EAUPlugin]
        private static void Initialize()
        {
            EAUploaderEditorManager.RegisterEditor(new EditorRegistration
            {
                MenuName = MENU_NAME,
                EditorName = "Shape Key Editor",
                Description = "Adjust the shape keys (blend shapes) of the selected item and save them as its default.",
                Version = "1.0.0",
                Author = "USLOG",
                Url = "https://uslog.tech/eauploader",
                Requirement = HasShapeKeys,
                RequirementDescription = "The selected item must have a mesh with shape keys."
            });
        }

        [MenuItem(MENU_NAME)]
        public static void Open()
        {
            ShowTool<ShapeKeyEditor>();
        }

        private static bool HasShapeKeys(string prefabPath)
        {
            var prefab = EditToolUtility.LoadPrefab(prefabPath);
            return prefab != null && prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(r => r.sharedMesh && r.sharedMesh.blendShapeCount > 0);
        }

        #endregion

        #region Side panel

        protected override void BuildSidePanel(VisualElement panel)
        {
            CollectRenderers();

            if (renderers.Count == 0)
            {
                panel.Add(EditToolUtility.StatusRow("warning", "warning",
                    T7e.Get("The selected item must have a mesh with shape keys.")));
                return;
            }

            panel.Add(EditToolUtility.Heading(T7e.Get("Mesh")));

            var meshChoices = renderers.Select(DescribeRenderer).ToList();
            if (currentRenderer == null || !renderers.Contains(currentRenderer))
            {
                currentRenderer = PickDefaultRenderer();
            }

            var meshDropdown = new DropdownField(meshChoices, renderers.IndexOf(currentRenderer));
            meshDropdown.AddToClassList("no-translate");
            meshDropdown.AddToClassList("mb-2");
            meshDropdown.RegisterValueChangedCallback(_ =>
            {
                currentRenderer = renderers[meshDropdown.index];
                RefreshEntries();
                Preview?.Repaint();
            });
            panel.Add(meshDropdown);

            var focusRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            focusRow.Add(EditToolUtility.IconButton("face", T7e.Get("Focus on face"), FocusFace));
            focusRow.Add(EditToolUtility.IconButton("accessibility", T7e.Get("Whole body"), () => Preview?.ResetPreview()));
            panel.Add(focusRow);

            panel.Add(EditToolUtility.Heading(T7e.Get("Shape Keys")));

            var search = new TextFieldPro { placeholder = T7e.Get("Search shape keys") };
            search.AddToClassList("mb-2");
            search.RegisterValueChangedCallback(_ =>
            {
                searchQuery = search.GetValue() ?? string.Empty;
                ApplyFilter();
            });
            panel.Add(search);

            var onlyChangedToggle = new Toggle(T7e.Get("Show changed only")) { value = onlyChanged };
            onlyChangedToggle.RegisterValueChangedCallback(evt =>
            {
                onlyChanged = evt.newValue;
                ApplyFilter();
            });
            panel.Add(onlyChangedToggle);

            countLabel = new Label();
            countLabel.AddToClassList("text-sm");
            countLabel.AddToClassList("pb-1");
            panel.Add(countLabel);

            listView = new ListView(visibleEntries, ROW_HEIGHT, MakeRow, BindRow)
            {
                selectionType = SelectionType.None,
                style = { height = 420, flexGrow = 1 }
            };
            listView.RegisterCallback<GeometryChangedEvent>(_ =>
                listView.Query<Label>(className: "unity-list-view__empty-label")
                    .ForEach(l => l.text = T7e.Get("No matching shape keys.")));
            panel.Add(listView);

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8, flexWrap = Wrap.Wrap } };
            actions.Add(EditToolUtility.IconButton("restart_alt", T7e.Get("Reset this mesh"), ResetCurrentRenderer));
            actions.Add(EditToolUtility.IconButton("exposure_zero", T7e.Get("Set all to 0"), ZeroCurrentRenderer));
            panel.Add(actions);

            panel.Add(EditToolUtility.Note(T7e.Get("Shape keys used for lip sync or blinking are controlled by VRChat while playing. Changing their default value may look wrong in game.")));

            RefreshEntries();
        }

        private void CollectRenderers()
        {
            renderers = Session.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh && r.sharedMesh.blendShapeCount > 0)
                .ToList();

            var previous = originalWeights.Keys.ToList();
            foreach (var renderer in previous.Where(r => r == null || !renderers.Contains(r)))
            {
                originalWeights.Remove(renderer);
            }

            foreach (var renderer in renderers.Where(r => !originalWeights.ContainsKey(r)))
            {
                originalWeights[renderer] = Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                    .Select(renderer.GetBlendShapeWeight)
                    .ToArray();
            }

            if (currentRenderer == null)
            {
                currentRenderer = PickDefaultRenderer();
            }
        }

        private SkinnedMeshRenderer PickDefaultRenderer()
        {
            var descriptor = Session.Root.GetComponent<VRCAvatarDescriptor>();
            if (descriptor && descriptor.VisemeSkinnedMesh && renderers.Contains(descriptor.VisemeSkinnedMesh))
            {
                return descriptor.VisemeSkinnedMesh;
            }

            return renderers.OrderByDescending(r => r.sharedMesh.blendShapeCount).FirstOrDefault();
        }

        private string DescribeRenderer(SkinnedMeshRenderer renderer)
        {
            var path = AnimationUtility.CalculateTransformPath(renderer.transform, Session.Root.transform);
            return $"{path} ({renderer.sharedMesh.blendShapeCount})";
        }

        #endregion

        #region Entries

        private void RefreshEntries()
        {
            allEntries = new List<ShapeKeyEntry>();
            if (currentRenderer == null) return;

            var mesh = currentRenderer.sharedMesh;
            var originals = originalWeights[currentRenderer];
            var usages = CollectUsages(currentRenderer, mesh);

            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var frameCount = mesh.GetBlendShapeFrameCount(i);
                var max = frameCount > 0 ? mesh.GetBlendShapeFrameWeight(i, frameCount - 1) : 100f;

                allEntries.Add(new ShapeKeyEntry
                {
                    Index = i,
                    Name = mesh.GetBlendShapeName(i),
                    Max = Mathf.Max(1f, max),
                    Original = originals[i],
                    Usage = usages.TryGetValue(i, out var usage) ? usage : null,
                });
            }

            ApplyFilter();
        }

        private Dictionary<int, string> CollectUsages(SkinnedMeshRenderer renderer, Mesh mesh)
        {
            var usages = new Dictionary<int, string>();
            var descriptor = Session.Root.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return usages;

            if (descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && descriptor.VisemeSkinnedMesh == renderer
                && descriptor.VisemeBlendShapes != null)
            {
                foreach (var name in descriptor.VisemeBlendShapes.Where(n => !string.IsNullOrEmpty(n)))
                {
                    var index = mesh.GetBlendShapeIndex(name);
                    if (index >= 0) usages[index] = T7e.Get("Lip sync");
                }
            }

            var eyeLook = descriptor.customEyeLookSettings;
            if (descriptor.enableEyeLook
                && eyeLook.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && eyeLook.eyelidsSkinnedMesh == renderer
                && eyeLook.eyelidsBlendshapes != null)
            {
                foreach (var index in eyeLook.eyelidsBlendshapes.Where(i => i >= 0 && i < mesh.blendShapeCount))
                {
                    usages[index] = T7e.Get("Eyelids");
                }
            }

            return usages;
        }

        private void ApplyFilter()
        {
            visibleEntries.Clear();
            visibleEntries.AddRange(allEntries.Where(e =>
                (string.IsNullOrEmpty(searchQuery) || e.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                && (!onlyChanged || IsChanged(e))));

            if (countLabel != null)
            {
                countLabel.text = $"{visibleEntries.Count} / {allEntries.Count}";
            }

            listView?.RefreshItems();
        }

        private bool IsChanged(ShapeKeyEntry entry)
        {
            return !Mathf.Approximately(currentRenderer.GetBlendShapeWeight(entry.Index), entry.Original);
        }

        #endregion

        #region Rows

        private VisualElement MakeRow()
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, height = ROW_HEIGHT }
            };

            var name = new Label { name = "shape_name", style = { width = 120, overflow = Overflow.Hidden, unityTextAlign = TextAnchor.MiddleLeft } };
            name.AddToClassList("no-translate");
            row.Add(name);

            var slider = new Slider(0f, 100f) { name = "shape_slider", style = { flexGrow = 1, marginLeft = 4, marginRight = 4 } };
            row.Add(slider);

            var field = new FloatField { name = "shape_value", style = { width = 52 } };
            row.Add(field);

            var reset = new Button { name = "shape_reset", style = { width = 24, height = 22, paddingLeft = 0, paddingRight = 0 } };
            reset.Add(new MaterialIcon { icon = "undo", style = { fontSize = 14 } });
            row.Add(reset);

            slider.RegisterValueChangedCallback(evt => SetWeight(row, evt.newValue));
            field.RegisterValueChangedCallback(evt => SetWeight(row, evt.newValue));
            reset.clicked += () =>
            {
                if (row.userData is ShapeKeyEntry entry) SetWeight(row, entry.Original);
            };

            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= visibleEntries.Count) return;

            var entry = visibleEntries[index];
            row.userData = entry;

            var name = row.Q<Label>("shape_name");
            name.text = entry.Usage == null ? entry.Name : $"{entry.Name}  [{entry.Usage}]";
            name.tooltip = entry.Name;
            name.EnableInClassList("warning", entry.Usage != null);

            var weight = currentRenderer.GetBlendShapeWeight(entry.Index);

            var slider = row.Q<Slider>("shape_slider");
            slider.highValue = entry.Max;
            slider.SetValueWithoutNotify(weight);

            row.Q<FloatField>("shape_value").SetValueWithoutNotify((float)Math.Round(weight, 2));
            row.Q<Button>("shape_reset").SetEnabled(!Mathf.Approximately(weight, entry.Original));
        }

        private void SetWeight(VisualElement row, float value)
        {
            if (row.userData is not ShapeKeyEntry entry || currentRenderer == null) return;

            var clamped = Mathf.Clamp(value, 0f, entry.Max);
            ApplyWeight(entry.Index, clamped);

            row.Q<Slider>("shape_slider").SetValueWithoutNotify(clamped);
            row.Q<FloatField>("shape_value").SetValueWithoutNotify((float)Math.Round(clamped, 2));
            row.Q<Button>("shape_reset").SetEnabled(!Mathf.Approximately(clamped, entry.Original));

            MarkDirty();
        }

        #endregion

        #region Actions

        private void ApplyWeight(int index, float weight)
        {
            currentRenderer.SetBlendShapeWeight(index, weight);

            var mirrored = Preview?.FindInPreview(currentRenderer);
            if (mirrored != null) mirrored.SetBlendShapeWeight(index, weight);
        }

        private void ResetCurrentRenderer()
        {
            if (currentRenderer == null) return;

            foreach (var entry in allEntries)
            {
                ApplyWeight(entry.Index, entry.Original);
            }

            ApplyFilter();
            MarkDirty();
        }

        private void ZeroCurrentRenderer()
        {
            if (currentRenderer == null) return;

            foreach (var entry in allEntries)
            {
                ApplyWeight(entry.Index, 0f);
            }

            ApplyFilter();
            MarkDirty();
        }

        private void FocusFace()
        {
            var head = FindHead();
            if (head == null)
            {
                var bounds = EditToolUtility.CalculateBounds(Session.Root);
                FramePreview(new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.08f, bounds.center.z), bounds.size.y * 0.08f);
                return;
            }

            FramePreview(head.position + Vector3.up * 0.06f, 0.15f);
        }

        private Transform FindHead()
        {
            var animator = Session.Root.GetComponent<Animator>();
            if (animator && animator.avatar && animator.avatar.isHuman)
            {
                var humanBone = animator.avatar.humanDescription.human
                    .FirstOrDefault(h => h.humanName == HumanBodyBones.Head.ToString());
                if (!string.IsNullOrEmpty(humanBone.boneName))
                {
                    var found = Session.Root.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == humanBone.boneName);
                    if (found != null) return found;
                }
            }

            return Session.Root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase));
        }

        protected override void OnAfterSave()
        {
            originalWeights.Clear();
            CollectRenderers();
            RefreshEntries();
        }

        #endregion

        protected override void OnClose()
        {
            originalWeights.Clear();
            currentRenderer = null;
        }
    }
}
