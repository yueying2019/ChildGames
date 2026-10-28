using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 对对消：相同图案，通路最多拐两个弯。
    /// </summary>
    public sealed class LianliankanEngine
    {
        public const int Rows = LianliankanGameView.Rows;
        public const int Cols = LianliankanGameView.Columns;

        private static readonly int[] DirR = { -1, 1, 0, 0 };
        private static readonly int[] DirC = { 0, 0, -1, 1 };

        private readonly int[,] _ids = new int[Rows, Cols];
        private readonly bool[,] _exist = new bool[Rows, Cols];
        private readonly bool[,] _blocked = new bool[Rows, Cols];

        public int Remaining { get; private set; }
        public bool Cleared => Remaining <= 0;

        /// <summary>
        /// 纯数据加载：ids[r,c] 小于 0 表示空格，blocked 为不可通行的墙（可传 null）。
        /// 视图层与单元测试共用这一份实现，保证规则一致。
        /// </summary>
        public void Load(int[,] ids, bool[,] blocked)
        {
            Remaining = 0;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    _ids[r, c] = -1;
                    _exist[r, c] = false;
                    _blocked[r, c] = blocked != null && blocked[r, c];
                }
            }

            if (ids == null)
                return;

            var rows = Mathf.Min(Rows, ids.GetLength(0));
            var cols = Mathf.Min(Cols, ids.GetLength(1));
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (_blocked[r, c])
                        continue;
                    var id = ids[r, c];
                    if (id < 0)
                        continue;
                    _ids[r, c] = id;
                    _exist[r, c] = true;
                    Remaining++;
                }
            }
        }

        public void Load(LianliankanTileView[] tiles)
        {
            var ids = new int[Rows, Cols];
            var blocked = new bool[Rows, Cols];
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                    ids[r, c] = -1;
            }

            if (tiles != null)
            {
                for (var i = 0; i < tiles.Length; i++)
                {
                    var tile = tiles[i];
                    if (tile == null)
                        continue;
                    if (tile.Row < 0 || tile.Row >= Rows || tile.Col < 0 || tile.Col >= Cols)
                        continue;
                    if (tile.Blocked)
                    {
                        blocked[tile.Row, tile.Col] = true;
                        continue;
                    }

                    if (!tile.Occupied)
                        continue;
                    ids[tile.Row, tile.Col] = tile.IconId;
                }
            }

            Load(ids, blocked);
        }

        public bool TryLink(int r0, int c0, int r1, int c1)
        {
            return TryFindPath(r0, c0, r1, c1, null);
        }

        public bool TryFindPath(int r0, int c0, int r1, int c1, List<Vector2Int> path)
        {
            if (path != null)
                path.Clear();
            if (!Exists(r0, c0) || !Exists(r1, c1))
                return false;
            if (r0 == r1 && c0 == c1)
                return false;
            if (_ids[r0, c0] < 0 || _ids[r0, c0] != _ids[r1, c1])
                return false;
            return SearchPath(r0, c0, r1, c1, path);
        }

        public void Clear(int r, int c)
        {
            if (!Exists(r, c))
                return;
            _exist[r, c] = false;
            _ids[r, c] = -1;
            Remaining--;
        }

        public void Shuffle(LianliankanTileView[] tiles)
        {
            var ids = new List<int>(Remaining);
            var cells = new List<LianliankanTileView>(Remaining);
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null || !tile.Occupied)
                    continue;
                ids.Add(_ids[tile.Row, tile.Col]);
                cells.Add(tile);
            }

            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var tmp = ids[i];
                ids[i] = ids[j];
                ids[j] = tmp;
            }

            for (var i = 0; i < cells.Count; i++)
                _ids[cells[i].Row, cells[i].Col] = ids[i];
        }

        public bool HasMove()
        {
            return TryHint(out _, out _, out _, out _);
        }

        public bool EnsureHasMove(LianliankanTileView[] tiles, int maxTries = 40)
        {
            for (var i = 0; i < maxTries; i++)
            {
                if (HasMove())
                    return true;
                Shuffle(tiles);
            }

            return HasMove();
        }

        /// <returns>发生了原地重排。</returns>
        public bool RefreshIfStuck(LianliankanTileView[] tiles, int maxTries = 40)
        {
            if (Cleared || Remaining < 2)
                return false;
            if (HasMove())
                return false;
            EnsureHasMove(tiles, maxTries);
            return true;
        }

        public bool TryHint(out int r0, out int c0, out int r1, out int c1)
        {
            return TryHint(out r0, out c0, out r1, out c1, null);
        }

        public bool TryHint(out int r0, out int c0, out int r1, out int c1, List<Vector2Int> path)
        {
            r0 = c0 = r1 = c1 = -1;
            for (var a = 0; a < Rows * Cols; a++)
            {
                var rA = a / Cols;
                var cA = a % Cols;
                    if (!_exist[rA, cA])
                        continue;
                    for (var b = a + 1; b < Rows * Cols; b++)
                    {
                        var rB = b / Cols;
                        var cB = b % Cols;
                        if (!_exist[rB, cB] || _ids[rA, cA] != _ids[rB, cB])
                            continue;
                        if (!SearchPath(rA, cA, rB, cB, path))
                        continue;
                    r0 = rA;
                    c0 = cA;
                    r1 = rB;
                    c1 = cB;
                    return true;
                }
            }

            return false;
        }

        public int Peek(int r, int c) => Exists(r, c) ? _ids[r, c] : -1;

        private bool SearchPath(int sr, int sc, int tr, int tc, List<Vector2Int> path)
        {
            var minR = -1;
            var maxR = Rows;
            var minC = -1;
            var maxC = Cols;
            var h = maxR - minR + 1;
            var w = maxC - minC + 1;
            var best = new int[h, w, 4];
            var prevR = new int[h, w, 4];
            var prevC = new int[h, w, 4];
            var prevD = new int[h, w, 4];
            for (var r = 0; r < h; r++)
            {
                for (var c = 0; c < w; c++)
                {
                    for (var d = 0; d < 4; d++)
                    {
                        best[r, c, d] = 99;
                        prevD[r, c, d] = -1;
                    }
                }
            }

            var queue = new Queue<int>(128);
            void Enqueue(int r, int c, int dir, int turns, int fromR, int fromC, int fromDir)
            {
                var rk = r - minR;
                var ck = c - minC;
                if (turns >= best[rk, ck, dir])
                    return;
                best[rk, ck, dir] = turns;
                prevR[rk, ck, dir] = fromR;
                prevC[rk, ck, dir] = fromC;
                prevD[rk, ck, dir] = fromDir;
                queue.Enqueue(r);
                queue.Enqueue(c);
                queue.Enqueue(dir);
                queue.Enqueue(turns);
            }

            for (var d = 0; d < 4; d++)
                Enqueue(sr, sc, d, 0, sr, sc, -1);

            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                var c = queue.Dequeue();
                var dir = queue.Dequeue();
                var turns = queue.Dequeue();
                for (var nd = 0; nd < 4; nd++)
                {
                    var nt = nd == dir ? turns : turns + 1;
                    if (nt > 2)
                        continue;
                    var nr = r + DirR[nd];
                    var nc = c + DirC[nd];
                    if (nr < minR || nr > maxR || nc < minC || nc > maxC)
                        continue;
                    if (nr == tr && nc == tc)
                    {
                        if (path != null)
                            BuildPath(path, sr, sc, r, c, dir, tr, tc, minR, minC, prevR, prevC, prevD);
                        return true;
                    }

                    if (!IsEmpty(nr, nc))
                        continue;
                    Enqueue(nr, nc, nd, nt, r, c, dir);
                }
            }

            return false;
        }

        private static void BuildPath(
            List<Vector2Int> path,
            int sr, int sc,
            int r, int c, int dir,
            int tr, int tc,
            int minR, int minC,
            int[,,] prevR, int[,,] prevC, int[,,] prevD)
        {
            var stack = new List<Vector2Int>(16);
            stack.Add(new Vector2Int(tc, tr));
            stack.Add(new Vector2Int(c, r));
            while (r != sr || c != sc)
            {
                var rk = r - minR;
                var ck = c - minC;
                var pr = prevR[rk, ck, dir];
                var pc = prevC[rk, ck, dir];
                var pd = prevD[rk, ck, dir];
                if (pd < 0)
                    break;
                r = pr;
                c = pc;
                dir = pd;
                stack.Add(new Vector2Int(c, r));
            }

            path.Clear();
            for (var i = stack.Count - 1; i >= 0; i--)
                path.Add(stack[i]);
        }

        private bool IsEmpty(int r, int c)
        {
            if (r < 0 || c < 0 || r >= Rows || c >= Cols)
                return true;
            if (_blocked[r, c])
                return false;
            return !_exist[r, c];
        }

        private bool Exists(int r, int c) => r >= 0 && c >= 0 && r < Rows && c < Cols && _exist[r, c];
    }
}
