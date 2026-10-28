using UnityEngine;

namespace SmallGames
{
    public enum Match3GuideId
    {
        Line = 0,
        Bomb = 1,
        ColorBomb = 2,
        ExtraMoves = 3,
        GoldBean = 4,
        Ice = 5,
        Stone = 6,
        Jelly = 7,
        Cage = 8
    }

    public static class Match3GuideCatalog
    {
        /// <summary>从 "match3.Ice" 这类 key 反解出枚举，用于图标等枚举侧逻辑。</summary>
        public static bool TryFromKey(string key, out Match3GuideId id)
        {
            id = Match3GuideId.Line;
            if (string.IsNullOrEmpty(key))
                return false;

            var dot = key.LastIndexOf('.');
            var name = dot >= 0 ? key.Substring(dot + 1) : key;
            return System.Enum.TryParse(name, true, out id);
        }

        public static bool TryFromSpecial(Match3Special special, out Match3GuideId id)
        {
            switch (special)
            {
                case Match3Special.LineH:
                case Match3Special.LineV:
                    id = Match3GuideId.Line;
                    return true;
                case Match3Special.Bomb:
                    id = Match3GuideId.Bomb;
                    return true;
                case Match3Special.ColorBomb:
                    id = Match3GuideId.ColorBomb;
                    return true;
                default:
                    id = Match3GuideId.Line;
                    return false;
            }
        }

        public static bool TryFromOverlay(Match3CellKind kind, out Match3GuideId id)
        {
            switch (kind)
            {
                case Match3CellKind.Ice:
                    id = Match3GuideId.Ice;
                    return true;
                case Match3CellKind.Stone:
                    id = Match3GuideId.Stone;
                    return true;
                case Match3CellKind.Jelly:
                    id = Match3GuideId.Jelly;
                    return true;
                case Match3CellKind.Cage:
                    id = Match3GuideId.Cage;
                    return true;
                default:
                    id = Match3GuideId.Line;
                    return false;
            }
        }

        public static bool TryFromDrop(Match3Drop drop, out Match3GuideId id)
        {
            switch (drop)
            {
                case Match3Drop.ExtraMoves:
                    id = Match3GuideId.ExtraMoves;
                    return true;
                case Match3Drop.GoldBean:
                    id = Match3GuideId.GoldBean;
                    return true;
                default:
                    id = Match3GuideId.Line;
                    return false;
            }
        }

        public static string Title(Match3GuideId id)
        {
            switch (id)
            {
                case Match3GuideId.Line: return "直线特效";
                case Match3GuideId.Bomb: return "炸弹特效";
                case Match3GuideId.ColorBomb: return "彩虹球";
                case Match3GuideId.ExtraMoves: return "加步道具";
                case Match3GuideId.GoldBean: return "金豆荚";
                case Match3GuideId.Ice: return "冰层";
                case Match3GuideId.Stone: return "石头";
                case Match3GuideId.Jelly: return "果冻";
                case Match3GuideId.Cage: return "牢笼";
                default: return "新内容";
            }
        }

        public static string Desc(Match3GuideId id)
        {
            switch (id)
            {
                case Match3GuideId.Line:
                    return "连成 4 个会生成直线。与旁边任意棋子交换，就能打掉一整行或一整列。";
                case Match3GuideId.Bomb:
                    return "连成 L 形会生成炸弹。与旁边任意棋子交换，就能炸掉周围 3×3。";
                case Match3GuideId.ColorBomb:
                    return "连成 5 个会生成彩虹球。与一种颜色交换，即可消除全场该颜色。";
                case Match3GuideId.ExtraMoves:
                    return "被直线、炸弹或彩虹球打到时，增加 5 步。";
                case Match3GuideId.GoldBean:
                    return "被特效打到时，获得 100 金币。";
                case Match3GuideId.Ice:
                    return "冰块盖住的棋子不能交换。用三消或特效敲碎冰块后才能移动。";
                case Match3GuideId.Stone:
                    return "石头占格，不能放棋子。相邻消除或特效可以砸碎。";
                case Match3GuideId.Jelly:
                    return "在这一格消除棋子会撕掉一层果冻，清完才算完成目标。";
                case Match3GuideId.Cage:
                    return "被锁住的棋子不能交换。先打破牢笼，才能把它换走。";
                default:
                    return string.Empty;
            }
        }

        public static Sprite Icon(Match3GuideId id, Sprite[] pack)
        {
            switch (id)
            {
                case Match3GuideId.Line:
                    return Match3Icons.TileOf(pack, 1);
                case Match3GuideId.Bomb:
                    return Match3Icons.FromFile(pack, Match3Icons.BombFile);
                case Match3GuideId.ColorBomb:
                    return Match3Icons.FromFile(pack, 19);
                case Match3GuideId.ExtraMoves:
                    return Match3Icons.FromFile(pack, Match3Icons.ExtraMovesFile);
                case Match3GuideId.GoldBean:
                    return Match3Icons.FromFile(pack, Match3Icons.GoldBeanFile);
                case Match3GuideId.Ice:
                    return Match3Icons.OverlayOf(pack, Match3CellKind.Ice);
                case Match3GuideId.Stone:
                    return Match3Icons.OverlayOf(pack, Match3CellKind.Stone);
                case Match3GuideId.Jelly:
                    return Match3Icons.OverlayOf(pack, Match3CellKind.Jelly);
                case Match3GuideId.Cage:
                    return Match3Icons.OverlayOf(pack, Match3CellKind.Cage);
                default:
                    return null;
            }
        }

        public static bool ShowLineArrows(Match3GuideId id)
        {
            return id == Match3GuideId.Line;
        }
    }
}
