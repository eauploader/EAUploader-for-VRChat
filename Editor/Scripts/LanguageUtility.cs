using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
using static EAUploader.Config;
using static EAUploader.T7e;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader
{
    public static class LanguageUtility
    {
        private const string TRANSLATE_FOLDER_PATH = "Packages/tech.uslog.eauploader/Editor/Resources/Localization";
        private static string cachedLanguage;

        internal static void InvalidateLanguageCache()
        {
            cachedLanguage = null;
        }

        public static string GetCurrentLanguage()
        {
            if (cachedLanguage != null)
            {
                return cachedLanguage;
            }

            if (!File.Exists(Config.SETTINGS_PATH))
            {
                Config.CreateDefaultSettings();
            }

            var settings = Config.LoadSettings();
            cachedLanguage = string.IsNullOrEmpty(settings?.language) ? "ja" : settings.language;
            return cachedLanguage;
        }

        /// <summary>
        /// このクラスを付けた要素は翻訳しない。
        /// Prefab名やタグ名など、ユーザーが入力した文字列に使う。
        /// </summary>
        private const string NO_TRANSLATE_CLASS = "no-translate";

        public static void Localization(VisualElement root)
        {
            root.Query<TextElement>().ForEach((e) =>
            {
                if (ShouldSkipTranslation(e))
                {
                    return;
                }
                var translated = T7e.Get(e.text);
                // Avoid resetting unchanged UI text during periodic localization. skipcq: CS-R1092
                if (e.text != translated)
                {
                    e.text = translated;
                }
            });
        }

        /// <summary>
        /// 翻訳の対象外かどうかを判定する。
        /// </summary>
        private static bool ShouldSkipTranslation(TextElement element)
        {
            if (element.ClassListContains(NO_TRANSLATE_CLASS))
            {
                return true;
            }

            return IsTextInput(element);
        }

        /// <summary>
        /// 入力欄の中身かどうかを判定する。
        /// この処理は一定間隔で走るため、入力中のテキストを翻訳して書き換えてしまうと
        /// 文字が打てなくなる。ラベル部分は翻訳したいので、入力部分だけを除外する。
        /// </summary>
        private static bool IsTextInput(TextElement element)
        {
            if (element.parent?.name == "unity-text-input")
            {
                return true;
            }

            // Unity 2022.2 以降、入力欄の中身のTextElementにはこのクラスが付く
            if (element.ClassListContains("unity-text-element--inner-input-field"))
            {
                return true;
            }

            // Labelは項目名なので翻訳対象。それ以外でTextField配下にあるものは入力欄とみなす。
            if (element is not Label && element.GetFirstAncestorOfType<TextField>() != null)
            {
                return true;
            }

            return false;
        }

        public static void LocalizationFromJsonFile(VisualElement root, string LocalizationFolderPath)
        {
            var allTranslations = new Dictionary<string, Dictionary<string, string>>();

            foreach (var language in GetAvailableLanguages())
            {
                if (language.name == "en")
                {
                    continue;
                }
                var path = $"{LocalizationFolderPath}/{language.name}.json";
                // プラグインなどは一部の言語のファイルしか持たないことがあるため、無い言語は飛ばす
                if (!File.Exists(path))
                {
                    continue;
                }
                Debug.Log($"Loading translations from {path}");
                var json = File.ReadAllText(path);
                var translations = JsonUtility.FromJson<LocalizationData>(json);
                var translationDict = new Dictionary<string, string>();
                foreach (var item in translations.items)
                {
                    translationDict.Add(item.key, item.value);
                }
                allTranslations.Add(language.name, translationDict);
            }

            string currentLanguage = GetCurrentLanguage();

            root.Query<TextElement>().ForEach((e) =>
            {
                if (ShouldSkipTranslation(e))
                {
                    return;
                }

                string key = e.text;
                if (allTranslations.ContainsKey(currentLanguage) && allTranslations[currentLanguage].ContainsKey(key))
                {
                    e.text = allTranslations[currentLanguage][key];
                }
            });
        }

        public static string T7eFromJsonFile(string key, string LocalizationFolderPath)
        {
            var allTranslations = new Dictionary<string, Dictionary<string, string>>();

            foreach (var language in GetAvailableLanguages())
            {
                if (language.name == "en")
                {
                    continue;
                }
                var path = $"{LocalizationFolderPath}/{language.name}.json";
                if (File.Exists(path))
                {
                    Debug.Log($"Loading translations from {path}");
                    var json = File.ReadAllText(path);
                    var translations = JsonUtility.FromJson<LocalizationData>(json);
                    var translationDict = new Dictionary<string, string>();
                    foreach (var item in translations.items)
                    {
                        translationDict.Add(item.key, item.value);
                    }
                    allTranslations.Add(language.name, translationDict);
                }
                else if (language.name == GetCurrentLanguage())
                {
                    // 使っていない言語のファイルが無いのは普通なので、選択中の言語だけ知らせる
                    Debug.LogWarning($"Translation file not found for language: {language.name}");
                }
            }

            string currentLanguage = GetCurrentLanguage();

            if (allTranslations.ContainsKey(currentLanguage) && allTranslations[currentLanguage].ContainsKey(key))
            {
                return allTranslations[currentLanguage][key];
            }

            return key;
        }

        public static void ChangeLanguage(string language)
        {
            cachedLanguage = language;
            var settings = Config.LoadSettings();
            settings.language = language;
            Config.SaveSettings(settings);  // Config.を追加
        }

        public static List<LanguageInfo> GetAvailableLanguages()
        {
            List<LanguageInfo> languages = new List<LanguageInfo>
            {
                new LanguageInfo { name = "en", display = "English" }
            };

            try
            {
                string[] jsonFiles = Directory.GetFiles(TRANSLATE_FOLDER_PATH, "*.json");
                foreach (string file in jsonFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    // Read Json file and get the language name
                    string jsonContent = File.ReadAllText(file);
                    var translations = JsonUtility.FromJson<T7e.LocalizationData>(jsonContent);
                    string languageName = translations.name;
                    languages.Add(new LanguageInfo { name = fileName, display = languageName });
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"Error retrieving available languages: {ex.Message}");
            }

            return languages;
        }

        #region Language font

        // 同梱のNoto Sans JPには簡体字の多くが含まれていないため、
        // 中国語ではOSに入っている中国語フォントを使い、足りない文字は同梱フォントで補う。
        private static readonly string[] CHINESE_FONT_FAMILIES =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "SimHei",
        };

        private static FontAsset languageFont;
        private static string languageFontFor;

        /// <summary>
        /// 現在の言語に合わせたフォントを要素に設定する。
        /// styles.uss の :root はスタイルシートを追加した要素ごとに当たるため、
        /// スタイルシートを追加した要素それぞれで呼ぶ。
        /// </summary>
        public static void ApplyLanguageFont(VisualElement root)
        {
            if (root == null) return;

            var font = GetLanguageFont();
            if (font == null) return;

            root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
        }

        private static FontAsset GetLanguageFont()
        {
            var language = GetCurrentLanguage();
            if (languageFontFor == language && languageFont != null)
            {
                return languageFont;
            }

            languageFontFor = language;
            languageFont = null;

            if (!language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            foreach (var family in CHINESE_FONT_FAMILIES)
            {
                FontAsset font;
                try
                {
                    font = FontAsset.CreateFontAsset(family, "Regular");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load font {family}: {e.Message}");
                    continue;
                }

                if (font == null) continue;

                // シーン切り替えなどで破棄されないようにし、保存もしない
                font.hideFlags = HideFlags.DontSave;

                var baseFont = Resources.Load<FontAsset>("UI/EAUploaderFont");
                font.fallbackFontAssetTable = baseFont != null
                    ? new List<FontAsset> { baseFont }
                    : new List<FontAsset>();

                languageFont = font;
                break;
            }

            if (languageFont == null)
            {
                Debug.LogWarning("No Chinese font was found on this system. Some characters may not be displayed.");
            }

            return languageFont;
        }

        #endregion

        // SaveSettingsメソッドを削除（Config.SaveSettingsを使用するため）
    }
}
