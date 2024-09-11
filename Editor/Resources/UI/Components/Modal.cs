using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    public class Modal : VisualElement
    {
        // State  
        private VisualTreeAsset visualTree;
        public string Title;
        public VisualElement Content;
        public ActionButton[] Actions;

        public class ActionButton
        {
            public string Text;
            public Action Callback;

            public ActionButton(string text, Action callback)
            {
                Text = text;
                Callback = callback;
            }
        }

        public Modal()
        {
            visualTree = Resources.Load<VisualTreeAsset>("UI/Components/Modal");
            style.display = DisplayStyle.None;
        }

        // --- Public Methods ---
        public void Initialize()
        {
            Clear();
            visualTree.CloneTree(this);
            this.AddToClassList("modal__container");
        }

        public void Show()
        {
            style.display = DisplayStyle.Flex;
        }

        public void Hide() {
            style.display = DisplayStyle.None;
        }

        public void setTitle(string title)
        {
            Title = title;
            
            this.Q<Label>("modal_title").text = title;
        }

        public void setContent(VisualElement content)
        {
            Content = content;
            var contentContainer = this.Q<VisualElement>("modal_content");
            contentContainer.Clear();
            contentContainer.Add(content);
        }

        public void setFooter(ActionButton[] actions)
        {
            Actions = actions;

            var footerContainer = this.Q<VisualElement>("modal_footer");
            footerContainer.Clear();

            foreach (var action in actions)
            {
                var button = new ShadowButton()
                {
                    text = action.Text,
                };

                button.AddToClassList("modal__button");

                button.clicked += () =>
                {
                    action.Callback();
                };

                footerContainer.Add(button);
            }
        }
    }
}
