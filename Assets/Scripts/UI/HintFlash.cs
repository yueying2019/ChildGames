using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 闲置提示用的闪烁：给一批 Graphic 做限时明暗呼吸，到期或被 Stop 时把颜色还原。
    /// 用 UITween 驱动，不额外起 Update；同一批 Graphic 重复触发会先还原再播，颜色不会叠。
    /// </summary>
    public static class HintFlash
    {
        public const float Period = 0.42f;
        public const float MinAlpha = 0.25f;
        public const float DefaultDuration = 3.2f;

        private static readonly List<Graphic> _active = new List<Graphic>(8);
        private static readonly Dictionary<Graphic, Color> _baseColors = new Dictionary<Graphic, Color>();
        private static bool _loop;

        public static bool Playing => _active.Count > 0;

        /// <summary>
        /// 一直闪到 Stop 为止（教学关高亮"该点这里"用）。
        /// 闲置提示那种几秒就结束的用 Play。
        /// </summary>
        public static void PlayLoop(IList<Graphic> graphics)
        {
            _loop = true;
            Play(graphics, Period * 2f);
        }

        /// <summary>闪烁一批图片。传进来的空引用会被跳过。</summary>
        public static void Play(IList<Graphic> graphics, float duration = DefaultDuration)
        {
            Stop();
            if (graphics == null)
                return;

            for (var i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                if (graphic == null)
                    continue;

                _active.Add(graphic);
                if (!_baseColors.ContainsKey(graphic))
                    _baseColors.Add(graphic, graphic.color);
                PlayOne(graphic, duration);
            }
        }

        /// <summary>停止闪烁并把颜色还原。tile 回池、重开局、结算时都要调，避免留下半透明的牌。</summary>
        public static void Stop()
        {
            _loop = false;
            if (_active.Count == 0)
                return;

            for (var i = 0; i < _active.Count; i++)
            {
                var graphic = _active[i];
                if (graphic == null)
                    continue;

                UITween.KillTarget(graphic);
                Color baseColor;
                if (_baseColors.TryGetValue(graphic, out baseColor))
                    graphic.color = baseColor;
            }

            _active.Clear();
            _baseColors.Clear();
        }

        /// <summary>某个对象要回池/销毁时先调用：如果它正在闪就立刻还原，避免池里留下半透明的牌。</summary>
        public static void RestoreIfPlaying(Graphic graphic)
        {
            if (graphic == null)
                return;

            var index = _active.IndexOf(graphic);
            if (index < 0)
                return;

            UITween.KillTarget(graphic);
            Color baseColor;
            if (_baseColors.TryGetValue(graphic, out baseColor))
                graphic.color = baseColor;
            _active.RemoveAt(index);
            _baseColors.Remove(graphic);
        }

        private static void PlayOne(Graphic graphic, float duration)
        {
            var baseColor = _baseColors[graphic];
            var cycles = Mathf.Max(1f, duration / Period);
            UITween.Custom(graphic, duration, u =>
            {
                if (graphic == null)
                    return;

                var pingpong = Mathf.PingPong(u * cycles, 1f);
                var alpha = Mathf.Lerp(MinAlpha, 1f, pingpong);
                graphic.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }, EaseKind.Linear).OnComplete(() =>
            {
                if (graphic != null)
                    graphic.color = baseColor;
                if (_loop && _active.Contains(graphic))
                    PlayOne(graphic, duration);
            });
        }
    }
}
