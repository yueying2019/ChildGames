using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 面板基类。界面结构来自预制体，运行时只绑定事件。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        public UIPanelId Id { get; private set; }
        public UILayer Layer { get; private set; }
        public bool IsOverlay { get; private set; }
        public bool IsOpen { get; private set; }

        internal void Bind(UIPanelId id, UILayer layer, bool overlay)
        {
            Id = id;
            Layer = layer;
            IsOverlay = overlay;
        }

        public virtual void BindListeners()
        {
            ApplyRuntimeFont();
        }

        public virtual void OnOpen(object payload)
        {
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public virtual void OnClose()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }

        protected void Wire(Button button, UnityAction action)
        {
            if (button == null || action == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        protected void ApplyRuntimeFont()
        {
            UIStyle.ApplyTextStyle(this);
        }
    }
}
