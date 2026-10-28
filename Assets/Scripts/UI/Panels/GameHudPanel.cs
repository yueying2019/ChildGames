using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class GameHudPanel : UIPanel
    {
        public Text TitleText;
        public Text StatusText;
        public Button PauseButton;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(PauseButton, () => GameManager.Instance.PauseGame());
        }

        /// <summary>进玩法就收掉 Banner：它是原生层，会盖住游戏画面，且遮挡底部按钮属违规。</summary>
        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            AdDirector.HideBanner();
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
