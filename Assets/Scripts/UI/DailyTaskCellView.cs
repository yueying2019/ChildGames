using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 每日任务列表里的一行。结构全部来自预制体 UI/DailyTaskCell，运行时只做数据绑定。
    /// </summary>
    public class DailyTaskCellView : MonoBehaviour
    {
        public const float CellHeight = 132f;

        public Image Background;
        public Text TitleText;
        public Text DescText;
        public Text ProgressText;
        public Text RewardText;
        public Button ClaimButton;
        public Text ClaimLabel;

        private DailyTaskDef _def;
        private Action<string> _onClaim;

        private void Awake()
        {
            EnsureRefs();
            if (ClaimButton != null)
            {
                ClaimButton.onClick.RemoveAllListeners();
                ClaimButton.onClick.AddListener(ClickClaim);
            }
        }

        /// <summary>引用丢了就按名字把预制体里的节点找回来，避免整行失效。</summary>
        private void EnsureRefs()
        {
            if (Background == null)
                Background = Node<Image>("Bg");
            if (TitleText == null)
                TitleText = Node<Text>("Title");
            if (DescText == null)
                DescText = Node<Text>("Desc");
            if (ProgressText == null)
                ProgressText = Node<Text>("Progress");
            if (RewardText == null)
                RewardText = Node<Text>("Reward");
            if (ClaimButton == null)
                ClaimButton = Node<Button>("Claim");
            if (ClaimLabel == null)
            {
                var claim = transform.Find("Claim");
                ClaimLabel = claim != null ? claim.GetComponentInChildren<Text>(true) : null;
            }
        }

        private T Node<T>(string name) where T : Component
        {
            var node = transform.Find(name);
            return node != null ? node.GetComponent<T>() : null;
        }

        private void ClickClaim()
        {
            if (_def == null || _onClaim == null)
                return;
            _onClaim(_def.Key);
        }

        public void Bind(DailyTaskDef def, Action<string> onClaim)
        {
            _def = def;
            _onClaim = onClaim;
            if (def == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var progress = DailyTaskRepository.Progress(def.Key);
            var done = progress >= def.Target;
            var claimed = DailyTaskRepository.IsClaimed(def.Key);

            if (TitleText != null)
                TitleText.text = def.Title;
            if (DescText != null)
                DescText.text = def.Desc;
            if (ProgressText != null)
                ProgressText.text = $"{progress}/{def.Target}";
            if (RewardText != null)
                RewardText.text = $"+{def.RewardCoins}金币";
            if (Background != null)
                Background.color = claimed ? UIStyle.SlotEmpty : done ? UIStyle.None : UIStyle.BgCardSoft;
            if (ClaimButton != null)
                ClaimButton.interactable = done && !claimed;
            if (ClaimLabel != null)
                ClaimLabel.text = claimed ? "已领取" : done ? "领取" : "未完成";
        }
    }
}
