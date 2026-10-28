using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class LegalPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Text TitleText;
        public Text BodyText;
        public ScrollRect Scroll;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            var doc = payload is LegalDocId id ? id : LegalDocId.PrivacyPolicy;
            if (payload is LegalPayload data)
                doc = data.Doc;

            if (TitleText != null)
                TitleText.text = ComplianceCatalog.TitleOf(doc);
            if (BodyText != null)
            {
                BodyText.text = ComplianceCatalog.BodyOf(doc);
                BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                BodyText.verticalOverflow = VerticalWrapMode.Overflow;
                LayoutRebuilder.ForceRebuildLayoutImmediate(BodyText.rectTransform);
            }

            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.Legal);
        }
    }
}
