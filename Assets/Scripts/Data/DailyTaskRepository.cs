using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class DailyTaskSaveData
    {
        public string Date = string.Empty;
        public List<string> Keys = new List<string>();
        public List<int> Values = new List<int>();
        public List<string> Claimed = new List<string>();
    }

    /// <summary>
    /// 每日任务进度。按自然日重置，进度与领取状态走加密存档。
    /// </summary>
    public static class DailyTaskRepository
    {
        private const string PlayerPrefsKey = "SmallGames.DailyTasks";
        private const string DateFormat = "yyyyMMdd";

        public static event Action Changed;

        private static DailyTaskSaveData _data = new DailyTaskSaveData();

        private static string Today => DateTime.Now.ToString(DateFormat);

        public static DailyTaskSaveData Load()
        {
            _data = Read();
            if (EnsureToday())
                Save();

            return _data;
        }

        public static int Progress(string key)
        {
            var index = _data.Keys.IndexOf(key);
            return index >= 0 && index < _data.Values.Count ? _data.Values[index] : 0;
        }

        public static int Target(string key)
        {
            var def = DailyTaskCatalog.Find(key);
            return def != null ? def.Target : 0;
        }

        public static bool IsComplete(string key)
        {
            var def = DailyTaskCatalog.Find(key);
            return def != null && Progress(key) >= def.Target;
        }

        public static bool IsClaimed(string key)
        {
            return _data.Claimed.Contains(key);
        }

        public static bool CanClaim(string key)
        {
            return IsComplete(key) && !IsClaimed(key);
        }

        /// <summary>领取奖励：发金币并标记，重复领取返回 false。</summary>
        public static bool Claim(string key)
        {
            if (!CanClaim(key))
                return false;

            var def = DailyTaskCatalog.Find(key);
            _data.Claimed.Add(key);
            Save();

            if (def != null && def.RewardCoins > 0)
                CoinWallet.AddCoins(def.RewardCoins);

            Changed?.Invoke();
            return true;
        }

        /// <summary>上报一次信号：所有匹配「玩法 + 信号」的任务进度一起累加（封顶到 Target）。</summary>
        public static void Add(GameModeType mode, DailyTaskSignal signal, int amount)
        {
            if (signal == DailyTaskSignal.None || amount <= 0)
                return;

            EnsureToday();

            var defs = DailyTaskCatalog.All;
            var dirty = false;
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Mode != mode || def.Signal != signal)
                    continue;

                var index = _data.Keys.IndexOf(def.Key);
                if (index < 0)
                {
                    _data.Keys.Add(def.Key);
                    _data.Values.Add(0);
                    index = _data.Keys.Count - 1;
                }

                _data.Values[index] = Math.Min(_data.Values[index] + amount, def.Target);
                dirty = true;
            }

            if (!dirty)
                return;

            Save();
            Changed?.Invoke();
        }

        /// <summary>是否有已完成但还没领的任务，供主菜单红点用。</summary>
        public static bool HasClaimable()
        {
            var defs = DailyTaskCatalog.All;
            for (var i = 0; i < defs.Count; i++)
            {
                if (CanClaim(defs[i].Key))
                    return true;
            }

            return false;
        }

        /// <summary>当日已完成的任务数。</summary>
        public static int CompletedCount()
        {
            var defs = DailyTaskCatalog.All;
            var n = 0;
            for (var i = 0; i < defs.Count; i++)
            {
                if (IsComplete(defs[i].Key))
                    n++;
            }

            return n;
        }

        /// <summary>开发者窗口「全成就」：当日任务全部做到可领取，不在这里发金币。</summary>
        public static void UnlockAll()
        {
            Load();
            if (_data.Keys == null)
                _data.Keys = new List<string>();
            if (_data.Values == null)
                _data.Values = new List<int>();
            if (_data.Claimed == null)
                _data.Claimed = new List<string>();

            _data.Keys.Clear();
            _data.Values.Clear();
            _data.Claimed.Clear();

            var defs = DailyTaskCatalog.All;
            for (var i = 0; i < defs.Count; i++)
            {
                _data.Keys.Add(defs[i].Key);
                _data.Values.Add(defs[i].Target);
            }

            Save();
            Changed?.Invoke();
        }

        public static void Reset()
        {
            _data = new DailyTaskSaveData { Date = Today };
            Save();
            Changed?.Invoke();
        }

        /// <summary>跨天时清进度；返回是否需要立刻落盘。</summary>
        private static bool EnsureToday()
        {
            var today = Today;
            if (string.Equals(_data.Date, today))
                return false;

            var fresh = _data.Date != today;
            _data = new DailyTaskSaveData { Date = today };
            return fresh;
        }

        private static DailyTaskSaveData Read()
        {
            if (!SecurePrefs.HasKey(PlayerPrefsKey))
                return new DailyTaskSaveData();

            try
            {
                var data = JsonUtility.FromJson<DailyTaskSaveData>(SecurePrefs.GetString(PlayerPrefsKey));
                if (data == null)
                    return new DailyTaskSaveData();
                if (data.Keys == null)
                    data.Keys = new List<string>();
                if (data.Values == null)
                    data.Values = new List<int>();
                if (data.Claimed == null)
                    data.Claimed = new List<string>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DailyTask] 读取失败，重置任务进度: " + e.Message);
                return new DailyTaskSaveData();
            }
        }

        private static void Save()
        {
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(_data));
            SecurePrefs.Save();
        }
    }
}
