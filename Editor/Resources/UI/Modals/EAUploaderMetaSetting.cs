using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.UI.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.Components;
using System;
using System.Collections.Generic;
using System.IO;

namespace EAUploader.UI.Modals
{
    public class AvatarSettingsModal
    {
        private string prefabPath;
        private Texture2D previewImage;
        private VisualElement modalContent = new VisualElement();
        private TextField nameTextField;
        private ShadowButton duplicateButton;
        private Image previewImageElement;
        private DropdownField genreDropdown;
        private PrefabInfo prefabInfo;

        // Create event when save button is clicked
        public event Action OnSave;

        public AvatarSettingsModal(string prefabPath, Texture2D preview)
        {
            this.prefabPath = prefabPath;
            this.previewImage = preview;
            UpdatePreviewImage();
            UpdateGenreDropdown();
        }

        private void UpdatePreviewImage()
        {
            if (previewImageElement != null)
            {
                previewImageElement.image = previewImage != null ? previewImage : null;
            }
        }

        private void UpdateGenreDropdown()
        {
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

        public void Open()
        {
            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle("Avatar Settings");

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/EAUploaderMetaSetting");
            if (visualTree == null)
            {
                Debug.LogError("Failed to load UXML file.");
                return;
            }

            visualTree.CloneTree(modalContent);

            nameTextField = modalContent.Q<TextField>("nameTextField");
            duplicateButton = modalContent.Q<ShadowButton>("duplicateButton");
            previewImageElement = modalContent.Q<Image>("previewImage");
            genreDropdown = modalContent.Q<DropdownField>("genreDropdown");

            if (nameTextField == null || duplicateButton == null || previewImageElement == null || genreDropdown == null)
            {
                Debug.LogError("One or more UI elements could not be found.");
                return;
            }

            // Set name of the prefab
            prefabInfo = PrefabManager.GetPrefabInfo(prefabPath);
            if (prefabInfo != null)
            {
                nameTextField.value = prefabInfo.Name;
            }

            // ボタンとフィールドのイベントを設定
            duplicateButton.clicked += DuplicatePrefab;

            UpdatePreviewImage();
            UpdateGenreDropdown(); // ドロップダウンの初期化

            EAUploader.modal.setContent(modalContent);

            Modal.ActionButton[] actionButtons = new Modal.ActionButton[]
            {
                new Modal.ActionButton("Close", () =>
                {
                    EAUploader.modal.Hide();
                }),
                new Modal.ActionButton("Save", () =>
                {
                    SavePrefab();
                    EAUploader.modal.Hide();
                    OnSave?.Invoke();
                }),
            };

            EAUploader.modal.setFooter(actionButtons);


            EAUploader.modal.Show();
        }

        private void SavePrefab()
        {
            if (prefabInfo.Name != nameTextField.value)
            {
                var newName = nameTextField.value;
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

                PrefabManager.RenamePrefab(prefabPath, newName);
            }
            

            // Update PrefabGenre
            if (genreDropdown.value != prefabInfo.Genre.ToString())
            {
                ChangePrefabGenre(genreDropdown.value);
            }
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
