using System;

namespace SmallGames
{
    /// <summary>
    /// 对对消 / 叠叠消 / 三消的每日挑战：选关多一格，从全部正式关里抽一关来打。
    /// 从选关进去时，同一天抽到的关不变。结算按钮是「换一关」。金币和道具一天只领一次。
    /// </summary>
    public static class DailyCatalog
    {
        /// <summary>选关「上次」高亮用的哨兵，不和正式关、无尽关（1000）冲突。</summary>
        public const int LevelId = 2000;

        /// <summary>StartGame 里表示「每日挑战」的 index 哨兵。</summary>
        public const int Index = -2;

        public static bool Supports(GameModeType mode)
        {
            return mode == GameModeType.Lianliankan
                   || mode == GameModeType.Sheep
                   || mode == GameModeType.Match3;
        }

        public static bool IsLevelId(int levelId)
        {
            return levelId == LevelId;
        }

        public static bool IsIndex(int index)
        {
            return index == Index;
        }

        /// <summary>教学关通过（含跳过）之后才能打。</summary>
        public static bool IsUnlocked(GameModeType mode)
        {
            if (!Supports(mode))
                return false;
            if (LevelBrowse.Count(mode) <= 0)
                return false;
            return TutorialCatalog.IsCleared(mode);
        }

        public static int TodayIndex(GameModeType mode)
        {
            return PickIndex(TodayStamp(), mode, LevelBrowse.Count(mode));
        }

        /// <summary>按日期和模式取下标。count 小于等于 0 时返回 0。</summary>
        public static int PickIndex(int stamp, GameModeType mode, int count)
        {
            if (count <= 0)
                return 0;

            var mixed = stamp * 397L + (long)mode * 131L;
            if (mixed < 0)
                mixed = -mixed;
            return (int)(mixed % count);
        }

        /// <summary>再来一局时换一关。只有一关时只能留在原下标。</summary>
        public static int NextIndex(GameModeType mode, int excludeIndex)
        {
            var count = LevelBrowse.Count(mode);
            if (count <= 1)
                return 0;
            return OtherIndex(excludeIndex, count, UnityEngine.Random.Range(0, count - 1));
        }

        /// <summary>用 roll（0 到 count-2）映射到除 exclude 以外的下标。</summary>
        public static int OtherIndex(int exclude, int count, int roll)
        {
            if (count <= 1)
                return 0;
            var span = count - 1;
            var pick = roll % span;
            if (pick < 0)
                pick += span;
            if (exclude < 0 || exclude >= count)
                return pick;
            if (pick >= exclude)
                pick++;
            return pick;
        }

        public static int TodayStamp()
        {
            var now = DateTime.Now;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }

        public static string Title(string levelTitle)
        {
            if (string.IsNullOrEmpty(levelTitle))
                return "每日挑战";
            return "每日 · " + levelTitle;
        }
    }
}
