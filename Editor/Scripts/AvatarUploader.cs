using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;

namespace EAUploader
{
    /// <summary>
    /// アップロード対象のプラットフォーム。
    /// Questやモバイル向けのアバターもWindowsビルドが前提となるため、Windowsは常に対象になる。
    /// </summary>
    [Flags]
    public enum UploadPlatforms
    {
        Windows = 1 << 0,
        Android = 1 << 1,
        IOS = 1 << 2,
    }

    public class AvatarUploader
    {
        public static event EventHandler OnComplete;

        public static event EventHandler<ProgressEventArgs> ProgressChanged;

        /// <summary>
        /// アップロードの開始/終了で発火する。UIの有効・無効切り替えに使う。
        /// </summary>
        public static event EventHandler<bool> UploadingStateChanged;

        public class ProgressEventArgs : EventArgs
        {
            public string Status { get; }
            public float Percentage { get; }

            public ProgressEventArgs(string status, float percentage)
            {
                Status = status;
                Percentage = percentage;
            }
        }

        // ビルド用に一時生成するシーンインスタンスの名前に付ける印
        internal const string TEMP_INSTANCE_SUFFIX = " (EAUploader Upload)";

        // VRCSDKのマルチプラットフォームビルドが進行中かどうかを保持するSessionStateキー。
        // VRCMultiPlatformBuild自体はinternalのため、キーを直接参照する。
        private const string VRC_MPB_SESSION_KEY = "VRCSdkControlPanel.MultiPlatformBuild";

        private static CancellationTokenSource uploadCts;

        /// <summary>
        /// アップロード処理が進行中かどうか。
        /// </summary>
        public static bool IsUploading => uploadCts != null;

        /// <summary>
        /// VRCSDKのマルチプラットフォームビルド(プラットフォームを切り替えながらの連続ビルド)が進行中かどうか。
        /// </summary>
        internal static bool IsMultiPlatformBuildInProgress => SessionState.GetBool(VRC_MPB_SESSION_KEY, false);

        /// <summary>
        /// 進行中のアップロードをキャンセルする。
        /// </summary>
        public static void CancelUpload()
        {
            if (uploadCts == null) return;

            try
            {
                uploadCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 既に完了している
            }

            // SDK側の転送も止める
            if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                try
                {
                    builder.CancelUpload();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Failed to cancel SDK upload: " + e.Message);
                }
            }
        }

        private static void BeginUpload(out CancellationToken token)
        {
            uploadCts = new CancellationTokenSource();
            token = uploadCts.Token;
            UploadingStateChanged?.Invoke(null, true);
        }

        private static void EndUpload()
        {
            uploadCts?.Dispose();
            uploadCts = null;
            UploadingStateChanged?.Invoke(null, false);
        }

        #region Build target instance

        /// <summary>
        /// Prefabアセットをシーンへ実体化してビルド対象を作る。
        ///
        /// Modular AvatarやAvatarOptimizer(NDMF)はビルド時にシーン上のアバターに対して
        /// 非破壊処理を行うため、Prefabアセットを直接ビルドすると正しく処理されない。
        /// またPrefabアセットを直接渡すとビルド中の変更がユーザーのPrefabへ書き戻ってしまう。
        /// </summary>
        private static GameObject CreateBuildInstance(string prefabPath)
        {
            var prefabAsset = PrefabManager.GetPrefab(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"Failed to load prefab: {prefabPath}");
                return null;
            }

            // 新規アバターの場合はここでBlueprint IDを確定させ、Prefabアセット側にも保存しておく。
            // プラットフォーム切り替えでドメインリロードが挟まっても、同じアバターへ紐づけ続けるため。
            EnsureBlueprintId(prefabAsset, prefabPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            if (instance == null)
            {
                Debug.LogError($"Failed to instantiate prefab: {prefabPath}");
                return null;
            }

            // Prefabとの接続を切り、ビルド中の破壊的変更がPrefabアセットへ波及しないようにする
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.name = Path.GetFileNameWithoutExtension(prefabPath) + TEMP_INSTANCE_SUFFIX;
            instance.SetActive(true);

            return instance;
        }

