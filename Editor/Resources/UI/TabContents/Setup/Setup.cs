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
    /// <summary>編集画面の表示状態。</summary>
    internal enum EditScreenMode
    {
        /// <summary>編集ツールとアイテム情報のペインを表示する</summary>
        Edit,
        /// <summary>アップロードのシートを表示する</summary>
        Upload,
    }

    /// <summary>
    /// 編集(セットアップ)画面。
    /// 3Dビューが画面いっぱいに広がり、その上に左の編集ツールと右のアイテム情報が浮かぶ。
    /// アップロードは画面の右側の大部分を覆うシートで行い、その間も左にアバターが見える。
    /// </summary>
    internal class Main
    {
        // ペインの開閉は、画面を作り直しても保つ
        private static bool editMenuOpen = true;
        private static bool sidePaneOpen = true;
        private static bool uploadMode;
        private static bool selectedIsAvatar;
        private static bool uploadFormBuilt;
        private static IVisualElementScheduledItem editMenuTransition;
        private static IVisualElementScheduledItem sidePaneTransition;
        private static IVisualElementScheduledItem uploadSheetTransition;

        private const string PaneClosedClass = "studio__shell--closed";
        private const string UploadingClass = "studio--uploading";
        // 閉じるアニメーション(styles.uss の .studio__shell の transition)が終わるまで待つ時間
        private const long PaneFadeMs = 220;
        // 開くとき、透明な状態を一度描画してからクラスを外す
        private const long PaneOpenDelayMs = 30;

        private static VisualElement root;
        internal static Preview preview;
        internal static Dictionary<string, bool> editorFoldouts = new Dictionary<string, bool>();
        private static bool isEditorInfoLoaded = false;

        private static readonly List<string> GENRE_CHOICES =
            new List<string> { "Avatar", "Cloth", "Accessory", "Other" };

        public static void ShowContent(VisualElement rootElement, EditScreenMode mode = EditScreenMode.Edit)
        {
            root = rootElement;
            uploadFormBuilt = false;

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

            BuildMetaPanel();
            BuildEditor();
            SetupExtensionButtons();
            SetupStudio(mode);

            SmoothScroll.Enable(root.Q<ScrollView>("avatar_editor_list"));
            SmoothScroll.Enable(root.Q<ScrollView>("meta_panel"));
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

            root.Q("side_pane_shell").style.display = DisplayStyle.None;
            root.Q("edit_menu_shell").style.display = DisplayStyle.None;
        }

        #region Navigation

        private static void SetupNavigation()
        {
            root.Q<ShadowButton>("back_to_list").clicked += () =>
                EAUploader.Navigate(EAUploaderScreen.AvatarList);
        }

        #endregion

        #region Studio (floating panes and the upload sheet)

        /// <summary>
        /// 左右のペインとアップロードのシートを組み立てる。
        /// </summary>
        private static void SetupStudio(EditScreenMode mode)
        {
            root.Q<Button>("collapse_edit_menu").clicked += () => SetEditMenuOpen(false);
            root.Q<Button>("open_edit_menu").clicked += () => SetEditMenuOpen(true);
            root.Q<Button>("collapse_side_pane").clicked += () => SetSidePaneOpen(false);
            root.Q<Button>("open_side_pane").clicked += () => SetSidePaneOpen(true);
            root.Q<ShadowButton>("open_upload").clicked += () => SetUploadMode(true);
            root.Q<Button>("open_upload_fab").clicked += () => SetUploadMode(true);
            root.Q<ShadowButton>("close_upload").clicked += () => SetUploadMode(false);

            UpdateHeader();

            // アップロードできるのはアバターだけ。前の画面のアップロード状態は持ち越さない
            uploadMode = false;
            UpdateUploadAvailability(IsSelectedAvatar());

            // 前回の開閉状態を、アニメーションなしで復元する
            uploadMode = mode == EditScreenMode.Upload && selectedIsAvatar;
            ApplyLayout(false);

            // ウィンドウの大きさが変わったら、シートが覆う幅も変わる
            root.Q("studio").RegisterCallback<GeometryChangedEvent>(_ => UpdatePreviewInset());
        }

        /// <summary>
        /// アップロード中は、シートが覆う幅をプレビューに伝えて、アバターを左側の空いた部分に寄せる。
        /// </summary>
        private static void UpdatePreviewInset()
        {
            if (preview == null) return;

            var studio = root.Q("studio");
            var width = studio.layout.width;
            if (float.IsNaN(width) || width <= 0f) return;

            // styles.uss の .studio__shell--sheet(右16px、幅62%、最小600px)と同じ計算
            var sheetWidth = Mathf.Max(width * 0.62f, 600f);
            preview.SetRightInset(uploadMode ? sheetWidth + 16f : 0f);
        }

        /// <summary>
        /// ヘッダーに、開いているアイテムの名前とジャンルを出す。
        /// </summary>
        private static void UpdateHeader()
        {
            var path = EAUploaderCore.selectedPrefabPath;
            var info = PrefabManager.GetPrefabInfo(path);
            var genre = PrefabManager.GetPrefabGenre(path);
            root.Q<Label>("studio_item_name").text = info != null ? info.Name : Path.GetFileNameWithoutExtension(path);
            root.Q<Label>("studio_genre_badge").text = T7e.Get(genre.ToString());
        }

        /// <summary>
        /// アップロードのボタンを、アイテムの種類にあわせて出し分ける。
        /// </summary>
        private static void UpdateUploadAvailability(bool isAvatar)
        {
            selectedIsAvatar = isAvatar;
            root.Q("side_pane_footer").style.display = isAvatar ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isAvatar && uploadMode)
            {
                SetUploadMode(false);
                return;
            }

            UpdateRails();
        }

        private static void SetEditMenuOpen(bool open)
        {
            editMenuOpen = open;
            ApplyLayout(true);
        }

        private static void SetSidePaneOpen(bool open)
        {
            sidePaneOpen = open;
            ApplyLayout(true);
        }

        private static void SetUploadMode(bool upload)
        {
            uploadMode = upload && selectedIsAvatar;
            ApplyLayout(true);
        }

        /// <summary>
        /// 現在の状態にあわせて、ペインとシートの表示を切り替える。
        /// アップロード中は編集用のペインを下げ、3Dビューをシートの左側に寄せる。
        /// </summary>
        private static void ApplyLayout(bool animate)
        {
            root.Q("studio").EnableInClassList(UploadingClass, uploadMode);
            root.Q<Label>("studio_title").text = T7e.Get(uploadMode ? "Upload" : "Edit");

            if (uploadMode && !uploadFormBuilt)
            {
                uploadFormBuilt = true;
                Upload.UploadForm.ShowContent(root.Q("upload_form"));
                LanguageUtility.Localization(root.Q("upload_form"));
            }

            editMenuTransition = AnimatePane("edit_menu_shell", !uploadMode && editMenuOpen, editMenuTransition, animate);
            sidePaneTransition = AnimatePane("side_pane_shell", !uploadMode && sidePaneOpen, sidePaneTransition, animate);
            uploadSheetTransition = AnimatePane("upload_sheet_shell", uploadMode, uploadSheetTransition, animate);

            UpdateRails();
            UpdatePreviewInset();

            // ペインの出入りと3Dの移動の間、エディターの更新を落とさない(アニメーションの時間より少し長く)
            if (animate) EAUploader.RequestSmoothFrames(0.5);
        }

        /// <summary>
        /// 閉じたペインを開き直すためのボタン。アップロード中はどれも出さない。
        /// </summary>
        private static void UpdateRails()
        {
            root.Q("open_edit_menu").EnableInClassList("hidden", uploadMode || editMenuOpen);
            root.Q("open_side_pane").EnableInClassList("hidden", uploadMode || sidePaneOpen);
            root.Q("open_upload_fab").EnableInClassList("hidden", uploadMode || sidePaneOpen || !selectedIsAvatar);
        }

        /// <summary>
        /// ペインやシートを開閉する。閉じるときは薄くなりながら端へ寄ってから隠し、開くときはその逆を行う。
        /// 透明なものがクリックを受けないよう、消えきったあとに hidden で取り除く。
        /// </summary>
        private static IVisualElementScheduledItem AnimatePane(
            string shellName, bool open, IVisualElementScheduledItem pending, bool animate)
        {
            pending?.Pause();

            var shell = root.Q(shellName);

            if (!animate)
            {
                shell.EnableInClassList("hidden", !open);
                shell.EnableInClassList(PaneClosedClass, !open);
                return null;
            }

            if (open)
            {
                shell.EnableInClassList("hidden", false);
                pending = shell.schedule.Execute(() => shell.RemoveFromClassList(PaneClosedClass));
                pending.StartingIn(PaneOpenDelayMs);
            }
            else
            {
                shell.AddToClassList(PaneClosedClass);
                pending = shell.schedule.Execute(() => shell.AddToClassList("hidden"));
                pending.StartingIn(PaneFadeMs);
            }

            return pending;
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

            // サムネイルは VRChat のアバターサムネイルと同じ 4:3 で、枠いっぱいに表示する
            AspectRatioFitter.Keep(root.Q<Image>("meta_thumbnail"), 0.75f);
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
            var genreDropdown = root.Q<DropdownFieldPro>("meta_genre");
            genreDropdown.choices = new List<string>(GENRE_CHOICES);
            genreDropdown.SetValueWithoutNotify(meta.genre.ToString());
            IVisualElementScheduledItem pendingGenreSave = null;
            genreDropdown.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse<EAUploaderMeta.PrefabGenre>(evt.newValue, out var genre)) return;

                // まずメモリ上のメタとUIだけを即座に更新し、重たいアセット書き出しはアニメーション完了後に遅らせる
                var currentPrefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
                var currentMeta = currentPrefab != null ? Utility.GetEAUploaderMeta(currentPrefab) : null;
                if (currentMeta != null)
                {
                    currentMeta.genre = genre;
                    currentMeta.genreConfirmed = true;
                }

                UpdateUploadAvailability(genre == EAUploaderMeta.PrefabGenre.Avatar);
                UpdateHeader();

                pendingGenreSave?.Pause();
                pendingGenreSave = genreDropdown.schedule.Execute(() =>
                {
                    PrefabManager.SetPrefabGenre(EAUploaderCore.selectedPrefabPath, genre);
                    AvatarList.Main.RequestRefresh();
                });
                pendingGenreSave.StartingIn(220);
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

            SetupMetaScrollShadows();
        }

        private const float SCROLL_SHADOW_RAMP_PX = 16f;

        /// <summary>
        /// アイテム情報のスクロール位置に応じて、上下の境界に影を出す。
        /// </summary>
        private static void SetupMetaScrollShadows()
        {
            ScrollShadows.Attach(root.Q<ScrollView>("meta_panel"),
                root.Q<Shadow>("meta_scroll_shadow_top"), root.Q<Shadow>("meta_scroll_shadow_bottom"));
        }

        #region Tags

        private static VisualElement tagDropdown;
        private static VisualElement tagDropdownControl;
        private static VisualElement tagDropdownHost;
        private static bool tagDropdownOpen;
        private static bool tagDropdownAnimationPending;
        private static IVisualElementScheduledItem tagDropdownReveal;

        /// <summary>
        /// タグの追加欄と、付いているタグの一覧を組み立てる。
        /// 入力欄で既存タグを絞り込み、候補を選ぶか新しいタグを追加する。
        /// </summary>
        private static void BuildTagSection()
        {
            CloseTagDropdown();
            tagDropdown = root.Q("tag_suggestions_section");
            tagDropdownControl = root.Q("tag_control");
            var input = root.Q<TextFieldPro>("tag_input");
            input.maxLength = -1;
            input.RegisterCallback<FocusInEvent>(_ =>
            {
                if (!input.ClassListContains("tag-editor__refocusing")) OpenTagDropdown();
            });
            input.RegisterCallback<PointerDownEvent>(_ => OpenTagDropdown());
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!string.IsNullOrEmpty(Input.compositionString)) return;
                if (evt.keyCode == KeyCode.Escape)
                {
                    CloseTagDropdown();
                    evt.StopPropagation();
                    evt.PreventDefault();
                    return;
                }
                if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
                {
                    OpenTagDropdown();
                    FocusTagOption(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                    evt.StopPropagation();
                    evt.PreventDefault();
                    return;
                }
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (tagDropdownOpen) AddTagFromInput();
                    else OpenTagDropdown();
                    evt.StopPropagation();
                    evt.PreventDefault();
                }
            }, TrickleDown.TrickleDown);
            input.RegisterValueChangedCallback(_ =>
            {
                if (!input.ClassListContains("tag-editor__refocusing")) OpenTagDropdown();
                RefreshInputSuggestions();
            });

            tagDropdown.Q<Button>("add_tag").clicked += AddTagFromInput;
            tagDropdown.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    RefocusTagInput();
                    CloseTagDropdown();
                }
                else if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
                {
                    FocusTagOption(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                }
                else return;
                evt.StopPropagation();
                evt.PreventDefault();
            });
            tagDropdownControl.RegisterCallback<FocusOutEvent>(_ => ScheduleTagDropdownFocusCheck());
            tagDropdown.RegisterCallback<FocusOutEvent>(_ => ScheduleTagDropdownFocusCheck());
            tagDropdownControl.RegisterCallback<DetachFromPanelEvent>(_ => CloseTagDropdown());
            tagDropdown.RegisterCallback<GeometryChangedEvent>(_ => AnimateTagDropdown());
            root.Q<ScrollView>("meta_panel").verticalScroller.valueChanged += _ => CloseTagDropdown();

            RefreshTagSection();
        }

        private static void OpenTagDropdown()
        {
            if (tagDropdownOpen || tagDropdownControl?.panel == null) return;
            tagDropdownHost = tagDropdownControl;
            while (tagDropdownHost.parent != null && tagDropdownHost.parent != tagDropdownControl.panel.visualTree)
                tagDropdownHost = tagDropdownHost.parent;

            tagDropdownOpen = true;
            tagDropdownAnimationPending = true;
            tagDropdown.AddToClassList("tag-editor__dropdown-shell--preparing");
            tagDropdown.style.opacity = 0f;
            tagDropdown.style.translate = new Translate(0f, 0f);
            tagDropdownControl.AddToClassList("tag-editor__control--open");
            tagDropdownHost.Add(tagDropdown);
            tagDropdown.RemoveFromClassList("hidden");
            tagDropdownHost.RegisterCallback<PointerDownEvent>(OnTagOutsidePointerDown, TrickleDown.TrickleDown);
            tagDropdownHost.RegisterCallback<GeometryChangedEvent>(OnTagDropdownHostGeometryChanged);
            RefreshInputSuggestions();
            PositionTagDropdown();
            tagDropdownReveal = tagDropdown.schedule.Execute(AnimateTagDropdown);
        }

        private static void CloseTagDropdown()
        {
            if (!tagDropdownOpen) return;
            tagDropdownOpen = false;
            tagDropdownAnimationPending = false;
            tagDropdownReveal?.Pause();
            tagDropdownReveal = null;
            tagDropdownHost?.UnregisterCallback<PointerDownEvent>(OnTagOutsidePointerDown, TrickleDown.TrickleDown);
            tagDropdownHost?.UnregisterCallback<GeometryChangedEvent>(OnTagDropdownHostGeometryChanged);
            tagDropdown.AddToClassList("hidden");
            tagDropdownControl.RemoveFromClassList("tag-editor__control--open");
            if (tagDropdownControl.parent != null) tagDropdownControl.parent.Add(tagDropdown);
            else tagDropdown.RemoveFromHierarchy();
            tagDropdownHost = null;
        }

        private static void AnimateTagDropdown()
        {
            var opensUpwards = PositionTagDropdown();
            if (!tagDropdownOpen || !tagDropdownAnimationPending || tagDropdown.layout.width <= 0f || tagDropdown.layout.height <= 0f) return;

            tagDropdownAnimationPending = false;
            tagDropdownReveal?.Pause();
            var dropdown = tagDropdown;
            dropdown.style.translate = new Translate(0f, opensUpwards ? DropdownFieldPro.SlideDistance : -DropdownFieldPro.SlideDistance);
            tagDropdownReveal = dropdown.schedule.Execute(() =>
            {
                if (!tagDropdownOpen || tagDropdown != dropdown) return;
                dropdown.RemoveFromClassList("tag-editor__dropdown-shell--preparing");
                tagDropdownReveal = dropdown.schedule.Execute(() =>
                {
                    if (!tagDropdownOpen || tagDropdown != dropdown) return;
                    dropdown.style.opacity = 1f;
                    dropdown.style.translate = new Translate(0f, 0f);
                    tagDropdownReveal = null;
                });
            });
        }

        private static void OnTagOutsidePointerDown(PointerDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target != null && !tagDropdownControl.Contains(target) && !tagDropdown.Contains(target))
                CloseTagDropdown();
        }

        private static void OnTagDropdownHostGeometryChanged(GeometryChangedEvent evt) => CloseTagDropdown();

        private static void ScheduleTagDropdownFocusCheck()
        {
            tagDropdownControl.schedule.Execute(() =>
            {
                if (!tagDropdownOpen) return;
                var focused = tagDropdownControl.panel?.focusController.focusedElement as VisualElement;
                if (focused == null || (!tagDropdownControl.Contains(focused) && !tagDropdown.Contains(focused)))
                    CloseTagDropdown();
            });
        }

        private static void RefocusTagInput()
        {
            var input = root.Q<TextFieldPro>("tag_input");
            input.AddToClassList("tag-editor__refocusing");
            input.Focus();
            input.schedule.Execute(() => input.RemoveFromClassList("tag-editor__refocusing"));
        }

        private static bool PositionTagDropdown()
        {
            if (!tagDropdownOpen || tagDropdownHost == null) return false;
            var field = tagDropdownControl.worldBound;
            var position = tagDropdownHost.WorldToLocal(field.position);
            var width = Mathf.Min(field.width, tagDropdownHost.layout.width - 8f);
            tagDropdown.style.width = width;
            tagDropdown.style.left = Mathf.Clamp(position.x, 4f, Mathf.Max(4f, tagDropdownHost.layout.width - width - 4f));
            var height = tagDropdown.layout.height;
            if (float.IsNaN(height)) height = 0f;
            var below = position.y + field.height + 4f;
            var top = below + height <= tagDropdownHost.layout.height - 4f
                ? below : Mathf.Max(4f, position.y - height - 4f);
            tagDropdown.style.top = top;
            return top < position.y;
        }

        private static void FocusTagOption(int direction)
        {
            var list = tagDropdown.Q<ScrollView>("tag_input_suggestions");
            var options = list.Children().OfType<Button>().ToList();
            var create = tagDropdown.Q<Button>("add_tag");
            if (create.enabledSelf && !create.ClassListContains("hidden")) options.Add(create);
            if (options.Count == 0) return;
            var focused = tagDropdown.panel?.focusController.focusedElement;
            var current = options.FindIndex(option => option == focused);
            var next = current < 0 ? (direction > 0 ? 0 : options.Count - 1)
                : (current + direction + options.Count) % options.Count;
            options[next].Focus();
            if (options[next] != create) list.ScrollTo(options[next]);
        }

        private static void AddTagFromInput()
        {
            var input = root.Q<TextFieldPro>("tag_input");
            if (input == null) return;

            AddTag(input.GetValue());
        }

        private static void AddTag(string tag)
        {
            if (tag != null && tag.Trim().Length > TagManager.MAX_TAG_LENGTH) return;
            var normalized = TagManager.Normalize(tag);
            if (normalized == null || TagManager.Contains(
                TagManager.GetTags(EAUploaderCore.selectedPrefabPath), normalized)) return;

            var added = TagManager.AddTagToPrefab(EAUploaderCore.selectedPrefabPath, tag);
            if (!added) return;

            var input = root.Q<TextFieldPro>("tag_input");
            input.SetValueWithoutNotify(string.Empty);
            input.Reset();
            RefocusTagInput();
            CloseTagDropdown();
            RefreshTagSection();

            AvatarList.Main.RequestRefresh();
        }

        /// <summary>
        /// 入力中の文字に一致する既存タグを候補として出す。
        /// </summary>
        private static void RefreshInputSuggestions()
        {
            var container = tagDropdown?.Q<ScrollView>("tag_input_suggestions");
            var input = root.Q<TextFieldPro>("tag_input");
            if (container == null || input == null) return;

            container.Clear();

            var attached = TagManager.GetTags(EAUploaderCore.selectedPrefabPath);
            var query = input.GetValue()?.Trim();
            if (string.IsNullOrEmpty(query)) query = null;
            var tooLong = query != null && query.Length > TagManager.MAX_TAG_LENGTH;
            var duplicate = TagManager.Contains(attached, query);
            var available = TagManager.GetAllTags()
                .Where(t => !TagManager.Contains(attached, t))
                .ToList();
            var matches = available
                .Where(t => query == null || t.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            var canCreate = query != null && !tooLong && !duplicate && !TagManager.Contains(available, query);
            var create = tagDropdown.Q<Button>("add_tag");
            create.SetEnabled(canCreate);
            create.EnableInClassList("hidden", !canCreate);
            tagDropdown.Q("tag_create_separator").EnableInClassList("hidden", !canCreate || matches.Count == 0);
            tagDropdown.Q<Label>("tag_create_name").text = query ?? string.Empty;
            var hint = tagDropdown.Q<Label>("tag_input_hint");
            hint.text = tooLong
                ? string.Format(T7e.Get("Tag names can be up to {0} characters."), TagManager.MAX_TAG_LENGTH)
                : T7e.Get(duplicate ? "This tag is already added."
                    : query == null ? "Type to add a new tag." : "Press Enter to add a tag.");
            hint.EnableInClassList("hidden", !tooLong && !duplicate && matches.Count > 0);
            container.EnableInClassList("hidden", matches.Count == 0);

            foreach (var tag in matches)
            {
                // タグ名はユーザーの入力なので翻訳対象から外す
                var button = new Button(() => AddTag(tag)) { tooltip = tag };
                button.AddToClassList("tag-suggestion");
                var label = new Label(tag);
                label.AddToClassList("no-translate");
                button.Add(label);
                container.Add(button);
            }
        }

        /// <summary>
        /// 付いているタグの表示と、追加できる候補を作り直す。
        /// </summary>
        private static void RefreshTagSection()
        {
            var prefabPath = EAUploaderCore.selectedPrefabPath;

            var chips = root.Q<VisualElement>("tag_chips");
            var input = root.Q<TextFieldPro>("tag_input");
            foreach (var chip in chips.Children().Where(child => child.ClassListContains("tag-chip--removable")).ToList())
                chip.RemoveFromHierarchy();

            var tags = TagManager.GetTags(prefabPath);
            foreach (var tag in tags)
            {
                chips.Insert(chips.IndexOf(input), CreateTagChip(tag));
            }

            input.placeholder = tags.Count > 0 ? string.Empty : T7e.Get("Search or create a tag");

            RefreshInputSuggestions();
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
            remove.tooltip = T7e.Get("Remove tag");
            remove.Add(new MaterialIcon { icon = "close" });
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

            var summary = new VisualElement();
            summary.AddToClassList("meta-details__summary");
            if (info != null)
            {
                summary.Add(CreateMetaValue("Last Updated", info.LastModified.ToString("yyyy/MM/dd HH:mm")));
            }

            if (Utility.CheckAvatarHasVRCAvatarDescriptor(prefab))
            {
                summary.Add(CreateMetaValue("Avatar Height", Utility.GetAvatarHeight(prefab).ToString("0.##") + " m"));
            }
            if (summary.childCount > 0) details.Add(summary);

            var pipelineManager = Utility.GetPipelineManager(prefab);
            if (pipelineManager != null && !string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                var blueprintId = pipelineManager.blueprintId;
                const string maskedBlueprintId = "••••••••••••••••";
                var blueprintRow = CreateMetaValue("Blueprint ID", maskedBlueprintId);
                blueprintRow.AddToClassList("meta-details__reference");
                var blueprintValueRow = blueprintRow[1];
                var blueprintValue = blueprintValueRow.Q<Label>(className: "meta-details__value");
                blueprintValue.tooltip = string.Empty;
                var blueprintVisible = false;
                var visibilityIcon = new MaterialIcon("visibility");
                var visibilityButton = new Button { tooltip = T7e.Get("Show Blueprint ID") };
                visibilityButton.AddToClassList("meta-details__icon-button");
                visibilityButton.Add(visibilityIcon);
                visibilityButton.clicked += () =>
                {
                    blueprintVisible = !blueprintVisible;
                    blueprintValue.text = blueprintVisible ? blueprintId : maskedBlueprintId;
                    blueprintValue.tooltip = blueprintVisible ? blueprintId : string.Empty;
                    visibilityIcon.icon = blueprintVisible ? "visibility_off" : "visibility";
                    visibilityButton.tooltip = T7e.Get(blueprintVisible ? "Hide Blueprint ID" : "Show Blueprint ID");
                };
                blueprintValueRow.Add(visibilityButton);
                blueprintValueRow.Add(CreateMetaCopyButton(blueprintId, "Copy Blueprint ID"));
                details.Add(blueprintRow);
            }

            var path = !string.IsNullOrEmpty(info?.Path) ? info.Path : AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(path))
            {
                var pathRow = CreateMetaValue("Asset Path", path);
                pathRow.AddToClassList("meta-details__reference");
                pathRow.AddToClassList("meta-details__path");
                pathRow[1].Add(CreateMetaCopyButton(path, "Copy Path"));
                var reveal = new Button(() => EditorGUIUtility.PingObject(prefab))
                {
                    tooltip = T7e.Get("Show in Project")
                };
                reveal.AddToClassList("meta-details__icon-button");
                reveal.Add(new MaterialIcon("folder_open"));
                pathRow[1].Add(reveal);
                details.Add(pathRow);
            }
        }

        private static VisualElement CreateMetaValue(string title, string value)
        {
            var group = new VisualElement();
            group.AddToClassList("meta-details__group");
            var header = new VisualElement();
            header.AddToClassList("meta-details__header");
            var caption = new Label(T7e.Get(title));
            caption.AddToClassList("meta-details__caption");
            header.Add(caption);
            group.Add(header);
            var label = new Label(value) { tooltip = value };
            label.AddToClassList("meta-details__value");
            label.AddToClassList("no-translate");
            var valueRow = new VisualElement();
            valueRow.AddToClassList("meta-details__value-row");
            valueRow.Add(label);
            group.Add(valueRow);
            return group;
        }

        private static Button CreateMetaCopyButton(string value, string title)
        {
            var icon = new MaterialIcon("content_copy");
            var button = new Button { tooltip = T7e.Get(title) };
            button.AddToClassList("meta-details__icon-button");
            button.Add(icon);
            IVisualElementScheduledItem reset = null;
            button.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = value;
                EAUploader.toasts?.Show(T7e.Get(title == "Copy Blueprint ID" ? "Blueprint ID copied" : "Path copied"),
                    type: ToastType.Success, durationSeconds: 3, key: "clipboard:" + title);
                reset?.Pause();
                icon.icon = "check";
                button.tooltip = T7e.Get("Copied");
                reset = button.schedule.Execute(() =>
                {
                    icon.icon = "content_copy";
                    button.tooltip = T7e.Get(title);
                }).StartingIn(1200);
            };
            return button;
        }

        private static void SetStatus(EAUploaderMeta.PrefabStatus status)
        {
            if (!PrefabManager.SetPrefabStatus(EAUploaderCore.selectedPrefabPath, status)) return;

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
            PrefabManager.ShowDeletePrefabDialog(EAUploaderCore.selectedPrefabPath, deleted =>
            {
                if (deleted || string.IsNullOrEmpty(EAUploaderCore.selectedPrefabPath))
                    EAUploader.Navigate(EAUploaderScreen.AvatarList);
            });
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

            // 色付きのバナーとして表示する(styles.uss の .status-row)
            row.AddToClassList("status-row");
            row.AddToClassList(string.IsNullOrEmpty(ussClass) ? "status-row--info" : "status-row--" + ussClass);
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

    /// <summary>
    /// 編集ツール1つ分のカード。名前(押すとツールを開く)と開閉ボタンが並び、開くとその下に説明などが出る。
    /// ボタンの形のカードの下から、詳細のシートが高さをアニメーションさせて出てくる。一覧を作り直さないので、他のカードは動かない。
    /// </summary>
    internal class EditorItem : VisualElement
    {
        private const float AnimationSeconds = 0.4f;
        private const double OpenCloseSeconds = 0.22;
        // 閉じているときも残す、ボタンの下に潜る高さ(styles.uss の .edit-tool__details-wrap)
        private const float TuckedHeight = 10f;

        private readonly VisualElement card;
        private readonly VisualElement detailsWrap;
        private readonly VisualElement details;
        private readonly string editorName;
        private bool isOpen;
        private bool measured;
        private float currentHeight = TuckedHeight;
        private float currentOpacity;
        private IVisualElementScheduledItem animation;

        public EditorItem(EditorRegistration editor)
        {
            editorName = editor.EditorName;
            isOpen = Main.editorFoldouts[editor.EditorName];

            card = new VisualElement();
            card.AddToClassList("edit-tool__card");

            // 下の詳細シートを先に置き、上から重ねる順(column-reverse)で並べる。
            // ボタン側のカードが手前に描かれるので、その影がシートの上端に落ちる
            style.flexDirection = FlexDirection.ColumnReverse;
            EnableInClassList("edit-tool--open", isOpen);

            var group = new VisualElement();
            group.AddToClassList("edit-tool__group");

            var item_button = new Button(() => EditorApplication.ExecuteMenuItem(editor.MenuName));
            item_button.AddToClassList("edit-tool__main");
            var nameLabel = new Label(T7e.Get(editor.EditorName)) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("edit-tool__name");
            item_button.Add(nameLabel);
            group.Add(item_button);

            var foldoutButton = new Button(Toggle);
            foldoutButton.AddToClassList("edit-tool__toggle");
            foldoutButton.Add(new MaterialIcon { icon = "expand_more", pickingMode = PickingMode.Ignore });
            group.Add(foldoutButton);
            card.Add(group);

            // 2つのボタンは枠線を1本で共有していて、開閉ボタンの枠が手前に描かれる。
            // 名前側にカーソルがあるときは、共有している線も青くして、枠が途切れて見えないようにする
            item_button.RegisterCallback<PointerEnterEvent>(_ => foldoutButton.AddToClassList("edit-tool__toggle--seam"));
            item_button.RegisterCallback<PointerLeaveEvent>(_ => foldoutButton.RemoveFromClassList("edit-tool__toggle--seam"));

            if (editor.Requirement != null && !editor.Requirement(EAUploaderCore.selectedPrefabPath))
            {
                item_button.SetEnabled(false);
                var warning = new VisualElement();
                warning.AddToClassList("edit-tool__warning");
                warning.AddToClassList("warning");
                warning.Add(new MaterialIcon { icon = "warning", style = { paddingRight = 4 } });
                warning.Add(new Label(T7e.Get(editor.RequirementDescription)));
                card.Add(warning);
            }

            // 詳細は、高さを0から中身の高さまで動かして開く
            detailsWrap = new VisualElement();
            detailsWrap.AddToClassList("edit-tool__details-wrap");

            details = new VisualElement();
            details.AddToClassList("edit-tool__details");
            details.Add(new Label(T7e.Get("Description: ") + T7e.Get(editor.Description)));
            details.Add(new Label(T7e.Get("Version: ") + editor.Version));
            details.Add(new Label(T7e.Get("Author: ") + editor.Author));

            if (!string.IsNullOrEmpty(editor.Url))
            {
                var openButton = new Button(() => Application.OpenURL(editor.Url))
                {
                    text = T7e.Get("Open the link")
                };
                openButton.AddToClassList("link");
                details.Add(openButton);
            }

            detailsWrap.Add(details);
            Add(detailsWrap);

            // 中身の高さが決まった(または文字の折り返しが変わった)ら、開いているときは高さを合わせる。
            // 作った直後に初めて高さが決まるときだけアニメーションしない。そのあとの変化でアニメーションを切ると、
            // 開く途中でレイアウトが更新されたときに、一瞬で開いてしまう
            details.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (details.layout.height <= 0f) return;

                var first = !measured;
                measured = true;
                if (isOpen) ApplyHeight(!first);
            });

            var shadow = new Shadow
            {
                shadowBlur = 10,
                shadowOffsetY = 3,
                shadowOpacity = 0.14f,
            };
            shadow.Add(card);
            Add(shadow);
        }

        private void Toggle()
        {
            isOpen = !isOpen;
            Main.editorFoldouts[editorName] = isOpen;
            EnableInClassList("edit-tool--open", isOpen);
            ApplyHeight(true);

            // 高さの変化の間、エディターの更新を落とさない
            EAUploader.RequestSmoothFrames(AnimationSeconds);
        }

        private void ApplyHeight(bool animate)
        {
            var targetHeight = isOpen ? details.layout.height : TuckedHeight;
            var targetOpacity = isOpen ? 1f : 0f;
            if (float.IsNaN(targetHeight)) return;

            animation?.Pause();
            animation = null;

            if (!animate)
            {
                SetSheet(targetHeight, targetOpacity);
                return;
            }

            // 高さと透明度は、USS の transition ではなくここで動かす。transition は、エディターが止まっていた直後だと
            // 開始が遅れて、最初の1フレームで大きく進んでしまう。最初の更新の時刻を開始時刻にして、時間で進める
            var fromHeight = currentHeight;
            var fromOpacity = currentOpacity;
            var start = -1.0;
            IVisualElementScheduledItem item = null;
            item = detailsWrap.schedule.Execute(() =>
            {
                var now = EditorApplication.timeSinceStartup;
                if (start < 0) start = now;

                var t = Mathf.Clamp01((float)((now - start) / OpenCloseSeconds));
                var eased = 1f - (1f - t) * (1f - t) * (1f - t);
                SetSheet(Mathf.Lerp(fromHeight, targetHeight, eased), Mathf.Lerp(fromOpacity, targetOpacity, eased));

                if (t >= 1f) item.Pause();
            }).Every(1);
            animation = item;
        }

        private void SetSheet(float height, float opacity)
        {
            currentHeight = height;
            currentOpacity = opacity;
            detailsWrap.style.height = height;
            detailsWrap.style.opacity = opacity;
        }
    }
}
