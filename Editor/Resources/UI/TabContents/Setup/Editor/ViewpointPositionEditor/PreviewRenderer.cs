using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.CustomPrefabUtility;
using System;
using EAUploader.UI.Setup.EditTools;

namespace EAUploader.UI.Setup
{
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
}
