using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SmallGames.Tests
{
    /// <summary>
    /// 对对消连通规则测试。走 Load(int[,], bool[,]) 纯数据入口，不需要 TileView。
    /// </summary>
    public sealed class LianliankanEngineTests
    {
        private const int Rows = LianliankanEngine.Rows;
        private const int Cols = LianliankanEngine.Cols;

        private static int[,] EmptyIds()
        {
            var ids = new int[Rows, Cols];
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                    ids[r, c] = -1;
            }

            return ids;
        }

        [Test]
        public void 相邻同图案_可以连接()
        {
            var ids = EmptyIds();
            ids[0, 0] = 5;
            ids[0, 1] = 5;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);
            var path = new List<Vector2Int>();

            Assert.IsTrue(engine.TryFindPath(0, 0, 0, 1, path));
            Assert.IsTrue(path.Count > 0, "应返回折线供 View 绘制连线");
        }

        [Test]
        public void 不同图案_不可连接()
        {
            var ids = EmptyIds();
            ids[0, 0] = 5;
            ids[0, 1] = 6;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);

            Assert.IsFalse(engine.TryFindPath(0, 0, 0, 1, null));
        }

        [Test]
        public void 同一格_不可连接()
        {
            var ids = EmptyIds();
            ids[3, 3] = 1;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);

            Assert.IsFalse(engine.TryFindPath(3, 3, 3, 3, null));
        }

        [Test]
        public void 满盘时_允许绕棋盘外圈连接()
        {
            var ids = new int[Rows, Cols];
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                    ids[r, c] = 100 + r * Cols + c;
            }

            ids[0, 0] = 1;
            ids[0, Cols - 1] = 1;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);

            Assert.IsTrue(engine.TryFindPath(0, 0, 0, Cols - 1, null), "外圈是通路，两个转弯内应能连上");
        }

        [Test]
        public void 四面被墙封死_不可连接()
        {
            var ids = EmptyIds();
            ids[5, 5] = 1;
            ids[0, 0] = 1;

            var blocked = new bool[Rows, Cols];
            blocked[4, 5] = true;
            blocked[6, 5] = true;
            blocked[5, 4] = true;
            blocked[5, 6] = true;

            var engine = new LianliankanEngine();
            engine.Load(ids, blocked);

            Assert.IsFalse(engine.TryFindPath(5, 5, 0, 0, null), "被墙围死的牌不应可连");
        }

        [Test]
        public void Clear_剩余数递减_清空后可连性失效()
        {
            var ids = EmptyIds();
            ids[2, 2] = 7;
            ids[2, 3] = 7;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);
            Assert.AreEqual(2, engine.Remaining);

            engine.Clear(2, 2);
            Assert.AreEqual(1, engine.Remaining);
            Assert.IsFalse(engine.TryFindPath(2, 2, 2, 3, null), "已消除的格子不能再参与配对");
            Assert.IsFalse(engine.Cleared);

            engine.Clear(2, 3);
            Assert.AreEqual(0, engine.Remaining);
            Assert.IsTrue(engine.Cleared);
        }

        [Test]
        public void TryHint_给出的配对_必定可连()
        {
            var ids = EmptyIds();
            ids[0, 0] = 3;
            ids[9, 9] = 3;
            ids[4, 4] = 4;
            ids[4, 5] = 4;

            var engine = new LianliankanEngine();
            engine.Load(ids, null);

            Assert.IsTrue(engine.TryHint(out var r0, out var c0, out var r1, out var c1));
            Assert.IsTrue(engine.TryFindPath(r0, c0, r1, c1, null), "提示必须是真实可连的一对");
        }

        [Test]
        public void Shuffle_LeavesAConnectablePair()
        {
            var ids = EmptyIds();
            var n = 0;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    ids[r, c] = n / 2;
                    n++;
                }
            }

            var engine = new LianliankanEngine();
            engine.Load(ids, null);
            engine.Shuffle(null);
            Assert.IsTrue(engine.HasMove());
        }
    }
}
