using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    public sealed class ToastNotification
    {
        private const long FadeMs = 200;
        private readonly ToastManager manager;
        private readonly Label titleLabel;
        private readonly Label descriptionLabel;
        private readonly MaterialIcon icon;
        private readonly Button actionButton;
        private IVisualElementScheduledItem entrance;
        private IVisualElementScheduledItem removal;
        private Action action;
        private double remaining;
        private bool persistent;
        private bool removed;
        private bool stackedBehind;

        internal string Key { get; }
        internal VisualElement Slot { get; }
        internal Shadow Shell { get; }
        internal VisualElement Card { get; }
        internal VisualElement Content { get; }
        internal Button CloseButton { get; }
        internal float NaturalHeight => float.IsNaN(Content.resolvedStyle.height) ? 44f :
            Mathf.Max(44f, Content.resolvedStyle.height + 2f);
        public bool IsClosed { get; private set; }
        public ToastType Type { get; private set; }

        internal ToastNotification(ToastManager manager, string key)
        {
            this.manager = manager;
            Key = key;
            Slot = new VisualElement { pickingMode = PickingMode.Ignore };
            Slot.AddToClassList("toast-slot");
            Shell = new Shadow { shadowBlur = 20, shadowOffsetY = 4, shadowOpacity = 0.12f,
                pickingMode = PickingMode.Ignore };
            Shell.AddToClassList("toast-shell");
            Shell.AddToClassList("toast-shell--hidden");
            Slot.Add(Shell);
            Card = new VisualElement();
            Card.AddToClassList("toast-card");
            Shell.Add(Card);
            Content = new VisualElement();
            Content.AddToClassList("toast-card__content");
            Content.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.oldRect.size != evt.newRect.size) manager.RefreshLayout();
            });
            Card.Add(Content);
            icon = new MaterialIcon();
            icon.AddToClassList("toast-card__icon");
            Content.Add(icon);
            var text = new VisualElement();
            text.AddToClassList("toast-card__text");
            titleLabel = new Label { enableRichText = false };
            titleLabel.AddToClassList("toast-card__title");
            titleLabel.AddToClassList("no-translate");
            descriptionLabel = new Label { enableRichText = false };
            descriptionLabel.AddToClassList("toast-card__description");
            descriptionLabel.AddToClassList("no-translate");
            text.Add(titleLabel);
            text.Add(descriptionLabel);
            Content.Add(text);
            actionButton = new Button(() =>
            {
                if (IsClosed || stackedBehind) return;
                action?.Invoke();
                Close();
            });
            actionButton.AddToClassList("toast-card__action");
            actionButton.AddToClassList("no-translate");
            Content.Add(actionButton);
            CloseButton = new Button(() =>
            {
                if (!stackedBehind) Close();
            }) { tooltip = T7e.Get("Close notification") };
            CloseButton.AddToClassList("toast-card__close");
            CloseButton.Add(new MaterialIcon("close"));
            Content.Add(CloseButton);
            Card.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close();
                evt.StopPropagation();
                evt.PreventDefault();
            });
        }

        public void Update(string title, string description = null, ToastType type = ToastType.Info,
            double durationSeconds = 4, string actionText = null, Action action = null)
        {
            if (IsClosed) return;
            Type = type;
            titleLabel.text = title;
            descriptionLabel.text = description;
            descriptionLabel.style.display = string.IsNullOrEmpty(description) ? DisplayStyle.None : DisplayStyle.Flex;
            this.action = action;
            actionButton.text = actionText;
            actionButton.style.display = action == null || string.IsNullOrEmpty(actionText) ? DisplayStyle.None : DisplayStyle.Flex;
            persistent = durationSeconds <= 0 || type == ToastType.Loading;
            remaining = Math.Max(0, durationSeconds);
            Card.EnableInClassList("toast-card--success", type == ToastType.Success);
            Card.EnableInClassList("toast-card--warning", type == ToastType.Warning);
            Card.EnableInClassList("toast-card--error", type == ToastType.Error);
            icon.style.rotate = new Rotate(new Angle(0));
            switch (type)
            {
                case ToastType.Success: icon.icon = "check_circle_outline"; break;
                case ToastType.Warning: icon.icon = "warning_amber"; break;
                case ToastType.Error: icon.icon = "error_outline"; break;
                case ToastType.Loading: icon.icon = "autorenew"; break;
                default: icon.icon = "info_outline"; break;
            }
            manager.RefreshLayout();
        }

        internal void SetStackPosition(float y, float scale, float height, bool behind)
        {
            Slot.style.translate = new Translate(0, y);
            Slot.style.scale = new Scale(new Vector2(scale, scale));
            Card.style.height = height;
            Shell.EnableInClassList("toast-shell--behind", behind);
            stackedBehind = behind;
            CloseButton.tabIndex = behind ? -1 : 0;
            actionButton.tabIndex = behind ? -1 : 0;
        }

        internal void Enter()
        {
            entrance = Shell.schedule.Execute(() =>
                entrance = Shell.schedule.Execute(() =>
                {
                    if (IsClosed) return;
                    Shell.RemoveFromClassList("toast-shell--hidden");
                    EAUploader.RequestSmoothFrames(0.25);
                }).StartingIn(16));
        }

        internal void Tick(double elapsed, double now)
        {
            if (IsClosed) return;
            if (Type == ToastType.Loading) icon.style.rotate = new Rotate(new Angle((float)(now * 240 % 360)));
            if (persistent) return;
            remaining -= elapsed;
            if (remaining <= 0) Close();
        }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            entrance?.Pause();
            manager.RestoreFocus(this);
            Shell.AddToClassList("toast-shell--hidden");
            manager.RefreshLayout();
            EAUploader.RequestSmoothFrames(0.25);
            if (Shell.panel == null) RemoveImmediate();
            else removal = Shell.schedule.Execute(RemoveImmediate).StartingIn(FadeMs);
        }

        internal void RemoveImmediate()
        {
            if (removed) return;
            removed = true;
            IsClosed = true;
            entrance?.Pause();
            removal?.Pause();
            manager.RestoreFocus(this);
            Slot.RemoveFromHierarchy();
            manager.Remove(this);
        }
    }
}
