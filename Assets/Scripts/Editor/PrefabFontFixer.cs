using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames.Editor
{
    public static class PrefabFontFixer
    {
        [MenuItem("SmallGames/修复预制体字体")]
        public static void FixFromMenu()
        {
            var count = FixAll();
            AssetDatabase.SaveAssets();
            Debug.Log($"[UI] 已把 {count} 个 Text 改为 {UIStyle.PrefabFontName} 粗体，行距 {UIStyle.TextLineSpacing}。");
        }

        [InitializeOnLoadMethod]
        private static void AutoFixIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (!NeedsFix())
                    return;
                FixAll();
                AssetDatabase.SaveAssets();
            };
        }

        public static int FixAll()
        {
            var font = UIStyle.PrefabFont;
            if (font == null)
            {
                Debug.LogError("[UI] 找不到 Assets/Font/NotoSansSC-UI.otf。");
                return 0;
            }

            var count = 0;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Content" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                var texts = root.GetComponentsInChildren<Text>(true);
                var dirty = false;
                for (var t = 0; t < texts.Length; t++)
                {
                    if (!NeedsFix(texts[t]))
                        continue;
                    UIStyle.ApplyTextStyle(texts[t]);
                    dirty = true;
                    count++;
                }

                if (dirty)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            return count;
        }

        private static bool NeedsFix()
        {
            var font = UIStyle.PrefabFont;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Content" });
            for (var i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null)
                    continue;
                var texts = prefab.GetComponentsInChildren<Text>(true);
                for (var t = 0; t < texts.Length; t++)
                {
                    if (NeedsFix(texts[t], font))
                        return true;
                }
            }

            return false;
        }

        private static bool NeedsFix(Text text, Font expected = null)
        {   
            if (text == null)
                return false;
            if (expected == null)
                expected = UIStyle.PrefabFont;
            return text.font != expected
                || text.fontStyle != UIStyle.TextFontStyle
                || !Mathf.Approximately(text.lineSpacing, UIStyle.TextLineSpacing);
        }
    }
}
