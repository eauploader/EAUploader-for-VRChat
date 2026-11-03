using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Modals
{
    internal sealed class DeletePrefabModal
    {
        private const double DetailsAnimationSeconds = 0.2;
        private readonly string prefabPath;
        private readonly string prefabGuid;
        private readonly Action<bool> completed;
        private readonly HashSet<string> deleteTargets = new HashSet<string>();
        private Modal modal;
        private VisualElement content;
        private Button cancelButton;
        private Button deleteButton;
        private PrefabDeletionPlan plan;
        private IEnumerator scan;
        private IVisualElementScheduledItem scanTask;
        private IVisualElementScheduledItem detailsAnimation;
        private VisualElement detailsBody;
        private VisualElement detailsList;
        private bool detailsExpanded;
        private float currentDetailsHeight;
        private bool closed;
        private bool busy;
        private bool confirmAfterScan;
        private HashSet<string> confirmedTargets;
        private Dictionary<string, string> confirmedGuids;
        private VisualElement previousFocus;

        internal DeletePrefabModal(string prefabPath, Action<bool> completed)
        {
            this.prefabPath = prefabPath;
            prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            this.completed = completed;
        }

        internal bool Open()
        {
            var window = EAUploader.Instance;
            window.Show();
            window.Focus();
            modal = EAUploader.modal;
            if (modal == null) return false;

            previousFocus = modal.panel?.focusController.focusedElement as VisualElement;
            modal.Initialize();
            var modalStyles = Resources.Load<StyleSheet>("UI/Modals/DeletePrefabModalStyles");
            if (!modal.styleSheets.Contains(modalStyles)) modal.styleSheets.Add(modalStyles);
            modal.AddToClassList("delete-prefab-dialog");
            modal.setTitle(T7e.Get("Delete Prefab"));
            content = new VisualElement();
            Resources.Load<VisualTreeAsset>("UI/Modals/DeletePrefabModal").CloneTree(content);
            content.Q<Label>("prefab_name").text = Path.GetFileNameWithoutExtension(prefabPath);
            content.Q<Label>("prefab_name").enableRichText = false;
            detailsBody = content.Q("deletion_details");
            detailsList = content.Q("deletion_list");
            detailsList.RegisterCallback<GeometryChangedEvent>(OnDetailsGeometryChanged);
            content.Q<Button>("toggle_details").clicked += ToggleDetails;
            content.Q<Button>("retry_search").clicked += () => StartScan(false);
            SmoothScroll.Enable(content.Q<ScrollView>("delete_scroll"));
            LanguageUtility.Localization(content);
            modal.setContent(content);
            modal.setFooter(Array.Empty<Modal.ActionButton>());
            var footer = modal.Q("modal_footer");
            cancelButton = new Button(() => Close(false)) { text = T7e.Get("Cancel") };
            cancelButton.AddToClassList("modal__button");
            cancelButton.AddToClassList("delete-prefab__action");
            cancelButton.AddToClassList("delete-prefab__cancel");
            footer.Add(cancelButton);
            deleteButton = new Button(Confirm);
            deleteButton.Add(new MaterialIcon("delete"));
            deleteButton.Add(new Label(T7e.Get("Delete")));
            deleteButton.AddToClassList("modal__button");
            deleteButton.AddToClassList("delete-prefab__action");
            deleteButton.AddToClassList("delete-prefab__confirm");
            footer.Add(deleteButton);
            content.RegisterCallback<DetachFromPanelEvent>(OnDetached);
            modal.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            EditorApplication.projectChanged += OnProjectChanged;
            modal.Show();
            modal.BringToFront();
            cancelButton.Focus();
            StartScan(false);
            return true;
        }

        private void ToggleDetails()
        {
            detailsExpanded = !detailsExpanded;
            content.Q("details_card").EnableInClassList("delete-prefab__details--open", detailsExpanded);
            AnimateDetails();
        }

        private void OnDetailsGeometryChanged(GeometryChangedEvent evt)
        {
            if (detailsExpanded && Mathf.Abs(evt.newRect.height - evt.oldRect.height) > 0.5f)
                AnimateDetails();
        }

        private void AnimateDetails()
        {
            var targetHeight = detailsExpanded ? detailsList.layout.height : 0f;
            if (float.IsNaN(targetHeight)) return;

            detailsAnimation?.Pause();
            var fromHeight = currentDetailsHeight;
            var start = -1.0;
            IVisualElementScheduledItem item = null;
            item = detailsBody.schedule.Execute(() =>
            {
                var now = EditorApplication.timeSinceStartup;
                if (start < 0) start = now;

                var t = Mathf.Clamp01((float)((now - start) / DetailsAnimationSeconds));
                var eased = 1f - (1f - t) * (1f - t) * (1f - t);
                currentDetailsHeight = Mathf.Lerp(fromHeight, targetHeight, eased);
                detailsBody.style.height = currentDetailsHeight;
                if (t >= 1f) item.Pause();
            }).Every(1);
            detailsAnimation = item;
            EAUploader.RequestSmoothFrames(DetailsAnimationSeconds + 0.1);
        }

        private void StartScan(bool confirm)
        {
            scanTask?.Pause();
            (scan as IDisposable)?.Dispose();
            confirmAfterScan = confirm;
            busy = true;
            deleteButton.SetEnabled(false);
            content.Q("deletion_list").Clear();
            SetHidden("delete_count", true);
            SetHidden("retry_search", true);
            SetHidden("delete_error", true);
            SetHidden("search_progress", false);
            SetHidden("search_status", false);
            content.Q<ProgressBar>("search_progress").value = 0;
            content.Q<Label>("search_status").text = T7e.Get(confirm
                ? "Checking deletion targets..." : "Preparing deletion...");
            content.Q<Label>("delete_summary").text = string.Empty;
            plan = new PrefabDeletionPlan(prefabPath);
            scan = plan.Scan();
            scanTask = content.schedule.Execute(ScanTick).Every(16).StartingIn(50);
        }

        private void ScanTick()
        {
            if (closed) return;
            try
            {
                var deadline = EditorApplication.timeSinceStartup + 0.008;
                do
                {
                    if (!scan.MoveNext())
                    {
                        scanTask.Pause();
                        ScanCompleted();
                        return;
                    }
                } while (EditorApplication.timeSinceStartup < deadline);
                content.Q<ProgressBar>("search_progress").value = plan.Progress * 100;
                EAUploader.RequestSmoothFrames(0.1);
            }
            catch (Exception ex)
            {
                scanTask.Pause();
                busy = false;
                confirmAfterScan = false;
                deleteButton.SetEnabled(false);
                SetHidden("search_progress", true);
                SetHidden("search_status", true);
                ShowError(T7e.Get("Could not check related assets. No assets were deleted. Please search again.") + "\n" + ex.Message);
                SetHidden("retry_search", false);
                Debug.LogException(ex);
            }
        }

        private void ScanCompleted()
        {
            busy = false;
            SetHidden("search_progress", true);
            if (plan.PrefabGuid != prefabGuid || string.IsNullOrEmpty(prefabGuid))
                throw new InvalidOperationException("The prefab has changed. Please open the delete dialog again.");

            deleteTargets.Clear();
            deleteTargets.UnionWith(plan.Assets.Where(a => !plan.UsedElsewhere.Contains(a.Path)).Select(a => a.Path));
            deleteTargets.ExceptWith(plan.GetBlockedAssets(deleteTargets));
            BuildDeletionList();
            deleteButton.SetEnabled(true);
            SetHidden("search_status", true);

            if (!confirmAfterScan) return;
            confirmAfterScan = false;
            if (!deleteTargets.SetEquals(confirmedTargets) || plan.Assets.Any(a => deleteTargets.Contains(a.Path) &&
                (!confirmedGuids.TryGetValue(a.Path, out var guid) || guid != a.Guid)))
            {
                ShowError(T7e.Get("Deletion targets changed. Review the list and confirm deletion again."));
                return;
            }

            ExecuteDeletion();
        }

        private void ExecuteDeletion()
        {
            scanTask?.Pause();
            (scan as IDisposable)?.Dispose();
            busy = false;
            SetHidden("search_progress", true);
            if (plan.PrefabGuid != prefabGuid || string.IsNullOrEmpty(prefabGuid))
            {
                deleteButton.SetEnabled(false);
                ShowError(T7e.Get("Deletion targets changed. Review the list and confirm deletion again."));
                return;
            }
            try
            {
                deleteButton.SetEnabled(false);
                EditorApplication.projectChanged -= OnProjectChanged;
                plan.Delete(deleteTargets);
            }
            catch (Exception ex)
            {
                ShowError(T7e.Get("Some assets could not be deleted. Check the paths below.") + "\n" + ex.Message);
                SetHidden("retry_search", false);
                cancelButton.text = T7e.Get("Close");
                Debug.LogException(ex);
                return;
            }
            Close(true);
        }

        private void BuildDeletionList()
        {
            var list = content.Q("deletion_list");
            list.Clear();
            AddAssetRow(list, prefabPath, T7e.Get("Prefab"), "view_in_ar");
            var previewPath = PrefabPreview.GetPreviewImagePath(prefabPath).Replace('\\', '/');
            if (File.Exists(previewPath)) AddAssetRow(list, previewPath, T7e.Get("Preview image"), "image");
            foreach (var asset in plan.Assets.Where(a => deleteTargets.Contains(a.Path))
                .OrderBy(a => a.IsSource ? 0 : 1).ThenBy(a => a.Path))
            {
                var material = Path.GetExtension(asset.Path).Equals(".mat", StringComparison.OrdinalIgnoreCase);
                AddAssetRow(list, asset.Path, T7e.Get(asset.IsSource ? "Imported prefab" : material ? "Material" : "Texture"),
                    asset.IsSource ? "view_in_ar" : material ? "palette" : "image");
            }
            content.Q<Label>("delete_count").text = list.childCount.ToString();
            SetHidden("delete_count", false);
            content.Q<Label>("delete_summary").text = string.Format(T7e.Get("{0} files will be deleted."), list.childCount);
        }

        private static void AddAssetRow(VisualElement list, string assetPath, string kind, string icon)
        {
            var row = new VisualElement();
            row.AddToClassList("delete-prefab__asset");
            var assetIcon = new MaterialIcon(icon) { pickingMode = PickingMode.Ignore };
            assetIcon.AddToClassList("delete-prefab__asset-icon");
            row.Add(assetIcon);
            var text = new VisualElement();
            text.AddToClassList("delete-prefab__asset-text");
            var heading = new VisualElement();
            heading.AddToClassList("delete-prefab__asset-heading");
            var name = new Label(Path.GetFileName(assetPath)) { enableRichText = false };
            name.AddToClassList("delete-prefab__asset-name");
            name.AddToClassList("no-translate");
            heading.Add(name);
            var type = new Label(kind);
            type.AddToClassList("delete-prefab__asset-kind");
            heading.Add(type);
            text.Add(heading);
            var path = new Label(assetPath) { enableRichText = false };
            path.AddToClassList("delete-prefab__path");
            path.AddToClassList("no-translate");
            text.Add(path);
            row.Add(text);
            list.Add(row);
        }

        private void Confirm()
        {
            if (closed || busy) return;
            if (plan.IsCurrent)
            {
                ExecuteDeletion();
                return;
            }
            confirmedTargets = new HashSet<string>(deleteTargets);
            confirmedGuids = plan.Assets.ToDictionary(a => a.Path, a => a.Guid);
            StartScan(true);
        }

        private void OnProjectChanged()
        {
            if (!closed) StartScan(false);
        }

        private void ShowError(string message)
        {
            content.Q<Label>("delete_error").text = message;
            SetHidden("delete_error", false);
        }

        private void SetHidden(string name, bool hidden) =>
            content.Q(name).EnableInClassList("delete-prefab__hidden", hidden);

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == content) Close(false, false);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close(false);
            }
            else if (evt.keyCode == KeyCode.Tab)
            {
                var focusables = modal.Query<VisualElement>().ToList()
                    .Where(e => e is Button && e.enabledInHierarchy && IsVisible(e))
                    .ToList();
                var index = focusables.IndexOf(modal.panel?.focusController.focusedElement as VisualElement);
                if (focusables.Count > 0)
                    focusables[(index + (evt.shiftKey ? -1 : 1) + focusables.Count) % focusables.Count].Focus();
            }
            else return;
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private bool IsVisible(VisualElement element)
        {
            for (var current = element; current != null && current != modal; current = current.hierarchy.parent)
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility != Visibility.Visible)
                    return false;
            return true;
        }

        private void Close(bool deleted, bool hide = true)
        {
            if (closed) return;
            closed = true;
            scanTask?.Pause();
            detailsAnimation?.Pause();
            detailsList.UnregisterCallback<GeometryChangedEvent>(OnDetailsGeometryChanged);
            (scan as IDisposable)?.Dispose();
            EditorApplication.projectChanged -= OnProjectChanged;
            content.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            modal.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            if (hide && modal.panel != null)
            {
                modal.Hide();
                content.RegisterCallback<DetachFromPanelEvent>(ReleaseStylingOnDetach);
                modal.schedule.Execute(() =>
                {
                    content.UnregisterCallback<DetachFromPanelEvent>(ReleaseStylingOnDetach);
                    if (ReferenceEquals(modal.Content, content)) modal.RemoveFromClassList("delete-prefab-dialog");
                }).StartingIn(160);
            }
            else
            {
                if (hide) modal.Hide();
                modal.RemoveFromClassList("delete-prefab-dialog");
            }
            if (previousFocus?.panel != null) previousFocus.Focus();
            try
            {
                completed?.Invoke(deleted);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void ReleaseStylingOnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target != content) return;
            content.UnregisterCallback<DetachFromPanelEvent>(ReleaseStylingOnDetach);
            modal.RemoveFromClassList("delete-prefab-dialog");
        }
    }
}
