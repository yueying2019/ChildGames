using UnityEngine;

namespace SmallGames
{
    public static class Match3LevelCatalog
    {
        public const string ResourceFolder = "Match3/Levels";
        public const string EditorPlayKey = "SmallGames.Match3.PlayPath";

        public static Match3LevelData Override { get; private set; }

        public static Match3LevelData[] LoadAll()
        {
            var levels = AssetHub.LoadAll<Match3LevelData>(AssetLabels.Match3Levels, ResourceFolder);
            if (levels == null || levels.Length == 0)
                return new Match3LevelData[0];

            System.Array.Sort(levels, (a, b) =>
            {
                var id = a.LevelId.CompareTo(b.LevelId);
                return id != 0 ? id : string.CompareOrdinal(a.name, b.name);
            });
            return levels;
        }

        public static Match3LevelData Get(int index)
        {
            if (Override != null)
                return Override;

            var all = LoadAll();
            if (all.Length == 0)
                return Match3LevelData.CreateDefault();
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
                return LoadAll().Length;
            }
        }

        public static Match3LevelData GetCampaign(int index)
        {
            var all = LoadAll();
            if (all.Length == 0)
                return Match3LevelData.CreateDefault();
            return all[Mathf.Clamp(index, 0, all.Length - 1)];
        }

        public static void SetOverride(Match3LevelData level)
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
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<Match3LevelData>(path);
            if (level == null)
                return;

            Override = level;
            if (GameManager.Instance != null)
                GameManager.Instance.StartGame(GameModeType.Match3);
#endif
        }
    }
}
