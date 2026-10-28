using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class PausePanel : UIPanel
    {
        public Button ResumeButton;
        public Button RestartButton;
        public Button SettingsButton;
        public Button ShopButton;
        public Button MenuButton;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(ResumeButton, () => GameManager.Instance.ResumeGame());
            Wire(RestartButton, () => GameManager.Instance.RestartGame());
            Wire(SettingsButton, () => GameManager.Instance.OpenSettings());
            Wire(ShopButton, () => GameManager.Instance.OpenShop());
            Wire(MenuButton, () => GameManager.Instance.ReturnToMenu());
        }
    }
}
