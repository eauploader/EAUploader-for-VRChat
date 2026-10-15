using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Modals
{
    /// <summary>
    /// タグ一覧を管理するモーダル。
    /// アバター一覧画面の「タグの設定」から開き、タグとタググループの追加・削除を行う。
    /// </summary>
    public class TagSettingsModal
    {
        private VisualElement modalContent;
        private ScrollView tagList;

        public void Open()
        {
            if (EAUploader.modal == null) return;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Tag Settings"));

            modalContent = new VisualElement();
            modalContent.styleSheets.Add(EAUploader.styles);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/TagSettingsModal");
            visualTree.CloneTree(modalContent);

            tagList = modalContent.Q<ScrollView>("tag_list");

            BindAddTag();
            BindAddGroup();
            PopulateTagList();

            LanguageUtility.Localization(modalContent);

            EAUploader.modal.setContent(modalContent);
            EAUploader.modal.setFooter(new[]
            {
                new Modal.ActionButton(T7e.Get("Close"), () => EAUploader.modal.Hide())
            });
            EAUploader.modal.Show();
        }

        #region Adding

        private void BindAddTag()
        {
            var field = modalContent.Q<TextFieldPro>("new_tag");
            field.maxLength = TagManager.MAX_TAG_LENGTH;
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddTag();
                }
            });

            RefreshGroupChoices();

            modalContent.Q<ShadowButton>("add_tag").clicked += AddTag;
        }

        private void AddTag()
        {
            var field = modalContent.Q<TextFieldPro>("new_tag");

            var registered = TagManager.RegisterTag(field.GetValue());
            if (registered == null) return;

            var groupDropdown = modalContent.Q<DropdownField>("new_tag_group");
            TagManager.SetTagGroup(registered, GroupNameFromChoice(groupDropdown.value));

            field.ClearValue();
            PopulateTagList();
        }

        private void BindAddGroup()
        {
            var field = modalContent.Q<TextFieldPro>("new_group");
            field.maxLength = TagManager.MAX_TAG_LENGTH;
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddGroup();
                }
            });

            modalContent.Q<ShadowButton>("add_group").clicked += AddGroup;
        }

        private void AddGroup()
        {
            var field = modalContent.Q<TextFieldPro>("new_group");

            if (TagManager.CreateGroup(field.GetValue()) == null) return;

            field.ClearValue();
            RefreshGroupChoices();
            PopulateTagList();
        }

        /// <summary>
        /// グループ選択用の選択肢を作り直す。先頭は「グループなし」。
        /// </summary>
        private void RefreshGroupChoices()
        {
            var dropdown = modalContent.Q<DropdownField>("new_tag_group");
            var current = dropdown.value;

            dropdown.choices = BuildGroupChoices();
            dropdown.SetValueWithoutNotify(
                dropdown.choices.Contains(current) ? current : dropdown.choices[0]);
        }

        private static List<string> BuildGroupChoices()
        {
            var choices = new List<string> { UngroupedLabel() };
            choices.AddRange(TagManager.GetGroupNames());
            return choices;
        }

        private static string UngroupedLabel() => T7e.Get("Ungrouped");

        /// <summary>
        /// 選択肢の表示文字列を、TagManagerが扱うグループ名(未設定はnull)に戻す。
        /// </summary>
        private static string GroupNameFromChoice(string choice)
        {
            return string.IsNullOrEmpty(choice) || choice == UngroupedLabel() ? null : choice;
        }

        #endregion

        #region Tag list

        private void PopulateTagList()
        {
            if (tagList == null) return;

            tagList.Clear();

            var groups = TagManager.GetGroupedTags();
            var hasAnyTag = groups.Any(g => g.Tags.Count > 0);

            modalContent.Q<Label>("tag_empty")?.EnableInClassList(
                "hidden", hasAnyTag || groups.Count > 0);

            foreach (var group in groups)
            {
                tagList.Add(CreateGroupHeader(group));

                foreach (var tag in group.Tags)
                {
                    tagList.Add(CreateTagRow(tag));
                }

                if (group.Tags.Count == 0)
                {
                    var empty = new Label(T7e.Get("No tags in this group."));
                    empty.AddToClassList("text-sm");
                    empty.AddToClassList("pl-4");
                    empty.AddToClassList("pb-1");
                    tagList.Add(empty);
                }
            }
        }

        private VisualElement CreateGroupHeader(TagGroupInfo group)
        {
            var header = new VisualElement();
            header.AddToClassList("flex-row");
            header.AddToClassList("items-center");
            header.AddToClassList("mt-2");
            header.AddToClassList("mb-1");
            header.AddToClassList("pb-1");
            header.AddToClassList("border-b");
            header.AddToClassList("border-b-zinc-300");

            var icon = new MaterialIcon { icon = group.IsUngrouped ? "label_off" : "folder_open" };
            icon.AddToClassList("mr-1");
            header.Add(icon);

            if (group.IsUngrouped)
            {
                header.Add(new Label(UngroupedLabel())
                {
                    style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold }
                });
                return header;
            }

            // グループ名はその場で書き換えられるようにする
            var nameField = new TextField { value = group.Name, maxLength = TagManager.MAX_TAG_LENGTH };
            nameField.style.flexGrow = 1;
            nameField.RegisterCallback<BlurEvent>(_ => RenameGroup(group.Name, nameField));
            nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    RenameGroup(group.Name, nameField);
                }
            });
            header.Add(nameField);

            var deleteButton = new Button(() => DeleteGroup(group.Name));
            deleteButton.style.marginLeft = 4;
            deleteButton.Add(new MaterialIcon { icon = "delete" });
            header.Add(deleteButton);

            return header;
        }

        private VisualElement CreateTagRow(string tag)
        {
            var row = new VisualElement();
            row.AddToClassList("flex-row");
            row.AddToClassList("items-center");
            row.AddToClassList("py-1");

            var icon = new MaterialIcon { icon = "sell" };
            icon.AddToClassList("mr-1");
            row.Add(icon);

            // タグ名はユーザーが入力した文字列なので翻訳対象から外す
            var label = new Label(tag) { style = { flexGrow = 1 } };
            label.AddToClassList("no-translate");
            row.Add(label);

            var usage = TagManager.CountUsage(tag);
            var usageLabel = new Label(usage.ToString());
            usageLabel.AddToClassList("text-sm");
            usageLabel.AddToClassList("mr-2");
            usageLabel.AddToClassList("no-translate");
            row.Add(usageLabel);

            // 所属グループの変更
            var groupDropdown = new DropdownField { choices = BuildGroupChoices() };
            groupDropdown.style.width = 160;
            groupDropdown.style.marginRight = 8;
            groupDropdown.SetValueWithoutNotify(TagManager.GetTagGroup(tag) ?? UngroupedLabel());
            groupDropdown.RegisterValueChangedCallback(evt =>
            {
                TagManager.SetTagGroup(tag, GroupNameFromChoice(evt.newValue));
                PopulateTagList();
            });
            row.Add(groupDropdown);

            var deleteButton = new Button(() => DeleteTag(tag, usage));
            deleteButton.Add(new MaterialIcon { icon = "delete" });
            row.Add(deleteButton);

            return row;
        }

        #endregion

        #region Commands

        private void DeleteTag(string tag, int usage)
        {
            var message = T7e.Get("Are you sure you want to delete this tag?") + "\n\n" + tag;
            if (usage > 0)
            {
                message += "\n\n" + T7e.Get("This tag will also be removed from the items using it.") + $" ({usage})";
            }

            if (!EditorUtility.DisplayDialog(T7e.Get("Tag Settings"), message, T7e.Get("Delete"), T7e.Get("Cancel")))
            {
                return;
            }

            TagManager.DeleteTag(tag);
            PopulateTagList();
            AvatarList.Main.RequestRefresh();
        }

        private void RenameGroup(string groupName, TextField nameField)
        {
            var newName = TagManager.Normalize(nameField.value);

            if (newName == null || newName == groupName)
            {
                nameField.SetValueWithoutNotify(groupName);
                return;
            }

            TagManager.RenameGroup(groupName, newName);
            RefreshGroupChoices();
            PopulateTagList();
        }

        private void DeleteGroup(string groupName)
        {
            var message = T7e.Get("Are you sure you want to delete this group?") + "\n\n" + groupName +
                          "\n\n" + T7e.Get("Tags in this group are kept and become ungrouped.");

            if (!EditorUtility.DisplayDialog(T7e.Get("Tag Settings"), message, T7e.Get("Delete"), T7e.Get("Cancel")))
            {
                return;
            }

            TagManager.DeleteGroup(groupName);
            RefreshGroupChoices();
            PopulateTagList();
        }

        #endregion
    }
}
