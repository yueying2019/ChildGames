using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 对对消 / 叠叠消 / 三消的无尽关：选关多一格，同一局清完升波。
    /// </summary>
    public static class EndlessCatalog
    {
        public const int LevelId = 1000;
        public const int UnlockClearedId = 5;
        public const int CoinsPerWave = 5;
        public const int CoinsCap = 100;

        public static float LianStartSeconds => GameBalance.Current.LianStartSeconds;
        public static float LianBonusSeconds => GameBalance.Current.LianBonusSeconds;
        public static float LianMaxSeconds => GameBalance.Current.LianMaxSeconds;
        public static int Match3BonusMoves => GameBalance.Current.Match3EndlessBonusMoves;

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

        public static bool IsIndex(GameModeType mode, int index)
        {
            return Supports(mode) && index == LevelBrowse.Count(mode);
        }

        public static int SelectCount(GameModeType mode)
        {
            var n = LevelBrowse.Count(mode);
            return Supports(mode) ? n + 1 : n;
        }

        public static bool IsUnlocked(GameModeType mode)
        {
            if (!Supports(mode))
                return false;
            var campaign = LevelBrowse.Count(mode);
            if (campaign <= 0)
                return false;
            var need = Mathf.Min(UnlockClearedId, campaign);
            return ProgressRepository.IsCleared(mode, LevelBrowse.LevelId(mode, need - 1));
        }

        public static int CoinsForWaves(int wave)
        {
            return Mathf.Clamp(Mathf.Max(0, wave) * CoinsPerWave, 0, CoinsCap);
        }

        public static string Title(int wave)
        {
            return $"无尽 · 第{Mathf.Max(1, wave)}波";
        }
    }
}
