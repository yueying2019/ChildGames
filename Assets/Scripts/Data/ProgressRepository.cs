using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class ProgressData
    {
        public int LianUnlocked = 1;
        public int SheepUnlocked = 1;
        public int Match3Unlocked = 1;
        public int LianLast = 1;
        public int SheepLast = 1;
        public int Match3Last = 1;
        public int LianEndlessBest;
        public int SheepEndlessBest;
        public int Match3EndlessBest;

        /// <summary>按关卡序号存星级，缺位即 0 星。JsonUtility 支持 List 字段。</summary>
        public List<int> LianStars = new List<int>();
        public List<int> SheepStars = new List<int>();
        public List<int> Match3Stars = new List<int>();

        /// <summary>按关卡序号存「满星奖励是否已领」，1 = 已领。与 Stars 一一对应。</summary>
        public List<int> LianPerfect = new List<int>();
        public List<int> SheepPerfect = new List<int>();
        public List<int> Match3Perfect = new List<int>();

        /// <summary>教学关通关位掩码，见 ProgressRepository.TutorialFlag。</summary>
        public int TutorialFlags;

        /// <summary>每日挑战上次领奖的日期，yyyyMMdd。0 表示还没领过。</summary>
        public int LianDailyRewardDay;
        public int SheepDailyRewardDay;
        public int Match3DailyRewardDay;

        /// <summary>星星兑换已经花掉的星。关卡纪录上的星不减，可兑数量是总星减去这里。</summary>
        public int LianStarsSpent;
        public int SheepStarsSpent;
        public int Match3StarsSpent;

        /// <summary>该关通关金币是否已发过。每日挑战提前打到的关靠它避免再发一次首通金币。</summary>
        public List<int> LianClearPaid = new List<int>();
        public List<int> SheepClearPaid = new List<int>();
        public List<int> Match3ClearPaid = new List<int>();

        /// <summary>揭棋残局已过关的位。第 n 位为 1 表示第 n 关已是三星。</summary>
        public int JieqiEndgameStars;
    }

    /// <summary>
    /// 各模式解锁进度。解锁到第 N 关表示 1..N 可打，1..N-1 已通关。
    /// </summary>
    public static class ProgressRepository
    {
        private const string PlayerPrefsKey = "SmallGames.Progress";

        public static ProgressData Current { get; private set; } = new ProgressData();

        public static ProgressData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var data = JsonUtility.FromJson<ProgressData>(SecurePrefs.GetString(PlayerPrefsKey));
                    Current = data ?? new ProgressData();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Progress] 读取失败，使用默认进度: {e.Message}");
                    Current = new ProgressData();
                }
            }
            else
            {
                Current = new ProgressData();
            }

            Clamp();
            return Current;
        }

        /// <summary>开发者窗口：关卡进度、星级、教学关标记恢复成新档。</summary>
        public static void ResetProgress()
        {
            Current = new ProgressData();
            Save();
        }

        /// <summary>
        /// 开发者窗口：正式关全部记为三星且可进入。
        /// 关卡资源还没加载时按 64 关预写，避免稍后选关读到空星级。
        /// </summary>
        public static void UnlockAllThreeStars()
        {
            Ensure();
            FillUnlocked(GameModeType.Lianliankan);
            FillUnlocked(GameModeType.Sheep);
            FillUnlocked(GameModeType.Match3);
            Current.TutorialFlags |= (1 << 0) | (1 << 1) | (1 << 2);
            Save();
        }

        public static void Save()
        {
            if (Current == null)
                Current = new ProgressData();
            Clamp();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        public static int UnlockedId(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepUnlocked;
                case GameModeType.Match3: return Current.Match3Unlocked;
                default: return Current.LianUnlocked;
            }
        }

        public static int LastPlayedId(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepLast;
                case GameModeType.Match3: return Current.Match3Last;
                default: return Current.LianLast;
            }
        }

        public static bool IsUnlocked(GameModeType mode, int levelId)
        {
            var index = LevelBrowse.IndexOf(mode, levelId);
            return index >= 0 && index < UnlockedId(mode);
        }

        public static bool IsCleared(GameModeType mode, int levelId)
        {
            var index = LevelBrowse.IndexOf(mode, levelId);
            return index >= 0 && index + 1 < UnlockedId(mode);
        }

        public static void SetLastPlayed(GameModeType mode, int levelId)
        {
            Ensure();
            if (EndlessCatalog.IsLevelId(levelId) || DailyCatalog.IsLevelId(levelId))
            {
                SetLastField(mode, levelId);
                Save();
                return;
            }

            var count = LevelBrowse.Count(mode);
            if (count <= 0)
                return;
            SetLastField(mode, Mathf.Clamp(levelId, 1, count));
            Save();
        }

        public static int EndlessBest(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepEndlessBest;
                case GameModeType.Match3: return Current.Match3EndlessBest;
                default: return Current.LianEndlessBest;
            }
        }

        /// <returns>是否刷新了最高波。</returns>
        public static bool RecordEndlessBest(GameModeType mode, int wave)
        {
            Ensure();
            wave = Mathf.Max(0, wave);
            var prev = EndlessBest(mode);
            if (wave <= prev)
                return false;

            switch (mode)
            {
                case GameModeType.Sheep: Current.SheepEndlessBest = wave; break;
                case GameModeType.Match3: Current.Match3EndlessBest = wave; break;
                default: Current.LianEndlessBest = wave; break;
            }

            Save();
            return true;
        }

        /// <summary>揭棋残局过关记为三星。人机和双人不记在这里。</summary>
        public static void RecordJieqiEndgame(int index)
        {
            if (index < 0 || index >= JieqiEndgameCatalog.Count)
                return;
            Ensure();
            var bit = 1 << index;
            if ((Current.JieqiEndgameStars & bit) != 0)
                return;
            Current.JieqiEndgameStars |= bit;
            Save();
        }

        /// <summary>残局选关上的星数。过关是 3，没过是 0。</summary>
        public static int JieqiEndgameStars(int index)
        {
            if (index < 0 || index >= JieqiEndgameCatalog.Count)
                return 0;
            Ensure();
            var bit = 1 << index;
            return (Current.JieqiEndgameStars & bit) != 0 ? StarCatalog.MaxStars : 0;
        }

        /// <summary>某关历史最好星级，未通关卡为 0。</summary>
        public static int Stars(GameModeType mode, int levelId)
        {
            Ensure();
            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return 0;

            var list = StarList(mode);
            if (list == null || index >= list.Count)
                return 0;
            return Mathf.Clamp(list[index], 0, StarCatalog.MaxStars);
        }

        /// <returns>是否刷新了该关的星级纪录。星级只升不降。</returns>
        public static bool RecordStars(GameModeType mode, int levelId, int stars)
        {
            Ensure();
            if (EndlessCatalog.IsLevelId(levelId))
                return false;

            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return false;

            stars = Mathf.Clamp(stars, 0, StarCatalog.MaxStars);
            if (stars <= Stars(mode, levelId))
                return false;

            var list = StarList(mode);
            if (list == null)
                return false;
            while (list.Count <= index)
                list.Add(0);
            list[index] = stars;
            Save();
            return true;
        }

        public static int TotalStars(GameModeType mode)
        {
            var list = StarList(mode);
            if (list == null)
                return 0;

            var sum = 0;
            for (var i = 0; i < list.Count; i++)
                sum += Mathf.Clamp(list[i], 0, StarCatalog.MaxStars);
            return sum;
        }

        /// <summary>这个模式还能拿来兑换的星星。关卡收集星不会因为兑换变少。</summary>
        public static int AvailableStars(GameModeType mode)
        {
            var left = TotalStars(mode) - SpentStars(mode);
            return left > 0 ? left : 0;
        }

        /// <returns>星星够不够。够就会记一笔花费。</returns>
        public static bool TrySpendStars(GameModeType mode, int cost)
        {
            Ensure();
            if (cost <= 0 || AvailableStars(mode) < cost)
                return false;
            switch (mode)
            {
                case GameModeType.Sheep: Current.SheepStarsSpent += cost; break;
                case GameModeType.Match3: Current.Match3StarsSpent += cost; break;
                default: Current.LianStarsSpent += cost; break;
            }

            Save();
            return true;
        }

        public static int TotalStars()
        {
            return TotalStars(GameModeType.Lianliankan)
                   + TotalStars(GameModeType.Sheep)
                   + TotalStars(GameModeType.Match3);
        }

        private static List<int> StarList(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepStars;
                case GameModeType.Match3: return Current.Match3Stars;
                default: return Current.LianStars;
            }
        }

        private static List<int> PerfectList(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepPerfect;
                case GameModeType.Match3: return Current.Match3Perfect;
                default: return Current.LianPerfect;
            }
        }

        private static int TutorialFlag(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return 1 << 1;
                case GameModeType.Match3: return 1 << 2;
                case GameModeType.Lianliankan: return 1 << 0;
                default: return 0;
            }
        }

        /// <summary>教学关是否已通过（跳过也算通过）。</summary>
        public static bool IsTutorialCleared(GameModeType mode)
        {
            Ensure();
            var flag = TutorialFlag(mode);
            return flag != 0 && (Current.TutorialFlags & flag) != 0;
        }

        /// <summary>今天这个模式的每日挑战奖励是否已经领过。</summary>
        public static bool HasDailyReward(GameModeType mode)
        {
            Ensure();
            return DailyRewardDay(mode) == DailyCatalog.TodayStamp();
        }

        /// <summary>记下今天的每日奖励已领。同一天再调不会改存档。</summary>
        public static void MarkDailyReward(GameModeType mode)
        {
            Ensure();
            var today = DailyCatalog.TodayStamp();
            if (DailyRewardDay(mode) == today)
                return;
            switch (mode)
            {
                case GameModeType.Sheep: Current.SheepDailyRewardDay = today; break;
                case GameModeType.Match3: Current.Match3DailyRewardDay = today; break;
                default: Current.LianDailyRewardDay = today; break;
            }

            Save();
        }

        /// <summary>标记教学关已通过。重复调用无副作用。</summary>
        public static void MarkTutorialCleared(GameModeType mode)
        {
            Ensure();
            var flag = TutorialFlag(mode);
            if (flag == 0 || (Current.TutorialFlags & flag) != 0)
                return;
            Current.TutorialFlags |= flag;
            Save();
        }

        /// <summary>该关的满星（三星）奖励是否已领过。</summary>
        public static bool HasPerfectReward(GameModeType mode, int levelId)
        {
            Ensure();
            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return false;

            var list = PerfectList(mode);
            if (list == null || index >= list.Count)
                return false;
            return list[index] != 0;
        }

        /// <summary>标记该关满星奖励已领。</summary>
        public static void MarkPerfectReward(GameModeType mode, int levelId)
        {
            Ensure();
            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return;

            var list = PerfectList(mode);
            if (list == null)
                return;
            while (list.Count <= index)
                list.Add(0);
            list[index] = 1;
            Save();
        }

        /// <summary>该关的首通金币是否已经发过（顺序通关，或每日挑战提前打过）。</summary>
        public static bool HasClearReward(GameModeType mode, int levelId)
        {
            Ensure();
            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return false;
            if (index + 1 < UnlockedId(mode))
                return true;
            return IsClearPaid(mode, index);
        }

        /// <returns>是否首次拿到该关通关金币。</returns>
        /// <param name="keepLastPlayed">每日挑战为 true：不把「上次」记到被抽中的那一关上。</param>
        public static bool MarkCleared(GameModeType mode, int levelId, bool keepLastPlayed = false)
        {
            Ensure();
            var count = LevelBrowse.Count(mode);
            if (levelId < 1 || count <= 0 || EndlessCatalog.IsLevelId(levelId) || DailyCatalog.IsLevelId(levelId))
                return false;

            if (!keepLastPlayed)
                SetLastPlayed(mode, levelId);
            var index = LevelBrowse.IndexOf(mode, levelId);
            if (index < 0)
                return false;
            // 顺序上已经通关过，金币按复刷。
            if (index + 1 < UnlockedId(mode))
                return false;

            var paid = IsClearPaid(mode, index);
            // 只有当前已经能打的关才推进解锁。更后面的关来自每日挑战，不能把中间关一并放开。
            if (index < UnlockedId(mode))
            {
                var next = Mathf.Min(index + 2, count + 1);
                switch (mode)
                {
                    case GameModeType.Sheep: Current.SheepUnlocked = Mathf.Max(Current.SheepUnlocked, next); break;
                    case GameModeType.Match3: Current.Match3Unlocked = Mathf.Max(Current.Match3Unlocked, next); break;
                    default: Current.LianUnlocked = Mathf.Max(Current.LianUnlocked, next); break;
                }
            }

            if (!paid)
                SetClearPaid(mode, index);
            Save();
            return !paid;
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }

        private static void Clamp()
        {
            if (Current == null)
                Current = new ProgressData();
            Current.LianUnlocked = Mathf.Max(1, Current.LianUnlocked);
            Current.SheepUnlocked = Mathf.Max(1, Current.SheepUnlocked);
            Current.Match3Unlocked = Mathf.Max(1, Current.Match3Unlocked);
            Current.LianLast = Mathf.Max(1, Current.LianLast);
            Current.SheepLast = Mathf.Max(1, Current.SheepLast);
            Current.Match3Last = Mathf.Max(1, Current.Match3Last);
            Current.LianEndlessBest = Mathf.Max(0, Current.LianEndlessBest);
            Current.SheepEndlessBest = Mathf.Max(0, Current.SheepEndlessBest);
            Current.Match3EndlessBest = Mathf.Max(0, Current.Match3EndlessBest);

            // 老存档反序列化后星级列表可能为 null，这里补齐，读星时不至于判空失败。
            if (Current.LianStars == null)
                Current.LianStars = new List<int>();
            if (Current.SheepStars == null)
                Current.SheepStars = new List<int>();
            if (Current.Match3Stars == null)
                Current.Match3Stars = new List<int>();
            if (Current.LianPerfect == null)
                Current.LianPerfect = new List<int>();
            if (Current.SheepPerfect == null)
                Current.SheepPerfect = new List<int>();
            if (Current.Match3Perfect == null)
                Current.Match3Perfect = new List<int>();
            if (Current.TutorialFlags < 0)
                Current.TutorialFlags = 0;
            Current.LianStarsSpent = Mathf.Max(0, Current.LianStarsSpent);
            Current.SheepStarsSpent = Mathf.Max(0, Current.SheepStarsSpent);
            Current.Match3StarsSpent = Mathf.Max(0, Current.Match3StarsSpent);
            Current.LianDailyRewardDay = Mathf.Max(0, Current.LianDailyRewardDay);
            Current.SheepDailyRewardDay = Mathf.Max(0, Current.SheepDailyRewardDay);
            Current.Match3DailyRewardDay = Mathf.Max(0, Current.Match3DailyRewardDay);
            if (Current.LianClearPaid == null)
                Current.LianClearPaid = new List<int>();
            if (Current.SheepClearPaid == null)
                Current.SheepClearPaid = new List<int>();
            if (Current.Match3ClearPaid == null)
                Current.Match3ClearPaid = new List<int>();
        }

        private static int SpentStars(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepStarsSpent;
                case GameModeType.Match3: return Current.Match3StarsSpent;
                default: return Current.LianStarsSpent;
            }
        }

        private static int DailyRewardDay(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepDailyRewardDay;
                case GameModeType.Match3: return Current.Match3DailyRewardDay;
                default: return Current.LianDailyRewardDay;
            }
        }

        private static List<int> ClearPaidList(GameModeType mode)
        {
            Ensure();
            switch (mode)
            {
                case GameModeType.Sheep: return Current.SheepClearPaid;
                case GameModeType.Match3: return Current.Match3ClearPaid;
                default: return Current.LianClearPaid;
            }
        }

        private static bool IsClearPaid(GameModeType mode, int index)
        {
            var list = ClearPaidList(mode);
            return list != null && index >= 0 && index < list.Count && list[index] != 0;
        }

        private static void SetClearPaid(GameModeType mode, int index)
        {
            var list = ClearPaidList(mode);
            if (list == null || index < 0)
                return;
            while (list.Count <= index)
                list.Add(0);
            list[index] = 1;
        }

        private static void FillUnlocked(GameModeType mode)
        {
            var count = LevelBrowse.Count(mode);
            if (count <= 0)
                count = 64;

            var unlocked = count + 1;
            switch (mode)
            {
                case GameModeType.Sheep: Current.SheepUnlocked = unlocked; break;
                case GameModeType.Match3: Current.Match3Unlocked = unlocked; break;
                default: Current.LianUnlocked = unlocked; break;
            }

            var stars = StarList(mode);
            stars.Clear();
            for (var i = 0; i < count; i++)
                stars.Add(StarCatalog.MaxStars);
        }

        private static void SetLastField(GameModeType mode, int levelId)
        {
            switch (mode)
            {
                case GameModeType.Sheep: Current.SheepLast = levelId; break;
                case GameModeType.Match3: Current.Match3Last = levelId; break;
                default: Current.LianLast = levelId; break;
            }
        }
    }
}
