using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class Match3SampleLevelBuilder
    {
        private const string Folder = "Assets/Content/Match3/Levels";

        [MenuItem("SmallGames/三消/生成示例关卡", false, 2)]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            // 休闲三消常见一章：得分入门 → 收集/异形盘 → 冰石 → 果冻牢笼混合，步数逐步收紧。
            CreateOrUpdate("Level_01.asset", 1, "甜品入门", 8, 8, 5, 28, Match3GoalType.Score, 1600, null, null);
            CreateOrUpdate("Level_02.asset", 2, "中空棋盘", 8, 8, 4, 24, Match3GoalType.Collect, 0, new[] { 0, 16, 0, 0, 10 }, DonutHoles);
            CreateOrUpdate("Level_03.asset", 3, "五色热身", 8, 8, 5, 24, Match3GoalType.Score, 2200, null, null);
            CreateOrUpdate("Level_04.asset", 4, "双色收集", 8, 8, 4, 22, Match3GoalType.Collect, 0, new[] { 0, 20, 0, 16 }, null);
            CreateOrUpdate("Level_05.asset", 5, "五色冲刺", 9, 7, 5, 26, Match3GoalType.Score, 2200, null, CrossHoles);
            CreateOrUpdate("Level_06.asset", 6, "细长走廊", 9, 6, 5, 20, Match3GoalType.Score, 2400, null, CorridorHoles);
            CreateOrUpdate("Level_07.asset", 7, "环形跑道", 8, 8, 5, 22, Match3GoalType.Collect, 0, new[] { 0, 14, 0, 0, 14 }, RingHoles);
            CreateOrUpdate("Level_08.asset", 8, "十字缺口", 8, 8, 5, 20, Match3GoalType.Collect, 0, new[] { 0, 18, 12 }, CrossHoles);
            CreateOrUpdate("Level_09.asset", 9, "步数挑战", 8, 8, 5, 22, Match3GoalType.Score, 2200, null, null);
            CreateOrUpdate("Level_10.asset", 10, "薄冰覆盖", 8, 8, 5, 24, Match3GoalType.ClearCells, 0, null, ThinIceField);
            CreateOrUpdate("Level_11.asset", 11, "冰石夹道", 8, 8, 5, 24, Match3GoalType.ClearCells, 0, null, IceStoneLane);
            CreateOrUpdate("Level_12.asset", 12, "双层寒冰", 8, 8, 5, 22, Match3GoalType.ClearCells, 0, null, ThickIceRows);
            CreateOrUpdate("Level_13.asset", 13, "石阵开口", 8, 8, 5, 22, Match3GoalType.ClearCells, 0, null, StoneGate);
            CreateOrUpdate("Level_14.asset", 14, "冰石混合", 8, 8, 5, 24, Match3GoalType.ClearCells, 0, null, IceStoneMix);
            CreateOrUpdate("Level_15.asset", 15, "果冻地毯", 8, 8, 5, 28, Match3GoalType.ClearCells, 0, null, JellyCarpet);
            CreateOrUpdate("Level_16.asset", 16, "果冻牢笼", 8, 8, 5, 24, Match3GoalType.ClearCells, 0, null, JellyCageYard);
            CreateOrUpdate("Level_17.asset", 17, "三色催收", 8, 8, 5, 26, Match3GoalType.Collect, 0, new[] { 0, 12, 12, 12 }, null);
            CreateOrUpdate("Level_18.asset", 18, "夹心蛋糕", 8, 8, 5, 26, Match3GoalType.ClearCells, 0, null, SandwichBlockers);
            CreateOrUpdate("Level_19.asset", 19, "锁与石墙", 8, 8, 5, 26, Match3GoalType.ClearCells, 0, null, CageAndStone);
            CreateOrUpdate("Level_20.asset", 20, "甜品终章", 9, 8, 5, 28, Match3GoalType.ClearCells, 0, null, FinaleMix);
            CreateOrUpdate("Level_21.asset", 21, "菱形甜点", 8, 8, 5, 26, Match3GoalType.Score, 1800, null, DiamondHoles);
            CreateOrUpdate("Level_22.asset", 22, "果冻工坊", 8, 8, 5, 30, Match3GoalType.ClearCells, 0, null, JellyWorkshop);
            CreateOrUpdate("Level_23.asset", 23, "彩虹订单", 8, 8, 5, 32, Match3GoalType.Collect, 0, new[] { 0, 20, 15, 10 }, null);
            CreateOrUpdate("Level_24.asset", 24, "限时甜点", 8, 8, 5, 40, Match3GoalType.Score, 2400, null, null, 150f);
            CreateOrUpdate("Level_25.asset", 25, "草莓拼盘", 8, 8, 5, 30, Match3GoalType.Score, 1800, null, null);
            CreateOrUpdate("Level_26.asset", 26, "四色小集", 8, 8, 4, 28, Match3GoalType.Collect, 0, new[] { 0, 10, 10 }, null);
            CreateOrUpdate("Level_27.asset", 27, "方格留白", 8, 8, 5, 28, Match3GoalType.Score, 2000, null, DonutHoles);
            CreateOrUpdate("Level_28.asset", 28, "薄冰一角", 8, 8, 5, 30, Match3GoalType.ClearCells, 0, null, TopIceRows);
            CreateOrUpdate("Level_29.asset", 29, "果冻边框", 8, 8, 5, 30, Match3GoalType.ClearCells, 0, null, JellyBorder);
            CreateOrUpdate("Level_30.asset", 30, "加时甜品", 8, 8, 5, 36, Match3GoalType.Score, 2000, null, null, 180f);
            CreateOrUpdate("Level_31.asset", 31, "双色再收", 8, 8, 5, 30, Match3GoalType.Collect, 0, new[] { 0, 12, 12 }, null);
            CreateOrUpdate("Level_32.asset", 32, "石子小路", 8, 8, 5, 30, Match3GoalType.ClearCells, 0, null, StonePath);
            CreateOrUpdate("Level_33.asset", 33, "中空再战", 8, 8, 4, 28, Match3GoalType.Collect, 0, new[] { 0, 12 }, DonutHoles);
            CreateOrUpdate("Level_34.asset", 34, "彩虹热身", 8, 8, 5, 28, Match3GoalType.Score, 2200, null, null);
            CreateOrUpdate("Level_35.asset", 35, "冰与果冻", 8, 8, 5, 32, Match3GoalType.ClearCells, 0, null, IceAndJellyRows);
            CreateOrUpdate("Level_36.asset", 36, "甜品新章", 8, 8, 5, 32, Match3GoalType.ClearCells, 0, null, ShortIceRow);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] 已生成示例关卡：" + Folder);
        }

        private static void CreateOrUpdate(
            string fileName,
            int id,
            string title,
            int width,
            int height,
            int colors,
            int moves,
            Match3GoalType goal,
            int score,
            int[] collect,
            System.Action<Match3LevelData> paint,
            float timeLimit = 0f)
        {
            var path = $"{Folder}/{fileName}";
            var asset = AssetDatabase.LoadAssetAtPath<Match3LevelData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Match3LevelData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.LevelId = id;
            asset.DisplayName = title;
            asset.Width = width;
            asset.Height = height;
            asset.ColorCount = colors;
            asset.MoveLimit = moves;
            asset.TimeLimit = timeLimit;
            asset.GoalType = goal;
            asset.TargetScore = score;
            asset.CollectCounts = new int[7];
            if (collect != null)
            {
                for (var i = 0; i < collect.Length && i < asset.CollectCounts.Length; i++)
                    asset.CollectCounts[i] = collect[i];
            }

            asset.Resize(width, height);
            asset.FillPlayable();
            paint?.Invoke(asset);
            EditorUtility.SetDirty(asset);
        }

        private static void DonutHoles(Match3LevelData level)
        {
            FillRect(level, 3, 3, 4, 4, Match3CellSpec.Empty);
        }

        private static void CrossHoles(Match3LevelData level)
        {
            var cx = level.Width / 2;
            var cy = level.Height / 2;
            level.SetCell(cx, cy, Match3CellSpec.Empty);
            level.SetCell(cx - 1, cy, Match3CellSpec.Empty);
            level.SetCell(cx + 1, cy, Match3CellSpec.Empty);
            level.SetCell(cx, cy - 1, Match3CellSpec.Empty);
            level.SetCell(cx, cy + 1, Match3CellSpec.Empty);
        }

        private static void CorridorHoles(Match3LevelData level)
        {
            for (var y = 0; y < level.Height; y++)
            {
                level.SetCell(0, y, Match3CellSpec.Empty);
                level.SetCell(level.Width - 1, y, Match3CellSpec.Empty);
            }
        }

        private static void RingHoles(Match3LevelData level)
        {
            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Empty);
            FillRect(level, 3, 3, 4, 4, Match3CellSpec.Playable());
        }

        private static void ThinIceField(Match3LevelData level)
        {
            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
        }

        private static void IceStoneLane(Match3LevelData level)
        {
            for (var x = 2; x <= 5; x++)
            {
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
                level.SetCell(x, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
            }

            level.SetCell(1, 3, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            level.SetCell(1, 4, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
            level.SetCell(6, 3, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            level.SetCell(6, 4, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
        }

        private static void ThickIceRows(Match3LevelData level)
        {
            for (var x = 1; x <= 6; x++)
            {
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
                level.SetCell(x, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
            }
        }

        private static void StoneGate(Match3LevelData level)
        {
            for (var y = 2; y <= 5; y++)
            {
                level.SetCell(2, y, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
                level.SetCell(5, y, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            }

            level.SetCell(2, 3, Match3CellSpec.Playable());
            level.SetCell(5, 4, Match3CellSpec.Playable());
        }

        private static void IceStoneMix(Match3LevelData level)
        {
            IceStoneLane(level);
            FillRect(level, 3, 3, 4, 4, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
        }

        private static void JellyCarpet(Match3LevelData level)
        {
            FillRect(level, 1, 1, 6, 6, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
        }

        private static void JellyCageYard(Match3LevelData level)
        {
            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            level.SetCell(2, 2, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(5, 2, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(2, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 2));
            level.SetCell(5, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 2));
            level.SetCell(3, 3, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 2));
            level.SetCell(4, 4, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 2));
        }

        private static void SandwichBlockers(Match3LevelData level)
        {
            for (var x = 1; x <= 6; x++)
            {
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
                level.SetCell(x, 3, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
                level.SetCell(x, 4, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 2));
                level.SetCell(x, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
            }
        }

        private static void CageAndStone(Match3LevelData level)
        {
            for (var y = 1; y <= 6; y++)
            {
                level.SetCell(1, y, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
                level.SetCell(6, y, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
            }

            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(3, 3, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 2));
            level.SetCell(4, 4, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 2));
        }

        private static void FinaleMix(Match3LevelData level)
        {
            FillRect(level, 1, 1, level.Width - 2, level.Height - 2, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            for (var x = 2; x <= level.Width - 3; x++)
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
            level.SetCell(1, 3, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            level.SetCell(level.Width - 2, 4, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
            level.SetCell(2, 1, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(level.Width - 3, 1, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 2));
            level.SetCell(3, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(5, 5, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 2));
        }

        private static void DiamondHoles(Match3LevelData level)
        {
            for (var y = 0; y < level.Height; y++)
            {
                var band = y <= 3 ? 3 - y : y - 4;
                for (var x = 0; x < band; x++)
                {
                    level.SetCell(x, y, Match3CellSpec.Empty);
                    level.SetCell(level.Width - 1 - x, y, Match3CellSpec.Empty);
                }
            }
        }

        private static void JellyWorkshop(Match3LevelData level)
        {
            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 2));
        }

        private static void TopIceRows(Match3LevelData level)
        {
            FillRect(level, 0, 0, level.Width - 1, 1, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
        }

        private static void JellyBorder(Match3LevelData level)
        {
            for (var x = 0; x < level.Width; x++)
            {
                level.SetCell(x, 0, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
                level.SetCell(x, level.Height - 1, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            }

            for (var y = 1; y < level.Height - 1; y++)
            {
                level.SetCell(0, y, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
                level.SetCell(level.Width - 1, y, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            }
        }

        private static void StonePath(Match3LevelData level)
        {
            for (var y = 1; y <= 6; y++)
                level.SetCell(3, y, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
        }

        private static void IceAndJellyRows(Match3LevelData level)
        {
            for (var x = 2; x <= 5; x++)
            {
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
                level.SetCell(x, 5, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            }
        }

        private static void ShortIceRow(Match3LevelData level)
        {
            for (var x = 2; x <= 5; x++)
                level.SetCell(x, 3, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
        }

        private static void FillRect(Match3LevelData level, int x0, int y0, int x1, int y1, Match3CellSpec spec)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                    level.SetCell(x, y, spec);
            }
        }
    }
}
