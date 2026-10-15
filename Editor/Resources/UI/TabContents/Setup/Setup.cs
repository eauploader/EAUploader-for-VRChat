using EAUploader.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Setup
{
    /// <summary>
    /// 編集(セットアップ)画面。
    /// 一覧から選んだアイテムを対象に、左側で編集ツールを起動し、
    /// 右側でメタ情報をその場で編集する。
    /// </summary>
    internal class Main
    {
        private static VisualElement root;
        internal static Preview preview;
        internal static Dictionary<string, bool> editorFoldouts = new Dictionary<string, bool>();
        private static bool isEditorInfoLoaded = false;

        private static readonly List<string> GENRE_CHOICES =
            new List<string> { "Avatar", "Cloth", "Accessory", "Other" };

        public static void ShowContent(VisualElement rootElement)
        {
            root = rootElement;

            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/Setup/Setup");
            visualTree.CloneTree(root);

            SetupNavigation();

            var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;

            if (string.IsNullOrEmpty(selectedPrefabPath) || PrefabManager.GetPrefab(selectedPrefabPath) == null)
            {
                ShowNoSelection();
                return;
            }

            preview = new Preview(root.Q("avatar_preview"), selectedPrefabPath);
            preview.ShowContent();
            preview.UpdatePreview(selectedPrefabPath);

            BuildMetaPanel();
            BuildEditor();
            SetupExtensionButtons();
        }

        private static void ShowNoSelection()
        {
            var message = new Label(T7e.Get("Select an item from the list first."))
            {
                style =
                {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 18,
                }
            };
            root.Q("avatar_preview").Add(message);

            root.Q<ScrollView>("meta_panel").style.display = DisplayStyle.None;
            root.Q("edit_menu").style.display = DisplayStyle.None;
            root.Q<Button>("go_upload").style.display = DisplayStyle.None;
        }

        #region Navigation

        private static void SetupNavigation()
        {
            root.Q<Button>("back_to_list").clicked += () =>
                EAUploader.Navigate(EAUploaderScreen.AvatarList);

            var uploadButton = root.Q<Button>("go_upload");
            uploadButton.clicked += () => EAUploader.Navigate(EAUploaderScreen.Upload);

            // アップロードできるのはアバターだけ
            uploadButton.style.display = IsSelectedAvatar() ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsSelectedAvatar()
        {
            var path = EAUploaderCore.selectedPrefabPath;
            if (string.IsNullOrEmpty(path)) return false;

            return PrefabManager.GetPrefabGenre(path) == EAUploaderMeta.PrefabGenre.Avatar;
        }

        #endregion

        #region Meta panel

        private static void BuildMetaPanel()
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;
            var info = PrefabManager.GetPrefabInfo(prefabPath);
            var prefab = PrefabManager.GetPrefab(prefabPath);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);

            UpdateThumbnail();

            root.Q<ShadowButton>("regenerate_thumbnail").clicked += RegenerateThumbnail;
            root.Q<ShadowButton>("change_thumbnail").clicked += ChangeThumbnail;

            // 名前 : フォーカスが外れた時点で反映する
            var nameField = root.Q<TextField>("meta_name");
            nameField.SetValueWithoutNotify(info != null ? info.Name : Path.GetFileNameWithoutExtension(prefabPath));
            nameField.RegisterCallback<BlurEvent>(_ => ApplyRename(nameField));
            nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    ApplyRename(nameField);
                }
            });

            // ジャンル
            var genreDropdown = root.Q<DropdownField>("meta_genre");
            genreDropdown.choices = new List<string>(GENRE_CHOICES);
            genreDropdown.SetValueWithoutNotify(meta.genre.ToString());
            genreDropdown.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse<EAUploaderMeta.PrefabGenre>(evt.newValue, out var genre)) return;

                PrefabManager.SetPrefabGenre(EAUploaderCore.selectedPrefabPath, genre);
                AvatarList.Main.RequestRefresh();

                // ジャンルが変わるとアップロード可否も変わるので、ボタンの表示を更新する
                root.Q<Button>("go_upload").style.display =
                    IsSelectedAvatar() ? DisplayStyle.Flex : DisplayStyle.None;
            });

            BuildTagSection();
            BuildMemoField();

            // ピン留め / 非表示
            var pinnedToggle = root.Q<SlideToggle>("meta_pinned");
            pinnedToggle.SetValueWithoutNotify(meta.status == EAUploaderMeta.PrefabStatus.Pinned);
            pinnedToggle.RegisterValueChangedCallback(evt => SetStatus(
                evt.newValue ? EAUploaderMeta.PrefabStatus.Pinned : EAUploaderMeta.PrefabStatus.Show));

            var hiddenToggle = root.Q<SlideToggle>("meta_hidden");
            hiddenToggle.SetValueWithoutNotify(meta.status == EAUploaderMeta.PrefabStatus.Hidden);
            hiddenToggle.RegisterValueChangedCallback(evt => SetStatus(
                evt.newValue ? EAUploaderMeta.PrefabStatus.Hidden : EAUploaderMeta.PrefabStatus.Show));

            BuildMetaDetails(prefab, info);

            root.Q<ShadowButton>("duplicate_prefab").clicked += DuplicatePrefab;
            root.Q<ShadowButton>("delete_prefab").clicked += DeletePrefab;
        }

        #region Tags

        // 入力に対して出す候補の最大件数
        private const int MAX_TAG_SUGGESTIONS = 8;

        /// <summary>
        /// タグの追加欄と、付いているタグの一覧を組み立てる。
        /// 入力欄に打ち込むと、一致する既存タグが候補として下に並ぶ。
        /// 一覧から探したい場合は、その下のドロップダウンからも選べる。
        /// </summary>
        private static void BuildTagSection()
        {
            var input = root.Q<TextFieldPro>("tag_input");
            input.maxLength = TagManager.MAX_TAG_LENGTH;
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddTagFromInput();
                }
            });
            input.RegisterValueChangedCallback(_ => RefreshInputSuggestions());

            root.Q<ShadowButton>("add_tag").clicked += AddTagFromInput;

            var dropdown = root.Q<DropdownField>("tag_suggestions");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue) || evt.newValue == TagPlaceholder()) return;

                // 選んだ候補を入力欄に入れて、追加ボタン(またはEnter)で確定させる
                input.value = evt.newValue;
                input.Reset();
                dropdown.SetValueWithoutNotify(TagPlaceholder());
            });

            RefreshTagSection();
        }

        private static string TagPlaceholder() => T7e.Get("Select from existing tags");

        private static void AddTagFromInput()
        {
            var input = root.Q<TextFieldPro>("tag_input");
            if (input == null) return;

            AddTag(input.GetValue());
        }

        private static void AddTag(string tag)
        {
            var added = TagManager.AddTagToPrefab(EAUploaderCore.selectedPrefabPath, tag);

            // 既に付いているタグや空文字の場合も、入力欄は消して次の入力に備える
            root.Q<TextFieldPro>("tag_input")?.ClearValue();
            HideInputSuggestions();
            RefreshTagSection();

            if (added) AvatarList.Main.RequestRefresh();
        }

        /// <summary>
        /// 入力中の文字に一致する既存タグを候補として出す。
        /// </summary>
        private static void RefreshInputSuggestions()
        {
            var container = root.Q<VisualElement>("tag_input_suggestions");
            var input = root.Q<TextFieldPro>("tag_input");
            if (container == null || input == null) return;

            container.Clear();

            var query = input.GetValue();
            if (string.IsNullOrWhiteSpace(query))
            {
                container.EnableInClassList("hidden", true);
                return;
            }

            var attached = TagManager.GetTags(EAUploaderCore.selectedPrefabPath);
            var matches = TagManager.GetAllTags()
                .Where(t => !TagManager.Contains(attached, t))
                .Where(t => t.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(MAX_TAG_SUGGESTIONS)
                .ToList();

            container.EnableInClassList("hidden", matches.Count == 0);

            foreach (var tag in matches)
            {
                // タグ名はユーザーの入力なので翻訳対象から外す
                var button = new Button(() => AddTag(tag)) { text = tag };
                button.AddToClassList("tag-suggestion");
                button.AddToClassList("no-translate");
                container.Add(button);
            }
        }

        private static void HideInputSuggestions()
        {
            root?.Q<VisualElement>("tag_input_suggestions")?.EnableInClassList("hidden", true);
        }

        /// <summary>
        /// 付いているタグの表示と、候補ドロップダウンの中身を作り直す。
        /// </summary>
        private static void RefreshTagSection()
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;

            var chips = root.Q<VisualElement>("tag_chips");
            chips.Clear();

            var tags = TagManager.GetTags(prefabPath);
            foreach (var tag in tags)
            {
                chips.Add(CreateTagChip(tag));
            }

            root.Q<Label>("tag_empty")?.EnableInClassList("hidden", tags.Count > 0);

            // 既に付いているタグは候補から外す
            var placeholder = TagPlaceholder();
            var choices = new List<string> { placeholder };
            choices.AddRange(TagManager.GetAllTags().Where(t => !TagManager.Contains(tags, t)));

            var dropdown = root.Q<DropdownField>("tag_suggestions");
            dropdown.choices = choices;
            dropdown.SetValueWithoutNotify(placeholder);
            dropdown.SetEnabled(choices.Count > 1);
        }

        private static VisualElement CreateTagChip(string tag)
        {
            var chip = new VisualElement();
            chip.AddToClassList("tag-chip--removable");

            // タグ名はユーザーの入力なので翻訳対象から外す
            var label = new Label(tag);
            label.AddToClassList("no-translate");
            chip.Add(label);

            var remove = new Button(() =>
            {
                TagManager.RemoveTagFromPrefab(EAUploaderCore.selectedPrefabPath, tag);
                RefreshTagSection();
                AvatarList.Main.RequestRefresh();
            });
            remove.AddToClassList("tag-chip__remove");
            remove.style.backgroundColor = Color.clear;
            remove.style.borderTopWidth = 0;
            remove.style.borderBottomWidth = 0;
            remove.style.borderLeftWidth = 0;
            remove.style.borderRightWidth = 0;
            remove.style.paddingLeft = 0;
            remove.style.paddingRight = 0;
            remove.style.marginLeft = 2;
            remove.style.marginRight = 0;
            remove.style.marginTop = 0;
            remove.style.marginBottom = 0;
            remove.Add(new MaterialIcon { icon = "close", style = { fontSize = 14 } });
            chip.Add(remove);

            return chip;
        }

        #endregion

        /// <summary>
        /// メモ欄。フォーカスが外れた時点で保存する。
        /// </summary>
        private static void BuildMemoField()
        {
            var memoField = root.Q<TextField>("meta_memo");
            if (memoField == null) return;

            memoField.SetValueWithoutNotify(PrefabManager.GetMemo(EAUploaderCore.selectedPrefabPath));
            memoField.RegisterCallback<BlurEvent>(_ =>
                PrefabManager.SetMemo(EAUploaderCore.selectedPrefabPath, memoField.value));
        }

        private static void BuildMetaDetails(GameObject prefab, PrefabInfo info)
        {
            var details = root.Q<VisualElement>("meta_details");
            details.Clear();

            if (info != null)
            {
                details.Add(new Label(T7e.Get("Last Modified: ") + info.LastModified.ToString("yyyy/MM/dd HH:mm")));
            }

            if (Utility.CheckAvatarHasVRCAvatarDescriptor(prefab))
            {
                details.Add(new Label(T7e.Get("Height: ") + Utility.GetAvatarHeight(prefab) + "m"));
            }

            var pipelineManager = Utility.GetPipelineManager(prefab);
            if (pipelineManager != null && !string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                var blueprintRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                blueprintRow.Add(new Label("Blueprint") { style = { flexGrow = 1 } });

                var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = pipelineManager.blueprintId)
                { text = T7e.Get("Copy") };
                blueprintRow.Add(copyButton);

                details.Add(blueprintRow);
            }

            details.Add(new Label(T7e.Get("Path: ") + info?.Path));
        }

        private static void SetStatus(EAUploaderMeta.PrefabStatus status)
        {
            var prefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null || meta.status == status) return;

            meta.status = status;
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            PrefabManager.InvalidateCache();

            // もう一方のトグルと排他になるよう表示を合わせる
            root.Q<SlideToggle>("meta_pinned")
                .SetValueWithoutNotify(status == EAUploaderMeta.PrefabStatus.Pinned);
            root.Q<SlideToggle>("meta_hidden")
                .SetValueWithoutNotify(status == EAUploaderMeta.PrefabStatus.Hidden);

            AvatarList.Main.RequestRefresh();
        }

        private static void ApplyRename(TextField nameField)
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;
            if (string.IsNullOrEmpty(prefabPath)) return;

            var newName = nameField.value?.Trim();
            var currentName = Path.GetFileNameWithoutExtension(prefabPath);

            if (string.IsNullOrEmpty(newName) || newName == currentName)
            {
                nameField.SetValueWithoutNotify(currentName);
                return;
            }

            var newPath = Path.Combine(Path.GetDirectoryName(prefabPath), newName + ".prefab").Replace("\\", "/");
            if (File.Exists(newPath))
            {
                EditorUtility.DisplayDialog(T7e.Get("Error"), T7e.Get("A file with this name already exists."), "OK");
                nameField.SetValueWithoutNotify(currentName);
                return;
            }

            // プレビュー画像はGUIDで管理しているため、リネームしてもそのまま引き継がれる
            PrefabManager.RenamePrefab(prefabPath, newName);

            // フォルダにこのPrefabしか入っていない場合は、フォルダ名も合わせる
            newPath = ManagedPrefabs.RenameItemFolder(newPath, newName);

            EAUploaderCore.selectedPrefabPath = newPath;
            nameField.SetValueWithoutNotify(newName);

            AvatarList.Main.RequestRefresh();
        }

        private static void UpdateThumbnail()
        {
            var image = root.Q<Image>("meta_thumbnail");
            if (image == null) return;

            image.image = PrefabPreview.GetPrefabPreview(EAUploaderCore.selectedPrefabPath);
            image.scaleMode = ScaleMode.ScaleToFit;
        }

        private static void RegenerateThumbnail()
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;
            var prefab = PrefabManager.GetPrefab(prefabPath);
            if (prefab == null) return;

            var newPreview = PrefabPreview.GeneratePreview(prefab);
            PrefabPreview.SavePrefabPreview(prefabPath, newPreview);
            PrefabManager.UpdateCachedPreview(prefabPath, newPreview);

            UpdateThumbnail();
            AvatarList.Main.RequestRefresh();
        }

        private static void ChangeThumbnail()
        {
            var path = EditorUtility.OpenFilePanel(T7e.Get("Select Thumbnail"), "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;

            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));

            var prefabPath = EAUploaderCore.selectedPrefabPath;
            PrefabPreview.SavePrefabPreview(prefabPath, texture);
            PrefabManager.UpdateCachedPreview(prefabPath, texture);

            UpdateThumbnail();
            AvatarList.Main.RequestRefresh();
        }

        /// <summary>
        /// 同じアイテムフォルダの中にVariantをもう1つ作る。
        /// 複製も取り込み元から見て同じVariantなので、元のPrefabには影響しない。
        /// </summary>
        private static void DuplicatePrefab()
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;
            if (string.IsNullOrEmpty(prefabPath)) return;

            var newPath = ManagedPrefabs.DuplicateVariant(prefabPath);
            if (newPath == null) return;

            PrefabManager.InvalidateCache();

            // 複製したものを編集対象にする
            EAUploaderCore.selectedPrefabPath = newPath;
            AvatarList.Main.RequestRefresh();
            EAUploader.RefreshCurrentScreen();
        }

        private static void DeletePrefab()
        {
            if (PrefabManager.ShowDeletePrefabDialog(EAUploaderCore.selectedPrefabPath))
            {
                EAUploader.Navigate(EAUploaderScreen.AvatarList);
            }
        }

        #endregion

        #region Edit menu

        private static void SetupExtensionButtons()
        {
            root.Q<ShadowButton>("import_extentions").clicked += ImportExtentionButtonClicked;
            root.Q<ShadowButton>("find_extentions").clicked += () =>
                Application.OpenURL("https://www.uslog.tech/eauploader-plug-ins");
        }

        private static void ImportExtentionButtonClicked()
        {
            var filePath = EditorUtility.OpenFilePanelWithFilters(
                T7e.Get("Import Extentions"), "",
                new[] { T7e.Get("Import a .unitypackage file."), "unitypackage", "All files", "*" });

            if (string.IsNullOrEmpty(filePath)) return;

            if (Path.GetExtension(filePath)?.ToLower() == ".unitypackage")
            {
                AssetDatabase.ImportPackage(filePath, false);
            }
            AssetDatabase.Refresh();
        }

        internal static void BuildEditor()
        {
            var avatarStatus = root.Q<VisualElement>("avatar_status");
            if (avatarStatus == null || EAUploaderCore.selectedPrefabPath == null) return;

            avatarStatus.Clear();

            var prefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
            if (prefab == null) return;

            var isVRM = Utility.CheckAvatarIsVRM(prefab);

            if (isVRM)
            {
                avatarStatus.Add(CreateStatusRow("warning", "warning",
                    T7e.Get("VRM Avatar needs to convert to VRChat Avatar")));

                var button = new Button(() =>
                {
#if HAS_VRM
                    if (VRMImporterWindow.ShowWindow(EAUploaderCore.selectedPrefabPath))
                    {
                        AvatarList.Main.RequestRefresh();
                    }
#endif
                })
                {
                    text = T7e.Get("Convert to VRChat Avatar")
                };
                avatarStatus.Add(button);
            }
            else
            {
                var hasDescriptor = Utility.CheckAvatarHasVRCAvatarDescriptor(prefab);
                var hasShader = ShaderChecker.CheckAvatarHasShader(prefab);

                if (!hasDescriptor)
                {
                    avatarStatus.Add(CreateStatusRow("warning", "warning", T7e.Get("No VRCAvatarDescriptor")));
                }

                if (!hasShader)
                {
                    avatarStatus.Add(CreateStatusRow("warning", "warning", T7e.Get("No Shader")));
                }

                if (hasDescriptor && hasShader)
                {
                    avatarStatus.Add(CreateStatusRow("success", "check_circle", T7e.Get("Ready to upload")));

                    var meta = prefab.GetComponent<EAUploaderMeta>();

                    if (meta == null)
                    {
                        meta = prefab.AddComponent<EAUploaderMeta>();
                        meta.type = EAUploaderMeta.PrefabType.VRChat;
                    }

                    if (meta.type == EAUploaderMeta.PrefabType.VRM)
                    {
                        avatarStatus.Add(CreateStatusRow(null, "info",
                            T7e.Get("This avatar is a converted VRM, Viewpoint Position may need to be adjusted.")));
                    }
                }
            }

            var editorRegistrations = new List<EditorRegistration>(EAUploaderEditorManager.GetRegisteredEditors());
            if (!isEditorInfoLoaded)
            {
                foreach (var editor in editorRegistrations)
                {
                    editorFoldouts[editor.EditorName] = false;
                }
                isEditorInfoLoaded = true;
            }

            var editorList = root.Q<VisualElement>("avatar_editor_list");
            editorList.Clear();

            foreach (var editor in editorRegistrations)
            {
                if (!editorFoldouts.ContainsKey(editor.EditorName))
                {
                    editorFoldouts[editor.EditorName] = false;
                }
                editorList.Add(new EditorItem(editor) { style = { marginBottom = 8 } });
            }
        }

        private static VisualElement CreateStatusRow(string ussClass, string icon, string text)
        {
            var row = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4,
                }
            };

            if (!string.IsNullOrEmpty(ussClass))
            {
                row.AddToClassList(ussClass);
            }

            row.Add(new MaterialIcon { icon = icon, style = { paddingRight = 4 } });
            row.Add(new Label(text));
            return row;
        }

        #endregion
    }

    internal class EditorItem : VisualElement
    {
        public EditorItem(EditorRegistration editor)
        {
            var buttonGroup = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                }
            };

            var item_button = new Button(() => EditorApplication.ExecuteMenuItem(editor.MenuName))
            {
                text = T7e.Get(editor.EditorName),
                style =
                {
                    flexGrow = 1,
                    borderBottomRightRadius = 0,
                    borderTopRightRadius = 0,
                    borderRightWidth = 1,
                },
            };

            buttonGroup.Add(item_button);

            var foldoutButton = new Button(() =>
            {
                Main.editorFoldouts[editor.EditorName] = !Main.editorFoldouts[editor.EditorName];
                Main.BuildEditor();
            })
            {
                style =
                {
                    width = 50,
                    justifyContent = Justify.Center,
                    paddingLeft = 2,
                    paddingRight = 2,
                    borderBottomLeftRadius = 0,
                    borderTopLeftRadius = 0,
                }
            };

            buttonGroup.Add(foldoutButton);

            Add(buttonGroup);

            var expandMore = new MaterialIcon { icon = "expand_more" };
            expandMore.style.fontSize = 20;
            var expandLess = new MaterialIcon { icon = "expand_less" };
            expandLess.style.fontSize = 20;

            if (editor.Requirement != null && !editor.Requirement(EAUploaderCore.selectedPrefabPath))
            {
                item_button.SetEnabled(false);
                var warning = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4,
                    }
                };
                warning.AddToClassList("warning");
                warning.Add(new MaterialIcon { icon = "warning", style = { paddingRight = 4 } });
                warning.Add(new Label(T7e.Get(editor.RequirementDescription)));
                Add(warning);
            }

            if (Main.editorFoldouts[editor.EditorName])
            {
                foldoutButton.Add(expandLess);
                var editorContent = new VisualElement()
                {
                    style =
                    {
                        paddingBottom = 8,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 8,
                    }
                };
                editorContent.Add(new Label(T7e.Get("Description: ") + T7e.Get(editor.Description)));
                editorContent.Add(new Label(T7e.Get("Version: ") + editor.Version));
                editorContent.Add(new Label(T7e.Get("Author: ") + editor.Author));

                if (!string.IsNullOrEmpty(editor.Url))
                {
                    var openButton = new Button(() =>
                    {
                        Application.OpenURL(editor.Url);
                    })
                    {
                        text = T7e.Get("Open the link")
                    };

                    openButton.AddToClassList("link");
                    editorContent.Add(openButton);
                }

                Add(editorContent);
            }
            else
            {
                foldoutButton.Add(expandMore);
            }
        }
    }
}
