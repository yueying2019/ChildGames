using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 强制引导状态机（教学关专用）。
    ///
    /// 分工：
    /// - Director 只管「当前第几步、允不允许这一步操作、什么时候进下一步」；
    /// - 显示交给 TutorialOverlay；
    /// - 棋盘坐标怎么变成 RectTransform 由玩法注册 resolver（框架不认识任何棋盘的坐标系）。
    ///
    /// 玩法侧用法：
    ///   开局   TutorialDirector.Start(Mode, ResolveTargets, ResolveGraphics)
    ///   操作时 if (!TutorialDirector.AllowsSwap(x0,y0,x1,y1)) { TutorialDirector.Reject(); return; }
    ///   完成   TutorialDirector.Notify(TutorialScript.Match3SwapDone)
    ///   结束   TutorialDirector.Stop()（退出对局 / 结算时必须调）
    /// </summary>
    public static class TutorialDirector
    {
        private static TutorialStep[] _steps = Array.Empty<TutorialStep>();
        private static int _index;
        private static TutorialOverlay _overlay;
        private static Func<int[], RectTransform[]> _targetResolver;
        private static Func<int[], Graphic[]> _graphicResolver;

        /// <summary>引导进行中。</summary>
        public static bool Active { get; private set; }

        public static GameModeType Mode { get; private set; } = GameModeType.None;

        public static TutorialStep Current => Active && _index >= 0 && _index < _steps.Length ? _steps[_index] : null;

        /// <summary>换步时触发，玩法可借此刷新自己的高亮。</summary>
        public static event Action<TutorialStep> StepChanged;

        /// <summary>整段走完或被跳过。</summary>
        public static event Action Finished;

        /// <summary>
        /// 开始引导。targetResolver 把 step.Highlight 的扁平坐标转成 RectTransform（挖洞用），
        /// graphicResolver 转成 Graphic（呼吸闪烁用），两者都可以返回 null/空表示这一步不挖洞。
        /// </summary>
        public static void Start(
            GameModeType mode,
            Func<int[], RectTransform[]> targetResolver,
            Func<int[], Graphic[]> graphicResolver = null)
        {
            Stop();
            Mode = mode;
            _steps = TutorialScript.Build(mode);
            _index = 0;
            _targetResolver = targetResolver;
            _graphicResolver = graphicResolver;
            if (_steps.Length == 0)
                return;

            Active = true;
            CreateOverlay();
            ApplyCurrent();
        }

        /// <summary>结束引导并清掉覆盖层。退出对局、结算、切场景都要调。</summary>
        public static void Stop()
        {
            CatMascotPlayer.DropAbove();
            HintFlash.Stop();
            if (_overlay != null)
            {
                Destroy(_overlay.gameObject);
                _overlay = null;
            }

            Active = false;
            Mode = GameModeType.None;
            _steps = Array.Empty<TutorialStep>();
            _index = 0;
            _targetResolver = null;
            _graphicResolver = null;
        }

        /// <summary>
        /// 上报「当前这一步要求的那件事做完了」。
        /// 玩法不用记住自己该发哪个信号，问当前步骤要即可。
        /// </summary>
        public static void NotifyCurrent()
        {
            var step = Current;
            if (step == null)
                return;
            Notify(step.Complete);
        }

        /// <summary>玩法上报一个信号，命中当前步骤的完成条件就进下一步。</summary>
        public static void Notify(string signal)
        {
            if (!Active || string.IsNullOrEmpty(signal))
                return;

            var step = Current;
            if (step == null || !step.Matches(signal))
                return;

            Advance();
        }

        /// <summary>跳过整段引导（不跳关，教学关继续玩，只是不再挡着）。</summary>
        public static void Skip()
        {
            if (!Active)
                return;

            var mode = Mode;
            Stop();
            TutorialCatalog.MarkCleared(mode);
            Finished?.Invoke();
        }

        /// <summary>单格操作是否允许（叠叠消点牌）。</summary>
        public static bool AllowsSingle(int cell)
        {
            var step = Current;
            return step == null || step.AllowsSingle(cell);
        }

        /// <summary>单个格子是否在允许列表里（对对消选第一张时用）。</summary>
        public static bool AllowsAny(int a0, int a1)
        {
            var step = Current;
            return step == null || step.AllowsAny(a0, a1);
        }

        /// <summary>两端操作是否允许（三消交换 / 对对消连线）。</summary>
        public static bool AllowsPair(int a0, int a1, int b0, int b1)
        {
            var step = Current;
            return step == null || step.AllowsPair(a0, a1, b0, b1);
        }

        /// <summary>玩家点了不允许的地方：抖一下文案提示。</summary>
        public static void Reject(string message = null)
        {
            if (!Active || _overlay == null)
                return;
            _overlay.ShowHint(string.IsNullOrEmpty(message) ? "先按提示操作这几张牌" : message);
        }

        /// <summary>棋盘重建后重挂高亮（牌被回收/重建时目标会失效）。</summary>
        public static void Refresh()
        {
            if (!Active)
                return;
            ApplyCurrent();
        }

        private static void Advance()
        {
            _index++;
            if (_index >= _steps.Length)
            {
                var mode = Mode;
                Stop();
                TutorialCatalog.MarkCleared(mode);
                Finished?.Invoke();
                return;
            }

            ApplyCurrent();
        }

        private static void ApplyCurrent()
        {
            var step = Current;
            if (step == null)
                return;

            var targets = Resolve(_targetResolver, step.Highlight);
            if (_overlay != null)
                _overlay.Apply(step, targets);

            HintFlash.Stop();
            if (step.Flash)
            {
                var graphics = Resolve(_graphicResolver, step.Highlight);
                if (graphics != null && graphics.Length > 0)
                    HintFlash.PlayLoop(graphics);
            }

            StepChanged?.Invoke(step);
        }

        private static T[] Resolve<T>(Func<int[], T[]> resolver, int[] cells) where T : class
        {
            if (resolver == null || cells == null || cells.Length == 0)
                return null;
            return resolver(cells);
        }

        private static void CreateOverlay()
        {
            var manager = UIManager.Instance;
            if (manager == null)
                return;

            var layer = manager.GetLayer(UILayer.Popup);
            if (layer == null)
                return;

            _overlay = TutorialOverlay.Attach(layer);
            if (_overlay == null)
                return;

            _overlay.OutsideClicked += OnOutsideClicked;
            _overlay.Skipped += Skip;
            CatMascotPlayer.LiftAbove(layer);
        }

        private static void OnOutsideClicked()
        {
            // 点洞外：如果这一步有操作限制，说明点错了，给提示；否则当作"继续"
            var step = Current;
            if (step != null && step.HasAllowed)
                Reject();
        }

        private static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
