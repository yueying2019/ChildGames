using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 通用对象池（纯 C#，不依赖 UnityEngine，可在 EditMode 里直接单测）。
    /// 用于替代棋盘、飘字、特效这类反复 Instantiate / Destroy 的开销。
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onTake;
        private readonly Action<T> _onReturn;
        private readonly Stack<T> _free = new Stack<T>();
        private readonly HashSet<T> _live = new HashSet<T>();

        public ObjectPool(Func<T> create, Action<T> onTake = null, Action<T> onReturn = null)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onTake = onTake;
            _onReturn = onReturn;
        }

        public int CountFree => _free.Count;
        public int CountActive => _live.Count;
        public int CountAll => _free.Count + _live.Count;

        public T Take()
        {
            var item = _free.Count > 0 ? _free.Pop() : _create();
            if (item == null)
                return null;

            _live.Add(item);
            _onTake?.Invoke(item);
            return item;
        }

        /// <summary>回收。重复回收或未借出的对象会被忽略。</summary>
        public bool Return(T item)
        {
            if (item == null)
                return false;
            if (!_live.Remove(item))
                return false;

            _onReturn?.Invoke(item);
            _free.Push(item);
            return true;
        }

        /// <summary>把池外已经存在的对象收编进空闲区（比如场景里预摆好的棋子）。</summary>
        public bool Adopt(T item)
        {
            if (item == null || _live.Contains(item))
                return false;

            _onReturn?.Invoke(item);
            _free.Push(item);
            return true;
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var item = _create();
                if (item == null)
                    return;
                _onReturn?.Invoke(item);
                _free.Push(item);
            }
        }

        public void Clear(Action<T> dispose = null)
        {
            if (dispose != null)
            {
                foreach (var item in _live)
                    dispose(item);
                while (_free.Count > 0)
                    dispose(_free.Pop());
            }

            _live.Clear();
            _free.Clear();
        }
    }

    /// <summary>
    /// UnityEngine 组件版对象池：取出时自动激活、回收时自动隐藏并归位。
    /// </summary>
    public sealed class ComponentPool<T> where T : UnityEngine.Component
    {
        private readonly ObjectPool<T> _inner;
        private readonly UnityEngine.Transform _parent;

        public ComponentPool(T prefab, UnityEngine.Transform parent, Action<T> onTake = null)
        {
            _parent = parent;
            _inner = new ObjectPool<T>(() =>
            {
                if (prefab == null)
                    return null;
                var made = UnityEngine.Object.Instantiate(prefab, parent, false);
                made.gameObject.SetActive(false);
                return made;
            }, item =>
            {
                if (item == null)
                    return;
                item.gameObject.SetActive(true);
                item.transform.SetParent(parent, false);
                item.transform.SetAsLastSibling();
                onTake?.Invoke(item);
            }, item =>
            {
                if (item == null)
                    return;
                item.gameObject.SetActive(false);
                item.transform.SetParent(parent, false);
            });
        }

        public UnityEngine.Transform Parent => _parent;

        public int CountFree => _inner.CountFree;
        public int CountActive => _inner.CountActive;
        public int CountAll => _inner.CountAll;

        public T Take() => _inner.Take();

        public bool Return(T item) => _inner.Return(item);

        public void Prewarm(int count) => _inner.Prewarm(count);

        public void Clear() => _inner.Clear(item =>
        {
            if (item != null)
                UnityEngine.Object.Destroy(item.gameObject);
        });
    }
}
