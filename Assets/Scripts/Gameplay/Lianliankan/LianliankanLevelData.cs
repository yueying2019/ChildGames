using System;
using UnityEngine;

namespace SmallGames
{
    public enum LianliankanCellKind : byte
    {
        Empty = 0,
        Tile = 1,
        Blocker = 2
    }

    [CreateAssetMenu(fileName = "LianliankanLevel", menuName = "SmallGames/对对消/关卡", order = 11)]
    public class LianliankanLevelData : ScriptableObject
    {
        public const int Rows = 10;
        public const int Cols = 10;
        public const int CellCount = Rows * Cols;
        public const int MinTypes = 2;
        public const int MaxTypes = 12;
        public const int BlockerIconIndex = 0;
        public const int FirstPlayableIconIndex = 1;

        public int LevelId = 1;
        public string Title = "第1关";
        public LianliankanLayoutKind Layout = LianliankanLayoutKind.Ring32;
        [Range(MinTypes, MaxTypes)] public int TypeCount = 6;
        public float TimeLimit = 180f;

        /// <summary>每级连击额外回补的秒数。0 = 关闭连锁回时（旧关卡默认行为）。</summary>
        public float ComboTimeBonus = 0f;
        [HideInInspector] public byte[] Cells = Array.Empty<byte>();
        [HideInInspector] public bool[] Occupied = Array.Empty<bool>();

        /// <summary>
        /// 固定图案表：长度等于 CellCount 时生效（教学关用），Tile 格按这张表发图案，不再随机洗牌。
        /// 值从 1 起（1..TypeCount）；为 0 表示该格仍然随机分配。
        /// </summary>
        [HideInInspector] public byte[] IconIds = Array.Empty<byte>();

        /// <summary>是否固定图案（长度对得上才算）。</summary>
        public bool HasFixedIcons => IconIds != null && IconIds.Length == CellCount;

        /// <summary>取固定图案，0 表示这格没固定。</summary>
        public int FixedIconAt(int row, int col)
        {
            if (!HasFixedIcons || row < 0 || col < 0 || row >= Rows || col >= Cols)
                return 0;
            return IconIds[row * Cols + col];
        }

        public void EnsureInitialized()
        {
            LevelId = Mathf.Max(1, LevelId);
            TypeCount = Mathf.Clamp(TypeCount, MinTypes, MaxTypes);
            TimeLimit = Mathf.Max(10f, TimeLimit);
            ComboTimeBonus = Mathf.Clamp(ComboTimeBonus, 0f, 10f);
            if (Cells != null && Cells.Length == CellCount)
            {
                SyncLegacyOccupied();
                return;
            }

            Cells = new byte[CellCount];
            if (Occupied != null && Occupied.Length == CellCount)
            {
                for (var i = 0; i < CellCount; i++)
                    Cells[i] = Occupied[i] ? (byte)LianliankanCellKind.Tile : (byte)LianliankanCellKind.Empty;
                return;
            }

            ApplyTemplate(Layout);
        }

        public LianliankanCellKind GetCell(int row, int col)
        {
            EnsureInitialized();
            if (row < 0 || col < 0 || row >= Rows || col >= Cols)
                return LianliankanCellKind.Empty;
            return (LianliankanCellKind)Cells[row * Cols + col];
        }

        public void SetCell(int row, int col, LianliankanCellKind kind)
        {
            EnsureInitialized();
            if (row < 0 || col < 0 || row >= Rows || col >= Cols)
                return;
            Cells[row * Cols + col] = (byte)kind;
            Layout = LianliankanLayoutKind.Custom;
            SyncLegacyOccupied();
        }

        public bool IsTile(int row, int col) => GetCell(row, col) == LianliankanCellKind.Tile;

        public bool IsBlocker(int row, int col) => GetCell(row, col) == LianliankanCellKind.Blocker;

        public bool IsOn(int row, int col) => IsTile(row, col);

        public void SetOn(int row, int col, bool on)
        {
            SetCell(row, col, on ? LianliankanCellKind.Tile : LianliankanCellKind.Empty);
        }

        public int CountKind(LianliankanCellKind kind)
        {
            EnsureInitialized();
            var count = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                if (Cells[i] == (byte)kind)
                    count++;
            }

            return count;
        }

        public int CountOccupied() => CountKind(LianliankanCellKind.Tile);

        public int CountBlockers() => CountKind(LianliankanCellKind.Blocker);

        public void ApplyTemplate(LianliankanLayoutKind kind, bool keepBlockers = true)
        {
            if (kind == LianliankanLayoutKind.Custom)
            {
                if (Cells == null || Cells.Length != CellCount)
                    Cells = new byte[CellCount];
                Layout = LianliankanLayoutKind.Custom;
                SyncLegacyOccupied();
                return;
            }

            Layout = kind;
            var mask = LianliankanBoardLayout.GetLayout(kind);
            var old = Cells != null && Cells.Length == CellCount ? Cells : null;
            Cells = new byte[CellCount];
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    var i = r * Cols + c;
                    if (LianliankanBoardLayout.IsOn(mask, r, c))
                        Cells[i] = (byte)LianliankanCellKind.Tile;
                    else if (keepBlockers && old != null && old[i] == (byte)LianliankanCellKind.Blocker)
                        Cells[i] = (byte)LianliankanCellKind.Blocker;
                    else
                        Cells[i] = (byte)LianliankanCellKind.Empty;
                }
            }

            SyncLegacyOccupied();
        }

        public void ClearAll()
        {
            EnsureInitialized();
            for (var i = 0; i < Cells.Length; i++)
                Cells[i] = (byte)LianliankanCellKind.Empty;
            Layout = LianliankanLayoutKind.Custom;
            SyncLegacyOccupied();
        }

        public static int PlayableIconCap(int iconCount)
        {
            if (iconCount <= FirstPlayableIconIndex)
                return Mathf.Max(0, iconCount);
            return iconCount - FirstPlayableIconIndex;
        }

        public static LianliankanLevelData CreateDefault()
        {
            var data = CreateInstance<LianliankanLevelData>();
            data.LevelId = 1;
            data.Title = "初入农场";
            data.TypeCount = 6;
            data.TimeLimit = 180f;
            data.ApplyTemplate(LianliankanLayoutKind.Ring32);
            return data;
        }

        private void SyncLegacyOccupied()
        {
            if (Occupied == null || Occupied.Length != CellCount)
                Occupied = new bool[CellCount];
            if (Cells == null || Cells.Length != CellCount)
                return;
            for (var i = 0; i < CellCount; i++)
                Occupied[i] = Cells[i] == (byte)LianliankanCellKind.Tile;
        }
    }
}
