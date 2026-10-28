using System;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class ModeRulesSaveData
    {
        public int Flags;
    }

    /// <summary>
    /// 各玩法规则弹窗是否已看过。每种模式只弹一次。
    /// </summary>
    public static class ModeRulesRepository
    {
        private const string PlayerPrefsKey = "SmallGames.ModeRules";

        public static ModeRulesSaveData Current { get; private set; } = new ModeRulesSaveData();

        public static ModeRulesSaveData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var data = JsonUtility.FromJson<ModeRulesSaveData>(SecurePrefs.GetString(PlayerPrefsKey));
                    Current = data ?? new ModeRulesSaveData();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ModeRules] 读取失败，将重新记录: " + e.Message);
                    Current = new ModeRulesSaveData();
                }
            }
            else
            {
                Current = new ModeRulesSaveData();
            }

            return Current;
        }

        public static bool HasSeen(GameModeType mode)
        {
            Ensure();
            return (Current.Flags & Flag(mode)) != 0;
        }

        public static void MarkSeen(GameModeType mode)
        {
            Ensure();
            var flag = Flag(mode);
            if (flag == 0 || (Current.Flags & flag) != 0)
                return;
            Current.Flags |= flag;
            Save();
        }

        public static void Save()
        {
            if (Current == null)
                Current = new ModeRulesSaveData();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        private static int Flag(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return 1 << 0;
                case GameModeType.Sheep: return 1 << 1;
                case GameModeType.Match3: return 1 << 2;
                default: return 0;
            }
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }
    }
}
