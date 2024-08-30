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
    public class Logger : EditorWindow
    {
        private static Logger instance;

        public static Logger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = GetWindow<Logger>();
                }
                return instance;
            }
        }

        internal void OnEnable()
        {
            instance = this;
            _stringBuilder = new StringBuilder();
        }

        private void OnDisable()
        {
            instance = null;
        }


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
        /// EAUplaoder起動単位でログファイルを出力するため、本変数の値は
        /// EAUploader起動時に一度設定したら、以降は変更を行わない。
        /// </summary>
        public static string OUTPUT_LOGFILE_NAME = "";

        /// <summary>
        /// ログファイルを生成するフォルダのパス。
        /// </summary>
        internal static string LOGFOLDER_PATH = "EAUploaderLog/";
        internal static long LOGFOLDER_MAX_SIZE_IN_BYTES = 100 * 1024 * 1024; // 100MB

        internal const string EAULOG_PREFIX = "[EAUb1bc40d3ff764a5d8081e5cd2f48bbc7]";

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

        [MenuItem("Window/Error Report")]
        public static void MakeError()
        {
            Debug.LogError("This is a test error message");
        }

        public static string GetLogFolderFullPath()
        {
            return new DirectoryInfo(LOGFOLDER_PATH).FullName;
        }

        internal static void OnReceiveLog(string logText, string stackTrace, LogType logType)
        {

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

            // ログ出力
            writeLog(logText, stackTrace, eAULog);

            if (logType == LogType.Exception || logType == LogType.Error)
            {
                var eauWindow = UI.EAUploader.Instance;
                Logger wnd = Instance;
                wnd.titleContent = new GUIContent(T7e.Get("Error Report"));
                wnd.position = new Rect(eauWindow.position.x + eauWindow.position.width / 2 - 400, eauWindow.position.y + eauWindow.position.height / 2 - 300, 800, 600);
                wnd.minSize = new Vector2(800, 600);

                wnd.rootVisualElement.styleSheets.Add(EAUploader.styles);
                wnd.rootVisualElement.styleSheets.Add(EAUploader.tailwind);

                wnd.rootVisualElement.Clear();
                var visualTree = Resources.Load<VisualTreeAsset>("UI/Windows/Logger");
                visualTree.CloneTree(wnd.rootVisualElement);

                LanguageUtility.Localization(wnd.rootVisualElement);

                StringBuilder errorReport = new();

                _stringBuilder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _stringBuilder.AppendLine(logText);
                _stringBuilder.AppendLine("Stack Trace:");
                _stringBuilder.AppendLine(stackTrace);

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
                errorReport.AppendLine("- Vpm-Dependency: \n" + String.Join("\n", VpmLockedPackages().Select(x => $"{x.package}@{x.version}")));

                wnd.rootVisualElement.Q<Label>("message").text = errorReport.ToString();

                var copyButton = wnd.rootVisualElement.Q<Button>("copy");
                var okButton = wnd.rootVisualElement.Q<Button>("ok");
                var restartButton = wnd.rootVisualElement.Q<Button>("restart");

                copyButton.clickable.clicked += () =>
                {
                    EditorGUIUtility.systemCopyBuffer = errorReport.ToString();
                };

                okButton.clickable.clicked += () =>
                {
                    wnd.Close();
                };

                restartButton.clickable.clicked += () =>
                {
                    AssetDatabase.ImportAsset("Packages/tech.uslog.eauploader", ImportAssetOptions.ImportRecursive);
                };
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
            // ログ出力処理
            // ログフォルダの容量がLOGFOLDER_MAX_SIZE_IN_BYTESを超えていた場合
            // 最も古いログファイルを削除する
            // ディレクトリ容量の対象となるのは*.logファイルのみである。
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

            // ディレクトリ容量取得
            long logFolderSizeInBytes = di.EnumerateFiles("*.log").Sum(fi => fi.Length);

            // ディレクトリ容量が規定の容量よりも大きかった場合
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

            // ログレベルがErrorかExceptionのときは、フルのトレースログを出力する
            // それ以外のログレベルでは、ログの呼び出し箇所のみを表示する
            // stackTraceのパース

            string outputStackTrace = "";
            string outputTimeStamp = "";

            // ログファイルに出力するログレベル
            // LOG:Log Level
            // WNG:Warning Level
            // ERR:Error Level
            // EXP:Exception Level
            // AST:Assert Level
            string outputLogLevel = "";

            outputTimeStamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");

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
                // そのままトレースログを出力すると見づらいので
                // インデントを付ける
                string[] lines = stackTrace.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = new string(' ', $"{outputTimeStamp} {outputLogLevel} {logText} ".Length) + lines[i];
                }
                // インデントを追加したフルのトレースログを出力
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

            // ファイルが存在しなければ作成する
            if (!File.Exists(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME))
            {
                File.Create(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME).Close();
            }
            using var writer = new StreamWriter(LOGFOLDER_PATH + OUTPUT_LOGFILE_NAME, true, Encoding.GetEncoding("UTF-8"));
            
            writer.WriteLine($"{outputTimeStamp} {outputLogLevel} {logText} {outputStackTrace}");
            
        }

        /// <summary>
        /// ログフォルダの中から、最も小さくなおかつ存在しないログファイルのログファイルナンバーを取得する。
        /// ex)2023-01-01-1.log 2023-01-01-2.logという二つのログファイルが存在した場合
        /// 本メソッドは3という数値を返す。初期値は1。
        /// </summary>
        /// <returns></returns>
        internal static int FetchLogFileNumber()
        {
            // ディレクトリ内のすべての.logファイルを取得する。
            var logFiles = Directory.GetFiles(LOGFOLDER_PATH, DateTime.UtcNow.ToString("yyyy-MM-dd") + "*.log");

            // ログファイルが存在しない場合初期値である1を返す。
            if (logFiles == null)
            {
                return 1;
            }

            // YYYY-MM-DD-number.log　形式のファイル名を想定。
            var regex = new Regex(@"\d{4}-\d{2}-\d{2}-(\d+)\.log$");

            // ファイル名から数値を抽出
            var numbers = logFiles.Select(path =>
            {
                var match = regex.Match(Path.GetFileName(path));
                // 形式に従わないファイル名を検出した場合-1を返す。
                return match.Success ? int.Parse(match.Groups[1].Value) : -1;
            })
                // 形式に従わないファイル名は、ソート対象から除外する。
            .Where(number => number != -1)
            .OrderBy(number => number)
            .ToList();

            // 存在しない最小の数値を取得する。
            var missingNumber = Enumerable.Range(1, numbers.Count + 1).Except(numbers).FirstOrDefault();

            return missingNumber;
            
        }

        /// <summary>
        /// EAUログをログファイルに出力する。
        /// </summary>
        /// <param name="message">出力するメッセージ</param>
        public static void writeEAULog(string message)
        {
            Debug.Log(EAULOG_PREFIX + message);
        }

    }
}