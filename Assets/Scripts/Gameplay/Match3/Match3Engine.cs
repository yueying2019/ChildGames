using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 纯逻辑三消引擎：交换、匹配、特效、下落、填充、死局重排。
    /// </summary>
    public sealed class Match3Engine
    {
        private const int MaxShuffleTries = 24;
        private const int MaxFillTries = 24;

        private readonly Random _rng;
        private int _nextId = 1;
        private int _lastSwapX = -1;
        private int _lastSwapY = -1;
        private int _lastSwapDx;
        private int _lastSwapDy;
        private bool[,] _playable;
        private Match3CellKind[,] _kind;
        private int[,] _layers;
        private Match3Tile[,] _tiles;
        private int[,] _horzLen;
        private int[,] _vertLen;
        private bool[,] _overlayHit;
        private bool[,] _remove;
        private bool[,] _matched;
        private bool[,] _seen;
        private readonly HashSet<int> _fired = new HashSet<int>();
        private readonly HashSet<int> _skipFire = new HashSet<int>();
        private readonly Queue<Match3Coord> _blast = new Queue<Match3Coord>();
        private readonly List<MatchGroup> _groups = new List<MatchGroup>(8);
        private readonly List<MatchGroup> _groupPool = new List<MatchGroup>(8);
        private readonly Stack<Match3Coord> _flood = new Stack<Match3Coord>(16);
        private readonly List<(Match3Tile tile, int fromY)> _falling = new List<(Match3Tile tile, int fromY)>(16);
        private readonly List<Match3Coord> _candidates = new List<Match3Coord>(64);
        private readonly List<Match3Tile> _shuffleTiles = new List<Match3Tile>(64);
        private int _bonusMoves;
        private int _bonusCoins;
        private readonly List<Match3Coord> _leftoverSpecials = new List<Match3Coord>();

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int ColorCount { get; private set; }
        public int MovesLeft { get; private set; }
        /// <summary>判定胜利那一刻的剩余步数快照。结算评星必须用它——
        /// 胜利动画会把 MovesLeft 消费成特效并清零，之后读到的恒为 0。</summary>
        public int MovesLeftAtWin { get; private set; }
        public int Score { get; private set; }
        public bool Endless { get; set; }

        public void AddMoves(int amount)
        {
            if (amount <= 0)
                return;
            MovesLeft += amount;
        }

        public void SetMovesLeft(int amount)
        {
            MovesLeft = System.Math.Max(0, amount);
        }

        /// <summary>本局生成的特殊棋子（炸弹 / 直线 / 彩虹）数量，供每日任务统计。</summary>
        public int SpecialsCreated { get; private set; }

        public void ResetScore()
        {
            Score = 0;
            SpecialsCreated = 0;
        }

        public Match3ResolveResult TryRemoveAt(int x, int y)
        {
            var result = new Match3ResolveResult();
            ResetBonuses();
            if (!InBoard(x, y) || _kind[x, y] == Match3CellKind.Hole)
                return result;

            ClearOverlayHits();
            if (IsStone(x, y))
            {
                HitOverlay(x, y, result.Events);
                result.Applied = true;
                Finish(result);
                return result;
            }

            var tile = GetTile(x, y);
            if (tile == null)
                return result;

            HitOverlay(x, y, result.Events);
            var gain = GameBalance.Current.Match3BaseMatchScore;
            Score += gain;
            if (tile.Color >= 1 && tile.Color <= 6)
                Collected[tile.Color]++;
            result.Events.Add(Match3BoardEvent.Remove(tile, x, y, gain));
            _tiles[x, y] = null;
            result.Applied = true;
            ApplyGravity(result.Events);
            Fill(result.Events);
            ResolveStable(result.Events, 1);
            Finish(result);
            return result;
        }

        public Match3ResolveResult TryPaintSpecial(int x, int y, Match3Special special)
        {
            var result = new Match3ResolveResult();
            if (special != Match3Special.LineH && special != Match3Special.LineV
                && special != Match3Special.Bomb && special != Match3Special.ColorBomb)
                return result;
            if (!CanSwapAt(x, y))
                return result;

            var tile = GetTile(x, y);
            if (tile == null || tile.Special == Match3Special.ColorBomb || tile.Drop != Match3Drop.None)
                return result;

            tile.Special = special;
            result.Applied = true;
            result.Events.Add(Match3BoardEvent.SpawnSpecial(tile, x, y));
            Finish(result);
            return result;
        }

        /// <summary>
        /// 同色全消：点一颗棋子，清掉全场同色并把被牵连的特效一起引爆。
        /// 不消耗步数（道具与 TryRemoveAt / TryPaintSpecial 保持一致）。
        /// </summary>
        public Match3ResolveResult TryClearColorAt(int x, int y)
        {
            var result = new Match3ResolveResult();
            ResetBonuses();
            if (!InBoard(x, y))
                return result;

            var tile = GetTile(x, y);
            if (tile == null || tile.Color <= 0)
                return result;

            var color = tile.Color;
            BeginBlast();
            ClearOverlayHits();
            result.Events.Add(Match3BoardEvent.Blast(Match3Special.ColorBomb, x, y, color));
            ClearColor(color, _remove, _fired, _blast, result.Events);
            FlushDetonation(_remove, _fired, _blast, result.Events, null);
            ApplyGravity(result.Events);
            Fill(result.Events);
            ResolveStable(result.Events, 1);
            result.Applied = true;
            Finish(result);
            return result;
        }

        /// <summary>
        /// 关卡过关后：把剩余步数 1:1 打成普通棋子上的横/竖/炸弹特效。
        /// </summary>
        public Match3ResolveResult ConvertLeftoverMoves()
        {
            var result = new Match3ResolveResult();
            _leftoverSpecials.Clear();
            if (Endless || MovesLeft <= 0)
                return result;

            _candidates.Clear();
            CollectLeftoverCandidates(_candidates);
            ShuffleCoords(_candidates);
            var n = Math.Min(MovesLeft, _candidates.Count);
            for (var i = 0; i < n; i++)
            {
                var pos = _candidates[i];
                var tile = GetTile(pos.X, pos.Y);
                if (tile == null)
                    continue;
                tile.Special = RandomLeftoverSpecial();
                _leftoverSpecials.Add(pos);
                result.Events.Add(Match3BoardEvent.SpawnSpecial(tile, pos.X, pos.Y));
                result.Applied = true;
            }

            return result;
        }

        /// <summary>
        /// 引爆剩余步转换出的特效，并结算连锁。
        /// </summary>
        public Match3ResolveResult DetonateLeftoverSpecials()
        {
            var result = new Match3ResolveResult();
            ResetBonuses();
            if (_leftoverSpecials.Count == 0)
                return result;

            BeginBlast();
            ClearOverlayHits();
            for (var i = 0; i < _leftoverSpecials.Count; i++)
            {
                var pos = _leftoverSpecials[i];
                var tile = GetTile(pos.X, pos.Y);
                if (tile == null || tile.Special == Match3Special.None)
                    continue;
                MarkRemove(pos.X, pos.Y, _remove, _fired, _blast, result.Events);
            }

            FlushDetonation(_remove, _fired, _blast, result.Events, null);
            ApplyGravity(result.Events);
            Fill(result.Events);
            ResolveStable(result.Events, 1, false);
            _leftoverSpecials.Clear();
            result.Applied = result.Events.Count > 0;
            result.BonusMoves = _bonusMoves;
            result.BonusCoins = _bonusCoins;
            return result;
        }

        public Match3LevelData Level { get; private set; }
        public int[] Collected { get; private set; } = new int[7];

        public Match3Engine(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public bool IsPlayable(int x, int y) => InBoard(x, y) && _playable[x, y];

        public bool HasSlot(int x, int y) => InBoard(x, y) && _kind[x, y] != Match3CellKind.Hole;

        public Match3CellKind GetKind(int x, int y) => InBoard(x, y) ? _kind[x, y] : Match3CellKind.Hole;

        public int GetLayers(int x, int y) => InBoard(x, y) ? _layers[x, y] : 0;

        public Match3Tile GetTile(int x, int y) => IsPlayable(x, y) ? _tiles[x, y] : null;

        public bool CanSwapAt(int x, int y) => GetTile(x, y) != null && !IsLocked(x, y);

        public int CountRemainingOverlays()
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (Match3CellUtil.IsOverlay(_kind[x, y]) && _layers[x, y] > 0)
                        count++;
                }
            }

            return count;
        }

        public int CountRemainingKind(Match3CellKind kind)
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (_kind[x, y] == kind && _layers[x, y] > 0)
                        count++;
                }
            }

            return count;
        }

        public void Load(Match3LevelData level)
        {
            Level = level != null ? level : Match3LevelData.CreateDefault();
            Level.EnsureInitialized();
            Width = Level.Width;
            Height = Level.Height;
            ColorCount = Level.ColorCount;
            MovesLeft = Level.MoveLimit;
            Score = 0;
            Collected = new int[7];
            _playable = new bool[Width, Height];
            _kind = new Match3CellKind[Width, Height];
            _layers = new int[Width, Height];
            _tiles = new Match3Tile[Width, Height];
            _horzLen = new int[Width, Height];
            _vertLen = new int[Width, Height];
            _overlayHit = new bool[Width, Height];
            _remove = new bool[Width, Height];
            _matched = new bool[Width, Height];
            _seen = new bool[Width, Height];
            _leftoverSpecials.Clear();
            _nextId = 1;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var spec = Level.GetCell(x, y);
                    _kind[x, y] = spec.Kind;
                    _layers[x, y] = spec.ResolvedLayers;
                    _playable[x, y] = Match3CellUtil.CanHoldItem(spec.Kind);
                    if (!_playable[x, y])
                        continue;

                    var color = spec.Color >= 1 && spec.Color <= ColorCount
                        ? spec.Color
                        : PickColorAvoidingMatch(x, y);
                    _tiles[x, y] = CreateTile(color, Match3Special.None);
                }
            }

            SettleOpening();
            if (!HasPossibleMove())
                ShuffleInternal();
            if (!Level.FixedBoard)
                PlaceHiddenDrops();
            if (!HasPossibleMove())
                ShuffleInternal();
        }

        public Match3ResolveResult TrySwap(int x0, int y0, int x1, int y1)
        {
            var result = new Match3ResolveResult();
            ResetBonuses();
            if (!AreAdjacent(x0, y0, x1, y1))
                return result;

            var a = GetTile(x0, y0);
            var b = GetTile(x1, y1);
            if (a == null || b == null || IsLocked(x0, y0) || IsLocked(x1, y1))
                return result;

            _lastSwapX = x1;
            _lastSwapY = y1;
            _lastSwapDx = x1 - x0;
            _lastSwapDy = y1 - y0;
            SwapTiles(x0, y0, x1, y1);

            if (TriggersWithoutMatch(a, b))
            {
                MovesLeft = Math.Max(0, MovesLeft - 1);
                result.Applied = true;
                result.Events.Add(Match3BoardEvent.Swap(a.Id, x0, y0, b.Id, x1, y1));
                DetonateSpecialCombo(x0, y0, x1, y1, result.Events);
                ApplyGravity(result.Events);
                Fill(result.Events);
                ResolveStable(result.Events, 1);
                Finish(result);
                return result;
            }

            var groups = FindMatchGroups();
            if (groups.Count == 0)
            {
                SwapTiles(x0, y0, x1, y1);
                result.Reverted = true;
                result.Events.Add(Match3BoardEvent.Revert(a.Id, x0, y0, b.Id, x1, y1));
                return result;
            }

            MovesLeft = Math.Max(0, MovesLeft - 1);
            result.Applied = true;
            result.Events.Add(Match3BoardEvent.Swap(a.Id, x0, y0, b.Id, x1, y1));
            ResolveFromGroups(groups, result.Events, 1);
            Finish(result);
            return result;
        }

        public bool TryGetHint(out Match3Coord a, out Match3Coord b)
        {
            return FindFirstMove(out a, out b);
        }

        public Match3ResolveResult Shuffle()
        {
            var result = new Match3ResolveResult();
            ResetBonuses();
            if (ShuffleInternal())
            {
                result.Applied = true;
                result.Events.Add(new Match3BoardEvent { Kind = Match3EventKind.Shuffle });
            }

            Finish(result);
            return result;
        }

        public bool GoalMet()
        {
            if (Level == null)
                return false;

            if (Level.GoalType == Match3GoalType.Score)
                return Score >= Level.TargetScore;
            if (Level.GoalType == Match3GoalType.ClearCells)
                return CountRemainingOverlays() <= 0;

            for (var color = 1; color <= ColorCount; color++)
            {
                var need = Level.CollectCounts != null && color < Level.CollectCounts.Length
                    ? Level.CollectCounts[color]
                    : 0;
                if (need > 0 && Collected[color] < need)
                    return false;
            }

            return HasAnyCollectTarget();
        }

        public string GoalLabel()
        {
            if (Level == null)
                return string.Empty;

            if (Level.GoalType == Match3GoalType.Score)
                return $"分数 {Score}/{Level.TargetScore}";
            if (Level.GoalType == Match3GoalType.ClearCells)
            {
                var parts = new List<string>(4);
                AddOverlayGoal(parts, Match3CellKind.Ice);
                AddOverlayGoal(parts, Match3CellKind.Stone);
                AddOverlayGoal(parts, Match3CellKind.Jelly);
                AddOverlayGoal(parts, Match3CellKind.Cage);
                return parts.Count == 0 ? "障碍已清" : string.Join("  ", parts);
            }

            var collect = new List<string>(4);
            for (var color = 1; color <= ColorCount; color++)
            {
                var need = Level.CollectCounts != null && color < Level.CollectCounts.Length
                    ? Level.CollectCounts[color]
                    : 0;
                if (need <= 0)
                    continue;
                collect.Add($"{Match3Icons.TileName(color)} {Math.Min(Collected[color], need)}/{need}");
            }

            return collect.Count == 0 ? "收集目标未配置" : string.Join("  ", collect);
        }

        private void AddOverlayGoal(List<string> parts, Match3CellKind kind)
        {
            var n = CountRemainingKind(kind);
            if (n > 0)
                parts.Add($"{Match3CellUtil.Label(kind)} {n}");
        }

        private void Finish(Match3ResolveResult result)
        {
            result.BonusMoves = _bonusMoves;
            result.BonusCoins = _bonusCoins;
            if (GoalMet())
            {
                if (Endless)
                    result.WaveCleared = true;
                else
                {
                    result.Won = true;
                    MovesLeftAtWin = MovesLeft;
                }
            }
            else if (result.Applied && MovesLeft <= 0)
                result.Lost = true;
        }

        private void ResetBonuses()
        {
            _bonusMoves = 0;
            _bonusCoins = 0;
        }

        private void SettleOpening()
        {
            var events = new List<Match3BoardEvent>(16);
            var cascade = 0;
            while (cascade < 12)
            {
                var groups = FindMatchGroups();
                if (groups.Count == 0)
                    break;
                ResolveFromGroups(groups, events, 0);
                cascade++;
            }

            events.Clear();
            Score = 0;
            Array.Clear(Collected, 0, Collected.Length);
            RestoreOverlaysFromLevel();
        }

        private void RestoreOverlaysFromLevel()
        {
            if (Level == null)
                return;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var spec = Level.GetCell(x, y);
                    _kind[x, y] = spec.Kind;
                    _layers[x, y] = spec.ResolvedLayers;
                    _playable[x, y] = Match3CellUtil.CanHoldItem(spec.Kind);
                }
            }
        }

        private void ResolveFromGroups(List<MatchGroup> groups, List<Match3BoardEvent> events, int cascade)
        {
            RemoveGroups(groups, events, cascade);
            ApplyGravity(events);
            Fill(events);
            ResolveStable(events, cascade + 1);
        }

        private void ResolveStable(List<Match3BoardEvent> events, int cascade, bool shuffleIfDead = true)
        {
            while (cascade < 16)
            {
                var groups = FindMatchGroups();
                if (groups.Count == 0)
                    break;
                RemoveGroups(groups, events, cascade);
                ApplyGravity(events);
                Fill(events);
                cascade++;
            }

            if (!shuffleIfDead || HasPossibleMove())
                return;
            if (ShuffleInternal())
                events.Add(new Match3BoardEvent { Kind = Match3EventKind.Shuffle, ScoreDelta = 1 });
        }

        private void CollectLeftoverCandidates(List<Match3Coord> cells)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!CanSwapAt(x, y))
                        continue;
                    var tile = GetTile(x, y);
                    if (tile == null || tile.Special != Match3Special.None || tile.Drop != Match3Drop.None)
                        continue;
                    cells.Add(new Match3Coord(x, y));
                }
            }
        }

        private void ShuffleCoords(List<Match3Coord> cells)
        {
            for (var i = cells.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(i + 1);
                var tmp = cells[i];
                cells[i] = cells[j];
                cells[j] = tmp;
            }
        }

        private Match3Special RandomLeftoverSpecial()
        {
            switch (_rng.Next(3))
            {
                case 0: return Match3Special.LineH;
                case 1: return Match3Special.LineV;
                default: return Match3Special.Bomb;
            }
        }

        private void RemoveGroups(List<MatchGroup> groups, List<Match3BoardEvent> events, int cascade)
        {
            BeginBlast();
            ClearOverlayHits();

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                for (var c = 0; c < group.Cells.Count; c++)
                    MarkRemove(group.Cells[c].X, group.Cells[c].Y, _remove, _fired, _blast, events);
            }

            while (_blast.Count > 0)
            {
                var pos = _blast.Dequeue();
                var tile = GetTile(pos.X, pos.Y);
                if (tile == null)
                    continue;
                FireSpecial(tile, pos.X, pos.Y, _remove, _fired, _blast, events);
            }

            HitNeighborsOf(_remove, events);

            var multiplier = Math.Max(1, cascade);
            ExtractRemoved(_remove, GameBalance.Current.Match3BaseMatchScore * multiplier, events, true);

            if (cascade <= 0)
                return;

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group.Reward == Match3Special.None)
                    continue;
                var at = group.RewardAt;
                if (!IsPlayable(at.X, at.Y) || _tiles[at.X, at.Y] != null)
                    at = FindEmptyInGroup(group);
                if (!IsPlayable(at.X, at.Y) || _tiles[at.X, at.Y] != null)
                    continue;

                var color = group.Reward == Match3Special.ColorBomb ? 0 : group.Color;
                var special = CreateTile(color, group.Reward);
                _tiles[at.X, at.Y] = special;
                events.Add(Match3BoardEvent.SpawnSpecial(special, at.X, at.Y));
            }
        }

        private void MarkRemove(int x, int y, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            if (!InBoard(x, y) || _kind[x, y] == Match3CellKind.Hole || remove[x, y])
                return;

            if (IsStone(x, y))
            {
                HitOverlay(x, y, events);
                return;
            }

            if (!IsPlayable(x, y))
                return;

            if (IsLocked(x, y))
            {
                HitOverlay(x, y, events);
                if (IsLocked(x, y))
                    return;
            }

            if (_kind[x, y] == Match3CellKind.Jelly)
                HitOverlay(x, y, events);

            remove[x, y] = true;
            var tile = _tiles[x, y];
            if (tile != null && tile.Special != Match3Special.None && fired.Add(tile.Id))
                queue.Enqueue(new Match3Coord(x, y));
        }

        private void FireSpecial(Match3Tile tile, int x, int y, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            var blastColor = tile.Color;
            if (tile.Special == Match3Special.ColorBomb)
                blastColor = tile.Color > 0 ? tile.Color : MostCommonColor();
            events.Add(Match3BoardEvent.Blast(tile.Special, x, y, blastColor, tile.Special == Match3Special.Bomb ? 1 : 0, tile.Id));

            switch (tile.Special)
            {
                case Match3Special.LineH:
                    for (var cx = 0; cx < Width; cx++)
                        MarkRemove(cx, y, remove, fired, queue, events);
                    break;
                case Match3Special.LineV:
                    for (var cy = 0; cy < Height; cy++)
                        MarkRemove(x, cy, remove, fired, queue, events);
                    break;
                case Match3Special.Bomb:
                    for (var cy = y - 1; cy <= y + 1; cy++)
                    {
                        for (var cx = x - 1; cx <= x + 1; cx++)
                            MarkRemove(cx, cy, remove, fired, queue, events);
                    }
                    break;
                case Match3Special.ColorBomb:
                    ClearColor(blastColor, remove, fired, queue, events);
                    break;
            }
        }

        private static bool IsLineSpecial(Match3Special special)
        {
            return special == Match3Special.LineH || special == Match3Special.LineV;
        }

        private static bool IsSpecialCombo(Match3Tile a, Match3Tile b)
        {
            return a != null && b != null && a.Special != Match3Special.None && b.Special != Match3Special.None;
        }

        private static bool TriggersWithoutMatch(Match3Tile a, Match3Tile b)
        {
            if (a == null || b == null)
                return false;
            return a.Special == Match3Special.ColorBomb
                || b.Special == Match3Special.ColorBomb
                || IsSpecialCombo(a, b)
                || IsSwapActivable(a.Special)
                || IsSwapActivable(b.Special);
        }

        private static bool IsSwapActivable(Match3Special special)
        {
            return special == Match3Special.LineH
                || special == Match3Special.LineV
                || special == Match3Special.Bomb;
        }

        private void DetonateSpecialCombo(int x0, int y0, int x1, int y1, List<Match3BoardEvent> events)
        {
            var a = GetTile(x0, y0);
            var b = GetTile(x1, y1);
            BeginBlast();
            ClearOverlayHits();

            var colorBomb = (a != null && a.Special == Match3Special.ColorBomb)
                || (b != null && b.Special == Match3Special.ColorBomb);
            if (colorBomb || IsSpecialCombo(a, b))
            {
                _skipFire.Clear();
                if (a != null)
                {
                    _skipFire.Add(a.Id);
                    _fired.Add(a.Id);
                }

                if (b != null)
                {
                    _skipFire.Add(b.Id);
                    _fired.Add(b.Id);
                }

                MarkRemove(x0, y0, _remove, _fired, _blast, events);
                MarkRemove(x1, y1, _remove, _fired, _blast, events);
                ApplySpecialCombo(a, b, x0, y0, x1, y1, _remove, _fired, _blast, events);
                FlushDetonation(_remove, _fired, _blast, events, _skipFire);
                return;
            }

            if (a != null && IsSwapActivable(a.Special))
                MarkRemove(x0, y0, _remove, _fired, _blast, events);
            if (b != null && IsSwapActivable(b.Special))
                MarkRemove(x1, y1, _remove, _fired, _blast, events);
            FlushDetonation(_remove, _fired, _blast, events, null);
        }

        private void ApplySpecialCombo(
            Match3Tile a,
            Match3Tile b,
            int x0, int y0,
            int x1, int y1,
            bool[,] remove,
            HashSet<int> fired,
            Queue<Match3Coord> queue,
            List<Match3BoardEvent> events)
        {
            var sa = a != null ? a.Special : Match3Special.None;
            var sb = b != null ? b.Special : Match3Special.None;

            if (sa == Match3Special.ColorBomb || sb == Match3Special.ColorBomb)
            {
                ApplyColorBombCombo(a, b, x0, y0, remove, fired, queue, events);
                return;
            }

            if (IsLineSpecial(sa) && IsLineSpecial(sb))
            {
                events.Add(Match3BoardEvent.Blast(Match3Special.LineH, x0, y0));
                events.Add(Match3BoardEvent.Blast(Match3Special.LineV, x0, y0));
                if (y1 != y0)
                    events.Add(Match3BoardEvent.Blast(Match3Special.LineH, x1, y1));
                if (x1 != x0)
                    events.Add(Match3BoardEvent.Blast(Match3Special.LineV, x1, y1));
                ClearRow(y0, remove, fired, queue, events);
                ClearCol(x0, remove, fired, queue, events);
                ClearRow(y1, remove, fired, queue, events);
                ClearCol(x1, remove, fired, queue, events);
                return;
            }

            if ((IsLineSpecial(sa) && sb == Match3Special.Bomb) || (sa == Match3Special.Bomb && IsLineSpecial(sb)))
            {
                var line = IsLineSpecial(sa) ? sa : sb;
                if (line == Match3Special.LineH)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        events.Add(Match3BoardEvent.Blast(Match3Special.LineH, x0, y0 + dy));
                        ClearRow(y0 + dy, remove, fired, queue, events);
                        ClearRow(y1 + dy, remove, fired, queue, events);
                    }
                }
                else
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        events.Add(Match3BoardEvent.Blast(Match3Special.LineV, x0 + dx, y0));
                        ClearCol(x0 + dx, remove, fired, queue, events);
                        ClearCol(x1 + dx, remove, fired, queue, events);
                    }
                }

                return;
            }

            if (sa == Match3Special.Bomb && sb == Match3Special.Bomb)
            {
                events.Add(Match3BoardEvent.Blast(Match3Special.Bomb, x0, y0, 0, 2));
                events.Add(Match3BoardEvent.Blast(Match3Special.Bomb, x1, y1, 0, 2));
                ClearSquare(x0, y0, 2, remove, fired, queue, events);
                ClearSquare(x1, y1, 2, remove, fired, queue, events);
            }
        }

        private void ApplyColorBombCombo(
            Match3Tile a,
            Match3Tile b,
            int x0, int y0,
            bool[,] remove,
            HashSet<int> fired,
            Queue<Match3Coord> queue,
            List<Match3BoardEvent> events)
        {
            var other = a != null && a.Special == Match3Special.ColorBomb ? b : a;
            if (a != null && a.Special == Match3Special.ColorBomb && b != null && b.Special == Match3Special.ColorBomb)
            {
                events.Add(Match3BoardEvent.Blast(Match3Special.ColorBomb, x0, y0, 0));
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                        MarkRemove(x, y, remove, fired, queue, events);
                }

                return;
            }

            if (other == null)
            {
                var color = MostCommonColor();
                events.Add(Match3BoardEvent.Blast(Match3Special.ColorBomb, x0, y0, color));
                ClearColor(color, remove, fired, queue, events);
                return;
            }

            if (other.Special != Match3Special.None && other.Special != Match3Special.ColorBomb)
            {
                events.Add(Match3BoardEvent.Blast(Match3Special.ColorBomb, x0, y0, other.Color));
                ConvertColorToSpecial(other.Color, other.Special, remove, fired, queue, events);
                return;
            }

            events.Add(Match3BoardEvent.Blast(Match3Special.ColorBomb, x0, y0, other.Color > 0 ? other.Color : MostCommonColor()));

            ClearColor(other.Color > 0 ? other.Color : MostCommonColor(), remove, fired, queue, events);
        }

        private void ConvertColorToSpecial(
            int color,
            Match3Special special,
            bool[,] remove,
            HashSet<int> fired,
            Queue<Match3Coord> queue,
            List<Match3BoardEvent> events)
        {
            if (color <= 0)
                color = MostCommonColor();
            if (color <= 0)
                return;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var tile = GetTile(x, y);
                    if (tile == null || tile.Drop != Match3Drop.None || tile.Color != color || tile.Special == Match3Special.ColorBomb)
                        continue;
                    tile.Special = special;
                    MarkRemove(x, y, remove, fired, queue, events);
                }
            }
        }

        private void ClearRow(int y, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            if (y < 0 || y >= Height)
                return;
            for (var x = 0; x < Width; x++)
                MarkRemove(x, y, remove, fired, queue, events);
        }

        private void ClearCol(int x, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            if (x < 0 || x >= Width)
                return;
            for (var y = 0; y < Height; y++)
                MarkRemove(x, y, remove, fired, queue, events);
        }

        private void ClearSquare(int x, int y, int radius, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            for (var cy = y - radius; cy <= y + radius; cy++)
            {
                for (var cx = x - radius; cx <= x + radius; cx++)
                    MarkRemove(cx, cy, remove, fired, queue, events);
            }
        }

        private void FlushDetonation(
            bool[,] remove,
            HashSet<int> fired,
            Queue<Match3Coord> queue,
            List<Match3BoardEvent> events,
            HashSet<int> skipFire)
        {
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                var tile = GetTile(pos.X, pos.Y);
                if (tile == null)
                    continue;
                if (skipFire != null && skipFire.Contains(tile.Id))
                    continue;
                if (tile.Special != Match3Special.None)
                    FireSpecial(tile, pos.X, pos.Y, remove, fired, queue, events);
            }

            HitNeighborsOf(remove, events);
            ExtractRemoved(remove, GameBalance.Current.Match3BaseMatchScore, events, true);
        }

        private void ClearColor(int color, bool[,] remove, HashSet<int> fired, Queue<Match3Coord> queue, List<Match3BoardEvent> events)
        {
            if (color <= 0)
                return;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var tile = GetTile(x, y);
                    if (tile != null && tile.Drop == Match3Drop.None && tile.Color == color)
                        MarkRemove(x, y, remove, fired, queue, events);
                }
            }
        }

        private void ApplyGravity(List<Match3BoardEvent> events)
        {
            for (var x = 0; x < Width; x++)
            {
                var y = Height - 1;
                while (y >= 0)
                {
                    if (!CanReceiveFall(x, y))
                    {
                        y--;
                        continue;
                    }

                    var bottom = y;
                    var top = y;
                    while (top > 0 && CanReceiveFall(x, top - 1))
                        top--;

                    _falling.Clear();
                    for (var sy = bottom; sy >= top; sy--)
                    {
                        if (_tiles[x, sy] == null)
                            continue;
                        _falling.Add((_tiles[x, sy], sy));
                        _tiles[x, sy] = null;
                    }

                    var write = bottom;
                    for (var i = 0; i < _falling.Count; i++)
                    {
                        var (tile, fromY) = _falling[i];
                        _tiles[x, write] = tile;
                        if (fromY != write)
                            events.Add(Match3BoardEvent.Move(tile, x, fromY, x, write));
                        write--;
                    }

                    y = top - 1;
                }
            }
        }

        private void Fill(List<Match3BoardEvent> events)
        {
            for (var x = 0; x < Width; x++)
            {
                var spawn = 0;
                for (var y = Height - 1; y >= 0; y--)
                {
                    if (!CanReceiveFall(x, y) || _tiles[x, y] != null)
                        continue;
                    var tile = CreateTile(PickColorAvoidingMatch(x, y), Match3Special.None);
                    _tiles[x, y] = tile;
                    events.Add(Match3BoardEvent.Spawn(tile, x, -1 - spawn, x, y));
                    spawn++;
                }
            }
        }

        private List<MatchGroup> FindMatchGroups()
        {
            Array.Clear(_matched, 0, _matched.Length);
            Array.Clear(_horzLen, 0, _horzLen.Length);
            Array.Clear(_vertLen, 0, _vertLen.Length);

            for (var y = 0; y < Height; y++)
            {
                var x = 0;
                while (x < Width)
                {
                    var color = ColorAt(x, y);
                    if (color <= 0)
                    {
                        x++;
                        continue;
                    }

                    var end = x + 1;
                    while (end < Width && ColorAt(end, y) == color)
                        end++;
                    var len = end - x;
                    if (len >= 3)
                    {
                        for (var i = x; i < end; i++)
                        {
                            _matched[i, y] = true;
                            _horzLen[i, y] = len;
                        }
                    }

                    x = end;
                }
            }

            for (var x = 0; x < Width; x++)
            {
                var y = 0;
                while (y < Height)
                {
                    var color = ColorAt(x, y);
                    if (color <= 0)
                    {
                        y++;
                        continue;
                    }

                    var end = y + 1;
                    while (end < Height && ColorAt(x, end) == color)
                        end++;
                    var len = end - y;
                    if (len >= 3)
                    {
                        for (var i = y; i < end; i++)
                        {
                            _matched[x, i] = true;
                            _vertLen[x, i] = len;
                        }
                    }

                    y = end;
                }
            }

            _groups.Clear();
            Array.Clear(_seen, 0, _seen.Length);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!_matched[x, y] || _seen[x, y])
                        continue;
                    _groups.Add(BuildGroup(x, y));
                }
            }

            return _groups;
        }

        private MatchGroup BuildGroup(int startX, int startY)
        {
            var color = ColorAt(startX, startY);
            var group = RentGroup();
            group.Color = color;
            _flood.Clear();
            _flood.Push(new Match3Coord(startX, startY));
            _seen[startX, startY] = true;

            var maxLine = 0;
            var lineDir = 0;
            var hasLt = false;
            var hasSwap = false;
            var swapAt = new Match3Coord(startX, startY);

            while (_flood.Count > 0)
            {
                var p = _flood.Pop();
                group.Cells.Add(p);
                if (p.X == _lastSwapX && p.Y == _lastSwapY)
                {
                    hasSwap = true;
                    swapAt = p;
                }

                if (_horzLen[p.X, p.Y] >= 3 && _vertLen[p.X, p.Y] >= 3)
                    hasLt = true;
                if (_horzLen[p.X, p.Y] > maxLine)
                {
                    maxLine = _horzLen[p.X, p.Y];
                    lineDir = 1;
                }

                if (_vertLen[p.X, p.Y] > maxLine)
                {
                    maxLine = _vertLen[p.X, p.Y];
                    lineDir = 2;
                }

                TryPush(p.X + 1, p.Y, color, _matched, _seen, _flood);
                TryPush(p.X - 1, p.Y, color, _matched, _seen, _flood);
                TryPush(p.X, p.Y + 1, color, _matched, _seen, _flood);
                TryPush(p.X, p.Y - 1, color, _matched, _seen, _flood);
            }

            if (maxLine >= 5)
                group.Reward = Match3Special.ColorBomb;
            else if (hasLt)
                group.Reward = Match3Special.Bomb;
            else if (maxLine >= 4)
                group.Reward = LineRewardFromSwap(lineDir);

            if (group.Reward != Match3Special.None)
                SpecialsCreated++;

            group.RewardAt = hasSwap ? swapAt : group.Cells[group.Cells.Count / 2];
            return group;
        }

        private Match3Special LineRewardFromSwap(int lineDir)
        {
            if (_lastSwapDx != 0 || _lastSwapDy != 0)
                return Math.Abs(_lastSwapDx) >= Math.Abs(_lastSwapDy)
                    ? Match3Special.LineH
                    : Match3Special.LineV;
            return lineDir == 2 ? Match3Special.LineV : Match3Special.LineH;
        }

        private void BeginBlast()
        {
            if (_remove != null)
                Array.Clear(_remove, 0, _remove.Length);
            _fired.Clear();
            _blast.Clear();
        }

        private MatchGroup RentGroup()
        {
            MatchGroup group;
            if (_groupPool.Count > _groups.Count)
            {
                group = _groupPool[_groups.Count];
            }
            else
            {
                group = new MatchGroup();
                _groupPool.Add(group);
            }

            group.Reward = Match3Special.None;
            group.Cells.Clear();
            return group;
        }

        private void TryPush(int x, int y, int color, bool[,] matched, bool[,] seen, Stack<Match3Coord> stack)
        {
            if (!InBoard(x, y) || seen[x, y] || !matched[x, y] || ColorAt(x, y) != color)
                return;
            seen[x, y] = true;
            stack.Push(new Match3Coord(x, y));
        }

        private Match3Coord FindEmptyInGroup(MatchGroup group)
        {
            for (var i = 0; i < group.Cells.Count; i++)
            {
                var p = group.Cells[i];
                if (IsPlayable(p.X, p.Y) && _tiles[p.X, p.Y] == null)
                    return p;
            }

            return group.RewardAt;
        }

        private bool HasPossibleMove()
        {
            return FindFirstMove(out _, out _);
        }

        private bool FindFirstMove(out Match3Coord a, out Match3Coord b)
        {
            a = default;
            b = default;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var tile = GetTile(x, y);
                    if (tile == null)
                        continue;
                    if (IsLocked(x, y))
                        continue;
                    if (tile.Special != Match3Special.None)
                    {
                        var anyNeighbor = tile.Special == Match3Special.ColorBomb || IsSwapActivable(tile.Special);
                        if (TrySpecialNeighbor(x, y, 1, 0, anyNeighbor, out a, out b)
                            || TrySpecialNeighbor(x, y, 0, 1, anyNeighbor, out a, out b))
                            return true;
                    }

                    if (WouldMatchAfterSwap(x, y, x + 1, y))
                    {
                        a = new Match3Coord(x, y);
                        b = new Match3Coord(x + 1, y);
                        return true;
                    }

                    if (WouldMatchAfterSwap(x, y, x, y + 1))
                    {
                        a = new Match3Coord(x, y);
                        b = new Match3Coord(x, y + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TrySpecialNeighbor(int x, int y, int dx, int dy, bool anyNeighbor, out Match3Coord a, out Match3Coord b)
        {
            a = new Match3Coord(x, y);
            b = new Match3Coord(x + dx, y + dy);
            if (!CanSwapAt(b.X, b.Y))
                return false;
            if (anyNeighbor)
                return true;
            var other = GetTile(b.X, b.Y);
            return other != null && other.Special != Match3Special.None;
        }

        private bool WouldMatchAfterSwap(int x0, int y0, int x1, int y1)
        {
            if (!CanSwapAt(x0, y0) || !CanSwapAt(x1, y1))
                return false;
            var left = GetTile(x0, y0);
            var right = GetTile(x1, y1);
            if (TriggersWithoutMatch(left, right))
                return true;
            SwapTiles(x0, y0, x1, y1);
            var matched = FindMatchGroups().Count > 0;
            SwapTiles(x0, y0, x1, y1);
            return matched;
        }

        private bool ShuffleInternal()
        {
            _shuffleTiles.Clear();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (_tiles[x, y] != null && !IsLocked(x, y))
                        _shuffleTiles.Add(_tiles[x, y]);
                }
            }

            if (_shuffleTiles.Count < 3)
                return false;

            for (var tryIndex = 0; tryIndex < MaxShuffleTries; tryIndex++)
            {
                for (var i = _shuffleTiles.Count - 1; i > 0; i--)
                {
                    var j = _rng.Next(i + 1);
                    var tmp = _shuffleTiles[i];
                    _shuffleTiles[i] = _shuffleTiles[j];
                    _shuffleTiles[j] = tmp;
                }

                var n = 0;
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        if (_tiles[x, y] != null && !IsLocked(x, y))
                            _tiles[x, y] = _shuffleTiles[n++];
                    }
                }

                if (FindMatchGroups().Count == 0 && HasPossibleMove())
                    return true;
            }

            return true;
        }

        private int PickColorAvoidingMatch(int x, int y)
        {
            var color = _rng.Next(1, ColorCount + 1);
            for (var i = 0; i < MaxFillTries; i++)
            {
                if (!WouldCreateMatch(x, y, color))
                    return color;
                color = _rng.Next(1, ColorCount + 1);
            }

            return color;
        }

        private bool WouldCreateMatch(int x, int y, int color)
        {
            if (Same(x - 1, y, color) && Same(x - 2, y, color))
                return true;
            if (Same(x + 1, y, color) && Same(x + 2, y, color))
                return true;
            if (Same(x - 1, y, color) && Same(x + 1, y, color))
                return true;
            if (Same(x, y - 1, color) && Same(x, y - 2, color))
                return true;
            if (Same(x, y + 1, color) && Same(x, y + 2, color))
                return true;
            if (Same(x, y - 1, color) && Same(x, y + 1, color))
                return true;
            return false;
        }

        private bool Same(int x, int y, int color) => ColorAt(x, y) == color;

        private int ColorAt(int x, int y)
        {
            var tile = GetTile(x, y);
            if (tile == null || tile.Drop != Match3Drop.None)
                return 0;
            return tile.Color;
        }

        private int MostCommonColor()
        {
            var counts = new int[7];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var color = ColorAt(x, y);
                    if (color >= 1 && color <= 6)
                        counts[color]++;
                }
            }

            var best = 1;
            for (var i = 2; i <= ColorCount; i++)
            {
                if (counts[i] > counts[best])
                    best = i;
            }

            return best;
        }

        private bool HasAnyCollectTarget()
        {
            if (Level.CollectCounts == null)
                return false;
            for (var i = 1; i < Level.CollectCounts.Length && i <= ColorCount; i++)
            {
                if (Level.CollectCounts[i] > 0)
                    return true;
            }

            return false;
        }

        private Match3Tile CreateTile(int color, Match3Special special, Match3Drop drop = Match3Drop.None)
        {
            return new Match3Tile
            {
                Id = _nextId++,
                Color = color,
                Special = special,
                Drop = drop
            };
        }

        private void PlaceHiddenDrops()
        {
            if (_rng.Next(Match3Icons.ExtraMovesChance) == 0)
                TryPlaceDrop(Match3Drop.ExtraMoves);
            if (_rng.Next(Match3Icons.GoldBeanChance) == 0)
                TryPlaceDrop(Match3Drop.GoldBean);
        }

        private void TryPlaceDrop(Match3Drop drop)
        {
            var cells = new List<Match3Coord>();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var tile = GetTile(x, y);
                    if (tile == null || tile.Drop != Match3Drop.None || tile.Special != Match3Special.None)
                        continue;
                    if (IsLocked(x, y))
                        continue;
                    cells.Add(new Match3Coord(x, y));
                }
            }

            if (cells.Count == 0)
                return;

            var at = cells[_rng.Next(cells.Count)];
            _tiles[at.X, at.Y] = CreateTile(0, Match3Special.None, drop);
        }

        private void ExtractRemoved(bool[,] remove, int gain, List<Match3BoardEvent> events, bool grantDrops)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!remove[x, y])
                        continue;
                    var tile = _tiles[x, y];
                    if (tile == null)
                        continue;

                    Score += gain;
                    if (tile.Color >= 1 && tile.Color <= 6)
                        Collected[tile.Color]++;
                    if (grantDrops)
                        GrantDrop(tile);
                    events.Add(Match3BoardEvent.Remove(tile, x, y, gain));
                    _tiles[x, y] = null;
                }
            }
        }

        private void GrantDrop(Match3Tile tile)
        {
            if (tile == null)
                return;
            if (tile.Drop == Match3Drop.ExtraMoves)
            {
                AddMoves(Match3Icons.ExtraMovesGain);
                _bonusMoves += Match3Icons.ExtraMovesGain;
            }
            else if (tile.Drop == Match3Drop.GoldBean)
            {
                _bonusCoins += Match3Icons.GoldBeanCoins;
            }
        }

        private void SwapTiles(int x0, int y0, int x1, int y1)
        {
            var tmp = _tiles[x0, y0];
            _tiles[x0, y0] = _tiles[x1, y1];
            _tiles[x1, y1] = tmp;
        }

        private static bool AreAdjacent(int x0, int y0, int x1, int y1)
        {
            return Math.Abs(x0 - x1) + Math.Abs(y0 - y1) == 1;
        }

        private bool InBoard(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        private bool IsStone(int x, int y) => InBoard(x, y) && _kind[x, y] == Match3CellKind.Stone && _layers[x, y] > 0;

        private bool IsLocked(int x, int y) => InBoard(x, y) && Match3CellUtil.LocksTile(_kind[x, y]) && _layers[x, y] > 0;

        private bool CanReceiveFall(int x, int y) => IsPlayable(x, y) && !IsLocked(x, y);

        private void ClearOverlayHits()
        {
            if (_overlayHit == null)
                return;
            Array.Clear(_overlayHit, 0, _overlayHit.Length);
        }

        private void HitOverlay(int x, int y, List<Match3BoardEvent> events)
        {
            if (!InBoard(x, y) || _overlayHit[x, y])
                return;
            if (!Match3CellUtil.IsOverlay(_kind[x, y]) || _layers[x, y] <= 0)
                return;

            var kind = _kind[x, y];
            _overlayHit[x, y] = true;
            _layers[x, y]--;
            if (_layers[x, y] <= 0)
            {
                _layers[x, y] = 0;
                _kind[x, y] = Match3CellKind.Playable;
                _playable[x, y] = true;
            }

            events?.Add(Match3BoardEvent.CellHit(x, y, kind, _layers[x, y]));
        }

        private void HitNeighborsOf(bool[,] remove, List<Match3BoardEvent> events)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!remove[x, y])
                        continue;
                    TryNeighborOverlay(x + 1, y, events);
                    TryNeighborOverlay(x - 1, y, events);
                    TryNeighborOverlay(x, y + 1, events);
                    TryNeighborOverlay(x, y - 1, events);
                }
            }
        }

        private void TryNeighborOverlay(int x, int y, List<Match3BoardEvent> events)
        {
            if (!InBoard(x, y))
                return;
            var kind = _kind[x, y];
            if (kind != Match3CellKind.Stone && kind != Match3CellKind.Ice)
                return;
            HitOverlay(x, y, events);
        }

        private sealed class MatchGroup
        {
            public int Color;
            public Match3Special Reward;
            public Match3Coord RewardAt;
            public readonly List<Match3Coord> Cells = new List<Match3Coord>(8);
        }
    }
}
