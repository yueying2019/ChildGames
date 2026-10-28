using System;
using UnityEngine;

namespace SmallGames
{
    [CreateAssetMenu(fileName = "Match3Level", menuName = "SmallGames/三消/关卡", order = 15)]
    public class Match3LevelData : ScriptableObject
    {
        public const int MinSize = 4;
        public const int MaxSize = 12;
        public const int MaxColor = 6;

        public int LevelId = 1;
        public string DisplayName = "第1关";
        [Range(MinSize, MaxSize)] public int Width = 8;
        [Range(MinSize, MaxSize)] public int Height = 8;
        [Range(3, MaxColor)] public int ColorCount = 5;
        public int MoveLimit = 25;

        /// <summary>秒。0 = 不限时（沿用步数限制），大于 0 时步数与倒计时同时生效。</summary>
        public float TimeLimit = 0f;

        public Match3GoalType GoalType = Match3GoalType.Score;
        public int TargetScore = 2000;
        public int[] CollectCounts = new int[7];
        public Match3CellSpec[] Cells = Array.Empty<Match3CellSpec>();

        /// <summary>
        /// 固定排布：Cells 里每格都给了 1..ColorCount 的颜色时棋盘就是确定的（教学关用）。
        /// 打开后开局不再随机埋「加步 / 金豆荚」，保证教学局面完全可控。
        /// </summary>
        public bool FixedBoard;

        public int CellCount => Width * Height;

        public void EnsureInitialized()
        {
            Width = Mathf.Clamp(Width, MinSize, MaxSize);
            Height = Mathf.Clamp(Height, MinSize, MaxSize);
            ColorCount = Mathf.Clamp(ColorCount, 3, MaxColor);
            MoveLimit = Mathf.Max(1, MoveLimit);
            TimeLimit = TimeLimit <= 0f ? 0f : Mathf.Max(10f, TimeLimit);
            TargetScore = Mathf.Max(0, TargetScore);

            if (CollectCounts == null || CollectCounts.Length != 7)
            {
                var old = CollectCounts;
                CollectCounts = new int[7];
                if (old != null)
                    Array.Copy(old, CollectCounts, Mathf.Min(old.Length, 7));
            }

            if (Cells == null || Cells.Length != CellCount)
                Resize(Width, Height);
        }

        public void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            var next = new Match3CellSpec[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var spec = Match3CellSpec.Playable(0);
                    if (Cells != null && x < Width && y < Height)
                        spec = GetCell(x, y);
                    next[y * width + x] = spec;
                }
            }

