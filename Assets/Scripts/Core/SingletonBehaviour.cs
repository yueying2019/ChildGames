using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 场景内单例基类。由 <see cref="GameBootstrap"/> 统一创建，不在场景中手动挂载。
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
            OnSingletonAwake();
        }

        protected virtual void OnSingletonAwake()
        {
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
