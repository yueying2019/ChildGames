namespace SmallGames
{
    public static class ModeRulesCatalog
    {
        public static string Title(GameModeType mode)
        {
            return GameModeCatalog.Get(mode).Title + "规则";
        }

        public static string Body(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return Lianliankan;
                case GameModeType.Sheep: return Sheep;
                case GameModeType.Match3: return Match3;
                default: return string.Empty;
            }
        }

        public static readonly string Lianliankan =
            "目标\n" +
            "在时限内把棋盘上的图案全部消除。限制时间内没清完即失败。\n\n" +
            "怎么连\n" +
            "点选两块相同图案。只要它们之间能画出一条通路，并且这条路最多拐两个弯（也就是最多三段直线），就可以消掉。直线可以沿着空格或棋盘外侧走，不能穿过还在的棋子，也不能穿过障碍格。\n\n" +
            "操作\n" +
            "先点一块再点另一块。选错了再点一次即可取消。连对后会画出连线并消除。连续快速连通可以叠连击。\n\n" +
            "道具\n" +
            "重排：打乱剩余棋子位置，适合卡住时使用。\n" +
            "提示：标出当前可连的一对。\n" +
            "道具次数在商店购买，过关也会获得。\n\n" +
            "无尽模式\n" +
            "清完一波会立刻进入下一波，并额外加一点时间。时间耗尽才结束。";

        public static readonly string Sheep =
            "目标\n" +
            "把场上所有牌都消除。底部收纳槽满 7 张且凑不成三个相同，就失败。\n\n" +
            "怎么消\n" +
            "只能点目前没有被其他牌压住的牌。点中后牌会飞进底部 7 格收纳槽。槽里出现三张相同图案会立刻消去。左右两侧也可能叠着备用牌，同样要点最上面没被挡住的。\n\n" +
            "注意\n" +
            "被压在下面的牌点不到。优先把能凑成三个的牌收进来，避免槽位被杂牌占满。\n\n" +
            "道具\n" +
            "移出：把槽里最边上的一张送回场上。\n" +
            "撤销：撤回上一步收牌。\n" +
            "重排：打乱场上剩余牌的叠放。\n\n" +
            "无尽模式\n" +
            "清完一波进入下一波，槽位会保留，更考验留槽空间。";

        public static readonly string Match3 =
            "目标\n" +
            "在步数用完前完成关卡目标。目标可能是达到分数，或清掉冰层、石头、果冻、牢笼等障碍。顶部会显示还剩几步、还要清什么。\n\n" +
            "怎么消\n" +
            "滑动或点选，交换相邻两格。交换后若有一方连成 3 个或以上相同图案，就会消除并下落补齐。换完不能形成三消会弹回去，也不扣步数。\n\n" +
            "特效棋\n" +
            "连成 4 个：生成直线，再与任意棋子交换可打掉一整行或一整列。\n" +
            "连成 L / T 形：生成炸弹，炸掉周围 3×3。\n" +
            "连成 5 个：生成彩虹球，与一种颜色交换即可消掉全场该颜色。\n" +
            "特效互相交换往往范围更大。首次遇到新障碍或掉落物时，还会再弹出对应说明。\n\n" +
            "障碍与掉落\n" +
            "冰层：盖住的棋子不能换，先敲碎。\n" +
            "石头：占格，相邻消除或特效可砸碎。\n" +
            "果冻：在该格消除才能撕掉。\n" +
            "牢笼：锁住的棋子不能换，先打破。\n" +
            "加步、金豆荚：被特效打到时加步或得金币。\n\n" +
            "道具\n" +
            "提示、重排、锤子、横消、竖消、炸弹、加步，均可在商店补充。";
    }
}