            Width = width;
            Height = height;
            Cells = next;
        }

        public Match3CellSpec GetCell(int x, int y)
        {
            if (Cells == null || x < 0 || y < 0 || x >= Width || y >= Height)
                return Match3CellSpec.Empty;
            var index = y * Width + x;
            if (index < 0 || index >= Cells.Length)
                return Match3CellSpec.Empty;
            return Cells[index];
        }

        public void SetCell(int x, int y, Match3CellSpec spec)
        {
            EnsureInitialized();
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;
            Cells[y * Width + x] = spec;
        }

        public void FillPlayable(int color = 0)
        {
            EnsureInitialized();
            for (var i = 0; i < Cells.Length; i++)
                Cells[i] = Match3CellSpec.Playable(color);
        }

        public int CountPlayable()
        {
            EnsureInitialized();
            var count = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                if (Match3CellUtil.CanHoldItem(Cells[i].Kind))
                    count++;
            }

            return count;
        }

        public int CountKind(Match3CellKind kind)
        {
            EnsureInitialized();
            var count = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                if (Cells[i].Kind == kind)
                    count++;
            }

            return count;
        }

        public int CountOverlays()
        {
            EnsureInitialized();
            var count = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                if (Match3CellUtil.IsOverlay(Cells[i].Kind))
                    count++;
            }

            return count;
        }

        public int CountFixedColor(int color)
        {
            EnsureInitialized();
            var count = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                var spec = Cells[i];
                if (Match3CellUtil.CanHoldItem(spec.Kind) && spec.Color == color)
                    count++;
            }

            return count;
        }

        public void FillColorsKeepOverlays(int color = 0)
        {
            EnsureInitialized();
            for (var i = 0; i < Cells.Length; i++)
            {
                var spec = Cells[i];
                if (!Match3CellUtil.CanHoldItem(spec.Kind))
                    continue;
                spec.Color = color;
                Cells[i] = spec;
            }
        }

        public void ClearOverlaysKeepTiles()
        {
            EnsureInitialized();
            for (var i = 0; i < Cells.Length; i++)
            {
                var spec = Cells[i];
                if (spec.Kind == Match3CellKind.Hole)
                    continue;
                Cells[i] = Match3CellSpec.Playable(spec.Color);
            }
        }

        public bool SyncCollectFromBoard()
        {
            EnsureInitialized();
            var next = new int[7];
            var any = false;
            for (var color = 1; color <= ColorCount; color++)
            {
                var n = CountFixedColor(color);
                next[color] = n;
                any |= n > 0;
            }

            if (!any)
                return false;

            CollectCounts = next;
            return true;
        }

        public bool HasCollectTarget()
        {
            EnsureInitialized();
            if (CollectCounts == null)
                return false;
            for (var color = 1; color <= ColorCount && color < CollectCounts.Length; color++)
            {
                if (CollectCounts[color] > 0)
                    return true;
            }

            return false;
        }

        public static Match3LevelData CreateDefault()
        {
            var data = CreateInstance<Match3LevelData>();
            data.LevelId = 1;
            data.DisplayName = "甜品入门";
            data.Width = 8;
            data.Height = 8;
            data.ColorCount = 5;
            data.MoveLimit = 25;
            data.GoalType = Match3GoalType.Score;
            data.TargetScore = 2000;
            data.CollectCounts = new int[7];
            data.FillPlayable();
            return data;
        }
    }

    [Serializable]
    public struct Match3CellSpec : IEquatable<Match3CellSpec>
    {
        public Match3CellKind Kind;
        public int Color;
        public int Layers;

        public static Match3CellSpec Empty => new Match3CellSpec { Kind = Match3CellKind.Hole };

        public static Match3CellSpec Playable(int color = 0)
        {
            return new Match3CellSpec { Kind = Match3CellKind.Playable, Color = color };
        }

        public static Match3CellSpec Overlay(Match3CellKind kind, int color = 0, int layers = 1)
        {
            return new Match3CellSpec
            {
                Kind = kind,
                Color = color,
                Layers = Mathf.Clamp(layers, 1, 3)
            };
        }

        public int ResolvedLayers => Match3CellUtil.IsOverlay(Kind) ? Mathf.Clamp(Layers <= 0 ? 1 : Layers, 1, 3) : 0;

        public Match3CellSpec PaintTile(int color)
        {
            if (Kind == Match3CellKind.Hole || Kind == Match3CellKind.Stone)
                return Playable(color);
            var next = this;
            next.Color = color;
            return next;
        }

        public Match3CellSpec PaintOverlay(Match3CellKind overlay, int layers = 1)
        {
            if (overlay == Match3CellKind.Hole)
                return Empty;
            if (overlay == Match3CellKind.Playable || !Match3CellUtil.IsOverlay(overlay))
                return Playable(Color);
            if (overlay == Match3CellKind.Stone)
                return Overlay(Match3CellKind.Stone, 0, layers);
            return Overlay(overlay, Color, layers);
        }

        public Match3CellSpec ClearOverlay()
        {
            if (Kind == Match3CellKind.Hole)
                return Empty;
            return Playable(Kind == Match3CellKind.Stone ? 0 : Color);
        }

        public bool Equals(Match3CellSpec other) => Kind == other.Kind && Color == other.Color && Layers == other.Layers;
        public override bool Equals(object obj) => obj is Match3CellSpec other && Equals(other);
        public override int GetHashCode() => (((int)Kind * 397) ^ Color) * 397 ^ Layers;
    }
}
