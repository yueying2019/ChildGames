using UnityEngine;

namespace SmallGames
{
    public enum SheepMotifKind
    {
        Auto = 0,
        Lawn = 1,
        Flower = 2,
        Tree = 3,
        Sheep = 4,
        Grove = 5,
        Butterfly = 6,
        Pond = 7
    }

    /// <summary>
    /// 叠叠消落点外形：左右对称，花草树木等剪影。
    /// </summary>
    public static class SheepMotifs
    {
        public const int OriginMax = SheepBoardBuilder.GridSize - SheepBoardBuilder.TileSpan;

        public static string DisplayName(SheepMotifKind kind)
        {
            switch (kind)
            {
                case SheepMotifKind.Lawn: return "草地";
                case SheepMotifKind.Flower: return "花朵";
                case SheepMotifKind.Tree: return "小树";
                case SheepMotifKind.Sheep: return "小羊";
                case SheepMotifKind.Grove: return "树丛";
                case SheepMotifKind.Butterfly: return "蝴蝶";
                case SheepMotifKind.Pond: return "池塘";
                default: return "自动";
            }
        }

        public static SheepMotifKind Resolve(SheepLevelData data)
        {
            if (data == null)
                return SheepMotifKind.Lawn;
            if (data.Motif != SheepMotifKind.Auto)
                return data.Motif;
            switch ((data.LevelId - 1) % 7)
            {
                case 0: return SheepMotifKind.Lawn;
                case 1: return SheepMotifKind.Sheep;
                case 2: return SheepMotifKind.Pond;
                case 3: return SheepMotifKind.Tree;
                case 4: return SheepMotifKind.Grove;
                case 5: return SheepMotifKind.Flower;
                default: return SheepMotifKind.Butterfly;
            }
        }

        public static bool[,] BuildMask(SheepMotifKind kind)
        {
            var mask = NewMask();
            switch (kind)
            {
                case SheepMotifKind.Flower:
                    FillFlower(mask);
                    break;
                case SheepMotifKind.Tree:
                    FillTree(mask);
                    break;
                case SheepMotifKind.Sheep:
                    FillSheep(mask);
                    break;
                case SheepMotifKind.Grove:
                    FillGrove(mask);
                    break;
                case SheepMotifKind.Butterfly:
                    FillButterfly(mask);
                    break;
                case SheepMotifKind.Pond:
                    FillPond(mask);
                    break;
                default:
                    FillLawn(mask);
                    break;
            }

            MirrorX(mask);
            return mask;
        }

        public static int MirrorX(int x) => OriginMax - x;

        public static int Key(int x, int y) => x * 100 + y;

        public static bool On(bool[,] mask, int x, int y)
        {
            if (x < 0 || y < 0 || x > OriginMax || y > OriginMax)
                return false;
            if (mask == null)
                return true;
            if (x >= mask.GetLength(0) || y >= mask.GetLength(1))
                return false;
            return mask[x, y];
        }

        public static int EmptySides(bool[,] mask, int x, int y)
        {
            var n = 0;
            if (!On(mask, x - 1, y)) n++;
            if (!On(mask, x + 1, y)) n++;
            if (!On(mask, x, y - 1)) n++;
            if (!On(mask, x, y + 1)) n++;
            return n;
        }

        public static bool IsRim(bool[,] mask, int x, int y) => On(mask, x, y) && EmptySides(mask, x, y) > 0;

        public static System.Collections.Generic.List<Vector2Int> CollectLeftOrigins(bool[,] mask)
        {
            var list = new System.Collections.Generic.List<Vector2Int>(128);
            for (var x = 0; x <= OriginMax; x++)
            {
                var mx = MirrorX(x);
                if (x > mx)
                    continue;
                for (var y = 0; y <= OriginMax; y++)
                {
                    if (On(mask, x, y))
                        list.Add(new Vector2Int(x, y));
                }
            }

            return list;
        }

        private static bool[,] NewMask()
        {
            return new bool[SheepBoardBuilder.GridSize, SheepBoardBuilder.GridSize];
        }

        private static void FillLawn(bool[,] mask)
        {
            FillEllipse(mask, 12f, 12f, 9.2f, 8.4f);
        }

