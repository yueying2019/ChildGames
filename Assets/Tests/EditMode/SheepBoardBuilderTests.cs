using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SmallGames.Tests
{
    /// <summary>
    /// 叠叠消棋盘生成的可解性不变量：图标必须成组出现，否则永远消不完。
    /// </summary>
    public sealed class SheepBoardBuilderTests
    {
        private static SheepLevelData MakeConfig(int typeNum, int layers, int blocks)
        {
            var config = ScriptableObject.CreateInstance<SheepLevelData>();
            config.TypeNum = typeNum;
            config.LevelNum = layers;
            config.LevelBlockNum = blocks;
            config.ComposeNum = 3;
            config.BorderStep = 1;
            config.EnsureInitialized();
            return config;
        }

        private static Dictionary<int, int> CountIcons(SheepBoardPlan plan)
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < plan.Cells.Length; i++)
                Add(counts, plan.Cells[i].IconId);
            for (var i = 0; i < plan.LeftIcons.Length; i++)
                Add(counts, plan.LeftIcons[i]);
            for (var i = 0; i < plan.RightIcons.Length; i++)
                Add(counts, plan.RightIcons[i]);
            return counts;
        }

        private static void Add(Dictionary<int, int> counts, int iconId)
        {
            counts.TryGetValue(iconId, out var value);
            counts[iconId] = value + 1;
        }

        [Test]
        public void Plan_总数等于关卡预期且是组合数的倍数()
        {
            var config = MakeConfig(4, 3, 8);
            var plan = SheepBoardBuilder.PlanPreview(config, 12345, 0);

            Assert.AreEqual(config.ResolvedTotal(0), plan.Total);
            Assert.AreEqual(0, plan.Total % config.ComposeNum, "总数必须能被每组张数整除");
            Assert.AreEqual(config.TypeNum, plan.TypeNum);
        }

        [Test]
        public void Plan_每种图标的数量都是组合数的倍数()
        {
            var config = MakeConfig(5, 3, 9);
            var plan = SheepBoardBuilder.PlanPreview(config, 777, 0);
            var counts = CountIcons(plan);

            var placed = 0;
            foreach (var pair in counts)
            {
                placed += pair.Value;
                Assert.AreEqual(0, pair.Value % config.ComposeNum, $"图标 {pair.Key} 出现 {pair.Value} 次，不成组则无法消完");
            }

            Assert.AreEqual(plan.Total, placed, "落盘数量应与计划总数一致");
        }

        [Test]
        public void Plan_相同种子_结果完全一致()
        {
            var config = MakeConfig(6, 4, 12);
            var a = SheepBoardBuilder.PlanPreview(config, 20240919, 0);
            var b = SheepBoardBuilder.PlanPreview(config, 20240919, 0);

            Assert.AreEqual(a.Total, b.Total);
            Assert.AreEqual(a.Cells.Length, b.Cells.Length);
            for (var i = 0; i < a.Cells.Length; i++)
            {
                Assert.AreEqual(a.Cells[i].IconId, b.Cells[i].IconId);
                Assert.AreEqual(a.Cells[i].GridX, b.Cells[i].GridX);
                Assert.AreEqual(a.Cells[i].GridY, b.Cells[i].GridY);
                Assert.AreEqual(a.Cells[i].Layer, b.Cells[i].Layer);
            }
        }

        [Test]
        public void Plan_图标上限会收窄类型数()
        {
            var config = MakeConfig(8, 2, 6);
            var plan = SheepBoardBuilder.PlanPreview(config, 5, 3);
            Assert.AreEqual(3, plan.TypeNum, "iconCap 不足时应按可用图标数收窄");
        }
    }
}
