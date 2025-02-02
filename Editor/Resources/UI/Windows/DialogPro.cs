using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAUploader.UI.Components; // ★ 追加

namespace EAUploader.UI.Windows
{
    public static class DialogPro
    {
        public enum DialogType
        {
            Info,
            Warning,
            Error,
            Success
        }

        private static bool isOpen = false;

        public static void Show(DialogType dialogType, string title, string message, bool isOkButtonClickedDialogClose = true)
        {
            Action action = () => { };
            Show(dialogType, title, message, "OK", action, isOkButtonClickedDialogClose);
        }

        public static void Show(
            DialogType dialogType,
            string title,
            string message,
            string okButtonText,
            Action okButtonAction,
            bool isOkButtonClickedDialogClose = true
        )
        {
            if (isOpen) return;
            isOpen = true;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(title);

            var container = new VisualElement();
            container.styleSheets.Add(EAUploader.styles);
            container.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Windows/DialogPro");
            visualTree.CloneTree(container);

            LanguageUtility.Localization(container);

            var titleLabel = container.Q<Label>("title");
            var messageLabel = container.Q<Label>("message");
            // ★ MaterialIcon は EAUploader.UI.Components.MaterialIcon
            //   -> using EAUploader.UI.Components; を追加しておけば↓でOK
            var icon = container.Q<MaterialIcon>("icon");

            var copyButton = container.Q<Button>("copy");
            var okButton = container.Q<Button>("ok");

            if (titleLabel != null) titleLabel.text = title;
            if (messageLabel != null) messageLabel.text = message;
            if (okButton != null) okButton.text = okButtonText;

            if (copyButton != null)
            {
                copyButton.clicked += () =>
                {
                    EditorGUIUtility.systemCopyBuffer = message;
                };
            }

            if (okButton != null)
            {
                okButton.clicked += okButtonAction;
                if (isOkButtonClickedDialogClose)
                {
                    okButton.clicked += CloseDialog;
                }
            }

            switch (dialogType)
            {
                case DialogType.Info:
                    if (icon != null) icon.icon = "info";
                    if (copyButton != null) copyButton.style.display = DisplayStyle.None;
                    break;
                case DialogType.Warning:
                    if (icon != null)
                    {
                        icon.icon = "warning";
                        icon.AddToClassList("warning");
                    }
                    break;
                case DialogType.Error:
                    if (icon != null)
                    {
                        icon.icon = "error";
                        icon.AddToClassList("danger");
                    }
                    break;
                case DialogType.Success:
                    if (icon != null)
                    {
                        icon.icon = "check_circle";
                        icon.AddToClassList("success");
                    }
                    if (copyButton != null) copyButton.style.display = DisplayStyle.None;
                    break;
            }

            EAUploader.modal.setContent(container);
            EAUploader.modal.Show();
        }

        public static void Show(DialogType dialogType, string title, string message)
        {
            if (isOpen) return;
            isOpen = true;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(title);

            var container = new VisualElement();
            container.styleSheets.Add(EAUploader.styles);
            container.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Windows/DialogPro");
            visualTree.CloneTree(container);

            LanguageUtility.Localization(container);

            var titleLabel = container.Q<Label>("title");
            var messageLabel = container.Q<Label>("message");
            var icon = container.Q<MaterialIcon>("icon");

            var copyButton = container.Q<Button>("copy");
            var okButton = container.Q<Button>("ok");

            if (titleLabel != null) titleLabel.text = title;
            if (messageLabel != null) messageLabel.text = message;

            if (copyButton != null)
            {
                copyButton.clicked += () =>
                {
                    EditorGUIUtility.systemCopyBuffer = message;
                };
            }
            if (okButton != null)
            {
                okButton.clicked += CloseDialog;
            }

            switch (dialogType)
            {
                case DialogType.Info:
                    if (icon != null) icon.icon = "info";
                    if (copyButton != null) copyButton.style.display = DisplayStyle.None;
                    break;
                case DialogType.Warning:
                    if (icon != null)
                    {
                        icon.icon = "warning";
                        icon.AddToClassList("warning");
                    }
                    break;
                case DialogType.Error:
                    if (icon != null)
                    {
                        icon.icon = "error";
                        icon.AddToClassList("danger");
                    }
                    break;
                case DialogType.Success:
                    if (icon != null)
                    {
                        icon.icon = "check_circle";
                        icon.AddToClassList("success");
                    }
                    if (copyButton != null) copyButton.style.display = DisplayStyle.None;
                    break;
            }

            EAUploader.modal.setContent(container);
            EAUploader.modal.Show();
        }

        public static void CloseDialog()
        {
            isOpen = false;
            EAUploader.modal.Hide();
        }
    }
}
