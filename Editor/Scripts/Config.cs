using System;
using System.IO;
using UnityEngine;

namespace EAUploader
{
    class Config
    {
        public static string SETTINGS_PATH = "Assets/EAUploader/settings.json";

        [Serializable]
        public class ConfigData
        {
            public string language;
            public bool isLibraryOpen;
            // エラー発生時にエラーレポートウィンドウ(モーダル)を表示するかどうか
            public bool showErrorReportWindow = true;

            // 旧方式(取り込んだPrefabに直接メタ情報を付ける)からVariant管理への移行が済んでいるか
            public bool managedPrefabsMigrated;
        }

        public static void CreateDefaultSettings()
        {
            var newSettings = new ConfigData() { language = "ja", isLibraryOpen = true, showErrorReportWindow = true };
            SaveSettings(newSettings);
        }

        public static void SaveSettings(ConfigData settings)
        {
            try
            {
                string jsonContent = JsonUtility.ToJson(settings);
                File.WriteAllText(SETTINGS_PATH, jsonContent);
            }
            catch (IOException ex)
            {
                Debug.LogError($"Error saving language settings: {ex.Message}");
            }
        }

        public static ConfigData LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_PATH))
                {
                    string jsonContent = File.ReadAllText(SETTINGS_PATH);
                    return JsonUtility.FromJson<ConfigData>(jsonContent);
                }
                else
                {
                    return new ConfigData() { language = "ja", isLibraryOpen = true, showErrorReportWindow = true };
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"Error loading language settings: {ex.Message}");
                return new ConfigData() { language = "ja", isLibraryOpen = true, showErrorReportWindow = true };
            }
        }


    }
}