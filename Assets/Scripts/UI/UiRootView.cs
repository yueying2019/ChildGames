using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// UI 根预制体上的图层与 Toast 引用。
    /// </summary>
    public class UiRootView : MonoBehaviour
    {
        public Canvas Canvas;
        public RectTransform Background;
        public RectTransform Gameplay;
        public RectTransform Normal;
        public RectTransform Hud;
        public RectTransform Popup;
        public RectTransform ToastLayer;
        public CanvasGroup ToastGroup;
        public Text ToastText;

        public RectTransform GetLayer(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Background: return Background;
                case UILayer.Gameplay: return Gameplay;
                case UILayer.Normal: return Normal;
                case UILayer.Hud: return Hud;
                case UILayer.Popup: return Popup;
                case UILayer.Toast: return ToastLayer;
                default: return Normal;
            }
        }
    }
}
