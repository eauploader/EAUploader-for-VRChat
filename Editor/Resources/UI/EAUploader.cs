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
        /// <summary>VRChatへのアップロード(編集画面でアップロードのシートを開いた状態)</summary>
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

        // --- Smooth frames ---
        // エディターは、何も起きていないと1秒に十数回しか画面を更新しない(測定値)。UIのアニメーションや3Dビューの
        // 移動の間だけ、ウィンドウの再描画を要求し続けて、更新を1秒に100回以上に保つ。
        private static double smoothFramesUntil;
        private static bool smoothFramesRegistered;

        /// <summary>
        /// 指定した秒数のあいだ、毎フレーム再描画を要求し続ける。アニメーションを始めるときに呼ぶ。
        /// </summary>
        internal static void RequestSmoothFrames(double seconds)
        {
            smoothFramesUntil = System.Math.Max(smoothFramesUntil, EditorApplication.timeSinceStartup + seconds);

            if (!smoothFramesRegistered)
            {
                smoothFramesRegistered = true;
                EditorApplication.update += SmoothFramesTick;
            }

            instance?.Repaint();
        }

        private static void SmoothFramesTick()
        {
            if (instance == null || EditorApplication.timeSinceStartup > smoothFramesUntil)
            {
                smoothFramesRegistered = false;
                EditorApplication.update -= SmoothFramesTick;
                return;
            }

            instance.Repaint();
        }

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
            }).Every(500);

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
            if (!window || window.contentRoot == null) return;
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
                    Setup.Main.ShowContent(contentRoot, Setup.EditScreenMode.Edit);
                    break;
                case EAUploaderScreen.Upload:
                    // アップロードは編集画面のシートにまとめた
                    EAULogger.writeEAULog("Opened upload screen.");
                    Setup.Main.ShowContent(contentRoot, Setup.EditScreenMode.Upload);
                    break;
                default:
                    break;
            }

            LanguageUtility.Localization(rootVisualElement);
        }
    }
}
