using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>棋盘空白处的点击。平时不接射线，编辑里的移动模式才打开。</summary>
    public class JieqiBoardClick : MonoBehaviour, IPointerClickHandler
    {
        public event Action<PointerEventData> Clicked;

        public void SetListening(bool listening)
        {
            var image = GetComponent<Image>();
            if (image != null)
                image.raycastTarget = listening;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
                return;
            Clicked?.Invoke(eventData);
        }
    }
}
