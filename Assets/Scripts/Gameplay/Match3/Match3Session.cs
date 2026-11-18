using UnityEngine;

namespace SmallGames
{
    public class Match3Session : GameSessionBase
    {
        public const int CustomLevelIndex = 3000;

        public static bool IsCustomIndex(int index) => index == CustomLevelIndex;

        public override GameModeType Mode => GameModeType.Match3;
        public override bool HasNextLevel => !IsCustom && !IsEndless && !IsDaily && Match3LevelCatalog.Override == null && LevelIndex + 1 < Match3LevelCatalog.Count;

        private bool IsCustom => IsCustomIndex(LevelIndex);
        protected override int CurrentLevelId => IsEndless ? EndlessCatalog.LevelId : (_level != null ? _level.LevelId : LevelIndex + 1);

        private Match3Engine _engine;
        private Match3GameView _view;
        private Match3LevelData _level;

        protected override void OnStart()
        {
            _level = IsCustom
                ? BuildCustomLevel()
                : IsTutorial
                ? TutorialCatalog.Match3Level()
                : IsEndless
                    ? Match3EndlessGenerator.Build(Wave)
                    : Match3LevelCatalog.Get(LevelIndex);
            _engine = new Match3Engine();
            _engine.Custom = IsCustom;
            _engine.Load(_level);
            PrepareEndless();

            _view = UiPrefabs.Instantiate<Match3GameView>(UiPrefabs.Match3View, Root);
            if (_view == null)
                return;
            _view.Won += OnWon;
            _view.Lost += OnLost;
            _view.WaveCleared += OnWaveCleared;
            _view.Tutorial = IsTutorial;
            _view.Daily = IsDaily;
            _view.Custom = IsCustom;
            _view.Setup(_engine);
            _view.InputLocked = true;
            if (IsCustom)
            {
                BeginAfterIntro();
                return;
            }

            LevelIntroPanel.Present(IntroLevelNumber(_level != null ? _level.LevelId : 0), IntroTitle(), BeginAfterIntro);
        }

        private static Match3LevelData BuildCustomLevel()
        {
            var data = ScriptableObject.CreateInstance<Match3LevelData>();
            data.LevelId = 0;
            data.DisplayName = "自定义";
            data.Width = 8;
            data.Height = 8;
            data.ColorCount = Match3LevelData.MaxColor;
            data.MoveLimit = 99;
            data.GoalType = Match3GoalType.Score;
            data.TargetScore = 0;
            data.FixedBoard = true;
            data.FillPlayable(0);
            return data;
        }

        private void BeginAfterIntro()
        {
            if (_view == null)
                return;
            BeginLevelTimer();
            _view.InputLocked = IsPaused;
            if (IsTutorial)
                _view.StartTutorialGuide();
            else if (!IsCustom)
                _view.BeginOpeningGuides();
        }

        private string IntroTitle()
        {
            if (IsEndless)
                return EndlessTitle;
            if (IsDaily)
                return DailyCatalog.Title(_level != null ? _level.DisplayName : "三消");
            if (_level != null && !string.IsNullOrEmpty(_level.DisplayName))
                return _level.DisplayName;
            return "三消";
        }

        /// <summary>关卡配了 TimeLimit 才起倒计时，无尽模式沿用纯步数规则。</summary>
        private void BeginLevelTimer()
        {
            if (_view == null || _level == null || IsEndless)
                return;
            if (_level.TimeLimit > 0f)
                _view.BeginTimer(_level.TimeLimit);
        }

        public override void StartNextLevel()
        {
            if (!HasNextLevel)
                return;
            IsTutorial = false; // 从教学关「下一关」出来就是正式关，不关掉会反复进教学
            IsDaily = false;
            LevelIndex++;
            Restart();
        }

        protected override void OnRestart()
        {
            Unbind();
            base.OnRestart();
        }

        protected override void OnDispose()
        {
            Unbind();
            Match3LevelCatalog.ClearOverride();
            base.OnDispose();
        }

        protected override void OnPauseChanged(bool paused)
        {
            if (_view == null)
                return;
            _view.InputLocked = paused;
            _view.SetTimerRunning(!paused);
        }

        protected override void RefreshHud()
        {
            if (_view != null)
            {
                _view.RefreshHud();
                return;
            }

            base.RefreshHud();
        }

        protected override string GetStatusText()
        {
            return _engine != null ? _engine.GoalLabel() : "三消";
        }

