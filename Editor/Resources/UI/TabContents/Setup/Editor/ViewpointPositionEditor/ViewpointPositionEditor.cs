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
}