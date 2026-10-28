using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 挂在启动场景上，把内容表交给 AssetHub。不要用查找场景物体当流程入口。
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public sealed class ContentIndexHolder : MonoBehaviour
    {
        public ContentIndex Index;

        private void Awake()
        {
            if (Index != null)
                ContentIndex.Bind(Index);
        }
    }
}
