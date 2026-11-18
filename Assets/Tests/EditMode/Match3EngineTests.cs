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

        [Test]
        public void Custom_满盘可涂可擦且不会结束()
        {
            var level = MakeLevel(8, 8, 6, 5);
            level.FixedBoard = true;
            level.TargetScore = 1;
            var engine = new Match3Engine(7);
            engine.Custom = true;
            engine.Load(level);

            for (var y = 0; y < engine.Height; y++)
            {
                for (var x = 0; x < engine.Width; x++)
                {
                    Assert.AreEqual(Match3CellKind.Playable, engine.GetKind(x, y));
                    Assert.IsNotNull(engine.GetTile(x, y));
                }
            }

            Assert.IsTrue(engine.EditorPaintColor(0, 0, 3));
            Assert.AreEqual(3, engine.GetTile(0, 0).Color);
            Assert.IsTrue(engine.EditorPaintObstacle(1, 0, Match3CellKind.Stone));
            Assert.IsNull(engine.GetTile(1, 0));
            Assert.AreEqual(Match3CellKind.Stone, engine.GetKind(1, 0));
            Assert.IsTrue(engine.EditorPaintObstacle(2, 0, Match3CellKind.Ice));
            Assert.IsNotNull(engine.GetTile(2, 0));
            Assert.AreEqual(Match3CellKind.Ice, engine.GetKind(2, 0));
            Assert.IsTrue(engine.EditorPaintSpecial(3, 0, Match3Special.LineH));
            Assert.AreEqual(Match3Special.LineH, engine.GetTile(3, 0).Special);
            Assert.IsTrue(engine.EditorPaintSpecial(4, 0, Match3Special.ColorBomb));
            Assert.AreEqual(Match3Special.ColorBomb, engine.GetTile(4, 0).Special);
            Assert.AreEqual(0, engine.GetTile(4, 0).Color);
            Assert.IsTrue(engine.EditorErase(0, 0));
            Assert.AreEqual(Match3CellKind.Hole, engine.GetKind(0, 0));
            Assert.IsFalse(engine.EditorErase(0, 0));

            engine.SetMovesLeft(0);
            var painted = engine.TryPaintSpecial(3, 0, Match3Special.Bomb);
            Assert.IsTrue(painted.Applied);
            Assert.IsFalse(painted.Won);
            Assert.IsFalse(painted.Lost);
            Assert.IsFalse(engine.GoalMet());
        }

        [Test]
        public void Custom_涂出三连后结算消除且不结束()
        {
            var level = MakeLevel(6, 6, 5, 12);
            level.FixedBoard = true;
            var engine = new Match3Engine(3);
            engine.Custom = true;
            engine.Load(level);
            var moves = engine.MovesLeft;

            Assert.IsTrue(engine.EditorPaintColor(0, 2, 2));
            Assert.IsTrue(engine.EditorPaintColor(1, 2, 2));
            Assert.IsTrue(engine.EditorPaintColor(2, 2, 2));
            var result = engine.EditorResolve();
            Assert.IsTrue(result.Applied);
            Assert.IsFalse(result.Won);
            Assert.IsFalse(result.Lost);
            Assert.Greater(engine.Score, 0);
            Assert.AreEqual(moves, engine.MovesLeft);
            Assert.IsFalse(engine.GoalMet());
        }
    }
}
