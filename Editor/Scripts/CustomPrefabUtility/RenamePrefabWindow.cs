using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.CustomPrefabUtility
{
    /// <summary>
    /// Avatar Setting追加に伴いRenamePrefabWindowはv1.4.4よりサポートを終了します
    /// </summary>
    public class RenamePrefabWindow : EditorWindow
    {
        public string FilePath;
        private bool _isChanged;

        public bool ShowWindow(string prefabPath)
        {
            RenamePrefabWindow wnd = GetWindow<RenamePrefabWindow>();
            wnd.FilePath = prefabPath;
            wnd.titleContent = new GUIContent("Prefabの名前を変更");
            wnd.position = new Rect(100, 100, 400, 200);
            wnd.minSize = new Vector2(400, 200);
            wnd.maxSize = wnd.minSize;

            wnd.rootVisualElement.style.unityFont = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/EAUploader/UI/Noto_Sans_JP SDF.ttf");

            var visualTree = new VisualElement();
            var newPrefabName = new TextField("新しいPrefabの名前")
            {
                value = Path.GetFileNameWithoutExtension(prefabPath)
            };
            visualTree.Add(newPrefabName);

            var renameButton = new Button(() => wnd.Rename(newPrefabName.value)) { text = "名前を変更" };
            visualTree.Add(renameButton);

            wnd.rootVisualElement.Add(visualTree);
            wnd.ShowModal();

            return wnd._isChanged;
        }

        private void Rename(string newPrefabName)
        {
            if (string.IsNullOrEmpty(newPrefabName))
            {
                EditorUtility.DisplayDialog("エラー", "新しいPrefabの名前が入力されていません", "OK");
                return;
            }

            string directory = Path.GetDirectoryName(FilePath);
            string newFilePath = Path.Combine(directory, newPrefabName + Path.GetExtension(FilePath));

            if (!File.Exists(newFilePath))
            {
                AssetDatabase.MoveAsset(FilePath, newFilePath);

                string previewImagePath = PrefabPreview.GetPreviewImagePath(FilePath);
                string newPreviewImagePath = Path.Combine(Path.GetDirectoryName(previewImagePath), newPrefabName + ".png");

                if (File.Exists(previewImagePath))
                {
                    try
                    {
                        Debug.Log($"Moving preview image: {previewImagePath} -> {newPreviewImagePath}");
                        if (File.Exists(newPreviewImagePath))
                        {
                            File.Delete(newPreviewImagePath);
                        }
                        File.Move(previewImagePath, newPreviewImagePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error moving preview image: {e.Message}");
                    }
                }

                AssetDatabase.Refresh();
                _isChanged = true;
                Close();
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", "この名前のファイルは既にあります。", "OK");
            }
        }
    }
}