        /// <summary>三消的特色指标：本局得分与生成的特效数。Engine 每局在 OnStart 里重建，所以这里直接取累计值即可。</summary>
        protected override void ReportDailyStats()
        {
            if (_engine == null)
                return;

            DailyTaskDirector.Report(Mode, DailyTaskSignal.Match3Score, _engine.Score);
            DailyTaskDirector.Report(Mode, DailyTaskSignal.Match3Special, _engine.SpecialsCreated);
        }

        /// <summary>评星口径的剩余步数：赢了用赢那刻的快照（动画会清零 MovesLeft），输了就是 0。</summary>
        private int StarsMovesLeft
        {
            get
            {
                if (_engine == null)
                    return 0;
                return _engine.MovesLeftAtWin > 0 ? _engine.MovesLeftAtWin : Mathf.Max(0, _engine.MovesLeft);
            }
        }

        /// <summary>三消按剩余步数占比评星，越省步越高。</summary>
        protected override int ComputeStars()
        {
            if (_engine == null || _level == null)
                return 1;
            if (IsTimeRated)
                return StarCatalog.FromRemaining(StarCatalog.Remaining(_view != null ? _view.TimeLeft : 0f, _level.TimeLimit));
            return StarCatalog.FromRemaining(StarCatalog.Remaining(StarsMovesLeft, _level.MoveLimit));
        }

        /// <summary>关卡配了倒计时就按剩余时间评星，否则按剩余步数。</summary>
        private bool IsTimeRated => _level != null && _level.TimeLimit > 0f && _view != null;

        protected override string StarConditionText()
        {
            if (_level == null)
                return string.Empty;
            return IsTimeRated
                ? $"剩余 {StarCatalog.ThreeStarSeconds(_level.TimeLimit)} 秒以上（共 {Mathf.RoundToInt(_level.TimeLimit)} 秒）"
                : $"剩余 {StarCatalog.ThreeStarMoves(_level.MoveLimit)} 步以上（共 {_level.MoveLimit} 步）";
        }

        protected override string StarProgressText()
        {
            if (_level == null)
                return string.Empty;
            if (IsTimeRated)
                return $"本局剩余 {Mathf.Max(0, Mathf.RoundToInt(_view.TimeLeft))} 秒";
            return _engine != null ? $"本局剩余 {StarsMovesLeft} 步" : string.Empty;
        }

        private void OnWon()
        {
            if (IsEndless)
            {
                OnWaveCleared();
                return;
            }

            var score = _engine != null ? _engine.Score : 0;
            var message = IsDaily || HasNextLevel ? $"得分 {score}" : $"全部通关  得分 {score}";
            var levelId = CurrentLevelId;
            if (_view != null)
                _view.CelebrateWin(() => ReportResult(true, message, levelId));
            else
                ReportResult(true, message, levelId);
        }

        private void OnWaveCleared()
        {
            if (!IsEndless)
                return;

            Wave++;
            var moves = (_engine != null ? _engine.MovesLeft : 0) + EndlessCatalog.Match3BonusMoves;
            _level = Match3EndlessGenerator.Build(Wave);
            _engine.Load(_level);
            _engine.Endless = true;
            _engine.SetMovesLeft(moves);
            PrepareEndless();
            if (_view == null)
                return;
            _view.ReloadBoard();
            if (IsPaused)
                _view.InputLocked = true;
            AnnounceWave();
        }

        private void PrepareEndless()
        {
            if (!IsEndless || _engine == null)
                return;

            _engine.Endless = true;
            _engine.ResetScore();
            if (_level == null || !_engine.GoalMet())
                return;
            _level.GoalType = Match3GoalType.Score;
            _level.TargetScore = GameBalance.Current.Match3EndlessScoreBase;
        }

        private void OnLost()
        {
            if (_view != null)
                _view.InputLocked = true;
            var goal = _engine != null ? _engine.GoalLabel() : string.Empty;
            var reason = _view != null && _view.TimedOut ? "时间到" : "步数用尽";
            OfferContinue(string.IsNullOrEmpty(goal) ? reason : $"{reason}\n{goal}");
        }

        protected override void ApplyContinue()
        {
            _engine?.AddMoves(ContinueCatalog.ExtraMoves);
            if (_view == null)
                return;
            _view.AddTimerSeconds(ContinueCatalog.Match3BonusSeconds);
            _view.ResumeAfterContinue();
            _view.InputLocked = IsPaused;
        }

        private void Unbind()
        {
            if (_view == null)
                return;
            _view.Won -= OnWon;
            _view.Lost -= OnLost;
            _view.WaveCleared -= OnWaveCleared;
        }
    }
}
