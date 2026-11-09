using System;

namespace SmallGames
{
    /// <summary>
    /// 三个玩法的引导脚本（教学关用）。
    ///
    /// 刻意写成代码而不是 ScriptableObject：教学关的棋盘本身就是代码内置的固定排布，
    /// 步骤里的坐标和棋盘强绑定，拆成两份反而容易对不上。改引导 = 改这里，一起提审。
    ///
    /// 每份脚本只锁前一步（强制指定操作），第二步放开只做提示：
    /// 棋盘一旦掉落新块就不可能再按脚本精确演，锁太死会变成"点不动"的挫败感。
    /// </summary>
    public static class TutorialScript
    {
        /// <summary>三消：交换出第一个三连，再自由消一次。</summary>
        public const string Match3SwapDone = "match3.swap";
        public const string Match3AnyMatch = "match3.match";

        /// <summary>对对消：连掉第一对，再自由连一对。</summary>
        public const string LianFirstPair = "lian.pair";
        public const string LianAnyPair = "lian.match";

        /// <summary>叠叠消：凑齐第一组三张，再自由消一组。</summary>
        public const string SheepFirstGroup = "sheep.group";
        public const string SheepAnyGroup = "sheep.match";

        public static TutorialStep[] Build(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Match3: return Match3();
                case GameModeType.Sheep: return Sheep();
                default: return Lianliankan();
            }
        }

        private static TutorialStep[] Match3()
        {
            TutorialCatalog.Match3FirstSwap(out var x0, out var y0, out var x1, out var y1);
            return new[]
            {
                new TutorialStep
                {
                    Key = "m3.step1",
                    Desc = "交换这两格，使" + UIStyle.Key("三个相同图案") + "连成" + UIStyle.Key("一线"),
                    Highlight = new[] { x0, y0, x1, y1 },
                    Allowed = new[] { x0, y0, x1, y1 },
                    Complete = Match3SwapDone
                },
                new TutorialStep
                {
                    Key = "m3.step2",
                    Desc = "继续消除，达到" + UIStyle.Key("分数") + "即" + UIStyle.Key("过关"),
                    Complete = Match3AnyMatch
                }
            };
        }

        private static TutorialStep[] Lianliankan()
        {
            TutorialCatalog.LianliankanFirstPair(out var r0, out var c0, out var r1, out var c1);
            return new[]
            {
                new TutorialStep
                {
                    Key = "lian.step1",
                    Desc = "点选这两张" + UIStyle.Key("相同") + "的牌并" + UIStyle.Key("消除"),
                    Highlight = new[] { r0, c0, r1, c1 },
                    Allowed = new[] { r0, c0, r1, c1 },
                    Complete = LianFirstPair
                },
                new TutorialStep
                {
                    Key = "lian.step2",
                    Desc = "继续消除相同图案，全部消除即" + UIStyle.Key("过关"),
                    Complete = LianAnyPair
                }
            };
        }

        private static TutorialStep[] Sheep()
        {
            // 叠叠消这里坐标语义是「图案 id」而不是格子索引：牌一进槽就会从棋盘列表里摘掉，
            // 索引会整体前移，用索引做门控第二张就对不上了。图案 id 不会变。
            return new[]
            {
                new TutorialStep
                {
                    Key = "sheep.step1",
                    Desc = "点选" + UIStyle.Key("三张相同") + "的牌，进入" + UIStyle.Key("槽位") + "后消除",
                    Highlight = new[] { 1 },
                    Allowed = new[] { 1 },
                    Complete = SheepFirstGroup
                },
                new TutorialStep
                {
                    Key = "sheep.step2",
                    Desc = UIStyle.Key("槽位有限") + "。继续凑齐三张，清空棋盘即" + UIStyle.Key("过关"),
                    Complete = SheepAnyGroup
                }
            };
        }
    }
}
