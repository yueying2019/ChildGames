using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 每日任务的统计信号。玩法侧只管上报「发生了什么、多少」，任务定义决定怎么算进度。
    /// </summary>
    public enum DailyTaskSignal
    {
        None = 0,

        /// <summary>通关一局，amount 传 1。</summary>
        ModeCleared = 1,

        /// <summary>三消：生成一个炸弹 / 直线 / 彩虹特效。</summary>
        Match3Special = 2,

        /// <summary>三消：本局得分。当日累计。</summary>
        Match3Score = 3,

        /// <summary>对对消：完成一次成功消除。</summary>
        LianliankanPair = 4,

        /// <summary>叠叠消：完成一次三同消除。</summary>
        SheepTriple = 5
    }

    public sealed class DailyTaskDef
    {
        public string Key;
        public GameModeType Mode;
        public DailyTaskSignal Signal;
        public int Target;
        public int RewardCoins;
        public string Title;
        public string Desc;
    }

    /// <summary>
    /// 每日任务定义。目前是内置表，后续想改成 ScriptableObject 资产，
    /// 只要把 Builtin 换成从 Addressables 读到的数组、其余接口不变即可。
    /// </summary>
    public static class DailyTaskCatalog
    {
        private static readonly DailyTaskDef[] Builtin =
        {
            // 四种玩法各通关一局
            new DailyTaskDef
            {
                Key = "clear.lianliankan", Mode = GameModeType.Lianliankan, Signal = DailyTaskSignal.ModeCleared,
                Target = 1, RewardCoins = 50, Title = "连通一局", Desc = "完成一次对对消关卡"
            },
            new DailyTaskDef
            {
                Key = "clear.sheep", Mode = GameModeType.Sheep, Signal = DailyTaskSignal.ModeCleared,
                Target = 1, RewardCoins = 50, Title = "清空一局", Desc = "完成一次叠叠消关卡"
            },
            new DailyTaskDef
            {
                Key = "clear.match3", Mode = GameModeType.Match3, Signal = DailyTaskSignal.ModeCleared,
                Target = 1, RewardCoins = 50, Title = "三消通关", Desc = "完成一次三消关卡"
            },
            // 各玩法特色任务
            new DailyTaskDef
            {
                Key = "match3.special5", Mode = GameModeType.Match3, Signal = DailyTaskSignal.Match3Special,
                Target = 5, RewardCoins = 80, Title = "制造 5 个特效", Desc = "生成炸弹或直线特效 5 个"
            },
            new DailyTaskDef
            {
                Key = "match3.score20000", Mode = GameModeType.Match3, Signal = DailyTaskSignal.Match3Score,
                Target = 20000, RewardCoins = 100, Title = "累计 20000 分", Desc = "当日三消累计得分达到 20000"
            },
            new DailyTaskDef
            {
                Key = "lian.pair20", Mode = GameModeType.Lianliankan, Signal = DailyTaskSignal.LianliankanPair,
                Target = 20, RewardCoins = 60, Title = "消除 20 次", Desc = "成功连通 20 对图案"
            },
            new DailyTaskDef
            {
                Key = "sheep.triple20", Mode = GameModeType.Sheep, Signal = DailyTaskSignal.SheepTriple,
                Target = 20, RewardCoins = 60, Title = "消除 20 次", Desc = "完成 20 次叠叠消消除"
            }
        };

        public static IReadOnlyList<DailyTaskDef> All => Builtin;

        public static int Count => Builtin.Length;

        public static DailyTaskDef Find(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            for (var i = 0; i < Builtin.Length; i++)
            {
                if (string.Equals(Builtin[i].Key, key))
                    return Builtin[i];
            }

            return null;
        }

        /// <summary>按玩法筛选任务，供任务面板分组展示。</summary>
        public static List<DailyTaskDef> ForMode(GameModeType mode)
        {
            var list = new List<DailyTaskDef>();
            for (var i = 0; i < Builtin.Length; i++)
            {
                if (Builtin[i].Mode == mode)
                    list.Add(Builtin[i]);
            }

            return list;
        }
    }
}
