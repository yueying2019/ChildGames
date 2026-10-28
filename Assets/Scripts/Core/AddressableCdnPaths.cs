using System;
using System.IO;
using System.Reflection;

namespace SmallGames
{
    /// <summary>
    /// Addressables Remote.LoadPath 用 {SmallGames.AddressableCdnPaths.RemoteLoadRoot}。
    /// 本地暂存在 CustomCloudAssets/Addressables；UOS 上在 CUS/Addressables（dataFileSubPrefix）。
    /// </summary>
    public static class AddressableCdnPaths
    {
        public const string FolderName = "Addressables";
        public const string CdnPrefix = "CUS";
        public const string StagingRoot = "CustomCloudAssets";

        public static string RemoteLoadRoot
        {
            get
            {
                // 若仍指向 StreamingAssets，优先用包内路径。
                var fromStreaming = FromStreamingAssetsRoot();
                if (!string.IsNullOrEmpty(fromStreaming))
                {
                    LogOnce(fromStreaming);
                    return fromStreaming;
                }

                var root = TryAutoStreamingRoot();
                if (!string.IsNullOrEmpty(root))
                {
                    var path = ToCdnFolder(root);
                    LogOnce(path);
                    return path;
                }

#if UNITY_EDITOR
                var local = Path.Combine(ProjectRoot, StagingRoot, FolderName);
                if (Directory.Exists(local))
                    return new Uri(local).AbsoluteUri.TrimEnd('/');
#endif
                return FolderName;
            }
        }

        public static string StagingFolder => Path.Combine(ProjectRoot, StagingRoot, FolderName);

        private static bool _loggedRoot;

        private static void LogOnce(string path)
        {
            if (_loggedRoot || string.IsNullOrEmpty(path))
                return;
            _loggedRoot = true;
            UnityEngine.Debug.Log("[Addressables] RemoteLoadRoot=" + path);
        }

        public static string ProjectRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
#else
                return UnityEngine.Application.persistentDataPath;
#endif
            }
        }

        private static string TryAutoStreamingRoot()
        {
            const string prop = "CustomCloudAssetsRoot";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type type = null;
                try
                {
                    type = assemblies[i].GetType("AutoStreaming", false)
                        ?? assemblies[i].GetType("Tuanjie.InstantGame.AutoStreaming", false)
                        ?? assemblies[i].GetType("Unity.AutoStreaming.AutoStreaming", false);
                }
                catch
                {
                    continue;
                }

                if (type == null)
                    continue;

                var flags = BindingFlags.Public | BindingFlags.Static;
                var property = type.GetProperty(prop, flags);
                if (property != null)
                {
                    var value = property.GetValue(null, null) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                var field = type.GetField(prop, flags);
                if (field != null)
                {
                    var value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            return null;
        }

        private static string FromStreamingAssetsRoot()
        {
            var streaming = UnityEngine.Application.streamingAssetsPath;
            if (string.IsNullOrEmpty(streaming)
                || streaming.IndexOf("://", StringComparison.Ordinal) < 0)
                return null;

            var root = TrimSlash(streaming);
            const string suffix = "/StreamingAssets";
            if (root.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                root = root.Substring(0, root.Length - suffix.Length);
            return ToCdnFolder(root);
        }

        private static string ToCdnFolder(string contentRoot)
        {
            var root = TrimSlash(contentRoot);
            var prefixed = "/" + CdnPrefix;
            var folder = "/" + CdnPrefix + "/" + FolderName;
            if (root.EndsWith(folder, StringComparison.OrdinalIgnoreCase))
                return root;
            if (root.EndsWith(prefixed, StringComparison.OrdinalIgnoreCase))
                return root + "/" + FolderName;
            return root + folder;
        }

        private static string TrimSlash(string path)
        {
            return path.TrimEnd('/', '\\');
        }
    }
}
