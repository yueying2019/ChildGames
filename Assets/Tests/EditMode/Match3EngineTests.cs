using NUnit.Framework;
using UnityEngine;

namespace SmallGames.Tests
{
    /// <summary>
    /// 三消引擎纯逻辑测试：不依赖场景、预制体、协程，可直接 EditMode 运行。
    /// </summary>
    public sealed class Match3EngineTests
    {
        private static Match3LevelData MakeLevel(int width, int height, int colors, int moves)
        {
            var level = ScriptableObject.CreateInstance<Match3LevelData>();
            level.Width = width;
            level.Height = height;
            level.ColorCount = colors;
            level.MoveLimit = moves;
            level.GoalType = Match3GoalType.Score;
            level.EnsureInitialized();
            level.FillPlayable(0);
            return level;
        }

        [Test]
        public void Load_默认关卡_开局不是死局()
        {
            var engine = new Match3Engine(12345);
            engine.Load(Match3LevelData.CreateDefault());
            Assert.IsTrue(engine.TryGetHint(out _, out _), "开局洗牌后必须存在至少一个可行交换");
        }

        [Test]
        public void Load_尺寸与步数来自关卡配置()
        {
            var level = MakeLevel(7, 9, 4, 30);
            var engine = new Match3Engine(1);
            engine.Load(level);
            Assert.AreEqual(7, engine.Width);
            Assert.AreEqual(9, engine.Height);
            Assert.AreEqual(4, engine.ColorCount);
            Assert.AreEqual(30, engine.MovesLeft);
        }

        [Test]
        public void TrySwap_非相邻格子_不生效也不扣步数()
        {
            var engine = new Match3Engine(7);
            engine.Load(MakeLevel(8, 8, 5, 20));
            var before = engine.MovesLeft;
            var result = engine.TrySwap(0, 0, 3, 3);
            Assert.IsFalse(result.Applied, "非相邻交换必须无效");
            Assert.AreEqual(before, engine.MovesLeft, "无效交换不应消耗步数");
        }

        [Test]
        public void TrySwap_按提示走一步_应当产生消除()
        {
            var engine = new Match3Engine(2024);
            engine.Load(MakeLevel(8, 8, 5, 20));
            Assert.IsTrue(engine.TryGetHint(out var a, out var b), "应能给出提示");
            var result = engine.TrySwap(a.X, a.Y, b.X, b.Y);
            Assert.IsTrue(result.Applied, "提示给出的交换必须能形成消除");
            Assert.IsTrue(result.Events.Count > 0, "消除后应产生棋盘事件供 View 播放");
        }

        [Test]
        public void 相同种子_棋盘完全一致()
        {
            var left = new Match3Engine(999);
            left.Load(MakeLevel(8, 8, 5, 20));
            var right = new Match3Engine(999);
            right.Load(MakeLevel(8, 8, 5, 20));

            for (var y = 0; y < left.Height; y++)
            {
                for (var x = 0; x < left.Width; x++)
                    Assert.AreEqual(ColorAt(left, x, y), ColorAt(right, x, y), $"格子 ({x},{y})");
            }
        }

        private static int ColorAt(Match3Engine engine, int x, int y)
        {
            var tile = engine.GetTile(x, y);
            return tile == null ? -1 : tile.Color;
        }

        [Test]
        public void EnsureInitialized_越界尺寸与颜色数被夹紧()
        {
            var level = ScriptableObject.CreateInstance<Match3LevelData>();
            level.Width = 99;
            level.Height = 1;
            level.ColorCount = 100;
            level.MoveLimit = 0;
            level.EnsureInitialized();

            Assert.AreEqual(Match3LevelData.MaxSize, level.Width);
            Assert.AreEqual(Match3LevelData.MinSize, level.Height);
            Assert.AreEqual(Match3LevelData.MaxColor, level.ColorCount);
            Assert.AreEqual(1, level.MoveLimit, "步数至少为 1");
        }
    }
}
