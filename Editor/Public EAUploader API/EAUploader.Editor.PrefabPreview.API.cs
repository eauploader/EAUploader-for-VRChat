using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using EAUploader.CustomPrefabUtility;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;

namespace EAUploader.Editor.Prefab.API
{
    public static class PrefabPreviewAPI
    {
        /// <summary>
        /// プレハブのプレビューを生成します。
        /// </summary>
        /// <param name="prefab">プレビューを生成するプレハブのGameObject</param>
        /// <returns>生成されたプレビューのTexture2D</returns>
        public static Texture2D GeneratePreview(GameObject prefab)
        {
            return PrefabPreview.GeneratePreview(prefab);
        }

        /// <summary>
        /// 全てのプレハブのプレビューを生成して保存します。
        /// </summary>
        public static void GenerateAndSaveAllPrefabPreviews()
        {
            PrefabPreview.GenerateAndSaveAllPrefabPreviews();
        }

        /// <summary>
        /// プレハブのプレビューを保存します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <param name="preview">保存するプレビューのTexture2D</param>
        public static void SavePrefabPreview(string prefabPath, Texture2D preview)
        {
            PrefabPreview.SavePrefabPreview(prefabPath, preview);
        }

        /// <summary>
        /// ファイルからテクスチャを読み込みます。
        /// </summary>
        /// <param name="filePath">テクスチャのファイルパス</param>
        /// <returns>読み込まれたテクスチャのTexture2D</returns>
        public static Texture2D LoadTextureFromFile(string filePath)
        {
            return PrefabPreview.LoadTextureFromFile(filePath);
        }

        /// <summary>
        /// テクスチャをファイルに保存します。
        /// </summary>
        /// <param name="texture">保存するテクスチャのTexture2D</param>
        /// <param name="filePath">テクスチャの保存先ファイルパス</param>
        public static void SaveTextureToFile(Texture2D texture, string filePath)
        {
            PrefabPreview.SaveTextureToFile(texture, filePath);
        }

        /// <summary>
        /// プレハブのプレビューの画像パスを取得します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブのプレビューの画像パス</returns>
        public static string GetPreviewImagePath(string prefabPath)
        {
            return PrefabPreview.GetPreviewImagePath(prefabPath);
        }

        /// <summary>
        /// プレハブのプレビューを取得します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブのプレビューのTexture2D。プレビューが存在しない場合はnullを返します。</returns>
        public static Texture2D GetPrefabPreview(string prefabPath)
        {
            return PrefabPreview.GetPrefabPreview(prefabPath);
        }
    }
}
