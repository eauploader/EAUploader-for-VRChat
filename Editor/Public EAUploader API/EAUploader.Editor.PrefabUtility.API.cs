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
