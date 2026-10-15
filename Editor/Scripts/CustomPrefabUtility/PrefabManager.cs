using EAUploader.Components;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using VRC.SDK3.Avatars.Components;

namespace EAUploader.CustomPrefabUtility
{
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

        // 関連アセット削除の判定で参照元として走査するアセットの拡張子
        private static readonly string[] REFERENCER_EXTENSIONS =
            { ".prefab", ".unity", ".controller", ".overridecontroller", ".anim", ".asset", ".mat" };

        // 移行処理などで、取り込みを検知してもVariantを作りたくない間だけtrueにする
        internal static bool SuppressAutoVariantCreation;

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

        /// <summary>
        /// 取り込まれたPrefabのVariantを作って管理下に置く。
        /// </summary>
        private static string ImportSourcePrefab(string sourcePrefabPath)
        {
            if (SuppressAutoVariantCreation) return null;

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

        public static bool ShowDeletePrefabDialog(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;

            var itemFolder = ManagedPrefabs.GetItemFolder(prefabPath);

            // 他に同じ取り込み元のVariantが残らない場合だけ、取り込み元も一緒に消せるようにする
            var sourcePath = ManagedPrefabs.GetSourcePath(prefabPath);
            var sourceIsOrphaned = sourcePath != null
                && ManagedPrefabs.FindVariantsOf(sourcePath).All(p => p == prefabPath);

            var deleteTargets = new List<string> { prefabPath };
            if (sourceIsOrphaned) deleteTargets.Add(sourcePath);

            // 削除対象からしか参照されていないMaterial/Textureを一緒に消せるようにする
            List<string> orphanedAssets;
            try
            {
                EditorUtility.DisplayProgressBar(
                    T7e.Get("Delete Prefab"),
                    T7e.Get("Looking for related assets..."),
                    0.5f);
                orphanedAssets = FindAssetsOnlyUsedBy(deleteTargets);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var message = T7e.Get("Are you sure you want to delete this prefab?") + "\n\n" +
                          Path.GetFileNameWithoutExtension(prefabPath);

            if (sourceIsOrphaned)
            {
                message += "\n\n" +
                    T7e.Get("The imported prefab it was created from is no longer used and will also be deleted:") +
                    "\n" + sourcePath;
            }

            if (orphanedAssets.Count > 0)
            {
                message += "\n\n" +
                    T7e.Get("The following related assets are used only by this prefab and will also be deleted:") +
                    $" ({orphanedAssets.Count})\n" + BuildAssetListPreview(orphanedAssets);
            }

            if (!EditorUtility.DisplayDialog(T7e.Get("Delete Prefab"), message, T7e.Get("Delete"), T7e.Get("Cancel")))
            {
                return false;
            }

            DeletePrefabPreview(prefabPath);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var assetPath in orphanedAssets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                foreach (var assetPath in deleteTargets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // 中身が無くなったアイテムフォルダは片付ける
            ManagedPrefabs.DeleteItemFolderIfEmpty(itemFolder);

            AssetDatabase.Refresh();

            if (EAUploaderCore.selectedPrefabPath == prefabPath)
            {
                EAUploaderCore.selectedPrefabPath = null;
            }

            InvalidateCache();
            return true;
        }

        private static string BuildAssetListPreview(List<string> assetPaths)
        {
            const int MAX_LINES = 15;
            var lines = assetPaths.Take(MAX_LINES).Select(Path.GetFileName).ToList();
            if (assetPaths.Count > MAX_LINES)
            {
                lines.Add("... (+" + (assetPaths.Count - MAX_LINES) + ")");
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 指定Prefabが参照しているMaterial/Textureのうち、
        /// プロジェクト内の他のアセットからは参照されていないものを返す。
        /// </summary>
        internal static List<string> FindAssetsOnlyUsedBy(string prefabPath)
        {
            return FindAssetsOnlyUsedBy(new[] { prefabPath });
        }

        /// <summary>
        /// これから削除するアセット群が参照しているMaterial/Textureのうち、
        /// 他のアセットからは参照されていないものを返す。
        /// </summary>
        internal static List<string> FindAssetsOnlyUsedBy(IEnumerable<string> deletedPaths)
        {
            var deleted = new HashSet<string>(deletedPaths.Where(p => !string.IsNullOrEmpty(p)));
            if (deleted.Count == 0) return new List<string>();

            try
            {
                var candidates = new HashSet<string>(
                    AssetDatabase.GetDependencies(deleted.ToArray(), true)
                        .Where(p => !deleted.Contains(p))
                        .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                        .Where(IsDeletableDependency));

                if (candidates.Count == 0) return new List<string>();

                // 削除候補と削除対象以外の全アセットを参照元として、依存関係を一括取得する
                var referencers = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Where(p => !deleted.Contains(p) && !candidates.Contains(p))
                    .Where(p => REFERENCER_EXTENSIONS.Contains(
                        (Path.GetExtension(p) ?? string.Empty).ToLowerInvariant()))
                    .ToArray();

                var used = referencers.Length > 0
                    ? new HashSet<string>(AssetDatabase.GetDependencies(referencers, true))
                    : new HashSet<string>();

                return candidates.Where(p => !used.Contains(p)).OrderBy(p => p).ToList();
            }
            catch (Exception e)
            {
                // 依存関係の解決に失敗した場合は関連アセットを消さず、Prefabのみの削除にフォールバックする
                Debug.LogError($"Failed to resolve related assets: {e.Message}");
                return new List<string>();
            }
        }

        private static bool IsDeletableDependency(string assetPath)
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

            meta.status = (meta.status == EAUploaderMeta.PrefabStatus.Pinned)
                ? EAUploaderMeta.PrefabStatus.Show
                : EAUploaderMeta.PrefabStatus.Pinned;

            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            InvalidateCache();
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
                EditorUtility.SetDirty(meta);
                AssetDatabase.SaveAssets();
                InvalidateCache();
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
