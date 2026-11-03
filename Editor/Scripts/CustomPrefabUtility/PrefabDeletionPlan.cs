using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace EAUploader.CustomPrefabUtility
{
    internal sealed class PrefabDeletionPlan
    {
        private static readonly HashSet<string> ReferencerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".prefab", ".unity", ".controller", ".overridecontroller", ".anim", ".asset", ".mat" };
        private static readonly Dictionary<string, PrefabDeletionPlan> cachedPlans =
            new Dictionary<string, PrefabDeletionPlan>();
        private static uint cachedDependencyVersion;
        private static int projectRevision;

        internal sealed class RelatedAsset
        {
            internal string Path;
            internal string Guid;
            internal bool IsSource;
            internal string[] Dependencies;
        }

        internal readonly string PrefabPath;
        internal readonly string PrefabGuid;
        internal readonly List<RelatedAsset> Assets = new List<RelatedAsset>();
        internal readonly HashSet<string> UsedElsewhere = new HashSet<string>();
        internal float Progress { get; private set; }
        private bool scanComplete;
        private uint dependencyVersion;
        private int scanRevision;
        internal bool IsCurrent => scanComplete && scanRevision == projectRevision &&
            dependencyVersion == AssetDatabase.GlobalArtifactDependencyVersion;

        static PrefabDeletionPlan()
        {
            EditorApplication.projectChanged += InvalidateCache;
        }

        internal static void InvalidateCache()
        {
            projectRevision++;
            cachedPlans.Clear();
        }

        internal PrefabDeletionPlan(string prefabPath)
        {
            PrefabPath = prefabPath;
            PrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath, AssetPathToGUIDOptions.OnlyExistingAssets);
        }

        internal IEnumerator Scan()
        {
            if (!IsValidPrefab())
                throw new InvalidOperationException("The prefab no longer exists or cannot be deleted.");

            dependencyVersion = AssetDatabase.GlobalArtifactDependencyVersion;
            if (cachedDependencyVersion != dependencyVersion)
            {
                cachedDependencyVersion = dependencyVersion;
                InvalidateCache();
            }
            scanRevision = projectRevision;
            if (cachedPlans.TryGetValue(PrefabPath, out var cached) && cached.IsCurrent && cached.PrefabGuid == PrefabGuid)
            {
                Assets.AddRange(cached.Assets);
                UsedElsewhere.UnionWith(cached.UsedElsewhere);
                Progress = 1;
                scanComplete = true;
                yield break;
            }

            yield return null;
            var source = ManagedPrefabs.GetSourcePath(PrefabPath);
            var dependencies = AssetDatabase.GetDependencies(PrefabPath, true);
            foreach (var path in dependencies.OrderBy(p => p))
            {
                if (path == PrefabPath || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                var isSource = path == source && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                if (!isSource && !PrefabManager.IsDeletableDependency(path)) continue;
                Assets.Add(new RelatedAsset
                {
                    Path = path,
                    Guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets),
                    IsSource = isSource
                });
            }

            if (Assets.Count == 0)
            {
                CompleteScan();
                yield break;
            }

            var candidates = new HashSet<string>(Assets.Select(a => a.Path));
            foreach (var asset in Assets)
            {
                asset.Dependencies = AssetDatabase.GetDependencies(asset.Path, true)
                    .Where(p => p != asset.Path && candidates.Contains(p)).ToArray();
                yield return null;
            }

            var referencers = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                            p != PrefabPath && !candidates.Contains(p) &&
                            ReferencerExtensions.Contains(Path.GetExtension(p)))
                .ToArray();
            yield return null;
            if (referencers.Length > 0)
            {
                foreach (var path in AssetDatabase.GetDependencies(referencers, true))
                    if (candidates.Contains(path)) UsedElsewhere.Add(path);
            }
            CompleteScan();
        }

        private void CompleteScan()
        {
            if (dependencyVersion != AssetDatabase.GlobalArtifactDependencyVersion || scanRevision != projectRevision)
                throw new InvalidOperationException("The project changed while checking related assets. Please search again.");
            Progress = 1;
            scanComplete = true;
            if (cachedPlans.Count >= 16) cachedPlans.Clear();
            cachedPlans[PrefabPath] = this;
        }

        internal HashSet<string> GetBlockedAssets(HashSet<string> selected)
        {
            var blocked = new HashSet<string>(UsedElsewhere);
            foreach (var asset in Assets)
                if (!selected.Contains(asset.Path) || UsedElsewhere.Contains(asset.Path))
                    blocked.UnionWith(asset.Dependencies);
            return blocked;
        }

        internal void Delete(HashSet<string> selected)
        {
            if (selected.Count > 0 && !IsCurrent)
                throw new InvalidOperationException("Related assets have not been checked yet.");
            if (!IsValidPrefab())
                throw new InvalidOperationException("The prefab has changed. Please open the delete dialog again.");

            var blocked = selected.Count > 0 ? GetBlockedAssets(selected) : new HashSet<string>();
            var extras = Assets.Where(a => selected.Contains(a.Path)).ToList();
            if (extras.Count != selected.Count || extras.Any(a => blocked.Contains(a.Path) ||
                string.IsNullOrEmpty(a.Guid) || AssetDatabase.AssetPathToGUID(a.Path, AssetPathToGUIDOptions.OnlyExistingAssets) != a.Guid))
                throw new InvalidOperationException("The related assets have changed. Please search again.");

            var previewPath = PrefabPreview.GetPreviewImagePath(PrefabPath).Replace('\\', '/');
            var prefabDeleted = false;
            var previousSuppression = PrefabManager.SuppressAutoVariantCreation;
            PrefabManager.SuppressAutoVariantCreation = true;
            AssetDatabase.StartAssetEditing();
            try
            {
                if (!AssetDatabase.DeleteAsset(PrefabPath))
                    throw new IOException("Could not delete: " + PrefabPath);
                prefabDeleted = true;

                if (File.Exists(previewPath) && !AssetDatabase.DeleteAsset(previewPath))
                    throw new IOException("Could not delete: " + previewPath);

                foreach (var asset in extras.OrderBy(a => a.IsSource ? 0 :
                    Path.GetExtension(a.Path).Equals(".mat", StringComparison.OrdinalIgnoreCase) ? 1 : 2))
                    if (!AssetDatabase.DeleteAsset(asset.Path))
                        throw new IOException("Could not delete: " + asset.Path);

            }
            finally
            {
                try
                {
                    AssetDatabase.StopAssetEditing();
                }
                finally
                {
                    PrefabManager.SuppressAutoVariantCreation = previousSuppression;
                    if (prefabDeleted && EAUploaderCore.selectedPrefabPath == PrefabPath)
                        EAUploaderCore.selectedPrefabPath = null;
                    PrefabManager.InvalidateCache();
                    UI.AvatarList.Main.RequestRefresh();
                }
            }
            ManagedPrefabs.DeleteItemFolderIfEmpty(ManagedPrefabs.GetItemFolder(PrefabPath));
        }

        private bool IsValidPrefab() => !string.IsNullOrEmpty(PrefabGuid) &&
            PrefabPath.StartsWith("Assets/", StringComparison.Ordinal) &&
            PrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
            AssetDatabase.AssetPathToGUID(PrefabPath, AssetPathToGUIDOptions.OnlyExistingAssets) == PrefabGuid;
    }
}
