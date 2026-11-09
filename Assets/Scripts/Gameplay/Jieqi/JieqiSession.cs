using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 新规揭棋对局。LevelIndex 0 是双人，1 是玩家执红对电脑，2 起是残局。
    /// </summary>
    public class JieqiSession : GameSessionBase
    {
        public override GameModeType Mode => SkillMatch ? GameModeType.SkillXiangqi : GameModeType.Jieqi;

        protected virtual bool SkillMatch => false;

        public override bool HasNextLevel =>
            JieqiEndgameCatalog.IsLevelIndex(LevelIndex) &&
            JieqiEndgameCatalog.IndexFromLevel(LevelIndex) + 1 < JieqiEndgameCatalog.Count;

        JieqiMatch _match;
        JieqiGameView _view;
        string _puzzleTitle;
        bool _finished;

        protected override void OnStart()
        {
            AudioManager.Instance?.HoldBgm();
            _finished = false;
            _match = new JieqiMatch();
            _puzzleTitle = null;
            if (SkillMatch)
            {
                _match.Engine.DealSkill();
                var versusAi = LevelIndex == 1;
                _match.StartOnCurrent(
                    versusAi ? JieqiPlayMode.VersusAi : JieqiPlayMode.TwoPlayer,
                    JieqiCamp.Red,
                    Random.Range(1, int.MaxValue));
            }
            else if (JieqiEndgameCatalog.IsLevelIndex(LevelIndex))
            {
                var index = JieqiEndgameCatalog.IndexFromLevel(LevelIndex);
                _puzzleTitle = JieqiEndgameCatalog.Title(index);
                JieqiEndgameCatalog.Load(_match.Engine, index);
                _match.StartOnCurrent(JieqiPlayMode.VersusAi, JieqiCamp.Red, Random.Range(1, int.MaxValue));
            }
            else
            {
                var versusAi = LevelIndex == 1;
                _match.Start(
                    versusAi ? JieqiPlayMode.VersusAi : JieqiPlayMode.TwoPlayer,
                    Random.Range(1, int.MaxValue),
                    JieqiCamp.Red);
            }

            _view = UiPrefabs.Instantiate<JieqiGameView>(UiPrefabs.JieqiView, Root);
            if (_view == null)
                return;

            _view.Changed += OnChanged;
            _view.Play(_match);
            _view.SetLocked(IsPaused);
            Sync();
        }

        protected override void OnRestart()
        {
            Unbind();
            base.OnRestart();
        }

        protected override void OnDispose()
        {
            Unbind();
            base.OnDispose();
        }

        protected override void OnPauseChanged(bool paused)
        {
            if (_view != null)
                _view.SetLocked(paused);
        }

        /// <summary>悔棋。对局已经结束时先收起结算，再把局面退回去。</summary>
        public void UndoMove()
        {
            if (IsPaused || _view == null)
                return;
            var finished = _finished;
            if (!_view.TryUndo())
            {
                UIManager.Instance?.ShowToast("还没有可悔的棋");
                return;
            }

            if (finished)
            {
                _finished = false;
                UIManager.Instance?.Close(UIPanelId.Result);
            }
        }

        public override void StartNextLevel()
        {
            if (!HasNextLevel)
                return;
            LevelIndex++;
            Restart();
        }

        protected override string GetStatusText()
        {
            if (_match == null)
                return "新规揭棋";
            var outcome = _match.Engine.Outcome;
            var head = string.IsNullOrEmpty(_puzzleTitle) ? null : _puzzleTitle;
            if (outcome == JieqiOutcome.Draw)
                return Join(head, "和棋");
            if (outcome == JieqiOutcome.RedWin)
                return Join(head, "红方胜");
            if (outcome == JieqiOutcome.BlackWin)
                return Join(head, "黑方胜");
            if (_match.AiToMove)
                return Join(head, "电脑在想");
            return Join(head, _match.Engine.Turn == JieqiCamp.Red ? "红方走" : "黑方走");
        }

        void OnChanged()
        {
            Sync();
            if (_finished || _match == null)
                return;
            var outcome = _match.Engine.Outcome;
            if (outcome == JieqiOutcome.Playing)
                return;

            _finished = true;
            if (_view != null)
                _view.SetLocked(true);

            var humanWon = _match.Mode == JieqiPlayMode.TwoPlayer
                ? outcome == JieqiOutcome.RedWin || outcome == JieqiOutcome.BlackWin
                : _match.HumanCamp == JieqiCamp.Red
                    ? outcome == JieqiOutcome.RedWin
                    : outcome == JieqiOutcome.BlackWin;
            // 揭棋的人机和残局赢了把体力补满。技能象棋、双人、和棋、输掉不算。
            var versusWin = humanWon && _match.Mode == JieqiPlayMode.VersusAi && !SkillMatch;
            var message = GetStatusText();
            if (versusWin)
            {
                FillEnergy();
                if (JieqiEndgameCatalog.IsLevelIndex(LevelIndex))
                    ProgressRepository.RecordJieqiEndgame(JieqiEndgameCatalog.IndexFromLevel(LevelIndex));
                var reward = "体力已回满（" + EnergyCatalog.Max + "/" + EnergyCatalog.Max + "）";
                message = string.IsNullOrEmpty(message) ? reward : message + "\n" + reward;
            }

            UIManager.Instance.Open(UIPanelId.Result, new ResultPayload
            {
                IsWin = humanWon,
                Mode = Mode,
                Message = message,
                HasNextLevel = HasNextLevel,
                Stars = humanWon ? StarCatalog.MaxStars : 0
            });
        }

        void Sync()
        {
            if (_view != null)
                _view.SetStatus(GetStatusText());
            RefreshHud();
        }

        /// <summary>人机或残局获胜后把体力补到上限。已经满了就只提示。</summary>
        static void FillEnergy()
        {
            var lack = EnergyCatalog.Max - EnergyRepository.Energy;
            if (lack > 0)
                EnergyRepository.Add(lack);
            UIManager.Instance?.ShowToast("体力已回满");
        }

        static string Join(string head, string body)
        {
            return string.IsNullOrEmpty(head) ? body : head + "  " + body;
        }

        void Unbind()
        {
            if (_view == null)
                return;
            _view.Changed -= OnChanged;
            _view = null;
        }
    }
}
