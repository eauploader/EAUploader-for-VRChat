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
    ///
    /// インポート時に VRCAvatarDescriptor を持たない Prefab に対して開かれ、
    /// 既定では「衣装(Cloth)」がチェック済みの状態で提示される。
    /// </summary>
    public class ClothSettingModal
    {
        private static readonly List<string> GENRE_CHOICES = new List<string> { "Cloth", "Accessory", "Other" };

        // 表示したいプレハブ一覧
        private readonly List<PrefabInfo> prefabInfos;

        // UXMLから複製したモーダルコンテンツ
        private VisualElement modalContent;

        // UXMLのScrollView
        private ScrollView prefabListScroll;

        // 行ごとの選択状態
        private readonly List<Row> rows = new List<Row>();

        private class Row
        {
            public PrefabInfo Info;
            public Toggle Apply;
            public DropdownField Genre;
        }

        // コンストラクタで「対象のプレハブ一覧」を受け取る
        public ClothSettingModal(List<PrefabInfo> prefabInfos)
        {
            this.prefabInfos = prefabInfos ?? new List<PrefabInfo>();
        }

        public void Open()
        {
            if (EAUploader.modal == null) return;

            // モーダル初期化
            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Outfit Settings"));

            // UXMLをロード
            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/ClothSettingModal");
            modalContent = new VisualElement();
            visualTree.CloneTree(modalContent);

            modalContent.styleSheets.Add(EAUploader.styles);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            // ScrollViewを取得
            prefabListScroll = modalContent.Q<ScrollView>("prefabList");

            // Prefab一覧を生成
            PopulatePrefabList();

            LanguageUtility.Localization(modalContent);

            // モーダルの中身を差し替え
            EAUploader.modal.setContent(modalContent);

            // フッタのボタン (Close, Save)
            Modal.ActionButton[] actionButtons = new Modal.ActionButton[]
            {
                new Modal.ActionButton(T7e.Get("Close"), () =>
                {
                    // 選択せずに閉じた場合はジャンルを確定させない。
                    // デフォルトの衣装のまま残り、次回インポート時に再度確認する。
                    EAUploader.modal.Hide();
                }),
                new Modal.ActionButton(T7e.Get("Save"), () =>
                {
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

            prefabListScroll.Clear();
            rows.Clear();

            var header = new Label(T7e.Get("Checked prefabs will be saved with the selected genre."));
            header.AddToClassList("pb-2");
            prefabListScroll.Add(header);

            var selectAllRow = new VisualElement();
            selectAllRow.style.flexDirection = FlexDirection.Row;
            selectAllRow.style.alignItems = Align.Center;
            selectAllRow.style.marginBottom = 8;

            var selectAll = new Toggle(T7e.Get("Select all")) { value = true };
            selectAll.RegisterValueChangedCallback(evt =>
            {
                foreach (var row in rows)
                {
                    row.Apply.SetValueWithoutNotify(evt.newValue);
                }
            });
            selectAllRow.Add(selectAll);
            prefabListScroll.Add(selectAllRow);

            foreach (var info in prefabInfos)
            {
                if (info == null) continue;

                // 1つのアイテム行
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.marginBottom = 8;

                // 適用チェックボックス(既定でチェック済み)
                var applyToggle = new Toggle { value = true };
                applyToggle.style.marginRight = 4;
                itemRow.Add(applyToggle);

                // プレビュー画像
                var imageElement = new Image();
                imageElement.style.width = 64;
                imageElement.style.height = 64;
                imageElement.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                var preview = info.Preview != null
                    ? info.Preview
                    : PrefabPreview.GetPrefabPreview(info.Path);
                if (preview != null)
                {
                    imageElement.image = preview;
                }
                itemRow.Add(imageElement);

                // 名前ラベル
                var nameLabel = new Label(info.Name);
                nameLabel.style.marginLeft = 8;
                nameLabel.style.flexGrow = 1;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemRow.Add(nameLabel);

                // ドロップダウン(既定はCloth)
                var currentGenre = info.Genre.ToString();
                var initialIndex = GENRE_CHOICES.IndexOf(currentGenre);
                if (initialIndex < 0) initialIndex = GENRE_CHOICES.IndexOf("Cloth");

                var dropdown = new DropdownField(T7e.Get("Genre"), new List<string>(GENRE_CHOICES), initialIndex);
                dropdown.style.marginLeft = 8;
                itemRow.Add(dropdown);

                prefabListScroll.Add(itemRow);

                rows.Add(new Row { Info = info, Apply = applyToggle, Genre = dropdown });
            }
        }

        /// <summary>
        /// 「Save」ボタン押下時に、ドロップダウンの値を PrefabManager に書き戻す
        /// </summary>
        private void SaveSettings()
        {
            foreach (var row in rows)
            {
                if (row.Info == null) continue;

                if (row.Apply.value && Enum.TryParse<EAUploaderMeta.PrefabGenre>(row.Genre.value, out var genre))
                {
                    PrefabManager.SetPrefabGenre(row.Info.Path, genre);
                }
                else
                {
                    // チェックが外れている場合は現在のジャンルを維持しつつ、
                    // 確認済みとして記録して再度尋ねないようにする。
                    PrefabManager.MarkGenreConfirmed(row.Info.Path);
                }
            }

            PrefabManager.InvalidateCache();
            UI.AvatarList.Main.RequestRefresh();
        }
    }
}
