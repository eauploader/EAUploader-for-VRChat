using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.CustomPrefabUtility;
using System;
using EAUploader.UI.Setup.EditTools;

namespace EAUploader.UI.Setup
{
    public class ViewpointPositionEditor
    {
        private const string LOCALIZATION_FOLDER_PATH = "Packages/tech.uslog.eauploader/Editor/Resources/UI/TabContents/Setup/Editor/ViewpointPositionEditor/Localization";
        private ViewpointPositionEditorUI _editorUI;
        private PreviewRenderer _previewRenderer;
        private GameObject _gameObject;

        [EAUPlugin]
        private static void Initialize()
        {
            string editorName = LanguageUtility.T7eFromJsonFile("Viewpoint Position Editor", LOCALIZATION_FOLDER_PATH);
            string description = LanguageUtility.T7eFromJsonFile("A powerful viewpoint position editor for Unity.", LOCALIZATION_FOLDER_PATH);
            string requirementDescription = LanguageUtility.T7eFromJsonFile("The selected model must have a VRCAvatarDescriptor component.", LOCALIZATION_FOLDER_PATH);

            var editorRegistration = new EditorRegistration
            {
                MenuName = "EAUploader/ViewpointPositionEditor",
                EditorName = editorName,
                Description = description,
                Version = "0.1.0",
                Author = "USLOG",
                Url = "https://uslog.tech/eauploader",
                Requirement = CheckAvatarHasVRCAvatarDescriptor,
                RequirementDescription = requirementDescription
            };

            EAUploaderEditorManager.RegisterEditor(editorRegistration);
        }

        [MenuItem("EAUploader/ViewpointPositionEditor")]
        public static void Popup()
        {
            if (!ToolModalFrame.TryGetWindowRoot(Popup, out var windowRoot)) return;

            var prefabPath = EAUploaderCore.selectedPrefabPath;
            var gameObject = PrefabManager.GetPrefab(prefabPath);
            if (gameObject == null) return;

            if (!EditToolModal.RequestCloseActive()) return;
            CloseActive();
            active = new ViewpointPositionEditor();
            active.Open(windowRoot, prefabPath, gameObject);
        }

        #region Modal

        private static ViewpointPositionEditor active;
        private ToolModalFrame _frame;

        internal static void CloseActive()
        {
            active?.Close();
        }

        private void Open(VisualElement windowRoot, string prefabPath, GameObject gameObject)
        {
            _gameObject = gameObject;
            _previewRenderer = new PreviewRenderer(_gameObject);

            _frame = new ToolModalFrame(
                LanguageUtility.T7eFromJsonFile("Viewpoint Position Editor", LOCALIZATION_FOLDER_PATH), Close);

            _editorUI = new ViewpointPositionEditorUI(prefabPath, _previewRenderer);
            _editorUI.Root.style.flexGrow = 1;
            _frame.Body.Add(_editorUI.Root);

            LanguageUtility.LocalizationFromJsonFile(_frame.Container, LOCALIZATION_FOLDER_PATH);
            _frame.Attach(windowRoot);

            AssemblyReloadEvents.beforeAssemblyReload += Close;
        }

        private void Close()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;

            _frame?.Detach();
            _frame = null;
            _previewRenderer?.Dispose();
            _previewRenderer = null;
            _editorUI?.Dispose();
            _editorUI = null;

            if (active == this) active = null;
        }

        #endregion

