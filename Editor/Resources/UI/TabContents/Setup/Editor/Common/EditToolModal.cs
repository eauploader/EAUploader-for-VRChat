using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Preview = EAUploader.UI.Components.Preview;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md
    internal abstract class EditToolModal
    {
        private static EditToolModal active;

        protected EditToolSession Session { get; private set; }
        protected Preview Preview { get; private set; }
        protected string TargetPrefabPath { get; private set; }

        private ToolModalFrame frame;
        private VisualElement previewArea;
        private VisualElement sidePanel;
        private Label targetLabel;
        private Label dirtyLabel;
        private Button applyButton;
        private Button revertButton;

        protected abstract string ToolTitle { get; }
        protected virtual float SidePanelWidth => 380f;

        protected abstract void BuildSidePanel(VisualElement panel);
        // Optional hooks
        protected virtual void BuildFooterActions(VisualElement footer) { } // skipcq: CS-R1093
        protected virtual void OnBeforeSave() { } // skipcq: CS-R1093
        protected virtual void OnAfterSave() { } // skipcq: CS-R1093
        protected virtual void OnClose() { } // skipcq: CS-R1093

        #region Opening

        protected static void ShowTool<T>() where T : EditToolModal, new()
        {
            if (!ToolModalFrame.TryGetWindowRoot(ShowTool<T>, out var windowRoot)) return;
            if (!RequestCloseActive()) return;
            ViewpointPositionEditor.CloseActive();

            var tool = new T();
            tool.Build(windowRoot, EAUploaderCore.selectedPrefabPath);
            active = tool;
        }

        internal static bool HasUnsavedChanges => active?.Session != null && active.Session.IsDirty;

        internal static void SaveActive()
        {
            active?.Save();
        }

        internal static bool RequestCloseActive()
        {
            return active == null || active.RequestClose();
        }

        internal static void CloseActiveImmediate()
        {
            active?.CloseImmediate();
        }

        public void SetTarget(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return;
            if (Session?.PrefabPath == prefabPath) return;
            if (!ConfirmLeave()) return;

            TargetPrefabPath = prefabPath;
            OpenSession();
        }

        #endregion

        #region Layout

        private void Build(VisualElement windowRoot, string prefabPath)
        {
            TargetPrefabPath = prefabPath;

            frame = new ToolModalFrame(ToolTitle, () => RequestClose());

            targetLabel = new Label { style = { flexGrow = 1 } };
            targetLabel.AddToClassList("no-translate");
            frame.HeaderCenter.Add(targetLabel);

            dirtyLabel = new Label(T7e.Get("Unsaved changes")) { style = { marginRight = 12 } };
            dirtyLabel.AddToClassList("warning");
            frame.HeaderCenter.Add(dirtyLabel);

            var body = new VisualElement();
            body.AddToClassList("flex-row");
            body.AddToClassList("flex-1");

            var sideScroll = new ScrollView { style = { width = SidePanelWidth, flexShrink = 0 } };
            sideScroll.AddToClassList("border-r");
            sideScroll.AddToClassList("border-zinc-300");
            sidePanel = new VisualElement();
            sidePanel.AddToClassList("p-4");
            sideScroll.Add(sidePanel);
            body.Add(sideScroll);

            previewArea = new VisualElement();
            previewArea.AddToClassList("flex-1");
            body.Add(previewArea);
            frame.Body.Add(body);

            frame.Body.Add(BuildFooter());
            frame.Attach(windowRoot);

            EAUploaderCore.SelectedPrefabPathChanged += OnSelectedPrefabPathChanged;
            AssemblyReloadEvents.beforeAssemblyReload += CloseImmediate;

            OpenSession();
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("flex-row");
            footer.AddToClassList("items-center");
            footer.AddToClassList("justify-between");
            footer.AddToClassList("p-2");
            footer.AddToClassList("border-t");
            footer.AddToClassList("border-zinc-300");
            footer.AddToClassList("shrink-0");

            var footerLeft = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            BuildFooterActions(footerLeft);
            footer.Add(footerLeft);

            var footerRight = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            revertButton = EditToolUtility.IconButton("undo", T7e.Get("Revert"), Revert);
            footerRight.Add(revertButton);
            applyButton = EditToolUtility.IconButton("save", T7e.Get("Apply"), () => Save());
            footerRight.Add(applyButton);
            footer.Add(footerRight);

            return footer;
        }

        #endregion

        #region Closing

        public bool RequestClose()
        {
            if (!ConfirmLeave()) return false;

            CloseImmediate();
            return true;
        }

        private void CloseImmediate()
        {
            EAUploaderCore.SelectedPrefabPathChanged -= OnSelectedPrefabPathChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CloseImmediate;

            CloseSession();
            OnClose();

            frame?.Detach();
            frame = null;

            if (active == this) active = null;
            NotifyWindow(false);
        }

        private void OnSelectedPrefabPathChanged(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            SetTarget(path);
        }

        #endregion

        #region Session

        private void OpenSession()
        {
            CloseSession();

            if (sidePanel == null) return;
            sidePanel.Clear();

            Session = EditToolSession.Open(TargetPrefabPath);
            if (Session == null)
            {
                sidePanel.Add(EditToolUtility.StatusRow("warning", "warning",
                    T7e.Get("Select an item from the list first.")));
                targetLabel.text = string.Empty;
                UpdateState();
                return;
            }

            Session.Changed += UpdateState;
            Session.Reloaded += OnSessionReloaded;

            targetLabel.text = Path.GetFileNameWithoutExtension(TargetPrefabPath);

            Preview = new Preview(previewArea, Session.Root);
            Preview.ShowContent();

            BuildSidePanel(sidePanel);
            UpdateState();
        }

        private void OnSessionReloaded()
        {
            Preview?.SetLiveTarget(Session.Root);
            sidePanel.Clear();
            BuildSidePanel(sidePanel);
        }

        private void CloseSession()
        {
            if (Session != null)
            {
                Session.Changed -= UpdateState;
                Session.Reloaded -= OnSessionReloaded;
                Session.Dispose();
                Session = null;
            }

            Preview?.Dispose();
            Preview = null;
            previewArea?.Clear();
            NotifyWindow(false);
        }

        protected void RefreshPreview()
        {
            Preview?.Refresh();
        }

        protected void FramePreview(Vector3 center, float radius)
        {
            Preview?.Frame(center, radius);
        }

        protected void RebuildSidePanel()
        {
            if (sidePanel == null || Session == null) return;
            sidePanel.Clear();
            BuildSidePanel(sidePanel);
        }

        protected void MarkDirty()
        {
            Session?.MarkDirty();
            Preview?.Repaint();
        }

        private void UpdateState()
        {
            var dirty = Session != null && Session.IsDirty;

            dirtyLabel?.EnableInClassList("hidden", !dirty);
            applyButton?.SetEnabled(dirty);
            revertButton?.SetEnabled(dirty);
            NotifyWindow(dirty);
        }

        private static void NotifyWindow(bool dirty)
        {
            EAUploader.InstanceOrNull?.SetToolUnsavedChanges(dirty);
        }

        protected bool Save()
        {
            if (Session == null) return false;

            OnBeforeSave();
            var saved = Session.Save();
            if (saved) OnAfterSave();

            RefreshPreview();
            return saved;
        }

        protected void Revert()
        {
            Session?.Reload();
        }

        protected void Retarget(string prefabPath)
        {
            if (Session == null) return;

            TargetPrefabPath = prefabPath;
            Session.Retarget(prefabPath);
            targetLabel.text = Path.GetFileNameWithoutExtension(prefabPath);
        }

        private bool ConfirmLeave()
        {
            if (Session == null || !Session.IsDirty) return true;

            var choice = EditorUtility.DisplayDialogComplex(
                ToolTitle,
                T7e.Get("This tool has unsaved changes. Do you want to save them?"),
                T7e.Get("Save"),
                T7e.Get("Cancel"),
                T7e.Get("Discard"));

            switch (choice)
            {
                case 0:
                    return Save();
                case 2:
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}
