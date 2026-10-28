using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class ContinuePanel : UIPanel
    {
        public Text TitleText;
        public Text MessageText;
        public Text BenefitText;
        public Text CoinText;
        public Text PriceText;
        public Button PayButton;
        public Button GiveUpButton;

        private bool _useAd;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(PayButton, OnPayClicked);
            Wire(GiveUpButton, () => GameManager.Instance.DeclineContinue());
        }

        private void OnPayClicked()
        {
            if (_useAd)
                GameManager.Instance.ConfirmAdContinue();
            else
                GameManager.Instance.ConfirmContinue();
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            CoinWallet.Changed += RefreshCoins;
            var data = payload as ContinuePayload;
            _useAd = data != null && data.UseAd;
            if (TitleText != null)
                TitleText.text = _useAd ? "看广告复活" : "继续挑战";
            if (MessageText != null)
                MessageText.text = data != null ? data.Message : "本局失败";
            if (BenefitText != null)
            {
                var benefit = data != null ? data.Benefit : string.Empty;
                var left = data != null ? data.ContinuesLeft : 0;
                if (_useAd)
                    BenefitText.text = string.IsNullOrEmpty(benefit)
                        ? "看完广告即可复活"
                        : $"看完广告即可复活\n{benefit}";
                else
                    BenefitText.text = string.IsNullOrEmpty(benefit)
                        ? $"还可续关 {left} 次"
                        : $"{benefit}\n还可续关 {left} 次";
            }

            if (PriceText != null)
                PriceText.text = _useAd ? "免费" : (data != null ? $"{data.Price} 金币" : string.Empty);
            var payLabel = PayButton != null ? PayButton.GetComponentInChildren<Text>() : null;
            if (payLabel != null)
                payLabel.text = _useAd ? "看广告" : "继续";
            RefreshCoins();
        }

        public override void OnClose()
        {
            CoinWallet.Changed -= RefreshCoins;
            base.OnClose();
        }

        private void OnDestroy()
        {
            CoinWallet.Changed -= RefreshCoins;
        }

        private void RefreshCoins()
        {
            if (CoinText != null)
                CoinText.text = $"金币 {CoinWallet.Coins}";
        }
    }
}
