namespace SmallGames
{
    /// <summary>
    /// 各模式的关卡列表查询，给选关和进度用。
    /// </summary>
    public static class LevelBrowse
    {
        public static int Count(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return SheepLevelCatalog.CampaignCount;
                case GameModeType.Match3: return Match3LevelCatalog.CampaignCount;
                case GameModeType.Lianliankan: return LianliankanLevelCatalog.CampaignCount;
                default: return 0;
            }
        }

        public static int LevelId(GameModeType mode, int index)
        {
            switch (mode)
            {
                case GameModeType.Sheep:
                    var sheep = SheepLevelCatalog.GetCampaign(index);
                    return sheep != null ? sheep.LevelId : index + 1;
                case GameModeType.Match3:
                    var match = Match3LevelCatalog.GetCampaign(index);
                    return match != null ? match.LevelId : index + 1;
                case GameModeType.Lianliankan:
                    var lian = LianliankanLevelCatalog.GetCampaign(index);
                    return lian != null ? lian.LevelId : index + 1;
                default:
                    return index + 1;
            }
        }

        public static string Title(GameModeType mode, int index)
        {
            switch (mode)
            {
                case GameModeType.Sheep:
                    var sheep = SheepLevelCatalog.GetCampaign(index);
                    return sheep != null ? sheep.Title : "叠叠消";
                case GameModeType.Match3:
                    var match = Match3LevelCatalog.GetCampaign(index);
                    return match != null ? match.DisplayName : "三消";
                case GameModeType.Lianliankan:
                    var lian = LianliankanLevelCatalog.GetCampaign(index);
                    return lian != null ? lian.Title : "对对消";
                default:
                    return "关卡";
            }
        }

        public static int IndexOf(GameModeType mode, int levelId)
        {
            var count = Count(mode);
            for (var i = 0; i < count; i++)
            {
                if (LevelId(mode, i) == levelId)
                    return i;
            }

            return -1;
        }
    }
}
