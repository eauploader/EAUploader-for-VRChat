using EAUploader.CustomPrefabUtility;
using EAUploader.Components;
using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Modals
{
    /// <summary>
    /// 衣装用の設定モーダル。非アバターのプレハブ一覧を表示し、
    /// Cloth, Accessory, Other のジャンルを設定できる。
    /// </summary>
    public class ClothSettingModal
    {
        // 表示したいプレハブ一覧
        private List<PrefabInfo> prefabInfos;

        // UXMLから複製したモーダルコンテンツ
        private VisualElement modalContent;

        // UXMLのScrollView
        private ScrollView prefabListScroll;

        // コンストラクタで「対象のプレハブ一覧」を受け取る
        public ClothSettingModal(List<PrefabInfo> prefabInfos)
        {
            this.prefabInfos = prefabInfos;
        }

        public void Open()
        {
            // モーダル初期化
            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle("Outfit Settings");

            // UXMLをロード
            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/ClothSettingModal");
            modalContent = new VisualElement();
            visualTree.CloneTree(modalContent);

            // ScrollViewを取得
            prefabListScroll = modalContent.Q<ScrollView>("prefabList");

            // Prefab一覧を生成
            PopulatePrefabList();

            // モーダルの中身を差し替え
            EAUploader.modal.setContent(modalContent);

            // フッタのボタン (Import, Close)
            Modal.ActionButton[] actionButtons = new Modal.ActionButton[]
            {
                new Modal.ActionButton("Close", () =>
                {
                    EAUploader.modal.Hide();
                }),
                new Modal.ActionButton("Import", () =>
                {
                    // 「Import」ボタン → 設定を保存
                    SaveSettings();
                    EAUploader.modal.Hide();
                })
            };
            EAUploader.modal.setFooter(actionButtons);

            EAUploader.modal.Show();
        }

        /// <summary>
        /// ScrollViewに各プレハブを表示する
        /// </summary>
        private void PopulatePrefabList()
        {
            if (prefabListScroll == null) return;

            // 既存をクリア
            prefabListScroll.Clear();

            foreach (var info in prefabInfos)
            {
                // 1つのアイテム行
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.marginBottom = 8;

                // プレビュー画像
                var imageElement = new Image();
                imageElement.style.width = 64;
                imageElement.style.height = 64;
                imageElement.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                // もしプレビューがないなら Lazy Load するなど
                // ここではシンプルに PrefabInfo.Preview を使う
                if (info.Preview != null)
                {
                    imageElement.image = info.Preview;
                }

                // 名前ラベル
                var nameLabel = new Label(info.Name);
                nameLabel.style.marginLeft = 8;
                nameLabel.style.flexGrow = 0;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                // ドロップダウン
                var dropdown = new DropdownField("Genre", new List<string> { "Cloth", "Accessory", "Other" }, 0);
                dropdown.style.marginLeft = 8;
                // 現在のGenreを文字列にして値を合わせる
                dropdown.value = info.Genre.ToString(); // e.g. "Cloth" or "Accessory" or "Other"

                // 生成した要素をアイテム行に追加
                itemRow.Add(imageElement);
                itemRow.Add(nameLabel);
                itemRow.Add(dropdown);

                // itemRowを ScrollView に追加
                prefabListScroll.Add(itemRow);

                // ついでに PrefabInfo をタグ付けしておく (or Dictionary<PrefabInfo, DropdownField>)
                // ここは保存時に参照できるようにするため
                dropdown.userData = info;
            }
        }

        /// <summary>
        /// 「Import」ボタン押下時に、ドロップダウンの値を PrefabManager に書き戻す
        /// </summary>
        private void SaveSettings()
        {
            if (prefabListScroll == null) return;

            foreach (var child in prefabListScroll.Children())
            {
                // child は itemRow
                // itemRowの子要素のうち、DropdownField を探す
                var dropdown = child.Q<DropdownField>();
                if (dropdown != null && dropdown.userData is PrefabInfo info)
                {
                    // enum parse
                    if (Enum.TryParse<EAUploaderMeta.PrefabGenre>(dropdown.value, out var genre))
                    {
                        PrefabManager.SetPrefabGenre(info.Path, genre);
                        Debug.Log($"Set {info.Name} => {genre}");
                    }
                }
            }
        }
    }
}