        private static void EnsureBlueprintId(GameObject prefabAsset, string prefabPath)
        {
            var pipelineManager = prefabAsset.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(pipelineManager.blueprintId))
            {
                return;
            }

            pipelineManager.AssignId();
            EditorUtility.SetDirty(pipelineManager);
            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned new blueprint id to {prefabPath}: {pipelineManager.blueprintId}");
        }

        private static void DestroyBuildInstance(GameObject instance)
        {
            if (instance == null) return;
            UnityEngine.Object.DestroyImmediate(instance);
        }

        /// <summary>
        /// マルチプラットフォームビルド中のドメインリロードなどで消し損ねた
        /// 一時インスタンスをシーンから取り除く。
        /// </summary>
        internal static void CleanupLeftoverBuildInstances()
        {
            // マルチプラットフォームビルドの最中は、SDKがこのインスタンスをビルド対象として参照しているので消さない
            if (IsMultiPlatformBuildInProgress) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root.name.EndsWith(TEMP_INSTANCE_SUFFIX, StringComparison.Ordinal))
                {
                    Debug.Log("Removing leftover EAUploader upload instance: " + root.name);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        #endregion

        #region Build & Test

        public static async Task BuildAndTestAsync(string prefabPath)
        {
            if (IsUploading)
            {
                DialogPro.Show(DialogPro.DialogType.Warning, T7e.Get("Build as Test"),
                    T7e.Get("Another build is already in progress."));
                return;
            }

            VRCSdkControlPanel.GetWindow<VRCSdkControlPanel>().Show();

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) return;

            GameObject instance = null;
            BeginUpload(out var token);

            try
            {
                instance = CreateBuildInstance(prefabPath);
                if (instance == null) return;

                token.ThrowIfCancellationRequested();

                await builder.BuildAndTest(instance);

                DialogPro.Show(DialogPro.DialogType.Success, T7e.Get("Build Succeed"),
                    T7e.Get("Avatar built and tested successfully"));
            }
            catch (OperationCanceledException)
            {
                DialogPro.Show(DialogPro.DialogType.Info, T7e.Get("Build as Test"), T7e.Get("The build was cancelled."));
            }
            catch (Exception e)
            {
                DialogPro.Show(DialogPro.DialogType.Error, T7e.Get("Build Failed"), T7e.Get("Failed to build avatar"));
                Debug.Log(e.Message);
            }
            finally
            {
                DestroyBuildInstance(instance);
                EndUpload();
            }
        }

        #endregion

        #region VRChat API

        public static async Task<VRCAvatar?> GetVRCAvatar(string selectedPrefabPath)
        {
            var selectedPrefab = PrefabManager.GetPrefab(selectedPrefabPath);
            if (selectedPrefab == null)
            {
                Debug.LogError($"Failed to load prefab: {selectedPrefabPath}");
                return null;
            }

            var pipelineManager = selectedPrefab.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                try
                {
                    // VRChat Loggined.
                    if (VRC.Core.APIUser.IsLoggedIn)
                    {
                        return await VRCApi.GetAvatar(pipelineManager.blueprintId, true);
                    }
                    // VRChat Not Loggin.
                    Debug.Log("VRchat Not login.return null.");
                    return null;

                }
                catch (ApiErrorException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                catch (ApiErrorException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Upload

        public static Task UploadAvatarAsync(string prefabPath, string contentName, string contentDescription,
            string releaseStatus, List<string> tags, string thumbnailPath)
        {
            return UploadAvatarAsync(prefabPath, contentName, contentDescription, releaseStatus, tags, thumbnailPath,
                UploadPlatforms.Windows);
        }

        /// <summary>
        /// アバターをビルドしてVRChatへアップロードする。
        ///
        /// Questやモバイル向けのアバターもWindowsビルドが必要なため、Windowsは常に対象に含める。
        /// 追加プラットフォームが指定された場合はVRCSDKのマルチプラットフォームビルドを使い、
        /// Windows → 追加プラットフォーム の順にビルド/アップロードしたあと元のプラットフォームへ戻す。
        /// </summary>
        public static async Task UploadAvatarAsync(string prefabPath, string contentName, string contentDescription,
            string releaseStatus, List<string> tags, string thumbnailPath, UploadPlatforms platforms)
        {
            if (IsUploading)
            {
                DialogPro.Show(DialogPro.DialogType.Warning, T7e.Get("Upload"),
                    T7e.Get("Another build is already in progress."));
                return;
            }

            VRCSdkControlPanel.GetWindow<VRCSdkControlPanel>().Show();

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                DialogPro.Show(DialogPro.DialogType.Error, T7e.Get("Upload Failed"),
                    T7e.Get("Could not get the VRChat SDK avatar builder."));
                return;
            }

            var buildTargets = ResolveBuildTargets(platforms, out var unavailable);
            if (unavailable.Count > 0)
            {
                DialogPro.Show(DialogPro.DialogType.Warning, T7e.Get("Upload"),
                    T7e.Get("The following platforms are skipped because their build support is not installed:") +
                    " " + string.Join(", ", unavailable));
            }

            GameObject instance = null;
            BeginUpload(out var token);

            EventHandler<(string status, float percentage)> onUploadProgress = (_, e) =>
                ReportProgress(e.status, e.percentage);
            EventHandler<string> onBuildProgress = (_, status) => ReportProgress(status, 0f);

            builder.OnSdkUploadProgress += onUploadProgress;
            builder.OnSdkBuildProgress += onBuildProgress;

            try
            {
                instance = CreateBuildInstance(prefabPath);
                if (instance == null) return;

                token.ThrowIfCancellationRequested();

                var avatarData = await GetVRCAvatar(prefabPath);
                VRCAvatar avatar;

                if (avatarData == null)
                {
                    Debug.Log("Creating new avatar..");
                    avatar = new VRCAvatar
                    {
                        Name = contentName,
                        Description = contentDescription,
                        ReleaseStatus = releaseStatus,
                        Tags = tags,
                    };
                }
                else
                {
                    avatar = (VRCAvatar)avatarData;
                    avatar.Name = contentName;
                    avatar.Description = contentDescription;
                    avatar.ReleaseStatus = releaseStatus;
                    avatar.Tags = tags;
                }

                token.ThrowIfCancellationRequested();

                if (buildTargets.Count > 1 && TrySetBuilderPlatforms(builder, buildTargets))
                {
                    // Windows → 追加プラットフォームの順にビルドし、最後に元のプラットフォームへ戻る。
                    // プラットフォーム切り替えを挟むため、ここでドメインリロードが発生し
                    // このawaitが戻らないままエディタが再読み込みされる場合がある。
                    await builder.BuildAndUploadMultiPlatform(instance, avatar, thumbnailPath, token);
                }
                else
                {
                    await builder.BuildAndUpload(instance, avatar, thumbnailPath, token);
                }

                SyncBlueprintIdBackToPrefab(instance, prefabPath);

                DialogPro.Show(DialogPro.DialogType.Success, T7e.Get("Upload Succeed"),
                    T7e.Get("Avatar updated successfully"));
            }
            catch (OperationCanceledException)
            {
                DialogPro.Show(DialogPro.DialogType.Info, T7e.Get("Upload"), T7e.Get("The upload was cancelled."));
            }
            catch (Exception e)
            {
                DialogPro.Show(DialogPro.DialogType.Error, T7e.Get("Upload Failed"), e.Message);
                Debug.LogError(e);
            }
            finally
            {
                builder.OnSdkUploadProgress -= onUploadProgress;
                builder.OnSdkBuildProgress -= onBuildProgress;

                // マルチプラットフォームビルド中はSDKがこのインスタンスを使い続けるため残す。
                // 残ったインスタンスは次回のEAUploader初期化時に掃除する。
                if (!IsMultiPlatformBuildInProgress)
                {
                    DestroyBuildInstance(instance);
                }

                EndUpload();
                OnComplete?.Invoke(null, EventArgs.Empty);
            }
        }

        private static void ReportProgress(string status, float percentage)
        {
            try
            {
                ProgressChanged?.Invoke(null, new ProgressEventArgs(status, percentage));
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        /// <summary>
        /// アップロード後、SDKが払い出したBlueprint IDをPrefabアセットへ書き戻す。
        /// </summary>
        private static void SyncBlueprintIdBackToPrefab(GameObject instance, string prefabPath)
        {
            if (instance == null) return;

            var instancePipeline = instance.GetComponent<PipelineManager>();
            if (instancePipeline == null || string.IsNullOrWhiteSpace(instancePipeline.blueprintId)) return;

            var prefabAsset = PrefabManager.GetPrefab(prefabPath);
            if (prefabAsset == null) return;

            var assetPipeline = prefabAsset.GetComponent<PipelineManager>();
            if (assetPipeline == null || assetPipeline.blueprintId == instancePipeline.blueprintId) return;

            assetPipeline.blueprintId = instancePipeline.blueprintId;
            EditorUtility.SetDirty(assetPipeline);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 指定されたプラットフォームからビルド対象を決める。
        /// Windowsは常に先頭に含める。
        /// </summary>
        private static List<BuildTarget> ResolveBuildTargets(UploadPlatforms platforms, out List<string> unavailable)
        {
            unavailable = new List<string>();

            // Quest等のモバイル向けアバターもWindowsビルドが必要なため、Windowsは常に対象
            var targets = new List<BuildTarget> { BuildTarget.StandaloneWindows64 };

            if (platforms.HasFlag(UploadPlatforms.Android))
            {
                if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    targets.Add(BuildTarget.Android);
                }
                else
                {
                    unavailable.Add("Android (Quest)");
                }
            }

            if (platforms.HasFlag(UploadPlatforms.IOS))
            {
                if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                {
                    targets.Add(BuildTarget.iOS);
                }
                else
                {
                    unavailable.Add("iOS");
                }
            }

            return targets;
        }

        /// <summary>
        /// VRCSDKのビルダーにマルチプラットフォームビルドの対象を設定する。
        ///
        /// SDKはビルド対象の一覧をコントロールパネルのUIから受け取る作りになっており、
        /// 外部から設定する公開APIが用意されていない。
        /// SessionState(AvatarBuilderSessionState.AvatarPlatforms)を更新したうえで、
        /// 既に生成済みのUIが持つ内部フィールドもリフレクションで合わせる。
        /// SDKの更新で内部フィールドが変わった場合はfalseを返し、Windows単体のビルドにフォールバックする。
        /// </summary>
        private static bool TrySetBuilderPlatforms(IVRCSdkAvatarBuilderApi builder, List<BuildTarget> targets)
        {
            try
            {
                AvatarBuilderSessionState.AvatarPlatforms = targets;

                var field = builder.GetType().GetField("_selectedBuildTargets",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field == null || !typeof(List<BuildTarget>).IsAssignableFrom(field.FieldType))
                {
                    Debug.LogWarning(
                        "EAUploader: could not set multi-platform build targets on the VRChat SDK builder. " +
                        "Falling back to a Windows-only upload.");
                    return false;
                }

                field.SetValue(builder, new List<BuildTarget>(targets));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("EAUploader: failed to set multi-platform build targets: " + e.Message);
                return false;
            }
        }

        public static async Task UpdateVRCAvatar(string prefabPath, string contentName, string contentDescription,
            string releaseStatus, List<string> tags, string thumbnailPath)
        {
            var avatarData = await GetVRCAvatar(prefabPath);

            if (avatarData == null)
            {
                Debug.LogError("Failed to get avatar data");
                return;
            }

            VRCAvatar avatar = (VRCAvatar)avatarData;

            if (avatar.Name != contentName || avatar.Description != contentDescription || avatar.ReleaseStatus != releaseStatus || avatar.Tags != tags)
            {
                avatar.Name = contentName;
                avatar.Description = contentDescription;
                avatar.ReleaseStatus = releaseStatus;
                avatar.Tags = tags;
                await VRCApi.UpdateAvatarInfo(avatar.ID, avatar);
            }

            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                await VRCApi.UpdateAvatarImage(avatar.ID, avatar, thumbnailPath);
            }

            DialogPro.Show(DialogPro.DialogType.Success, T7e.Get("Update Succeed"), T7e.Get("Avatar updated successfully"));
            OnComplete?.Invoke(null, EventArgs.Empty);
        }

        #endregion
    }
}
