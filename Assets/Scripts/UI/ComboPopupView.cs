using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class ComboPopupView : MonoBehaviour
    {
        private const float Duration = 0.7f;
        private const float PopEnd = 0.25f;
        private const float FadeStart = 0.55f;

        public Text Label;
        public CanvasGroup Group;

        public static ComboPopupView Attach(Transform parent)
        {
            if (parent == null)
                return null;
            var existing = parent.GetComponentInChildren<ComboPopupView>(true);
            if (existing != null)
                return existing;

            var view = UiPrefabs.Instantiate<ComboPopupView>(UiPrefabs.ComboPopup, parent, false);
            if (view == null)
                return null;
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.SetAsLastSibling();
            }

            return view;
        }

        public void Show(string text, Color color)
        {
            if (string.IsNullOrEmpty(text) || Label == null)
                return;
            Label.text = text;
            Label.color = color;
            UIStyle.ApplyTextStyle(Label);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            UITween.KillTarget(this); // 连击会连着来，先把上一段掐掉，避免两段动画抢同一个 alpha
            if (Group == null)
                Group = GetComponent<CanvasGroup>();

            var rect = transform as RectTransform;
            var group = Group;
            UITween.Custom(this, Duration, u =>
            {
                if (rect != null)
                {
                    var scale = u < PopEnd
                        ? Mathf.Lerp(0.55f, 1.18f, u / PopEnd)
                        : Mathf.Lerp(1.18f, 1f, (u - PopEnd) / (1f - PopEnd));
                    rect.localScale = Vector3.one * scale;
                }

                if (group != null)
                    group.alpha = u < FadeStart ? 1f : Mathf.Lerp(1f, 0f, (u - FadeStart) / (1f - FadeStart));
            }, EaseKind.Linear).SetUnscaled(true).OnComplete(() =>
            {
                if (group != null)
                    group.alpha = 0f;
            });
        }

        public void ShowCombo(int combo, string text)
        {
            Show(text, ComboCatalog.ColorFor(combo));
            AudioManager.Instance?.PlayCombo(combo);
        }

    }
}
