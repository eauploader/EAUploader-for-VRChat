using EAUploader.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EAUploader.CustomPrefabUtility
{
    /// <summary>
    /// タググループ1つ分の情報。<see cref="TagManager.GetGroupedTags"/> が返す。
    /// </summary>
    public class TagGroupInfo
    {
        /// <summary>グループ名。どのグループにも属さないタグの場合はnull。</summary>
        public string Name;

        public List<string> Tags = new List<string>();

        public bool IsUngrouped => Name == null;
    }

    /// <summary>
    /// ユーザーが自由に作れるタグを管理する。
    ///
    /// 「どんなタグ名が存在するか」「どのグループに属するか」はプロジェクト内のJSONに保存し、
    /// 「どのPrefabにどのタグが付いているか」は各Prefabの EAUploaderMeta.tags が持つ。
    /// タグ名とグループ名の比較は大文字小文字を区別しない。
    /// </summary>
    public static class TagManager
    {
        private const string TAGS_PATH = "Assets/EAUploader/tags.json";

        /// <summary>タグ名・グループ名の長さ上限。UIの表示崩れを防ぐための制限。</summary>
        public const int MAX_TAG_LENGTH = 32;

        [Serializable]
        private class TagGroupData
        {
            public string name;
            public List<string> tags = new List<string>();
        }

        [Serializable]
        private class TagRegistry
        {
            // 既知のタグ名すべて。グループ分けとは独立に持つ。
            public List<string> tags = new List<string>();

            // タグのグループ分け。1つのタグが属せるグループは1つだけ。
            public List<TagGroupData> groups = new List<TagGroupData>();
        }

        // JSONの読み込み結果。追加/削除のたびに破棄する。
        private static TagRegistry cachedRegistry;

        #region Tags

        /// <summary>
        /// 選択肢として提示するタグの一覧。
        /// 登録済みのタグに加えて、実際にPrefabで使われているタグも含める。
        /// </summary>
        public static List<string> GetAllTags()
        {
            var registry = LoadRegistry();
            var merged = new List<string>(registry.tags);

            // JSONを手で消した場合などでも、使用中のタグが候補から消えないようにする
            foreach (var info in PrefabManager.GetAllPrefabs())
            {
                if (info.Tags == null) continue;

                foreach (var tag in info.Tags)
                {
                    if (!Contains(merged, tag)) merged.Add(tag);
                }
            }

            merged.Sort(StringComparer.OrdinalIgnoreCase);
            return merged;
        }

        /// <summary>
        /// タグ名を一覧に登録する。
        /// </summary>
        /// <returns>登録された(あるいは既に存在した)正規化後のタグ名。無効な名前の場合はnull</returns>
        public static string RegisterTag(string tag)
        {
            var normalized = Normalize(tag);
            if (normalized == null) return null;

            var registry = LoadRegistry();

            // 既存のタグと大文字小文字だけが違う場合は、既存の表記に合わせる
            var existing = registry.tags.FirstOrDefault(
                t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            registry.tags.Add(normalized);
            SaveRegistry(registry);
            return normalized;
        }

        /// <summary>
        /// タグを一覧から削除し、そのタグが付いているPrefabからも取り除く。
        /// </summary>
        public static void DeleteTag(string tag)
        {
            var normalized = Normalize(tag);
            if (normalized == null) return;

            var registry = LoadRegistry();
            registry.tags.RemoveAll(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            foreach (var group in registry.groups)
            {
                group.tags.RemoveAll(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            }
            SaveRegistry(registry);

            foreach (var info in PrefabManager.GetAllPrefabs().ToList())
            {
                if (info.Tags == null || !Contains(info.Tags, normalized)) continue;
                RemoveTagFromPrefab(info.Path, normalized);
            }

            PrefabManager.InvalidateCache();
        }

        /// <summary>
        /// 指定のタグが付いているPrefabの数。
        /// </summary>
        public static int CountUsage(string tag)
        {
            var normalized = Normalize(tag);
            if (normalized == null) return 0;

            return PrefabManager.GetAllPrefabs()
                .Count(p => p.Tags != null && Contains(p.Tags, normalized));
        }

        #endregion

        #region Groups

        /// <summary>
        /// 登録されているグループ名の一覧。
        /// </summary>
        public static List<string> GetGroupNames()
        {
            return LoadRegistry().groups.Select(g => g.name).ToList();
        }

        /// <summary>
        /// タグをグループごとにまとめて返す。
        /// グループに属さないタグがある場合は、末尾に Name=null の項目として付く。
        /// </summary>
        public static List<TagGroupInfo> GetGroupedTags()
        {
            var registry = LoadRegistry();
            var allTags = GetAllTags();
            var result = new List<TagGroupInfo>();
            var grouped = new List<string>();

            foreach (var group in registry.groups)
            {
                // グループ側に残っているだけで実体の無いタグは表示しない
                var tags = allTags.Where(t => Contains(group.tags, t)).ToList();
                grouped.AddRange(tags);
                result.Add(new TagGroupInfo { Name = group.name, Tags = tags });
            }

            var ungrouped = allTags.Where(t => !Contains(grouped, t)).ToList();
            if (ungrouped.Count > 0)
            {
                result.Add(new TagGroupInfo { Name = null, Tags = ungrouped });
            }

            return result;
        }

        /// <summary>
        /// グループを作る。
        /// </summary>
        /// <returns>作成された(あるいは既に存在した)正規化後のグループ名。無効な名前の場合はnull</returns>
        public static string CreateGroup(string name)
        {
            var normalized = Normalize(name);
            if (normalized == null) return null;

            var registry = LoadRegistry();

            var existing = registry.groups.FirstOrDefault(
                g => string.Equals(g.name, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing.name;

            registry.groups.Add(new TagGroupData { name = normalized });
            SaveRegistry(registry);
            return normalized;
        }

        /// <summary>
        /// グループの名前を変える。
        /// </summary>
        public static void RenameGroup(string oldName, string newName)
        {
            var normalized = Normalize(newName);
            if (normalized == null || oldName == null) return;

            var registry = LoadRegistry();

            var group = registry.groups.FirstOrDefault(
                g => string.Equals(g.name, oldName, StringComparison.OrdinalIgnoreCase));
            if (group == null) return;

            // 同名のグループが既にある場合は何もしない
            if (registry.groups.Any(g => g != group
                    && string.Equals(g.name, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            group.name = normalized;
            SaveRegistry(registry);
        }

        /// <summary>
        /// グループを削除する。中のタグは削除せず、グループ未設定に戻す。
        /// </summary>
        public static void DeleteGroup(string name)
        {
            if (name == null) return;

            var registry = LoadRegistry();
            var removed = registry.groups.RemoveAll(
                g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return;

            SaveRegistry(registry);
        }

        /// <summary>
        /// タグの所属グループを変える。groupNameがnullの場合はグループ未設定にする。
        /// </summary>
        public static void SetTagGroup(string tag, string groupName)
        {
            var normalizedTag = RegisterTag(tag);
            if (normalizedTag == null) return;

            var registry = LoadRegistry();

            foreach (var group in registry.groups)
            {
                group.tags.RemoveAll(t => string.Equals(t, normalizedTag, StringComparison.OrdinalIgnoreCase));
            }

            if (groupName != null)
            {
                var target = registry.groups.FirstOrDefault(
                    g => string.Equals(g.name, groupName, StringComparison.OrdinalIgnoreCase));
                target?.tags.Add(normalizedTag);
            }

            SaveRegistry(registry);
        }

        /// <summary>
        /// タグが属しているグループ名。どこにも属していない場合はnull。
        /// </summary>
        public static string GetTagGroup(string tag)
        {
            if (tag == null) return null;

            return LoadRegistry().groups
                .FirstOrDefault(g => Contains(g.tags, tag))?.name;
        }

        #endregion

        #region Prefab tags

        public static List<string> GetTags(string prefabPath)
        {
            var meta = GetMeta(prefabPath);
            if (meta == null || meta.tags == null) return new List<string>();

            return meta.tags.Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        /// <summary>
        /// Prefabにタグを付ける。一覧に無いタグ名は同時に登録する。
        /// </summary>
        /// <returns>付与した場合はtrue。既に付いていた場合や無効な名前の場合はfalse</returns>
        public static bool AddTagToPrefab(string prefabPath, string tag)
        {
            var normalized = RegisterTag(tag);
            if (normalized == null) return false;

            var meta = GetMeta(prefabPath);
            if (meta == null) return false;

            if (meta.tags == null) meta.tags = new List<string>();
            if (Contains(meta.tags, normalized)) return false;

            meta.tags.Add(normalized);
            Save(meta);
            return true;
        }

        public static void RemoveTagFromPrefab(string prefabPath, string tag)
        {
            var normalized = Normalize(tag);
            if (normalized == null) return;

            var meta = GetMeta(prefabPath);
            if (meta == null || meta.tags == null) return;

            var removed = meta.tags.RemoveAll(
                t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return;

            Save(meta);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 入力されたタグ名/グループ名を保存できる形に整える。
        /// </summary>
        public static string Normalize(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            // 改行やタブが混ざると表示が崩れるため、空白は半角スペースにまとめる
            var normalized = new string(tag.Select(c => char.IsWhiteSpace(c) ? ' ' : c).ToArray()).Trim();
            if (normalized.Length == 0) return null;

            if (normalized.Length > MAX_TAG_LENGTH)
            {
                normalized = normalized.Substring(0, MAX_TAG_LENGTH).Trim();
            }

            return normalized.Length == 0 ? null : normalized;
        }

        public static bool Contains(IEnumerable<string> tags, string tag)
        {
            if (tags == null || tag == null) return false;
            return tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        }

        private static EAUploaderMeta GetMeta(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return null;

            var prefab = PrefabManager.GetPrefab(prefabPath);
            if (prefab == null) return null;

            return Utility.GetEAUploaderMeta(prefab);
        }

        private static void Save(EAUploaderMeta meta)
        {
            EditorUtility.SetDirty(meta);
            AssetDatabase.SaveAssets();
            PrefabManager.InvalidateCache();
        }

        private static TagRegistry LoadRegistry()
        {
            if (cachedRegistry != null) return cachedRegistry;

            TagRegistry registry = null;

            try
            {
                if (File.Exists(TAGS_PATH))
                {
                    registry = JsonUtility.FromJson<TagRegistry>(File.ReadAllText(TAGS_PATH));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading tags: {e.Message}");
            }

            registry = registry ?? new TagRegistry();
            registry.tags = (registry.tags ?? new List<string>())
                .Where(t => !string.IsNullOrEmpty(t)).ToList();
            registry.groups = (registry.groups ?? new List<TagGroupData>())
                .Where(g => g != null && !string.IsNullOrEmpty(g.name)).ToList();

            foreach (var group in registry.groups)
            {
                group.tags = (group.tags ?? new List<string>())
                    .Where(t => !string.IsNullOrEmpty(t)).ToList();
            }

            cachedRegistry = registry;
            return cachedRegistry;
        }

        private static void SaveRegistry(TagRegistry registry)
        {
            cachedRegistry = registry;

            try
            {
                var directory = Path.GetDirectoryName(TAGS_PATH);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(TAGS_PATH, JsonUtility.ToJson(registry, true));
                AssetDatabase.ImportAsset(TAGS_PATH);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving tags: {e.Message}");
            }
        }

        #endregion
    }
}
