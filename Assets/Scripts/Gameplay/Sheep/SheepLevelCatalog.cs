using UnityEngine;

namespace SmallGames
{
    public struct SheepLevelSpec
    {
        public int Id;
        public string Title;
        public int TypeNum;
        public int LevelNum;
        public int LevelBlockNum;
        public int BorderStep;
        public int LeftStack;
        public int RightStack;
        public SheepMotifKind Motif;
        public float TimeLimit;
        public int GoalCount;
        public int SlotBonus;
    }

    public static class SheepLevelCatalog
    {
        public const string ResourceFolder = "Sheep/Levels";
        public const string EditorPlayKey = "SmallGames.Sheep.PlayPath";

        public static readonly SheepLevelSpec[] Samples =
        {
            Spec(1, "青草地", 4, 3, 8, 1, 0, 0, SheepMotifKind.Lawn),
            Spec(2, "羊圈午后", 4, 3, 9, 1, 3, 0, SheepMotifKind.Sheep),
            Spec(3, "小溪边", 5, 3, 10, 1, 3, 3, SheepMotifKind.Pond),
            Spec(4, "叠叠山坡", 5, 4, 8, 1, 3, 3, SheepMotifKind.Tree),
            Spec(5, "午后剪毛", 6, 4, 10, 1, 4, 3, SheepMotifKind.Sheep),
            Spec(6, "山坡野餐", 6, 4, 10, 1, 4, 4, SheepMotifKind.Grove),
            Spec(7, "羊毛成堆", 6, 4, 12, 1, 6, 4, SheepMotifKind.Sheep),
            Spec(8, "层层叠叠", 7, 5, 10, 1, 6, 6, SheepMotifKind.Flower),
            Spec(9, "迷雾牧场", 7, 5, 12, 1, 6, 6, SheepMotifKind.Grove),
            Spec(10, "叠叠消", 8, 5, 12, 2, 6, 6, SheepMotifKind.Sheep),
            Spec(11, "夜牧归圈", 8, 5, 12, 2, 8, 6, SheepMotifKind.Sheep),
            Spec(12, "陡坡叠石", 8, 6, 10, 2, 8, 8, SheepMotifKind.Tree),
            Spec(13, "双堆干草", 8, 6, 12, 2, 6, 6, SheepMotifKind.Grove),
            Spec(14, "高山牧场", 8, 6, 12, 2, 6, 6, SheepMotifKind.Tree),
            Spec(15, "暴雨羊圈", 8, 6, 12, 2, 6, 6, SheepMotifKind.Pond),
            Spec(16, "迷宫草垛", 8, 6, 12, 2, 6, 6, SheepMotifKind.Grove),
            Spec(17, "雪地寻羊", 8, 6, 12, 2, 8, 6, SheepMotifKind.Sheep, 0f, 0, 1),
            Spec(18, "悬崖叠层", 8, 6, 12, 2, 8, 8, SheepMotifKind.Tree, 0f, 0, 1),
            Spec(19, "万羊奔腾", 8, 6, 12, 2, 8, 8, SheepMotifKind.Butterfly, 0f, 0, 1),
            Spec(20, "羊村终章", 8, 6, 12, 2, 8, 8, SheepMotifKind.Flower, 0f, 0, 1),
            Spec(21, "晨雾草地", 6, 4, 8, 1, 2, 2, SheepMotifKind.Lawn),
            Spec(22, "花田小径", 6, 4, 10, 1, 3, 3, SheepMotifKind.Flower),
            Spec(23, "池塘倒影", 6, 5, 8, 1, 4, 4, SheepMotifKind.Pond),
            Spec(24, "小羊回家", 7, 5, 8, 1, 4, 4, SheepMotifKind.Sheep),
            Spec(25, "树荫午休", 7, 5, 10, 1, 4, 4, SheepMotifKind.Tree),
            Spec(26, "蝴蝶停驻", 7, 5, 10, 1, 6, 4, SheepMotifKind.Butterfly, 0f, 8, 0),
            Spec(27, "林间小路", 7, 5, 10, 1, 6, 6, SheepMotifKind.Grove, 240f, 0, 0),
            Spec(28, "双圈剪毛", 8, 5, 10, 1, 6, 6, SheepMotifKind.Sheep, 0f, 0, 1),
            Spec(29, "山坡野花", 8, 5, 12, 1, 6, 6, SheepMotifKind.Flower),
            Spec(30, "雨后池塘", 8, 6, 10, 1, 6, 6, SheepMotifKind.Pond),
            Spec(31, "草垛迷宫", 8, 6, 10, 1, 6, 6, SheepMotifKind.Grove),
            Spec(32, "雪后羊圈", 8, 6, 12, 1, 6, 6, SheepMotifKind.Sheep, 0f, 0, 1),
            Spec(33, "悬崖小树", 8, 6, 12, 1, 6, 6, SheepMotifKind.Tree),
            Spec(34, "万花牧场", 8, 6, 12, 1, 8, 6, SheepMotifKind.Flower, 0f, 12, 0),
            Spec(35, "夜色归途", 8, 6, 12, 1, 8, 8, SheepMotifKind.Grove, 300f, 0, 0),
            Spec(36, "羊村新章", 8, 6, 12, 1, 8, 8, SheepMotifKind.Sheep, 0f, 0, 1)
        };

