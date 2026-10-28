using UnityEngine;

namespace SmallGames
{
    public enum ShopItemId
    {
        None = 0,
        LianShuffle = 1,
        LianHint = 2,
        SheepRemove = 3,
        SheepUndo = 4,
        SheepShuffle = 5,
        Match3Hint = 6,
        Match3Shuffle = 7,
        Match3Hammer = 8,
        Match3LineH = 9,
        Match3LineV = 10,
        Match3Bomb = 11,
        Match3Moves = 12,
        SheepSlot = 13,
        LianTime = 14,
        LianFreeze = 15,
        LianAuto = 16,
        Match3ColorBomb = 17,
        Match3ColorClear = 18
    }

    public readonly struct ShopItemInfo
    {
        public ShopItemId Id { get; }
        public GameModeType Mode { get; }
        public string Title { get; }
        public string Desc { get; }
        public int Price { get; }

        public ShopItemInfo(ShopItemId id, GameModeType mode, string title, string desc, int price)
        {
            Id = id;
            Mode = mode;
            Title = title;
            Desc = desc;
            Price = price;
        }
    }

    public static class ShopCatalog
    {
        public const int FirstClearCoins = 50;
        public const int ExtraCoinsPerLevel = 10;
        public const int ReplayClearCoins = 10;
        public const int PageCount = 3;
        public static int Match3ExtraMoves => GameBalance.Current.Match3ShopExtraMoves;

        /// <summary>加时道具一次补的秒数。写成 const 而不是读 GameBalance：SO 走 Fallback 时新字段会是 0。</summary>
        public const int LianBonusSeconds = 30;

        /// <summary>冻结道具让倒计时停住的秒数。</summary>
        public const int LianFreezeSeconds = 5;

        public static int CoinsForClear(int levelId)
        {
            var index = levelId < 1 ? 0 : levelId - 1;
            return FirstClearCoins + index * ExtraCoinsPerLevel;
        }

        public static readonly ShopItemInfo[] All =
        {
            new ShopItemInfo(ShopItemId.LianShuffle, GameModeType.Lianliankan, "重排", "打乱剩余棋子位置", 80),
            new ShopItemInfo(ShopItemId.LianHint, GameModeType.Lianliankan, "提示", "标出一对可连的棋子", 50),
            new ShopItemInfo(ShopItemId.LianTime, GameModeType.Lianliankan, "加时", $"立刻补 {LianBonusSeconds} 秒", 60),
            new ShopItemInfo(ShopItemId.LianFreeze, GameModeType.Lianliankan, "冻结", $"倒计时停 {LianFreezeSeconds} 秒", 40),
            new ShopItemInfo(ShopItemId.LianAuto, GameModeType.Lianliankan, "自动连", "自动消掉一对可连的棋子", 50),
            new ShopItemInfo(ShopItemId.SheepRemove, GameModeType.Sheep, "移出", "把槽里最多 3 张牌移回场地", 80),
            new ShopItemInfo(ShopItemId.SheepUndo, GameModeType.Sheep, "撤回", "撤销上一步操作", 40),
            new ShopItemInfo(ShopItemId.SheepShuffle, GameModeType.Sheep, "洗牌", "打乱场上剩余牌的图案", 60),
            new ShopItemInfo(ShopItemId.SheepSlot, GameModeType.Sheep, "暂存位", "本局槽位上限 +1，差一张也不必认输", 70),
            new ShopItemInfo(ShopItemId.Match3Hint, GameModeType.Match3, "提示", "标出一步可消的交换", 50),
            new ShopItemInfo(ShopItemId.Match3Shuffle, GameModeType.Match3, "重排", "重排棋盘并保证有可消步", 80),
            new ShopItemInfo(ShopItemId.Match3Hammer, GameModeType.Match3, "消除", "点掉一颗棋子", 80),
            new ShopItemInfo(ShopItemId.Match3LineH, GameModeType.Match3, "横消", "把一颗棋子变成横向特效", 60),
            new ShopItemInfo(ShopItemId.Match3LineV, GameModeType.Match3, "竖消", "把一颗棋子变成纵向特效", 60),
            new ShopItemInfo(ShopItemId.Match3Bomb, GameModeType.Match3, "炸弹", "把一颗棋子变成炸弹特效", 80),
            new ShopItemInfo(ShopItemId.Match3Moves, GameModeType.Match3, "加步", $"立即增加 {Match3ExtraMoves} 步", 50),
            new ShopItemInfo(ShopItemId.Match3ColorBomb, GameModeType.Match3, "彩球笔", "把一颗棋子变成彩虹球", 120),
            new ShopItemInfo(ShopItemId.Match3ColorClear, GameModeType.Match3, "同色消", "点一颗棋子，消掉全场同色", 100)
        };

        public static GameModeType ModeOfPage(int page)
        {
            switch (page)
            {
                case 0: return GameModeType.Lianliankan;
                case 1: return GameModeType.Sheep;
                case 2: return GameModeType.Match3;
                default: return GameModeType.Lianliankan;
            }
        }

        public static int PageOf(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return 1;
                case GameModeType.Match3: return 2;
                default: return 0;
            }
        }

        public static string PageTitle(int page)
        {
            return GameModeCatalog.Get(ModeOfPage(page)).Title;
        }

        public static int CountOnPage(int page)
        {
            var mode = ModeOfPage(page);
            var count = 0;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Mode == mode)
                    count++;
            }

            return count;
        }

        public static ShopItemInfo GetOnPage(int page, int index)
        {
            var mode = ModeOfPage(page);
            var seen = 0;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Mode != mode)
                    continue;
                if (seen == index)
                    return All[i];
                seen++;
            }

            return default;
        }

        public static bool TryGet(ShopItemId id, out ShopItemInfo info)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id != id)
                    continue;
                info = All[i];
                return true;
            }

            info = default;
            return false;
        }

        /// <summary>用商店标价折成星星价，至少 10，按 5 取整。三种模式各自花自己的星星。</summary>
        public static int StarCost(ShopItemInfo info)
        {
            var raw = info.Price / 5;
            if (raw < 10)
                raw = 10;
            return (raw + 2) / 5 * 5;
        }

        /// <summary>当前模式商店里的一件道具，均匀随机。没有道具时返回 None。</summary>
        public static ShopItemId RandomItem(GameModeType mode)
        {
            var count = CountOnPage(PageOf(mode));
            if (count <= 0)
                return ShopItemId.None;
            return GetOnPage(PageOf(mode), Random.Range(0, count)).Id;
        }
    }
}
