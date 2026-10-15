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
}
