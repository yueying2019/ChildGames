using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class ModeRulesPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Button OkButton;
        public Text TitleText;
        public Text BodyText;
        public ScrollRect Scroll;

        private bool _resumeOnClose;
        private GameModeType _mode;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            Wire(OkButton, CloseSelf);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            var data = payload as ModeRulesPayload;
            _mode = data != null ? data.Mode : GameModeType.None;
            _resumeOnClose = data == null || data.ResumeOnClose;

            if (TitleText != null)
                TitleText.text = ModeRulesCatalog.Title(_mode);
            if (BodyText != null)
            {
                BodyText.text = ModeRulesCatalog.Body(_mode);
                BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                BodyText.verticalOverflow = VerticalWrapMode.Overflow;
                LayoutRebuilder.ForceRebuildLayoutImmediate(BodyText.rectTransform);
            }

            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
        }

        public override void OnClose()
        {
            if (_mode != GameModeType.None)
                ModeRulesRepository.MarkSeen(_mode);
            var resume = _resumeOnClose;
            _resumeOnClose = false;
            _mode = GameModeType.None;
            base.OnClose();
            if (resume)
                GameManager.Instance?.ResumeAfterModeRules();
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.ModeRules);
        }
    }
}
