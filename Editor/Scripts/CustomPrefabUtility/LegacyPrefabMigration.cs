using EAUploader.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EAUploader.CustomPrefabUtility
{
    /// <summary>
    /// 旧バージョンの管理方式からの移行。
    ///
    /// 以前は取り込んだPrefabそのものに EAUploaderMeta を付けて管理していたが、
    /// 現在は Variant を作って <see cref="ManagedPrefabs.ROOT"/> 以下で管理する。
    /// 既存プロジェクトの一覧が空にならないよう、旧方式のPrefabからVariantを作り直し、
    /// 取り込み元のPrefabからは EAUploaderMeta を取り除いて元の状態に戻す。
    /// </summary>
    internal static class LegacyPrefabMigration
    {
        /// <summary>
        /// 旧方式のPrefabから引き継ぐメタ情報。
        /// </summary>
        private class LegacyMeta
        {
            public EAUploaderMeta.PrefabStatus Status;
            public EAUploaderMeta.PrefabType Type;
            public EAUploaderMeta.PrefabGenre Genre;
            public long LastModifiedTicks;
            public bool GenreConfirmed;
            public string Memo;
            public List<string> Tags = new List<string>();
        }

        public static void RunIfNeeded()
        {
            var settings = Config.LoadSettings();
            if (settings.managedPrefabsMigrated) return;

            var legacyPaths = FindLegacyPrefabs();

            if (legacyPaths.Count == 0)
            {
                MarkDone();
                return;
            }

            // T7e.Get は見つかった翻訳に string.Format をかけるため、キー側に {0} を持たせない。
            // 英語(翻訳なし)ではキーがそのまま表示されるので、値は連結して組み立てる。
            var message =
                T7e.Get("EAUploader now manages prefabs as variants in the following folder:") +
                "\n" + ManagedPrefabs.ROOT +
                "\n\n" +
                T7e.Get("Number of prefabs to move to the new management folder:") +
                " " + legacyPaths.Count +
                "\n\n" +
                T7e.Get("The original prefabs are left as they are, apart from removing the information EAUploader added to them.");

            if (!EditorUtility.DisplayDialog(
                    T7e.Get("EAUploader"), message, T7e.Get("Migrate"), T7e.Get("Later")))
            {
                // 次回起動時にもう一度尋ねる
                return;
            }

            Migrate(legacyPaths);
            MarkDone();
        }

        private static void MarkDone()
        {
            var settings = Config.LoadSettings();
            settings.managedPrefabsMigrated = true;
            Config.SaveSettings(settings);
        }

        /// <summary>
        /// 管理フォルダの外にあり、EAUploaderMeta が付いているPrefabを探す。
        /// </summary>
        private static List<string> FindLegacyPrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(p => !ManagedPrefabs.IsInEAUploaderFolder(p))
                .Where(HasMeta)
                .OrderBy(p => p)
                .ToList();
        }

        private static bool HasMeta(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null && prefab.GetComponent<EAUploaderMeta>() != null;
        }

        private static void Migrate(List<string> legacyPaths)
        {
            // 移行中はAssetPostprocessorが勝手にVariantを作らないようにする
            PrefabManager.SuppressAutoVariantCreation = true;

            try
            {
                for (int i = 0; i < legacyPaths.Count; i++)
                {
                    var path = legacyPaths[i];

                    EditorUtility.DisplayProgressBar(
                        T7e.Get("EAUploader"),
                        Path.GetFileNameWithoutExtension(path),
                        (float)i / legacyPaths.Count);

                    try
                    {
                        MigrateOne(path);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to migrate {path}: {e.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                PrefabManager.SuppressAutoVariantCreation = false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PrefabManager.InvalidateCache();
        }

        private static void MigrateOne(string sourcePath)
        {
            var legacy = ReadLegacyMeta(sourcePath);
            if (legacy == null) return;

            // 先に取り込み元からメタ情報を取り除く。
            // Variantを作ってから消すと、継承しているメタ情報ごと消えてしまう。
            RemoveMetaFromSource(sourcePath);

            if (ManagedPrefabs.HasVariantOf(sourcePath)) return;

            var variantPath = ManagedPrefabs.CreateVariant(sourcePath, meta =>
            {
                meta.status = legacy.Status;
                meta.type = legacy.Type;
                meta.genre = legacy.Genre;
                meta.lastModifiedTicks = legacy.LastModifiedTicks;
                meta.genreConfirmed = legacy.GenreConfirmed;
                meta.memo = legacy.Memo;
                meta.tags = new List<string>(legacy.Tags);
            });

            if (variantPath == null) return;

            MovePreview(sourcePath, variantPath);
        }

        private static LegacyMeta ReadLegacyMeta(string sourcePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var meta = prefab != null ? prefab.GetComponent<EAUploaderMeta>() : null;
            if (meta == null) return null;

            return new LegacyMeta
            {
                Status = meta.status,
                Type = meta.type,
                Genre = meta.genre,
                LastModifiedTicks = meta.lastModifiedTicks,
                GenreConfirmed = meta.genreConfirmed,
                Memo = meta.memo,
                Tags = meta.tags != null ? new List<string>(meta.tags) : new List<string>()
            };
        }

        /// <summary>
        /// 取り込み元のPrefabから EAUploaderMeta を取り除き、EAUploader導入前の状態に戻す。
        /// </summary>
        private static void RemoveMetaFromSource(string sourcePath)
        {
            var contents = PrefabUtility.LoadPrefabContents(sourcePath);
            if (contents == null) return;

            try
            {
                var metas = contents.GetComponents<EAUploaderMeta>();
                if (metas.Length == 0) return;

                foreach (var meta in metas)
                {
                    Object.DestroyImmediate(meta, true);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, sourcePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 旧方式の(名前で保存されていた)プレビュー画像を、新しいVariantのものとして引き継ぐ。
        /// </summary>
        private static void MovePreview(string sourcePath, string variantPath)
        {
            var oldPath = Path.Combine("Assets/EAUploader/PrefabPreviews",
                Path.GetFileNameWithoutExtension(sourcePath) + ".png");
            if (!File.Exists(oldPath)) return;

            var newPath = PrefabPreview.GetPreviewImagePath(variantPath);
            if (oldPath == newPath || File.Exists(newPath)) return;

            try
            {
                File.Copy(oldPath, newPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to carry over the preview image of {sourcePath}: {e.Message}");
            }
        }
    }
}
