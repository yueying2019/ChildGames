using UnityEngine;

namespace SmallGames
{
    public struct LianliankanLevelSpec
    {
        public int Id;
        public string Title;
        public LianliankanLayoutKind Layout;
        public int TypeCount;
        public float TimeLimit;
    }

    public static class LianliankanLevelCatalog
    {
        public const string ResourceFolder = "Lianliankan/Levels";
        public const string EditorPlayKey = "SmallGames.Lianliankan.PlayPath";

        public static readonly LianliankanLevelSpec[] Samples =
        {
            Spec(1, "初入农场", LianliankanLayoutKind.Ring32, 6, 180f),
            Spec(2, "田埂小路", LianliankanLayoutKind.Ring32, 6, 160f),
            Spec(3, "菜畦午后", LianliankanLayoutKind.Center36, 6, 160f),
            Spec(4, "丰收时节", LianliankanLayoutKind.Center36, 7, 150f),
            Spec(5, "双园并蒂", LianliankanLayoutKind.Twin40, 7, 150f),
            Spec(6, "葡萄长廊", LianliankanLayoutKind.Wide48, 7, 140f),
            Spec(7, "向日葵田", LianliankanLayoutKind.Wide48, 8, 130f),
            Spec(8, "空心果篮", LianliankanLayoutKind.Hollow48, 8, 130f),
            Spec(9, "三垄麦田", LianliankanLayoutKind.Columns42, 8, 120f),
            Spec(10, "迷宫果园", LianliankanLayoutKind.Full64, 8, 120f),
            Spec(11, "南瓜集市", LianliankanLayoutKind.UShape44, 9, 110f),
            Spec(12, "隔岸花田", LianliankanLayoutKind.Twin40, 9, 110f),
            Spec(13, "竹篱小院", LianliankanLayoutKind.Hollow48, 10, 100f),
            Spec(14, "秋日果园", LianliankanLayoutKind.Wide48, 10, 100f),
            Spec(15, "满园金黄", LianliankanLayoutKind.Full64, 8, 130f),
            Spec(16, "夜色麦浪", LianliankanLayoutKind.Columns42, 8, 130f),
            Spec(17, "霜降果篮", LianliankanLayoutKind.Hollow48, 8, 130f),
            Spec(18, "丰年庆典", LianliankanLayoutKind.UShape44, 9, 140f),
            Spec(19, "雪藏谷仓", LianliankanLayoutKind.Full64, 9, 150f),
            Spec(20, "农场终章", LianliankanLayoutKind.Full64, 9, 160f),
            Spec(21, "石墙迷宫", LianliankanLayoutKind.Full64, 8, 150f),
            Spec(22, "十字花园", LianliankanLayoutKind.Columns42, 6, 120f),
            Spec(23, "极速果园", LianliankanLayoutKind.Ring32, 3, 90f),
            Spec(24, "果园回廊", LianliankanLayoutKind.Wide48, 6, 170f),
            Spec(25, "田边小径", LianliankanLayoutKind.Ring32, 6, 160f),
            Spec(26, "双畦菜园", LianliankanLayoutKind.Twin40, 7, 160f),
            Spec(27, "中心花坛", LianliankanLayoutKind.Center36, 7, 150f),
            Spec(28, "三排果树", LianliankanLayoutKind.Columns42, 7, 160f),
            Spec(29, "空心谷仓", LianliankanLayoutKind.Hollow48, 8, 160f),
            Spec(30, "U形篱笆", LianliankanLayoutKind.UShape44, 8, 160f),
            Spec(31, "宽畦麦田", LianliankanLayoutKind.Wide48, 8, 150f),
            Spec(32, "满畦向阳", LianliankanLayoutKind.Full64, 8, 170f),
            Spec(33, "双园晚霞", LianliankanLayoutKind.Twin40, 8, 150f),
            Spec(34, "竹影回廊", LianliankanLayoutKind.Columns42, 9, 160f),
            Spec(35, "环园小道", LianliankanLayoutKind.Hollow48, 9, 170f),
            Spec(36, "农场新章", LianliankanLayoutKind.Full64, 9, 180f)
        };

        public static LianliankanLevelData Override { get; private set; }

        private static LianliankanLevelData[] _builtin;

        public static LianliankanLevelData[] LoadAll()
        {
            var levels = AssetHub.LoadAll<LianliankanLevelData>(AssetLabels.LianliankanLevels, ResourceFolder);
            if (levels == null || levels.Length == 0)
                return Builtin();

            System.Array.Sort(levels, (a, b) =>
            {
                var id = a.LevelId.CompareTo(b.LevelId);
                return id != 0 ? id : string.CompareOrdinal(a.name, b.name);
            });
            return levels;
        }

        public static LianliankanLevelData Get(int index)
        {
            if (Override != null)
                return Override;

            var all = LoadAll();
            if (all.Length == 0)
                return LianliankanLevelData.CreateDefault();
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

        public static LianliankanLevelData GetCampaign(int index)
        {
            var all = LoadAll();
            if (all.Length == 0)
                return LianliankanLevelData.CreateDefault();
            return all[Mathf.Clamp(index, 0, all.Length - 1)];
        }

        public static void ApplySpec(LianliankanLevelData data, LianliankanLevelSpec spec)
        {
            if (data == null)
                return;
            data.LevelId = spec.Id;
            data.Title = spec.Title;
            data.TypeCount = spec.TypeCount;
            data.TimeLimit = spec.TimeLimit;
            data.ApplyTemplate(spec.Layout, false);
        }

        public static void SetOverride(LianliankanLevelData level)
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
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LianliankanLevelData>(path);
            if (level == null)
                return;

            Override = level;
            if (GameManager.Instance != null)
                GameManager.Instance.StartGame(GameModeType.Lianliankan);
#endif
        }

        private static LianliankanLevelSpec Spec(int id, string title, LianliankanLayoutKind layout, int types, float seconds)
        {
            return new LianliankanLevelSpec
            {
                Id = id,
                Title = title,
                Layout = layout,
                TypeCount = types,
                TimeLimit = seconds
            };
        }

        private static LianliankanLevelData[] Builtin()
        {
            if (_builtin != null)
                return _builtin;

            _builtin = new LianliankanLevelData[Samples.Length];
            for (var i = 0; i < Samples.Length; i++)
            {
                var data = ScriptableObject.CreateInstance<LianliankanLevelData>();
                data.hideFlags = HideFlags.HideAndDontSave;
                data.name = $"Level_{Samples[i].Id:00}";
                ApplySpec(data, Samples[i]);
                _builtin[i] = data;
            }

            return _builtin;
        }
    }
}
