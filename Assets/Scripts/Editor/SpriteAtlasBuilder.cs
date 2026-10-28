using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace SmallGames.Editor
{
    /// <summary>
    /// 一键把指定文件夹里的图片打成 Sprite Atlas（V2，扩展名 .spriteatlasv2）。
    /// 不走 UnityEditor.U2D.SpriteAtlasExtensions：团结引擎把它塞进了 Unity.InstantGame.Editor 程序集，
    /// asmdef 引用成本高。这里改成「写图集 YAML + 改 .meta 里的 importer 参数」，效果一致且不挑引擎版本。
    /// 用法：菜单 SmallGames/图集打包工具 打开窗口；或在 Project 里选中文件夹后走 SmallGames/为选中文件夹生成图集。
    /// </summary>
    public class SpriteAtlasBuilderWindow : EditorWindow
    {
        private const string PrefsKey = "SmallGames.SpriteAtlasBuilder.Settings";
        private const string AtlasExt = ".spriteatlasv2";
        private const string YamlTagLine = "%TAG !u! tag:yousandi.cn,2023:";

        private static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".bmp" };
        private static readonly int[] SizeOptions = { 256, 512, 1024, 2048, 4096 };
        private static readonly int[] PaddingOptions = { 2, 4, 8 };

        [SerializeField] private List<string> _folders = new List<string>();
        [SerializeField] private string _manual = "Assets/UI";
        [SerializeField] private bool _advanced;
        [SerializeField] private string _log = string.Empty;
        [SerializeField] private Vector2 _scroll;

        private AtlasSettings _settings;

        [MenuItem("SmallGames/图集打包工具")]
        public static void Open()
        {
            var window = GetWindow<SpriteAtlasBuilderWindow>(false, "图集打包", true);
            window.minSize = new Vector2(430, 540);
        }

        [MenuItem("SmallGames/为选中文件夹生成图集", false, 100)]
        public static void BuildSelectionFromMenu()
        {
            var folders = SelectedFolders();
            if (folders.Count == 0)
            {
                EditorUtility.DisplayDialog("图集", "请先在 Project 窗口里选中至少一个文件夹。", "确定");
                return;
            }

            var report = Build(folders, LoadSettings());
            Debug.Log("[图集] " + report);
            EditorUtility.DisplayDialog("图集", report, "确定");
        }

        [MenuItem("SmallGames/为选中文件夹生成图集", true)]
        public static bool BuildSelectionValidate()
        {
            return SelectedFolders().Count > 0;
        }

        [MenuItem("Assets/生成图集（Sprite Atlas）", false, 2000)]
        private static void BuildSelectionFromAssetMenu()
        {
            BuildSelectionFromMenu();
        }

        [MenuItem("Assets/生成图集（Sprite Atlas）", true)]
        private static bool BuildSelectionFromAssetMenuValidate()
        {
            return SelectedFolders().Count > 0;
        }

        private void OnEnable()
        {
            _settings = LoadSettings();
            if (_folders == null)
                _folders = new List<string>();
        }

        private void OnDisable()
        {
            if (_settings != null)
                SaveSettings(_settings);
        }

        private void OnGUI()
        {
            if (_settings == null)
                _settings = LoadSettings();

            EditorGUILayout.LabelField("源文件夹", EditorStyles.boldLabel);
            for (var i = _folders.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_folders[i], EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("移除", GUILayout.Width(46)))
                    _folders.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }

            if (_folders.Count == 0)
                EditorGUILayout.HelpBox("还没有文件夹。在 Project 里选中文件夹后点下面的按钮，或手动填路径添加。", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加 Project 选中的文件夹", GUILayout.Height(22)))
            {
                var added = 0;
                var folders = SelectedFolders();
                for (var i = 0; i < folders.Count; i++)
                {
                    if (_folders.Contains(folders[i]))
                        continue;
                    _folders.Add(folders[i]);
                    added++;
                }

                if (added == 0)
                    _log = "Project 里没有选中新文件夹。";
            }

            if (GUILayout.Button("清空", GUILayout.Width(60), GUILayout.Height(22)))
                _folders.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _manual = EditorGUILayout.TextField(_manual);
            if (GUILayout.Button("添加路径", GUILayout.Width(74)))
                AddManualPath();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            _settings.PerFolder = EditorGUILayout.Toggle("每个文件夹一个图集", _settings.PerFolder);
            if (!_settings.PerFolder)
                _settings.MergedName = EditorGUILayout.TextField("合并图集名", _settings.MergedName);

            _settings.OutputLocation = EditorGUILayout.Popup(
                "图集存放位置",
                _settings.OutputLocation,
                new[] { "源文件夹内", "统一目录" });
            if (_settings.OutputLocation == 1)
            {
                EditorGUILayout.BeginHorizontal();
                _settings.OutputFolder = EditorGUILayout.TextField("输出目录", _settings.OutputFolder);
                if (GUILayout.Button("浏览", GUILayout.Width(50)))
                {
                    var picked = EditorUtility.OpenFolderPanel("选择图集输出目录", _settings.OutputFolder, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        var rel = ToAssetPath(picked);
                        if (string.IsNullOrEmpty(rel))
                            _log = "输出目录必须在当前工程的 Assets 下。";
                        else
                            _settings.OutputFolder = rel;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            _settings.IncludeSubfolders = EditorGUILayout.Toggle("包含子文件夹", _settings.IncludeSubfolders);
            _settings.ForceSpriteImport = EditorGUILayout.Toggle("图片导入设为 Sprite", _settings.ForceSpriteImport);

            EditorGUILayout.Space();
            _advanced = EditorGUILayout.Foldout(_advanced, "打包参数", true);
            if (_advanced)
            {
                _settings.MaxTextureSize = EditorGUILayout.IntPopup(
                    "Max Size",
                    _settings.MaxTextureSize,
                    new[] { "256", "512", "1024", "2048", "4096" },
                    SizeOptions);
                _settings.Padding = EditorGUILayout.IntPopup(
                    "Padding",
                    _settings.Padding,
                    new[] { "2", "4", "8" },
                    PaddingOptions);
                _settings.FilterMode = EditorGUILayout.Popup(
                    "Filter Mode",
                    _settings.FilterMode,
                    new[] { "Point", "Bilinear", "Trilinear" });
                _settings.CompressionQuality = EditorGUILayout.IntSlider(
                    "压缩质量",
                    _settings.CompressionQuality,
                    0,
                    100);
                _settings.CrunchedCompression = EditorGUILayout.Toggle("Crunched 压缩", _settings.CrunchedCompression);
                _settings.GenerateMipMaps = EditorGUILayout.Toggle("Generate Mip Maps", _settings.GenerateMipMaps);
                _settings.AllowRotation = EditorGUILayout.Toggle("允许旋转", _settings.AllowRotation);
                _settings.TightPacking = EditorGUILayout.Toggle("紧凑打包", _settings.TightPacking);
                _settings.AlphaDilation = EditorGUILayout.Toggle("Alpha 膨胀", _settings.AlphaDilation);
                _settings.IncludeInBuild = EditorGUILayout.Toggle("Include In Build", _settings.IncludeInBuild);

                EditorGUILayout.Space();
                _settings.AddToAddressables = EditorGUILayout.Toggle("登记到 Addressables", _settings.AddToAddressables);
                if (_settings.AddToAddressables)
                    _settings.AddressablesGroup = EditorGUILayout.TextField("目标分组", _settings.AddressablesGroup);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("生成 / 更新图集", GUILayout.Height(32)))
            {
                SaveSettings(_settings);
                _log = Build(_folders, _settings);
                Debug.Log("[图集] " + _log);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清理失效条目"))
                _log = Clean(AtlasPathsOf(_folders, _settings));
            if (GUILayout.Button("存为默认参数"))
            {
                SaveSettings(_settings);
                _log = "参数已保存，下次打开窗口或走右键菜单时生效。";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(_log, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void AddManualPath()
        {
            var path = string.IsNullOrEmpty(_manual) ? string.Empty : _manual.Replace('\\', '/').Trim();
            if (!AssetDatabase.IsValidFolder(path))
            {
                _log = "不是有效的 Assets 内文件夹：" + _manual;
                return;
            }

            if (!_folders.Contains(path))
                _folders.Add(path);
        }

        // ---------------------------------------------------------------- 主流程

        public static string Build(IList<string> folders, AtlasSettings s)
        {
            var report = new StringBuilder();
            if (folders == null || folders.Count == 0)
                return "没有指定文件夹。";
            if (s == null)
                s = new AtlasSettings();

            var jobs = Plan(folders, s);
            if (jobs.Count == 0)
                return "没有可打包的图片。";

            var converted = 0;
            if (s.ForceSpriteImport)
            {
                for (var i = 0; i < jobs.Count; i++)
                    converted += EnsureSpriteImport(jobs[i].Images);
            }

            var created = 0;
            var updated = 0;
            for (var i = 0; i < jobs.Count; i++)
            {
                var abs = ToAbsolute(jobs[i].Path);
                var existed = File.Exists(abs);
                WriteAtlasFile(jobs[i]);
                if (existed)
                    updated++;
                else
                    created++;
            }

            AssetDatabase.Refresh();

            var spriteTotal = 0;
            for (var i = 0; i < jobs.Count; i++)
            {
                // 保险：万一 Refresh 没给新文件生成 .meta，这里先单独导一次。
                if (!File.Exists(ToAbsolute(jobs[i].Path) + ".meta"))
                    AssetDatabase.ImportAsset(jobs[i].Path, ImportAssetOptions.ForceUpdate);

                ApplyMetaSettings(jobs[i].Path, s);
                AssetDatabase.ImportAsset(jobs[i].Path, ImportAssetOptions.ForceUpdate);
                spriteTotal += jobs[i].Images.Count;

                var capacity = (long)s.MaxTextureSize * s.MaxTextureSize;
                var area = jobs[i].Images.Count <= 512 ? TotalArea(jobs[i].Images) : -1L;

                var line = new StringBuilder();
                line.Append(jobs[i].Path).Append("  (").Append(jobs[i].Images.Count).Append(" 张");
                if (area >= 0 && capacity > 0)
                    line.Append("，占用 ").Append(area * 100L / capacity).Append('%');
                line.Append(')');
                report.AppendLine(line.ToString());
                if (area > capacity)
                    report.AppendLine("  ⚠ 精灵总面积超过 Max Size 的画布，建议拆成多个图集或调大 Max Size。");

                if (s.AddToAddressables && MarkAddressable(jobs[i].Path, s.AddressablesGroup, report))
                    report.AppendLine("  已登记到 Addressables 分组 " + s.AddressablesGroup + "。");
            }

            // .meta 是磁盘上改的，再刷一次确保 Unity 读到新的 importer 参数。
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            var head = new StringBuilder();
            head.Append("新建 ").Append(created).Append(" 个，更新 ").Append(updated).Append(" 个图集，共 ")
                .Append(spriteTotal).Append(" 张精灵。");
            if (converted > 0)
                head.Append(" 另有 ").Append(converted).Append(" 张图片的导入类型改成了 Sprite。");
            report.Insert(0, head + "\n");
            return report.ToString().TrimEnd();
        }

        /// <summary>清掉图集里已经不存在的图片条目（图片被删/被移走后图集会残留死引用）。</summary>
        public static string Clean(IList<string> atlasPaths)
        {
            if (atlasPaths == null || atlasPaths.Count == 0)
                return "没有可清理的图集。先添加文件夹或勾选和生成时一样的输出设置。";

            var removed = 0;
            var touched = 0;
            var report = new StringBuilder();
            for (var i = 0; i < atlasPaths.Count; i++)
            {
                var abs = ToAbsolute(atlasPaths[i]);
                if (!File.Exists(abs))
                    continue;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(abs);
                }
                catch (Exception e)
                {
                    report.AppendLine("读取失败 " + atlasPaths[i] + "：" + e.Message);
                    continue;
                }

                var kept = new List<string>(lines.Length);
                var inPackables = false;
                var n = 0;
                for (var j = 0; j < lines.Length; j++)
                {
                    var line = lines[j];
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("packables:", StringComparison.Ordinal))
                    {
                        inPackables = true;
                        kept.Add(line);
                        continue;
                    }

                    if (inPackables)
                    {
                        var guid = GuidInLine(line);
                        if (string.IsNullOrEmpty(guid))
                        {
                            inPackables = false;
                            kept.Add(line);
                            continue;
                        }

                        var asset = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(asset) || !File.Exists(ToAbsolute(asset)))
                        {
                            n++;
                            continue;
                        }

                        kept.Add(line);
                        continue;
                    }

                    kept.Add(line);
                }

                if (n <= 0)
                    continue;

                File.WriteAllLines(abs, kept.ToArray());
                AssetDatabase.ImportAsset(atlasPaths[i], ImportAssetOptions.ForceUpdate);
                removed += n;
                touched++;
                report.AppendLine(atlasPaths[i] + "  移除 " + n + " 条");
            }

            AssetDatabase.SaveAssets();
            if (removed == 0)
                return "没有发现失效条目。";
            report.Insert(0, "共移除 " + removed + " 条失效条目，涉及 " + touched + " 个图集。\n");
            return report.ToString().TrimEnd();
        }

        private static List<AtlasJob> Plan(IList<string> folders, AtlasSettings s)
        {
            var jobs = new List<AtlasJob>();
            if (s.PerFolder)
            {
                for (var i = 0; i < folders.Count; i++)
                {
                    var folder = folders[i];
                    if (!AssetDatabase.IsValidFolder(folder))
                        continue;

                    var images = CollectImages(folder, s.IncludeSubfolders);
                    if (images.Count == 0)
                        continue;

                    var name = SafeName(Path.GetFileName(folder));
                    var dir = s.OutputLocation == 1 ? s.OutputFolder : folder;
                    jobs.Add(new AtlasJob { Path = dir.TrimEnd('/') + "/" + name + AtlasExt, Images = images });
                }

                return jobs;
            }

            var all = new List<string>();
            for (var i = 0; i < folders.Count; i++)
            {
                if (!AssetDatabase.IsValidFolder(folders[i]))
                    continue;
                var images = CollectImages(folders[i], s.IncludeSubfolders);
                for (var j = 0; j < images.Count; j++)
                {
                    if (!all.Contains(images[j]))
                        all.Add(images[j]);
                }
            }

            if (all.Count == 0)
                return jobs;

            var root = folders.Count > 0 ? folders[0] : "Assets";
            var outDir = s.OutputLocation == 1 ? s.OutputFolder : root;
            jobs.Add(new AtlasJob { Path = outDir.TrimEnd('/') + "/" + SafeName(s.MergedName) + AtlasExt, Images = all });
            return jobs;
        }

        private static List<string> CollectImages(string folder, bool includeSubfolders)
        {
            var result = new List<string>();
            var abs = ToAbsolute(folder);
            if (!Directory.Exists(abs))
                return result;

            var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(abs, "*.*", option);
            for (var i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                var ext = Path.GetExtension(files[i]).ToLowerInvariant();
                if (Array.IndexOf(ImageExts, ext) < 0)
                    continue;
                var assetPath = ToAssetPath(files[i]);
                if (!string.IsNullOrEmpty(assetPath))
                    result.Add(assetPath);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static int EnsureSpriteImport(List<string> images)
        {
            var n = 0;
            for (var i = 0; i < images.Count; i++)
            {
                var importer = AssetImporter.GetAtPath(images[i]) as TextureImporter;
                if (importer == null)
                    continue;
                if (importer.textureType == TextureImporterType.Sprite)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                n++;
            }

            return n;
        }

        private static void WriteAtlasFile(AtlasJob job)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine(YamlTagLine);
            sb.AppendLine("--- !u!612988286 &1");
            sb.AppendLine("SpriteAtlasAsset:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_Name: ");
            sb.AppendLine("  serializedVersion: 3");
            sb.AppendLine("  m_MasterAtlas: {fileID: 0}");
            sb.AppendLine("  m_ImporterData:");
            sb.AppendLine("    packables:");
            for (var i = 0; i < job.Images.Count; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(job.Images[i]);
                if (string.IsNullOrEmpty(guid))
                    continue;
                sb.Append("    - {fileID: 2800000, guid: ").Append(guid).AppendLine(", type: 3}");
            }

            sb.AppendLine("  m_IsVariant: 0");
            sb.AppendLine("  m_IsPlaceholder: 0");

            var abs = ToAbsolute(job.Path);
            var dir = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(abs, sb.ToString());
        }

        private static void ApplyMetaSettings(string atlasPath, AtlasSettings s)
        {
            var meta = ToAbsolute(atlasPath) + ".meta";
            if (!File.Exists(meta))
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(meta);
            }
            catch (Exception)
            {
                return;
            }

            var block = string.Empty;
            var dirty = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.Length > 0 && !line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("\t", StringComparison.Ordinal))
                {
                    var colon = trimmed.IndexOf(':');
                    if (colon > 0)
                        block = trimmed.Substring(0, colon);
                }

                var next = line;
                if (block == "textureSettings")
                {
                    SetField(ref next, "    ", "maxTextureSize", s.MaxTextureSize.ToString());
                    SetField(ref next, "    ", "compressionQuality", s.CompressionQuality.ToString());
                    SetField(ref next, "    ", "crunchedCompression", s.CrunchedCompression ? "1" : "0");
                    SetField(ref next, "    ", "filterMode", s.FilterMode.ToString());
                    SetField(ref next, "    ", "generateMipMaps", s.GenerateMipMaps ? "1" : "0");
                }
                else if (block == "packingSettings")
                {
                    SetField(ref next, "    ", "padding", s.Padding.ToString());
                    SetField(ref next, "    ", "enableRotation", s.AllowRotation ? "1" : "0");
                    SetField(ref next, "    ", "enableTightPacking", s.TightPacking ? "1" : "0");
                    SetField(ref next, "    ", "enableAlphaDilation", s.AlphaDilation ? "1" : "0");
                }
                else if (block == "SpriteAtlasImporter")
                {
                    SetField(ref next, "  ", "bindAsDefault", s.IncludeInBuild ? "1" : "0");
                }

                if (next == line)
                    continue;

                lines[i] = next;
                dirty = true;
            }

            if (dirty)
                File.WriteAllLines(meta, lines);
        }

        private static void SetField(ref string line, string indent, string key, string value)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith(key + ":", StringComparison.Ordinal))
                return;
            line = indent + key + ": " + value;
        }

        private static bool MarkAddressable(string atlasPath, string groupName, StringBuilder report)
        {
            if (string.IsNullOrEmpty(groupName))
                return false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                report.AppendLine("  没有 AddressableAssetSettings，跳过登记。");
                return false;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                report.AppendLine("  Addressables 里没有分组 " + groupName + "，跳过登记。");
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(atlasPath);
            if (string.IsNullOrEmpty(guid))
                return false;

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
                return false;

            entry.address = AddressFromPath(atlasPath);
            EditorUtility.SetDirty(settings);
            return true;
        }

        private static string AddressFromPath(string assetPath)
        {
            var path = assetPath.Replace('\\', '/');
            if (path.StartsWith("Assets/Content/", StringComparison.Ordinal))
                path = path.Substring("Assets/Content/".Length);
            else if (path.StartsWith("Assets/", StringComparison.Ordinal))
                path = path.Substring("Assets/".Length);

            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
                path = path.Substring(0, path.Length - ext.Length);
            return path;
        }

        private static long TotalArea(List<string> images)
        {
            long area = 0;
            for (var i = 0; i < images.Count; i++)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(images[i]);
                if (texture == null)
                    continue;
                area += (long)texture.width * texture.height;
            }

            return area;
        }

        // ---------------------------------------------------------------- 工具

        private static List<string> AtlasPathsOf(IList<string> folders, AtlasSettings s)
        {
            var jobs = Plan(folders, s);
            var paths = new List<string>(jobs.Count);
            for (var i = 0; i < jobs.Count; i++)
                paths.Add(jobs[i].Path);
            return paths;
        }

        private static List<string> SelectedFolders()
        {
            var result = new List<string>();
            var guids = Selection.assetGUIDs;
            if (guids == null)
                return result;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                    continue;
                if (!result.Contains(path))
                    result.Add(path);
            }

            return result;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Atlas";
            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return sb.ToString();
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
        }

        private static string ToAbsolute(string assetPath)
        {
            var rel = assetPath.Replace('\\', '/');
            if (rel.StartsWith("/", StringComparison.Ordinal))
                rel = rel.Substring(1);
            return Path.GetFullPath(Path.Combine(ProjectRoot(), rel)).Replace('\\', '/');
        }

        private static string ToAssetPath(string absolutePath)
        {
            var full = Path.GetFullPath(absolutePath).Replace('\\', '/');
            var root = ProjectRoot();
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;
            return full.Substring(root.Length).TrimStart('/');
        }

        private static string GuidInLine(string line)
        {
            var at = line.IndexOf("guid: ", StringComparison.Ordinal);
            if (at < 0)
                return null;
            var start = at + "guid: ".Length;
            if (start + 32 > line.Length)
                return null;
            var guid = line.Substring(start, 32);
            for (var i = 0; i < guid.Length; i++)
            {
                if (!char.IsLetterOrDigit(guid[i]))
                    return null;
            }

            return guid;
        }

        private static AtlasSettings LoadSettings()
        {
            var json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new AtlasSettings();
            try
            {
                var loaded = JsonUtility.FromJson<AtlasSettings>(json);
                return loaded ?? new AtlasSettings();
            }
            catch (Exception)
            {
                return new AtlasSettings();
            }
        }

        private static void SaveSettings(AtlasSettings s)
        {
            if (s == null)
                return;
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(s));
        }

        private class AtlasJob
        {
            public string Path;
            public List<string> Images = new List<string>();
        }

        [Serializable]
        public class AtlasSettings
        {
            public bool PerFolder = true;
            public string MergedName = "Atlas";
            public int OutputLocation;
            public string OutputFolder = "Assets/UI/Atlas";
            public bool IncludeSubfolders;
            public bool ForceSpriteImport = true;
            public int MaxTextureSize = 2048;
            public int FilterMode = 1;
            public int Padding = 4;
            public int CompressionQuality = 50;
            public bool CrunchedCompression;
            public bool GenerateMipMaps;
            public bool AllowRotation = true;
            public bool TightPacking = true;
            public bool AlphaDilation;
            public bool IncludeInBuild;
            public bool AddToAddressables;
            public string AddressablesGroup = "core_ui";
        }
    }
}
