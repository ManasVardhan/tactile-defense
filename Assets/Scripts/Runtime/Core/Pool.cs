using System;
using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Core
{
    /// <summary>Simple stack based object pool for components. Prewarm to avoid runtime instantiation.</summary>
    public class Pool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free;
        readonly Action<T> onCreate;
        public int CountAll { get; private set; }
        public int CountFree => free.Count;

        public Pool(T prefab, Transform parent, int prewarm, Action<T> onCreate = null)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.onCreate = onCreate;
            free = new Stack<T>(Mathf.Max(4, prewarm));
            for (int i = 0; i < prewarm; i++)
            {
                var item = Create();
                item.gameObject.SetActive(false);
                free.Push(item);
            }
        }

        T Create()
        {
            var item = UnityEngine.Object.Instantiate(prefab, parent);
            CountAll++;
            onCreate?.Invoke(item);
            return item;
        }

        public T Get()
        {
            var item = free.Count > 0 ? free.Pop() : Create();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }
    }
}
