using EAUploader.UI.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAULogger = EAUploader.UI.Windows.Logger;

namespace EAUploader.UI
{
    /// <summary>
    /// EAUploaderの画面。タブではなく、1画面ずつ切り替えて表示する。
    /// </summary>
    public enum EAUploaderScreen
    {
        /// <summary>アバター/衣装/アイテムの一覧(起動時の画面)</summary>
        AvatarList,
        /// <summary>選択中のアイテムの編集(セットアップ)</summary>
        Setup,
        /// <summary>VRChatへのアップロード</summary>
        Upload,
    }

    public class EAUploader : EditorWindow
    {
        private static EAUploader instance;

        public static EAUploader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = GetWindow<EAUploader>();
                }
                return instance;
            }
        }

        /// <summary>
        /// ウィンドウが開かれていない場合にnullを返す。ウィンドウを生成したくない場面で使う。
        /// </summary>
        internal static EAUploader InstanceOrNull => instance;

        private void OnEnable()
        {
            instance = this;
        }

        private void OnDisable()
        {
            Setup.EditTools.EditToolModal.CloseActiveImmediate();
            Setup.ViewpointPositionEditor.CloseActive();
            instance = null;
        }

        #region Edit tool modal

        internal void SetToolUnsavedChanges(bool dirty)
        {
            saveChangesMessage = T7e.Get("This tool has unsaved changes. Do you want to save them?");
            hasUnsavedChanges = dirty;
        }

        public override void SaveChanges()
        {
            Setup.EditTools.EditToolModal.SaveActive();
            base.SaveChanges();
        }

        #endregion

        [MenuItem("EAUploader/Open EAUploader")]
        public static void ShowWindow()
        {
            EAUploader window = Instance;
            window.titleContent = new GUIContent("EAUploader");
            window.position = new Rect(100, 100, 1280, 640);
            window.minSize = new Vector2(1080, 640);
            window.Show();
            window.Focus();
        }

        private VisualElement contentRoot = null;
        private EAUploaderScreen currentScreen = EAUploaderScreen.AvatarList;
        public static StyleSheet styles;
        public static StyleSheet tailwind;
        public static Modal modal;

        internal EAUploaderScreen CurrentScreen => currentScreen;

        public void CreateGUI()
        {
            styles = Resources.Load<StyleSheet>("UI/styles");
            tailwind = Resources.Load<StyleSheet>("UI/tailwind");
            rootVisualElement.styleSheets.Add(styles);
            rootVisualElement.styleSheets.Add(tailwind);

            // Import UXML
            var visualTree = Resources.Load<VisualTreeAsset>("UI/MainWindow");
            visualTree.CloneTree(rootVisualElement);

            // Setup Modal
            modal = new Modal();
            rootVisualElement.Add(modal);

            ThemeUtility.ApplyTheme(rootVisualElement, ThemeUtility.GetCurrentTheme());

            // Localization
            rootVisualElement.schedule.Execute(() =>
            {
                LanguageUtility.Localization(rootVisualElement);
            }).Every(100);

            contentRoot = rootVisualElement.Q("contentRoot");

            ShowScreen(EAUploaderScreen.AvatarList);
        }

        /// <summary>
        /// 画面を切り替える。ウィンドウが開いていない場合は開いてから切り替える。
        /// </summary>
        internal static void Navigate(EAUploaderScreen screen)
        {
            var window = Instance;
            window.Show();
            window.Focus();
            window.ShowScreen(screen);
        }

        /// <summary>
        /// 現在表示中の画面を組み立て直す。
        /// </summary>
        internal static void RefreshCurrentScreen()
        {
            var window = InstanceOrNull;
            if (window == null || window.contentRoot == null) return;
            window.ShowScreen(window.currentScreen, force: true);
        }

        internal void ShowScreen(EAUploaderScreen screen, bool force = false)
        {
            if (contentRoot == null) return;
            if (!force && currentScreen == screen && contentRoot.childCount > 0) return;

            currentScreen = screen;
            contentRoot.Clear();

            switch (screen)
            {
                case EAUploaderScreen.AvatarList:
                    EAULogger.writeEAULog("Opened avatar list screen.");
                    AvatarList.Main.ShowContent(contentRoot);
                    break;
                case EAUploaderScreen.Setup:
                    EAULogger.writeEAULog("Opened setup screen.");
                    Setup.Main.ShowContent(contentRoot);
                    break;
                case EAUploaderScreen.Upload:
                    EAULogger.writeEAULog("Opened upload screen.");
                    Upload.Main.ShowContent(contentRoot);
                    break;
            }

            LanguageUtility.Localization(rootVisualElement);
        }

        private void OnGUI()
        {
            rootVisualElement.MarkDirtyRepaint();
        }
    }
}
