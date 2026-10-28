using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 一个玩法的完整引导流程。做成 Asset 后策划可以直接改文案和触发条件，不用动代码。
    /// 菜单：Assets/Create/SmallGames/引导流程
    /// </summary>
    [CreateAssetMenu(fileName = "GuideSequence", menuName = "SmallGames/引导流程", order = 30)]
    public class GuideSequence : ScriptableObject
    {
        public GameModeType Mode = GameModeType.Match3;
        public GuideStep[] Steps = Array.Empty<GuideStep>();

        public GuideStep Find(string key)
        {
            if (string.IsNullOrEmpty(key) || Steps == null)
                return null;

            for (var i = 0; i < Steps.Length; i++)
            {
                if (Steps[i] != null && string.Equals(Steps[i].Key, key, StringComparison.Ordinal))
                    return Steps[i];
            }

            return null;
        }

        public GuideStep[] ByTrigger(GuideTrigger trigger)
        {
            if (Steps == null)
                return Array.Empty<GuideStep>();

            var result = new System.Collections.Generic.List<GuideStep>(Steps.Length);
            for (var i = 0; i < Steps.Length; i++)
            {
                if (Steps[i] != null && Steps[i].IsValid && Steps[i].Trigger == trigger)
                    result.Add(Steps[i]);
            }

            return result.ToArray();
        }
    }
}
