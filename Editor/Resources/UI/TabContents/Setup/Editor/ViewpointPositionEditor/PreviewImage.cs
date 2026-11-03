using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.CustomPrefabUtility;
using System;
using EAUploader.UI.Setup.EditTools;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Setup
{
    public class PreviewImage : Image, IDisposable
    {
        private const int DefaultImageSize = 1024;
        private Rect _imageDimension;
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
