using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    /// <summary>
    /// アバターなどの3Dプレビュー。
    /// PreviewRenderUtility で描画し、左ドラッグで回転、右/中ドラッグで平行移動、ホイールで拡大縮小する。
    /// 背景はテーマにあわせたグラデーションで、y=0 のXZグリッドと軸は下部のツールバーで表示を切り替えられる。
    /// プレハブのアセットを指定した場合も、ツールが編集中のGameObjectを指定した場合も、同じ描画を使う。
    /// </summary>
    public class Preview
    {
        // Configuration
        private const string PREVIEW_VISUAL_TREE_ASSET_PATH = "UI/Components/Preview";
        private const string GRID_PREF_KEY = "EAUploader.Preview.ShowGrid";
        private const string AXES_PREF_KEY = "EAUploader.Preview.ShowAxes";

        private const float LIVE_FIELD_OF_VIEW = 30f;
        private const float LIVE_ORBIT_SPEED = 0.4f;
        private const float LIVE_ZOOM_SPEED = 0.05f;
        // 床のグリッドを少し上から見下ろす角度
        private const float LIVE_DEFAULT_PITCH = 12f;
        // Three.js の OrbitControls (enableDamping = true) と同様の慣性減衰速度
        private const float DAMPING_SPEED = 14f;
        // スーパーサンプリング倍率 (MeshTopology.Lines のグリッド/軸やアルファカットアウトも含め全体のジャギーを除去)
        private const float SUPERSAMPLE_SCALE = 2f;

        // State
        private readonly VisualElement root;
        private readonly VisualTreeAsset visualTree;
        private IMGUIContainer iMGUIContainer;
        private GameObject liveTarget;
        private PreviewRenderUtility liveUtility;
        private GameObject liveHolder;
        private GameObject liveInstance;
        private Vector3 livePivot;
        private float liveYaw = 180f;
        private float livePitch = LIVE_DEFAULT_PITCH;
        private float liveDistance = 2f;
        private Vector3 targetPivot;
        private float targetYaw = 180f;
        private float targetPitch = LIVE_DEFAULT_PITCH;
        private float targetDistance = 2f;
        private double lastDampTime;
        private bool dampingActive;
        private bool liveFramed;
        private int liveDragButton = -1;
        private Vector2 lastPointerPosition;
        private bool showGrid = true;
        private bool showAxes = true;
        private float gridOpacity = 1f;
        private float axesOpacity = 1f;

        // 右側を覆うUI(アップロードのシートなど)の幅。3Dはその左側の空いた部分の中央に描く
        private float rightInset;
        private float targetRightInset;
        private IVisualElementScheduledItem insetAnimation;
        private VisualElement toolbarShell;
        // 覆うUIのアニメーション(styles.uss の .studio__shell)にあわせて寄せる時間
        private const float InsetAnimationSeconds = 0.22f;

        public Preview(VisualElement rootElement, string prefabPath)
        {
            root = rootElement;
            liveTarget = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            visualTree = Resources.Load<VisualTreeAsset>(PREVIEW_VISUAL_TREE_ASSET_PATH);
        }

        public Preview(VisualElement rootElement, GameObject liveTarget)
        {
            root = rootElement;
            this.liveTarget = liveTarget;
            visualTree = Resources.Load<VisualTreeAsset>(PREVIEW_VISUAL_TREE_ASSET_PATH);
        }

        public bool IsLive => liveTarget != null;

        // --- Public Methods ---

        public void ShowContent()
        {
            if (!IsLive)
            {
                root.Add(new Label(T7e.Get("Select a prefab to preview")));
                return;
            }

            ShowLiveContent();
        }

        public void UpdatePreview(string prefabPath)
        {
            var target = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (target == null) return;

            SetLiveTarget(target);
            ShowLiveContent();
        }

        public void ResetPreview()
        {
            FrameLive();
        }

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
            Frame(center, radius, true);
        }

        private void Frame(Vector3 center, float radius, bool smooth)
        {
            var dist = Mathf.Max(0.05f, radius / Mathf.Sin(LIVE_FIELD_OF_VIEW * 0.5f * Mathf.Deg2Rad));
            targetPivot = center;
            targetDistance = dist;
            // 最短回転角で正面(180度)へ戻るようにターゲットYawを合わせる
            targetYaw = liveYaw + Mathf.DeltaAngle(liveYaw, 180f);
            targetPitch = LIVE_DEFAULT_PITCH;

            if (!smooth)
            {
                livePivot = targetPivot;
                liveDistance = targetDistance;
                liveYaw = 180f;
                targetYaw = 180f;
                livePitch = targetPitch;
                Repaint();
                return;
            }

            StartDamping();
        }

        private void StartDamping()
        {
            if (!dampingActive)
            {
                dampingActive = true;
                lastDampTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += StepDamping;
            }
            Repaint();
        }

        private void StopDamping()
        {
            if (!dampingActive) return;
            dampingActive = false;
            EditorApplication.update -= StepDamping;
        }

        private void StepDamping()
        {
            if (iMGUIContainer == null || iMGUIContainer.panel == null)
            {
                StopDamping();
                return;
            }

            Repaint();
        }

        /// <summary>
        /// 実際の描画フレーム(EventType.Repaint)の直前に呼ばれ、前回描画からの実経過時間でダンピング補間を進める。
        /// PointerMoveEvent で1フレームに多数のOSマウスイベントが来ても、補間と描画が完全に1対1で同期するためカクつかない。
        /// </summary>
        private void AdvanceDampingForCurrentFrame()
        {
            if (!dampingActive) return;

            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - lastDampTime);
            lastDampTime = now;
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);

            var factor = 1f - Mathf.Exp(-DAMPING_SPEED * dt);

            liveYaw = Mathf.Lerp(liveYaw, targetYaw, factor);
            livePitch = Mathf.Lerp(livePitch, targetPitch, factor);
            liveDistance = Mathf.Lerp(liveDistance, targetDistance, factor);
            livePivot = Vector3.Lerp(livePivot, targetPivot, factor);

            var targetGridOpacity = showGrid ? 1f : 0f;
            var targetAxesOpacity = showAxes ? 1f : 0f;
            gridOpacity = Mathf.Lerp(gridOpacity, targetGridOpacity, factor);
            axesOpacity = Mathf.Lerp(axesOpacity, targetAxesOpacity, factor);

            var settled =
                liveDragButton < 0 &&
                Mathf.Abs(targetYaw - liveYaw) < 0.01f &&
                Mathf.Abs(targetPitch - livePitch) < 0.01f &&
                Mathf.Abs(targetDistance - liveDistance) < 0.0005f &&
                (targetPivot - livePivot).sqrMagnitude < 0.000001f &&
                Mathf.Abs(targetGridOpacity - gridOpacity) < 0.005f &&
                Mathf.Abs(targetAxesOpacity - axesOpacity) < 0.005f;

            if (settled)
            {
                liveYaw = targetYaw;
                livePitch = targetPitch;
                liveDistance = targetDistance;
                livePivot = targetPivot;
                gridOpacity = targetGridOpacity;
                axesOpacity = targetAxesOpacity;
                StopDamping();
            }
        }

        public T FindInPreview<T>(T source) where T : Component
        {
            if (source == null || liveTarget == null || liveInstance == null) return null;

            var indices = new List<int>();
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

        /// <summary>
        /// 右側を覆うUIの幅を指定する。3Dとツールバーは、その左側の空いた部分の中央へ滑らかに寄る。
        /// </summary>
        public void SetRightInset(float inset)
        {
            targetRightInset = Mathf.Max(0f, inset);
            EAUploader.RequestSmoothFrames(InsetAnimationSeconds + 0.3);
            insetAnimation?.Pause();

            var start = rightInset;
            var startTime = Time.realtimeSinceStartup;
            insetAnimation = root.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / InsetAnimationSeconds);
                rightInset = Mathf.Lerp(start, targetRightInset, 1f - (1f - t) * (1f - t));
                UpdateToolbarPosition();
                Repaint();
            }).Every(16).Until(() => Mathf.Approximately(rightInset, targetRightInset));
        }

        public void Dispose()
        {
            StopDamping();
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            liveUtility?.Cleanup();
            liveUtility = null;
            liveHolder = null;
            liveInstance = null;
        }

        // --- UI ---

        private void ShowLiveContent()
        {
            showGrid = EditorPrefs.GetBool(GRID_PREF_KEY, true);
            showAxes = EditorPrefs.GetBool(AXES_PREF_KEY, true);
            gridOpacity = showGrid ? 1f : 0f;
            axesOpacity = showAxes ? 1f : 0f;

            root.Clear();
            visualTree.CloneTree(root);

            var previewContainer = root.Q<VisualElement>("preview_area");
            var preview = new VisualElement()
            {
                name = "preview",
                style = { flexGrow = 1 }
            };
            previewContainer.Add(preview);

            iMGUIContainer = new IMGUIContainer(DrawLive)
            {
                style = { flexGrow = 1 },
                cullingEnabled = true,
                pickingMode = PickingMode.Ignore
            };
            RegisterPointerCallbacks(preview);
            preview.Add(iMGUIContainer);

            // 画面から外れたとき、スクリプトの再読み込みの前に、描画用のシーンを片付ける(次に描くときに作り直す)
            iMGUIContainer.RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;

            CreateViewToolbar();
        }

        /// <summary>
        /// 3Dビューの下部中央に浮かぶツールバー。グリッドと軸の表示切り替えと、視点のリセット。
        /// </summary>
        private void CreateViewToolbar()
        {
            var shell = new Shadow { shadowBlur = 24, shadowOffsetY = 6, shadowOpacity = 0.08f };
            shell.AddToClassList("preview-toolbar-shell");

            var bar = new VisualElement();
            bar.AddToClassList("preview-toolbar");

            bar.Add(CreateToggleButton("Grid", "grid_on", GRID_PREF_KEY, on => showGrid = on));
            bar.Add(CreateToggleButton("Axes", "open_with", AXES_PREF_KEY, on => showAxes = on));

            var divider = new VisualElement();
            divider.AddToClassList("preview-toolbar__divider");
            bar.Add(divider);

            var reset = new Button(ResetPreview) { name = "preview_reset", tooltip = T7e.Get("Regenerate Preview") };
            reset.AddToClassList("preview-toolbar__button");
            reset.AddToClassList("preview-toolbar__button--icon");
            reset.Add(new MaterialIcon { icon = "restart_alt" });
            bar.Add(reset);

            shell.Add(bar);
            root.Add(shell);

            toolbarShell = shell;
            root.RegisterCallback<GeometryChangedEvent>(_ => UpdateToolbarPosition());
            UpdateToolbarPosition();
        }

        /// <summary>
        /// ツールバーを、右側を覆うUIを除いた部分の中央に置く。
        /// </summary>
        private void UpdateToolbarPosition()
        {
            if (toolbarShell == null) return;

            var width = root.layout.width;
            if (float.IsNaN(width) || width <= 0f) return;

            toolbarShell.style.left = (width - rightInset) * 0.5f;
        }

        private Button CreateToggleButton(string label, string icon, string prefKey, System.Action<bool> onChanged)
        {
            var button = new Button();
            button.AddToClassList("preview-toolbar__button");
            button.Add(new MaterialIcon { icon = icon });
            button.Add(new Label(T7e.Get(label)));

            button.EnableInClassList("preview-toolbar__button--on", EditorPrefs.GetBool(prefKey, true));
            button.clicked += () =>
            {
                var on = !EditorPrefs.GetBool(prefKey, true);
                EditorPrefs.SetBool(prefKey, on);
                onChanged?.Invoke(on);
                button.EnableInClassList("preview-toolbar__button--on", on);
                StartDamping();
            };

            return button;
        }

        // --- Drawing ---

        private void DrawLive()
        {
            if (Event.current.type != EventType.Repaint) return;

            var size = iMGUIContainer.contentRect.size;
            var rect = new Rect(0, 0, size.x, size.y);
            if (rect.width < 2f || rect.height < 2f) return;

            if (!EnsureLiveInstance()) return;

            AdvanceDampingForCurrentFrame();

            // 背景はグラデーションの画像を先に敷き、その上に背景が透明な3Dを重ねる
            GUI.DrawTexture(rect, PreviewBackdrop.GetBackground(), ScaleMode.StretchToFill, false);

            var camera = liveUtility.camera;
            var rotation = Quaternion.Euler(livePitch, liveYaw, 0f);
            camera.transform.rotation = rotation;
            camera.transform.position = livePivot - rotation * Vector3.forward * liveDistance;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowMSAA = true;

            var prevAA = QualitySettings.antiAliasing;
            if (prevAA < 8)
            {
                QualitySettings.antiAliasing = 8;
            }
            try
            {
                // BeginPreview の内部では EditorGUIUtility.pixelsPerPoint を掛けたサイズで RenderTexture が作られるため、
                // 2倍サイズの Rect を渡してスーパーサンプリング(SSAA)し、Bilinear フィルタで rect に縮小描画する。
                // 右側を覆うUIがあっても描画サイズは常に rect 全体にする。サイズを変えると、移動のアニメーション中、
                // 毎フレーム巨大な RenderTexture を作り直すことになり、カクつく。
                var renderRect = new Rect(0f, 0f, rect.width * SUPERSAMPLE_SCALE, rect.height * SUPERSAMPLE_SCALE);
                liveUtility.BeginPreview(renderRect, GUIStyle.none);

                // アバターは、右側を覆うUIを除いた部分の中央に見せたいので、画面を作り直さずにレンズをずらす(レンズシフト)
                // projectionMatrix を一度でも代入すると以後は代入した行列のままになるので、毎回 FOV と縦横比から作り直してからずらす
                camera.ResetProjectionMatrix();
                if (rightInset > 0.01f)
                {
                    var projection = camera.projectionMatrix;
                    projection.m02 += rightInset / rect.width;
                    camera.projectionMatrix = projection;
                }

                if (gridOpacity > 0.001f)
                {
                    liveUtility.DrawMesh(
                        PreviewBackdrop.GetGridMesh(),
                        Matrix4x4.identity,
                        PreviewBackdrop.GetGridMaterial(gridOpacity),
                        0);
                }
                if (axesOpacity > 0.001f)
                {
                    liveUtility.DrawMesh(
                        PreviewBackdrop.GetAxesMesh(),
                        Matrix4x4.identity,
                        PreviewBackdrop.GetAxesMaterial(axesOpacity),
                        0);
                }

                liveUtility.Render(true);

                var texture = liveUtility.EndPreview();
                if (texture != null)
                {
                    texture.filterMode = FilterMode.Bilinear;
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
                }
            }
            finally
            {
                QualitySettings.antiAliasing = prevAA;
            }
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
                    FrameLive(false);
                }
            }

            return true;
        }

        private void DestroyLiveInstance()
        {
            if (liveInstance != null) Object.DestroyImmediate(liveInstance);
            liveInstance = null;
        }

        private void FrameLive(bool smooth = true)
        {
            if (liveInstance == null) return;

            var renderers = liveInstance.GetComponentsInChildren<Renderer>()
                .Where(r => r is not ParticleSystemRenderer && r is not TrailRenderer && r is not LineRenderer)
                .ToList();
            if (renderers.Count == 0)
            {
                Frame(liveInstance.transform.position, 1f, smooth);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Frame(bounds.center, bounds.extents.magnitude, smooth);
        }

        private void RegisterPointerCallbacks(VisualElement element)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                liveDragButton = evt.button;
                lastPointerPosition = evt.localPosition;
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (liveDragButton < 0 || !element.HasPointerCapture(evt.pointerId)) return;

                Vector2 pos = evt.localPosition;
                var delta = pos - lastPointerPosition;
                lastPointerPosition = pos;
                if (delta.sqrMagnitude < 0.0001f) return;

                if (liveDragButton == 0)
                {
                    targetYaw += delta.x * LIVE_ORBIT_SPEED;
                    targetPitch = Mathf.Clamp(targetPitch + delta.y * LIVE_ORBIT_SPEED, -89f, 89f);
                }
                else
                {
                    var height = Mathf.Max(2f, element.contentRect.height);
                    var worldPerPixel = 2f * targetDistance * Mathf.Tan(LIVE_FIELD_OF_VIEW * 0.5f * Mathf.Deg2Rad) / height;
                    var rotation = Quaternion.Euler(livePitch, liveYaw, 0f);
                    targetPivot += rotation * Vector3.left * delta.x * worldPerPixel;
                    targetPivot += rotation * Vector3.up * delta.y * worldPerPixel;
                }

                StartDamping();
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (element.HasPointerCapture(evt.pointerId))
                {
                    element.ReleasePointer(evt.pointerId);
                }
                liveDragButton = -1;
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                liveDragButton = -1;
            });

            element.RegisterCallback<WheelEvent>(evt =>
            {
                targetDistance = Mathf.Clamp(targetDistance * (1f + evt.delta.y * LIVE_ZOOM_SPEED), 0.05f, 100f);
                StartDamping();
                evt.StopPropagation();
            });
        }
    }

    /// <summary>
    /// 3Dビューの背景(グラデーション)と、y=0 のXZグリッド・軸の線を作る。どのプレビューでも共有する。
    /// 画像とメッシュは、テーマが変わったときだけ作り直す。
    /// </summary>
    internal static class PreviewBackdrop
    {
        // グリッドは原点から±GridRange メートル。遠いほど薄くなる
        private const float GridRange = 6f;
        private const float GridStep = 0.1f;
        private const int GridMajorEvery = 10;
        // 線を分けて頂点ごとに色を付けることで、遠くへ向かって薄くなるようにする
        private const float SegmentLength = 0.5f;
        private const int BackgroundSize = 64;

        private static Texture2D background;
        private static Mesh gridMesh;
        private static Mesh axesMesh;
        private static Material gridMaterial;
        private static Material axesMaterial;
        private static string builtTheme;

        public static Texture2D GetBackground()
        {
            Refresh();
            return background;
        }

        public static Mesh GetGridMesh()
        {
            Refresh();
            return gridMesh;
        }

        public static Mesh GetAxesMesh()
        {
            Refresh();
            return axesMesh;
        }

        public static Material GetGridMaterial(float opacity)
        {
            if (gridMaterial == null)
            {
                gridMaterial = CreateLineMaterial();
            }
            gridMaterial.SetColor("_Color", new Color(1f, 1f, 1f, opacity));
            return gridMaterial;
        }

        public static Material GetAxesMaterial(float opacity)
        {
            if (axesMaterial == null)
            {
                axesMaterial = CreateLineMaterial();
            }
            axesMaterial.SetColor("_Color", new Color(1f, 1f, 1f, opacity));
            return axesMaterial;
        }

        private static Material CreateLineMaterial()
        {
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            // アバターより後(半透明の順)に描いて、足元でアバターに隠れるようにする
            mat.renderQueue = 3000;
            return mat;
        }

        private static void Refresh()
        {
            var theme = ThemeUtility.GetCurrentTheme();
            if (background != null && gridMesh != null && axesMesh != null && builtTheme == theme) return;

            Destroy(background);
            Destroy(gridMesh);
            Destroy(axesMesh);

            builtTheme = theme;
            background = BuildBackground(theme);
            BuildLines(theme == "dark", out gridMesh, out axesMesh);
        }

        private static void Destroy(Object obj)
        {
            if (obj != null) Object.DestroyImmediate(obj);
        }

        private static void GetBackgroundColors(string theme, out Color center, out Color edge)
        {
            switch (theme)
            {
                case "dark":
                    center = new Color32(74, 80, 92, 255);
                    edge = new Color32(24, 26, 32, 255);
                    break;

                case "pastel":
                    center = new Color32(255, 248, 243, 255);
                    edge = new Color32(222, 196, 190, 255);
                    break;

                default:
                    center = new Color32(244, 246, 250, 255);
                    edge = new Color32(143, 153, 173, 255);
                    break;
            }
        }

        /// <summary>
        /// 中心がやや上にある放射状のグラデーション。周辺と足元側を暗くして奥行きを出す。
        /// </summary>
        private static Texture2D BuildBackground(string theme)
        {
            GetBackgroundColors(theme, out var center, out var edge);

            var texture = new Texture2D(BackgroundSize, BackgroundSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[BackgroundSize * BackgroundSize];
            for (var y = 0; y < BackgroundSize; y++)
            {
                // テクスチャは下が0。v=1が上端
                var v = y / (float)(BackgroundSize - 1);
                for (var x = 0; x < BackgroundSize; x++)
                {
                    var u = x / (float)(BackgroundSize - 1);

                    var dx = (u - 0.5f) / 0.9f;
                    var dy = (v - 0.62f) / 0.8f;
                    var radial = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy)));
                    var color = Color.Lerp(center, edge, radial);

                    var floor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0f, v)) * 0.14f;
                    pixels[y * BackgroundSize + x] = Color.Lerp(color, Color.black, floor);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void BuildLines(bool darkBackground, out Mesh grid, out Mesh axes)
        {
            var lineColor = darkBackground ? new Color(1f, 1f, 1f) : new Color(0.2f, 0.25f, 0.33f);

            var gridVertices = new List<Vector3>();
            var gridColors = new List<Color>();
            var lines = Mathf.RoundToInt(GridRange / GridStep);

            for (var i = -lines; i <= lines; i++)
            {
                // 軸になる i = 0 の線は、別のメッシュで色を付けて描く
                if (i == 0) continue;

                var alpha = i % GridMajorEvery == 0 ? 0.34f : 0.14f;
                var offset = i * GridStep;
                AddFadingLine(gridVertices, gridColors, new Vector3(offset, 0f, 0f), Vector3.forward, lineColor, alpha);
                AddFadingLine(gridVertices, gridColors, new Vector3(0f, 0f, offset), Vector3.right, lineColor, alpha);
            }

            grid = CreateLineMesh("EAUploader Preview Grid", gridVertices, gridColors);

            var axisVertices = new List<Vector3>();
            var axisColors = new List<Color>();
            // X軸は赤、Z軸は青
            AddFadingLine(axisVertices, axisColors, Vector3.zero, Vector3.right, new Color(0.94f, 0.27f, 0.27f), 0.85f);
            AddFadingLine(axisVertices, axisColors, Vector3.zero, Vector3.forward, new Color(0.23f, 0.51f, 0.96f), 0.85f);

            axes = CreateLineMesh("EAUploader Preview Axes", axisVertices, axisColors);
        }

        /// <summary>
        /// 原点を通る線を、短い線分に分けて追加する。各頂点の不透明度は原点からの距離で決める。
        /// </summary>
        private static void AddFadingLine(
            List<Vector3> vertices, List<Color> colors, Vector3 through, Vector3 direction, Color color, float alpha)
        {
            var segments = Mathf.RoundToInt(GridRange * 2f / SegmentLength);
            for (var s = 0; s < segments; s++)
            {
                var from = -GridRange + s * SegmentLength;
                var to = from + SegmentLength;

                var a = through + direction * from;
                var b = through + direction * to;

                vertices.Add(a);
                vertices.Add(b);
                colors.Add(WithAlpha(color, alpha * Fade(a)));
                colors.Add(WithAlpha(color, alpha * Fade(b)));
            }
        }

        private static float Fade(Vector3 position)
        {
            var distance = new Vector2(position.x, position.z).magnitude;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GridRange * 0.25f, GridRange, distance));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Mesh CreateLineMesh(string name, List<Vector3> vertices, List<Color> colors)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(Enumerable.Range(0, vertices.Count).ToArray(), MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(GridRange * 2f, 1f, GridRange * 2f));
            return mesh;
        }
    }
}
