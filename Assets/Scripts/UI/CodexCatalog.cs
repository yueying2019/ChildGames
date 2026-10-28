using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 图鉴表。页签和条目都来自 UI/Codex，增删玩法或分类只改 json。
    /// </summary>
    public static class CodexCatalog
    {
        public const string Address = "UI/Codex";

        public static FileData Load()
        {
            var text = AssetHub.Load<TextAsset>(Address);
            if (text == null || string.IsNullOrEmpty(text.text))
                return null;
            try
            {
                var raw = text.text;
            if (raw.Length > 0 && raw[0] == '\uFEFF')
                raw = raw.Substring(1);
            return JsonUtility.FromJson<FileData>(raw);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[图鉴] json 解析失败: " + e.Message);
                return null;
            }
        }

        [Serializable]
        public sealed class FileData
        {
            public Mode[] modes;
        }

        [Serializable]
        public sealed class Mode
        {
            public string id;
            public string name;
            public Tab[] tabs;
        }

        [Serializable]
        public sealed class Tab
        {
            public string id;
            public string name;
            public Entry[] entries;
        }

        [Serializable]
        public sealed class Entry
        {
            public string name;
            public string icon;
            public string desc;
            public string obtain;
        }
    }
}
