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
            private readonly UxmlIntAttributeDescription _msgId = new() { name = "msg-id" };

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
}
