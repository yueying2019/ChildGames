using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>固定排布里的一张牌：格子坐标 + 层号 + 图案。</summary>
    [System.Serializable]
    public struct SheepFixedCell
    {
        public int GridX;
        public int GridY;
        public int Layer;
        public int IconId;

        public static SheepFixedCell Of(int gridX, int gridY, int layer, int iconId)
        {
            return new SheepFixedCell { GridX = gridX, GridY = gridY, Layer = layer, IconId = iconId };
        }
    }

    [CreateAssetMenu(fileName = "SheepLevel", menuName = "SmallGames/叠叠消/关卡", order = 13)]
    public class SheepLevelData : ScriptableObject
    {
        public const int MinTypes = 2;
        public const int MaxTypes = 12;
        public const int MinLayers = 1;
        public const int MaxLayers = 10;
        public const int MaxBlocks = 24;
        public const int MaxStack = 20;

        /// <summary>一组几张才能消。3 是常规玩法，4 以上即「四同/多同」变体。</summary>
        public const int DefaultCompose = 3;
        public const int MinCompose = 2;
        public const int MaxCompose = 6;

        /// <summary>
        /// 关卡自带的基础槽位增量（道具「暂存位」另算，两者共用这个额度）。
        /// 上限刻意压到 2：Slots 用的是 HorizontalLayoutGroup，槽越多每张牌越窄，
        /// 超过 9 个槽时 80px 的牌会互相压住。
        /// </summary>
        public const int MaxSlotBonus = 2;

        /// <summary>限时上限，单位秒。</summary>
        public const float MaxTimeLimit = 900f;

        public int LevelId = 1;
        public string Title = "第1关";
        [Range(MinTypes, MaxTypes)] public int TypeNum = 4;
        [Range(MinLayers, MaxLayers)] public int LevelNum = 3;
        [Range(1, MaxBlocks)] public int LevelBlockNum = 8;
        [Range(0, 4)] public int BorderStep = 1;
        [Range(0, MaxStack)] public int LeftStack;
        [Range(0, MaxStack)] public int RightStack;
        [Range(MinCompose, MaxCompose)] public int ComposeNum = 3;
        public SheepMotifKind Motif = SheepMotifKind.Auto;

        [Range(0, MaxSlotBonus)] public int SlotBonus;
        /// <summary>本关限时，单位秒。0 或负数表示不限时。</summary>
        [Range(0, MaxTimeLimit)] public float TimeLimit;
        /// <summary>已消除多少组即通关。0 表示沿用「清空全部」的常规胜利条件。</summary>
        public int GoalCount;

        /// <summary>
        /// 固定排布：非空时棋盘完全按这张表摆（教学关用），不再走随机生成。
        /// IconId 从 1 起，同一图案的张数必须是 ComposeNum 的整数倍，否则会出现消不掉的残局。
        /// </summary>
        public SheepFixedCell[] FixedCells = Array.Empty<SheepFixedCell>();

        /// <summary>是否固定排布。</summary>
        public bool HasFixedCells => FixedCells != null && FixedCells.Length > 0;

        public void EnsureInitialized()
        {
            LevelId = Mathf.Max(1, LevelId);
            TypeNum = Mathf.Clamp(TypeNum, MinTypes, MaxTypes);
            LevelNum = Mathf.Clamp(LevelNum, MinLayers, MaxLayers);
            LevelBlockNum = Mathf.Clamp(LevelBlockNum, 1, MaxBlocks);
            BorderStep = Mathf.Clamp(BorderStep, 0, 4);
            LeftStack = Mathf.Clamp(LeftStack, 0, MaxStack);
            RightStack = Mathf.Clamp(RightStack, 0, MaxStack);
            ComposeNum = Mathf.Clamp(ComposeNum, MinCompose, MaxCompose);
            SlotBonus = Mathf.Clamp(SlotBonus, 0, MaxSlotBonus);
            TimeLimit = Mathf.Clamp(TimeLimit, 0f, MaxTimeLimit);
            GoalCount = Mathf.Max(0, GoalCount);
        }

        /// <summary>本关是否限时。</summary>
        public bool HasTimeLimit => TimeLimit > 0.01f;

        /// <summary>本关是否采用「消够指定组数」的胜利条件。</summary>
        public bool HasGoal => GoalCount > 0;

        public int PlannedMinCount
        {
            get
            {
                EnsureInitialized();
                return LevelNum * LevelBlockNum + LeftStack + RightStack;
            }
        }

        public int ResolvedTotal(int iconCap = 0)
        {
            EnsureInitialized();
            var types = TypeNum;
            if (iconCap > 0)
                types = Mathf.Min(types, iconCap);
            types = Mathf.Max(1, types);
            var unit = ComposeNum * types;
            var total = PlannedMinCount;
            if (unit > 0 && total % unit != 0)
                total = (total / unit + 1) * unit;
            return total;
        }

        public static SheepLevelData CreateDefault()
        {
            var data = CreateInstance<SheepLevelData>();
            data.LevelId = 1;
            data.Title = "青草地";
            data.TypeNum = 4;
            data.LevelNum = 3;
            data.LevelBlockNum = 8;
            data.BorderStep = 1;
            data.LeftStack = 0;
            data.RightStack = 0;
            data.ComposeNum = 3;
            return data;
        }
    }
}
