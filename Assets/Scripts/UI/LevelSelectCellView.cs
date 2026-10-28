using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmallGames
{
    public class LevelSelectCellView : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public Text NumberText;
        public Text TitleText;
        public Text StateText;

        /// <summary>三颗星标。序列化字段优先，没配就按名字 Star1/Star2/Star3 回填。</summary>
        public Image[] Stars;

        private Image[] _stars;

        public void Bind(int levelId, string title, bool unlocked, bool cleared, bool lastPlayed, int reward, int stars, UnityAction onClick)
        {
            SetStars(stars);
            if (NumberText != null)
                NumberText.text = levelId.ToString();
            if (TitleText != null)
                TitleText.text = title;
            if (StateText != null)
            {
                if (!unlocked)
                    StateText.text = "未解锁";
                else if (cleared)
                    StateText.text = lastPlayed ? "已通关 · 上次" : "已通关";
                else
                    StateText.text = lastPlayed ? $"上次 · {reward}金币" : $"{reward}金币";
                StateText.horizontalOverflow = HorizontalWrapMode.Overflow;
                StateText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (Button == null)
                return;
            Button.interactable = unlocked;
            Button.onClick.RemoveAllListeners();
            if (unlocked && onClick != null)
                Button.onClick.AddListener(onClick);
        }

        public void BindEndless(bool unlocked, bool lastPlayed, int bestWave, UnityEngine.Events.UnityAction onClick)
        {
            SetStars(0);
            if (NumberText != null)
                NumberText.text = "∞";
            if (TitleText != null)
                TitleText.text = "无尽";
            if (StateText != null)
            {
                if (!unlocked)
                    StateText.text = $"通关第{EndlessCatalog.UnlockClearedId}关解锁";
                else if (bestWave > 0)
                    StateText.text = lastPlayed ? $"最高 {bestWave} 波 · 上次" : $"最高 {bestWave} 波";
                else
                    StateText.text = lastPlayed ? "上次 · 无尽" : "看能撑几波";
            }

            if (Button == null)
                return;
            Button.interactable = unlocked;
            Button.onClick.RemoveAllListeners();
            if (unlocked && onClick != null)
                Button.onClick.AddListener(onClick);
        }

        /// <summary>每日挑战格子。标题是今天抽到的关卡名，奖励一天只领一次。</summary>
        public void BindDaily(bool unlocked, bool claimed, bool lastPlayed, string levelTitle, UnityAction onClick)
        {
            SetStars(0);
            if (NumberText != null)
                NumberText.text = "每日";
            if (TitleText != null)
                TitleText.text = string.IsNullOrEmpty(levelTitle) ? "每日挑战" : levelTitle;
            if (StateText != null)
            {
                if (!unlocked)
                    StateText.text = "完成教学解锁";
                else if (claimed)
                    StateText.text = lastPlayed ? "今日已领 · 上次" : "今日已领";
                else
                    StateText.text = lastPlayed ? "可领奖励 · 上次" : "可领奖励";
                StateText.horizontalOverflow = HorizontalWrapMode.Overflow;
                StateText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (Button == null)
                return;
            Button.interactable = unlocked;
            Button.onClick.RemoveAllListeners();
            if (unlocked && onClick != null)
                Button.onClick.AddListener(onClick);
        }

        /// <summary>教学关（第 0 关）格子：不算星级，只显示「学会 / 未学」。</summary>
        public void BindTutorial(bool cleared, UnityAction onClick)
        {
            SetStars(0);
            if (NumberText != null)
                NumberText.text = "0";
            if (TitleText != null)
                TitleText.text = "教学";
            if (StateText != null)
            {
                StateText.text = cleared ? "已学会" : "未学习";
                StateText.horizontalOverflow = HorizontalWrapMode.Overflow;
                StateText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (Button == null)
                return;
            Button.interactable = true;
            Button.onClick.RemoveAllListeners();
            if (onClick != null)
                Button.onClick.AddListener(onClick);
        }

        /// <summary>点亮前 n 颗星，其余隐藏。0 星时三颗都不显示。</summary>
        public void SetStars(int stars)
        {
            var images = StarImages();
            if (images == null)
                return;
            var count = stars < 0 ? 0 : stars;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                    images[i].gameObject.SetActive(i < count);
            }
        }

        private Image[] StarImages()
        {
            if (_stars != null)
                return _stars;
            _stars = Stars != null && Stars.Length > 0 ? Stars : CollectStars();
            return _stars;
        }

        private Image[] CollectStars()
        {
            return new[]
            {
                NodeStar("Star1"),
                NodeStar("Star2"),
                NodeStar("Star3")
            };
        }

        private Image NodeStar(string name)
        {
            var child = transform.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }
    }
}
