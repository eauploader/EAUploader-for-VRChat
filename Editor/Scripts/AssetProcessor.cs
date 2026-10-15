using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Modals;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EAUploader
{
    public class AssetImportProcessor : AssetPostprocessor
    {
        // インポート時にジャンル選択が必要なPrefabのパス。
        // unitypackage展開などで複数同時にインポートされるため、1回のモーダルにまとめる。
        private static readonly List<string> pendingGenreSelection = new List<string>();
        private static bool genreSelectionScheduled = false;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(a => a.Contains("/Resources/Localization/") && a.EndsWith(".json")))
            {
                T7e.ClearCache();
            }

            foreach (var asset in importedAssets)
            {
                if (asset.EndsWith(".prefab"))
                {
                    var path = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
                    if (string.IsNullOrEmpty(path)) continue;

                    ShaderChecker.CheckShadersInPrefabs(path);

                    // 管理フォルダの外のPrefabが取り込まれた場合はVariantが作られる。
                    // ジャンル選択の対象になるのは、取り込み元ではなくそのVariant。
                    var pending = PrefabManager.ImportPrefab(path);
                    if (pending != null && !pendingGenreSelection.Contains(pending))
                    {
                        pendingGenreSelection.Add(pending);
                    }
                }
            }

            if (deletedAssets.Any(a => a.EndsWith(".prefab")))
            {
                PrefabManager.InvalidateCache();
            }

            ScheduleGenreSelection();
        }

        private static void ScheduleGenreSelection()
        {
            if (pendingGenreSelection.Count == 0 || genreSelectionScheduled) return;

            genreSelectionScheduled = true;
            EditorApplication.delayCall += ShowGenreSelectionModal;
        }

        private static void ShowGenreSelectionModal()
        {
            genreSelectionScheduled = false;

            var paths = pendingGenreSelection.Where(p => PrefabManager.GetPrefab(p) != null).ToList();
            pendingGenreSelection.Clear();

            if (paths.Count == 0) return;

            // モーダルはEAUploaderウィンドウ内に描画されるため、ウィンドウが無い場合は表示できない。
            // その場合はデフォルト(衣装)のまま据え置き、次にインポートされたときに再度尋ねる。
            if (UI.EAUploader.modal == null) return;

            var prefabInfos = paths
                .Select(PrefabManager.GetPrefabInfo)
                .Where(info => info != null)
                .ToList();

            if (prefabInfos.Count == 0) return;

            new ClothSettingModal(prefabInfos).Open();
        }
    }
}
