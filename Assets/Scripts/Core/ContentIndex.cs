using System;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 单机内容表。编辑器按短路径登记直接引用，运行时不再走 Addressables 包。
    /// </summary>
    public sealed class ContentIndex : ScriptableObject
    {
        public const string AssetPath = "Assets/Content/ContentIndex.asset";

        public static ContentIndex Current { get; private set; }

        public string[] Addresses = Array.Empty<string>();
        public string[] Labels = Array.Empty<string>();
        public string[] ExtraLabels = Array.Empty<string>();
        public UnityEngine.Object[] Assets = Array.Empty<UnityEngine.Object>();

        public static void Bind(ContentIndex index)
        {
            Current = index;
        }

        public T Get<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address) || Addresses == null || Assets == null)
                return null;
            for (var i = 0; i < Addresses.Length; i++)
            {
                if (Addresses[i] != address)
                    continue;
                return Cast<T>(Assets[i]);
            }

            return null;
        }

        public T[] GetAll<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label) || Addresses == null || Assets == null)
                return Array.Empty<T>();

            var count = 0;
            for (var i = 0; i < Addresses.Length; i++)
            {
                if (HasLabel(i, label) && Cast<T>(Assets[i]) != null)
                    count++;
            }

            if (count == 0)
                return Array.Empty<T>();

            var list = new T[count];
            var n = 0;
            for (var i = 0; i < Addresses.Length; i++)
            {
                if (!HasLabel(i, label))
                    continue;
                var asset = Cast<T>(Assets[i]);
                if (asset == null)
                    continue;
                list[n] = asset;
                n++;
            }

            if (n == list.Length)
                return list;
            var trimmed = new T[n];
            for (var i = 0; i < n; i++)
                trimmed[i] = list[i];
            return trimmed;
        }

        private bool HasLabel(int index, string label)
        {
            if (Labels != null && index < Labels.Length && Labels[index] == label)
                return true;
            if (ExtraLabels != null && index < ExtraLabels.Length && ExtraLabels[index] == label)
                return true;
            return false;
        }

        private static T Cast<T>(UnityEngine.Object asset) where T : UnityEngine.Object
        {
            if (asset == null)
                return null;
            if (asset is T typed)
                return typed;
            if (typeof(Component).IsAssignableFrom(typeof(T)) && asset is GameObject go)
                return go.GetComponent<T>();
            return null;
        }
    }
}
