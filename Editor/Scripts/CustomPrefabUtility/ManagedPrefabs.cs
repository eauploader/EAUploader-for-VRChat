using EAUploader.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EAUploader.CustomPrefabUtility
{
    /// <summary>
    /// EAUploaderが管理するPrefabの置き場所と作成を受け持つ。
    ///
    /// 取り込んだPrefabそのものには手を加えず、Prefab Variantを作って
    /// <see cref="ROOT"/> 以下のアイテムごとのフォルダで管理する。
    /// EAUploaderMeta(ジャンルやタグなどのメタ情報)もVariant側だけが持つ。
    ///
    ///   Assets/EAUploader/Prefabs/&lt;アイテム名&gt;/&lt;Variant名&gt;.prefab
    /// </summary>
    public static class ManagedPrefabs
    {
        public const string ROOT = "Assets/EAUploader/Prefabs";

        private const string EAUPLOADER_ROOT = "Assets/EAUploader";

        // Windowsのファイル名に使えない文字
        private static readonly char[] INVALID_NAME_CHARS =
            Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();

        /// <summary>
        /// EAUploaderの管理下にあるPrefabかどうか。
        /// </summary>
        public static bool IsManaged(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.StartsWith(ROOT + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// EAUploader自身が使うフォルダ内のアセットかどうか。
        /// 取り込み対象から外すために使う。
        /// </summary>
        public static bool IsInEAUploaderFolder(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.StartsWith(EAUPLOADER_ROOT + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static void EnsureRoot()
        {
            if (!AssetDatabase.IsValidFolder(EAUPLOADER_ROOT))
            {
                AssetDatabase.CreateFolder("Assets", "EAUploader");
            }

            if (!AssetDatabase.IsValidFolder(ROOT))
            {
                AssetDatabase.CreateFolder(EAUPLOADER_ROOT, "Prefabs");
            }
        }

        #region Creating

        /// <summary>
        /// 取り込んだPrefabのVariantを作り、管理フォルダに置く。
        /// 取り込み元のPrefabは読み取るだけで変更しない。
        /// </summary>
        /// <param name="sourcePrefabPath">取り込み元のPrefabのパス</param>
        /// <param name="configure">作成するVariantのメタ情報を設定する処理</param>
        /// <returns>作成したVariantのパス。失敗した場合はnull</returns>
        public static string CreateVariant(string sourcePrefabPath, Action<EAUploaderMeta> configure = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (source == null) return null;

            EnsureRoot();

            var itemName = SanitizeName(Path.GetFileNameWithoutExtension(sourcePrefabPath));
            var folder = CreateItemFolder(itemName);
            if (folder == null) return null;

            var variantPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{itemName}.prefab");

            // 作業用のインスタンスは編集中のシーンに置かずに済ませる。
            // 開いているシーンを変更済みにしてしまわないため。
            var workScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source, workScene);
                if (instance == null) return null;

                // メタ情報はVariantだけに持たせる。
                // 取り込み元が(旧バージョンの名残で)持っている場合はその値を上書きする形になる。
                var meta = instance.GetComponent<EAUploaderMeta>();
                if (meta == null) meta = instance.AddComponent<EAUploaderMeta>();
                configure?.Invoke(meta);

                PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out var success);
                if (!success)
                {
                    Debug.LogError($"Failed to create a prefab variant of {sourcePrefabPath}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create a prefab variant of {sourcePrefabPath}: {e.Message}");
                return null;
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(workScene);
            }

            return variantPath;
        }

        /// <summary>
        /// Variantを同じアイテムフォルダ内に複製する。
        /// 複製もまた同じ取り込み元のVariantになる。
        /// </summary>
        /// <returns>複製したVariantのパス。失敗した場合はnull</returns>
        public static string DuplicateVariant(string variantPath)
        {
            if (string.IsNullOrEmpty(variantPath)) return null;

            var folder = GetItemFolder(variantPath);
            if (folder == null) return null;

            var name = Path.GetFileNameWithoutExtension(variantPath);
            var newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.prefab");

            if (!AssetDatabase.CopyAsset(variantPath, newPath))
            {
                Debug.LogError($"Failed to duplicate {variantPath}");
                return null;
            }

            AssetDatabase.SaveAssets();
            return newPath;
        }

        /// <summary>
        /// アイテム1つ分のフォルダを作る。同名のフォルダがある場合は別のフォルダにする。
        /// </summary>
        private static string CreateItemFolder(string itemName)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"{ROOT}/{itemName}");
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);

            var guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"Failed to create a folder for {itemName}");
                return null;
            }

            return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Item";

            var sanitized = new string(name.Select(c => INVALID_NAME_CHARS.Contains(c) ? '_' : c).ToArray()).Trim();
            return sanitized.Length == 0 ? "Item" : sanitized;
        }

        #endregion

        #region Lookup

        /// <summary>
        /// Variantが置かれているアイテムフォルダ。
        /// </summary>
        public static string GetItemFolder(string variantPath)
        {
            if (!IsManaged(variantPath)) return null;

            var folder = Path.GetDirectoryName(variantPath)?.Replace("\\", "/");
            return string.IsNullOrEmpty(folder) ? null : folder;
        }

        /// <summary>
        /// Variantの取り込み元(ベース)となっているPrefabのパス。
        /// Variantでない場合はnull。
        /// </summary>
        public static string GetSourcePath(string variantPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (asset == null) return null;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
            if (source == null) return null;

            var path = AssetDatabase.GetAssetPath(source);
            return string.IsNullOrEmpty(path) || path == variantPath ? null : path;
        }

        /// <summary>
        /// 管理下にあるPrefabのパスをすべて返す。
        /// </summary>
        public static List<string> GetAllVariantPaths()
        {
            if (!AssetDatabase.IsValidFolder(ROOT)) return new List<string>();

            return AssetDatabase.FindAssets("t:Prefab", new[] { ROOT })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsManaged)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 指定した取り込み元から作られたVariantをすべて返す。
        /// </summary>
        public static List<string> FindVariantsOf(string sourcePrefabPath)
        {
            if (string.IsNullOrEmpty(sourcePrefabPath)) return new List<string>();

            return GetAllVariantPaths()
                .Where(p => string.Equals(GetSourcePath(p), sourcePrefabPath, StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>
        /// 指定した取り込み元のVariantが既にあるかどうか。
        /// </summary>
        public static bool HasVariantOf(string sourcePrefabPath)
        {
            if (string.IsNullOrEmpty(sourcePrefabPath)) return false;

            return GetAllVariantPaths()
                .Any(p => string.Equals(GetSourcePath(p), sourcePrefabPath, StringComparison.Ordinal));
        }

        #endregion

        #region Housekeeping

        /// <summary>
        /// アイテムフォルダに何も残っていない場合、フォルダごと片付ける。
        /// </summary>
        public static void DeleteItemFolderIfEmpty(string itemFolder)
        {
            if (string.IsNullOrEmpty(itemFolder) || !IsManaged(itemFolder + "/")) return;
            if (!AssetDatabase.IsValidFolder(itemFolder)) return;

            var hasContent = AssetDatabase.FindAssets(string.Empty, new[] { itemFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(p => p != itemFolder);

            if (hasContent) return;

            AssetDatabase.DeleteAsset(itemFolder);
        }

        /// <summary>
        /// アイテムフォルダの名前を変える。
        /// フォルダにそのPrefabしか入っていない場合だけ行う。
        /// </summary>
        /// <returns>変更後のPrefabのパス</returns>
        public static string RenameItemFolder(string variantPath, string newFolderName)
        {
            var folder = GetItemFolder(variantPath);
            if (folder == null) return variantPath;

            var siblings = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .ToList();
            if (siblings.Count != 1) return variantPath;

            var sanitized = SanitizeName(newFolderName);
            if (sanitized == Path.GetFileName(folder)) return variantPath;

            // 同名のフォルダが既にある場合は変更しない
            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder($"{parent}/{sanitized}")) return variantPath;

            var error = AssetDatabase.RenameAsset(folder, sanitized);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"Failed to rename the item folder: {error}");
                return variantPath;
            }

            return $"{parent}/{sanitized}/{Path.GetFileName(variantPath)}";
        }

        #endregion
    }
}
