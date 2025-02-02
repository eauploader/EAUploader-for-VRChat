using EAUploader.Components;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using VRC.SDK3.Avatars.Components;

namespace EAUploader.CustomPrefabUtility
{
    public static class PrefabManager
    {
        // キャッシュフィールド
        private static List<PrefabInfo> cachedPrefabs;

        // リスト更新タイム
        private static double lastUpdateTime = 0.0;

        // 再スキャンまでのインターバル
        private const double REFRESH_INTERVAL = 2.0;

        public static void Initialize()
        {
            PrefabPreview.GenerateAndSaveAllPrefabPreviews();
        }

        public static void UpdatePrefabInfo()
        {
            Initialize();
        }

        public static void ImportPrefab(string prefabPath)
        {
            GameObject prefab = GetPrefab(prefabPath);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            meta.type = GetPrefabType(prefabPath);
            meta.status = GetPrefabStatus(prefabPath);
            meta.genre = GetPrefabGenre(prefabPath);

            // メタ変更時に保存
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();

            // Previewを生成して保存
            Texture2D preview = PrefabPreview.GeneratePreview(prefab);
            PrefabPreview.SavePrefabPreview(prefabPath, preview);

            UI.ImportSettings.ManageModels.UpdateModelList();
        }

        /// <summary>
        /// リストをキャッシュし、一定時間以内であれば再利用
        /// </summary>
        internal static List<PrefabInfo> GetAllPrefabs()
        {
            double now = EditorApplication.timeSinceStartup;
            // キャッシュが未作成、あるいは一定時間経過したら再スキャン
            if (cachedPrefabs == null || (now - lastUpdateTime) > REFRESH_INTERVAL)
            {
                cachedPrefabs = ScanAllPrefabs();
                lastUpdateTime = now;
            }
            return cachedPrefabs;
        }

        /// <summary>
        /// 全Prefabスキャン処理
        /// </summary>
        private static List<PrefabInfo> ScanAllPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            var result = new List<PrefabInfo>();

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var meta = Utility.GetEAUploaderMeta(prefab);
                if (meta == null) continue;

                var oldType = meta.type;
                var newType = GetPrefabType(path);
                if (oldType != newType)
                {
                    meta.type = newType;
                    EditorUtility.SetDirty(meta);
                    AssetDatabase.SaveAssets();
                }

                var info = new PrefabInfo
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    LastModified = File.GetLastWriteTime(path),
                    Type = meta.type,
                    Status = meta.status,
                    Genre = meta.genre
                };
                result.Add(info);
            }

            // 更新日時でソート
            result = result.OrderBy(p => p.LastModified).ToList();
            return result;
        }

        private static Texture2D TryLoadPreview(string path)
        {
            string previewImagePath = PrefabPreview.GetPreviewImagePath(path);
            if (File.Exists(previewImagePath))
            {
                return PrefabPreview.LoadTextureFromFile(previewImagePath);
            }
            return null;
        }

        public static List<PrefabInfo> GetAllPrefabsWithPreview()
        {
            var allPrefabs = GetAllPrefabs()
                .Where(p => p.Status != EAUploaderMeta.PrefabStatus.Hidden)
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var info in allPrefabs)
            {
                if (info.Preview == null)
                {
                    info.Preview = TryLoadPreview(info.Path);
                }
            }

            return allPrefabs;
        }

        public static List<PrefabInfo> GetAllPrefabsIncludingHidden()
        {
            var allPrefabs = GetAllPrefabs()
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var info in allPrefabs)
            {
                if (info.Preview == null)
                {
                    info.Preview = TryLoadPreview(info.Path);
                }
            }

            return allPrefabs;
        }

        public static PrefabInfo GetPrefabInfo(string path)
        {
            var all = GetAllPrefabs();
            var info = all.Find(p => p.Path == path);
            if (info != null && info.Preview == null)
            {
                info.Preview = TryLoadPreview(path);
            }
            return info;
        }

        public static VRCAvatarDescriptor GetAvatarDescriptor(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                return prefab.GetComponent<VRCAvatarDescriptor>();
            }
            return null;
        }

        public static bool ShowDeletePrefabDialog(string prefabPath)
        {
            if (EditorUtility.DisplayDialog("Prefabの消去", "本当にPrefabを消去しますか？", "消去", "キャンセル"))
            {
                DeletePrefabPreview(prefabPath);
                AssetDatabase.DeleteAsset(prefabPath);
                EAUploaderCore.selectedPrefabPath = null;
                return true;
            }
            return false;
        }

        public static void DeletePrefabPreview(string prefabPath)
        {
            string previewImagePath = PrefabPreview.GetPreviewImagePath(prefabPath);
            if (File.Exists(previewImagePath))
            {
                File.Delete(previewImagePath);
            }
        }

        public static void PinPrefab(string prefabPath)
        {
            GameObject prefab = GetPrefab(prefabPath);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            meta.status = (meta.status == EAUploaderMeta.PrefabStatus.Pinned)
                ? EAUploaderMeta.PrefabStatus.Show
                : EAUploaderMeta.PrefabStatus.Pinned;

            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
        }

        public static bool IsPinned(string prefabPath)
        {
            var prefab = GetPrefab(prefabPath);
            if (prefab == null) return false;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return false;

            return meta.status == EAUploaderMeta.PrefabStatus.Pinned;
        }

        public static void SavePrefab(GameObject prefab, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            ImportPrefab(path);
        }

        public static void SetPrefabType(string path, EAUploaderMeta.PrefabType type)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            if (meta.type != type)
            {
                meta.type = type;
                EditorUtility.SetDirty(meta);
                AssetDatabase.SaveAssets();
            }
        }

        public static void SetPrefabGenre(string path, EAUploaderMeta.PrefabGenre genre)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            if (meta.genre != genre)
            {
                meta.genre = genre;
                EditorUtility.SetDirty(meta);
                AssetDatabase.SaveAssets();
            }
        }

        public static void ChangePrefabGenre(string path, EAUploaderMeta.PrefabGenre newGenre)
        {
            SetPrefabGenre(path, newGenre);
        }

        public static void RenamePrefab(string path, string newName)
        {
            var newPrefabPath = Path.Combine(Path.GetDirectoryName(path), newName + ".prefab").Replace("\\", "/");
            AssetDatabase.RenameAsset(path, newName);
            AssetDatabase.SaveAssets();
        }

        public static GameObject GetPrefab(string prefabPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static EAUploaderMeta.PrefabType GetPrefabType(string path)
        {
            GameObject prefab = GetPrefab(path);
            if (prefab != null)
            {
                if (prefab.GetComponent("VRC_AvatarDescriptor") != null)
                    return EAUploaderMeta.PrefabType.VRChat;
                if (prefab.GetComponent("VRMMeta") != null)
                    return EAUploaderMeta.PrefabType.VRM;
            }
            return EAUploaderMeta.PrefabType.Other;
        }

        private static EAUploaderMeta.PrefabStatus GetPrefabStatus(string path)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return EAUploaderMeta.PrefabStatus.Show;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return EAUploaderMeta.PrefabStatus.Show;

            return meta.status;
        }

        public static EAUploaderMeta.PrefabGenre GetPrefabGenre(string path)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return EAUploaderMeta.PrefabGenre.Other;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return EAUploaderMeta.PrefabGenre.Other;

            return meta.genre;
        }
    }
}
