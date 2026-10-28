using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public enum EaseKind
    {
        Linear = 0,
        OutQuad = 1,
        OutCubic = 2,
        InOutQuad = 3,
        OutBack = 4,
        OutElastic = 5
    }

    /// <summary>
    /// 一段补间。拿它来 Kill / 挂回调，避免两段动画叠在同一个对象上互相打架。
    /// </summary>
    public sealed class TweenHandle
    {
        internal Coroutine Routine;
        internal UnityEngine.Object Target;
        internal Action<float> Apply;
        internal Func<float, float> EaseFn;
        internal float Delay;
        internal float Duration;
        internal bool Unscaled;
        internal Action OnDone;

        public bool IsDone { get; internal set; }
        public bool Killed { get; internal set; }

        /// <summary>在协程里等这段动画播完（含延迟），被 Kill 时也会立即跳出。</summary>
        public IEnumerator WaitForCompletion()
        {
            while (!IsDone)
                yield return null;
        }

        public TweenHandle SetDelay(float seconds)
        {
            Delay = Mathf.Max(0f, seconds);
            return this;
        }

        public TweenHandle SetUnscaled(bool on)
        {
            Unscaled = on;
            return this;
        }

        public TweenHandle OnComplete(Action callback)
        {
            OnDone = callback;
            return this;
        }

        public void Kill()
        {
            UITween.Kill(this);
        }
    }

    /// <summary>
    /// 统一 UI 补间层：替代各 View 里手写的 Time.deltaTime 插值协程。
    /// 用法：UITween.Fade(group, 0f, 1f, 0.25f).SetUnscaled(true).OnComplete(...)
    /// </summary>
    public static class UITween
    {
        private sealed class Host : MonoBehaviour
        {
        }

        private static Host _host;
        private static readonly List<TweenHandle> Active = new List<TweenHandle>(32);

        /// <summary>缓动求值，纯函数；t 会被夹紧到 0~1。</summary>
        public static float EaseValue(EaseKind ease, float t)
        {
            var x = t < 0f ? 0f : t > 1f ? 1f : t;
            switch (ease)
            {
                case EaseKind.OutQuad: return 1f - (1f - x) * (1f - x);
                case EaseKind.OutCubic:
                {
                    var inv = 1f - x;
                    return 1f - inv * inv * inv;
                }
                case EaseKind.InOutQuad:
                    return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
                case EaseKind.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
                }
                case EaseKind.OutElastic:
                {
                    if (x <= 0f) return 0f;
                    if (x >= 1f) return 1f;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * c4) + 1f;
                }
                default:
                    return x;
            }
        }

        /// <summary>通用入口：自行在 apply 里处理缓动后的 0~1 进度。</summary>
        public static TweenHandle Custom(UnityEngine.Object target, float duration, Action<float> apply, EaseKind ease = EaseKind.OutCubic)
        {
            if (apply == null)
                return null;

            var handle = new TweenHandle
            {
                Target = target,
                Apply = apply,
                EaseFn = u => EaseValue(ease, u),
                Duration = Mathf.Max(0f, duration)
            };

            Start(handle);
            return handle;
        }

        public static TweenHandle Move(RectTransform target, Vector2 from, Vector2 to, float duration, EaseKind ease = EaseKind.OutCubic)
        {
            if (target == null)
                return null;
            return Custom(target, duration, u => target.anchoredPosition = Vector2.LerpUnclamped(from, to, u), ease);
        }

        public static TweenHandle MoveLocal(Transform target, Vector3 from, Vector3 to, float duration, EaseKind ease = EaseKind.OutCubic)
        {
            if (target == null)
                return null;
            return Custom(target, duration, u => target.localPosition = Vector3.LerpUnclamped(from, to, u), ease);
        }

        public static TweenHandle Scale(Transform target, float from, float to, float duration, EaseKind ease = EaseKind.OutBack)
        {
            if (target == null)
                return null;
            return Custom(target, duration, u =>
            {
                var s = Mathf.LerpUnclamped(from, to, u);
                target.localScale = new Vector3(s, s, s);
            }, ease);
        }

        public static TweenHandle Fade(CanvasGroup group, float from, float to, float duration, EaseKind ease = EaseKind.OutCubic)
        {
            if (group == null)
                return null;
            return Custom(group, duration, u => group.alpha = Mathf.LerpUnclamped(from, to, u), ease);
        }

        public static TweenHandle FadeTo(Graphic graphic, float from, float to, float duration, EaseKind ease = EaseKind.OutCubic)
        {
            if (graphic == null)
                return null;
            var color = graphic.color;
            return Custom(graphic, duration, u =>
            {
                var a = Mathf.LerpUnclamped(from, to, u);
                graphic.color = new Color(color.r, color.g, color.b, a);
            }, ease);
        }

        /// <summary>先弹出再回落，做按钮/弹窗的"啵"一下。</summary>
        public static TweenHandle Pop(Transform target, float fromScale = 0.6f, float overshoot = 1.18f, float duration = 0.32f)
        {
            if (target == null)
                return null;
            return Custom(target, duration, u =>
            {
                var s = u < 0.35f
                    ? Mathf.LerpUnclamped(fromScale, overshoot, u / 0.35f)
                    : Mathf.LerpUnclamped(overshoot, 1f, (u - 0.35f) / 0.65f);
                target.localScale = new Vector3(s, s, s);
            }, EaseKind.Linear);
        }

        /// <summary>横向抖动，用于"不能这样操作"的反馈。</summary>
        public static TweenHandle Shake(RectTransform target, float strength = 12f, float duration = 0.32f)
        {
            if (target == null)
                return null;
            var home = target.anchoredPosition;
            return Custom(target, duration, u =>
            {
                var damp = 1f - u;
                target.anchoredPosition = home + Vector2.right * (Mathf.Sin(u * Mathf.PI * 6f) * strength * damp);
            }, EaseKind.Linear).OnComplete(() =>
            {
                if (target != null)
                    target.anchoredPosition = home;
            });
        }

        public static TweenHandle Delay(UnityEngine.Object target, float seconds, Action callback)
        {
            // 注意：StartCoroutine 会同步跑第一段，所以 Delay 必须在 Start 之前设好
            var handle = new TweenHandle
            {
                Target = target,
                Apply = _ => { },
                EaseFn = u => u,
                Duration = 0f,
                Delay = Mathf.Max(0f, seconds),
                OnDone = callback
            };

            Start(handle);
            return handle;
        }

        public static void Kill(TweenHandle handle)
        {
            if (handle == null || handle.Killed)
                return;

            handle.Killed = true;
            handle.IsDone = true;
            Active.Remove(handle);
            if (handle.Routine != null && _host != null)
                _host.StopCoroutine(handle.Routine);
            handle.OnDone = null;
        }

        /// <summary>同一个对象上的动画全部停掉（重复触发时先调这个）。</summary>
        public static void KillTarget(UnityEngine.Object target)
        {
            if (target == null)
                return;
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i].Target == target)
                    Kill(Active[i]);
            }
        }

        public static void KillAll()
        {
            for (var i = Active.Count - 1; i >= 0; i--)
                Kill(Active[i]);
        }

        private static void Start(TweenHandle handle)
        {
            var host = EnsureHost();
            if (host == null)
                return;

            Active.Add(handle);
            handle.Routine = host.StartCoroutine(Drive(handle));
        }

        private static IEnumerator Drive(TweenHandle handle)
        {
            if (handle.Delay > 0f)
            {
                if (handle.Unscaled)
                {
                    var wait = 0f;
                    while (wait < handle.Delay)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(handle.Delay);
                }
            }

            var elapsed = 0f;
            while (elapsed < handle.Duration)
            {
                elapsed += handle.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                if (handle.Target == null)
                    break;
                var u = handle.Duration <= 0f ? 1f : Mathf.Clamp01(elapsed / handle.Duration);
                handle.Apply(handle.EaseFn(u));
                yield return null;
            }

            if (!handle.Killed)
            {
                if (handle.Target != null)
                    handle.Apply(handle.EaseFn(1f));
                handle.IsDone = true;
            }

            Active.Remove(handle);
            var done = handle.OnDone;
            handle.OnDone = null;
            if (!handle.Killed && done != null)
                done.Invoke();
        }

        private static Host EnsureHost()
        {
            if (_host != null)
                return _host;

            var gameObject = new GameObject("[UITween]");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            _host = gameObject.AddComponent<Host>();
            return _host;
        }
    }
}
