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
                    Desc = "按住这一颗，和它下面的交换，让三颗一样的连成一条线",
                    Highlight = new[] { x0, y0, x1, y1 },
                    Allowed = new[] { x0, y0, x1, y1 },
                    Complete = Match3SwapDone
                },
                new TutorialStep
                {
                    Key = "m3.step2",
                    Desc = "很好！再接着消除，攒够分数就过关",
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
                    Desc = "点这两张一样的牌，把它们连起来消掉",
                    Highlight = new[] { r0, c0, r1, c1 },
                    Allowed = new[] { r0, c0, r1, c1 },
                    Complete = LianFirstPair
                },
                new TutorialStep
                {
                    Key = "lian.step2",
                    Desc = "继续找出一样的牌，全部消完就过关",
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
                    Desc = "点三张一样的牌，它们会掉进下面的槽里消掉",
                    Highlight = new[] { 1 },
                    Allowed = new[] { 1 },
                    Complete = SheepFirstGroup
                },
                new TutorialStep
                {
                    Key = "sheep.step2",
                    Desc = "槽位只有几个，别乱点。继续凑齐三张，清空棋盘就过关",
                    Complete = SheepAnyGroup
                }
            };
        }
    }
}
