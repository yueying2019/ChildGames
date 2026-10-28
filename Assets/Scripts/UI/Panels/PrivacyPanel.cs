using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class PrivacyPanel : UIPanel
    {
        public Button AgreeButton;
        public Button DisagreeButton;
        public Button PrivacyLinkButton;
        public Button AgreementLinkButton;
        public Toggle AgreeToggle;
        public Image AgreeToggleBox;
        public Text AgreeToggleState;
        public Text TitleText;
        public Text BodyText;

        public override void BindListeners()
        {
            base.BindListeners();
            if (TitleText != null)
                TitleText.text = "用户隐私保护提示";
            if (BodyText != null)
            {
                BodyText.text = ComplianceCatalog.PrivacyPopupBody;
                BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                BodyText.verticalOverflow = VerticalWrapMode.Overflow;
            }
            Wire(AgreeButton, Agree);
            Wire(DisagreeButton, Disagree);
            Wire(PrivacyLinkButton, () => GameBootstrap.OpenLegal(LegalDocId.PrivacyPolicy));
            Wire(AgreementLinkButton, () => GameBootstrap.OpenLegal(LegalDocId.UserAgreement));
            if (AgreeToggle != null)
            {
                AgreeToggle.onValueChanged.RemoveAllListeners();
                AgreeToggle.onValueChanged.AddListener(OnToggle);
            }
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            if (AgreeToggle != null)
                AgreeToggle.SetIsOnWithoutNotify(false);
            if (AgreeToggle != null && AgreeToggle.graphic != null)
                AgreeToggle.graphic.enabled = false;
            ApplyToggleVisual(false);
            SetAgreeEnabled(false);
            Analytics.Track("privacy_show");
        }

        private void OnToggle(bool on)
        {
            ApplyToggleVisual(on);
            SetAgreeEnabled(on);
        }

        private void ApplyToggleVisual(bool on)
        {
            if (AgreeToggleBox != null)
                AgreeToggleBox.color = on ? UIStyle.TabSelect : UIStyle.None;
            if (AgreeToggleState != null)
                AgreeToggleState.text = on ? "已勾选" : "未勾选";
        }

        private void SetAgreeEnabled(bool on)
        {
            if (AgreeButton != null)
                AgreeButton.interactable = on;
        }

        private void Agree()
        {
            if (AgreeToggle == null || !AgreeToggle.isOn)
            {
                UIManager.Instance?.ShowToast("请先勾选同意协议");
                return;
            }

            PrivacyRepository.Agree();
            Analytics.Track("privacy_agree");
            GameBootstrap.EnterMainMenu(true);
        }

        private void Disagree()
        {
            Analytics.Track("privacy_disagree");
            UIManager.Instance?.ShowToast("需同意后才能进入游戏");
        }
    }
}
