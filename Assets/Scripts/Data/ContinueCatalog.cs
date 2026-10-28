using UnityEngine;

namespace SmallGames
{
    public static class ContinueCatalog
    {
        public const int MaxPerAttempt = 2;
        public const int SheepClearSlots = 3;

        /// <summary>
        /// 叠叠消续关时补的秒数。限时关必须补，
        /// 否则 TickTimer 会在复活的下一帧再次判定超时。
        /// </summary>
        public const int SheepBonusSeconds = 20;

        /// <summary>三消限时关续关时补的秒数，理由同 SheepBonusSeconds：不补会在复活帧立刻再判负。</summary>
        public const int Match3BonusSeconds = 20;

        public static int ExtraSeconds => Mathf.RoundToInt(GameBalance.Current.LianContinueSeconds);
        public static int ExtraMoves => GameBalance.Current.Match3ContinueMoves;

        public static int Price(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return 50;
                case GameModeType.Match3: return 50;
                default: return 40;
            }
        }

        public static string Benefit(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return $"移出槽里最多 {SheepClearSlots} 张牌，补 {SheepBonusSeconds} 秒";
                case GameModeType.Match3: return $"增加 {ExtraMoves} 步";
                default: return $"增加 {ExtraSeconds} 秒";
            }
        }
    }
}
