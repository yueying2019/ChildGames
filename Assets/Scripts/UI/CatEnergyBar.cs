using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>猫咪脚下的两段能量条。左段数字是技能一所需能量，右段是技能二的累计能量。</summary>
    public class CatEnergyBar : MonoBehaviour
    {
        public Image Fill;
        public Text Stage1;
        public Text Stage2;

        public void SetCharge(int energy, int stage)
        {
            if (stage < 1)
                stage = 1;
            var total = stage * 2;
            SetRatio(Mathf.Clamp01(energy / (float)total));
            if (Stage1 != null)
                Stage1.text = stage.ToString();
            if (Stage2 != null)
                Stage2.text = total.ToString();
        }

        private void SetRatio(float ratio)
        {
            if (Fill == null)
                return;

            var rect = Fill.rectTransform;
            var parent = rect.parent as RectTransform;
            var width = parent != null ? parent.rect.width : 0f;
            if (width < 8f && parent != null)
                width = parent.sizeDelta.x;
            if (width < 8f)
                width = 220f;

            var inner = Mathf.Max(0f, width - 8f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(inner * ratio, 0f);
            Fill.enabled = ratio > 0.001f;
        }
    }
}