        private static bool CheckAvatarHasVRCAvatarDescriptor(string prefabPath)
        {
            GameObject gameObject = PrefabManager.GetPrefab(prefabPath);
            return Utility.CheckAvatarHasVRCAvatarDescriptor(gameObject);
        }
    }

    public class ViewpointPositionEditorUI : IDisposable
    {
        private readonly string _prefabPath;
        private readonly PreviewRenderer _previewRenderer;
        private VisualElement _root;
        private PreviewImage _previewImage;
        private PreviewImage _actualView;

        public ViewpointPositionEditorUI(string prefabPath, PreviewRenderer previewRenderer)
        {
            _prefabPath = prefabPath;
            _previewRenderer = previewRenderer;
            CreateUI();
        }

        public VisualElement Root => _root;

        private void CreateUI()
        {
            _root = new VisualElement();
            _root.styleSheets.Add(EAUploader.styles);
            _root.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/Setup/Editor/ViewpointPositionEditor/ViewpointPositionEditor");
            visualTree.CloneTree(_root);

            _previewImage = new PreviewImage(_previewRenderer.PreviewUtility);
            _root.Q<VisualElement>("preview_area").Add(_previewImage);

            _actualView = new PreviewImage(_previewRenderer.ActualViewUtility);
            _root.Q<VisualElement>("actual_view").Add(_actualView);

            SetupSliders();
            SetupButtons();
            SetupRadioButton();
        }

        private void SetupSliders()
        {
            var avatarDescriptor = Utility.GetAvatarDescriptor(_previewRenderer.GameObject);
            var bounds = _previewRenderer.Bounds;

            var horizontalSlider = _root.Q<Slider>("horizontal_slider");
            SetupSlider(horizontalSlider, bounds.center.z - bounds.size.z / 2, bounds.center.z + bounds.size.z / 2, avatarDescriptor.ViewPosition.z, UpdateHorizontalPosition);

            var verticalSlider = _root.Q<Slider>("vertical_slider");
            SetupSlider(verticalSlider, bounds.center.y - bounds.size.y / 2, bounds.center.y + bounds.size.y / 2, avatarDescriptor.ViewPosition.y, UpdateVerticalPosition);
        }

        private void SetupSlider(Slider slider, float minValue, float maxValue, float initialValue, EventCallback<ChangeEvent<float>> callback)
        {
            slider.lowValue = minValue;
            slider.highValue = maxValue;
            slider.value = initialValue;
            slider.RegisterValueChangedCallback(callback);
        }

        private void SetupRadioButton()
        {
            var radioSquare = _root.Q<RadioButton>("radio_square");

            radioSquare.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == true)
                {
                    _actualView.UpdateImageSize(1024, 1024);
                }
            });

            var radioPC = _root.Q<RadioButton>("radio_pc");
            radioPC.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == true)
                {
                    _actualView.UpdateImageSize(1920, 1080);
                }
            });

        }

        private void UpdateHorizontalPosition(ChangeEvent<float> evt)
        {
            _previewRenderer.UpdateViewpointPosition(new Vector3(_previewRenderer.ViewpointPosition.x, _previewRenderer.ViewpointPosition.y, evt.newValue));
            _previewImage.UpdateView();
            _actualView.UpdateView();
        }

        private void UpdateVerticalPosition(ChangeEvent<float> evt)
        {
            _previewRenderer.UpdateViewpointPosition(new Vector3(_previewRenderer.ViewpointPosition.x, evt.newValue, _previewRenderer.ViewpointPosition.z));
            _previewImage.UpdateView();
            _actualView.UpdateView();
        }


        private void SetupButtons()
        {
            _root.Q<Button>("invert_view").clicked += InvertViewButtonClicked;
            _root.Q<Button>("apply_button").clicked += ApplyButtonClicked;
        }

        private void InvertViewButtonClicked()
        {
            var horizontalSlider = _root.Q<Slider>("horizontal_slider");
            horizontalSlider.inverted = !horizontalSlider.inverted;

            Bounds bounds = _previewRenderer.Bounds;
            float size = bounds.size.magnitude;
            float distance = size / (4 * Mathf.Tan(_previewRenderer.PreviewUtility.camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            _previewRenderer.PreviewUtility.camera.transform.position = bounds.center + _previewRenderer.PreviewUtility.camera.transform.forward * distance;
            _previewRenderer.PreviewUtility.camera.transform.LookAt(bounds.center);

            _previewImage.UpdateView();
        }

        private void ApplyButtonClicked()
        {
            if (!SaveViewPosition(_previewRenderer.ViewpointPosition)) return;

            _previewRenderer.UpdatePreviousViewpointPosition(new Vector3(_previewRenderer.ViewpointPosition.x, _previewRenderer.ViewpointPosition.y, _previewRenderer.ViewpointPosition.z));
            _previewImage.UpdateView();

            EditToolUtility.OnPrefabSaved(_prefabPath);
        }

        private bool SaveViewPosition(Vector3 viewPosition)
        {
            if (string.IsNullOrEmpty(_prefabPath)) return false;

            GameObject root;
            try
            {
                root = PrefabUtility.LoadPrefabContents(_prefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to open {_prefabPath}: {e.Message}");
                return false;
            }

            try
            {
                var avatarDescriptor = Utility.GetAvatarDescriptor(root);
                if (avatarDescriptor == null) return false;

                avatarDescriptor.ViewPosition = viewPosition;

                PrefabUtility.SaveAsPrefabAsset(root, _prefabPath, out var success);
                if (!success) Debug.LogError($"Failed to save {_prefabPath}");
                return success;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public void Dispose()
        {
            _previewImage?.Dispose();
            _actualView?.Dispose();
        }
    }

    public class PreviewRenderer : IDisposable
    {
        private readonly GameObject _gameObject;
        private readonly PreviewRenderUtility _previewUtility;
        private readonly PreviewRenderUtility _actualViewUtility;
        private GameObject _viewpointSphere;
        private GameObject _previousViewpointSphere;
        private Bounds _bounds;

        public PreviewRenderer(GameObject gameObject)
        {
            _gameObject = gameObject;
            _previewUtility = CreatePreviewUtility();
            _actualViewUtility = CreateActualViewUtility();
            SetupPreviewScene();
        }

        public PreviewRenderUtility PreviewUtility => _previewUtility;
        public PreviewRenderUtility ActualViewUtility => _actualViewUtility;
        public GameObject GameObject => _gameObject;
        public Bounds Bounds => _bounds;
        public Vector3 ViewpointPosition => _viewpointSphere.transform.position;

        private PreviewRenderUtility CreatePreviewUtility()
        {
            var previewUtility = new PreviewRenderUtility();
            previewUtility.camera.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1);
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewUtility.camera.orthographic = true;
            return previewUtility;
        }

        private PreviewRenderUtility CreateActualViewUtility()
        {
            var actualViewUtility = new PreviewRenderUtility();
            actualViewUtility.camera.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1);
            actualViewUtility.camera.clearFlags = CameraClearFlags.Skybox;
            actualViewUtility.camera.nearClipPlane = 0.05f;
            actualViewUtility.camera.fieldOfView = 90;
            return actualViewUtility;
        }

        private void SetupPreviewScene()
        {
            _bounds = CalculateBounds(_gameObject);

            SetupPreviewCamera(_previewUtility.camera, _bounds);

            GameObject prefab = _previewUtility.InstantiatePrefabInScene(_gameObject);
            _previewUtility.AddSingleGO(prefab);

            SetupViewpointSphere();
            _previewUtility.AddSingleGO(_viewpointSphere);

            SetupPreviousViewpointSphere();
            _previewUtility.AddSingleGO(_previousViewpointSphere);

            SetupActualViewCamera();
            prefab = _actualViewUtility.InstantiatePrefabInScene(_gameObject);
            _actualViewUtility.AddSingleGO(prefab);
        }

        private void SetupPreviewCamera(Camera camera, Bounds bounds)
        {
            camera.orthographicSize = bounds.size.y / 2;

            float size = bounds.size.magnitude;
            float distance = size / (4 * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            camera.transform.position = bounds.center + camera.transform.right * distance;
            camera.transform.LookAt(bounds.center);
        }

        private void SetupViewpointSphere()
        {
            var avatarDescriptor = Utility.GetAvatarDescriptor(_gameObject);
            _viewpointSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _viewpointSphere.transform.position = avatarDescriptor.ViewPosition;
            _viewpointSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            var material = new Material(Resources.Load<Shader>("Shaders/AlwaysShow"));
            material.color = Color.red;
            _viewpointSphere.GetComponent<Renderer>().material = material;
        }

        private void SetupPreviousViewpointSphere()
        {
            _previousViewpointSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _previousViewpointSphere.transform.position = _viewpointSphere.transform.position;
            _previousViewpointSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            var material = new Material(Resources.Load<Shader>("Shaders/AlwaysShow"));
            material.color = Color.green;
            _previousViewpointSphere.GetComponent<Renderer>().material = material;
        }

        private void SetupActualViewCamera()
        {
            _actualViewUtility.camera.transform.position = ViewpointPosition;
            _actualViewUtility.camera.transform.LookAt(ViewpointPosition + Vector3.forward);
        }

        public void UpdateViewpointPosition(Vector3 newPosition)
        {
            _viewpointSphere.transform.position = newPosition;
            SetupActualViewCamera();
        }

        public void UpdatePreviousViewpointPosition(Vector3 newPosition)
        {
            _previousViewpointSphere.transform.position = newPosition;
        }

        private static Bounds CalculateBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(obj.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        public void Dispose()
        {
            _previewUtility?.Cleanup();
            _actualViewUtility?.Cleanup();
        }
    }

    public class PreviewImage : Image, IDisposable
    {
        private const int DefaultImageSize = 1024;
        private static Rect _imageDimension = new Rect(0, 0, DefaultImageSize, DefaultImageSize);
        private readonly PreviewRenderUtility _previewUtility;

        public PreviewImage(PreviewRenderUtility previewUtility)
        {
            style.flexGrow = 1;
            _previewUtility = previewUtility;
            _imageDimension = new Rect(0, 0, DefaultImageSize, DefaultImageSize);

            _previewUtility.BeginPreview(_imageDimension, GUIStyle.none);
            _previewUtility.camera.Render();
            image = _previewUtility.EndPreview();
        }

        public void UpdateView()
        {
            _previewUtility.BeginPreview(_imageDimension, GUIStyle.none);
            _previewUtility.camera.Render();
            image = _previewUtility.EndPreview();
        }

        public void UpdateImageSize(int width, int height)
        {
            _imageDimension = new Rect(0, 0, width, height);
            UpdateView();
        }

        public void Dispose()
        {
            _previewUtility?.Cleanup();
        }
    }
}