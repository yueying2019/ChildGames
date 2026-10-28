namespace SmallGames
{
    /// <summary>
    /// Addressables Label，与分组脚本保持一致。
    /// </summary>
    public static class AssetLabels
    {
        public const string Core = "ui.core";
        public const string Lianliankan = "mode.lianliankan";
        public const string Sheep = "mode.sheep";
        public const string Match3 = "mode.match3";
        public const string LianliankanLevels = "levels.lianliankan";
        public const string SheepLevels = "levels.sheep";
        public const string Match3Levels = "levels.match3";

        public static string ForMode(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return Lianliankan;
                case GameModeType.Sheep: return Sheep;
                case GameModeType.Match3: return Match3;
                default: return null;
            }
        }

        public static string LevelsForMode(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return LianliankanLevels;
                case GameModeType.Sheep: return SheepLevels;
                case GameModeType.Match3: return Match3Levels;
                default: return null;
            }
        }

        public static string PrefixForMode(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return "Lianliankan/";
                case GameModeType.Sheep: return "Sheep/";
                case GameModeType.Match3: return "Match3/";
                default: return null;
            }
        }
    }
}
