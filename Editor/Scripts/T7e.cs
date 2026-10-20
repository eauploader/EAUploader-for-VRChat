using System;
using System.Collections.Generic;
using UnityEngine;

namespace EAUploader
{
    public class T7e
    {
        [Serializable]
        public class LocalizationData
        {
            public string name;
            public List<LocalizationItem> items;
        }

        [Serializable]
        public class LocalizationItem
        {
            public string key;
            public string value;
        }

        private static Dictionary<string, Dictionary<string, string>> allTranslations;

        public static string Get(string key, params object[] args)
        {
            if (allTranslations == null)
            {
                LoadTranslations();
            }

            string currentLanguage = LanguageUtility.GetCurrentLanguage();

            if (allTranslations != null && allTranslations.ContainsKey(currentLanguage) && allTranslations[currentLanguage].ContainsKey(key))
            {
                string formatString = allTranslations[currentLanguage][key];
                return args != null && args.Length > 0 ? string.Format(formatString, args) : formatString;
            }
            else
            {
                return key;
            }
        }

        internal static void ClearCache()
        {
            allTranslations = null;
            LanguageUtility.InvalidateLanguageCache();
        }

        private static void LoadTranslations()
        {
            allTranslations = new Dictionary<string, Dictionary<string, string>>();

            foreach (var language in LanguageUtility.GetAvailableLanguages())
            {
                if (language.name == "en")
                {
                    continue;
                }
                var path = $"Localization/{language.name}";
                var json = Resources.Load<TextAsset>(path).text;
                var translations = JsonUtility.FromJson<LocalizationData>(json);
                var translationDict = new Dictionary<string, string>();
                foreach (var item in translations.items)
                {
                    translationDict.Add(item.key, item.value);
                }
                allTranslations.Add(language.name, translationDict);
            }
        }
    }
}
