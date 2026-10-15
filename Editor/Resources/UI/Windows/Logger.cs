using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Windows
{
    /// <summary>
    /// EAUploaderのログ管理クラス。
    /// エラーが発生した際に、EditorWindow ではなくモーダルで表示するよう変更。
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// EAUploaderのログ出力において使用するログ種別。
        /// </summary>
        public enum EAULogType
        {
            Error,
            Assert,
            Warning,
            Log,
            Exception,
            EAUploader // EAUploaderの操作を出力する場合に用いる。
        }

        private static StringBuilder _stringBuilder;

        /// <summary>
        /// 出力を行うログファイルの名前。
        /// </summary>
        public static string OUTPUT_LOGFILE_NAME = "";

        /// <summary>
        /// ログファイルを生成するフォルダのパス。
        /// </summary>
        internal static string LOGFOLDER_PATH = "EAUploaderLog/";
        internal static long LOGFOLDER_MAX_SIZE_IN_BYTES = 100 * 1024 * 1024; // 100MB

        internal const string EAULOG_PREFIX = "[EAUb1bc40d3ff764a5d8081e5cd2f48bbc7]";

        // UI Toolkitのテキスト描画は1つのTextElementあたりの頂点数に上限があり、
        // 長すぎる文字列を1つのLabelに入れるとメッシュ生成に失敗して何も描画されなくなる。
        // そのため表示は複数のLabelに分割し、全体の文字数にも上限を設ける。
        private const int MESSAGE_CHUNK_LENGTH = 4000;
        private const int MESSAGE_DISPLAY_MAX_LENGTH = 40000;

        // 蓄積するエラーログ本文の上限。超えた分は古いものから捨てる。
        private const int ERROR_LOG_BUFFER_MAX_LENGTH = 200000;

        // エラーレポート表示中に発生したログでモーダルを再帰的に開かないためのガード
        private static bool isReportingError = false;

        [Serializable]
        public class DependencyInfo
        {
            public string version;
            public Dictionary<string, string> dependencies;
        }

        [Serializable]
        public class VpmManifest
        {
            public Dictionary<string, DependencyInfo> dependencies;
            public Dictionary<string, DependencyInfo> locked;
        }

        /// <summary>
        /// VPMの locked パッケージを取得するユーティリティ。
        /// </summary>
        internal static IEnumerable<(string package, string version)> VpmLockedPackages()
        {
            try
            {
                var vpmManifestJson = File.ReadAllText("Packages/vpm-manifest.json");
                var manifest = JsonConvert.DeserializeObject<VpmManifest>(vpmManifestJson)
                               ?? throw new InvalidOperationException();
                return manifest.locked
                    .Where(x => x.Value.version != null)
                    .Select(x => (x.Key, x.Value.version!));
            }
            catch
            {
                return Array.Empty<(string, string)>();
            }
        }

        [MenuItem("Window/Error Report")]
        public static void MakeError()
        {
            Debug.LogError("This is a test error message");
        }

        public static string GetLogFolderFullPath()
        {
            return new DirectoryInfo(LOGFOLDER_PATH).FullName;
        }

        /// <summary>
        /// Unityのログコールバック（RegisterLogCallback 経由）から呼ばれるメソッド。
        /// </summary>
        internal static void OnReceiveLog(string logText, string stackTrace, LogType logType)
        {
            if (_stringBuilder == null)
            {
                _stringBuilder = new StringBuilder();
            }

            EAULogType eAULog = new();

            switch (logType)
            {
                case LogType.Error:
                    eAULog = EAULogType.Error;
                    break;
                case LogType.Assert:
                    eAULog = EAULogType.Assert;
                    break;
                case LogType.Warning:
                    eAULog = EAULogType.Warning;
                    break;
                case LogType.Log:
                    // EAUログかどうか判定
                    if (logText.Contains(EAULOG_PREFIX))
                    {
                        eAULog = EAULogType.EAUploader;
                        break;
                    }
                    eAULog = EAULogType.Log;
                    break;
                case LogType.Exception:
                    eAULog = EAULogType.Exception;
                    break;
                default:
                    eAULog = (EAULogType)(-1);
                    break;
            }

            // ログファイルへの書き込み
            writeLog(logText, stackTrace, eAULog);

            // エラーor例外時にモーダル表示
            if (logType == LogType.Exception || logType == LogType.Error)
            {
                // レポート用の蓄積は表示可否に関わらず行う
                AppendErrorLog(logText, stackTrace);

                // 設定で無効化されている場合はログファイルへの記録のみ行う
                if (!Config.LoadSettings().showErrorReportWindow) return;

                // モーダルはEAUploaderウィンドウ内に描画されるため、
                // ウィンドウ生成前のエラーでは表示できない
                if (EAUploader.modal == null) return;

                // モーダル構築中に発生したログで再入しないようにする
                if (isReportingError) return;
                isReportingError = true;

                try
                {
                    ShowErrorReport(logText, stackTrace);
                }
                catch (Exception e)
                {
                    // 表示に失敗してもログ記録は済んでいるため、コンソール出力だけに留める
                    UnityEngine.Debug.LogWarning("Failed to show EAUploader error report: " + e.Message);
                }
                finally
                {
                    isReportingError = false;
                }
            }
        }

        /// <summary>
        /// エラーレポート用のバッファへ追記する。
        /// 上限を超えた場合は古い内容から捨てて、メモリと描画量が際限なく増えないようにする。
        /// </summary>
        private static void AppendErrorLog(string logText, string stackTrace)
        {
            _stringBuilder ??= new StringBuilder();

            _stringBuilder.AppendLine(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            _stringBuilder.AppendLine(logText);
            _stringBuilder.AppendLine("Stack Trace:");
            _stringBuilder.AppendLine(stackTrace);

            if (_stringBuilder.Length > ERROR_LOG_BUFFER_MAX_LENGTH)
            {
                var trimmed = _stringBuilder.ToString(
                    _stringBuilder.Length - ERROR_LOG_BUFFER_MAX_LENGTH,
                    ERROR_LOG_BUFFER_MAX_LENGTH);
                _stringBuilder.Clear();
                _stringBuilder.AppendLine("(older entries omitted)");
                _stringBuilder.Append(trimmed);
            }
        }

        /// <summary>
        /// 1つのTextElementに収まる長さへ文字列を分割する。
        /// できるだけ改行位置で切って、行の途中で分断されないようにする。
        /// </summary>
        private static IEnumerable<string> SplitIntoChunks(string text, int chunkLength)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            int index = 0;
            while (index < text.Length)
            {
                int length = Math.Min(chunkLength, text.Length - index);

                if (index + length < text.Length)
                {
                    int lastNewLine = text.LastIndexOf('\n', index + length - 1, length);
                    if (lastNewLine > index)
                    {
                        length = lastNewLine - index + 1;
                    }
                }

                yield return text.Substring(index, length);
                index += length;
            }
        }

        private static void ShowErrorReport(string logText, string stackTrace)
        {
            {
                EAUploader.modal.Initialize();
                EAUploader.modal.setTitle(T7e.Get("Error Report"));

                // モーダル用 VisualElement
                var modalContent = new VisualElement();
                modalContent.styleSheets.Add(EAUploader.styles);
                modalContent.styleSheets.Add(EAUploader.tailwind);

                // UI テンプレートを読み込み
                var visualTree = Resources.Load<VisualTreeAsset>("UI/Windows/Logger");
                visualTree.CloneTree(modalContent);

                LanguageUtility.Localization(modalContent);

                // エラーログ文字列の構築(蓄積はAppendErrorLogで済ませている)
                StringBuilder errorReport = new();
                errorReport.AppendLine("Error Report");
                errorReport.AppendLine("------------");
                errorReport.AppendLine();
                errorReport.AppendLine("Error Logs:");
                errorReport.AppendLine();
                errorReport.AppendLine(_stringBuilder.ToString());
                errorReport.AppendLine("Environment Details:");
                errorReport.AppendLine("- Application-Version: " + EAUploaderCore.GetVersion(true));
                errorReport.AppendLine("- Unity-Version: " + Application.unityVersion);
                errorReport.AppendLine("- Editor-Platform: " + Application.platform);
                errorReport.AppendLine("- Vpm-Dependency: \n" +
                    string.Join("\n", VpmLockedPackages().Select(x => $"{x.package}@{x.version}")));

                var fullReport = errorReport.ToString();

                // 長大なレポートを1つのLabelに流し込むと描画が破綻して真っ白になるため、
                // 末尾(最新のエラー)を優先して切り詰めたうえで複数のLabelに分割して並べる
                var displayText = fullReport;
                bool truncated = false;
                if (displayText.Length > MESSAGE_DISPLAY_MAX_LENGTH)
                {
                    displayText = displayText.Substring(displayText.Length - MESSAGE_DISPLAY_MAX_LENGTH);
                    truncated = true;
                }

                var messageContainer = modalContent.Q<VisualElement>("message_container");
                if (messageContainer != null)
                {
                    messageContainer.Clear();
                    foreach (var chunk in SplitIntoChunks(displayText, MESSAGE_CHUNK_LENGTH))
                    {
                        messageContainer.Add(new Label(chunk));
                    }
                }

                var truncatedNotice = modalContent.Q<Label>("truncated_notice");
                if (truncatedNotice != null)
                {
                    truncatedNotice.text = truncated
                        ? T7e.Get("The log is too long to display in full. Only the latest part is shown. Use Copy to get the whole report.")
                        : string.Empty;
                    truncatedNotice.style.display = truncated ? DisplayStyle.Flex : DisplayStyle.None;
                }

                // ボタンたち
                var copyButton = modalContent.Q<Button>("copy");
                var okButton = modalContent.Q<Button>("ok");
                var restartButton = modalContent.Q<Button>("restart");

                // このウィンドウを今後表示しない
                var disableToggle = modalContent.Q<Toggle>("disable_error_window");
                if (disableToggle != null)
                {
                    disableToggle.value = false;
                    disableToggle.RegisterValueChangedCallback(evt =>
                    {
                        var settings = Config.LoadSettings();
                        settings.showErrorReportWindow = !evt.newValue;
                        Config.SaveSettings(settings);
                    });
                }

                // コピー: エラーレポートをクリップボードにコピー
                if (copyButton != null)
                {
                    copyButton.clicked += () => EditorGUIUtility.systemCopyBuffer = fullReport;
                }

                // OK: モーダルを閉じる
                if (okButton != null)
                {
                    okButton.clicked += () => EAUploader.modal.Hide();
                }

                // 再読み込み
                if (restartButton != null)
                {
                    restartButton.clicked += () =>
                        AssetDatabase.ImportAsset("Packages/tech.uslog.eauploader", ImportAssetOptions.ImportRecursive);
                }

                // モーダルの内容を設定して表示
                EAUploader.modal.setContent(modalContent);
                EAUploader.modal.Show();
            }
        }

        /// <summary>
        /// ログ出力をログファイルに対して行う。
        /// UnityConsole上へのログ出力は行わない。
        /// </summary>
        /// <param name="logText"></param>
        /// <param name="stackTrace"></param>
        /// <param name="logType"></param>
        internal static void writeLog(string logText, string stackTrace, EAULogType logType)
        {
            DirectoryInfo di = new(LOGFOLDER_PATH);

            if (!di.Exists)
            {
                Directory.CreateDirectory(LOGFOLDER_PATH);
                // 確実にディレクトリが作成されるのを待つ
                while (!di.Exists)
                {
                    di.Refresh();
                }
            }

            // ログフォルダサイズ確認
            long logFolderSizeInBytes = di.EnumerateFiles("*.log").Sum(fi => fi.Length);
            if (logFolderSizeInBytes > LOGFOLDER_MAX_SIZE_IN_BYTES)
            {
                var oldestLogFile = di.EnumerateFiles("*.log")
                                      .OrderBy(fi => fi.CreationTime)
                                      .FirstOrDefault();

                if (oldestLogFile != null)
                {
                    oldestLogFile.Delete();
                }
            }

            string outputStackTrace = "";
            string outputTimeStamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            string outputLogLevel;

            switch (logType)
            {
                case EAULogType.Error:
                    outputLogLevel = "ERR";
                    break;
                case EAULogType.Assert:
                    outputLogLevel = "AST";
                    break;
                case EAULogType.Warning:
                    outputLogLevel = "WNG";
                    break;
                case EAULogType.Log:
                    outputLogLevel = "LOG";
                    break;
                case EAULogType.Exception:
                    outputLogLevel = "EXP";
                    break;
                case EAULogType.EAUploader:
                    outputLogLevel = "EAU";
                    break;
                default:
                    outputLogLevel = "OTHER";
                    break;
            }

            if (logType == EAULogType.Exception || logType == EAULogType.Error)
            {
                // インデント
                string[] lines = stackTrace.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = new string(' ', $"{outputTimeStamp} {outputLogLevel} {logText} ".Length) + lines[i];
                }
                outputStackTrace = string.Join('\n', lines);
            }
            else if (logType == EAULogType.EAUploader)
            {
                logText = logText.Replace(EAULOG_PREFIX, "");
                string[] lines = stackTrace.Split('\n');
                // トレース文字列が3行以上ある場合、3行目を返す。
                outputStackTrace = lines.Length >= 3 ? lines[2] : stackTrace;
            }
            else
            {
                string[] lines = stackTrace.Split('\n');
                // トレース文字列が2行以上ある場合、2行目を返す。
                outputStackTrace = lines.Length >= 2 ? lines[1] : stackTrace;
            }

            // ファイルが存在しなければ作成
            if (!File.Exists(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME))
            {
                File.Create(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME).Close();
            }

            using var writer = new StreamWriter(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME, true, Encoding.GetEncoding("UTF-8"));
            writer.WriteLine($"{outputTimeStamp} {outputLogLevel} {logText} {outputStackTrace}");
        }

        /// <summary>
        /// ログフォルダの中から、最も小さくなおかつ存在しないログファイルのログファイルナンバーを取得する。
        /// </summary>
        internal static int FetchLogFileNumber()
        {
            var logFiles = Directory.GetFiles(LOGFOLDER_PATH, DateTime.UtcNow.ToString("yyyy-MM-dd") + "*.log");

            if (logFiles == null)
            {
                return 1;
            }

            var regex = new Regex(@"\d{4}-\d{2}-\d{2}-(\d+)\.log$");
            var numbers = logFiles.Select(path =>
            {
                var match = regex.Match(Path.GetFileName(path));
                return match.Success ? int.Parse(match.Groups[1].Value) : -1;
            })
            .Where(number => number != -1)
            .OrderBy(number => number)
            .ToList();

            var missingNumber = Enumerable.Range(1, numbers.Count + 1).Except(numbers).FirstOrDefault();
            return missingNumber;
        }

        /// <summary>
        /// EAUログをログファイルに出力する。
        /// </summary>
        public static void writeEAULog(string message)
        {
            Debug.Log(EAULOG_PREFIX + message);
        }
    }
}
