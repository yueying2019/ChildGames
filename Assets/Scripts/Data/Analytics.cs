using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class AnalyticsEvent
    {
        public string Name;
        public string Time;
        public string Mode;
        public int LevelId;
        public string Item;
        public int Value;
        public string Extra;
    }

    [Serializable]
    public sealed class AnalyticsCounter
    {
        public string Key;
        public int Count;
    }

    [Serializable]
    public sealed class AnalyticsData
    {
        public int LaunchCount;
        public string FirstLaunch;
        public string LastLaunch;
        public AnalyticsEvent[] Events = Array.Empty<AnalyticsEvent>();
        public AnalyticsCounter[] Counters = Array.Empty<AnalyticsCounter>();
    }

    /// <summary>
    /// 本地埋点。事件与计数写入 persistentDataPath JSON，后续可换成上报。
    /// </summary>
    public static class Analytics
    {
        public const string FileName = "analytics.json";
        private const int MaxEvents = 400;

        public static AnalyticsData Current { get; private set; } = new AnalyticsData();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "SmallGames", FileName);

        public static AnalyticsData Load()
        {
            try
            {
                var path = FilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    Current = JsonUtility.FromJson<AnalyticsData>(json) ?? new AnalyticsData();
                }
                else
                {
                    Current = new AnalyticsData();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] 读取失败，使用空记录: {e.Message}");
                Current = new AnalyticsData();
            }

            if (Current.Events == null)
                Current.Events = Array.Empty<AnalyticsEvent>();
            if (Current.Counters == null)
                Current.Counters = Array.Empty<AnalyticsCounter>();
            return Current;
        }

        public static void Save()
        {
            if (Current == null)
                Current = new AnalyticsData();
            try
            {
                var path = FilePath;
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllText(path, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] 写入失败: {e.Message}");
            }
        }

        public static void AppLaunch()
        {
            Ensure();
            Current.LaunchCount++;
            var now = Now();
            if (string.IsNullOrEmpty(Current.FirstLaunch))
                Current.FirstLaunch = now;
            Current.LastLaunch = now;
            Track("app_launch", extra: Application.version);
            Debug.Log("[Analytics] 本地文件: " + FilePath);
        }

        public static void ModeClick(GameModeType mode)
        {
            Track("mode_click", mode);
        }

        public static void LevelStart(GameModeType mode, int levelId)
        {
            Track("level_start", mode, levelId);
        }

        public static void LevelWin(GameModeType mode, int levelId, int coins, bool firstClear)
        {
            Track("level_win", mode, levelId, value: coins, extra: firstClear ? "first" : "replay");
        }

        public static void LevelFail(GameModeType mode, int levelId, string reason)
        {
            Track("level_fail", mode, levelId, extra: reason ?? string.Empty);
        }

        public static void ItemUse(ShopItemId id)
        {
            Track("item_use", item: id.ToString());
        }

        public static void ItemBuy(ShopItemId id, int price)
        {
            Track("item_buy", item: id.ToString(), value: price);
        }

        public static void ContinueShow(GameModeType mode, int levelId, string reason)
        {
            Track("continue_show", mode, levelId, extra: reason ?? string.Empty);
        }

        public static void ContinuePay(GameModeType mode, int price)
        {
            Track("continue_pay", mode, value: price);
        }

        public static void ContinueAd(GameModeType mode)
        {
            Track("continue_ad", mode);
        }

        public static void ContinueGiveUp(GameModeType mode, int levelId)
        {
            Track("continue_giveup", mode, levelId);
        }

        public static void ShopOpen(GameModeType mode)
        {
            Track("shop_open", mode);
        }

        public static void DailyClaim(int day, string reward)
        {
            Track("daily_claim", value: day, extra: reward ?? string.Empty);
        }

        public static void EnergyBuy(int amount, int price)
        {
            Track("energy_buy", value: amount, extra: price.ToString());
        }

        public static int CountOf(string key)
        {
            Ensure();
            if (Current.Counters == null)
                return 0;
            for (var i = 0; i < Current.Counters.Length; i++)
            {
                if (Current.Counters[i] != null && Current.Counters[i].Key == key)
                    return Current.Counters[i].Count;
            }

            return 0;
        }

        public static void Track(
            string name,
            GameModeType mode = GameModeType.None,
            int levelId = 0,
            string item = "",
            int value = 0,
            string extra = "")
        {
            if (string.IsNullOrEmpty(name))
                return;
            Ensure();
            Bump(name);
            if (mode != GameModeType.None)
                Bump(name + "." + mode);
            if (!string.IsNullOrEmpty(item))
                Bump(name + "." + item);

            var list = new List<AnalyticsEvent>(Current.Events ?? Array.Empty<AnalyticsEvent>())
            {
                new AnalyticsEvent
                {
                    Name = name,
                    Time = Now(),
                    Mode = mode != GameModeType.None ? mode.ToString() : string.Empty,
                    LevelId = levelId,
                    Item = item ?? string.Empty,
                    Value = value,
                    Extra = extra ?? string.Empty
                }
            };

            if (list.Count > MaxEvents)
                list.RemoveRange(0, list.Count - MaxEvents);
            Current.Events = list.ToArray();
            Save();
        }

        private static void Bump(string key)
        {
            var counters = Current.Counters != null
                ? new List<AnalyticsCounter>(Current.Counters)
                : new List<AnalyticsCounter>();
            for (var i = 0; i < counters.Count; i++)
            {
                if (counters[i] == null || counters[i].Key != key)
                    continue;
                counters[i].Count++;
                Current.Counters = counters.ToArray();
                return;
            }

            counters.Add(new AnalyticsCounter { Key = key, Count = 1 });
            Current.Counters = counters.ToArray();
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }

        private static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
