namespace SmallGames
{
    /// <summary>
    /// 引导高亮目标的通用 ID。GuideStep.HighlightTarget 填这里的常量，
    /// 运行时由玩法侧的 resolver 翻译成场景里的 RectTransform。
    /// 玩法私有的目标可以直接写别的字符串，只要 resolver 认就行。
    /// </summary>
    public static class GuideTargets
    {
        public const string None = "";
        public const string Board = "board";
        public const string Moves = "moves";
        public const string Goal = "goal";
        public const string Tools = "tools";
        public const string Pause = "pause";
        public const string Hand = "hand";
        public const string Slot = "slot";
        public const string Timer = "timer";
    }
}
