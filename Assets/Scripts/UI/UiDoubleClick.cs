using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace SmallGames
{
    /// <summary>
    /// 短时间连点两次视为双击。微信触摸没有可靠的 clickCount，用时间窗判断。
    /// </summary>
    public class UiDoubleClick : MonoBehaviour, IPointerClickHandler
    {
        private const float WindowSeconds = 0.35f;

        public UnityEvent onDoubleClick = new UnityEvent();

        private float _lastClick = -1f;

        public void OnPointerClick(PointerEventData eventData)
        {
            var now = Time.unscaledTime;
            if (_lastClick >= 0f && now - _lastClick <= WindowSeconds)
            {
                _lastClick = -1f;
                onDoubleClick.Invoke();
                return;
            }

            _lastClick = now;
        }
    }
}
