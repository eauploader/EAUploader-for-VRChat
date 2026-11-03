using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EAUploader.CustomPrefabUtility
{
    public class PrefabPreview
    {
        private const string PREVIEW_SAVE_PATH = "Assets/EAUploader/PrefabPreviews";

        /// <summary>プレビュー画像の解像度(横長)。カードのサムネイル比率もこれにあわせている。</summary>
        public const int PREVIEW_WIDTH = 1200;
        public const int PREVIEW_HEIGHT = 900;

        public static Texture2D GeneratePreview(GameObject prefab)
        {
            var previewRenderUtility = new PreviewRenderUtility();
            var rect = new Rect(0, 0, PREVIEW_WIDTH, PREVIEW_HEIGHT);

            var gameObject = previewRenderUtility.InstantiatePrefabInScene(prefab);

            Bounds bounds = CalculateBounds(gameObject);
            //Debug.Log($"Calculated Bounds: {bounds}");

            previewRenderUtility.BeginStaticPreview(rect);
            previewRenderUtility.AddSingleGO(gameObject);

            previewRenderUtility.camera.backgroundColor = new UnityEngine.Color(0.9f, 0.9f, 0.9f, 1);
            previewRenderUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewRenderUtility.camera.orthographic = true;

            // orthographicSizeは縦方向の半分の大きさなので、
            // 横長のフレームに収めるには横幅をアスペクト比で割って比べる。
            float aspect = rect.width / rect.height;
            previewRenderUtility.camera.orthographicSize =
                Mathf.Max(bounds.size.y, bounds.size.x / aspect) / 2;
            previewRenderUtility.camera.nearClipPlane = 0.01f;

            Vector3 cameraPosition = bounds.center;
            cameraPosition.z = bounds.center.z + bounds.size.z * 2;
            previewRenderUtility.camera.transform.position = cameraPosition;
            previewRenderUtility.camera.transform.LookAt(bounds.center);

            //Debug.Log($"Camera Position: {previewRenderUtility.camera.transform.position}");
            //Debug.Log($"Camera LookAt: {bounds.center}");

            previewRenderUtility.Render();

            Texture2D texture = previewRenderUtility.EndStaticPreview();

            previewRenderUtility.Cleanup();

            return texture;
        }

        private static Bounds CalculateBounds(GameObject obj)
        {
            var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers.Length > 0)
            {
                return CalculateMeshBounds(obj);
            }
            else
            {
                var renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    return CalculateRendererBounds(obj);
                }
                else
                {
                    //Debug.LogWarning($"No MeshRenderers or Renderers found in GameObject: {obj.name}");
                    return new Bounds(obj.transform.position, Vector3.one);
                }
            }
        }

        private static Bounds CalculateMeshBounds(GameObject obj)
        {
            var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0)
            {
                //Debug.LogWarning($"No MeshFilters found in GameObject: {obj.name}");
                return new Bounds(obj.transform.position, Vector3.zero);
            }

            Bounds bounds = meshFilters[0].sharedMesh.bounds;
            foreach (MeshFilter meshFilter in meshFilters)
            {
                bounds.Encapsulate(meshFilter.sharedMesh.bounds);
            }

            //Debug.Log($"Calculated Mesh Bounds for GameObject: {obj.name}, Bounds: {bounds}");

            return bounds;
        }

        private static Bounds CalculateRendererBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            //Debug.Log($"Calculated Renderer Bounds for GameObject: {obj.name}, Bounds: {bounds}");

            return bounds;
        }

        public static void GenerateAndSaveAllPrefabPreviews()
        {
            var allPrefabs = PrefabManager.GetAllPrefabs();

            foreach (var prefabInfo in allPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabInfo.Path);
                if (prefab != null)
                {
                    if (!IsPrefabPreviewExist(prefabInfo.Path))
                    {
                        Texture2D preview = GeneratePreview(prefab);
                        SavePrefabPreview(prefabInfo.Path, preview);
                    }
                }
            }
        }

        public static void SavePrefabPreview(string prefabPath, Texture2D preview)
        {
            string savePath = GetPreviewImagePath(prefabPath);

            string directoryPath = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            try
            {
                byte[] pngData = preview.EncodeToPNG();
                File.WriteAllBytes(savePath, pngData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving preview for prefab: {prefabPath}. \nError: {e.Message}");
                return;
            }
        }

        internal static Texture2D LoadTextureFromFile(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }

        public static void SaveTextureToFile(Texture2D texture, string filePath)
        {
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
        }

        /// <summary>
        /// プレビュー画像の保存先。
        ///
        /// アイテムごとのフォルダに分けて管理するようになり、別フォルダに同じ名前のPrefabが
        /// 並ぶことがあるため、名前ではなくGUIDで保存先を決める。
        /// GUIDはリネームしても変わらないので、名前を変えても画像を移動する必要がない。
        /// </summary>
        public static string GetPreviewImagePath(string prefabPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (!string.IsNullOrEmpty(guid))
            {
                return Path.Combine(PREVIEW_SAVE_PATH, $"{guid}.png");
            }

            // 削除済みなどでGUIDが引けない場合は、従来の名前による保存先を返す
            string fileName = Path.GetFileNameWithoutExtension(prefabPath);
            return Path.Combine(PREVIEW_SAVE_PATH, $"{fileName}.png");
        }


        public static Texture2D GetPrefabPreview(string prefabPath)
        {
            string previewImagePath = PrefabPreview.GetPreviewImagePath(prefabPath);
            if (File.Exists(previewImagePath))
            {
                return PrefabPreview.LoadTextureFromFile(previewImagePath);
            }
            return null;
        }

        private static bool IsPrefabPreviewExist(string prefabPath)
        {
            string previewImagePath = GetPreviewImagePath(prefabPath);
            return File.Exists(previewImagePath);
        }
    }
}