using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace SmallGames.Editor
{
    /// <summary>
    /// 把 Assets/UI/ICO.png 写进 Player Settings 图标槽。图标必须是 Default 纹理，Sprite 不会出现在 Icon 面板。
    /// </summary>
    public static class AppIconAssigner
    {
        private const string IconPath = "Assets/UI/ICO.png";

        [MenuItem("SmallGames/设置应用图标")]
        public static void ApplyFromMenu()
        {
            if (Apply())
                EditorUtility.DisplayDialog("应用图标", "已使用 Assets/UI/ICO.png 作为默认图标和 Android 图标。", "确定");
            else
                EditorUtility.DisplayDialog("应用图标", "没有找到 Assets/UI/ICO.png，或无法按 Default 纹理加载。", "确定");
        }

        [InitializeOnLoadMethod]
        private static void AutoApply()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                Apply();
            };
        }

        public static bool Apply()
        {
            var tex = LoadIcon();
            if (tex == null)
                return false;

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex });
            ApplyAndroidKind(AndroidPlatformIconKind.Legacy, tex, 1);
            ApplyAndroidKind(AndroidPlatformIconKind.Round, tex, 1);
            ApplyAndroidKind(AndroidPlatformIconKind.Adaptive, tex, 2);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static Texture2D LoadIcon()
        {
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        private static void ApplyAndroidKind(PlatformIconKind kind, Texture2D tex, int layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons == null || icons.Length == 0)
                return;
            for (var i = 0; i < icons.Length; i++)
            {
                var icon = icons[i];
                if (icon == null)
                    continue;
                var max = icon.maxLayerCount;
                var count = layers;
                if (count > max)
                    count = max;
                if (count < icon.minLayerCount)
                    count = icon.minLayerCount;
                for (var layer = 0; layer < count; layer++)
                    icon.SetTexture(tex, layer);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
    }
}
