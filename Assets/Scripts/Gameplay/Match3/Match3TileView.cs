using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmallGames
{
    public class Match3TileView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Image Background;
        public Image Icon;
        public GameObject SelectFrame;
        public GameObject SpecialBoom;
        public GameObject SpecialArrowU;
        public GameObject SpecialArrowD;
        public GameObject SpecialArrowL;
        public GameObject SpecialArrowR;
        public Button Button;

        public int TileId { get; private set; }
        public int ColorId { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        public void SetCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Action<Match3TileView, int, int> Swiped;
        public Action<Match3TileView> Tapped;

        private Vector2 _dragStart;
        private bool _swiped;
        private bool _tapped;

        public void Bind(int x, int y, Match3Tile tile, Sprite icon)
        {
            X = x;
            Y = y;
            if (tile == null)
            {
                TileId = 0;
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            TileId = tile.Id;
            ColorId = tile.Color;
            ApplyVisual(tile, icon);
            SetSelected(false);
        }

        public void SetLocked(bool locked)
        {
            // 冰/锁仍可点选道具，交换由 GameView 拦截。
        }

        public void ApplyVisual(Match3Tile tile, Sprite icon)
        {
            ColorId = tile.Color;
            var lineH = tile.Special == Match3Special.LineH;
            var lineV = tile.Special == Match3Special.LineV;
            var bomb = tile.Special == Match3Special.Bomb;
            if (Icon != null)
            {
                Icon.sprite = icon;
                Icon.enabled = icon != null && !lineH && !lineV;
                Icon.preserveAspect = true;
            }

            Show(SpecialArrowL, lineH);
            Show(SpecialArrowR, lineH);
            Show(SpecialArrowU, lineV);
            Show(SpecialArrowD, lineV);
            ShowBoom(bomb, icon);
        }

        public void SetSelected(bool selected)
        {
            Show(SelectFrame, selected && gameObject.activeSelf);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _swiped = false;
            _tapped = false;
            _dragStart = eventData.position;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _swiped = false;
            _tapped = false;
            _dragStart = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            TrySwipe(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            TrySwipe(eventData.position);
            EmitTapIfNeeded();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EmitTapIfNeeded();
        }

        private void EmitTapIfNeeded()
        {
            if (_swiped || _tapped)
                return;
            _tapped = true;
            Tapped?.Invoke(this);
        }

        private void TrySwipe(Vector2 screenPos)
        {
            if (_swiped)
                return;
            var delta = screenPos - _dragStart;
            if (delta.sqrMagnitude < 900f)
                return;

            var dx = 0;
            var dy = 0;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                dx = delta.x > 0f ? 1 : -1;
            else
                dy = delta.y > 0f ? -1 : 1;

            _swiped = true;
            Swiped?.Invoke(this, dx, dy);
        }

        private void ShowBoom(bool on, Sprite icon)
        {
            if (SpecialBoom == null)
            {
                var found = transform.Find("SpecialBoom");
                if (found != null)
                    SpecialBoom = found.gameObject;
            }

            if (SpecialBoom == null)
                return;

            var image = SpecialBoom.GetComponent<Image>();
            if (image == null && on)
                image = SpecialBoom.AddComponent<Image>();
            if (image != null)
            {
                if (icon != null)
                    image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, on ? 0.85f : 1f);
            }

            Show(SpecialBoom, on);
        }

        private static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
        }
    }
}
