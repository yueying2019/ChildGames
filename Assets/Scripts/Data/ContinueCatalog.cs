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
                case GameModeType.Sheep: return "移出槽内最多 " + UIStyle.Key(SheepClearSlots + " 张") + "牌，并增加 " + UIStyle.Key(SheepBonusSeconds + " 秒");
                case GameModeType.Match3: return "增加 " + UIStyle.Key(ExtraMoves + " 步");
                default: return "增加 " + UIStyle.Key(ExtraSeconds + " 秒");
            }
        }
    }
}
