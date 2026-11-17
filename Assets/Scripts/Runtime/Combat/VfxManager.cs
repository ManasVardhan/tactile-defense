using System;
using System.Collections.Generic;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    public enum VfxType { Hit = 0, Puff = 1, Ghost = 2, Explosion = 3, Muzzle = 4, BubblePop = 5, Capture = 6, Reinforce = 7, Geyser = 8, Zap = 9, Coins = 10 }

    /// <summary>Pools every effect type. Released automatically after their lifetime.</summary>
    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        public PooledVfx[] prefabs = Array.Empty<PooledVfx>();
        public int prewarmEach = 12;

        Pool<PooledVfx>[] pools;
        struct Live { public PooledVfx fx; public float end; }
        readonly List<Live> live = new List<Live>(256);
        public int PlayedCount { get; private set; }

        void Awake()
        {
            Instance = this;
            int n = Enum.GetValues(typeof(VfxType)).Length;
            pools = new Pool<PooledVfx>[n];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var p = prefabs[i];
                if (p == null) continue;
                int idx = (int)p.type;
                if (idx >= 0 && idx < n && pools[idx] == null)
                    pools[idx] = new Pool<PooledVfx>(p, transform, p.type == VfxType.Hit ? prewarmEach * 3 : prewarmEach);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(VfxType type, Vector3 pos, Color color, float scale = 1f)
        {
            var pool = pools != null && (int)type < pools.Length ? pools[(int)type] : null;
            if (pool == null) return;
            if (live.Count >= 220) return; // hard cap so a big firefight cannot flood particles
            var fx = pool.Get();
            fx.transform.position = pos;
            fx.Play(color, scale);
            live.Add(new Live { fx = fx, end = Time.time + fx.lifetime });
            PlayedCount++;
        }

        void Update()
        {
            float now = Time.time;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].end > now) continue;
                var fx = live[i].fx;
                int last = live.Count - 1;
                live[i] = live[last];
                live.RemoveAt(last);
                if (fx != null) pools[(int)fx.type].Release(fx);
            }
        }
    }
}
