using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// UI 点击音效。
    /// 旧实现在 Update 里每帧读 Input，再自己 RaycastAll 判定点到哪个 IPointerClickHandler，
    /// 既每帧 new 一个 PointerEventData，又要自己复刻 UGUI 早就算好的拖动阈值、禁用态、多指判定。
    /// 现在改为由 UiClickRelay 在 UGUI 真正派发 click 时回调，判定全部交给 EventSystem：零轮询、零射线。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class UIClickSound : MonoBehaviour
    {
        /// <summary>
        /// 给 root 下所有能接收点击的节点挂上 UiClickRelay，已挂过的会跳过。
        /// 面板实例化后、玩法视图建好后各调用一次即可。
        /// </summary>
        public static void BindHierarchy(GameObject root)
        {
            if (root == null)
                return;

            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || !(component is IPointerClickHandler))
                    continue;

                var target = component.gameObject;
                if (target.GetComponent<UiClickRelay>() == null)
                    target.AddComponent<UiClickRelay>();
            }
        }

        /// <summary>有效点击的回调入口：过滤掉不可交互的控件和棋盘棋子。</summary>
        public static void PlayFor(GameObject handler)
        {
            if (!IsClickable(handler) || IsBoardTile(handler))
                return;

            AudioManager.PlayUiClick();
        }

        private static bool IsClickable(GameObject handler)
        {
            if (handler == null || !handler.activeInHierarchy)
                return false;

            var selectable = handler.GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private static bool IsBoardTile(GameObject handler)
        {
            if (handler == null)
                return false;
            return handler.GetComponentInParent<LianliankanTileView>() != null
                || handler.GetComponentInParent<SheepTileView>() != null;
        }
    }
}