        public static SheepLevelData Override { get; private set; }

        private static SheepLevelData[] _builtin;

        public static SheepLevelData[] LoadAll()
        {
            var levels = AssetHub.LoadAll<SheepLevelData>(AssetLabels.SheepLevels, ResourceFolder);
            if (levels == null || levels.Length == 0)
                return Builtin();

            System.Array.Sort(levels, (a, b) =>
            {
                var id = a.LevelId.CompareTo(b.LevelId);
                return id != 0 ? id : string.CompareOrdinal(a.name, b.name);
            });
            return levels;
        }

        public static SheepLevelData Get(int index)
        {
            if (Override != null)
                return Override;

            var all = LoadAll();
            if (all.Length == 0)
                return SheepLevelData.CreateDefault();
            return all[Mathf.Clamp(index, 0, all.Length - 1)];
        }

        public static int Count
        {
            get
            {
                if (Override != null)
                    return 1;
                return CampaignCount;
            }
        }

        public static int CampaignCount
        {
            get
            {
                var n = LoadAll().Length;
                return n > 0 ? n : 1;
            }
        }

        public static SheepLevelData GetCampaign(int index)
        {
            var all = LoadAll();
            if (all.Length == 0)
                return SheepLevelData.CreateDefault();
            return all[Mathf.Clamp(index, 0, all.Length - 1)];
        }

        public static void ApplySpec(SheepLevelData data, SheepLevelSpec spec)
        {
            if (data == null)
                return;
            data.LevelId = spec.Id;
            data.Title = spec.Title;
            data.TypeNum = spec.TypeNum;
            data.LevelNum = spec.LevelNum;
            data.LevelBlockNum = spec.LevelBlockNum;
            data.BorderStep = spec.BorderStep;
            data.LeftStack = spec.LeftStack;
            data.RightStack = spec.RightStack;
            data.Motif = spec.Motif;
            data.ComposeNum = 3;
            data.TimeLimit = spec.TimeLimit;
            data.GoalCount = spec.GoalCount;
            data.SlotBonus = spec.SlotBonus;
            data.EnsureInitialized();
        }

        public static void SetOverride(SheepLevelData level)
        {
            Override = level;
        }

        public static void ClearOverride()
        {
            Override = null;
        }

        public static void TryStartEditorPlay()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.HasKey(EditorPlayKey))
                return;

            var path = UnityEditor.EditorPrefs.GetString(EditorPlayKey);
            UnityEditor.EditorPrefs.DeleteKey(EditorPlayKey);
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<SheepLevelData>(path);
            if (level == null)
                return;

            Override = level;
            if (GameManager.Instance != null)
                GameManager.Instance.StartGame(GameModeType.Sheep);
#endif
        }

        private static SheepLevelSpec Spec(
            int id,
            string title,
            int types,
            int layers,
            int blocks,
            int border,
            int left,
            int right,
            SheepMotifKind motif,
            float timeLimit = 0f,
            int goalCount = 0,
            int slotBonus = 0)
        {
            return new SheepLevelSpec
            {
                Id = id,
                Title = title,
                TypeNum = types,
                LevelNum = layers,
                LevelBlockNum = blocks,
                BorderStep = border,
                LeftStack = left,
                RightStack = right,
                Motif = motif,
                TimeLimit = timeLimit,
                GoalCount = goalCount,
                SlotBonus = slotBonus
            };
        }

        private static SheepLevelData[] Builtin()
        {
            if (_builtin != null)
                return _builtin;

            _builtin = new SheepLevelData[Samples.Length];
            for (var i = 0; i < Samples.Length; i++)
            {
                var data = ScriptableObject.CreateInstance<SheepLevelData>();
                data.hideFlags = HideFlags.HideAndDontSave;
                data.name = $"Level_{Samples[i].Id:00}";
                ApplySpec(data, Samples[i]);
                _builtin[i] = data;
            }

            return _builtin;
        }
    }
}
