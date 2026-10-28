using UnityEngine;
using UnityEngine.EventSystems;

namespace SmallGames
{
    /// <summary>
    /// 挂在「本来就能接收点击」的 UI 节点上，等 UGUI 判定为有效点击后回调。
    /// ExecuteEvents.Execute 会把事件发给目标节点上的每一个 IPointerClickHandler，
    /// 所以本组件不会抢走 Button / Toggle 自己的 onClick，也无需自己再做一次射线。
    /// </summary>
    public sealed class UiClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            UIClickSound.PlayFor(gameObject);
        }
    }
}
