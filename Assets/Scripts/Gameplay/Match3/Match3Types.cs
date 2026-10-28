using System;
using System.Collections.Generic;

namespace SmallGames
{
    public enum Match3CellKind
    {
        Hole = 0,
        Playable = 1,
        Ice = 2,
        Stone = 3,
        Jelly = 4,
        Cage = 5
    }

    public enum Match3Special
    {
        None = 0,
        LineH = 1,
        LineV = 2,
        Bomb = 3,
        ColorBomb = 4
    }

    public enum Match3Drop
    {
        None = 0,
        ExtraMoves = 1,
        GoldBean = 2
    }

    public enum Match3GoalType
    {
        Score = 0,
        Collect = 1,
        ClearCells = 2
    }

    public enum Match3EventKind
    {
        Swap = 0,
        SwapRevert = 1,
        Remove = 2,
        SpawnSpecial = 3,
        Move = 4,
        Spawn = 5,
        Shuffle = 6,
        CellHit = 7,
        Blast = 8
    }

    public readonly struct Match3Coord : IEquatable<Match3Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Match3Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Match3Coord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Match3Coord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
    }

    public sealed class Match3Tile
    {
        public int Id;
        public int Color;
        public Match3Special Special;
        public Match3Drop Drop;
    }

    public struct Match3BoardEvent
    {
        public Match3EventKind Kind;
        public int Id;
        public int Color;
        public Match3Special Special;
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
        public int ScoreDelta;
        public Match3Drop Drop;

        public static Match3BoardEvent Swap(int aId, int ax, int ay, int bId, int bx, int by)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.Swap,
                Id = aId,
                FromX = ax,
                FromY = ay,
                ToX = bx,
                ToY = by,
                Color = bId
            };
        }

        public static Match3BoardEvent Revert(int aId, int ax, int ay, int bId, int bx, int by)
        {
            var e = Swap(aId, ax, ay, bId, bx, by);
            e.Kind = Match3EventKind.SwapRevert;
            return e;
        }

        public static Match3BoardEvent Remove(Match3Tile tile, int x, int y, int score)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.Remove,
                Id = tile.Id,
                Color = tile.Color,
                Special = tile.Special,
                FromX = x,
                FromY = y,
                ToX = x,
                ToY = y,
                ScoreDelta = score,
                Drop = tile.Drop
            };
        }

        public static Match3BoardEvent SpawnSpecial(Match3Tile tile, int x, int y)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.SpawnSpecial,
                Id = tile.Id,
                Color = tile.Color,
                Special = tile.Special,
                ToX = x,
                ToY = y
            };
        }

        public static Match3BoardEvent Move(Match3Tile tile, int fromX, int fromY, int toX, int toY)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.Move,
                Id = tile.Id,
                Color = tile.Color,
                Special = tile.Special,
                FromX = fromX,
                FromY = fromY,
                ToX = toX,
                ToY = toY
            };
        }

        public static Match3BoardEvent Spawn(Match3Tile tile, int fromX, int fromY, int toX, int toY)
        {
            var e = Move(tile, fromX, fromY, toX, toY);
            e.Kind = Match3EventKind.Spawn;
            return e;
        }

        public static Match3BoardEvent CellHit(int x, int y, Match3CellKind kind, int layersLeft)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.CellHit,
                FromX = x,
                FromY = y,
                ToX = x,
                ToY = y,
                Color = (int)kind,
                ScoreDelta = layersLeft
            };
        }

        public static Match3BoardEvent Blast(Match3Special special, int x, int y, int color = 0, int extra = 0, int id = 0)
        {
            return new Match3BoardEvent
            {
                Kind = Match3EventKind.Blast,
                Id = id,
                Color = color,
                Special = special,
                FromX = x,
                FromY = y,
                ToX = x,
                ToY = y,
                ScoreDelta = extra
            };
        }
    }

    public static class Match3CellUtil
    {
        public static bool CanHoldItem(Match3CellKind kind) => kind != Match3CellKind.Hole && kind != Match3CellKind.Stone;

        public static bool IsOverlay(Match3CellKind kind)
        {
            return kind == Match3CellKind.Ice
                || kind == Match3CellKind.Stone
                || kind == Match3CellKind.Jelly
                || kind == Match3CellKind.Cage;
        }

        public static bool LocksTile(Match3CellKind kind) => kind == Match3CellKind.Ice || kind == Match3CellKind.Cage;

        public static string Label(Match3CellKind kind)
        {
            switch (kind)
            {
                case Match3CellKind.Hole: return "空";
                case Match3CellKind.Ice: return "冰";
                case Match3CellKind.Stone: return "石";
                case Match3CellKind.Jelly: return "冻";
                case Match3CellKind.Cage: return "锁";
                default: return "格";
            }
        }

        public static UnityEngine.Color OverlayColor(Match3CellKind kind)
        {
            switch (kind)
            {
                case Match3CellKind.Ice: return UIStyle.Hex("#7FD4FF");
                case Match3CellKind.Stone: return UIStyle.Hex("#6B5A4A");
                case Match3CellKind.Jelly: return UIStyle.Hex("#FF6BA8");
                case Match3CellKind.Cage: return UIStyle.Hex("#F4C95D");
                default: return UIStyle.Hex("#1A2836");
            }
        }
    }

    public sealed class Match3ResolveResult
    {
        public readonly List<Match3BoardEvent> Events = new List<Match3BoardEvent>(64);
        public bool Applied;
        public bool Reverted;
        public bool Won;
        public bool Lost;
        public bool WaveCleared;
        public int BonusMoves;
        public int BonusCoins;
    }

    public static class Match3Palette
    {
        public static readonly string[] Hex =
        {
            "#2A3A4A",
            "#E85D5D",
            "#F0A05A",
            "#F4D35E",
            "#6BCB77",
            "#5BA3E8",
            "#B388FF"
        };

        public static readonly string[] Names =
        {
            "空",
            "甜甜圈",
            "曲奇",
            "冰淇淋",
            "蛋糕",
            "纸杯蛋糕",
            "切片"
        };

        public static UnityEngine.Color Get(int color)
        {
            var index = color < 0 || color >= Hex.Length ? 0 : color;
            return UIStyle.Hex(Hex[index]);
        }
    }

    public static class Match3Icons
    {
        public static readonly int[] TileFiles = { 3, 7, 8, 11, 15, 21 };
        public const int IceFile = 2;
        public const int StoneFile = 4;
        public const int JellyFile = 14;
        public const int CageFile = 9;
        public const int BombFile = 12;
        public const int ExtraMovesFile = 16;
        public const int GoldBeanFile = 6;

        public static int ExtraMovesChance => GameBalance.Current.Match3ExtraMovesChance;
        public static int GoldBeanChance => GameBalance.Current.Match3GoldBeanChance;
        public static int ExtraMovesGain => GameBalance.Current.Match3ExtraMovesGain;
        public static int GoldBeanCoins => GameBalance.Current.Match3GoldBeanCoins;

        public static string TileName(int color)
        {
            if (color <= 0 || color >= Match3Palette.Names.Length)
                return "随机";
            return Match3Palette.Names[color];
        }

        public static UnityEngine.Sprite FromFile(UnityEngine.Sprite[] pack, int fileNumber)
        {
            if (pack == null || fileNumber < 1)
                return null;
            var index = fileNumber - 1;
            if (index < 0 || index >= pack.Length)
                return null;
            return pack[index];
        }

        public static UnityEngine.Sprite TileOf(UnityEngine.Sprite[] pack, int color)
        {
            if (color < 1 || color > TileFiles.Length)
                return null;
            return FromFile(pack, TileFiles[color - 1]);
        }

        public static UnityEngine.Sprite OverlayOf(UnityEngine.Sprite[] pack, Match3CellKind kind)
        {
            switch (kind)
            {
                case Match3CellKind.Ice: return FromFile(pack, IceFile);
                case Match3CellKind.Stone: return FromFile(pack, StoneFile);
                case Match3CellKind.Jelly: return FromFile(pack, JellyFile);
                case Match3CellKind.Cage: return FromFile(pack, CageFile);
                default: return null;
            }
        }

        public static float OverlayAlpha(int layers)
        {
            if (layers <= 1)
                return 0.4f;
            if (layers == 2)
                return 0.62f;
            return 0.84f;
        }
    }
}
