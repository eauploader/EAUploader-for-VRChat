using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.CustomPrefabUtility;
using System;
using EAUploader.UI.Setup.EditTools;

namespace EAUploader.UI.Setup
{
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
}
