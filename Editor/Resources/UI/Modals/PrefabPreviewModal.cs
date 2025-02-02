using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader;
using EAUploader.UI.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.Components; // Utility.GetEAUploaderMeta などがある場合

namespace EAUploader.UI.Modals
{
    /// <summary>
    /// Prefabのプレビューを表示するモーダル
    /// </summary>
    public class PrefabPreviewModal
    {
        private string prefabPath;
        private Texture2D previewImage;
        private VisualElement modalContent;
        private Image previewElement;

        /// <summary>
        /// コンストラクタでPrefabのパスとプレビュー画像を受け取る
        /// </summary>
        public PrefabPreviewModal(string prefabPath, Texture2D image)
        {
            this.prefabPath = prefabPath;
            this.previewImage = image;
        }

        /// <summary>
        /// モーダルを開く
        /// </summary>
        public void Open()
        {
            // モーダルの初期化
            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle("Prefab Preview");

            // UXMLをロードし、モーダルコンテンツとして複製
            // ここでは PrefabPreviewer.uxml をそのまま利用
            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/PrefabPreviewer");
            modalContent = new VisualElement();
            visualTree.CloneTree(modalContent);

            // Tailwindや共通スタイルを適用する
            modalContent.styleSheets.Add(EAUploader.styles);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            // UI要素を取得
            previewElement = modalContent.Q<Image>("preview");
            if (previewElement != null)
            {
                previewElement.image = previewImage;
            }

            // 「Regenerate Preview」ボタン
            var regenerateButton = modalContent.Q<ShadowButton>("regenerate");
            regenerateButton.clicked += RegeneratePreview;

            // 多言語対応が必要なら
            LanguageUtility.Localization(modalContent);

            // コンテンツをモーダルに設定
            EAUploader.modal.setContent(modalContent);

            // フッタのボタン（例としてCloseだけ）
            Modal.ActionButton[] actionButtons = new Modal.ActionButton[]
            {
                new Modal.ActionButton("Close", () =>
                {
                    EAUploader.modal.Hide();
                }),
            };
            EAUploader.modal.setFooter(actionButtons);

            // 最後にモーダルを表示
            EAUploader.modal.Show();
        }

        /// <summary>
        /// プレビューを再生成する
        /// </summary>
        private void RegeneratePreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                // 新しくプレビューを生成
                Texture2D newPreview = PrefabPreview.GeneratePreview(prefab);
                previewImage = newPreview;

                // UIを更新
                if (previewElement != null)
                {
                    previewElement.image = previewImage;
                }

                // 生成したプレビューを保存
                PrefabPreview.SavePrefabPreview(prefabPath, newPreview);
            }
        }
    }
}
