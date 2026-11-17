using System.Collections.Generic;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Live targets per team with O(1) add and remove. Iterate backwards when the loop may kill.</summary>
    public static class UnitRegistry
    {
        static readonly List<Health>[] lists = { new List<Health>(256), new List<Health>(256) };

        public static List<Health> Of(Team t) => lists[(int)t];
        public static Team Opponent(Team t) => t == Team.Player ? Team.Enemy : Team.Player;

        public static void Register(Health h)
        {
            if (h.registryIndex >= 0) return;
            var l = lists[(int)h.team];
            h.registryIndex = l.Count;
            l.Add(h);
        }

        public static void Unregister(Health h)
        {
            int i = h.registryIndex;
            if (i < 0) return;
            var l = lists[(int)h.team];
            int last = l.Count - 1;
            if (i <= last && l[i] == h)
            {
                l[i] = l[last];
                l[i].registryIndex = i;
                l.RemoveAt(last);
            }
            else
            {
                // Team changed after registration: fall back to a search.
                for (int k = 0; k < 2; k++)
                {
                    int idx = lists[k].IndexOf(h);
                    if (idx >= 0)
                    {
                        int lk = lists[k].Count - 1;
                        lists[k][idx] = lists[k][lk];
                        lists[k][idx].registryIndex = idx;
                        lists[k].RemoveAt(lk);
                    }
                }
            }
            h.registryIndex = -1;
        }

        public static void Clear()
        {
            lists[0].Clear();
            lists[1].Clear();
        }

        /// <summary>Nearest living target of team 'of' within range of pos (XZ distance minus target radius).</summary>
        public static Health FindNearest(Team of, Vector3 pos, float range)
        {
            var l = lists[(int)of];
            Health best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < l.Count; i++)
            {
                var h = l[i];
                if (!h.IsAlive) continue;
                Vector3 p = h.transform.position;
                float dx = p.x - pos.x, dz = p.z - pos.z;
                float r = range + h.targetRadius;
                float d2 = dx * dx + dz * dz;
                if (d2 > r * r || d2 >= bestD) continue;
                bestD = d2;
                best = h;
            }
            return best;
        }

        public static int CountWithin(Team of, Vector3 pos, float radius)
        {
            var l = lists[(int)of];
            int c = 0;
            float r2 = radius * radius;
            for (int i = 0; i < l.Count; i++)
            {
                if (l[i].kind == TargetKind.Structure || l[i].kind == TargetKind.Mine) continue;
                Vector3 p = l[i].transform.position;
                float dx = p.x - pos.x, dz = p.z - pos.z;
                if (dx * dx + dz * dz <= r2) c++;
            }
            return c;
        }

        /// <summary>Area damage with linear falloff to 40 percent at the edge.</summary>
        public static int DamageArea(Team victims, Vector3 center, float radius, float damage, TeamColorId by)
        {
            var l = lists[(int)victims];
            int hits = 0;
            for (int i = l.Count - 1; i >= 0; i--)
            {
                if (i >= l.Count) continue;
                var h = l[i];
                Vector3 p = h.transform.position;
                float dx = p.x - center.x, dz = p.z - center.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz) - h.targetRadius;
                if (d > radius) continue;
                float f = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(d / Mathf.Max(0.01f, radius)));
                h.TakeDamage(damage * f, center, by);
                hits++;
            }
            return hits;
        }
    }
}
