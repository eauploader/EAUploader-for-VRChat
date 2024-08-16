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
        private const string PREFABS_INFO_PATH = "Assets/EAUploader/PrefabManager.json";
        private static List<PrefabInfo> prefabs;

        public static void Initialize()
        {
            UpdatePrefabInfo();
            PrefabPreview.GenerateAndSaveAllPrefabPreviews();
        }

        public static void UpdatePrefabInfo()
        {
            var allPrefabs = GetAllPrefabs();

            allPrefabs = allPrefabs
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            SavePrefabsInfo(allPrefabs);

            if (prefabs == null)
            {
                prefabs = allPrefabs;
            }
        }

        public static void ImportPrefab(string prefabPath)
        {
            GameObject prefab = GetPrefab(prefabPath);
            var existingMeta = prefab.GetComponent<EAUploaderMeta>();
            if (existingMeta == null)
            {
                var meta = prefab.AddComponent<EAUploaderMeta>();
                meta.type = GetPrefabType(prefabPath);
            }

            Texture2D preview = PrefabPreview.GeneratePreview(prefab);
            PrefabPreview.SavePrefabPreview(prefabPath, preview);

            UI.ImportSettings.ManageModels.UpdateModelList();
        }

        internal static void SavePrefabsInfo(List<PrefabInfo> prefabs)
        {
            string directory = Path.GetDirectoryName(PREFABS_INFO_PATH);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var prefabList = new PrefabInfoList { Prefabs = prefabs };
            string json = JsonUtility.ToJson(prefabList, true);

            File.WriteAllText(PREFABS_INFO_PATH, json);
        }

        internal static List<PrefabInfo> LoadPrefabsInfo()
        {
            if (!File.Exists(PREFABS_INFO_PATH)) return new List<PrefabInfo>();

            string json = File.ReadAllText(PREFABS_INFO_PATH);
            PrefabInfoList prefabList = JsonUtility.FromJson<PrefabInfoList>(json);
            return prefabList.Prefabs;
        }

        internal static List<PrefabInfo> GetAllPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            return prefabGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => CreatePrefabInfo(path))
                .OrderBy(p => p.LastModified)
                .ToList();
        }

        public static List<PrefabInfo> GetAllPrefabsWithPreview()
        {
            var allPrefabs = GetAllPrefabs();
            allPrefabs = allPrefabs
                .Where(p => p.Status != EAUploaderMeta.PrefabStatus.Hidden)
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var prefab in allPrefabs)
            {
                string previewImagePath = PrefabPreview.GetPreviewImagePath(prefab.Path);
                if (File.Exists(previewImagePath))
                {
                    prefab.Preview = PrefabPreview.LoadTextureFromFile(previewImagePath);
                }
            }
            return allPrefabs;
        }

        public static List<PrefabInfo> GetAllPrefabsIncludingHidden()
        {
            var allPrefabs = GetAllPrefabs();
            allPrefabs = allPrefabs
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var prefab in allPrefabs)
            {
                string previewImagePath = PrefabPreview.GetPreviewImagePath(prefab.Path);
                if (File.Exists(previewImagePath))
                {
                    prefab.Preview = PrefabPreview.LoadTextureFromFile(previewImagePath);
                }
            }
            return allPrefabs;
        }

        private static PrefabInfo CreatePrefabInfo(string path)
        {
            return new PrefabInfo
            {
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path),
                LastModified = File.GetLastWriteTime(path),
                Type = GetPrefabType(path),
                Status = GetPrefabStatus(path)
            };
        }

        private static EAUploaderMeta.PrefabType GetPrefabType(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
            var allPrefabsInfo = LoadPrefabsInfo();
            var prefabInfo = allPrefabsInfo.FirstOrDefault(info => info.Path == path);

            return prefabInfo?.Status ?? EAUploaderMeta.PrefabStatus.Other;
        }

        public static GameObject GetPrefab(string prefabPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        public static void ChangePrefabGenre(string path, EAUploaderMeta.PrefabGenre newGenre)
        {
            var prefab = prefabs.FirstOrDefault(p => p.Path == path);
            if (prefab != null)
            {
                prefab.Genre = newGenre;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    var metaComponent = asset.GetComponent<EAUploaderMeta>();
                    if (metaComponent != null)
                    {
                        metaComponent.genre = newGenre;
                        EditorUtility.SetDirty(metaComponent);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        public static EAUploaderMeta.PrefabGenre? GetPrefabGenre(string path)
        {
            var prefab = prefabs.FirstOrDefault(p => p.Path == path);
            return prefab?.Genre;
        }

        public static PrefabInfo GetPrefabInfo(string path)
        {
            return prefabs.FirstOrDefault(p => p.Path == path);
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
            var allPrefabs = LoadPrefabsInfo();
            var prefab = allPrefabs.FirstOrDefault(p => p.Path == prefabPath);
            if (prefab != null)
            {
                prefab.Status = (prefab.Status == EAUploaderMeta.PrefabStatus.Pinned) ? EAUploaderMeta.PrefabStatus.Show : EAUploaderMeta.PrefabStatus.Pinned;
                SavePrefabsInfo(allPrefabs);
            }
        }

        public static VRCAvatarDescriptor GetAvatarDescriptor(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var avatarDescriptor = prefab.GetComponent<VRCAvatarDescriptor>();
                return avatarDescriptor;
            }
            return null;
        }

        public static bool IsPinned(string prefabPath)
        {
            var allPrefabs = LoadPrefabsInfo();
            var prefab = allPrefabs.FirstOrDefault(p => p.Path == prefabPath);
            return prefab?.Status == EAUploaderMeta.PrefabStatus.Pinned;
        }

        public static void SavePrefab(GameObject prefab, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            ImportPrefab(path);
        }

        public static void SetPrefabType(string path, EAUploaderMeta.PrefabType type)
        {
            var prefabInfo = prefabs.FirstOrDefault(p => p.Path == path);
            if (prefabInfo != null)
            {
                prefabInfo.SetType(type);
                SavePrefabsInfo(prefabs);
            }
        }

        public static void SetPrefabGenre(string path, EAUploaderMeta.PrefabGenre genre)
        {
            var prefabInfo = prefabs.FirstOrDefault(p => p.Path == path);
            if (prefabInfo != null)
            {
                prefabInfo.SetGenre(genre);
                SavePrefabsInfo(prefabs);
            }
        }
    }
}
