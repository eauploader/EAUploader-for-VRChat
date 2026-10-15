using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#tool-modal
    internal sealed class ToolModalFrame
    {
        public VisualElement Container { get; }
        public VisualElement Header { get; }
        public VisualElement HeaderCenter { get; }
        public VisualElement Body { get; }

        public ToolModalFrame(string title, Action onBack)
        {
            Container = new VisualElement();
            Container.AddToClassList("modal__container");
            Container.AddToClassList("edit-tool-modal__container");

            var panel = new VisualElement();
            panel.AddToClassList("edit-tool-modal");
            panel.AddToClassList("flex-col");
            Container.Add(panel);

            Header = new VisualElement();
            Header.AddToClassList("flex-row");
            Header.AddToClassList("items-center");
            Header.AddToClassList("p-2");
            Header.AddToClassList("border-b");
            Header.AddToClassList("border-zinc-300");
            Header.AddToClassList("shrink-0");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("text-xl");
            titleLabel.AddToClassList("font-bold");
            titleLabel.AddToClassList("ml-2");
            Header.Add(titleLabel);

            HeaderCenter = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 12 }
            };
            Header.Add(HeaderCenter);

            var back = EditToolUtility.IconButton("arrow_back", T7e.Get("Back"), () => onBack?.Invoke());
            back.style.marginBottom = 0;
            back.style.marginRight = 0;
            Header.Add(back);

            panel.Add(Header);

            Body = new VisualElement();
            Body.AddToClassList("flex-1");
            Body.AddToClassList("flex-col");
            panel.Add(Body);
        }

        public static bool TryGetWindowRoot(Action retry, out VisualElement windowRoot)
        {
            var window = EAUploader.Instance;
            window.Show();
            window.Focus();

            windowRoot = window.rootVisualElement;
            if (windowRoot.childCount > 0) return true;

            EditorApplication.delayCall += () => retry();
            windowRoot = null;
            return false;
        }

        public void Attach(VisualElement windowRoot)
        {
            var modalIndex = EAUploader.modal != null ? windowRoot.IndexOf(EAUploader.modal) : -1;
            if (modalIndex >= 0) windowRoot.Insert(modalIndex, Container);
            else windowRoot.Add(Container);
        }

        public void Detach()
        {
            Container.RemoveFromHierarchy();
        }
    }
}
