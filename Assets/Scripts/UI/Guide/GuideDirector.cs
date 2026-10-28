using System;
using System.Collections;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 引导播放器：玩法只负责"上报发生了什么"，弹什么文案由配置决定。
    /// 用法：GuideDirector.TryShow(GameModeType.Sheep, "sheep.slot")；
    /// 需要等玩家看完再继续时用 yield return GuideDirector.Play(...)。
    /// </summary>
    public static class GuideDirector
    {
        public const float DefaultTimeout = 30f;

        public static bool HasSeen(GameModeType mode, string key)
        {
            return GuideRepository.HasSeen(mode, key);
        }

        /// <summary>上报一条引导。已看过 / 没配过就直接返回 false，玩法侧无需关心。</summary>
        public static bool TryShow(
            GameModeType mode,
            string key,
            Sprite icon = null,
            Func<string, RectTransform> targetResolver = null,
            Action closed = null)
        {
            if (string.IsNullOrEmpty(key) || UIManager.Instance == null)
                return false;
            if (GuideRepository.HasSeen(mode, key))
                return false;

            var step = GuideCatalog.Resolve(mode, key);
            if (step == null || !step.IsValid)
                return false;

            GuideRepository.MarkSeen(mode, key);
            UIManager.Instance.Open(UIPanelId.Guide, new GuidePayload
            {
                Id = LegacyIdOf(key),
                StepKey = step.Key,
                Mode = mode,
                Icon = icon,
                Title = step.Title,
                Desc = step.Desc,
                ShowLineArrows = step.ShowLineArrows,
                Shape = step.Shape,
                Target = ResolveTarget(step, targetResolver),
                Closed = closed
            });
            return true;
        }

        /// <summary>
        /// 只上报"发生了什么"（如 first-match / board-deadlock），由配置决定弹哪一步。
        /// 没配 Custom 步骤时，会退化成按 Key 直接找，所以两种用法可以并存。
        /// </summary>
        public static bool TryShowTrigger(
            GameModeType mode,
            string triggerKey,
            Sprite icon = null,
            Func<string, RectTransform> targetResolver = null,
            Action closed = null)
        {
            if (string.IsNullOrEmpty(triggerKey))
                return false;

            var customs = GuideCatalog.ByTrigger(mode, GuideTrigger.Custom);
            for (var i = 0; i < customs.Length; i++)
            {
                var step = customs[i];
                if (step == null || !step.MatchesTrigger(triggerKey))
                    continue;
                if (GuideRepository.HasSeen(mode, step.Key))
                    continue;
                return TryShow(mode, step.Key, icon, targetResolver, closed);
            }

            return TryShow(mode, triggerKey, icon, targetResolver, closed);
        }

        /// <summary>打开并等待玩家关掉，供开局连续放几条引导的协程使用。</summary>
        public static IEnumerator Play(
            GameModeType mode,
            string key,
            Sprite icon = null,
            Func<string, RectTransform> targetResolver = null)
        {
            if (!TryShow(mode, key, icon, targetResolver))
                yield break;
            yield return WaitUntilClosed();
        }

        /// <summary>带超时保护，避免面板意外没关导致流程卡死。</summary>
        public static IEnumerator WaitUntilClosed(float timeout = DefaultTimeout)
        {
            var waited = 0f;
            while (UIManager.Instance != null && UIManager.Instance.IsOpen(UIPanelId.Guide) && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static RectTransform ResolveTarget(GuideStep step, Func<string, RectTransform> resolver)
        {
            if (step == null || resolver == null || string.IsNullOrEmpty(step.HighlightTarget))
                return null;
            return resolver(step.HighlightTarget);
        }

        /// <summary>给旧的 GuidePayload.Id 一个兼容值，新的逻辑只看 StepKey。</summary>
        private static Match3GuideId LegacyIdOf(string key)
        {
            return Match3GuideCatalog.TryFromKey(key, out var parsed) ? parsed : Match3GuideId.Line;
        }
    }
}
