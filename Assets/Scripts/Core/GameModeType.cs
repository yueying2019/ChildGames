namespace SmallGames
{
    /// <summary>
    /// 游戏模式。后续新增玩法时在此扩展。
    /// </summary>
    public enum GameModeType
    {
        None = 0,
        Lianliankan = 1,
        Sheep = 2,
        Match3 = 3,
        Jieqi = 4,
        SkillXiangqi = 5
    }

    public readonly struct GameModeInfo
    {
        public GameModeType Type { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string AccentHex { get; }

        public GameModeInfo(GameModeType type, string title, string subtitle, string accentHex)
        {
            Type = type;
            Title = title;
            Subtitle = subtitle;
            AccentHex = accentHex;
        }
    }

    public static class GameModeCatalog
    {
        public static readonly GameModeInfo Lianliankan = new(
            GameModeType.Lianliankan,
            "对对消",
            "相同图案两两相连即可消除",
            "#F0A05A");

        public static readonly GameModeInfo Sheep = new(
            GameModeType.Sheep,
            "叠叠消",
            "层层叠叠，三个相同即可消除",
            "#6BCB77");

        public static readonly GameModeInfo Match3 = new(
            GameModeType.Match3,
            "三消",
            "滑动交换相邻两格，三个相同即可消除",
            "#5BA3E8");

        public static readonly GameModeInfo Jieqi = new(
            GameModeType.Jieqi,
            "新规揭棋",
            "除将帅外全部打乱",
            "#C45C26");

        public static readonly GameModeInfo SkillXiangqi = new(
            GameModeType.SkillXiangqi,
            "技能象棋",
            "吃子学会对方走法",
            "#8B3A2F");

        public static GameModeInfo Get(GameModeType type)
        {
            switch (type)
            {
                case GameModeType.Lianliankan: return Lianliankan;
                case GameModeType.Sheep: return Sheep;
                case GameModeType.Match3: return Match3;
                case GameModeType.Jieqi: return Jieqi;
                case GameModeType.SkillXiangqi: return SkillXiangqi;
                default: return new GameModeInfo(GameModeType.None, "未知", string.Empty, "#FFFFFF");
            }
        }
    }
}
