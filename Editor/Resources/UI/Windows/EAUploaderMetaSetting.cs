using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.UI.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.Components;
using System;
using System.Collections.Generic;

namespace EAUploader.UI.Windows
{
    public class AvatarSettingsWindow : EditorWindow
    {
        private string prefabPath;
        private Texture2D previewImage;

        [MenuItem("EAUploader/Avatar Settings")]
        public static AvatarSettingsWindow ShowWindow()
        {
            // ウィンドウがすでに開かれている場合は新たに開かない
            if (HasOpenInstances<AvatarSettingsWindow>())
                return GetWindow<AvatarSettingsWindow>();

            AvatarSettingsWindow wnd = GetWindow<AvatarSettingsWindow>();
            wnd.titleContent = new GUIContent("Avatar Settings");
            return wnd;
        }

        public void SetPrefabPath(string path, Texture2D preview)
        {
            prefabPath = path;
            previewImage = preview;
            UpdatePreviewImage();
            UpdateGenreDropdown();
        }

        private void UpdatePreviewImage()
        {
            var previewImageElement = rootVisualElement.Q<Image>("previewImage");
            if (previewImageElement != null)
            {
                previewImageElement.image = previewImage != null ? previewImage : null;
            }
        }

        private void UpdateGenreDropdown()
        {
            var genreDropdown = rootVisualElement.Q<DropdownField>("genreDropdown");
            if (genreDropdown != null)
            {
                // 選択肢を動的に設定
                genreDropdown.choices = new List<string> { "Avatar", "Cloth", "Accessory" };
                var genre = PrefabManager.GetPrefabGenre(prefabPath);
                if (genre != null)
                {
                    genreDropdown.value = genre.ToString();
                }
            }
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/tech.uslog.eauploader/Editor/Resources/UI/Windows/EAUploaderMetaSetting.uxml");
            if (visualTree == null)
            {
                Debug.LogError("Failed to load UXML file.");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            rootVisualElement.styleSheets.Add(EAUploader.styles);
            rootVisualElement.styleSheets.Add(EAUploader.tailwind);

            TextField renameTextField = rootVisualElement.Q<TextField>("renameTextField");
            ShadowButton renameButton = rootVisualElement.Q<ShadowButton>("renameButton");
            ShadowButton duplicateButton = rootVisualElement.Q<ShadowButton>("duplicateButton");
            ShadowButton closeButton = rootVisualElement.Q<ShadowButton>("closeButton");
            Image previewImageElement = rootVisualElement.Q<Image>("previewImage");
            DropdownField genreDropdown = rootVisualElement.Q<DropdownField>("genreDropdown");

            if (renameTextField == null || renameButton == null || duplicateButton == null || closeButton == null || previewImageElement == null || genreDropdown == null)
            {
                Debug.LogError("One or more UI elements could not be found.");
                return;
            }

            // ボタンとフィールドのイベントを設定
            renameButton.clicked += () => RenamePrefab(renameTextField.value);
            duplicateButton.clicked += DuplicatePrefab;
            closeButton.clicked += Close;
            genreDropdown.RegisterValueChangedCallback(evt => ChangePrefabGenre(evt.newValue));

            UpdatePreviewImage();
            UpdateGenreDropdown(); // ドロップダウンの初期化
        }

        private void RenamePrefab(string newName)
        {
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a new name.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.DisplayDialog("Error", "No prefab selected.", "OK");
                return;
            }

            var newPrefabPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(prefabPath), newName + ".prefab");
            AssetDatabase.RenameAsset(prefabPath, newName);
            AssetDatabase.SaveAssets();
        }

        private void DuplicatePrefab()
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.DisplayDialog("Error", "No prefab selected.", "OK");
                return;
            }

            string prefabDirectory = System.IO.Path.GetDirectoryName(prefabPath);
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            string newPrefabPath = System.IO.Path.Combine(prefabDirectory, prefabName + "_clone.prefab");

            AssetDatabase.CopyAsset(prefabPath, newPrefabPath);
            AssetDatabase.SaveAssets();
        }

        private void ChangePrefabGenre(string newGenre)
        {
            if (Enum.TryParse(newGenre, out EAUploaderMeta.PrefabGenre genre))
            {
                PrefabManager.ChangePrefabGenre(prefabPath, genre);
            }
            else
            {
                Debug.LogError("Invalid genre selected.");
            }
        }
    }
}
