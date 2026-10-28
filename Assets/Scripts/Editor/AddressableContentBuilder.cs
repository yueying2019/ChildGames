using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace SmallGames.Editor
{
    /// <summary>
    /// 图鉴重建时仍把图标登记进 core_ui。运行时加载走内容索引，不再提供 Addressables 菜单。
    /// </summary>
    public static class AddressableContentBuilder
    {
        private const string ContentRoot = "Assets/Content";

        /// <summary>
        /// 图鉴 json 里的 icon 是短路径，对应 Assets 下的 png。
        /// </summary>
        public static void MarkCodexIcons(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var path = ContentRoot + "/UI/Codex.json";
            if (settings == null || group == null || !File.Exists(path))
                return;
            var data = JsonUtility.FromJson<CodexCatalog.FileData>(File.ReadAllText(path));
            if (data == null || data.modes == null)
                return;
            var seen = new HashSet<string>();
            for (var m = 0; m < data.modes.Length; m++)
            {
                var mode = data.modes[m];
                if (mode == null || mode.tabs == null)
                    continue;
                for (var t = 0; t < mode.tabs.Length; t++)
                {
                    var tab = mode.tabs[t];
                    if (tab == null || tab.entries == null)
                        continue;
                    for (var e = 0; e < tab.entries.Length; e++)
                    {
                        var icon = tab.entries[e] != null ? tab.entries[e].icon : null;
                        if (string.IsNullOrEmpty(icon) || !seen.Add(icon))
                            continue;
                        MarkAsset(settings, group, "Assets/" + icon + ".png", AssetLabels.Core, null);
                    }
                }
            }
        }

        private static void MarkAsset(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string path,
            string modeLabel,
            string extraLabel)
        {
            if (!File.Exists(path))
                return;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return;

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
                return;
            entry.address = AddressFromPath(path);
            if (!string.IsNullOrEmpty(modeLabel))
                entry.SetLabel(modeLabel, true, true);
            if (!string.IsNullOrEmpty(extraLabel))
                entry.SetLabel(extraLabel, true, true);
        }

        private static string AddressFromPath(string assetPath)
        {
            var path = assetPath.Replace('\\', '/');
            const string content = "Assets/Content/";
            const string resources = "Assets/Resources/";
            if (path.StartsWith(content))
                path = path.Substring(content.Length);
            else if (path.StartsWith(resources))
                path = path.Substring(resources.Length);
            else if (path.StartsWith("Assets/"))
                path = path.Substring("Assets/".Length);

            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
                path = path.Substring(0, path.Length - ext.Length);
            return path;
        }
    }
}
