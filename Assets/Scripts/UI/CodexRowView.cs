using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 图鉴一行：左图标，右上名字，右下描述。描述超出视口时从右向左循环滚动。
    /// </summary>
    public class CodexRowView : MonoBehaviour
    {
        public Image IconImage;
        public Text NameText;
        public Text BodyText;
        public RectTransform BodyViewport;

        const float Speed = 70f;
        const float Gap = 48f;

        bool _scroll;
        float _textWidth;
        float _viewWidth;
        float _x;

        public void Bind(string name, string body, Sprite icon)
        {
            if (NameText != null)
                NameText.text = name ?? string.Empty;
            if (BodyText != null)
                BodyText.text = body ?? string.Empty;
            if (IconImage != null)
            {
                IconImage.sprite = icon;
                IconImage.enabled = icon != null;
            }

            _scroll = false;
            _x = 0f;
            Place(0f);
            if (BodyText == null || BodyViewport == null)
                return;

            Canvas.ForceUpdateCanvases();
            _viewWidth = BodyViewport.rect.width;
            _textWidth = BodyText.preferredWidth;
            _scroll = _textWidth > _viewWidth + 4f;
        }

        void Update()
        {
            if (!_scroll || BodyText == null)
                return;

            _x -= Speed * Time.unscaledDeltaTime;
            if (_x < -_textWidth - Gap)
                _x = _viewWidth;
            Place(_x);
        }

        void Place(float x)
        {
            if (BodyText == null)
                return;
            var rect = BodyText.rectTransform;
            var pos = rect.anchoredPosition;
            pos.x = x;
            rect.anchoredPosition = pos;
        }
    }
}
