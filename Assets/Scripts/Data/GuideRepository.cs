using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class GuideSaveData
    {
        /// <summary>旧版三消位掩码存档，仅用于一次性迁移，之后不再写入。</summary>
        public int Flags;

        public string[] SeenKeys = Array.Empty<string>();
    }

    /// <summary>
    /// 引导"看过"记录。按玩法分 key 存 PlayerPrefs，每条引导用字符串 Key 去重。
    /// 老存档（SmallGames.Match3Guide 的位掩码）首次读取时自动迁移到新结构，
    /// 老玩家的已看状态不会丢。
    /// </summary>
    public static class GuideRepository
    {
        private const string LegacyMatch3Key = "SmallGames.Match3Guide";
        private const string KeyPrefix = "SmallGames.Guide.";

        private static readonly Dictionary<GameModeType, HashSet<string>> Seen =
            new Dictionary<GameModeType, HashSet<string>>();

        public static void Load()
        {
            var modes = (GameModeType[])Enum.GetValues(typeof(GameModeType));
            for (var i = 0; i < modes.Length; i++)
                LoadMode(modes[i]);
        }

        public static bool HasSeen(GameModeType mode, string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return Set(mode).Contains(key);
        }

        public static bool HasSeen(Match3GuideId id)
        {
            return HasSeen(GameModeType.Match3, GuideCatalog.KeyFor(id));
        }

        public static void MarkSeen(GameModeType mode, string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            var set = Set(mode);
            if (!set.Add(key))
                return;
            SaveMode(mode, set);
        }

        public static void MarkSeen(Match3GuideId id)
        {
            MarkSeen(GameModeType.Match3, GuideCatalog.KeyFor(id));
        }

        /// <summary>忘掉某个玩法的引导记录，方便 QA 复现第一次进入的表现。</summary>
        public static void Reset(GameModeType mode)
        {
            Set(mode).Clear();
            SaveMode(mode, Seen[mode]);
        }

        public static void ResetAll()
        {
            var modes = (GameModeType[])Enum.GetValues(typeof(GameModeType));
            for (var i = 0; i < modes.Length; i++)
                Reset(modes[i]);
        }

        public static string PlayerPrefsKey(GameModeType mode)
        {
            return KeyPrefix + mode;
        }

        private static HashSet<string> Set(GameModeType mode)
        {
            if (Seen.TryGetValue(mode, out var set) && set != null)
                return set;

            LoadMode(mode);
            return Seen[mode];
        }

        private static void LoadMode(GameModeType mode)
        {
            var key = PlayerPrefsKey(mode);
            var migrated = false;
            var set = new HashSet<string>(StringComparer.Ordinal);
            Seen[mode] = set;

            if (SecurePrefs.HasKey(key))
            {
                try
                {
                    var data = JsonUtility.FromJson<GuideSaveData>(SecurePrefs.GetString(key));
                    var keys = data != null ? data.SeenKeys : null;
                    if (keys != null)
                    {
                        for (var i = 0; i < keys.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(keys[i]))
                                set.Add(keys[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Guide] 读取 {key} 失败，将重新记录: {e.Message}");
                    set.Clear();
                }
            }
            else if (mode == GameModeType.Match3)
            {
                migrated = MigrateLegacy(set);
            }

            if (migrated)
                SaveMode(mode, set);
        }

        private static bool MigrateLegacy(HashSet<string> set)
        {
            if (!SecurePrefs.HasKey(LegacyMatch3Key))
                return false;

            var flags = 0;
            try
            {
                var data = JsonUtility.FromJson<GuideSaveData>(SecurePrefs.GetString(LegacyMatch3Key));
                if (data != null)
                    flags = data.Flags;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Guide] 旧存档迁移失败: {e.Message}");
                return false;
            }

            var ids = (Match3GuideId[])Enum.GetValues(typeof(Match3GuideId));
            for (var i = 0; i < ids.Length; i++)
            {
                if ((flags & (1 << (int)ids[i])) != 0)
                    set.Add(GuideCatalog.KeyFor(ids[i]));
            }

            return true;
        }

        private static void SaveMode(GameModeType mode, HashSet<string> set)
        {
            var keys = new string[set.Count];
            set.CopyTo(keys);
            SecurePrefs.SetString(PlayerPrefsKey(mode), JsonUtility.ToJson(new GuideSaveData { SeenKeys = keys }));
            SecurePrefs.Save();
        }
    }
}
