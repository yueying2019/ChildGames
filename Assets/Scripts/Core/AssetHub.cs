using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 单机资源入口。优先查 ContentIndex 的直接引用，编辑器里再回退到 AssetDatabase。
    /// </summary>
    public static class AssetHub
    {
        private static readonly Dictionary<string, UnityEngine.Object> Cache =
            new Dictionary<string, UnityEngine.Object>(64);
        private static readonly Dictionary<string, Array> LabelArrays =
            new Dictionary<string, Array>(8);

        public static bool Ready { get; private set; }
        private static bool _initTried;

        public static bool BlockingLoadsAllowed => true;

        public static void Initialize()
        {
            if (_initTried)
                return;
            _initTried = true;
            BindIndex();
            Ready = true;
        }

        public static IEnumerator InitializeRoutine()
        {
            Initialize();
            yield break;
        }

        private static void BindIndex()
        {
            if (ContentIndex.Current != null)
                return;
#if UNITY_EDITOR
            var index = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentIndex>(ContentIndex.AssetPath);
            if (index != null)
                ContentIndex.Bind(index);
#endif
        }

        public static T Load<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
                return null;
            if (!Ready)
                Initialize();
            if (Cache.TryGetValue(key, out var cached) && cached is T hit)
                return hit;

            var fromIndex = ContentIndex.Current != null ? ContentIndex.Current.Get<T>(key) : null;
            if (fromIndex != null)
            {
                Cache[key] = fromIndex;
                return fromIndex;
            }

            var fromEditor = LoadFromEditor<T>(key);
            if (fromEditor != null)
            {
                Cache[key] = fromEditor;
                return fromEditor;
            }

            Debug.LogError("[AssetHub] 缺少资源 " + key);
            return null;
        }

        public static T[] LoadAll<T>(string label, string resourceFolder) where T : UnityEngine.Object
        {
            if (!Ready)
                Initialize();
            var cacheKey = typeof(T).FullName + "|" + label;
            if (!string.IsNullOrEmpty(label) && LabelArrays.TryGetValue(cacheKey, out var cached))
                return (T[])cached;

            if (ContentIndex.Current != null && !string.IsNullOrEmpty(label))
            {
                var fromIndex = ContentIndex.Current.GetAll<T>(label);
                if (fromIndex.Length > 0)
                {
                    LabelArrays[cacheKey] = fromIndex;
                    return fromIndex;
                }
            }

            var fromCache = CollectCached<T>(resourceFolder);
            if (fromCache.Length > 0)
            {
                if (!string.IsNullOrEmpty(label))
                    LabelArrays[cacheKey] = fromCache;
                return fromCache;
            }

            var editorList = LoadAllFromEditor<T>(resourceFolder);
            if (editorList.Length > 0)
            {
                if (!string.IsNullOrEmpty(label))
                    LabelArrays[cacheKey] = editorList;
                return editorList;
            }

            return Array.Empty<T>();
        }

        public static void Preload(string label)
        {
        }

        public static IEnumerator PreloadLabelRoutine(string label)
        {
            yield break;
        }

        public static void PreloadMode(GameModeType mode)
        {
        }

        public static IEnumerator PreloadModeRoutine(GameModeType mode, Action<float> onProgress)
        {
            onProgress?.Invoke(1f);
            yield break;
        }

        public static IEnumerator PrepareStartupRoutine(Action<float, string> onProgress)
        {
            if (!Ready)
                Initialize();
            onProgress?.Invoke(1f, string.Empty);
            yield break;
        }

        public static void UnloadMode(GameModeType mode)
        {
            var prefix = AssetLabels.PrefixForMode(mode);
            if (string.IsNullOrEmpty(prefix))
                return;
            var keys = new List<string>(Cache.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i].StartsWith(prefix, StringComparison.Ordinal))
                    Cache.Remove(keys[i]);
            }
        }

        private static T LoadFromEditor<T>(string key) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            var folders = new[] { "Assets/Content/", "Assets/" };
            var exts = new[] { ".prefab", ".asset", ".json", ".ogg", ".wav", ".mp3", ".png", ".jpg", ".otf", ".ttf" };
            for (var f = 0; f < folders.Length; f++)
            {
                for (var e = 0; e < exts.Length; e++)
                {
                    var path = folders[f] + key + exts[e];
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                        return asset;
                    if (!typeof(Component).IsAssignableFrom(typeof(T)))
                        continue;
                    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null)
                        continue;
                    var comp = go.GetComponent<T>();
                    if (comp != null)
                        return comp;
                }
            }
#endif
            return null;
        }

        private static T[] CollectCached<T>(string resourceFolder) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(resourceFolder) || Cache.Count == 0)
                return Array.Empty<T>();

            var folder = resourceFolder.Replace('\\', '/').Trim('/');
            var list = new List<T>(16);
            foreach (var pair in Cache)
            {
                if (!(pair.Value is T asset) || asset == null)
                    continue;
                var key = pair.Key != null ? pair.Key.Replace('\\', '/') : string.Empty;
                if (key.IndexOf(folder, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                list.Add(asset);
            }

            return list.Count > 0 ? list.ToArray() : Array.Empty<T>();
        }

        private static T[] LoadAllFromEditor<T>(string folder) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(folder))
                return Array.Empty<T>();
            var roots = new[] { "Assets/Content/" + folder, "Assets/" + folder };
            var list = new List<T>(16);
            for (var i = 0; i < roots.Length; i++)
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder(roots[i]))
                    continue;
                var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { roots[i] });
                for (var g = 0; g < guids.Length; g++)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[g]);
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                        list.Add(asset);
                }
            }

            return list.Count > 0 ? list.ToArray() : Array.Empty<T>();
#else
            return Array.Empty<T>();
#endif
        }
    }
}
