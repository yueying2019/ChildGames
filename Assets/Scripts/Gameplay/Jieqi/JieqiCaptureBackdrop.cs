using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 吃子动画底下的半透明黄沙。棋子演完先留着，等棋盘停顿结束或被停掉再收。
    /// </summary>
    public class JieqiCaptureBackdrop : MonoBehaviour
    {
        public RectTransform Sheet;

        const float ShowAlpha = 0.5f;

        Image _image;
        bool _ready;

        void Awake()
        {
            Ensure();
            Hide();
        }

        void Ensure()
        {
            if (_ready)
                return;
            if (Sheet != null)
                _image = Sheet.GetComponent<Image>();
            _ready = true;
        }

        public void Show()
        {
            Ensure();
            if (Sheet == null || _image == null)
                return;

            var parent = Sheet.parent as RectTransform;
            if (parent != null && parent.rect.width > 10f)
            {
                Sheet.anchorMin = new Vector2(0.5f, 0.5f);
                Sheet.anchorMax = new Vector2(0.5f, 0.5f);
                Sheet.pivot = new Vector2(0.5f, 0.5f);
                Sheet.sizeDelta = new Vector2(parent.rect.width * 1.12f, parent.rect.height * 1.12f);
            }

            Sheet.anchoredPosition = Vector2.zero;
            SetAlpha(ShowAlpha);
            UITween.KillTarget(this);
            UITween.Custom(this, 8f, Drift, EaseKind.Linear);
        }

        public void Hide()
        {
            Ensure();
            UITween.KillTarget(this);
            if (Sheet != null)
                Sheet.anchoredPosition = Vector2.zero;
            SetAlpha(0f);
        }

        void OnDisable()
        {
            UITween.KillTarget(this);
        }

        void Drift(float u)
        {
            if (Sheet == null)
                return;
            var t = u * 8f;
            Sheet.anchoredPosition = new Vector2(Mathf.Sin(t * 1.3f) * 26f, Mathf.Sin(t * 0.7f) * 10f);
        }

        void SetAlpha(float alpha)
        {
            if (_image == null)
                return;
            _image.color = new Color(1f, 1f, 1f, alpha);
            _image.enabled = alpha > 0.01f;
            _image.raycastTarget = alpha > 0.01f;
        }
    }
}
