using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class EnergyPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Button BuyOneButton;
        public Button BuyFullButton;
        public Text TitleText;
        public Text EnergyText;
        public Text TimerText;
        public Text CoinText;
        public Text OnePriceText;
        public Text FullPriceText;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            Wire(BuyOneButton, BuyOne);
            Wire(BuyFullButton, BuyFull);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            EnergyRepository.Changed += Refresh;
            CoinWallet.Changed += Refresh;
            Refresh();
        }

        public override void OnClose()
        {
            EnergyRepository.Changed -= Refresh;
            CoinWallet.Changed -= Refresh;
            base.OnClose();
        }

        private void OnDestroy()
        {
            EnergyRepository.Changed -= Refresh;
            CoinWallet.Changed -= Refresh;
        }

        private void Update()
        {
            if (!IsOpen)
                return;
            if (TimerText != null)
                TimerText.text = EnergyRepository.TimerLabel();
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.Energy);
        }

        private void BuyOne()
        {
            if (EnergyRepository.TryBuyOne())
            {
                Analytics.EnergyBuy(EnergyCatalog.BuyOneAmount, EnergyCatalog.BuyOnePrice);
                UIManager.Instance?.ShowToast("体力 +1");
                return;
            }

            UIManager.Instance?.ShowToast("金币不够，过关可获得金币");
        }

        private void BuyFull()
        {
            if (EnergyRepository.IsFull)
            {
                UIManager.Instance?.ShowToast("体力已满");
                return;
            }

            if (EnergyRepository.TryBuyFull())
            {
                Analytics.EnergyBuy(EnergyCatalog.Max, EnergyCatalog.BuyFullPrice);
                UIManager.Instance?.ShowToast("体力已补满");
                return;
            }

            UIManager.Instance?.ShowToast("金币不够，过关可获得金币");
        }

        private void Refresh()
        {
            if (TitleText != null)
                TitleText.text = EnergyRepository.Energy > 0 ? "补充体力" : "体力不足";
            if (EnergyText != null)
                EnergyText.text = EnergyRepository.StatusLabel();
            if (TimerText != null)
                TimerText.text = EnergyRepository.TimerLabel();
            if (CoinText != null)
                CoinText.text = $"金币 {CoinWallet.Coins}";
            if (OnePriceText != null)
                OnePriceText.text = $"{EnergyCatalog.BuyOnePrice} 金币";
            if (FullPriceText != null)
                FullPriceText.text = $"{EnergyCatalog.BuyFullPrice} 金币";
        }
    }
}
