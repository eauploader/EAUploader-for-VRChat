using EAUploader.UI.Components;
using EAUploader.UI.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EAULogger = EAUploader.UI.Windows.Logger;

namespace EAUploader.UI.Modals
{
    /// <summary>
    /// EAUploader全体の設定モーダル。
    /// 旧 Import/Settings タブの「Settings」ペインをモーダル化したもの。
    /// </summary>
    public class SettingsModal
    {
        private static readonly List<LanguageInfo> languageInfos = LanguageUtility.GetAvailableLanguages();

        private VisualElement modalContent;

        public void Open()
        {
            if (EAUploader.modal == null) return;

            EAUploader.modal.Initialize();
            EAUploader.modal.setTitle(T7e.Get("Settings"));

            modalContent = new VisualElement();
            modalContent.styleSheets.Add(EAUploader.styles);
            modalContent.styleSheets.Add(EAUploader.tailwind);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Modals/SettingsModal");
            visualTree.CloneTree(modalContent);

            BindLanguage();
            BindTheme();
            BindErrorReportWindow();

            modalContent.Q<Label>("version").text = EAUploaderCore.GetVersion();
            modalContent.Q<ShadowButton>("open_logreport").clicked += GenerateLogReport;

            LanguageUtility.Localization(modalContent);

            EAUploader.modal.setContent(modalContent);
            EAUploader.modal.setFooter(new[]
            {
                new Modal.ActionButton(T7e.Get("Close"), () => EAUploader.modal.Hide())
            });
            EAUploader.modal.Show();
        }

        private void BindLanguage()
        {
            var dropdown = modalContent.Q<DropdownField>("language");
            dropdown.choices = languageInfos.Select(x => x.display).ToList();
            dropdown.index = languageInfos.FindIndex(x => x.name == LanguageUtility.GetCurrentLanguage());
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null) return;

                var selectedLanguage = languageInfos.Find(x => x.display == evt.newValue);
                if (selectedLanguage == null || selectedLanguage.name == LanguageUtility.GetCurrentLanguage()) return;

                var previousLanguage = LanguageUtility.GetCurrentLanguage();
                if (ShowConfirmationDialog())
                {
                    LanguageUtility.ChangeLanguage(selectedLanguage.name);
                    ExecuteMenuItem("VRChat SDK/Reload SDK");
                }
                else
                {
                    dropdown.value = languageInfos.Find(x => x.name == previousLanguage).display;
                }
            });
        }

        private void BindTheme()
        {
            var themeInfos = ThemeUtility.GetAvailableThemes();
            var dropdown = modalContent.Q<DropdownField>("thema");
            dropdown.choices = themeInfos.Select(x => x.display).ToList();
            dropdown.index = themeInfos.FindIndex(x => x.name == ThemeUtility.GetCurrentTheme());
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null) return;

                var selectedTheme = themeInfos.Find(x => x.display == evt.newValue);
                if (selectedTheme == null || selectedTheme.name == ThemeUtility.GetCurrentTheme()) return;

                ThemeUtility.ChangeTheme(selectedTheme.name);
                ThemeUtility.ApplyCurrentTheme();
            });
        }

        private void BindErrorReportWindow()
        {
            var toggle = modalContent.Q<SlideToggle>("show_error_report_window");
            if (toggle == null) return;

            toggle.SetValueWithoutNotify(Config.LoadSettings().showErrorReportWindow);
            toggle.RegisterValueChangedCallback(evt =>
            {
                var settings = Config.LoadSettings();
                settings.showErrorReportWindow = evt.newValue;
                Config.SaveSettings(settings);
            });
        }

        private static void GenerateLogReport()
        {
            DialogPro.Show(DialogPro.DialogType.Info, T7e.Get("Open log report"),
                T7e.Get("The folder where the log files are stored is opened."),
                "OK", DialogPro.CloseDialog, true);

            // ログフォルダを開く
            string logFolderFullPath = EAULogger.GetLogFolderFullPath();
            System.Diagnostics.Process.Start(logFolderFullPath);
        }

        private static bool ShowConfirmationDialog()
        {
            return EditorUtility.DisplayDialog(
                T7e.Get("Language Change Confirmation"),
                T7e.Get("Are you sure you want to change the language?"),
                T7e.Get("Yes"), T7e.Get("No"));
        }

        private static void ExecuteMenuItem(string menuPath)
        {
            var menuItem = EditorApplication.ExecuteMenuItem(menuPath);
            if (!menuItem)
            {
                Debug.LogError($"Failed to execute menu item: {menuPath}");
            }
        }
    }
}
