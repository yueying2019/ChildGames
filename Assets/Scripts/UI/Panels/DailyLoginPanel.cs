using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class DailyLoginPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Button ClaimButton;
        public Text TitleText;
        public Text MessageText;
        public DailyLoginCellView Day0;
        public DailyLoginCellView Day1;
        public DailyLoginCellView Day2;
        public DailyLoginCellView Day3;
        public DailyLoginCellView Day4;
        public DailyLoginCellView Day5;
        public DailyLoginCellView Day6;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            Wire(ClaimButton, Claim);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            DailyLoginRepository.Changed += Refresh;
            Refresh();
        }

        public override void OnClose()
        {
            DailyLoginRepository.Changed -= Refresh;
            base.OnClose();
        }

        private void OnDestroy()
        {
            DailyLoginRepository.Changed -= Refresh;
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.DailyLogin);
        }

        private void Claim()
        {
            if (!DailyLoginRepository.TryClaim())
            {
                UIManager.Instance?.ShowToast("今天已经领过了");
                return;
            }

            var day = DailyLoginRepository.TodayDay;
            Analytics.DailyClaim(day, DailyLoginCatalog.Get(day).Label);
            UIManager.Instance?.ShowToast("登录奖励已领取");
            Refresh();
        }

        private void Refresh()
        {
            var today = DailyLoginRepository.TodayDay;
            var claimedToday = DailyLoginRepository.ClaimedToday;
            BindDay(Day0, 1, today, claimedToday);
            BindDay(Day1, 2, today, claimedToday);
            BindDay(Day2, 3, today, claimedToday);
            BindDay(Day3, 4, today, claimedToday);
            BindDay(Day4, 5, today, claimedToday);
            BindDay(Day5, 6, today, claimedToday);
            BindDay(Day6, 7, today, claimedToday);

            if (TitleText != null)
                TitleText.text = "每日登录";
            if (MessageText != null)
            {
                MessageText.text = claimedToday
                    ? $"今天已领取第 {today} 天奖励"
                    : $"领取第 {today} 天奖励";
            }

            if (ClaimButton != null)
                ClaimButton.interactable = DailyLoginRepository.CanClaim;
            var claimLabel = ClaimButton != null ? ClaimButton.GetComponentInChildren<Text>() : null;
            if (claimLabel != null)
                claimLabel.text = claimedToday ? "已领取" : "领取";
        }

        private static void BindDay(DailyLoginCellView view, int day, int today, bool claimedToday)
        {
            if (view == null)
                return;
            view.Bind(day, DailyLoginCatalog.Get(day), day == today && !claimedToday, DailyLoginRepository.IsClaimed(day));
        }
    }
}
