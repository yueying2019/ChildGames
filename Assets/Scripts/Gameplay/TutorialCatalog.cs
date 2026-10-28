using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 教学关（第 0 关）：每个玩法一个固定排布的引导关，排在正式关卡之前。
    ///
    /// 与正式关的三点区别：
    /// 1. 棋盘完全固定（不随机），保证引导脚本里写的坐标一定成立；
    /// 2. 不计星级、不计入解锁进度，只记「是否已学会」；
    /// 3. 失败不弹续关付费面板，直接重来。
    ///
    /// 关卡数据是代码内置的（不走 Addressables）：教学关要改就是改引导逻辑，本来就得提审。
    /// </summary>
    public static class TutorialCatalog
    {
        /// <summary>教学关的 levelId 哨兵：不与任何正式关（从 1 起）冲突。</summary>
        public const int LevelId = 0;

        /// <summary>选关列表 / StartGame 里表示「教学关」的 index 哨兵。</summary>
        public const int Index = -1;

        public static bool IsLevelId(int levelId)
        {
            return levelId == LevelId;
        }

        public static bool IsIndex(int index)
        {
            return index == Index;
        }

        public static bool Supports(GameModeType mode)
        {
            return mode == GameModeType.Lianliankan
                   || mode == GameModeType.Sheep
                   || mode == GameModeType.Match3;
        }

        /// <summary>该玩法的教学关是否已通过（含「跳过教学」）。</summary>
        public static bool IsCleared(GameModeType mode)
        {
            return ProgressRepository.IsTutorialCleared(mode);
        }

        public static void MarkCleared(GameModeType mode)
        {
            ProgressRepository.MarkTutorialCleared(mode);
        }

        public static string Title(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Match3: return "三消教学";
                case GameModeType.Sheep: return "叠叠消教学";
                default: return "对对消教学";
            }
        }

        public static string Desc(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Match3: return "学会交换与消除";
                case GameModeType.Sheep: return "学会凑齐三张消掉";
                default: return "学会连线消对";
            }
        }

        // ============================ 三消 ============================

        /// <summary>
        /// 6×6 固定盘。整盘走对角线循环（(x+y)%4），天然没有任何三连；
        /// 只把 (3,3) 特设成 1，于是把 (2,2) 的 1 换下来就能和 (1,3)(3,3) 凑成三连——
        /// 引导第一步要玩家做的正是这一手。
        /// </summary>
        public static Match3LevelData Match3Level()
        {
            var data = ScriptableObject.CreateInstance<Match3LevelData>();
            data.name = "Tutorial_Match3";
            data.hideFlags = HideFlags.HideAndDontSave;
            data.DisplayName = Title(GameModeType.Match3);
            data.Width = 6;
            data.Height = 6;
            data.ColorCount = 4;
            // 教学关刻意宽松：目标定得高会让新手在"学操作"的时候先尝到失败
            data.MoveLimit = 15;
            data.TimeLimit = 0f;
            data.GoalType = Match3GoalType.Score;
            data.TargetScore = 200;
            data.FixedBoard = true;
            data.CollectCounts = new int[7];
            data.Cells = new Match3CellSpec[data.Width * data.Height];

            for (var y = 0; y < data.Height; y++)
            {
                for (var x = 0; x < data.Width; x++)
                    data.SetCell(x, y, Match3CellSpec.Playable((x + y) % data.ColorCount + 1));
            }

            // 制造「差一步」的三连机会：(1,3)=1 (3,3)=1，只差 (2,3) 也是 1
            data.SetCell(3, 3, Match3CellSpec.Playable(1));
            return data;
        }

        /// <summary>引导第一步要求的交换：(2,2) 与 (2,3) 竖向相邻。</summary>
        public static void Match3FirstSwap(out int x0, out int y0, out int x1, out int y1)
        {
            x0 = 2;
            y0 = 2;
            x1 = 2;
            y1 = 3;
        }

        // =========================== 对对消 ===========================

        /// <summary>
        /// 10×10 网格里只摆 8 张（4 对），两两横向相邻，第一对就是引导要求点的那一对。
        /// 相邻同类可以零转弯直连，新手第一次点就能成功。
        /// </summary>
        public static LianliankanLevelData LianliankanLevel()
        {
            var data = ScriptableObject.CreateInstance<LianliankanLevelData>();
            data.name = "Tutorial_Lianliankan";
            data.hideFlags = HideFlags.HideAndDontSave;
            data.Title = Title(GameModeType.Lianliankan);
            data.TypeCount = 4;
            data.TimeLimit = 180f;
            data.ComboTimeBonus = 0f;
            data.Cells = new byte[LianliankanLevelData.CellCount];
            data.IconIds = new byte[LianliankanLevelData.CellCount];

            PlacePair(data, 4, 3, 1);
            PlacePair(data, 4, 5, 2);
            PlacePair(data, 5, 3, 3);
            PlacePair(data, 5, 5, 4);
            data.Layout = LianliankanLayoutKind.Custom;
            return data;
        }

        /// <summary>引导第一步要求点的一对：(4,3) 与 (4,4)。</summary>
        public static void LianliankanFirstPair(out int r0, out int c0, out int r1, out int c1)
        {
            r0 = 4;
            c0 = 3;
            r1 = 4;
            c1 = 4;
        }

        private static void PlacePair(LianliankanLevelData data, int row, int col, int icon)
        {
            for (var i = 0; i < 2; i++)
            {
                var c = col + i;
                var index = row * LianliankanLevelData.Cols + c;
                data.Cells[index] = (byte)LianliankanCellKind.Tile;
                data.IconIds[index] = (byte)icon;
            }
        }

        // =========================== 叠叠消 ===========================

        /// <summary>
        /// 9 张单层牌、3 种图案各 3 张（= 3 组）。
        /// 每种图案的张数必须是 ComposeNum 的整数倍，否则消到最后会剩几张凑不成一组，
        /// 关卡直接卡死——所以这里是 3×3 而不是 4×4（4 张除不尽 3）。
        /// 图案 1 的三张（索引 0/5/7）就是引导第一步要求点的那一组。
        /// </summary>
        public static SheepLevelData SheepLevel()
        {
            var data = ScriptableObject.CreateInstance<SheepLevelData>();
            data.name = "Tutorial_Sheep";
            data.hideFlags = HideFlags.HideAndDontSave;
            data.Title = Title(GameModeType.Sheep);
            data.TypeNum = 3;
            data.LevelNum = 1;
            data.LevelBlockNum = 9;
            data.ComposeNum = SheepLevelData.DefaultCompose;
            data.SlotBonus = 0;
            data.TimeLimit = 0f;
            data.GoalCount = 0;

            var icons = new int[,]
            {
                { 1, 2, 3 },
                { 2, 3, 1 },
                { 3, 1, 2 }
            };

            // 注意：Sheep 的 GridX/GridY 是 24 格画布网格（SheepBoardBuilder.GridSize），
            // 牌中心 = Grid + 1.5，宽约 2.7 格。这里取 {6,11,16}（中心 7.5/12.5/17.5，
            // 间距 5 格）才能在画布上居中铺开；写 0..2 会把 9 张牌全挤进左下角。
            const int lo = 6;
            const int step = 5;
            var cells = new SheepFixedCell[9];
            var n = 0;
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                    cells[n++] = SheepFixedCell.Of(lo + x * step, lo + y * step, 0, icons[y, x]);
            }

            data.FixedCells = cells;
            return data;
        }

        /// <summary>引导第一步要求点的三张（图案 1 的其中三张，第四张留作容错）。</summary>
        public static void SheepFirstGroup(out int[] iconIds, out int count)
        {
            iconIds = new[] { 1, 1, 1 };
            count = 3;
        }
    }
}
