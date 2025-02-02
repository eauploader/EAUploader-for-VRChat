using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    public class HelpButton : VisualElement
    {
        // UxmlFactory: UXML上で <HelpButton> を認識するため
        public new class UxmlFactory : UxmlFactory<HelpButton, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private UxmlIntAttributeDescription _msgId = new UxmlIntAttributeDescription { name = "msg-id" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var helpButton = ve as HelpButton;

                helpButton.msg_id = _msgId.GetValueFromBag(bag, cc);
            }
        }

        private int msg_id;

        public HelpButton()
        {
            var shadow = new Shadow()
            {
                shadowDistance = 0,
                shadowCornerRadius = 8,
                shadowOffsetY = 2,
            };
            var button = new Button();
            button.AddToClassList("help-button");

            var icon = new MaterialIcon()
            {
                icon = "help"
            };
            button.Add(icon);

            var label = new Label(T7e.Get("Help"));
            button.Add(label);

            button.RegisterCallback<ClickEvent>(OnButtonClicked);

            shadow.Add(button);
            Add(shadow);
        }

        private void OnButtonClicked(ClickEvent evt)
        {
            // モーダル表示へ変更したため実装は同じでも表示先が変わる
            EAUploaderMessageWindow.ShowMsg(msg_id);
        }
    }

    public static class EAUploaderMessageWindow
    {
        private static readonly Vector2 windowSize = new Vector2(600, 300);

        /// <summary>
        /// ShowMsgを呼ぶと、EAUploaderのモーダルを表示する。
        /// </summary>
        /// <param name="msgNum"></param>
        public static void ShowMsg(int msgNum)
        {
            // 1. モーダル初期化
            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Message"));

            // 2. コンテンツ（VisualElement）作成
            var container = new VisualElement();
            container.styleSheets.Add(Resources.Load<StyleSheet>("UI/styles"));

            // ScrollView
            var scrollView = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            container.Add(scrollView);

            // メッセージファイル読込
            LoadMsg(msgNum, scrollView);

            // 閉じるボタン
            var closeButton = new ShadowButton()
            {
                name = "close_button",
                text = T7e.Get("Close")
            };
            closeButton.clicked += () =>
            {
                // モーダルを閉じる
                EAUploader.modal.Hide();
            };
            container.Add(closeButton);

            // 3. モーダルにコンテンツをセット
            EAUploader.modal.setContent(container);

            // 4. モーダルを表示
            EAUploader.modal.Show();
        }

        /// <summary>
        /// 指定された msgNum テキストを読み込み、ScrollView に表示
        /// </summary>
        private static void LoadMsg(int msgNum, ScrollView scrollView)
        {
            string language = LanguageUtility.GetCurrentLanguage();
            string contentPath = $"Packages/tech.uslog.eauploader/Editor/Resources/Message/{language}/{msgNum}.txt";

            var article = new ArticleRenderer(contentPath);
            scrollView.Add(article);
        }
    }
}
