using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>步骤什么时候被触发。</summary>
    public enum GuideTrigger
    {
        Custom = 0,
        Opening = 1,
        FirstMatch = 2,
        SpecialMade = 3,
        ObstacleSeen = 4,
        LowResource = 5
    }

    public enum GuideHighlightShape
    {
        None = 0,
        RoundRect = 1,
        Circle = 2
    }

    /// <summary>
    /// 引导步骤配置：文案 + 触发条件 + 高亮目标。
    /// Key 是去重用的唯一标识（同一条引导只会弹一次）。
    /// </summary>
    [Serializable]
    public sealed class GuideStep
    {
        public string Key = string.Empty;

        public GuideTrigger Trigger = GuideTrigger.Opening;

        /// <summary>Trigger 为 Custom 时，玩法侧上报同样的 key。</summary>
        public string TriggerKey = string.Empty;

        public string Title = string.Empty;

        [TextArea(2, 5)]
        public string Desc = string.Empty;

        /// <summary>高亮目标 ID，运行时由玩法侧的 resolver 解析成 RectTransform。</summary>
        public string HighlightTarget = string.Empty;

        public GuideHighlightShape Shape = GuideHighlightShape.RoundRect;

        public bool ShowLineArrows;

        public bool IsValid => !string.IsNullOrEmpty(Key);

        public bool MatchesTrigger(string triggerKey)
        {
            if (Trigger != GuideTrigger.Custom)
                return false;
            return string.Equals(TriggerKey, triggerKey, StringComparison.Ordinal);
        }
    }
}
