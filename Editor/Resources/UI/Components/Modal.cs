using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    public class Modal : VisualElement
    {
        // Marks the modal as one that fades in; only this component gets the animation (ToolModalFrame reuses modal__container).
        private const string AnimatedClass = "modal--animated";
        // Added a moment after the modal is shown, removed when it is hidden. The backdrop darkens and the dialog fades in
        // and scales up (and the reverse on hide) by transitions in the USS, so on show the unchanged starting state has
        // to be rendered first.
        private const string ShownClass = "modal--shown";
        private const long ShownDelayMs = 30;
        // How long Hide waits for the exit transitions before taking the modal out of the layout. Keep in sync with the USS.
        private const long HideDelayMs = 150;

        private IVisualElementScheduledItem showItem;
        private IVisualElementScheduledItem hideItem;

        // State  
        private readonly VisualTreeAsset visualTree;
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
            this.AddToClassList(AnimatedClass);
        }

        public void Show()
        {
            // Already open (the content was swapped): nothing to animate.
            if (style.display == DisplayStyle.Flex && ClassListContains(ShownClass))
            {
                return;
            }

            // Shown again while a previous hide is still fading out: cancel that hide.
            showItem?.Pause();
            hideItem?.Pause();
            hideItem = null;

            style.display = DisplayStyle.Flex;
            RemoveFromClassList(ShownClass);

            if (panel == null)
            {
                AddToClassList(ShownClass);
                return;
            }

            showItem = schedule.Execute(() => AddToClassList(ShownClass));
            showItem.StartingIn(ShownDelayMs);
        }

        public void Hide()
        {
            // A show that has not reached its second step yet must not complete after this hide.
            showItem?.Pause();
            RemoveFromClassList(ShownClass);

            if (panel == null)
            {
                style.display = DisplayStyle.None;
                return;
            }

            // Let the exit transition play, then take the modal out of the layout. If it was shown again in the
            // meantime, leave it alone.
            hideItem?.Pause();
            hideItem = schedule.Execute(() =>
            {
                if (!ClassListContains(ShownClass))
                {
                    style.display = DisplayStyle.None;
                }
            });
            hideItem.StartingIn(HideDelayMs);
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

                button.clicked += () => action.Callback();

                footerContainer.Add(button);
            }
        }
    }
}
