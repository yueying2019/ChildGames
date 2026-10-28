using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 游戏流程中枢：开局、暂停、重开、返回主界面。
    /// </summary>
    public class GameManager : SingletonBehaviour<GameManager>
    {
        public GameModeType CurrentMode { get; private set; } = GameModeType.None;
        public IGameSession CurrentSession { get; private set; }
        public bool IsInGame => CurrentSession != null;
        public bool IsPaused => CurrentSession != null && CurrentSession.IsPaused;

        private bool _entering;
        private bool _backRulesReady;

        public event Action<GameModeType> GameStarted;
        public event Action ReturnedToMenu;
        public event Action GamePaused;
        public event Action GameResumed;

        public void Initialize()
        {
        }

        public void OpenLevelSelect(GameModeType mode)
        {
            if (mode == GameModeType.None || _entering)
                return;
            UIManager.Instance.Open(UIPanelId.LevelSelect, mode);
        }

        public void StartGame(GameModeType mode)
        {
            StartGame(mode, 0);
        }

        public void OpenDailyLogin()
        {
            UIManager.Instance.Open(UIPanelId.DailyLogin);
        }

        public void OpenEnergy()
        {
            UIManager.Instance.Open(UIPanelId.Energy);
        }

        /// <summary>直接开一局教学关（不扣体力）。</summary>
        public void StartTutorial(GameModeType mode)
        {
            StartGame(mode, TutorialCatalog.Index);
        }

        public void StartGame(GameModeType mode, int levelIndex)
        {
            if (mode == GameModeType.None || _entering)
                return;
            // 教学关不扣体力：新手第一步就被体力卡住最劝退。
            if (!TutorialCatalog.IsIndex(levelIndex) && !TrySpendEnergy())
                return;

            _entering = true;
            CloseMenusForGame();
            DisposeSession();
            BeginSession(mode, levelIndex);
            _entering = false;
        }

        private static void CloseMenusForGame()
        {
            UIManager.Instance.Close(UIPanelId.MainMenu);
            UIManager.Instance.Close(UIPanelId.Settings);
            UIManager.Instance.Close(UIPanelId.Shop);
            UIManager.Instance.Close(UIPanelId.Pause);
            UIManager.Instance.Close(UIPanelId.Result);
            UIManager.Instance.Close(UIPanelId.LevelSelect);
            UIManager.Instance.Close(UIPanelId.Continue);
            UIManager.Instance.Close(UIPanelId.DailyLogin);
            UIManager.Instance.Close(UIPanelId.Energy);
            UIManager.Instance.Close(UIPanelId.Guide);
            UIManager.Instance.Close(UIPanelId.Splash);
            UIManager.Instance.Close(UIPanelId.Privacy);
            UIManager.Instance.Close(UIPanelId.Legal);
            UIManager.Instance.Close(UIPanelId.ModeRules);
        }

        private void BeginSession(GameModeType mode, int levelIndex)
        {
            CurrentMode = mode;

            var sessionGo = new GameObject($"{mode}Session", typeof(RectTransform));
            sessionGo.transform.SetParent(UIManager.Instance.GameplayRoot, false);

            GameSessionBase session;
            switch (mode)
            {
                case GameModeType.Lianliankan:
                    session = sessionGo.AddComponent<LianliankanSession>();
                    break;
                case GameModeType.Sheep:
                    session = sessionGo.AddComponent<SheepSession>();
                    break;
                case GameModeType.Match3:
                    session = sessionGo.AddComponent<Match3Session>();
                    break;
                default:
                    Destroy(sessionGo);
                    return;
            }

            var rect = sessionGo.GetComponent<RectTransform>();
            UIViewFactory.StretchFull(rect);
            session.Attach(rect);
            if (TutorialCatalog.IsIndex(levelIndex))
                session.JumpToTutorial();
            else if (DailyCatalog.IsIndex(levelIndex))
                session.JumpToDaily();
            else
                session.JumpToLevel(levelIndex);
            CurrentSession = session;

            var isTutorial = session.IsTutorial;
            var levelId = isTutorial
                ? TutorialCatalog.LevelId
                : session.IsDaily
                    ? LevelBrowse.LevelId(mode, session.LevelIndex)
                    : EndlessCatalog.IsIndex(mode, levelIndex)
                        ? EndlessCatalog.LevelId
                        : LevelBrowse.LevelId(mode, levelIndex);
            // 教学关不写「上次玩的关」，否则选关页会把第 0 关高亮成上次进度。
            // 每日挑战记在每日格子上，不把抽中的正式关标成上次。
            if (!isTutorial)
                ProgressRepository.SetLastPlayed(mode, session.IsDaily ? DailyCatalog.LevelId : levelId);

            if (mode != GameModeType.Lianliankan && mode != GameModeType.Sheep && mode != GameModeType.Match3)
                UIManager.Instance.Open(UIPanelId.GameHud);
            session.StartSession();
            // 玩法视图里也有按钮（暂停、道具等），视图建好后补一次点击音效绑定
            UIClickSound.BindHierarchy(sessionGo);
            Analytics.LevelStart(mode, levelId);
            GameStarted?.Invoke(mode);
            TryShowFirstModeRules(mode);
        }

        private void TryShowFirstModeRules(GameModeType mode)
        {
            if (mode == GameModeType.None || ModeRulesRepository.HasSeen(mode))
                return;
            // 教学关自己在教规则，再叠一层规则弹窗只会互相挡
            if (CurrentSession != null && CurrentSession.IsTutorial)
                return;
            CurrentSession?.Pause();
            UIManager.Instance.Open(UIPanelId.ModeRules, new ModeRulesPayload
            {
                Mode = mode,
                ResumeOnClose = true
            });
        }

        public void ResumeAfterModeRules()
        {
            if (!IsInGame || !IsPaused)
                return;
            if (UIManager.Instance.IsOpen(UIPanelId.Pause))
                return;
            CurrentSession.Resume();
            GameResumed?.Invoke();
        }

        public void ConfirmContinue()
        {
            var session = CurrentSession as GameSessionBase;
            if (session == null)
                return;
            if (!session.TryContinue())
            {
                UIManager.Instance?.ShowToast("金币不够，过关或去商城看看");
                return;
            }

            UIManager.Instance.Close(UIPanelId.Continue);
        }

        /// <summary>续关次数用尽后的「看广告复活」。看完才关闭面板并复活，中途退出就留在面板上。</summary>
        public void ConfirmAdContinue()
        {
            var session = CurrentSession as GameSessionBase;
            if (session == null)
                return;

            AdDirector.WatchRewarded(
                onRewarded: () =>
                {
                    UIManager.Instance.Close(UIPanelId.Continue);
                    session.AdContinue();
                },
                onUnavailable: () => UIManager.Instance?.ShowToast("广告还没准备好，稍后再试"),
                onAbandoned: () => UIManager.Instance?.ShowToast("要看完广告才能复活哦"));
        }

        public void DeclineContinue()
        {
            UIManager.Instance.Close(UIPanelId.Continue);
            (CurrentSession as GameSessionBase)?.GiveUp();
        }

        public void PauseGame()
        {
            if (!IsInGame || IsPaused)
                return;

            CurrentSession.Pause();
            UIManager.Instance.Open(UIPanelId.Pause);
            GamePaused?.Invoke();
        }

        public void ResumeGame()
        {
            if (!IsInGame || !IsPaused)
                return;

            UIManager.Instance.Close(UIPanelId.Pause);
            CurrentSession.Resume();
            GameResumed?.Invoke();
        }

        public void RestartGame(bool rollDaily = false)
        {
            if (!IsInGame)
                return;
            if (!TrySpendEnergy())
                return;

            UIManager.Instance.Close(UIPanelId.Pause);
            UIManager.Instance.Close(UIPanelId.Result);
            UIManager.Instance.Close(UIPanelId.Continue);
            UIManager.Instance.Close(UIPanelId.Guide);
            UIManager.Instance.Close(UIPanelId.ModeRules);
            if (rollDaily)
                (CurrentSession as GameSessionBase)?.RollNextDaily();
            CurrentSession.Restart();
        }

        public void StartNextLevel()
        {
            if (!IsInGame || CurrentSession == null || !CurrentSession.HasNextLevel)
                return;
            if (!TrySpendEnergy())
                return;

            UIManager.Instance.Close(UIPanelId.Pause);
            UIManager.Instance.Close(UIPanelId.Result);
            UIManager.Instance.Close(UIPanelId.Continue);
            UIManager.Instance.Close(UIPanelId.Guide);
            UIManager.Instance.Close(UIPanelId.ModeRules);
            CurrentSession.StartNextLevel();
        }

        public void ReturnToLevelSelect()
        {
            var mode = CurrentMode;
            ReturnToMenu(false);
            if (mode != GameModeType.None)
                OpenLevelSelect(mode);
        }

        public void ReturnToMenu()
        {
            ReturnToMenu(true);
        }

        public void ReturnToMenu(bool promptDaily)
        {
            DisposeSession();
            CurrentMode = GameModeType.None;

            UIManager.Instance.Close(UIPanelId.GameHud);
            UIManager.Instance.Close(UIPanelId.Pause);
            UIManager.Instance.Close(UIPanelId.Result);
            UIManager.Instance.Close(UIPanelId.Settings);
            UIManager.Instance.Close(UIPanelId.Shop);
            UIManager.Instance.Close(UIPanelId.LevelSelect);
            UIManager.Instance.Close(UIPanelId.Continue);
            UIManager.Instance.Close(UIPanelId.DailyLogin);
            UIManager.Instance.Close(UIPanelId.Energy);
            UIManager.Instance.Close(UIPanelId.Guide);
            UIManager.Instance.Close(UIPanelId.Splash);
            UIManager.Instance.Close(UIPanelId.Privacy);
            UIManager.Instance.Close(UIPanelId.Legal);
            UIManager.Instance.Close(UIPanelId.ModeRules);
            UIManager.Instance.Close(UIPanelId.Loading);
            UIManager.Instance.Open(UIPanelId.MainMenu);
            if (promptDaily && DailyLoginRepository.CanClaim)
                OpenDailyLogin();
            ReturnedToMenu?.Invoke();
        }

        public void OpenSettings()
        {
            UIManager.Instance.Open(UIPanelId.Settings);
        }

        public void OpenShop()
        {
            UIManager.Instance.Open(UIPanelId.Shop, CurrentMode);
        }

        /// <summary>
        /// 返回键。面板怎么响应交给 BackStack 里登记的处理项，这里只兜底游戏内的暂停与退出。
        /// </summary>
        public void HandleBack()
        {
            if (_entering)
                return;

            EnsureBackRules();
            if (BackStack.Handle())
                return;

            if (IsInGame && IsPaused)
            {
                ResumeGame();
                return;
            }

            if (IsInGame)
            {
                PauseGame();
                return;
            }

            GameBootstrap.QuitApp();
        }

        /// <summary>
        /// 面板返回规则：默认一律关闭，个别面板在这里覆盖成「吞掉返回键」或走业务退出流程。
        /// 延迟到首次按返回键才注册，避免和 UIManager 的初始化顺序打架。
        /// </summary>
        private void EnsureBackRules()
        {
            if (_backRulesReady)
                return;

            var ui = UIManager.Instance;
            if (ui == null)
                return;

            ui.SetBackOverride(UIPanelId.Result, SwallowBack);
            ui.SetBackOverride(UIPanelId.Loading, SwallowBack);
            ui.SetBackOverride(UIPanelId.Privacy, SwallowBack);
            ui.SetBackOverride(UIPanelId.Splash, SwallowBack);
            ui.SetBackOverride(UIPanelId.Continue, OnContinueBack);
            _backRulesReady = true;
        }

        /// <summary>吞掉这次返回键：什么都不做，但告诉调用方已经消费掉了。</summary>
        private static bool SwallowBack()
        {
            return true;
        }

        private bool OnContinueBack()
        {
            DeclineContinue();
            return true;
        }

        private bool TrySpendEnergy()
        {
            if (EnergyRepository.TrySpend(EnergyCatalog.CostPerPlay))
                return true;
            UIManager.Instance?.ShowToast("体力不足，等回复或去补充");
            OpenEnergy();
            return false;
        }

        private void DisposeSession()
        {
            if (CurrentSession == null)
                return;

            CurrentSession.Dispose();
            CurrentSession = null;
        }
    }
}
