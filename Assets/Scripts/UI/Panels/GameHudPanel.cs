using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class GameHudPanel : UIPanel
    {
        public Text TitleText;
        public Text StatusText;
        public Button PauseButton;
        public Button RulesButton;
        public Button UndoButton;
        public Button HintButton;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(PauseButton, () => GameManager.Instance.PauseGame());
            Wire(RulesButton, ShowRules);
            Wire(UndoButton, UndoMove);
            Wire(HintButton, ShowHint);
        }

        /// <summary>进玩法就收掉 Banner：它是原生层，会盖住游戏画面，且遮挡底部按钮属违规。</summary>
        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            AdDirector.HideBanner();
            var mode = GameManager.Instance != null ? GameManager.Instance.CurrentMode : GameModeType.None;
            var jieqi = mode == GameModeType.Jieqi || mode == GameModeType.SkillXiangqi;
            if (RulesButton != null)
                RulesButton.gameObject.SetActive(jieqi);
            if (UndoButton != null)
                UndoButton.gameObject.SetActive(jieqi);
            if (HintButton != null)
                HintButton.gameObject.SetActive(jieqi);
        }

        void ShowHint()
        {
            var session = GameManager.Instance != null
                ? GameManager.Instance.CurrentSession as JieqiSession
                : null;
            if (session != null)
                session.ShowHint();
        }

        void UndoMove()
        {
            var session = GameManager.Instance != null
                ? GameManager.Instance.CurrentSession as JieqiSession
                : null;
            if (session != null)
                session.UndoMove();
        }

        void ShowRules()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsInGame)
                return;
            var mode = game.CurrentMode;
            if (mode != GameModeType.Jieqi && mode != GameModeType.SkillXiangqi)
                return;
            if (UIManager.Instance != null && UIManager.Instance.IsOpen(UIPanelId.ModeRules))
                return;
            game.CurrentSession?.Pause();
            UIManager.Instance.Open(UIPanelId.ModeRules, new ModeRulesPayload
            {
                Mode = mode,
                ResumeOnClose = true
            });
        }

        public void SetTitle(string title)
        {
            if (TitleText != null)
                TitleText.text = title;
        }

        public void SetStatus(string status)
        {
            if (StatusText != null)
                StatusText.text = status;
        }
    }
}
