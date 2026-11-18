using UnityEngine;

namespace SmallGames
{
    public class LianliankanSession : GameSessionBase
    {
        public const int CustomLevelIndex = 3000;

        public static bool IsCustomIndex(int index) => index == CustomLevelIndex;

        public override GameModeType Mode => GameModeType.Lianliankan;
        public override bool HasNextLevel => !IsCustom && !IsEndless && !IsDaily && LianliankanLevelCatalog.Override == null && LevelIndex + 1 < LianliankanLevelCatalog.Count;
        protected override int CurrentLevelId => IsEndless ? EndlessCatalog.LevelId : (_level != null ? _level.LevelId : LevelIndex + 1);

        private bool IsCustom => IsCustomIndex(LevelIndex);

        private LianliankanGameView _view;
        private LianliankanLevelData _level;

        protected override void OnStart()
        {
            _level = IsCustom
                ? BuildCustomLevel()
                : IsTutorial
                    ? TutorialCatalog.LianliankanLevel()
                    : IsEndless
                        ? LianliankanEndlessGenerator.Build(Wave)
                        : LianliankanLevelCatalog.Get(LevelIndex);

            _view = UiPrefabs.Instantiate<LianliankanGameView>(UiPrefabs.LianliankanView, Root);
            _view?.CollectTiles();

            if (_view == null)
                return;

            _view.Won += OnWon;
            _view.Lost += OnLost;
            _view.Tutorial = IsTutorial;
            _view.Custom = IsCustom;
            _view.StartPlay(_level);
            if (IsCustom)
            {
                RefreshHud();
                BeginAfterIntro();
                return;
            }

            _view.InputLocked = true;
            RefreshHud();
            LevelIntroPanel.Present(IntroLevelNumber(_level != null ? _level.LevelId : 0), IntroTitle(), BeginAfterIntro);
        }

        private static LianliankanLevelData BuildCustomLevel()
        {
            var data = ScriptableObject.CreateInstance<LianliankanLevelData>();
            data.LevelId = 1;
            data.Title = "自定义";
            data.TypeCount = LianliankanLevelData.MaxTypes;
            data.ComboTimeBonus = 0f;
            data.EnsureInitialized();
            for (var r = 0; r < LianliankanLevelData.Rows; r++)
            {
                for (var c = 0; c < LianliankanLevelData.Cols; c++)
                    data.SetCell(r, c, LianliankanCellKind.Tile);
            }

            return data;
        }

        private void BeginAfterIntro()
        {
            if (_view == null)
                return;
            if (!IsCustom)
            {
                _view.BeginTimer(IsEndless
                    ? EndlessCatalog.LianStartSeconds
                    : (_level != null ? _level.TimeLimit : 180f));
            }
            _view.InputLocked = IsPaused;
            if (IsTutorial)
                _view.StartTutorialGuide();
        }

        private string IntroTitle()
        {
            if (IsEndless)
                return EndlessTitle;
            if (IsDaily)
                return DailyCatalog.Title(_level != null ? _level.Title : "对对消");
            if (_level != null && !string.IsNullOrEmpty(_level.Title))
                return _level.Title;
            return "对对消";
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
            _view = null;
            base.OnRestart();
        }

        protected override void OnDispose()
        {
            Unbind();
            LianliankanLevelCatalog.ClearOverride();
            base.OnDispose();
        }

        protected override void OnPauseChanged(bool paused)
        {
            if (_view == null)
                return;
            _view.SetTimerRunning(!paused);
            _view.InputLocked = paused;
        }

        protected override void RefreshHud()
        {
            if (_view == null)
            {
                base.RefreshHud();
                return;
            }

            var title = IsEndless
                ? (IsPaused ? EndlessTitle + "（暂停）" : EndlessTitle)
                : IsDaily
                    ? DailyCatalog.Title(_level != null ? _level.Title : "对对消")
                    : (_level != null ? _level.Title : "对对消");
            var id = _level != null ? _level.LevelId : 1;
            _view.SetLevel(id, IsPaused && !IsEndless ? "已暂停" : title);
        }

        /// <summary>对对消按剩余时间占比评星，连得越快越高。</summary>
        protected override int ComputeStars()
        {
            if (_view == null)
                return 1;
            return StarCatalog.FromRemaining(StarCatalog.Remaining(_view.TimeLeft, _view.TimeLimit));
        }

        protected override string StarConditionText()
        {
            if (_view == null || _view.TimeLimit <= 0f)
                return string.Empty;
            var limit = Mathf.RoundToInt(_view.TimeLimit);
            return $"剩余 {StarCatalog.ThreeStarSeconds(_view.TimeLimit)} 秒以上（共 {limit} 秒）";
        }

        protected override string StarProgressText()
        {
            if (_view == null || _view.TimeLimit <= 0f)
                return string.Empty;
            return $"本局剩余 {Mathf.Max(0, Mathf.RoundToInt(_view.TimeLeft))} 秒";
        }

        private void OnWon()
        {
            if (IsEndless)
            {
                BeginNextWave();
                return;
            }

            if (_view != null)
                _view.SetTimerRunning(false);
            var title = _level != null ? _level.Title : "对对消";
            var message = IsDaily || HasNextLevel ? $"{title} 已连通" : "全部关卡连通";
            var levelId = CurrentLevelId;
            if (_view != null)
                _view.CelebrateWin(() => ReportResult(true, message, levelId));
            else
                ReportResult(true, message, levelId);
        }

        private void BeginNextWave()
        {
            Wave++;
            _level = LianliankanEndlessGenerator.Build(Wave);
            if (_view != null)
                _view.AddTimerSeconds(EndlessCatalog.LianBonusSeconds, EndlessCatalog.LianMaxSeconds);
            StartCoroutine(ApplyNextWave());
        }

        private System.Collections.IEnumerator ApplyNextWave()
        {
            yield return null;
            if (_view == null)
                yield break;
            _view.StartPlay(_level);
            _view.SetTimerRunning(!IsPaused);
            _view.InputLocked = IsPaused;
            RefreshHud();
            AnnounceWave();
        }

        private void OnLost()
        {
            if (_view != null)
            {
                _view.SetTimerRunning(false);
                _view.InputLocked = true;
            }

            OfferContinue("时间到");
        }

        protected override void ApplyContinue()
        {
            if (_view == null)
                return;
            _view.AddTimerSeconds(ContinueCatalog.ExtraSeconds);
            _view.ResumeAfterContinue();
            _view.SetTimerRunning(!IsPaused);
            _view.InputLocked = IsPaused;
        }

        private void Unbind()
        {
            if (_view == null)
                return;
            _view.Won -= OnWon;
            _view.Lost -= OnLost;
        }
    }
}
