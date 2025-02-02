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
    /// <summary>
    /// Prefabの操作、情報の取得に関するAPI
    /// </summary>
    public static class PrefabAPI
    {
        /// <summary>
        /// プレハブ情報を更新します。
        /// </summary>
        public static void UpdatePrefabInfo()
        {
            PrefabManager.UpdatePrefabInfo();
        }

        /// <summary>
        /// 指定したパスのプレハブをインポートします。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        public static void ImportPrefab(string prefabPath)
        {
            PrefabManager.ImportPrefab(prefabPath);
        }

        /// <summary>
        /// 指定したパスのプレハブを取得します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブのGameObject。プレハブが見つからない場合はnullを返します。</returns>
        public static GameObject GetPrefab(string prefabPath)
        {
            return PrefabManager.GetPrefab(prefabPath);
        }

        /// <summary>
        /// プレハブの削除ダイアログを表示します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブが削除された場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool ShowDeletePrefabDialog(string prefabPath)
        {
            return PrefabManager.ShowDeletePrefabDialog(prefabPath);
        }

        /// <summary>
        /// 指定したパスのプレハブのプレビューを削除します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        public static void DeletePrefabPreview(string prefabPath)
        {
            PrefabManager.DeletePrefabPreview(prefabPath);
        }

        /// <summary>
        /// 指定したパスのプレハブをピン留めします。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        public static void PinPrefab(string prefabPath)
        {
            PrefabManager.PinPrefab(prefabPath);
        }

        /// <summary>
        /// 指定したパスのプレハブのVRCAvatarDescriptorを取得します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブのVRCAvatarDescriptor。プレハブが見つからない場合はnullを返します。</returns>
        public static VRCAvatarDescriptor GetAvatarDescriptor(string prefabPath)
        {
            return PrefabManager.GetAvatarDescriptor(prefabPath);
        }

        /// <summary>
        /// 指定したパスのプレハブがピン留めされているかどうかを確認します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>プレハブがピン留めされている場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool IsPinned(string prefabPath)
        {
            return PrefabManager.IsPinned(prefabPath);
        }

        /// <summary>
        /// プレハブを保存します。
        /// </summary>
        /// <param name="prefab">保存するプレハブのGameObject</param>
        /// <param name="path">プレハブの保存先パス</param>
        public static void SavePrefab(GameObject prefab, string path)
        {
            PrefabManager.SavePrefab(prefab, path);
        }
    }

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

    public static class ShaderAPI
    {
        /// <summary>
        /// シェーダーをチェックします。
        /// </summary>
        public static void CheckShaders()
        {
            ShaderChecker.CheckShaders();
        }

        /// <summary>
        /// 既存のシェーダーを取得します。
        /// </summary>
        /// <returns>既存のシェーダーのリスト</returns>
        public static List<string> GetExistingShaders()
        {
            return ShaderChecker.GetExistingShaders();
        }

        /// <summary>
        /// 指定したプレハブのシェーダーをチェックします。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        public static void CheckShadersInPrefabs(string prefabPath)
        {
            ShaderChecker.CheckShadersInPrefabs(prefabPath);
        }

        /// <summary>
        /// アバターにシェーダーが含まれているかどうかを確認します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターにシェーダーが含まれている場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool CheckAvatarHasShader(GameObject avatar)
        {
            return ShaderChecker.CheckAvatarHasShader(avatar);
        }
    }
#if HAS_VRM
    public static class VRMAPI
    {
        /// <summary>
        /// VRM Comverter for VRChat パッケージが導入されている場合のみ
        /// VRMをインポートします。
        /// </summary>
        /// <param name="path">VRMファイルのパス</param>
        public static void ImportVRM(string path = null)
        {
            VRMImporter.ImportVRM(path);
        }
    }
#endif

    public static class PrefabUtilityAPI
    {
        /// <summary>
        /// アバターの高さを取得します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターの高さ</returns>
        public static float GetAvatarHeight(GameObject avatar)
        {
            return Utility.GetAvatarHeight(avatar);
        }

        /// <summary>
        /// アバターがVRCAvatarDescriptorを持っているかどうかを確認します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターがVRCAvatarDescriptorを持っている場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool CheckAvatarHasVRCAvatarDescriptor(GameObject avatar)
        {
            return Utility.CheckAvatarHasVRCAvatarDescriptor(avatar);
        }

        /// <summary>
        /// アバターがVRMかどうかを確認します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターがVRMの場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool CheckAvatarIsVRM(GameObject avatar)
        {
            return Utility.CheckAvatarIsVRM(avatar);
        }

        /// <summary>
        /// アバターがTraceAndOptimizeを持っているかどうかを確認します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターがTraceAndOptimizeを持っている場合はtrue、そうでない場合はfalseを返します。</returns>
        public static bool CheckAvatarHasTandO(GameObject avatar)
        {
            return Utility.CheckAvatarHasTandO(avatar);
        }

        /// <summary>
        /// アバターのVRCAvatarDescriptorを取得します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターのVRCAvatarDescriptor</returns>
        public static VRCAvatarDescriptor GetAvatarDescriptor(GameObject avatar)
        {
            var descriptor = Utility.GetAvatarDescriptor(avatar);
            return descriptor as VRCAvatarDescriptor;
        }

        /// <summary>
        /// アバターのPipelineManagerを取得します。
        /// </summary>
        /// <param name="avatar">アバターのGameObject</param>
        /// <returns>アバターのPipelineManager</returns>
        public static VRC.Core.PipelineManager GetPipelineManager(GameObject avatar)
        {
            return Utility.GetPipelineManager(avatar);
        }
    }
}
