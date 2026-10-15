using EAUploader.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Setup.EditTools
{
    internal static class EditToolUtility
    {
        private const string FALLBACK_GENERATED_ROOT = "Assets/EAUploader/Generated";

        #region Generated assets

        public static string GetGeneratedFolder(string prefabPath, string subFolder)
        {
            var baseFolder = ManagedPrefabs.GetItemFolder(prefabPath);
            if (baseFolder == null)
            {
                EnsureFolder(FALLBACK_GENERATED_ROOT);
                baseFolder = FALLBACK_GENERATED_ROOT;
            }

            var folder = $"{baseFolder}/{subFolder}";
            EnsureFolder(folder);
            return folder;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Asset";

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return sanitized.Length == 0 ? "Asset" : sanitized;
        }

        #endregion

        #region Refresh

        public static void OnPrefabSaved(string prefabPath)
        {
            PrefabManager.InvalidateCache();

            var prefab = PrefabManager.GetPrefab(prefabPath);
            if (prefab != null)
            {
                var preview = PrefabPreview.GeneratePreview(prefab);
                PrefabPreview.SavePrefabPreview(prefabPath, preview);
                PrefabManager.UpdateCachedPreview(prefabPath, preview);
            }

            AvatarList.Main.RequestRefresh();

            var window = EAUploader.InstanceOrNull;
            if (window != null && window.CurrentScreen == EAUploaderScreen.Setup
                && EAUploaderCore.selectedPrefabPath == prefabPath)
            {
                EAUploader.RefreshCurrentScreen();
            }
        }

        #endregion

        #region Requirements

        public static GameObject LoadPrefab(string prefabPath)
        {
            return string.IsNullOrEmpty(prefabPath) ? null : PrefabManager.GetPrefab(prefabPath);
        }

        public static bool IsAvatar(string prefabPath)
        {
            var prefab = LoadPrefab(prefabPath);
            return prefab != null
                && Utility.CheckAvatarHasVRCAvatarDescriptor(prefab)
                && PrefabManager.GetPrefabGenre(prefabPath) == EAUploaderMeta.PrefabGenre.Avatar;
        }

        public static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer))
                .ToList();

            if (renderers.Count == 0) return new Bounds(target.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        #endregion

        #region UI

        public static Label Heading(string text)
        {
            var label = new Label(text);
            label.AddToClassList("text-lg");
            label.AddToClassList("font-bold");
            label.AddToClassList("pb-2");
            return label;
        }

        public static Label Note(string text)
        {
            var label = new Label(text) { style = { whiteSpace = WhiteSpace.Normal } };
            label.AddToClassList("text-sm");
            label.AddToClassList("pb-2");
            return label;
        }

        public static VisualElement StatusRow(string ussClass, string icon, string text)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4,
                }
            };

            if (!string.IsNullOrEmpty(ussClass)) row.AddToClassList(ussClass);

            row.Add(new MaterialIcon { icon = icon, style = { paddingRight = 4 } });
            row.Add(new Label(text) { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1 } });
            return row;
        }

        public static Button IconButton(string icon, string text, System.Action onClick)
        {
            var button = new Button(onClick);
            button.AddToClassList("button-with-icons");
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;

            if (!string.IsNullOrEmpty(icon))
            {
                button.Add(new MaterialIcon { icon = icon, style = { paddingRight = string.IsNullOrEmpty(text) ? 0 : 4 } });
            }

            if (!string.IsNullOrEmpty(text))
            {
                button.Add(new Label(text));
            }

            return button;
        }

        #endregion
    }
}
