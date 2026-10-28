namespace SmallGames
{
    public enum LianliankanLayoutKind
    {
        Ring32 = 0,
        Center36 = 1,
        Wide48 = 2,
        Full64 = 3,
        Twin40 = 4,
        Hollow48 = 5,
        Columns42 = 6,
        UShape44 = 7,
        Custom = 8
    }

    /// <summary>
    /// 入门关：四周留空方便绕线，中间开口，32 格、6 种图案。
    /// </summary>
    public static class LianliankanBoardLayout
    {
        public const int EasyTypeCount = 6;

        public static readonly bool[,] Level1 =
        {
            { false, false, false, false, false, false, false, false, false, false },
            { false, false, true,  true,  true,  true,  true,  true,  false, false },
            { false, false, true,  true,  true,  true,  true,  true,  false, false },
            { false, false, true,  true,  false, false, true,  true,  false, false },
            { false, false, true,  true,  false, false, true,  true,  false, false },
            { false, false, true,  true,  true,  true,  true,  true,  false, false },
            { false, false, true,  true,  true,  true,  true,  true,  false, false },
            { false, false, false, false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, false, false, false }
        };

        public static int OccupiedCount()
        {
            var count = 0;
            var rows = Level1.GetLength(0);
            var cols = Level1.GetLength(1);
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (Level1[r, c])
                        count++;
                }
            }

            return count;
        }

        public static bool IsOn(int row, int col)
        {
            return IsOn(Level1, row, col);
        }

        public static bool[,] GetLayout(LianliankanLayoutKind kind)
        {
            switch (kind)
            {
                case LianliankanLayoutKind.Center36:
                    return FilledRect(2, 7, 2, 7);
                case LianliankanLayoutKind.Wide48:
                    return FilledRect(1, 8, 2, 7);
                case LianliankanLayoutKind.Full64:
                    return FilledRect(1, 8, 1, 8);
                case LianliankanLayoutKind.Twin40:
                    return Merge(FilledRect(2, 6, 1, 4), FilledRect(2, 6, 5, 8));
                case LianliankanLayoutKind.Hollow48:
                    return Subtract(FilledRect(1, 8, 1, 8), FilledRect(3, 6, 3, 6));
                case LianliankanLayoutKind.Columns42:
                    return Merge(Merge(FilledRect(2, 8, 1, 2), FilledRect(2, 8, 4, 5)), FilledRect(2, 8, 7, 8));
                case LianliankanLayoutKind.UShape44:
                    return Subtract(FilledRect(1, 8, 1, 8), FilledRect(1, 5, 3, 6));
                case LianliankanLayoutKind.Custom:
                    return new bool[Level1.GetLength(0), Level1.GetLength(1)];
                default:
                    return Level1;
            }
        }

        public static bool IsOn(bool[,] layout, int row, int col)
        {
            if (layout == null || row < 0 || col < 0 || row >= layout.GetLength(0) || col >= layout.GetLength(1))
                return false;
            return layout[row, col];
        }

        public static string DisplayName(LianliankanLayoutKind kind)
        {
            switch (kind)
            {
                case LianliankanLayoutKind.Ring32: return "入门环";
                case LianliankanLayoutKind.Center36: return "中心块";
                case LianliankanLayoutKind.Wide48: return "宽幅田";
                case LianliankanLayoutKind.Full64: return "满盘";
                case LianliankanLayoutKind.Twin40: return "双园";
                case LianliankanLayoutKind.Hollow48: return "空心篮";
                case LianliankanLayoutKind.Columns42: return "三垄";
                case LianliankanLayoutKind.UShape44: return "U形";
                default: return "自定义";
            }
        }

        private static bool[,] FilledRect(int row0, int row1, int col0, int col1)
        {
            var rows = Level1.GetLength(0);
            var cols = Level1.GetLength(1);
            var mask = new bool[rows, cols];
            for (var r = row0; r <= row1 && r < rows; r++)
            {
                if (r < 0)
                    continue;
                for (var c = col0; c <= col1 && c < cols; c++)
                {
                    if (c < 0)
                        continue;
                    mask[r, c] = true;
                }
            }

            return mask;
        }

        private static bool[,] Merge(bool[,] a, bool[,] b)
        {
            var rows = Level1.GetLength(0);
            var cols = Level1.GetLength(1);
            var mask = new bool[rows, cols];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                    mask[r, c] = IsOn(a, r, c) || IsOn(b, r, c);
            }

            return mask;
        }

        private static bool[,] Subtract(bool[,] source, bool[,] cut)
        {
            var rows = Level1.GetLength(0);
            var cols = Level1.GetLength(1);
            var mask = new bool[rows, cols];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                    mask[r, c] = IsOn(source, r, c) && !IsOn(cut, r, c);
            }

            return mask;
        }
    }
}
