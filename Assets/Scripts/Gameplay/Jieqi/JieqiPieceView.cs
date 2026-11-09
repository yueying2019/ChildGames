using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>棋盘上的一枚棋子或落点标记。由 JieqiGameView 摆放。</summary>
    public class JieqiPieceView : MonoBehaviour
    {
        public Image Icon;
        public Image SkillIcon;
        public Button Button;

        public void Show(Sprite sprite, Color color)
        {
            Apply(Icon, sprite, color, 0f, 0f, 1f, 1f, sprite != null);
            if (SkillIcon != null)
                SkillIcon.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        /// <summary>技能棋：自己和吃到的初始子并排，每一枚都是单棋子的 70%。</summary>
        public void ShowPair(Sprite self, Color selfColor, Sprite other, Color otherColor)
        {
            var root = transform as RectTransform;
            var full = root != null && root.sizeDelta.y > 1f ? root.sizeDelta.y : 72f;
            var icon = full * 0.7f;
            var gap = full * 0.06f;
            var width = icon * 2f + gap;
            if (root != null)
                root.sizeDelta = new Vector2(width, icon);
            var left = icon / width;
            var right = (icon + gap) / width;
            Apply(Icon, self, selfColor, 0f, 0f, left, 1f, self != null);
            Apply(SkillIcon, other, otherColor, right, 0f, 1f, 1f, other != null);
            gameObject.SetActive(true);
        }

        public void SetHitPadding(Vector4 padding)
        {
            var graphic = HitGraphic();
            if (graphic != null)
                graphic.raycastPadding = padding;
        }

        public void SetHitEnabled(bool on)
        {
            var graphic = HitGraphic();
            if (graphic != null)
                graphic.raycastTarget = on;
        }

        Image HitGraphic()
        {
            return Button != null ? Button.targetGraphic as Image : null;
        }

        static void Apply(Image image, Sprite sprite, Color color, float x0, float y0, float x1, float y1, bool on)
        {
            if (image == null)
                return;
            image.gameObject.SetActive(on);
            if (!on)
                return;
            image.sprite = sprite;
            image.color = color;
            image.enabled = sprite != null;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
