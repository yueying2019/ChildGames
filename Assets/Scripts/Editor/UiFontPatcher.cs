using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SmallGames.Editor
{
    /// <summary>
    /// 补界面字体。菜单扫整个 Assets；面板只补输入框里的字。只替换字体字节，不改 .meta。
    /// </summary>
    public class UiFontPatcher : EditorWindow
    {
        private const string FontAssetPath = "Assets/Font/NotoSansSC-UI.otf";

        private string _text = "";
        private string _result = "";
        private MessageType _resultType = MessageType.Info;
        private Vector2 _scroll;
        private bool _busy;

        [MenuItem("SmallGames/补全界面字体")]
        public static void ScanFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("补全界面字体", "请先退出播放模式。", "确定");
                return;
            }

            if (!File.Exists(FontAssetPath))
            {
                EditorUtility.DisplayDialog("补全界面字体", "找不到 " + FontAssetPath + "。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "补全界面字体",
                    "扫描 Assets 里的文字，把 NotoSansSC-UI.otf 缺少的字补上。\n不会改字体的 .meta。第一次会下载约 10MB 的完整字体到 Tools/FontCache。",
                    "补字",
                    "取消"))
                return;

            var project = Directory.GetParent(Application.dataPath).FullName;
            var script = Path.Combine(project, "Tools", "PatchUiFont.py");
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("补全界面字体", "找不到 Tools/PatchUiFont.py。", "确定");
                return;
            }

            EditorUtility.DisplayProgressBar("补全界面字体", "正在扫描界面文字", 0.3f);
            try
            {
                if (!TryRunPython(project, Quote(script) + " --scan", out var stdout, out var stderr, out var code))
                {
                    EditorUtility.DisplayDialog("补全界面字体", "找不到 Python。请安装 Python 3，并执行：\npython -m pip install fonttools", "确定");
                    return;
                }

                if (code != 0)
                {
                    var error = string.IsNullOrEmpty(stderr) ? "补字失败。" : stderr.Trim();
                    Debug.LogError("[UI] 补字失败\n" + error);
                    EditorUtility.DisplayDialog("补全界面字体", error, "确定");
                    return;
                }

                var added = ReadInt(stdout, "ADDED");
                var chars = ReadLine(stdout, "CHARS");
                var uncovered = ReadLine(stdout, "UNCOVERED");
                if (added > 0)
                    AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

                var message = added > 0
                    ? "已补 " + added + " 个字：" + chars + "\n重新导出微信包后，开发者工具才会显示。"
                    : "字体里已经有界面用到的字，不用补。";
                if (!string.IsNullOrEmpty(uncovered))
                    message += "\n完整字体里没有，没补上：" + uncovered;
                Debug.Log("[UI] " + message);
                EditorUtility.DisplayDialog("补全界面字体", message, "确定");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("补全界面字体", error.Message, "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("SmallGames/补字体")]
        public static void Open()
        {
            var window = GetWindow<UiFontPatcher>("补字体");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            _busy = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("把下面的文字补进 NotoSansSC-UI.otf", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("只补输入框里的字，重复字会自动去掉。不会改字体的 .meta。补完后需要重新导出微信包，开发者工具里才会显示。", MessageType.None);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("要补的文字");
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160f));
            _text = EditorGUILayout.TextArea(_text, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_busy || string.IsNullOrWhiteSpace(_text)))
            {
                if (GUILayout.Button("补进字体", GUILayout.Height(32f)))
                    Patch();
            }

            if (!string.IsNullOrEmpty(_result))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_result, _resultType);
            }
        }

        private void Patch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetResult("请先退出播放模式。", MessageType.Warning);
                return;
            }

            if (!File.Exists(FontAssetPath))
            {
                SetResult("找不到 " + FontAssetPath + "。", MessageType.Error);
                return;
            }

            var project = Directory.GetParent(Application.dataPath).FullName;
            var script = Path.Combine(project, "Tools", "PatchUiFont.py");
            if (!File.Exists(script))
            {
                SetResult("找不到 Tools/PatchUiFont.py。", MessageType.Error);
                return;
            }

            var input = Path.Combine(project, "Tools", "FontCache", "input.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(input));
            File.WriteAllText(input, _text, new UTF8Encoding(false));

            _busy = true;
            EditorUtility.DisplayProgressBar("补字体", "正在补字", 0.3f);
            try
            {
                var arguments = Quote(script) + " --text-file " + Quote(input);
                if (!TryRunPython(project, arguments, out var stdout, out var stderr, out var code))
                {
                    SetResult("找不到 Python。请安装 Python 3，并执行：\npython -m pip install fonttools", MessageType.Error);
                    return;
                }

                if (code != 0)
                {
                    var error = string.IsNullOrEmpty(stderr) ? "补字失败。" : stderr.Trim();
                    Debug.LogError("[UI] 补字失败\n" + error);
                    SetResult(error, MessageType.Error);
                    return;
                }

                var added = ReadInt(stdout, "ADDED");
                var chars = ReadLine(stdout, "CHARS");
                var already = ReadLine(stdout, "ALREADY");
                var uncovered = ReadLine(stdout, "UNCOVERED");
                if (added > 0)
                    AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

                var message = BuildMessage(added, chars, already, uncovered);
                Debug.Log("[UI] " + message);
                SetResult(message, added > 0 ? MessageType.Info : MessageType.Warning);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SetResult(error.Message, MessageType.Error);
            }
            finally
            {
                _busy = false;
                EditorUtility.ClearProgressBar();
                if (File.Exists(input))
                    File.Delete(input);
            }
        }

        private void SetResult(string message, MessageType type)
        {
            _result = message;
            _resultType = type;
            Repaint();
        }

        private static string BuildMessage(int added, string chars, string already, string uncovered)
        {
            var message = added > 0
                ? "已补 " + added + " 个字：" + chars + "\n重新导出微信包后，开发者工具才会显示。"
                : "这些字字体里已经有了，不用补。";
            if (!string.IsNullOrEmpty(already) && added > 0)
                message += "\n本来就有：" + already;
            if (!string.IsNullOrEmpty(uncovered))
                message += "\n完整字体里没有，没补上：" + uncovered;
            return message;
        }

        private static bool TryRunPython(string project, string arguments, out string stdout, out string stderr, out int code)
        {
            if (Run("python", arguments, project, out stdout, out stderr, out code))
                return true;
            return Run("py", "-3 " + arguments, project, out stdout, out stderr, out code);
        }

        private static bool Run(string fileName, string arguments, string project, out string stdout, out string stderr, out int code)
        {
            stdout = "";
            stderr = "";
            code = -1;
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = project,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                using (var process = Process.Start(start))
                {
                    if (process == null)
                        return false;
                    var output = new StringBuilder();
                    var error = new StringBuilder();
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (string.IsNullOrEmpty(eventArgs.Data))
                            return;
                        if (!eventArgs.Data.StartsWith("PROGRESS ", StringComparison.Ordinal))
                            output.AppendLine(eventArgs.Data);
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (!string.IsNullOrEmpty(eventArgs.Data))
                            error.AppendLine(eventArgs.Data);
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    stdout = output.ToString();
                    stderr = error.ToString();
                    code = process.ExitCode;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

        private static string ReadLine(string text, string key)
        {
            var prefix = key + " ";
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    return lines[i].Substring(prefix.Length);
            }

            return "";
        }

        private static int ReadInt(string text, string key)
        {
            var line = ReadLine(text, key);
            return int.TryParse(line, out var value) ? value : 0;
        }
    }
}
