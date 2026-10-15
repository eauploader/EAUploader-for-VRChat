using EAUploader.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using EAUploader.UI.Modals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace EAUploader.UI.AvatarList
{
    /// <summary>
    /// 起動時に表示される一覧画面。
    /// 検索とカテゴリ絞り込みで、アバター/衣装/アイテムをカードのグリッドとして並べる。
    /// </summary>
    internal class Main
    {
        private static VisualElement root;
        private static VisualElement cardGrid;

        // 画面を作り直しても選択状態を保つため、絞り込み条件は静的に保持する
        private static SortOrder sortOrder = SortOrder.LastModifiedDescending;
        private static Category selectedCategory = Category.Avatar;
        private static bool showHidden = false;
        private static string searchQuery = string.Empty;

        // タグによる絞り込み。選択したタグをすべて持つアイテムだけを表示する(AND条件)。
        private static readonly List<string> selectedTags = new List<string>();

        private static CancellationTokenSource cts;

        // カードの表示サイズ(幅px)。スライダーで変更でき、EditorPrefsに保存する。
        private const string CARD_WIDTH_PREF_KEY = "EAUploader.CardWidth";
        internal const float DEFAULT_CARD_WIDTH = 360f;
        private const float MIN_CARD_WIDTH = 160f;
        private const float MAX_CARD_WIDTH = 600f;

        // サムネイル領域の縦横比(幅に対する高さ)。
        // プレビュー画像の解像度(1200x900)にあわせる。
        internal const float THUMBNAIL_RATIO = 900f / 1200f;

        internal static float CardWidth
        {
            get => Mathf.Clamp(
                EditorPrefs.GetFloat(CARD_WIDTH_PREF_KEY, DEFAULT_CARD_WIDTH),
                MIN_CARD_WIDTH, MAX_CARD_WIDTH);
            set => EditorPrefs.SetFloat(CARD_WIDTH_PREF_KEY, Mathf.Clamp(value, MIN_CARD_WIDTH, MAX_CARD_WIDTH));
        }

        public enum SortOrder
        {
            LastModifiedDescending,
            LastModifiedAscending,
            NameDescending,
            NameAscending
        }

        /// <summary>
        /// 一覧の表示カテゴリ。
        /// 内部のジャンルは Avatar / Cloth / Accessory / Other の4種だが、
        /// 一覧では Accessory と Other をまとめて「アイテム・その他」として扱う。
        /// </summary>
        public enum Category
        {
            Avatar,
            Cloth,
            ItemsAndOthers
        }

        public static void ShowContent(VisualElement rootContainer)
        {
            root = rootContainer;

            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/AvatarList/AvatarList");
            visualTree.CloneTree(root);

            cardGrid = root.Q<VisualElement>("card_grid");

            BuildUI();
        }

        /// <summary>
        /// 一覧が表示されている場合だけ内容を更新する。
        /// インポート処理など、画面外から呼ばれることを想定している。
        /// </summary>
        internal static void RequestRefresh()
        {
            // rootは全画面で共有しているコンテナなので、一覧固有の要素が
            // まだパネルに載っているかどうかで表示中か判定する
            if (cardGrid?.panel == null) return;
            UpdateCards();
        }

        private static void BuildUI()
        {
            // 入力するそばから絞り込む。Enterと検索ボタンでも実行できる。
            // プレースホルダー表示中は GetValue() がnullを返すので、空文字として扱う。
            var searchField = root.Q<TextFieldPro>("searchQuery");
            if (!string.IsNullOrEmpty(searchQuery))
            {
                searchField.SetValueWithoutNotify(searchQuery);
                searchField.Reset();
            }
            searchField.RegisterValueChangedCallback(_ =>
            {
                searchQuery = searchField.GetValue() ?? string.Empty;
                UpdateCards();
            });
            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    searchQuery = searchField.GetValue() ?? string.Empty;
                    UpdateCards();
                }
            });

            root.Q<ShadowButton>("searchButton").clicked += () =>
            {
                searchQuery = searchField.GetValue() ?? string.Empty;
                UpdateCards();
            };

            root.Q<ShadowButton>("open_import").clicked += () => new ImportModal().Open();
            root.Q<ShadowButton>("open_tag_settings").clicked += () => new TagSettingsModal().Open();
            root.Q<ShadowButton>("open_tag_filter").clicked += () =>
                new TagFilterModal(selectedTags, () =>
                {
                    UpdateTagFilterCount();
                    UpdateCards();
                }).Open();
            UpdateTagFilterCount();
            root.Q<ShadowButton>("open_settings").clicked += () => new SettingsModal().Open();
            root.Q<ShadowButton>("exit_unity").clicked += ExitUnity;

            // カードサイズ
            var cardSizeSlider = new Slider(MIN_CARD_WIDTH, MAX_CARD_WIDTH) { value = CardWidth };
            cardSizeSlider.style.width = 120;
            cardSizeSlider.RegisterValueChangedCallback(evt =>
            {
                CardWidth = evt.newValue;
                ApplyCardWidth();
            });
            var cardSizeBar = root.Q<VisualElement>("card_size_bar");
            cardSizeBar.Clear();
            cardSizeBar.Add(cardSizeSlider);

            // カテゴリ選択
            var categoryDropdown = new DropdownField("", new List<string>
            {
                T7e.Get("Avatar"),
                T7e.Get("Cloth"),
                T7e.Get("Items and Others")
            }, (int)selectedCategory);
            categoryDropdown.RegisterValueChangedCallback(_ =>
            {
                selectedCategory = (Category)categoryDropdown.index;
                UpdateCards();
            });
            var categorybar = root.Q<VisualElement>("categorybar");
            categorybar.Clear();
            categorybar.Add(categoryDropdown);

            // 並び替え
            var sortDropdown = new DropdownField("", new List<string>
            {
                T7e.Get("Last Modified Descending"),
                T7e.Get("Last Modified Ascending"),
                T7e.Get("Name Descending"),
                T7e.Get("Name Ascending")
            }, (int)sortOrder);
            sortDropdown.RegisterValueChangedCallback(_ =>
            {
                sortOrder = (SortOrder)sortDropdown.index;
                UpdateCards();
            });
            var sortbar = root.Q<VisualElement>("sortbar");
            sortbar.Clear();
            sortbar.Add(sortDropdown);

            var showHiddenToggle = root.Q<Toggle>("show_hidden");
            showHiddenToggle.SetValueWithoutNotify(showHidden);
            showHiddenToggle.RegisterValueChangedCallback(evt =>
            {
                showHidden = evt.newValue;
                UpdateCards();
            });

            if (EAUploaderCore.HasVRM)
            {
                root.Q<VisualElement>("drop_model").Q<Label>("drop_model_label").text =
                    T7e.Get("Drop files(.unitypackage, .prefab, .vrm(ver. 0.x)) to import");
            }

            root.RegisterCallback<DragEnterEvent>(OnDragEnter);
            root.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);

            UpdateCards();
        }

        /// <summary>
        /// 絞り込みに使っているタグの数をボタンに出す。
        /// </summary>
        private static void UpdateTagFilterCount()
        {
            var label = root?.Q<Label>("tag_filter_count");
            if (label == null) return;

            label.text = "(" + selectedTags.Count + ")";
            label.EnableInClassList("hidden", selectedTags.Count == 0);
        }

        private static void ExitUnity()
        {
            if (EditorUtility.DisplayDialog(
                    T7e.Get("Confirm Exit"),
                    T7e.Get("Are you sure you want to exit Unity?"),
                    T7e.Get("Yes"), T7e.Get("No")))
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// 表示中のカードにスライダーの値を反映する。
        /// 並べ直しは不要なのでサイズだけ更新する。
        /// </summary>
        private static void ApplyCardWidth()
        {
            if (cardGrid == null) return;

            foreach (var child in cardGrid.Children())
            {
                if (child is AvatarCard card)
                {
                    card.ApplySize(CardWidth);
                }
            }
        }

        #region Drag & drop

        private static void OnDragEnter(DragEnterEvent _)
        {
            root.Q<VisualElement>("drop_model").EnableInClassList("hidden", false);
        }

        private static void OnDragLeave(DragLeaveEvent _)
        {
            root.Q<VisualElement>("drop_model").EnableInClassList("hidden", true);
        }

        private static void OnDragUpdate(DragUpdatedEvent _)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
        }

        private static void OnDragPerform(DragPerformEvent _)
        {
            root.Q<VisualElement>("drop_model").EnableInClassList("hidden", true);

            if (DragAndDrop.paths.Length > 0 && DragAndDrop.objectReferences.Length == 0)
            {
                foreach (string path in DragAndDrop.paths)
                {
                    var fileExtension = Path.GetExtension(path)?.ToLower();

                    if (fileExtension == ".prefab")
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
                    }
                    else if (fileExtension == ".unitypackage")
                    {
                        AssetDatabase.ImportPackage(path, false);
                    }
#if HAS_VRM
                    else if (fileExtension == ".vrm")
                    {
                        VRMImporter.ImportVRM(path);
                    }
#endif
                }
            }
            else
            {
                Debug.Log("Out of reach");
                foreach (string path in DragAndDrop.paths) Debug.Log("- " + path);
                foreach (Object obj in DragAndDrop.objectReferences) Debug.Log("- " + obj);
            }
        }

        #endregion

        #region Card list

        internal static void UpdateCards()
        {
            _ = UpdateCardsAsync();
        }

        private static async Task UpdateCardsAsync()
        {
            try
            {
                await RebuildCardsAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static async Task RebuildCardsAsync()
        {
            await Task.Yield();

            if (root == null || cardGrid?.panel == null) return;

            var items = GetFilteredPrefabs();

            cardGrid.Clear();

            var emptyLabel = root.Q<Label>("empty_label");
            emptyLabel?.EnableInClassList("hidden", items.Count > 0);

            cts?.Cancel();
            cts = new CancellationTokenSource();

            try
            {
                await AddCardsAsync(items, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 新しい検索で置き換えられた
            }
        }

        private static List<PrefabInfo> GetFilteredPrefabs()
        {
            var prefabs = PrefabManager.GetAllPrefabsIncludingHidden();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                // 名前だけでなくタグも検索対象にする
                prefabs = prefabs.Where(p =>
                        p.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0
                        || p.Tags?.Any(
                            t => t.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) == true)
                    .ToList();
            }

            if (!showHidden)
            {
                prefabs = prefabs.Where(p => p.Status != EAUploaderMeta.PrefabStatus.Hidden).ToList();
            }

            prefabs = prefabs.Where(p => MatchesCategory(p.Genre, selectedCategory)).ToList();

            // 選択したタグをすべて持つものだけを残す(AND条件)
            if (selectedTags.Count > 0)
            {
                prefabs = prefabs
                    .Where(p => selectedTags.All(t => TagManager.Contains(p.Tags, t)))
                    .ToList();
            }

            switch (sortOrder)
            {
                case SortOrder.LastModifiedDescending:
                    prefabs = prefabs.OrderByDescending(p => p.LastModified).ToList();
                    break;
                case SortOrder.LastModifiedAscending:
                    prefabs = prefabs.OrderBy(p => p.LastModified).ToList();
                    break;
                case SortOrder.NameDescending:
                    prefabs = prefabs.OrderByDescending(p => p.Name).ToList();
                    break;
                case SortOrder.NameAscending:
                    prefabs = prefabs.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    break;
            }

            // ピン留めは常に先頭へ
            return prefabs
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ToList();
        }

        private static bool MatchesCategory(EAUploaderMeta.PrefabGenre genre, Category category)
        {
            switch (category)
            {
                case Category.Avatar:
                    return genre == EAUploaderMeta.PrefabGenre.Avatar;
                case Category.Cloth:
                    return genre == EAUploaderMeta.PrefabGenre.Cloth;
                case Category.ItemsAndOthers:
                    return genre == EAUploaderMeta.PrefabGenre.Accessory
                        || genre == EAUploaderMeta.PrefabGenre.Other;
                default:
                    return false;
            }
        }

        private static async Task AddCardsAsync(List<PrefabInfo> items, CancellationToken token)
        {
            foreach (var prefab in items)
            {
                token.ThrowIfCancellationRequested();
                cardGrid.Add(new AvatarCard(prefab));
                await Task.Yield();
            }
        }

        #endregion
    }

    /// <summary>
    /// 一覧のカード1枚。
    /// サムネイル・タイトル・最終更新日時をまとめ、
    /// カーソルを乗せると編集/アップロード/削除の操作を表示する。
    /// </summary>
    internal class AvatarCard : VisualElement
    {
        private readonly PrefabInfo prefabInfo;

        public AvatarCard(PrefabInfo prefab)
        {
            prefabInfo = prefab;

            AddToClassList("avatar-card");

            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/AvatarList/AvatarCard");
            visualTree.CloneTree(this);

            var isAvatar = prefab.Genre == EAUploaderMeta.PrefabGenre.Avatar;

            // サムネイル
            var thumbnail = this.Q<Image>("thumbnail");
            thumbnail.image = prefab.Preview != null
                ? prefab.Preview
                : PrefabPreview.GetPrefabPreview(prefab.Path);
            thumbnail.scaleMode = ScaleMode.ScaleToFit;

            this.Q<Label>("title").text = prefab.Name;
            this.Q<Label>("last_modified").text = prefab.LastModified.ToString("yyyy/MM/dd HH:mm");

            this.Q<MaterialIcon>("pinned_badge")
                .EnableInClassList("hidden", prefab.Status != EAUploaderMeta.PrefabStatus.Pinned);
            this.Q<MaterialIcon>("hidden_badge")
                .EnableInClassList("hidden", prefab.Status != EAUploaderMeta.PrefabStatus.Hidden);

            ApplySize(Main.CardWidth);

            SetupTags(prefab);
            SetupWarning(prefab, isAvatar);
            SetupActions(prefab, isAvatar);
            SetupContextMenu(prefab);

            // カード本体のクリックで編集画面へ
            this.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                OpenSetup();
            });
        }

        /// <summary>
        /// カード幅を指定し、サムネイル領域の高さを比率にあわせて変える。
        /// </summary>
        internal void ApplySize(float width)
        {
            var thumbnailHeight = Mathf.Round(width * Main.THUMBNAIL_RATIO);

            style.width = width;

            var thumbnailArea = this.Q<VisualElement>("thumbnail_area");
            if (thumbnailArea != null) thumbnailArea.style.height = thumbnailHeight;

            var thumbnail = this.Q<Image>("thumbnail");
            if (thumbnail != null) thumbnail.style.height = thumbnailHeight;
        }

        /// <summary>
        /// 付けられているタグをカード下部に並べる。
        /// 幅が限られるので、多い場合は残りの件数だけを示す。
        /// </summary>
        private void SetupTags(PrefabInfo prefab)
        {
            const int MAX_VISIBLE_TAGS = 3;

            var container = this.Q<VisualElement>("card_tags");
            if (container == null) return;

            container.Clear();

            var tags = prefab.Tags;
            if (tags == null || tags.Count == 0)
            {
                container.EnableInClassList("hidden", true);
                return;
            }

            container.EnableInClassList("hidden", false);

            foreach (var tag in tags.Take(MAX_VISIBLE_TAGS))
            {
                // タグ名はユーザーの入力なので翻訳対象から外す
                var chip = new Label(tag);
                chip.AddToClassList("tag-chip");
                chip.AddToClassList("no-translate");
                container.Add(chip);
            }

            if (tags.Count > MAX_VISIBLE_TAGS)
            {
                var more = new Label("+" + (tags.Count - MAX_VISIBLE_TAGS));
                more.AddToClassList("tag-chip");
                more.AddToClassList("no-translate");
                container.Add(more);
            }
        }

        private void SetupWarning(PrefabInfo prefab, bool isAvatar)
        {
            if (!isAvatar) return;

            var go = PrefabManager.GetPrefab(prefab.Path);
            if (go == null) return;

            var hasDescriptor = Utility.CheckAvatarHasVRCAvatarDescriptor(go);
            var hasShader = ShaderChecker.CheckAvatarHasShader(go);
            var isVRM = Utility.CheckAvatarIsVRM(go);

            if (hasDescriptor && hasShader && !isVRM) return;

            var warning = this.Q<Label>("warning");
            warning.text = isVRM
                ? T7e.Get("Needs conversion")
                : T7e.Get("Can't be uploaded");
            warning.EnableInClassList("hidden", false);
        }

        private void SetupActions(PrefabInfo prefab, bool isAvatar)
        {
            var actions = this.Q<VisualElement>("card_actions");

            var editButton = this.Q<Button>("edit_button");
            var uploadButton = this.Q<Button>("upload_button");
            var deleteButton = this.Q<Button>("delete_button");

            editButton.clicked += OpenSetup;
            uploadButton.clicked += OpenUpload;
            deleteButton.clicked += Delete;

            // アップロードはアバターのみ
            uploadButton.style.display = isAvatar ? DisplayStyle.Flex : DisplayStyle.None;

            // 操作ボタンのクリックがカード本体(編集画面へ遷移)に伝わらないようにする
            actions.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            actions.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            RegisterCallback<MouseEnterEvent>(_ => actions.EnableInClassList("hidden", false));
            RegisterCallback<MouseLeaveEvent>(_ => actions.EnableInClassList("hidden", true));
        }

        private void SetupContextMenu(PrefabInfo prefab)
        {
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var isPinned = prefab.Status == EAUploaderMeta.PrefabStatus.Pinned;
                var isHidden = prefab.Status == EAUploaderMeta.PrefabStatus.Hidden;

                evt.menu.AppendAction(
                    isPinned ? T7e.Get("Unpin") : T7e.Get("Pin"),
                    _ =>
                    {
                        PrefabManager.PinPrefab(prefab.Path);
                        Main.UpdateCards();
                    });

                evt.menu.AppendAction(
                    isHidden ? T7e.Get("Show") : T7e.Get("Hide"),
                    _ =>
                    {
                        SetHidden(prefab.Path, !isHidden);
                        Main.UpdateCards();
                    });

                evt.menu.AppendSeparator();

                evt.menu.AppendAction(T7e.Get("Show in Project"), _ =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab.Path);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                });

                evt.menu.AppendAction(T7e.Get("Delete"), _ => Delete());
            }));
        }

        /// <summary>
        /// 一覧での表示/非表示を切り替える。
        /// </summary>
        internal static void SetHidden(string prefabPath, bool hidden)
        {
            var go = PrefabManager.GetPrefab(prefabPath);
            if (go == null) return;

            var meta = Utility.GetEAUploaderMeta(go);
            if (meta == null) return;

            var newStatus = hidden
                ? EAUploaderMeta.PrefabStatus.Hidden
                : EAUploaderMeta.PrefabStatus.Show;

            if (meta.status == newStatus) return;

            meta.status = newStatus;
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            PrefabManager.InvalidateCache();
        }

        private void OpenSetup()
        {
            EAUploaderCore.selectedPrefabPath = prefabInfo.Path;
            EAUploader.Navigate(EAUploaderScreen.Setup);
        }

        private void OpenUpload()
        {
            EAUploaderCore.selectedPrefabPath = prefabInfo.Path;
            EAUploader.Navigate(EAUploaderScreen.Upload);
        }

        private void Delete()
        {
            if (PrefabManager.ShowDeletePrefabDialog(prefabInfo.Path))
            {
                Main.UpdateCards();
            }
        }
    }
}
