using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 盖在对对消棋盘上的涂色层。页签没选中时不接射线，连线照旧。
    /// 左键替换，右键擦成空格。右键拖动不走拖拽事件，按住时在 Update 里跟着鼠标走。
    /// </summary>
    public class LianliankanBoardPaint : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Image Target;

        private LianliankanGameView _view;
        private int _lastRow = -1;
        private int _lastCol = -1;
        private bool _erasing;
        private bool _changed;

        public void Bind(LianliankanGameView view)
        {
            _view = view;
        }

        /// <summary>关掉监听前先收笔，避免页签切走后这一笔没有记进棋盘。</summary>
        public void SetListening(bool on)
        {
            if (!on)
                EndStroke();
            if (Target != null)
                Target.raycastTarget = on;
            _erasing = false;
            _lastRow = -1;
            _lastCol = -1;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastRow = -1;
            _lastCol = -1;
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
            _lastRow = -1;
            _lastCol = -1;
            if (!changed || _view == null)
                return;
            _view.FinishCustomStroke();
        }

        private void Paint(Vector2 screen, Camera camera, bool erase)
        {
            if (_view == null || !_view.TryCustomCell(screen, camera, out var row, out var col))
                return;
            if (row == _lastRow && col == _lastCol)
                return;
            _lastRow = row;
            _lastCol = col;
            if (_view.PaintCustom(row, col, erase))
                _changed = true;
        }
    }
}
