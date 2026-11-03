using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
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
        // Assigned by UI Toolkit callbacks after construction. skipcq: CS-R1137
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
            // A missing focus and a detached root both have null panels. skipcq: CS-R1136
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

}
