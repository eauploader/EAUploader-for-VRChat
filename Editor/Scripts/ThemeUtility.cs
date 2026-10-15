using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace EAUploader.UI
{
    public class ThemeInfo
    {
        public string name;
        public string display;

        public ThemeInfo(string name, string display)
        {
            this.name = name;
            this.display = display;
        }
    }

    public static class ThemeUtility
    {
        private static readonly string[] THEME_CLASSES = { "white", "dark", "pastel" };

        public static List<ThemeInfo> GetAvailableThemes() => new List<ThemeInfo>
        {
            new ThemeInfo("white", T7e.Get("Light")),
            new ThemeInfo("dark", T7e.Get("Dark")),
            new ThemeInfo("pastel", T7e.Get("Pastel"))
        };

        public static string GetCurrentTheme()
        {
            return EditorPrefs.GetString("EAUploader.Theme", "white");
        }

        public static void ChangeTheme(string theme)
        {
            EditorPrefs.SetString("EAUploader.Theme", theme);
        }

        /// <summary>
        /// テーマ用のUSSクラスを付け替える。
        /// </summary>
        public static void ApplyTheme(VisualElement root, string theme)
        {
            if (root == null) return;

            foreach (var themeClass in THEME_CLASSES)
            {
                root.RemoveFromClassList(themeClass);
            }

            root.AddToClassList(theme);
        }

        /// <summary>
        /// 現在のテーマをEAUploaderウィンドウへ適用する。
        /// </summary>
        public static void ApplyCurrentTheme()
        {
            var window = EAUploader.InstanceOrNull;
            if (window == null) return;

            ApplyTheme(window.rootVisualElement, GetCurrentTheme());
        }
    }
}
