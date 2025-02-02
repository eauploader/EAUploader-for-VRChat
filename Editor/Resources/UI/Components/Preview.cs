using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.UI.ImportSettings;

namespace EAUploader.UI.Components
{
    public class Preview
    {
        // Configuration 
        private const string PREVIEW_VISUAL_TREE_ASSET_PATH = "UI/Components/Preview";

        // State  
        private float previewScale = 1.0f;
        private Vector2 previewOffset = Vector2.zero;
        private VisualElement root;
        private IMGUIContainer iMGUIContainer;
        private UnityEditor.Editor gameObjectEditor;
        private GameObject prefab;
        private VisualTreeAsset visualTree;
        private float previewWidth;
        private float previewHeight;
        private string prefabPath;

        public Preview(VisualElement rootElement, string prefabPath)
        {
            root = rootElement;
            this.prefabPath = prefabPath;
            visualTree = Resources.Load<VisualTreeAsset>(PREVIEW_VISUAL_TREE_ASSET_PATH);
        }

        // --- Public Methods ---

        public void ShowContent()
        {
            if (!TryLoadPrefab(prefabPath, out prefab))
            {
                root.Add(new Label(T7e.Get("Select a prefab to preview")));
                return;
            }
            else
            {
                CreateResetButton();
                UpdatePreview(prefabPath);
            }
        }

        private void CreateResetButton()
        {
            var button = new ShadowButton()
            {
                name = "reset_preview",
            };

            button.clicked += () =>
            {
                ResetPreview();
            };

            var icon = new MaterialIcon()
            {
                icon = "restart_alt"
            };

            button.Add(icon);

            root.Add(button);
        }

        public void UpdatePreview(string prefabPath)
        {
            ResetPreview(); // Start with reset state
            root.Clear();

            // Load and clone the preview visual tree UI
            visualTree.CloneTree(root);

            if (!TryLoadPrefab(prefabPath, out prefab))
                return; // Error handling

            ShowPrefabPreview();

            CreateResetButton();
        }

        public void ResetPreview()
        {
            previewScale = 1.0f;
            previewOffset = Vector2.zero;
            ClearPreview();
        }

        // --- Helper Methods --- 

        private bool TryLoadPrefab(string prefabPath, out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return false;
            }
            return true;
        }

        private void ShowPrefabPreview()
        {
            var previewContainer = root.Q<VisualElement>("preview_area");
            ClearPreview();

            var preview = new VisualElement()
            {
                name = "preview",
                style = { flexGrow = 1 }
            };
            previewContainer.Add(preview);

            // Subscribe to the GeometryChangeEvent with a local method
            preview.RegisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);

            void OnPreviewGeometryChanged(GeometryChangedEvent evt)
            {
                preview.UnregisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);
                previewWidth = evt.newRect.width;
                previewHeight = evt.newRect.height;
                CreateIMGUIContainer();
            }

            preview.RegisterCallback<GeometryChangedEvent>((evt) =>
            {
                previewWidth = evt.newRect.width;
                previewHeight = evt.newRect.height;
            });
        }

        Texture2D MakeBackgroundTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Color32 GetBackgroundColorForTheme()
        {
            string currentTheme = ThemeUtility.GetCurrentTheme();

            switch (currentTheme)
            {
                case "dark":
                    return new Color32(41, 41, 41, 255);

                case "pastel":
                    return new Color32(253, 248, 245, 255);

                default:
                    return new Color32(255, 255, 255, 255);
            }
        }

        private void CreateIMGUIContainer()
        {
            iMGUIContainer = new IMGUIContainer(() =>
            {
                Color32 bgColor = GetBackgroundColorForTheme();

                var bgStyle = new GUIStyle
                {
                    normal = { background = MakeBackgroundTexture(1, 1, bgColor) }
                };

                var previewRectArea = new Rect(0, 0, previewWidth, previewHeight);
                var previewRect = new Rect(previewOffset.x, previewOffset.y, previewWidth * previewScale, previewHeight * previewScale);

                GUILayout.BeginArea(previewRectArea);
                {
                    CreateOrReuseGameObjectEditor();
                    HandleMouseEvents(previewRect, previewRectArea);
                    gameObjectEditor.OnInteractivePreviewGUI(previewRect, bgStyle);
                }
                GUILayout.EndArea();
            });

            iMGUIContainer.style.width = previewWidth;
            iMGUIContainer.style.height = previewHeight;

            var preview = root.Q("preview");
            preview.Add(iMGUIContainer);
        }

        private void CreateOrReuseGameObjectEditor()
        {
            gameObjectEditor ??= UnityEditor.Editor.CreateEditor(prefab);
        }

        private void ClearPreview()
        {
            // Ensure cleanup even on errors
            iMGUIContainer = null;
            gameObjectEditor = null;
        }

        private bool isDragging = false;

        private void HandleMouseEvents(Rect previewRect, Rect previewRectArea)
        {
            Event e = Event.current;

            // 拡大縮小
            if (e.type == EventType.ScrollWheel && previewRectArea.Contains(e.mousePosition))
            {
                // スケール変更量
                float scaleDelta = -e.delta.y * 0.05f;
                float newScale = Mathf.Max(previewScale + scaleDelta, 0.1f);
                float oldScale = previewScale;

                previewScale = newScale;

                Vector2 localPreviewCenter = new Vector2(previewRect.width / 2 * (newScale / oldScale), previewRect.height / 2 * (newScale / oldScale));
                Vector2 mousePosition = e.mousePosition;
                Vector2 fixedToCenter = mousePosition - localPreviewCenter;
                Vector2 PreviewCenter = previewRect.center;
                Vector2 relativeMousePosition = mousePosition - PreviewCenter;

                previewOffset = fixedToCenter - relativeMousePosition * (newScale / oldScale);

                e.Use();
            }

            // 移動
            if (e.type == EventType.MouseDrag && e.button == 1 && (previewRectArea.Contains(e.mousePosition) || isDragging))
            {
                isDragging = true;
                previewOffset += e.delta;
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 1)
            {
                isDragging = false;
            }
        }
    }
}
