using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 盖在棋盘上的涂色层。页签没选中时不接射线，正常交换照旧。
    /// 左键涂，右键擦。右键拖动不走拖拽事件，按住时在 Update 里跟着鼠标走。
    /// </summary>
    public class Match3BoardPaint : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Image Target;

        private Match3GameView _view;
        private int _lastX = -1;
        private int _lastY = -1;
        private bool _erasing;
        private bool _changed;

        public void Bind(Match3GameView view)
        {
            _view = view;
        }

        public void SetListening(bool on)
        {
            if (!on)
                EndStroke();
            if (Target != null)
                Target.raycastTarget = on;
            _erasing = false;
            _lastX = -1;
            _lastY = -1;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastX = -1;
            _lastY = -1;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _erasing = true;
                Paint(eventData.position, eventData.pressEventCamera, true);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            Paint(eventData.position, eventData.pressEventCamera, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            Paint(eventData.position, eventData.pressEventCamera, false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                _erasing = false;
            EndStroke();
        }

        private void Update()
        {
            if (!_erasing)
                return;
            if (!Input.GetMouseButton(1))
            {
                _erasing = false;
                EndStroke();
                return;
            }

            Paint(Input.mousePosition, null, true);
        }

        private void EndStroke()
        {
            var changed = _changed;
            _changed = false;
            _lastX = -1;
            _lastY = -1;
            if (!changed || _view == null)
                return;
            _view.FinishCustomStroke();
        }

        private void Paint(Vector2 screen, Camera camera, bool erase)
        {
            if (_view == null || !_view.TryCustomCell(screen, camera, out var x, out var y))
                return;
            if (x == _lastX && y == _lastY)
                return;
            _lastX = x;
            _lastY = y;
            if (_view.PaintCustom(x, y, erase))
                _changed = true;
        }
    }
}
