using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class DailyLoginCellView : MonoBehaviour
    {
        public Image Background;
        public Text DayText;
        public Text RewardText;
        public Text StateText;
        public Image GotImage;
        public Text GotText;

        public void Bind(int day, DailyReward reward, bool today, bool claimed)
        {
            EnsureRefs();
            if (DayText != null)
                DayText.text = $"第{day}天";
            if (RewardText != null)
                RewardText.text = reward.Label;
            var state = claimed ? "已领" : today ? "可领" : string.Empty;
            if (StateText != null)
                StateText.text = state;
            if (GotText != null)
                GotText.text = state;
            if (GotImage != null)
                GotImage.gameObject.SetActive(!string.IsNullOrEmpty(state));
        }

        private void EnsureRefs()
        {
            var got = transform.Find("Got");
            if (got == null)
                return;
            if (GotImage == null)
                GotImage = got.GetComponent<Image>();
            if (GotText == null)
                GotText = got.GetComponent<Text>() ?? got.GetComponentInChildren<Text>(true);
        }
    }
}
