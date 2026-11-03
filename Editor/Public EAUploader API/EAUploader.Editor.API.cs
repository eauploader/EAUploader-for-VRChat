using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VRC;
using VRC.Core;
using VRC.SDK3.Validation;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;

namespace EAUploader.Editor.API
{
    /// <summary>
    /// EAUploaderの公開APIを提供するクラス
    /// EAUploader全体に関わるAPI, 言語, アップロードのAPI
    /// </summary>
    public static class CoreAPI
    {
        /// <summary>
        /// 現在選択されているプレファブのパスを取得または設定します。
        /// </summary>
        public static string SelectedPrefabPath
        {
            get => EAUploaderCore.selectedPrefabPath;
            set => EAUploaderCore.selectedPrefabPath = value;
        }
        /// <summary>
        /// 選択されているプレファブのパスが変更されたときに発生するイベント
        /// </summary>
        public static event System.Action<string> SelectedPrefabPathChanged
        {
            add => EAUploaderCore.SelectedPrefabPathChanged += value;
            remove => EAUploaderCore.SelectedPrefabPathChanged -= value;
        }

        /// <summary>
        /// VRMパッケージが利用可能かどうかを示す値を取得します。
        /// </summary>
        public static bool HasVRM => EAUploaderCore.HasVRM;

        /// <summary>
        /// Avatar-Optimizerパッケージが利用可能かどうかを示す値を取得します。
        /// </summary>
        public static bool HasAvatarOptimizer => EAUploaderCore.HasAAO;

        /// <summary>
        /// EAUploaderの現在のバージョンを取得します。
        /// </summary>
        /// <param name="noText">バージョン番号のみを取得する場合はtrue、説明テキストを含める場合はfalse</param>
        /// <returns>EAUploaderのバージョン文字列</returns>
        public static string GetVersion(bool noText = false)
        {
            return EAUploaderCore.GetVersion(noText);
        }

        /// <summary>
        /// 再読み込みします。
        /// </summary>
        public static void Reload()
        {
            EAUploaderCore.ReloadSDK();
        }
    }

    /// <summary>
    /// 言語関連の公開APIを提供するクラス
    /// </summary>
    public static class LanguageAPI
    {
        /// <summary>
        /// 現在の言語を取得します。
        /// </summary>
        /// <returns>現在の言語コード</returns>
        public static string GetCurrentLanguage()
        {
            return LanguageUtility.GetCurrentLanguage();
        }
        /// <summary>
        /// 指定したVisualElementとその子要素のテキストをローカライズします。
        /// </summary>
        /// <param name="root">ローカライズ対象のルートVisualElement</param>
        public static void Localize(VisualElement root)
        {
            LanguageUtility.Localization(root);
        }

        /// <summary>
        /// 指定したJSONファイルを使用して、指定したVisualElementとその子要素のテキストをローカライズします。
        /// </summary>
        /// <param name="root">ローカライズ対象のルートVisualElement</param>
        /// <param name="localizationFolderPath">ローカライズ用のJSONファイルが格納されているフォルダのパス</param>
        public static void LocalizeFromJsonFile(VisualElement root, string localizationFolderPath)
        {
            LanguageUtility.LocalizationFromJsonFile(root, localizationFolderPath);
        }

        /// <summary>
        /// 指定したキーに対応するローカライズされたテキストを、指定したJSONファイルから取得します。
        /// </summary>
        /// <param name="key">ローカライズ対象のキー</param>
        /// <param name="localizationFolderPath">ローカライズ用のJSONファイルが格納されているフォルダのパス</param>
        /// <returns>ローカライズされたテキスト。対応するテキストが見つからない場合はキーの値が返されます。</returns>
        public static string GetLocalizedTextFromJsonFile(string key, string localizationFolderPath)
        {
            return LanguageUtility.T7eFromJsonFile(key, localizationFolderPath);
        }

        /// <summary>
        /// アプリケーションの言語を変更します。
        /// </summary>
        /// <param name="language">設定する言語コード</param>
        public static void ChangeLanguage(string language)
        {
            LanguageUtility.ChangeLanguage(language);
        }

        /// <summary>
        /// 利用可能な言語のリストを取得します。
        /// </summary>
        /// <returns>利用可能な言語情報のリスト</returns>
        public static List<LanguageInfo> GetAvailableLanguages()
        {
            return LanguageUtility.GetAvailableLanguages();
        }
    }

    /// <summary>
    /// アップロード関連の公開APIを提供するクラス
    /// </summary>
    public static class UploadAPI
    {
        /// <summary>
        /// アップロードが完了したときに発生するイベント
        /// </summary>
        public static event EventHandler OnUploadComplete
        {
            add => AvatarUploader.OnComplete += value;
            remove => AvatarUploader.OnComplete -= value;
        }
        /// <summary>
        /// アップロードの進行状況が変更されたときに発生するイベント
        /// </summary>
        public static event EventHandler<AvatarUploader.ProgressEventArgs> UploadProgressChanged
        {
            add => AvatarUploader.ProgressChanged += value;
            remove => AvatarUploader.ProgressChanged -= value;
        }

        /// <summary>
        /// 指定したプレハブをビルドしてテストします。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>ビルドとテストの非同期タスク</returns>
        public static Task BuildAndTestAsync(string prefabPath)
        {
            return AvatarUploader.BuildAndTestAsync(prefabPath);
        }

        /// <summary>
        /// 指定したプレハブのVRCAvatar情報を取得します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <returns>VRCAvatarオブジェクト。取得に失敗した場合はnullを返します。</returns>
        public static Task<VRCAvatar?> GetVRCAvatar(string prefabPath)
        {
            return AvatarUploader.GetVRCAvatarAsync(prefabPath);
        }

        /// <summary>
        /// 指定したプレハブをアバターとしてアップロードします。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <param name="contentName">アバター名</param>
        /// <param name="contentDescription">アバターの説明</param>
        /// <param name="releaseStatus">公開ステータス</param>
        /// <param name="tags">タグ</param>
        /// <param name="thumbnailPath">サムネイルのパス</param>
        /// <returns>アップロードの非同期タスク</returns>
        public static Task UploadAvatarAsync(string prefabPath, string contentName, string contentDescription, string releaseStatus, List<string> tags, string thumbnailPath)
        {
            return AvatarUploader.UploadAvatarAsync(prefabPath, contentName, contentDescription, releaseStatus, tags, thumbnailPath);
        }

        /// <summary>
        /// 指定したプレハブのVRCAvatar情報を更新します。
        /// </summary>
        /// <param name="prefabPath">プレハブのパス</param>
        /// <param name="contentName">アバター名</param>
        /// <param name="contentDescription">アバターの説明</param>
        /// <param name="releaseStatus">公開ステータス</param>
        /// <param name="tags">タグ</param>
        /// <param name="thumbnailPath">サムネイルのパス</param>
        /// <returns>更新の非同期タスク</returns>
        public static Task UpdateVRCAvatar(string prefabPath, string contentName, string contentDescription, string releaseStatus, List<string> tags, string thumbnailPath)
        {
            return AvatarUploader.UpdateVRCAvatarAsync(prefabPath, contentName, contentDescription, releaseStatus, tags, thumbnailPath);
        }
    }

}