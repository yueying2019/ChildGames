using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmallGames.Editor
{
    public static class ContentIndexBuilder
    {
        private const string SoundsFolder = "Assets/Sounds";
        private const string ContentRoot = "Assets/Content";
        private const string ScenePath = "Assets/Scenes/GameScene.scene";

        [MenuItem("SmallGames/刷新内容索引")]
        public static void ApplyFromMenu()
        {
            Apply();
            EditorUtility.DisplayDialog(
                "内容索引",
                "已按 Assets/Content 登记直接引用。运行时不再加载 Addressables 包。",
                "确定");
        }

        [InitializeOnLoadMethod]
        private static void AutoApplyIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                var index = AssetDatabase.LoadAssetAtPath<ContentIndex>(ContentIndex.AssetPath);
                if (index != null && index.Addresses != null && index.Addresses.Length > 0)
                    return;
                Apply();
            };
        }

        public static void Apply()
        {
            var index = AssetDatabase.LoadAssetAtPath<ContentIndex>(ContentIndex.AssetPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<ContentIndex>();
                if (!AssetDatabase.IsValidFolder(ContentRoot))
                    AssetDatabase.CreateFolder("Assets", "Content");
                AssetDatabase.CreateAsset(index, ContentIndex.AssetPath);
            }

            var addresses = new List<string>(256);
            var labels = new List<string>(256);
            var extras = new List<string>(256);
            var assets = new List<UnityEngine.Object>(256);

            MarkFolder(addresses, labels, extras, assets, ContentRoot + "/UI", AssetLabels.Core, null);
            MarkAsset(addresses, labels, extras, assets, ContentRoot + "/GameBalance.asset", AssetLabels.Core, null);
            MarkAsset(addresses, labels, extras, assets, "Assets/Font/NotoSansSC-UI.otf", AssetLabels.Core, null);
            MarkFolder(addresses, labels, extras, assets, ContentRoot + "/Sounds", AssetLabels.Core, null, IsCoreSound);
            MarkFolder(addresses, labels, extras, assets, SoundsFolder, AssetLabels.Core, null, IsCoreSound);
            MarkCodexIcons(addresses, labels, extras, assets);

            MarkFolder(addresses, labels, extras, assets, ContentRoot + "/Lianliankan", AssetLabels.Lianliankan, AssetLabels.LianliankanLevels);
            MarkFolder(addresses, labels, extras, assets, ContentRoot + "/Sheep", AssetLabels.Sheep, AssetLabels.SheepLevels);
            MarkFolder(addresses, labels, extras, assets, ContentRoot + "/Match3", AssetLabels.Match3, AssetLabels.Match3Levels);
            MarkFolder(addresses, labels, extras, assets, "Assets/UI/Cats/Lianliankan", AssetLabels.Lianliankan, null, IsCatFrame);
            MarkFolder(addresses, labels, extras, assets, "Assets/UI/Cats/Sheep", AssetLabels.Sheep, null, IsCatFrame);
            MarkFolder(addresses, labels, extras, assets, "Assets/UI/Cats/Match3", AssetLabels.Match3, null, IsCatFrame);
            MarkModeBgm(addresses, labels, extras, assets, "bgm_lianliankan", AssetLabels.Lianliankan);
            MarkModeBgm(addresses, labels, extras, assets, "bgm_sheep", AssetLabels.Sheep);
            MarkModeBgm(addresses, labels, extras, assets, "bgm_match3", AssetLabels.Match3);

            Undo.RecordObject(index, "刷新内容索引");
            index.Addresses = addresses.ToArray();
            index.Labels = labels.ToArray();
            index.ExtraLabels = extras.ToArray();
            index.Assets = assets.ToArray();
            EditorUtility.SetDirty(index);
            WireScene(index);
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentIndex] 已登记 " + addresses.Count + " 项。");
        }

        private static void WireScene(ContentIndex index)
        {
            if (index == null || !File.Exists(ScenePath))
                return;

            var scene = SceneManager.GetActiveScene();
            var opened = false;
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                opened = true;
            }

            ContentIndexHolder holder = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                holder = roots[i].GetComponent<ContentIndexHolder>();
                if (holder != null)
                    break;
            }

            if (holder == null)
            {
                var go = new GameObject("ContentIndex");
                Undo.RegisterCreatedObjectUndo(go, "创建内容索引");
                holder = go.AddComponent<ContentIndexHolder>();
            }

            if (holder.Index != index)
            {
                Undo.RecordObject(holder, "绑定内容索引");
                holder.Index = index;
                EditorUtility.SetDirty(holder);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                Debug.Log("[ContentIndex] 已写入 " + ScenePath);
        }

        private static void MarkFolder(
            List<string> addresses,
            List<string> labels,
            List<string> extras,
            List<UnityEngine.Object> assets,
            string folder,
            string modeLabel,
            string levelsLabel,
            Func<string, bool> include = null)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;
                if (path.EndsWith(".cs") || path.EndsWith(".js") || path.EndsWith(".txt"))
                    continue;
                if (path.EndsWith("ContentIndex.asset"))
                    continue;
                if (include != null && !include(path))
                    continue;
                var extra = levelsLabel != null && IsLevelAsset(path) ? levelsLabel : null;
                MarkPath(addresses, labels, extras, assets, path, modeLabel, extra);
            }
        }

        private static void MarkCodexIcons(
            List<string> addresses,
            List<string> labels,
            List<string> extras,
            List<UnityEngine.Object> assets)
        {
            var path = ContentRoot + "/UI/Codex.json";
            if (!File.Exists(path))
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
                        MarkPath(addresses, labels, extras, assets, "Assets/" + icon + ".png", AssetLabels.Core, null);
                    }
                }
            }
        }

        private static void MarkAsset(
            List<string> addresses,
            List<string> labels,
            List<string> extras,
            List<UnityEngine.Object> assets,
            string path,
            string modeLabel,
            string extraLabel)
        {
            if (!File.Exists(path))
                return;
            MarkPath(addresses, labels, extras, assets, path, modeLabel, extraLabel);
        }

        private static void MarkModeBgm(
            List<string> addresses,
            List<string> labels,
            List<string> extras,
            List<UnityEngine.Object> assets,
            string fileName,
            string label)
        {
            var folder = ContentRoot + "/Sounds/Bgm";
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            var guids = AssetDatabase.FindAssets(fileName, new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;
                if (Path.GetFileNameWithoutExtension(path) != fileName)
                    continue;
                MarkPath(addresses, labels, extras, assets, path, label, null);
            }
        }

        private static void MarkPath(
            List<string> addresses,
            List<string> labels,
            List<string> extras,
            List<UnityEngine.Object> assets,
            string path,
            string modeLabel,
            string extraLabel)
        {
            var asset = LoadRecord(path);
            if (asset == null)
                return;
            var address = AddressFromPath(path);
            for (var i = 0; i < addresses.Count; i++)
            {
                if (addresses[i] != address)
                    continue;
                assets[i] = asset;
                labels[i] = modeLabel;
                extras[i] = extraLabel ?? string.Empty;
                return;
            }

            addresses.Add(address);
            labels.Add(modeLabel ?? string.Empty);
            extras.Add(extraLabel ?? string.Empty);
            assets.Add(asset);
        }

        private static UnityEngine.Object LoadRecord(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        private static bool IsCoreSound(string path)
        {
            var n = path.Replace('\\', '/');
            if (n.IndexOf("/Sounds/Bgm/bgm_lianliankan", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("/Sounds/Bgm/bgm_sheep", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("/Sounds/Bgm/bgm_match3", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        private static bool IsCatFrame(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return false;
            return path.IndexOf("/sheet.", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsLevelAsset(string path)
        {
            return path.Replace('\\', '/').IndexOf("/Levels/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string AddressFromPath(string assetPath)
        {
            var path = assetPath.Replace('\\', '/');
            const string content = "Assets/Content/";
            if (path.StartsWith(content))
                path = path.Substring(content.Length);
            else if (path.StartsWith("Assets/"))
                path = path.Substring("Assets/".Length);

            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
                path = path.Substring(0, path.Length - ext.Length);
            return path;
        }
    }
}
