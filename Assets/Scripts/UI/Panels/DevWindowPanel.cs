using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class DevWindowPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Button ClearSaveButton;
        public Button CoinsButton;
        public Button UnlockButton;
        public Button AchievementButton;
        public Text TitleText;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            Wire(ClearSaveButton, ClearSave);
            Wire(CoinsButton, GrantCoins);
            Wire(UnlockButton, UnlockAll);
            Wire(AchievementButton, UnlockAchievements);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            if (TitleText != null)
                TitleText.text = "开发者窗口";
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.DevWindow);
        }

        private static void ClearSave()
        {
            ProgressRepository.ResetProgress();
            GuideRepository.ResetAll();
            UIManager.Instance?.ShowToast("已清除关卡和引导进度");
        }

        private static void GrantCoins()
        {
            CoinWallet.AddCoins(9999);
            UIManager.Instance?.ShowToast("金币 +9999");
        }

        private static void UnlockAll()
        {
            ProgressRepository.UnlockAllThreeStars();
            UIManager.Instance?.ShowToast("关卡已全部三星解锁");
        }

        private static void UnlockAchievements()
        {
            DailyTaskRepository.UnlockAll();
            UIManager.Instance?.ShowToast("成就已全部解锁");
        }
    }
}
