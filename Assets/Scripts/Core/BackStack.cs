using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 返回键处理项。Handle 返回 true 表示这次返回已被消费；返回 false 表示不处理，继续找下一项。
    /// </summary>
    public sealed class BackEntry
    {
        public string Key;
        public int Priority;
        public Func<bool> Handle;
        public bool Enabled = true;
    }

    /// <summary>
    /// 返回键注册中心：谁要接管返回键就 Push 一项，不需要时 Pop，不必再到 GameManager 里堆 if。
    /// 每次按下返回键取「优先级最高」的可用项执行。
    /// </summary>
    public static class BackStack
    {
        private static readonly List<BackEntry> Entries = new List<BackEntry>(16);

        public static int Count => Entries.Count;

        /// <summary>注册一项返回处理；同 key 会覆盖（优先级和回调一起刷新）。</summary>
        public static void Push(string key, int priority, Func<bool> handle)
        {
            if (string.IsNullOrEmpty(key) || handle == null)
                return;

            for (var i = 0; i < Entries.Count; i++)
            {
                var existing = Entries[i];
                if (existing == null || !string.Equals(existing.Key, key))
                    continue;

                existing.Priority = priority;
                existing.Handle = handle;
                existing.Enabled = true;
                return;
            }

            Entries.Add(new BackEntry
            {
                Key = key,
                Priority = priority,
                Handle = handle,
                Enabled = true
            });
        }

        /// <summary>注销一项返回处理。重复调用安全。</summary>
        public static void Pop(string key)
        {
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i] != null && string.Equals(Entries[i].Key, key))
                    Entries.RemoveAt(i);
            }
        }

        public static bool Contains(string key)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && string.Equals(Entries[i].Key, key))
                    return true;
            }

            return false;
        }

        /// <summary>临时屏蔽某项（比如过场动画期间），不用注销再重注册。</summary>
        public static void SetEnabled(string key, bool enabled)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && string.Equals(Entries[i].Key, key))
                    Entries[i].Enabled = enabled;
            }
        }

        public static void Clear()
        {
            Entries.Clear();
        }

        /// <summary>处理一次返回键。返回 true 表示已被某项消费。</summary>
        public static bool Handle()
        {
            BackEntry top = null;
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null || !entry.Enabled || entry.Handle == null)
                    continue;
                if (top == null || entry.Priority > top.Priority)
                    top = entry;
            }

            return top != null && top.Handle();
        }
    }
}
