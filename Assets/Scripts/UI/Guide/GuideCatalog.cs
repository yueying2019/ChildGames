using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 引导配置入口：优先用策划做的 Asset（GuideSequence.SetOverride），没有就退回内置定义。
    /// 各玩法通过 Key 上报，例如 Report(Match3, KeyFor(Match3GuideId.Ice))。
    /// </summary>
    public static class GuideCatalog
    {
        private static readonly Dictionary<GameModeType, GuideSequence> Overrides = new Dictionary<GameModeType, GuideSequence>();
        private static readonly Dictionary<GameModeType, GuideSequence> Builtins = new Dictionary<GameModeType, GuideSequence>();

        public static void SetOverride(GuideSequence sequence)
        {
            if (sequence == null)
                return;
            Overrides[sequence.Mode] = sequence;
        }

        public static void ClearOverrides()
        {
            Overrides.Clear();
        }

        public static GuideSequence Get(GameModeType mode)
        {
            if (Overrides.TryGetValue(mode, out var asset) && asset != null)
                return asset;
            if (Builtins.TryGetValue(mode, out var cached) && cached != null)
                return cached;

            var built = BuildBuiltin(mode);
            Builtins[mode] = built;
            return built;
        }

        public static GuideStep Resolve(GameModeType mode, string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            var sequence = Get(mode);
            return sequence != null ? sequence.Find(key) : null;
        }

        public static GuideStep[] ByTrigger(GameModeType mode, GuideTrigger trigger)
        {
            var sequence = Get(mode);
            return sequence != null ? sequence.ByTrigger(trigger) : Array.Empty<GuideStep>();
        }

        public static string KeyFor(Match3GuideId id)
        {
            return "match3." + id;
        }

        public static GuideStep[] All(GameModeType mode)
        {
            var sequence = Get(mode);
            return sequence != null && sequence.Steps != null ? sequence.Steps : Array.Empty<GuideStep>();
        }

        private static GuideStep[] BuildMatch3Steps()
        {
            var ids = (Match3GuideId[])Enum.GetValues(typeof(Match3GuideId));
            var steps = new GuideStep[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                steps[i] = new GuideStep
                {
                    Key = KeyFor(id),
                    Trigger = TriggerFor(id),
                    Title = Match3GuideCatalog.Title(id),
                    Desc = Match3GuideCatalog.Desc(id),
                    ShowLineArrows = Match3GuideCatalog.ShowLineArrows(id),
                    HighlightTarget = TargetFor(id),
                    Shape = ShapeFor(id)
                };
            }

            return steps;
        }

        private static string TargetFor(Match3GuideId id)
        {
            switch (id)
            {
                case Match3GuideId.ExtraMoves:
                    return GuideTargets.Moves;
                case Match3GuideId.GoldBean:
                    return GuideTargets.Goal;
                default:
                    return GuideTargets.Board;
            }
        }

        private static GuideHighlightShape ShapeFor(Match3GuideId id)
        {
            switch (id)
            {
                case Match3GuideId.ExtraMoves:
                case Match3GuideId.GoldBean:
                    return GuideHighlightShape.Circle;
                default:
                    return GuideHighlightShape.RoundRect;
            }
        }

        private static GuideTrigger TriggerFor(Match3GuideId id)
        {
            switch (id)
            {
                case Match3GuideId.Line:
                case Match3GuideId.Bomb:
                case Match3GuideId.ColorBomb:
                    return GuideTrigger.SpecialMade;
                default:
                    return GuideTrigger.Opening;
            }
        }

        private static GuideSequence BuildBuiltin(GameModeType mode)
        {
            var steps = mode == GameModeType.Match3 ? BuildMatch3Steps() : Array.Empty<GuideStep>();
            var sequence = ScriptableObject.CreateInstance<GuideSequence>();
            sequence.Mode = mode;
            sequence.Steps = steps;
            sequence.hideFlags = HideFlags.HideAndDontSave;
            sequence.name = "Guide_" + mode;
            return sequence;
        }
    }
}
