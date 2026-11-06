using EAUploader.Components;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using VRC.SDK3.Avatars.Components;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.CustomPrefabUtility
{
    [InitializeOnLoad]
    public static class PrefabManager
    {
        // キャッシュフィールド
        private static List<PrefabInfo> cachedPrefabs;

        // リスト更新タイム
        private static double lastUpdateTime = 0.0;

        // 再スキャンまでのインターバル
        private const double REFRESH_INTERVAL = 2.0;

        // 自身の保存が誘発する再インポートでインポート日時を上書きしないための猶予(秒)
        private const double REIMPORT_DEBOUNCE_SECONDS = 5.0;

        // 移行処理などで、取り込みを検知してもVariantを作りたくない間だけtrueにする
        internal static bool SuppressAutoVariantCreation;

        private const double METADATA_SAVE_DELAY_SECONDS = 0.3;
        private static readonly Dictionary<EAUploaderMeta, double> pendingPrefabSaves = new Dictionary<EAUploaderMeta, double>();
        private static readonly HashSet<string> metadataSavePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static PrefabManager()
        {
            EditorApplication.update += PersistPendingMetadata;
            AssemblyReloadEvents.beforeAssemblyReload += FlushPendingMetadataSaves;
            EditorApplication.quitting += FlushPendingMetadataSaves;
        }

        internal static void ScheduleMetadataSave(EAUploaderMeta meta)
        {
            if (!meta) return;
            EditorUtility.SetDirty(meta);
            pendingPrefabSaves[meta] = EditorApplication.timeSinceStartup + METADATA_SAVE_DELAY_SECONDS;
            UpdateCachedMetadata(meta);
        }

        internal static bool IsMetadataSaveInProgress(string path) => metadataSavePaths.Contains(path);

        private static void PersistPendingMetadata()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var now = EditorApplication.timeSinceStartup;
            var due = pendingPrefabSaves.FirstOrDefault(entry => entry.Value <= now);
            if (!ReferenceEquals(due.Key, null)) SavePendingPrefab(due.Key);
        }

        private static void SavePendingPrefab(EAUploaderMeta meta)
        {
            pendingPrefabSaves.Remove(meta);
            if (!meta || !EditorUtility.IsDirty(meta)) return;
            var path = AssetDatabase.GetAssetPath(meta);
            if (string.IsNullOrEmpty(path)) return;

            metadataSavePaths.Add(path);
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    AssetDatabase.SaveAssetIfDirty(meta);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
            catch (Exception e)
            {
                pendingPrefabSaves[meta] = EditorApplication.timeSinceStartup + 1.0;
                Debug.LogError($"Error saving item metadata for {path}: {e.Message}");
            }
            finally
            {
                metadataSavePaths.Remove(path);
            }
        }

        private static void FlushPendingMetadataSaves()
        {
            foreach (var meta in pendingPrefabSaves.Keys.ToList()) SavePendingPrefab(meta);
        }

        public static void Initialize()
        {
            ManagedPrefabs.EnsureRoot();
            LegacyPrefabMigration.RunIfNeeded();
            PrefabPreview.GenerateAndSaveAllPrefabPreviews();
        }

        public static void UpdatePrefabInfo()
        {
            Initialize();
        }

        /// <summary>
        /// キャッシュを破棄し、次回取得時に必ず再スキャンさせる。
        /// </summary>
        public static void InvalidateCache()
        {
            cachedPrefabs = null;
            lastUpdateTime = 0.0;
            TagManager.InvalidateTagCache();
        }

        private static void UpdateCachedMetadata(EAUploaderMeta meta)
        {
            var prefabPath = AssetDatabase.GetAssetPath(meta);
            var info = cachedPrefabs?.Find(prefab => prefab.Path == prefabPath);
            if (info == null) return;
            info.Status = meta.status;
            info.Genre = meta.genre;
            info.Type = meta.type;
            info.Tags = meta.tags != null ? new List<string>(meta.tags) : new List<string>();
            info.Memo = meta.memo;
        }

        /// <summary>
        /// キャッシュ上のプレビュー画像を差し替える。
        /// プレビュー再生成後にリスト表示を即座に追従させるために使う。
        /// </summary>
        internal static void UpdateCachedPreview(string prefabPath, Texture2D preview)
        {
            var info = cachedPrefabs?.Find(p => p.Path == prefabPath);
            if (info != null)
            {
                info.Preview = preview;
            }
        }

        /// <summary>
        /// Prefabを取り込む。
        ///
        /// 管理下(<see cref="ManagedPrefabs.ROOT"/>以下)のPrefabであればメタ情報とプレビューを更新し、
        /// それ以外のPrefabが取り込まれた場合はVariantを作って管理下に置く。
        /// 取り込み元のPrefabそのものには手を加えない。
        /// </summary>
        /// <returns>インポート時のジャンル選択が必要なPrefabのパス。不要な場合はnull</returns>
        public static string ImportPrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return null;

            return ManagedPrefabs.IsManaged(prefabPath)
                ? RefreshManagedPrefab(prefabPath)
                : ImportSourcePrefab(prefabPath);
        }

        private static bool IsSdkTemporaryBuildPrefab(string prefabPath)
        {
            if (AvatarUploader.IsUploading || AvatarUploader.IsMultiPlatformBuildInProgress) return true;

            return Path.GetFileName(prefabPath).StartsWith("prefab-id-v", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 取り込まれたPrefabのVariantを作って管理下に置く。
        /// </summary>
        private static string ImportSourcePrefab(string sourcePrefabPath)
        {
            if (SuppressAutoVariantCreation) return null;

            // VRChat SDKはビルド中に一時的なPrefab(prefab-id-v1_...)を作り、ビルド後に消す。
            // これを取り込むと、元が消えた壊れたVariantが管理フォルダに残るため対象外にする。
            if (IsSdkTemporaryBuildPrefab(sourcePrefabPath)) return null;

            // EAUploader自身が使うフォルダの中身(プレビュー画像など)は対象外
            if (ManagedPrefabs.IsInEAUploaderFolder(sourcePrefabPath)) return null;

            // Assets以外(Packagesなど)は取り込み対象にしない
            if (!sourcePrefabPath.StartsWith("Assets/", StringComparison.Ordinal)) return null;

            var source = GetPrefab(sourcePrefabPath);
            if (source == null) return null;

            // 同じPrefabを再度取り込んだ場合にVariantが増えないようにする
            if (ManagedPrefabs.HasVariantOf(sourcePrefabPath)) return null;

            var type = GetPrefabType(sourcePrefabPath);
            var isAvatar = type == EAUploaderMeta.PrefabType.VRChat;

            var variantPath = ManagedPrefabs.CreateVariant(sourcePrefabPath, meta =>
            {
                meta.type = type;
                meta.lastModifiedTicks = DateTime.UtcNow.Ticks;

                // VRCAvatarDescriptorを持つ場合はアバター確定。ジャンル選択はスルーする。
                // それ以外は衣装をデフォルトにして、インポート時に選択させる。
                meta.genre = isAvatar ? EAUploaderMeta.PrefabGenre.Avatar : EAUploaderMeta.PrefabGenre.Cloth;
                meta.genreConfirmed = isAvatar;
            });

            if (variantPath == null) return null;

            SavePreview(variantPath);

            InvalidateCache();
            UI.AvatarList.Main.RequestRefresh();

            return isAvatar ? null : variantPath;
        }

        /// <summary>
        /// 管理下のPrefabのメタ情報とプレビューを更新する。
        /// </summary>
        private static string RefreshManagedPrefab(string prefabPath)
        {
            GameObject prefab = GetPrefab(prefabPath);
            if (prefab == null) return null;

            var meta = prefab.GetComponent<EAUploaderMeta>();
            if (meta == null)
            {
                // 管理フォルダ内で手作業で複製された場合など
                meta = Utility.GetEAUploaderMeta(prefab);
                if (meta == null) return null;
            }

            var type = GetPrefabType(prefabPath);
            bool isAvatar = type == EAUploaderMeta.PrefabType.VRChat;
            bool needsGenreSelection = false;
            bool dirty = false;

            if (meta.type != type)
            {
                meta.type = type;
                dirty = true;
            }

            if (isAvatar)
            {
                if (meta.genre != EAUploaderMeta.PrefabGenre.Avatar)
                {
                    meta.genre = EAUploaderMeta.PrefabGenre.Avatar;
                    dirty = true;
                }
                if (!meta.genreConfirmed)
                {
                    meta.genreConfirmed = true;
                    dirty = true;
                }
            }
            else if (!meta.genreConfirmed)
            {
                if (meta.genre != EAUploaderMeta.PrefabGenre.Cloth)
                {
                    meta.genre = EAUploaderMeta.PrefabGenre.Cloth;
                    dirty = true;
                }
                needsGenreSelection = true;
            }

            // インポート日時を更新する。
            // ファイルのタイムスタンプはコピーやunitypackage展開で古い日時を引き継ぐため、
            // EAUploaderが取り込んだ時刻を別途保持して一覧の並び替えに使う。
            //
            // ここでのSaveAssetsはPrefabを書き換えるため、AssetPostprocessor経由で
            // このメソッドが再度呼ばれる。毎回新しい時刻を書き込むと書き換えが収束しないので、
            // 直前に記録した時刻から一定時間が経っている場合だけ更新する。
            var nowUtc = DateTime.UtcNow;
            var recordedUtc = meta.lastModifiedTicks > 0
                ? new DateTime(meta.lastModifiedTicks, DateTimeKind.Utc)
                : DateTime.MinValue;

            if ((nowUtc - recordedUtc).TotalSeconds > REIMPORT_DEBOUNCE_SECONDS)
            {
                meta.lastModifiedTicks = nowUtc.Ticks;
                dirty = true;
            }

            if (!dirty)
            {
                // 自分自身の保存によって発生した再インポート。何もしない。
                return null;
            }

            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();

            SavePreview(prefabPath);

            InvalidateCache();
            UI.AvatarList.Main.RequestRefresh();

            return needsGenreSelection ? prefabPath : null;
        }

        private static void SavePreview(string prefabPath)
        {
            var prefab = GetPrefab(prefabPath);
            if (prefab == null) return;

            Texture2D preview = PrefabPreview.GeneratePreview(prefab);
            PrefabPreview.SavePrefabPreview(prefabPath, preview);
        }

        /// <summary>
        /// リストをキャッシュし、一定時間以内であれば再利用
        /// </summary>
        internal static List<PrefabInfo> GetAllPrefabs()
        {
            double now = EditorApplication.timeSinceStartup;
            // キャッシュが未作成、あるいは一定時間経過したら再スキャン
            if (cachedPrefabs == null || (now - lastUpdateTime) > REFRESH_INTERVAL)
            {
                cachedPrefabs = ScanAllPrefabs();
                lastUpdateTime = now;
            }
            return cachedPrefabs;
        }

        /// <summary>
        /// 管理下のPrefabを走査する。
        /// 管理フォルダの外にあるPrefabは読み書きしない。
        /// </summary>
        private static List<PrefabInfo> ScanAllPrefabs()
        {
            var result = new List<PrefabInfo>();

            foreach (var path in ManagedPrefabs.GetAllVariantPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // 走査でメタ情報を付け足さない。EAUploaderが作ったVariantだけを対象にする。
                var meta = prefab.GetComponent<EAUploaderMeta>();
                if (meta == null) continue;

                var oldType = meta.type;
                var newType = GetPrefabType(path);
                if (oldType != newType)
                {
                    meta.type = newType;
                    EditorUtility.SetDirty(meta);
                    AssetDatabase.SaveAssets();
                }

                var info = new PrefabInfo
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    LastModified = GetLastModified(path, meta),
                    Type = meta.type,
                    Status = meta.status,
                    Genre = meta.genre,
                    Tags = meta.tags != null ? new List<string>(meta.tags) : new List<string>(),
                    Memo = meta.memo
                };
                result.Add(info);
            }

            // 更新日時でソート
            result = result.OrderBy(p => p.LastModified).ToList();
            return result;
        }

        /// <summary>
        /// EAUploaderが記録したインポート日時を優先し、無ければファイルの更新日時を使う。
        /// </summary>
        private static DateTime GetLastModified(string path, EAUploaderMeta meta)
        {
            if (meta && meta.lastModifiedTicks > 0)
            {
                return new DateTime(meta.lastModifiedTicks, DateTimeKind.Utc).ToLocalTime();
            }
            return File.GetLastWriteTime(path);
        }

        private static Texture2D TryLoadPreview(string path)
        {
            string previewImagePath = PrefabPreview.GetPreviewImagePath(path);
            if (File.Exists(previewImagePath))
            {
                return PrefabPreview.LoadTextureFromFile(previewImagePath);
            }
            return null;
        }

        public static List<PrefabInfo> GetAllPrefabsWithPreview()
        {
            var allPrefabs = GetAllPrefabs()
                .Where(p => p.Status != EAUploaderMeta.PrefabStatus.Hidden)
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var info in allPrefabs)
            {
                if (info.Preview == null)
                {
                    info.Preview = TryLoadPreview(info.Path);
                }
            }

            return allPrefabs;
        }

        public static List<PrefabInfo> GetAllPrefabsIncludingHidden()
        {
            var allPrefabs = GetAllPrefabs()
                .OrderByDescending(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned)
                .ThenByDescending(p => p.LastModified)
                .ToList();

            foreach (var info in allPrefabs)
            {
                if (info.Preview == null)
                {
                    info.Preview = TryLoadPreview(info.Path);
                }
            }

            return allPrefabs;
        }

        public static PrefabInfo GetPrefabInfo(string path)
        {
            var all = GetAllPrefabs();
            var info = all.Find(p => p.Path == path);
            if (info == null) return null;

            if (info.Preview == null)
            {
                info.Preview = TryLoadPreview(path);
            }
            return info;
        }

        public static VRCAvatarDescriptor GetAvatarDescriptor(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                return prefab.GetComponent<VRCAvatarDescriptor>();
            }
            return null;
        }

        /// <summary>旧プラグイン向けの同期確認。削除が完了した場合だけtrueを返す。</summary>
        [Obsolete("Use the callback overload for the in-window UI Toolkit confirmation.")]
        public static bool ShowDeletePrefabDialog(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || GetPrefab(prefabPath) == null)
                return false;

            var plan = new PrefabDeletionPlan(prefabPath);
            var scan = plan.Scan();
            try
            {
                while (scan.MoveNext()) { }
            }
            finally
            {
                (scan as IDisposable)?.Dispose();
            }

            var targets = new HashSet<string>(plan.Assets.Where(a => !plan.UsedElsewhere.Contains(a.Path)).Select(a => a.Path));
            targets.ExceptWith(plan.GetBlockedAssets(targets));
            var message = T7e.Get("Are you sure you want to delete this prefab?") + "\n\n" + prefabPath;
            if (targets.Count > 0)
                message += "\n\n" + T7e.Get("The following related assets are used only by this prefab and will also be deleted:") +
                    "\n" + string.Join("\n", targets.OrderBy(p => p));
            if (!EditorUtility.DisplayDialog(T7e.Get("Delete Prefab"), message, T7e.Get("Delete"), T7e.Get("Cancel")))
                return false;

            plan.Delete(targets);
            return true;
        }

        /// <summary>削除確認モーダルを開き、削除完了時またはキャンセル時に結果を通知する。</summary>
        public static bool ShowDeletePrefabDialog(string prefabPath, Action<bool> completed)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;
            return new UI.Modals.DeletePrefabModal(prefabPath, completed).Open();
        }

        internal static List<string> FindAssetsOnlyUsedBy(string prefabPath)
        {
            var plan = new PrefabDeletionPlan(prefabPath);
            var scan = plan.Scan();
            try
            {
                while (scan.MoveNext()) { }
                var selected = new HashSet<string>(plan.Assets.Where(a => !a.IsSource).Select(a => a.Path));
                selected.ExceptWith(plan.GetBlockedAssets(selected));
                return selected.OrderBy(p => p).ToList();
            }
            finally
            {
                (scan as IDisposable)?.Dispose();
            }
        }

        internal static bool IsDeletableDependency(string assetPath)
        {
            var extension = (Path.GetExtension(assetPath) ?? string.Empty).ToLowerInvariant();
            switch (extension)
            {
                case ".mat":
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".tif":
                case ".tiff":
                case ".bmp":
                case ".gif":
                case ".exr":
                case ".hdr":
                    return true;
                default:
                    return false;
            }
        }

        public static void DeletePrefabPreview(string prefabPath)
        {
            string previewImagePath = PrefabPreview.GetPreviewImagePath(prefabPath);
            if (File.Exists(previewImagePath))
            {
                File.Delete(previewImagePath);
            }
        }

        public static void PinPrefab(string prefabPath)
        {
            GameObject prefab = GetPrefab(prefabPath);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            var status = (meta.status == EAUploaderMeta.PrefabStatus.Pinned)
                ? EAUploaderMeta.PrefabStatus.Show
                : EAUploaderMeta.PrefabStatus.Pinned;

            SetPrefabStatus(prefabPath, status);
        }

        public static bool SetPrefabStatus(string prefabPath, EAUploaderMeta.PrefabStatus status)
        {
            var prefab = GetPrefab(prefabPath);
            if (prefab == null) return false;
            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null || meta.status == status) return false;

            meta.status = status;
            ScheduleMetadataSave(meta);
            return true;
        }

        public static bool IsPinned(string prefabPath)
        {
            var prefab = GetPrefab(prefabPath);
            if (prefab == null) return false;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return false;

            return meta.status == EAUploaderMeta.PrefabStatus.Pinned;
        }

        public static void SavePrefab(GameObject prefab, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            ImportPrefab(path);
        }

        public static void SetPrefabType(string path, EAUploaderMeta.PrefabType type)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            if (meta.type != type)
            {
                meta.type = type;
                EditorUtility.SetDirty(meta);
                AssetDatabase.SaveAssets();
                InvalidateCache();
            }
        }

        public static void SetPrefabGenre(string path, EAUploaderMeta.PrefabGenre genre)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            bool dirty = false;

            if (meta.genre != genre)
            {
                meta.genre = genre;
                dirty = true;
            }

            if (!meta.genreConfirmed)
            {
                meta.genreConfirmed = true;
                dirty = true;
            }

            if (dirty)
            {
                ScheduleMetadataSave(meta);
            }
        }

        /// <summary>
        /// ユーザーが記入したメモを取得する。
        /// </summary>
        public static string GetMemo(string path)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return string.Empty;

            var meta = Utility.GetEAUploaderMeta(prefab);
            return meta != null ? (meta.memo ?? string.Empty) : string.Empty;
        }

        /// <summary>
        /// メモを保存する。内容が変わっていない場合は何もしない。
        /// </summary>
        public static void SetMemo(string path, string memo)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return;

            var newMemo = memo ?? string.Empty;
            if ((meta.memo ?? string.Empty) == newMemo) return;

            meta.memo = newMemo;
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            InvalidateCache();
        }

        /// <summary>
        /// ジャンルを変更せずに「インポート時の選択が済んだ」ことだけを記録する。
        /// </summary>
        public static void MarkGenreConfirmed(string path)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null || meta.genreConfirmed) return;

            meta.genreConfirmed = true;
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            InvalidateCache();
        }

        public static void ChangePrefabGenre(string path, EAUploaderMeta.PrefabGenre newGenre)
        {
            SetPrefabGenre(path, newGenre);
        }

        public static void RenamePrefab(string path, string newName)
        {
            AssetDatabase.RenameAsset(path, newName);
            AssetDatabase.SaveAssets();
            InvalidateCache();
        }

        public static GameObject GetPrefab(string prefabPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static EAUploaderMeta.PrefabType GetPrefabType(string path)
        {
            GameObject prefab = GetPrefab(path);
            if (prefab != null)
            {
                if (prefab.GetComponent("VRC_AvatarDescriptor") != null)
                    return EAUploaderMeta.PrefabType.VRChat;
                if (prefab.GetComponent("VRMMeta") != null)
                    return EAUploaderMeta.PrefabType.VRM;
            }
            return EAUploaderMeta.PrefabType.Other;
        }

        public static EAUploaderMeta.PrefabGenre GetPrefabGenre(string path)
        {
            var prefab = GetPrefab(path);
            if (prefab == null) return EAUploaderMeta.PrefabGenre.Other;

            var meta = Utility.GetEAUploaderMeta(prefab);
            if (meta == null) return EAUploaderMeta.PrefabGenre.Other;

            return meta.genre;
        }
    }
}
