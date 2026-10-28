using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class GuidePanel : UIPanel
    {
        public Button DimButton;
        public Button OkButton;
        public Image Icon;
        public Text TitleText;
        public Text DescText;
        public GameObject ArrowU;
        public GameObject ArrowD;

        private System.Action _closed;
        private GuideHighlight _highlight;
        private bool _dimWasActive;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(OkButton, CloseSelf);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            var data = payload as GuidePayload;
            _closed = data != null ? data.Closed : null;
            if (data != null)
                MarkSeen(data);

            if (TitleText != null)
                TitleText.text = data != null ? data.Title : string.Empty;
            if (DescText != null)
                DescText.text = data != null ? data.Desc : string.Empty;

            var showArrows = data != null && data.ShowLineArrows;
            Show(ArrowU, showArrows);
            Show(ArrowD, showArrows);

            if (Icon != null)
            {
                Icon.sprite = data != null ? data.Icon : null;
                Icon.enabled = Icon.sprite != null;
                Icon.preserveAspect = true;
            }

            ApplyHighlight(data);
        }

        public override void OnClose()
        {
            if (_highlight != null)
            {
                _highlight.Hide();
                Destroy(_highlight.gameObject);
                _highlight = null;
            }

            if (DimButton != null && DimButton.gameObject.activeSelf != _dimWasActive)
                DimButton.gameObject.SetActive(_dimWasActive);

            var closed = _closed;
            _closed = null;
            base.OnClose();
            closed?.Invoke();
        }

        /// <summary>有高亮目标时，用挖洞遮罩替掉整屏暗底。</summary>
        private void ApplyHighlight(GuidePayload data)
        {
            var target = data != null ? data.Target : null;
            var shape = data != null ? data.Shape : GuideHighlightShape.None;
            if (target == null || shape == GuideHighlightShape.None)
                return;

            if (DimButton != null)
            {
                _dimWasActive = DimButton.gameObject.activeSelf;
                DimButton.gameObject.SetActive(false);
            }

            _highlight = GuideHighlight.Attach(transform as RectTransform, UIStyle.Overlay, CloseSelf);
            if (_highlight != null)
                _highlight.Show(target, shape);
        }

        private static void MarkSeen(GuidePayload data)
        {
            // 新流程按 StepKey 去重；老调用（只有 Id）走兼容分支
            if (!string.IsNullOrEmpty(data.StepKey))
                GuideRepository.MarkSeen(data.Mode, data.StepKey);
            else
                GuideRepository.MarkSeen(data.Id);
        }

        private void CloseSelf()
        {
            UIManager.Instance.Close(UIPanelId.Guide);
        }

        private static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
        }
    }
}
