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
        /// <returns>モーダルを開けた場合はtrueを返します。削除結果はコールバック付きのオーバーロードで取得してください。</returns>
        [Obsolete("Deletion is asynchronous. Use ShowDeletePrefabDialog(prefabPath, completed) to receive the deletion result.")]
        public static bool ShowDeletePrefabDialog(string prefabPath)
        {
            return PrefabManager.ShowDeletePrefabDialog(prefabPath, null);
        }

        /// <summary>画面内の削除確認モーダルを表示します。</summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <param name="completed">削除成功時にtrue、キャンセル時にfalseを受け取ります。</param>
        /// <returns>モーダルを開けた場合はtrueを返します。</returns>
        public static bool ShowDeletePrefabDialog(string prefabPath, Action<bool> completed)
        {
            return PrefabManager.ShowDeletePrefabDialog(prefabPath, completed);
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
}
