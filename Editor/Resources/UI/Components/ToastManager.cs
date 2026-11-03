using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    public enum ToastType { Info, Success, Warning, Error, Loading }

    public sealed class ToastManager : IDisposable
    {
        private const int Limit = 3;
        private readonly VisualElement root;
        private readonly VisualElement viewport;
        private readonly VisualElement stack;
        private readonly EditorWindow owner;
        private readonly List<ToastNotification> notifications = new List<ToastNotification>();
        private readonly IVisualElementScheduledItem timer;
        private VisualElement previousFocus;
        private double lastTick;
        private bool hovered;
        private bool expanded;
        private bool disposed;

        public int Count => notifications.Count(n => !n.IsClosed);

        public ToastManager(VisualElement root, EditorWindow owner = null)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.owner = owner;
            viewport = new VisualElement { name = "toast_viewport", pickingMode = PickingMode.Ignore };
            viewport.AddToClassList("toast-viewport");
            stack = new VisualElement { pickingMode = PickingMode.Ignore };
            stack.AddToClassList("toast-stack");
            stack.RegisterCallback<PointerEnterEvent>(_ =>
            {
                hovered = true;
                SetExpanded(true);
            });
            stack.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                hovered = false;
                SetExpanded(HasFocus());
            });
            stack.RegisterCallback<FocusInEvent>(_ => SetExpanded(true));
            stack.RegisterCallback<FocusOutEvent>(evt =>
                SetExpanded(hovered || stack.Contains(evt.relatedTarget as VisualElement)));
            viewport.Add(stack);
            root.Add(viewport);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            viewport.RegisterCallback<DetachFromPanelEvent>(OnDetached);
            Resize(root.resolvedStyle.width);
            timer = viewport.schedule.Execute(Tick).Every(50);
            timer.Pause();
            EditorApplication.focusChanged += OnApplicationFocusChanged;
        }

        public ToastNotification Show(string title, string description = null, ToastType type = ToastType.Info,
            double durationSeconds = 4, string key = null, string actionText = null, Action action = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ToastManager));
            viewport.BringToFront();
            var existing = string.IsNullOrEmpty(key) ? null : notifications.Find(n => !n.IsClosed && n.Key == key);
            if (existing != null)
            {
                existing.Update(title, description, type, durationSeconds, actionText, action);
                return existing;
            }
            while (notifications.Count >= Limit) notifications[0].RemoveImmediate();
            var toast = new ToastNotification(this, key);
            notifications.Add(toast);
            stack.Add(toast.Slot);
            toast.Update(title, description, type, durationSeconds, actionText, action);
            toast.Enter();
            RefreshLayout();
            lastTick = EditorApplication.timeSinceStartup;
            timer.Resume();
            return toast;
        }

        public void Clear()
        {
            foreach (var toast in notifications.ToArray()) toast.RemoveImmediate();
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var elapsed = Math.Max(0, now - lastTick);
            lastTick = now;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var paused = !UnityEditorInternal.InternalEditorUtility.isApplicationActive ||
                (owner != null && EditorWindow.focusedWindow != owner) ||
                hovered || (focused != null && stack.Contains(focused));
            foreach (var toast in notifications.ToArray()) toast.Tick(paused ? 0 : elapsed, now);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.F6 || Count == 0) return;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            if (focused != null && viewport.Contains(focused))
            {
                RestoreFocus();
            }
            else
            {
                previousFocus = focused;
                notifications.Last(n => !n.IsClosed).CloseButton.Focus();
            }
            evt.StopPropagation();
            evt.PreventDefault();
        }

        internal void RestoreFocus(ToastNotification toast = null)
        {
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            if (toast != null && (focused == null || !toast.Card.Contains(focused))) return;
            if (previousFocus != null && previousFocus.panel == root.panel) previousFocus.Focus();
            else focused?.Blur();
            previousFocus = null;
        }

        internal void Remove(ToastNotification toast)
        {
            notifications.Remove(toast);
            if (notifications.Count == 0) timer.Pause();
            RefreshLayout();
        }

        private bool HasFocus()
        {
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && stack.Contains(focused);
        }

        private void SetExpanded(bool value)
        {
            if (expanded == value) return;
            expanded = value;
            RefreshLayout();
        }

        internal void RefreshLayout()
        {
            var visible = notifications.Where(n => !n.IsClosed).Reverse().ToArray();
            stack.pickingMode = visible.Length > 0 ? PickingMode.Position : PickingMode.Ignore;
            var frontHeight = visible.Length > 0 ? visible[0].NaturalHeight : 0;
            var offset = 0f;
            for (var i = 0; i < visible.Length; i++)
            {
                var scale = expanded ? 1f : 1f - i * 0.1f;
                var height = expanded ? visible[i].NaturalHeight : frontHeight;
                var y = expanded ? offset : i * 12f + frontHeight * (1f - scale);
                visible[i].SetStackPosition(y, scale, height, !expanded && i > 0);
                offset += height + 12f;
            }
            stack.style.height = expanded ? Mathf.Max(0, offset - 12f) :
                frontHeight + Mathf.Max(0, visible.Length - 1) * 12f;
            EAUploader.RequestSmoothFrames(0.35);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => Resize(evt.newRect.width);

        private void OnApplicationFocusChanged(bool focused) => lastTick = EditorApplication.timeSinceStartup;

        private void Resize(float width)
        {
            if (!float.IsNaN(width)) stack.style.width = Mathf.Max(0, Mathf.Min(320, width - 32));
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == viewport) Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            timer.Pause();
            EditorApplication.focusChanged -= OnApplicationFocusChanged;
            Clear();
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            viewport.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            viewport.RemoveFromHierarchy();
        }
    }

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
            {
                entrance = Shell.schedule.Execute(() =>
                {
                    if (IsClosed) return;
                    Shell.RemoveFromClassList("toast-shell--hidden");
                    EAUploader.RequestSmoothFrames(0.25);
                }).StartingIn(16);
            });
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
