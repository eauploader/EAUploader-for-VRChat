using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Modals
{
    /// <summary>
    /// アセットの取り込み(インポート)モーダル。
    /// 旧 Import/Settings タブの「Import」ペインをモーダル化したもの。
    /// </summary>
    public class ImportModal
    {
        private VisualElement modalContent;

        public void Open()
        {
            if (EAUploader.modal == null) return;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Import"));

            modalContent = new VisualElement();
            modalContent.styleSheets.Add(EAUploader.styles);
            LanguageUtility.ApplyLanguageFont(modalContent);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/ImportModal");
            visualTree.CloneTree(modalContent);

            BindEvents();

            LanguageUtility.Localization(modalContent);

            EAUploader.modal.setContent(modalContent);
            EAUploader.modal.setFooter(new[]
            {
                new Modal.ActionButton(T7e.Get("Close"), () => EAUploader.modal.Hide())
            });
            EAUploader.modal.Show();
        }

        private void BindEvents()
        {
            modalContent.Q<ShadowButton>("import_prefab").clicked += ImportPrefabButtonClicked;
            modalContent.Q<ShadowButton>("import_folder").clicked += ImportFolderButtonClicked;
            modalContent.Q<ShadowButton>("import_shaders").clicked += ImportShaderButtonClicked;
            modalContent.Q<ShadowButton>("show_existing_shaders").clicked += ShowExistingShaders;

            // VRM関連パッケージが導入されていない場合は取り込めない
            var importVrm = modalContent.Q<ShadowButton>("import_vrm");
            importVrm.SetEnabled(EAUploaderCore.HasVRM);
            importVrm.clicked += ImportVRMButtonClicked;
        }

        private static void ImportPrefabButtonClicked()
        {
            ImportAsset(EditorUtility.OpenFilePanelWithFilters(
                T7e.Get("Import Avatar"), "",
                new[] { T7e.Get("Import a .prefab file or .unitypackage file."), "prefab,unitypackage", "All files", "*" }));
        }

        private static void ImportShaderButtonClicked()
        {
            ImportAsset(EditorUtility.OpenFilePanelWithFilters(
                T7e.Get("Import Shaders"), "",
                new[] { T7e.Get("Import a .unitypackage file."), "unitypackage", "All files", "*" }));
        }

        private static void ShowExistingShaders()
        {
            var shaderList = ShaderChecker.GetExistingShaders();
            var shaderListString = string.Join("\n", shaderList);
            EditorUtility.DisplayDialog(T7e.Get("Existing Shaders"), shaderListString, "OK");
        }

        private static void ImportFolderButtonClicked()
        {
            ImportAllAssetsFromFolder(EditorUtility.OpenFolderPanel(T7e.Get("Import from folder"), "", ""));
        }

        private static void ImportVRMButtonClicked()
        {
#if HAS_VRM
            VRMImporter.ImportVRM();
#endif
        }

        internal static bool ImportAsset(string filePath, bool showInvalidPrefabMessage = true)
        {
            if (string.IsNullOrEmpty(filePath)) return true;

            var fileExtension = Path.GetExtension(filePath)?.ToLowerInvariant();

            if (fileExtension == ".prefab")
            {
                var assetPath = GetProjectPrefabPath(filePath);
                if (assetPath == null || (ManagedPrefabs.IsInEAUploaderFolder(assetPath) && !ManagedPrefabs.IsManaged(assetPath)))
                {
                    if (showInvalidPrefabMessage) ShowInvalidPrefabMessage();
                    return false;
                }
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            else if (fileExtension == ".unitypackage")
            {
                AssetDatabase.ImportPackage(filePath, false);
            }
            AssetDatabase.Refresh();
            return true;
        }

        internal static string GetProjectPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var assetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var projectPath = Path.GetDirectoryName(assetsPath);
                var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectPath, path));
                var comparison = Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var prefix = assetsPath + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(prefix, comparison)) return null;
                return "Assets/" + fullPath.Substring(prefix.Length).Replace('\\', '/');
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        internal static void ShowInvalidPrefabMessage()
        {
            EditorUtility.DisplayDialog(T7e.Get("Cannot import prefab"),
                T7e.Get("Select a prefab in this project's Assets folder, outside EAUploader's internal folders. To import assets from another project, use a unitypackage."), "OK");
        }

        private static void ImportAllAssetsFromFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)).ToList();

            if (allFiles.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    T7e.Get("Asset not found"),
                    T7e.Get("There are no prefab or unitypackage files in the selected folder."), "OK");
                return;
            }

            var fileList = string.Join("\n", allFiles.Select(Path.GetFileName));
            var confirmImport = EditorUtility.DisplayDialog(
                T7e.Get("Confirm Import"),
                $"{T7e.Get("The following files will be imported")}:\n{fileList}",
                T7e.Get("Import"), T7e.Get("Cancel"));
            if (!confirmImport) return;

            bool invalidPrefab = false;
            foreach (var file in allFiles)
            {
                if (!ImportAsset(file, false)) invalidPrefab = true;
            }
            if (invalidPrefab) ShowInvalidPrefabMessage();
        }
    }
}
