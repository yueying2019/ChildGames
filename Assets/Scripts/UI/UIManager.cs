using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class UIManager : SingletonBehaviour<UIManager>
    {
        private readonly Dictionary<UIPanelId, UIPanel> _panels = new Dictionary<UIPanelId, UIPanel>();
        private readonly Dictionary<UIPanelId, PanelRecipe> _recipes = new Dictionary<UIPanelId, PanelRecipe>();
        private readonly List<UIPanelId> _overlayStack = new List<UIPanelId>();
        private readonly Dictionary<UIPanelId, Func<bool>> _backOverrides = new Dictionary<UIPanelId, Func<bool>>();

        /// <summary>
        /// 面板接管返回键的优先级，数值越大越先响应。
        /// 顺序刻意对齐旧版 GameManager.HandleBack 的 if 判定链，别随意调整相对大小。
        /// </summary>
        private static readonly Dictionary<UIPanelId, int> BackPriorities = new Dictionary<UIPanelId, int>
        {
            { UIPanelId.Result, 100 },
            { UIPanelId.Loading, 95 },
            { UIPanelId.Legal, 90 },
            { UIPanelId.ModeRules, 85 },
            { UIPanelId.Privacy, 80 },
            { UIPanelId.Splash, 80 },
            { UIPanelId.Guide, 75 },
            { UIPanelId.Energy, 70 },
            { UIPanelId.DailyLogin, 65 },
            { UIPanelId.DailyTask, 58 },
            { UIPanelId.Continue, 60 },
            { UIPanelId.Shop, 55 },
            { UIPanelId.Codex, 52 },
            { UIPanelId.LevelSelect, 45 },
            { UIPanelId.Settings, 40 },
            { UIPanelId.TestWindow, 46 },
            { UIPanelId.DevWindow, 47 }
        };

        /// <summary>
        /// 实例化开销偏大的面板，提前建好放那儿，避免首次点开时一次性卡顿。
        /// 资源本身在启动时已随 AssetLabels.Core 异步预加载，这里摊的是 Instantiate + BindListeners 的开销。
        /// </summary>
        private static readonly UIPanelId[] HeavyPanels =
        {
            UIPanelId.Shop,
            UIPanelId.LevelSelect,
            UIPanelId.DailyLogin,
            UIPanelId.DailyTask,
            UIPanelId.Settings,
            UIPanelId.Result
        };

        private UiRootView _root;
        private Text _toastText;
        private CanvasGroup _toastGroup;
        private Coroutine _toastRoutine;
        private Coroutine _warmupRoutine;

        public RectTransform GameplayRoot => GetLayer(UILayer.Gameplay);

        public void Initialize()
        {
            UIViewFactory.EnsureEventSystem();
            _root = UiPrefabs.Instantiate<UiRootView>(UiPrefabs.Root, transform, false);
            if (_root == null)
            {
                Debug.LogError("[UI] 未找到 UIRoot 预制体，界面无法启动。");
                return;
            }

            _toastText = _root.ToastText;
            _toastGroup = _root.ToastGroup;
            UIStyle.ApplyTextStyle(_root);
            RegisterBuiltInPanels();
        }

        public RectTransform GetLayer(UILayer layer)
        {
            return _root != null ? _root.GetLayer(layer) : null;
        }

        public T Get<T>(UIPanelId id) where T : UIPanel
        {
            return _panels.TryGetValue(id, out var panel) ? panel as T : null;
        }

        public bool IsOpen(UIPanelId id)
        {
            return _panels.TryGetValue(id, out var panel) && panel.IsOpen;
        }

        public void Open(UIPanelId id, object payload = null)
        {
            var panel = EnsurePanel(id);
            if (panel == null)
            {
                Debug.LogError($"[UI] 未注册面板: {id}");
                return;
            }

            if (panel.IsOverlay)
            {
                if (!_overlayStack.Contains(id))
                    _overlayStack.Add(id);
                panel.transform.SetAsLastSibling();
            }

            panel.OnOpen(payload);
            PushBack(id);
        }

        public void Close(UIPanelId id)
        {
            if (!_panels.TryGetValue(id, out var panel) || !panel.IsOpen)
            {
                _overlayStack.Remove(id);
                PopBack(id);
                return;
            }

            panel.OnClose();
            _overlayStack.Remove(id);
            PopBack(id);
        }

        public bool CloseTopOverlay()
        {
            for (var i = _overlayStack.Count - 1; i >= 0; i--)
            {
                var id = _overlayStack[i];
                if (_panels.TryGetValue(id, out var panel) && panel.IsOpen)
                {
                    Close(id);
                    return true;
                }

                _overlayStack.RemoveAt(i);
            }

            return false;
        }

        public void CloseAllOverlays()
        {
            for (var i = _overlayStack.Count - 1; i >= 0; i--)
                Close(_overlayStack[i]);
            _overlayStack.Clear();
        }

        private const string BackKeyPrefix = "ui.panel.";

        /// <summary>
        /// 给面板指定返回键行为。返回 true 表示已消费这次返回（可以吞掉返回键，也可以做自定义退出）。
        /// 传 null 恢复成默认的「直接关闭」。
        /// </summary>
        public void SetBackOverride(UIPanelId id, Func<bool> handler)
        {
            if (handler == null)
                _backOverrides.Remove(id);
            else
                _backOverrides[id] = handler;
        }

        /// <summary>面板打开时把它登记进返回栈；不在优先级表里的面板不接管返回键。</summary>
        private void PushBack(UIPanelId id)
        {
            if (!BackPriorities.TryGetValue(id, out var priority))
                return;

            BackStack.Push(BackKey(id), priority, () => HandleBackDefault(id));
        }

        private void PopBack(UIPanelId id)
        {
            BackStack.Pop(BackKey(id));
        }

        private bool HandleBackDefault(UIPanelId id)
        {
            if (_backOverrides.TryGetValue(id, out var handler) && handler != null)
                return handler();

            Close(id);
            return true;
        }

        private static string BackKey(UIPanelId id)
        {
            return BackKeyPrefix + (int)id;
        }

        /// <summary>
        /// 后台分帧预热大面板：一帧建一个，玩家基本感知不到。重复调用安全，已在跑就跳过。
        /// 只实例化不显示，面板自己的 OnOpen 会按 payload 再刷新一次内容。
        /// </summary>
        public void WarmupHeavyPanels()
        {
            if (_warmupRoutine != null)
                return;

            _warmupRoutine = StartCoroutine(WarmupHeavyPanelsRoutine());
        }

        private IEnumerator WarmupHeavyPanelsRoutine()
        {
            for (var i = 0; i < HeavyPanels.Length; i++)
            {
                EnsurePanel(HeavyPanels[i]);
                yield return null;
            }

            _warmupRoutine = null;
        }

        public void ShowToast(string message, float duration = 1.6f)
        {
            if (_toastText == null)
                return;

            _toastText.text = message;
            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(duration));
        }

        private IEnumerator ToastRoutine(float duration)
        {
            UITween.KillTarget(_toastGroup);
            _toastGroup.alpha = 1f;
            yield return new WaitForSeconds(duration);

            var fade = UITween.Fade(_toastGroup, 1f, 0f, 0.25f, EaseKind.Linear);
            if (fade != null)
            {
                fade.SetUnscaled(true);
                yield return fade.WaitForCompletion();
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        private void RegisterBuiltInPanels()
        {
            Register<MainMenuPanel>(UIPanelId.MainMenu, UILayer.Normal, false, UiPrefabs.MainMenu);
            Register<SettingsPanel>(UIPanelId.Settings, UILayer.Popup, true, UiPrefabs.Settings);
            Register<GameHudPanel>(UIPanelId.GameHud, UILayer.Hud, false, UiPrefabs.GameHud);
            Register<PausePanel>(UIPanelId.Pause, UILayer.Popup, true, UiPrefabs.Pause);
            Register<ResultPanel>(UIPanelId.Result, UILayer.Popup, true, UiPrefabs.Result);
            Register<ShopPanel>(UIPanelId.Shop, UILayer.Popup, true, UiPrefabs.Shop);
            Register<LevelSelectPanel>(UIPanelId.LevelSelect, UILayer.Popup, true, UiPrefabs.LevelSelect);
            Register<ContinuePanel>(UIPanelId.Continue, UILayer.Popup, true, UiPrefabs.Continue);
            Register<DailyLoginPanel>(UIPanelId.DailyLogin, UILayer.Popup, true, UiPrefabs.DailyLogin);
            Register<DailyTaskPanel>(UIPanelId.DailyTask, UILayer.Popup, true, UiPrefabs.DailyTask);
            Register<EnergyPanel>(UIPanelId.Energy, UILayer.Popup, true, UiPrefabs.Energy);
            Register<GuidePanel>(UIPanelId.Guide, UILayer.Popup, true, UiPrefabs.Guide);
            Register<SplashPanel>(UIPanelId.Splash, UILayer.Normal, false, UiPrefabs.Splash);
            Register<PrivacyPanel>(UIPanelId.Privacy, UILayer.Popup, true, UiPrefabs.Privacy);
            Register<LegalPanel>(UIPanelId.Legal, UILayer.Popup, true, UiPrefabs.Legal);
            Register<LoadingPanel>(UIPanelId.Loading, UILayer.Popup, true, UiPrefabs.Loading);
            Register<LevelIntroPanel>(UIPanelId.LevelIntro, UILayer.Popup, true, UiPrefabs.LevelIntro);
            Register<ModeRulesPanel>(UIPanelId.ModeRules, UILayer.Popup, true, UiPrefabs.ModeRules);
            Register<TestWindowPanel>(UIPanelId.TestWindow, UILayer.Popup, true, UiPrefabs.TestWindow);
            Register<DevWindowPanel>(UIPanelId.DevWindow, UILayer.Popup, true, UiPrefabs.DevWindow);
            Register<CodexPanel>(UIPanelId.Codex, UILayer.Popup, true, UiPrefabs.Codex);
        }

        private void Register<T>(UIPanelId id, UILayer layer, bool overlay, string path) where T : UIPanel
        {
            _recipes[id] = new PanelRecipe
            {
                Layer = layer,
                Overlay = overlay,
                Path = path,
                Create = parent => UiPrefabs.Instantiate<T>(path, parent)
            };
        }

        private UIPanel EnsurePanel(UIPanelId id)
        {
            if (_panels.TryGetValue(id, out var existing) && existing != null)
                return existing;
            if (!_recipes.TryGetValue(id, out var recipe))
                return null;

            var parent = GetLayer(recipe.Layer);
            var panel = recipe.Create(parent);
            if (panel == null)
                return null;

            panel.Bind(id, recipe.Layer, recipe.Overlay);
            panel.BindListeners();
            UIClickSound.BindHierarchy(panel.gameObject);
            panel.gameObject.SetActive(false);
            _panels[id] = panel;
            return panel;
        }

        private sealed class PanelRecipe
        {
            public UILayer Layer;
            public bool Overlay;
            public string Path;
            public Func<RectTransform, UIPanel> Create;
        }
    }
}
