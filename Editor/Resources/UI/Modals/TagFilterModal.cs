using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Modals
{
    /// <summary>
    /// タグによる絞り込みを選ぶモーダル。
    /// タググループごとに区切って、タグをチェックボックスのグリッドで並べる。
    /// チェックしたタグをすべて持つアイテムだけが一覧に残る(AND条件)。
    /// </summary>
    public class TagFilterModal
    {
        private readonly List<string> selection;
        private readonly Action onChanged;

        private VisualElement modalContent;

        /// <param name="selection">選択中のタグ。モーダル内での変更をそのまま書き込む。</param>
        /// <param name="onChanged">選択が変わったときに呼ばれる</param>
        public TagFilterModal(List<string> selection, Action onChanged)
        {
            this.selection = selection;
            this.onChanged = onChanged;
        }

        public void Open()
        {
            if (EAUploader.modal == null) return;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Filter by tag"));

            modalContent = new VisualElement();
            modalContent.styleSheets.Add(EAUploader.styles);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/TagFilterModal");
            visualTree.CloneTree(modalContent);

            PopulateList();

            LanguageUtility.Localization(modalContent);

            EAUploader.modal.setContent(modalContent);
            EAUploader.modal.setFooter(new[]
            {
                new Modal.ActionButton(T7e.Get("Clear selection"), ClearSelection),
                new Modal.ActionButton(T7e.Get("Close"), () => EAUploader.modal.Hide())
            });
            EAUploader.modal.Show();
        }

        private void ClearSelection()
        {
            if (selection.Count == 0) return;

            selection.Clear();
            PopulateList();
            onChanged?.Invoke();
        }

        private void PopulateList()
        {
            var list = modalContent.Q<ScrollView>("tag_filter_list");
            if (list == null) return;

            list.Clear();

            var groups = TagManager.GetGroupedTags();
            var hasAnyTag = groups.Any(g => g.Tags.Count > 0);

            modalContent.Q<Label>("tag_filter_empty")?.EnableInClassList("hidden", hasAnyTag);

            foreach (var group in groups)
            {
                if (group.Tags.Count == 0) continue;

                list.Add(CreateGroupHeader(group));

                // グリッド状に並べる。幅が足りなくなったら折り返す。
                var grid = new VisualElement();
                grid.AddToClassList("tag-filter-grid");

                foreach (var tag in group.Tags)
                {
                    grid.Add(CreateTagToggle(tag));
                }

                list.Add(grid);
            }
        }

        private static VisualElement CreateGroupHeader(TagGroupInfo group)
        {
            var header = new VisualElement();
            header.AddToClassList("flex-row");
            header.AddToClassList("items-center");
            header.AddToClassList("mt-2");
            header.AddToClassList("mb-1");

            var icon = new MaterialIcon { icon = group.IsUngrouped ? "label_off" : "folder_open" };
            icon.AddToClassList("mr-1");
            header.Add(icon);

            // グループ名はユーザーが入力した文字列なので翻訳対象から外す
            var label = new Label(group.IsUngrouped ? T7e.Get("Ungrouped") : group.Name)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold }
            };
            if (!group.IsUngrouped) label.AddToClassList("no-translate");
            header.Add(label);

            return header;
        }

        private VisualElement CreateTagToggle(string tag)
        {
            var cell = new VisualElement();
            cell.AddToClassList("tag-filter-cell");

            var toggle = new Toggle(tag) { value = TagManager.Contains(selection, tag) };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    if (!TagManager.Contains(selection, tag)) selection.Add(tag);
                }
                else
                {
                    selection.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                }

                onChanged?.Invoke();
            });

            // タグ名はユーザーが入力した文字列なので翻訳対象から外す
            var label = toggle.Q<Label>();
            label?.AddToClassList("no-translate");

            cell.Add(toggle);
            return cell;
        }
    }
}
