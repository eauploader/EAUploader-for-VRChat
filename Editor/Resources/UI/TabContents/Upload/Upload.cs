using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Upload
{
    /// <summary>
    /// アップロード画面。
    /// アバターの選択は一覧画面で行うため、この画面では選択中のアバターだけを扱う。
    /// </summary>
    internal class Main
    {
        public static VisualElement root;
        public static Components.Preview preview;

        public static void ShowContent(VisualElement rootContainer)
        {
            root = rootContainer;
            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/Upload/Upload");
            visualTree.CloneTree(root);

            SetupNavigation();

            var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;

            root.Q<Label>("selected_avatar_name").text = string.IsNullOrEmpty(selectedPrefabPath)
                ? T7e.Get("No avatar selected")
                : Path.GetFileNameWithoutExtension(selectedPrefabPath);

            UploadForm.ShowContent(root.Q("upload_form"));

            preview = new Components.Preview(root.Q("avatar_preview"), selectedPrefabPath);
            preview.ShowContent();
        }

        private static void SetupNavigation()
        {
            root.Q<Button>("back_to_list").clicked += () =>
                EAUploader.Navigate(EAUploaderScreen.AvatarList);

            root.Q<Button>("go_setup").clicked += () =>
                EAUploader.Navigate(EAUploaderScreen.Setup);
        }

        /// <summary>
        /// アップロード完了時など、選択中アバターの表示を更新する。
        /// </summary>
        internal static void RefreshSelectedAvatar()
        {
            if (root?.panel == null) return;

            var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;
            var label = root.Q<Label>("selected_avatar_name");
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(selectedPrefabPath)
                    ? T7e.Get("No avatar selected")
                    : Path.GetFileNameWithoutExtension(selectedPrefabPath);
            }

            AvatarList.Main.RequestRefresh();
        }
    }
}
