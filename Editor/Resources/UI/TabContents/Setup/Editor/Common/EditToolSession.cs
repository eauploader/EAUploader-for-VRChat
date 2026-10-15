using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md
    internal sealed class EditToolSession : IDisposable
    {
        public string PrefabPath { get; private set; }
        public GameObject Root { get; private set; }
        public Scene Scene => Root != null ? Root.scene : default;
        public bool IsDirty { get; private set; }
        public bool IsValid => Root != null;

        public event Action Changed;
        public event Action Reloaded;

        private EditToolSession(string prefabPath)
        {
            PrefabPath = prefabPath;
        }

        public static EditToolSession Open(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return null;

            var session = new EditToolSession(prefabPath);
            session.Load();
            return session.IsValid ? session : null;
        }

        private void Load()
        {
            try
            {
                Root = PrefabUtility.LoadPrefabContents(PrefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to open {PrefabPath}: {e.Message}");
                Root = null;
            }
        }

        private void Unload()
        {
            if (Root == null) return;

            try
            {
                PrefabUtility.UnloadPrefabContents(Root);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to close {PrefabPath}: {e.Message}");
            }

            Root = null;
        }

        public void MarkDirty()
        {
            IsDirty = true;
            Changed?.Invoke();
        }

        public bool Save()
        {
            return SaveTo(PrefabPath);
        }

        public bool SaveTo(string prefabPath)
        {
            if (Root == null || string.IsNullOrEmpty(prefabPath)) return false;

            PrefabUtility.SaveAsPrefabAsset(Root, prefabPath, out var success);
            if (!success)
            {
                Debug.LogError($"Failed to save {prefabPath}");
                return false;
            }

            if (prefabPath == PrefabPath)
            {
                IsDirty = false;
                Changed?.Invoke();
            }

            EditToolUtility.OnPrefabSaved(prefabPath);
            return true;
        }

        public void Reload()
        {
            Unload();
            Load();
            IsDirty = false;
            Reloaded?.Invoke();
            Changed?.Invoke();
        }

        public void Retarget(string prefabPath)
        {
            Unload();
            PrefabPath = prefabPath;
            Load();
            IsDirty = false;
            Reloaded?.Invoke();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
