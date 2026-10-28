using System.Collections;
using UnityEngine;

namespace SmallGames
{
    public class SheepSession : GameSessionBase
    {
        public override GameModeType Mode => GameModeType.Sheep;
        public override bool HasNextLevel => !IsEndless && !IsDaily && SheepLevelCatalog.Override == null && LevelIndex + 1 < SheepLevelCatalog.Count;
        protected override int CurrentLevelId => IsEndless ? EndlessCatalog.LevelId : (_level != null ? _level.LevelId : LevelIndex + 1);

        public const int SlotCount = SheepGameView.SlotCount;
        private SheepGameView _view;
        private SheepLevelData _level;

        protected override void OnStart()
        {
            _level = IsTutorial
                ? TutorialCatalog.SheepLevel()
                : IsEndless
                    ? SheepEndlessGenerator.Build(Wave)
                    : SheepLevelCatalog.Get(LevelIndex);
            _view = UiPrefabs.Instantiate<SheepGameView>(UiPrefabs.SheepView, Root);

            if (_view == null)
                return;

            _view.Won += OnWon;
            _view.Lost += OnLost;
            _view.Tutorial = IsTutorial;
            _view.SetDate(IsEndless
                ? EndlessTitle
                : IsDaily
                    ? DailyCatalog.Title(_level != null ? _level.Title : "叠叠消")
                    : $"第{(_level != null ? _level.LevelId : 1)}关 {(_level != null ? _level.Title : "叠叠消")}");
            _view.StartPlay(_level);
            _view.InputLocked = true;
            LevelIntroPanel.Present(IntroLevelNumber(_level != null ? _level.LevelId : 0), IntroTitle(), BeginAfterIntro);
        }

        private void BeginAfterIntro()
        {
            if (_view == null)
                return;
            _view.InputLocked = IsPaused;
            if (IsTutorial)
                _view.StartTutorialGuide();
        }

        private string IntroTitle()
        {
            if (IsEndless)
                return EndlessTitle;
            if (IsDaily)
                return DailyCatalog.Title(_level != null ? _level.Title : "叠叠消");
            if (_level != null && !string.IsNullOrEmpty(_level.Title))
                return _level.Title;
            return "叠叠消";
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
            SheepLevelCatalog.ClearOverride();
            base.OnDispose();
        }

        protected override void OnPauseChanged(bool paused)
        {
            if (_view != null)
                _view.InputLocked = paused;
        }

        protected override void RefreshHud()
        {
            if (_view != null)
                return;
            base.RefreshHud();
        }

        /// <summary>
        /// 叠叠消评星。
        /// 常规按本局连击峰值 —— 通关时槽位必然清空，用槽位空余毫无区分度。
        /// 限时关改成看剩余时间占比，因为那时的主要难度已经从「记牌」变成「抢时间」。
        /// </summary>
        protected override int ComputeStars()
        {
            if (_view == null)
                return 1;
            if (_view.TimeLimit > 0f)
                return StarCatalog.FromRemaining(StarCatalog.Remaining(_view.TimeLeft, _view.TimeLimit));
            return StarCatalog.FromCombo(_view.ComboPeak);
        }

        protected override string StarConditionText()
        {
            if (_view == null)
                return string.Empty;
            if (_view.TimeLimit > 0f)
            {
                var limit = Mathf.RoundToInt(_view.TimeLimit);
                return $"剩余 {StarCatalog.ThreeStarSeconds(_view.TimeLimit)} 秒以上（共 {limit} 秒）";
            }

            return $"一次不落空地连消 {StarCatalog.ThreeStarCombo} 组以上";
        }

        protected override string StarProgressText()
        {
            if (_view == null)
                return string.Empty;
            if (_view.TimeLimit > 0f)
                return $"本局剩余 {Mathf.Max(0, Mathf.RoundToInt(_view.TimeLeft))} 秒";
            return $"本局最高连消 {Mathf.Max(0, _view.ComboPeak)} 组";
        }

        private void OnWon()
        {
            if (IsEndless)
            {
                BeginNextWave();
                return;
            }

            var title = _level != null ? _level.Title : "叠叠消";
            var message = IsDaily || HasNextLevel ? $"{title} 已清空" : "全部关卡消除";
            var levelId = CurrentLevelId;
            if (_view != null)
                _view.CelebrateWin(() => ReportResult(true, message, levelId));
            else
                ReportResult(true, message, levelId);
        }

        private void BeginNextWave()
        {
            Wave++;
            _level = SheepEndlessGenerator.Build(Wave);
            StartCoroutine(ApplyNextWave());
        }

        private IEnumerator ApplyNextWave()
        {
            yield return null;
            if (_view == null)
                yield break;
            _view.SetDate(EndlessTitle);
            _view.StartPlay(_level);
            _view.InputLocked = IsPaused;
            AnnounceWave();
        }

        private void OnLost()
        {
            if (_view != null)
                _view.InputLocked = true;
            OfferContinue(_view != null && _view.TimedOut ? "时间到了" : "槽位已满");
        }

        protected override void ApplyContinue()
        {
            if (_view == null)
                return;
            _view.ResumeAfterContinue();
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
