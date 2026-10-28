using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public sealed class LoadingPayload
    {
        public string Title;
        public string Tip;
        public GameModeType Mode;
    }

    public class LoadingPanel : UIPanel
    {
        public Text TitleText;
        public Text TipText;
        public Text PercentText;
        public Image BarFill;

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            var data = payload as LoadingPayload;
            if (TitleText != null)
                TitleText.text = data != null && !string.IsNullOrEmpty(data.Title) ? data.Title : "正在进入游戏";
            SetProgress(0f, data != null && !string.IsNullOrEmpty(data.Tip) ? data.Tip : "准备中");
        }

        public void SetProgress(float progress, string tip = null)
        {
            var t = Mathf.Clamp01(progress);
            if (BarFill != null)
            {
                var rect = BarFill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(Mathf.Max(0.02f, t), 1f);
                rect.offsetMin = new Vector2(3f, 3f);
                rect.offsetMax = new Vector2(-3f, -3f);
            }

            if (PercentText != null)
                PercentText.text = Mathf.RoundToInt(t * 100f) + "%";
            if (tip != null && TipText != null)
                TipText.text = tip;
        }
    }
}
