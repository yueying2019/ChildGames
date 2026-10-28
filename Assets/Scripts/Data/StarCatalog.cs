using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 关卡星级：统一 3 星制，按「剩余资源比例」评定，给玩家补星的重玩动机。
    /// 三个玩法的资源口径不同，但都归约成 0~1 的剩余比例后共用同一套阈值：
    /// 三消看剩余步数、对对消看剩余时间、叠叠消限时关也看剩余时间。
    /// 叠叠消的常规则外：通关时槽位必然空着，用「槽位空余」没有区分度，
    /// 改看本局连击峰值（FromCombo）。
    /// </summary>
    public static class StarCatalog
    {
        public const int MaxStars = 3;

        /// <summary>剩余比例达到这个值给 3 星。</summary>
        public const float ThreeStarRemain = 0.35f;

        /// <summary>剩余比例达到这个值给 2 星，否则 1 星。</summary>
        public const float TwoStarRemain = 0.12f;

        /// <summary>叠叠消口径：连击峰值达到这个值给 3 星。</summary>
        public const int ThreeStarCombo = 5;

        /// <summary>叠叠消口径：连击峰值达到这个值给 2 星。</summary>
        public const int TwoStarCombo = 3;

        /// <summary>
        /// 把「剩余 / 总量」折算成 0~1 的比例。总量为 0（异常关卡）时按 0 处理，避免除零。
        /// </summary>
        public static float Remaining(float left, float total)
        {
            if (total <= 0f)
                return 0f;
            var ratio = left / total;
            return ratio < 0f ? 0f : ratio > 1f ? 1f : ratio;
        }

        /// <summary>剩余比例 → 星级。通关至少 1 星。</summary>
        public static int FromRemaining(float remain)
        {
            if (remain < 0f)
                remain = 0f;
            if (remain >= ThreeStarRemain)
                return 3;
            if (remain >= TwoStarRemain)
                return 2;
            return 1;
        }

        /// <summary>连击峰值 → 星级。叠叠消通关时槽位必然清空，改以连续消除的能力评级。</summary>
        public static int FromCombo(int combo)
        {
            if (combo >= ThreeStarCombo)
                return 3;
            if (combo >= TwoStarCombo)
                return 2;
            return 1;
        }

        /// <summary>拿三星至少要剩多少步（步数口径）。关卡总步数为 0 时给 0。</summary>
        public static int ThreeStarMoves(int moveLimit)
        {
            return moveLimit <= 0 ? 0 : Mathf.CeilToInt(moveLimit * ThreeStarRemain);
        }

        public static int TwoStarMoves(int moveLimit)
        {
            return moveLimit <= 0 ? 0 : Mathf.CeilToInt(moveLimit * TwoStarRemain);
        }

        /// <summary>拿三星至少要剩多少秒（时间口径）。</summary>
        public static int ThreeStarSeconds(float timeLimit)
        {
            return timeLimit <= 0f ? 0 : Mathf.CeilToInt(timeLimit * ThreeStarRemain);
        }

        public static int TwoStarSeconds(float timeLimit)
        {
            return timeLimit <= 0f ? 0 : Mathf.CeilToInt(timeLimit * TwoStarRemain);
        }

        /// <summary>结算面板上的评级文案。</summary>
        public static string Label(int stars)
        {
            switch (stars)
            {
                case 3: return "完美";
                case 2: return "优秀";
                case 1: return "过关";
                default: return string.Empty;
            }
        }

        /// <summary>某个玩法理论上的满星数，用于「X / Y 星」的进度展示。</summary>
        public static int Capacity(GameModeType mode)
        {
            return LevelBrowse.Count(mode) * MaxStars;
        }

        /// <summary>全玩法理论满星数。</summary>
        public static int Capacity()
        {
            return Capacity(GameModeType.Lianliankan)
                   + Capacity(GameModeType.Sheep)
                   + Capacity(GameModeType.Match3);
        }
    }
}
