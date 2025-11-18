using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using static EAUploader.Config;
using static EAUploader.T7e;

namespace EAUploader
{
    public class LanguageInfo
    {
        public string name { get; set; }
        public string display { get; set; }
    }

    public static class LanguageUtility
    {
        private const string SETTINGS_PATH = "Assets/EAUploader/settings.json";
        private const string TRANSLATE_FOLDER_PATH = "Packages/tech.uslog.eauploader/Editor/Resources/Localization";

        public static string GetCurrentLanguage()
        {
            if (!File.Exists(SETTINGS_PATH))
            {
                Config.CreateDefaultSettings();  // Config.を追加
            }

            try
            {
                string jsonContent = File.ReadAllText(SETTINGS_PATH);
                var settings = JsonUtility.FromJson<Config.ConfigData>(jsonContent);  // Config.ConfigDataに変更
                return settings.language;
            }
            catch (IOException ex)
            {
                Debug.LogError($"Error reading language settings: {ex.Message}");
                return "en";
            }
        }

        public static void Localization(VisualElement root)
        {
            root.Query<TextElement>().ForEach((e) =>
            {
                if (e.parent.name == "unity-text-input")
                {
                    return;
                }
                e.text = T7e.Get(e.text);
            });
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
                string key = e.text;
                if (allTranslations != null && allTranslations.ContainsKey(currentLanguage) && allTranslations[currentLanguage].ContainsKey(key))
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
                else
                {
                    Debug.LogWarning($"Translation file not found for language: {language.name}");
                }
            }

            string currentLanguage = GetCurrentLanguage();

            if (allTranslations != null && allTranslations.ContainsKey(currentLanguage) && allTranslations[currentLanguage].ContainsKey(key))
            {
                return allTranslations[currentLanguage][key];
            }

            return key;
        }

        public static void ChangeLanguage(string language)
        {
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

        // SaveSettingsメソッドを削除（Config.SaveSettingsを使用するため）
    }
}
