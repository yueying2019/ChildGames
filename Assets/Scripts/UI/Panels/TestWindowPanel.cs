using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class TestWindowPanel : UIPanel
    {
        public const string Password = "kazihe666";

        public Button DimButton;
        public Button CloseButton;
        public Button ConfirmButton;
        public InputField PasswordInput;
        public Text TitleText;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            Wire(ConfirmButton, Confirm);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            if (PasswordInput != null)
                PasswordInput.text = string.Empty;
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.TestWindow);
        }

        private void Confirm()
        {
            var text = PasswordInput != null ? PasswordInput.text : string.Empty;
            if (text != Password)
                return;

            CloseSelf();
            UIManager.Instance.Open(UIPanelId.DevWindow);
        }
    }
}
