using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 每日任务面板。只读 DailyTaskRepository，改进度的事全交给玩法侧上报。
    /// </summary>
    public class DailyTaskPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Text TitleText;
        public Text SummaryText;
        public RectTransform ListRoot;
        public DailyTaskCellView CellPrefab;

        private readonly List<DailyTaskCellView> _cells = new List<DailyTaskCellView>();

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
            if (TitleText != null)
                TitleText.text = "每日任务";
            // 任务条目在这里建好，EnsurePanel 之后统一挂点击音，晚建的会漏掉。
            EnsureCells();
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            DailyTaskDirector.Changed += Refresh;
            Refresh();
        }

        public override void OnClose()
        {
            DailyTaskDirector.Changed -= Refresh;
            base.OnClose();
        }

        private void OnDestroy()
        {
            DailyTaskDirector.Changed -= Refresh;
        }

        private void CloseSelf()
        {
            UIManager.Instance?.Close(UIPanelId.DailyTask);
        }

        /// <summary>
        /// 任务行的真正父节点。ListRoot 是滚动区域根节点，行必须挂到 ScrollRect.content 上，
        /// 挂错层会被 Viewport 的 Mask 裁掉。没有 ScrollRect 就退化成直接挂 ListRoot。
        /// </summary>
        private RectTransform ContentRoot()
        {
            if (ListRoot == null)
                return null;

            var scroll = ListRoot.GetComponent<ScrollRect>();
            return scroll != null && scroll.content != null ? scroll.content : ListRoot;
        }

        private void EnsureCells()
        {
            var parent = ContentRoot();
            if (parent == null || CellPrefab == null)
                return;

            CellPrefab.gameObject.SetActive(false);
            var defs = DailyTaskCatalog.All;
            while (_cells.Count < defs.Count)
            {
                var cell = Instantiate(CellPrefab, parent);
                cell.name = "Cell" + _cells.Count;
                // 中文字体在微信端是运行时加载的，新实例出来的行要重新套一次样式
                UIStyle.ApplyTextStyle(cell.transform);
                UIClickSound.BindHierarchy(cell.gameObject);
                _cells.Add(cell);
            }

            for (var i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null)
                    _cells[i].gameObject.SetActive(i < defs.Count);
            }
        }

        private void Refresh()
        {
            EnsureCells();
            var defs = DailyTaskCatalog.All;
            for (var i = 0; i < _cells.Count; i++)
            {
                if (i >= defs.Count || _cells[i] == null)
                    continue;
                _cells[i].Bind(defs[i], Claim);
            }

            if (SummaryText != null)
            {
                SummaryText.text = $"已完成 {DailyTaskRepository.CompletedCount()}/{DailyTaskCatalog.Count} "
                                   + (DailyTaskRepository.HasClaimable() ? "有奖励可领" : "继续加油");
            }
        }

        private void Claim(string key)
        {
            if (!DailyTaskDirector.Claim(key))
            {
                UIManager.Instance?.ShowToast("还不能领取");
                return;
            }

            var def = DailyTaskCatalog.Find(key);
            UIManager.Instance?.ShowToast($"{def?.Title}　+{def?.RewardCoins ?? 0} 金币");
        }
    }
}
