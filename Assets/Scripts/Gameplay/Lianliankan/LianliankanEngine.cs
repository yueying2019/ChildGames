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

        private const int PathH = Rows + 2;
        private const int PathW = Cols + 2;

        private readonly int[,] _ids = new int[Rows, Cols];
        private readonly bool[,] _exist = new bool[Rows, Cols];
        private readonly bool[,] _blocked = new bool[Rows, Cols];
        private readonly int[,,] _best = new int[PathH, PathW, 4];
        private readonly int[,,] _prevR = new int[PathH, PathW, 4];
        private readonly int[,,] _prevC = new int[PathH, PathW, 4];
        private readonly int[,,] _prevD = new int[PathH, PathW, 4];
        private readonly Queue<int> _pathQueue = new Queue<int>(256);
        private readonly List<Vector2Int> _pathStack = new List<Vector2Int>(24);
        private readonly int[] _cellR = new int[Rows * Cols];
        private readonly int[] _cellC = new int[Rows * Cols];
        private readonly int[] _cellId = new int[Rows * Cols];
        private readonly List<int> _restIds = new List<int>(Rows * Cols);

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
            return SearchPath(r0, c0, r1, c1, path, 2);
        }

        public void Clear(int r, int c)
        {
            if (!Exists(r, c))
                return;
            _exist[r, c] = false;
            _ids[r, c] = -1;
            Remaining--;
        }

        /// <summary>
        /// 重排图案。先找一对直线或只拐一次就能连上的空位，把同一种图案放上去，其余再打乱。
        /// 找不到这种空位时，退回到任意合法通路（最多两拐）。
        /// </summary>
        public void Shuffle(LianliankanTileView[] tiles)
        {
            if (tiles == null && Remaining < 0)
                return;
            var count = CollectCells();
            if (count < 2)
                return;

            var anchorA = -1;
            var anchorB = -1;
            if (!FindOpenPair(count, 1, out anchorA, out anchorB))
                FindOpenPair(count, 2, out anchorA, out anchorB);

            var icon = PairIcon(count);
            if (anchorA >= 0 && icon >= 0)
            {
                PlacePair(count, anchorA, anchorB, icon);
                return;
            }

            for (var i = count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var tmp = _cellId[i];
                _cellId[i] = _cellId[j];
                _cellId[j] = tmp;
            }

            WriteCells(count);
        }

        public bool HasMove()
        {
            return TryHint(out _, out _, out _, out _);
        }

        public bool EnsureHasMove(LianliankanTileView[] tiles, int maxTries = 40)
        {
            if (maxTries < 1)
                return HasMove();
            if (HasMove())
                return true;
            Shuffle(tiles);
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
            var count = CollectCells();
            SortCells(count);
            var index = 0;
            while (index < count)
            {
                var end = index + 1;
                while (end < count && _cellId[end] == _cellId[index])
                    end++;
                for (var a = index; a < end; a++)
                {
                    for (var b = a + 1; b < end; b++)
                    {
                        if (!SearchPath(_cellR[a], _cellC[a], _cellR[b], _cellC[b], path, 2))
                            continue;
                        r0 = _cellR[a];
                        c0 = _cellC[a];
                        r1 = _cellR[b];
                        c1 = _cellC[b];
                        return true;
                    }
                }

                index = end;
            }

            return false;
        }

        public int Peek(int r, int c) => Exists(r, c) ? _ids[r, c] : -1;

        private int CollectCells()
        {
            var count = 0;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    if (!_exist[r, c])
                        continue;
                    _cellR[count] = r;
                    _cellC[count] = c;
                    _cellId[count] = _ids[r, c];
                    count++;
                }
            }

            return count;
        }

        private void SortCells(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var id = _cellId[i];
                var r = _cellR[i];
                var c = _cellC[i];
                var j = i - 1;
                while (j >= 0 && _cellId[j] > id)
                {
                    _cellId[j + 1] = _cellId[j];
                    _cellR[j + 1] = _cellR[j];
                    _cellC[j + 1] = _cellC[j];
                    j--;
                }

                _cellId[j + 1] = id;
                _cellR[j + 1] = r;
                _cellC[j + 1] = c;
            }
        }

        private bool FindOpenPair(int count, int maxTurns, out int a, out int b)
        {
            for (var i = 0; i < count; i++)
            {
                if (!ReachOther(i, count, maxTurns, out var other))
                    continue;
                a = i;
                b = other;
                return true;
            }

            a = -1;
            b = -1;
            return false;
        }

        private bool ReachOther(int origin, int count, int maxTurns, out int other)
        {
            const int minR = -1;
            const int minC = -1;
            var sr = _cellR[origin];
            var sc = _cellC[origin];
            for (var r = 0; r < PathH; r++)
            {
                for (var c = 0; c < PathW; c++)
                {
                    for (var d = 0; d < 4; d++)
                        _best[r, c, d] = 99;
                }
            }

            _pathQueue.Clear();
            void Enqueue(int r, int c, int dir, int turns)
            {
                var rk = r - minR;
                var ck = c - minC;
                if (turns >= _best[rk, ck, dir])
                    return;
                _best[rk, ck, dir] = turns;
                _pathQueue.Enqueue(r);
                _pathQueue.Enqueue(c);
                _pathQueue.Enqueue(dir);
                _pathQueue.Enqueue(turns);
            }

            for (var d = 0; d < 4; d++)
                Enqueue(sr, sc, d, 0);

            while (_pathQueue.Count > 0)
            {
                var r = _pathQueue.Dequeue();
                var c = _pathQueue.Dequeue();
                var dir = _pathQueue.Dequeue();
                var turns = _pathQueue.Dequeue();
                for (var nd = 0; nd < 4; nd++)
                {
                    var nt = nd == dir ? turns : turns + 1;
                    if (nt > maxTurns)
                        continue;
                    var nr = r + DirR[nd];
                    var nc = c + DirC[nd];
                    if (nr < minR || nr > Rows || nc < minC || nc > Cols)
                        continue;
                    if (nr == sr && nc == sc)
                        continue;
                    if (Exists(nr, nc))
                    {
                        for (var j = 0; j < count; j++)
                        {
                            if (_cellR[j] != nr || _cellC[j] != nc)
                                continue;
                            other = j;
                            return true;
                        }

                        continue;
                    }

                    if (!IsEmpty(nr, nc))
                        continue;
                    Enqueue(nr, nc, nd, nt);
                }
            }

            other = -1;
            return false;
        }

        private int PairIcon(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var id = _cellId[i];
                if (id < 0)
                    continue;
                var seen = 0;
                for (var j = 0; j < count; j++)
                {
                    if (_cellId[j] == id)
                        seen++;
                }

                if (seen >= 2)
                    return id;
            }

            return -1;
        }

        private void PlacePair(int count, int a, int b, int icon)
        {
            _restIds.Clear();
            var skipped = 0;
            for (var i = 0; i < count; i++)
            {
                if (_cellId[i] == icon && skipped < 2)
                {
                    skipped++;
                    continue;
                }

                _restIds.Add(_cellId[i]);
            }

            for (var i = _restIds.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var tmp = _restIds[i];
                _restIds[i] = _restIds[j];
                _restIds[j] = tmp;
            }

            var cursor = 0;
            for (var i = 0; i < count; i++)
            {
                var id = i == a || i == b ? icon : _restIds[cursor++];
                _ids[_cellR[i], _cellC[i]] = id;
            }
        }

        private void WriteCells(int count)
        {
            for (var i = 0; i < count; i++)
                _ids[_cellR[i], _cellC[i]] = _cellId[i];
        }

        private bool SearchPath(int sr, int sc, int tr, int tc, List<Vector2Int> path, int maxTurns)
        {
            const int minR = -1;
            const int minC = -1;
            var maxR = Rows;
            var maxC = Cols;
            for (var r = 0; r < PathH; r++)
            {
                for (var c = 0; c < PathW; c++)
                {
                    for (var d = 0; d < 4; d++)
                    {
                        _best[r, c, d] = 99;
                        _prevD[r, c, d] = -1;
                    }
                }
            }

            _pathQueue.Clear();
            void Enqueue(int r, int c, int dir, int turns, int fromR, int fromC, int fromDir)
            {
                var rk = r - minR;
                var ck = c - minC;
                if (turns >= _best[rk, ck, dir])
                    return;
                _best[rk, ck, dir] = turns;
                _prevR[rk, ck, dir] = fromR;
                _prevC[rk, ck, dir] = fromC;
                _prevD[rk, ck, dir] = fromDir;
                _pathQueue.Enqueue(r);
                _pathQueue.Enqueue(c);
                _pathQueue.Enqueue(dir);
                _pathQueue.Enqueue(turns);
            }

            for (var d = 0; d < 4; d++)
                Enqueue(sr, sc, d, 0, sr, sc, -1);

            while (_pathQueue.Count > 0)
            {
                var r = _pathQueue.Dequeue();
                var c = _pathQueue.Dequeue();
                var dir = _pathQueue.Dequeue();
                var turns = _pathQueue.Dequeue();
                for (var nd = 0; nd < 4; nd++)
                {
                    var nt = nd == dir ? turns : turns + 1;
                    if (nt > maxTurns)
                        continue;
                    var nr = r + DirR[nd];
                    var nc = c + DirC[nd];
                    if (nr < minR || nr > maxR || nc < minC || nc > maxC)
                        continue;
                    if (nr == tr && nc == tc)
                    {
                        if (path != null)
                            BuildPath(path, sr, sc, r, c, dir, tr, tc);
                        return true;
                    }

                    if (!IsEmpty(nr, nc))
                        continue;
                    Enqueue(nr, nc, nd, nt, r, c, dir);
                }
            }

            return false;
        }

        private void BuildPath(List<Vector2Int> path, int sr, int sc, int r, int c, int dir, int tr, int tc)
        {
            const int minR = -1;
            const int minC = -1;
            _pathStack.Clear();
            _pathStack.Add(new Vector2Int(tc, tr));
            _pathStack.Add(new Vector2Int(c, r));
            while (r != sr || c != sc)
            {
                var rk = r - minR;
                var ck = c - minC;
                var pr = _prevR[rk, ck, dir];
                var pc = _prevC[rk, ck, dir];
                var pd = _prevD[rk, ck, dir];
                if (pd < 0)
                    break;
                r = pr;
                c = pc;
                dir = pd;
                _pathStack.Add(new Vector2Int(c, r));
            }

            path.Clear();
            for (var i = _pathStack.Count - 1; i >= 0; i--)
                path.Add(_pathStack[i]);
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
