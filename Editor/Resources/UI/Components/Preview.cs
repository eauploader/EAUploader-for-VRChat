using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

        public Preview(VisualElement rootElement, GameObject liveTarget)
        {
            root = rootElement;
            this.liveTarget = liveTarget;
            visualTree = Resources.Load<VisualTreeAsset>(PREVIEW_VISUAL_TREE_ASSET_PATH);
        }

        // --- Public Methods ---

        public void ShowContent()
        {
            if (IsLive)
            {
                ShowLiveContent();
                return;
            }

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

            if (IsLive)
            {
                FrameLive();
                return;
            }

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
            if (IsLive)
            {
                CreateLiveIMGUIContainer();
                return;
            }

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

        #region Live target

        private const float LIVE_FIELD_OF_VIEW = 30f;
        private const float LIVE_ORBIT_SPEED = 0.4f;
        private const float LIVE_ZOOM_SPEED = 0.05f;

        private GameObject liveTarget;
        private PreviewRenderUtility liveUtility;
        private GameObject liveHolder;
        private GameObject liveInstance;
        private Vector3 livePivot;
        private float liveYaw = 180f;
        private float livePitch = 5f;
        private float liveDistance = 2f;
        private bool liveFramed;
        private int liveDragButton = -1;

        public bool IsLive => liveTarget != null;

        public void SetLiveTarget(GameObject target)
        {
            liveTarget = target;
            liveFramed = false;
            DestroyLiveInstance();
            Repaint();
        }

        public void Refresh()
        {
            DestroyLiveInstance();
            Repaint();
        }

        public void Repaint()
        {
            iMGUIContainer?.MarkDirtyRepaint();
        }

        public void Frame(Vector3 center, float radius)
        {
            livePivot = center;
            liveDistance = Mathf.Max(0.05f, radius / Mathf.Sin(LIVE_FIELD_OF_VIEW * 0.5f * Mathf.Deg2Rad));
            liveYaw = 180f;
            livePitch = 5f;
            Repaint();
        }

        public T FindInPreview<T>(T source) where T : Component
        {
            if (source == null || liveTarget == null || liveInstance == null) return null;

            var indices = new System.Collections.Generic.List<int>();
            var current = source.transform;
            while (current != null && current != liveTarget.transform)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            if (current == null) return null;

            var mirrored = liveInstance.transform;
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                if (indices[i] >= mirrored.childCount) return null;
                mirrored = mirrored.GetChild(indices[i]);
            }

            var index = System.Array.IndexOf(source.GetComponents<T>(), source);
            var targets = mirrored.GetComponents<T>();
            return index >= 0 && index < targets.Length ? targets[index] : null;
        }

        public void Dispose()
        {
            liveUtility?.Cleanup();
            liveUtility = null;
            liveHolder = null;
            liveInstance = null;
        }

        private void ShowLiveContent()
        {
            root.Clear();
            visualTree.CloneTree(root);
            ShowPrefabPreview();
            CreateResetButton();
        }

        private void CreateLiveIMGUIContainer()
        {
            iMGUIContainer = new IMGUIContainer(DrawLive) { style = { flexGrow = 1 } };
            root.Q("preview").Add(iMGUIContainer);
        }

        private void DrawLive()
        {
            var size = iMGUIContainer.contentRect.size;
            var rect = new Rect(0, 0, size.x, size.y);
            if (rect.width < 2f || rect.height < 2f) return;
            if (!EnsureLiveInstance()) return;

            HandleLiveMouseEvents(rect);
            if (Event.current.type != EventType.Repaint) return;

            var camera = liveUtility.camera;
            var rotation = Quaternion.Euler(livePitch, liveYaw, 0f);
            camera.transform.rotation = rotation;
            camera.transform.position = livePivot - rotation * Vector3.forward * liveDistance;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GetBackgroundColorForTheme();

            liveUtility.BeginPreview(rect, GUIStyle.none);
            liveUtility.Render(true);
            liveUtility.EndAndDrawPreview(rect);
        }

        private bool EnsureLiveInstance()
        {
            if (liveTarget == null) return false;

            if (liveUtility == null)
            {
                liveUtility = new PreviewRenderUtility();
                liveUtility.cameraFieldOfView = LIVE_FIELD_OF_VIEW;
                liveUtility.ambientColor = new Color(0.4f, 0.4f, 0.4f, 1f);
                liveUtility.lights[0].intensity = 1.0f;
                liveUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 140f, 0f);
                liveUtility.lights[1].intensity = 0.5f;
            }

            if (liveHolder == null)
            {
                liveHolder = EditorUtility.CreateGameObjectWithHideFlags("EAUploader Preview", HideFlags.HideAndDontSave);
                liveUtility.AddSingleGO(liveHolder);
            }

            if (liveInstance == null)
            {
                liveInstance = Object.Instantiate(liveTarget, liveHolder.transform);
                liveInstance.name = liveTarget.name;
                liveInstance.SetActive(true);

                if (!liveFramed)
                {
                    liveFramed = true;
                    FrameLive();
                }
            }

            return true;
        }

        private void DestroyLiveInstance()
        {
            if (liveInstance != null) Object.DestroyImmediate(liveInstance);
            liveInstance = null;
        }

        private void FrameLive()
        {
            if (liveInstance == null) return;

            var renderers = liveInstance.GetComponentsInChildren<Renderer>()
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer))
                .ToList();
            if (renderers.Count == 0)
            {
                Frame(liveInstance.transform.position, 1f);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Frame(bounds.center, bounds.extents.magnitude);
        }

        private void HandleLiveMouseEvents(Rect rect)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (!rect.Contains(e.mousePosition)) break;
                    GUIUtility.hotControl = id;
                    liveDragButton = e.button;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;

                    if (liveDragButton == 0)
                    {
                        liveYaw += e.delta.x * LIVE_ORBIT_SPEED;
                        livePitch = Mathf.Clamp(livePitch + e.delta.y * LIVE_ORBIT_SPEED, -89f, 89f);
                    }
                    else
                    {
                        var worldPerPixel = 2f * liveDistance * Mathf.Tan(LIVE_FIELD_OF_VIEW * 0.5f * Mathf.Deg2Rad) / rect.height;
                        var rotation = Quaternion.Euler(livePitch, liveYaw, 0f);
                        livePivot += rotation * Vector3.left * e.delta.x * worldPerPixel;
                        livePivot += rotation * Vector3.up * e.delta.y * worldPerPixel;
                    }

                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    liveDragButton = -1;
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    if (!rect.Contains(e.mousePosition)) break;
                    liveDistance = Mathf.Clamp(liveDistance * (1f + e.delta.y * LIVE_ZOOM_SPEED), 0.05f, 100f);
                    e.Use();
                    Repaint();
                    break;
            }
        }

        #endregion
    }
}