        private static void FillFlower(bool[,] mask)
        {
            FillDisk(mask, 12f, 12.5f, 3.4f);
            for (var i = 0; i < 6; i++)
            {
                var a = i * 60f * Mathf.Deg2Rad;
                FillDisk(mask, 12f + Mathf.Cos(a) * 5.6f, 12.5f + Mathf.Sin(a) * 5.6f, 3.1f);
            }
        }

        private static void FillTree(bool[,] mask)
        {
            FillRect(mask, 10, 1, 13, 9);
            FillDisk(mask, 12f, 14.2f, 6.4f);
            FillDisk(mask, 8.2f, 13.2f, 4.2f);
            FillDisk(mask, 15.8f, 13.2f, 4.2f);
        }

        private static void FillSheep(bool[,] mask)
        {
            FillEllipse(mask, 12f, 9.2f, 7.2f, 4.6f);
            FillDisk(mask, 12f, 14.6f, 3.3f);
            FillDisk(mask, 8.6f, 16.6f, 1.7f);
            FillDisk(mask, 15.4f, 16.6f, 1.7f);
            FillRect(mask, 6, 2, 8, 5);
            FillRect(mask, 9, 2, 11, 5);
            FillRect(mask, 12, 2, 14, 5);
            FillRect(mask, 15, 2, 17, 5);
        }

        private static void FillGrove(bool[,] mask)
        {
            FillDisk(mask, 7.2f, 10.5f, 5.4f);
            FillDisk(mask, 16.8f, 10.5f, 5.4f);
            FillDisk(mask, 12f, 14.8f, 3.6f);
            FillRect(mask, 11, 3, 13, 8);
        }

        private static void FillButterfly(bool[,] mask)
        {
            FillRect(mask, 11, 6, 13, 16);
            FillEllipse(mask, 7.4f, 13.5f, 5.2f, 4.4f);
            FillEllipse(mask, 16.6f, 13.5f, 5.2f, 4.4f);
            FillEllipse(mask, 8.2f, 8.2f, 4.2f, 3.4f);
            FillEllipse(mask, 15.8f, 8.2f, 4.2f, 3.4f);
        }

        private static void FillPond(bool[,] mask)
        {
            FillDisk(mask, 12f, 12f, 8.6f);
            CutDisk(mask, 12f, 12f, 4.2f);
        }

        private static void FillDisk(bool[,] mask, float ox, float oy, float r)
        {
            FillEllipse(mask, ox, oy, r, r);
        }

        private static void CutDisk(bool[,] mask, float ox, float oy, float r)
        {
            var r2 = r * r;
            for (var x = 0; x <= OriginMax; x++)
            {
                for (var y = 0; y <= OriginMax; y++)
                {
                    var dx = x + 1.5f - ox;
                    var dy = y + 1.5f - oy;
                    if (dx * dx + dy * dy <= r2)
                        mask[x, y] = false;
                }
            }
        }

        private static void FillEllipse(bool[,] mask, float ox, float oy, float rx, float ry)
        {
            var rx2 = Mathf.Max(0.01f, rx * rx);
            var ry2 = Mathf.Max(0.01f, ry * ry);
            for (var x = 0; x <= OriginMax; x++)
            {
                for (var y = 0; y <= OriginMax; y++)
                {
                    var dx = x + 1.5f - ox;
                    var dy = y + 1.5f - oy;
                    if (dx * dx / rx2 + dy * dy / ry2 <= 1f)
                        mask[x, y] = true;
                }
            }
        }

        private static void FillRect(bool[,] mask, int x0, int y0, int x1, int y1)
        {
            for (var x = x0; x <= x1; x++)
            {
                for (var y = y0; y <= y1; y++)
                    Stamp(mask, x, y);
            }
        }

        private static void Stamp(bool[,] mask, int x, int y)
        {
            if (x < 0 || y < 0 || x >= SheepBoardBuilder.GridSize || y >= SheepBoardBuilder.GridSize)
                return;
            mask[x, y] = true;
        }

        private static void MirrorX(bool[,] mask)
        {
            for (var x = 0; x <= OriginMax; x++)
            {
                var mx = MirrorX(x);
                for (var y = 0; y <= OriginMax; y++)
                {
                    if (mask[x, y])
                        mask[mx, y] = true;
                }
            }
        }
    }
}
