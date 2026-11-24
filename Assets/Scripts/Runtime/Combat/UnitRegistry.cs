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

        /// <summary>Nearest living target of team 'of' within range of pos (XZ distance minus target radius). With
        /// preferDrawsFire, the nearest living drawsFire target in range wins over any nearer target.</summary>
        public static Health FindNearest(Team of, Vector3 pos, float range, bool preferDrawsFire = false) =>
            FindNearest(of, pos, range, preferDrawsFire, false);

        /// <summary>As FindNearest; with preferStructures the nearest living structure in range wins over everything.</summary>
        public static Health FindNearest(Team of, Vector3 pos, float range, bool preferDrawsFire, bool preferStructures)
        {
            var l = lists[(int)of];
            Health best = null, bestDraws = null, bestStructure = null;
            float bestD = float.MaxValue, bestDrawsD = float.MaxValue, bestStructureD = float.MaxValue;
            for (int i = 0; i < l.Count; i++)
            {
                var h = l[i];
                if (!h.IsAlive) continue;
                Vector3 p = h.transform.position;
                float dx = p.x - pos.x, dz = p.z - pos.z;
                float r = range + h.targetRadius;
                float d2 = dx * dx + dz * dz;
                if (d2 > r * r) continue;
                if (preferStructures && h.kind == TargetKind.Structure && d2 < bestStructureD)
                {
                    bestStructureD = d2;
                    bestStructure = h;
                }
                if (preferDrawsFire && h.drawsFire && d2 < bestDrawsD)
                {
                    bestDrawsD = d2;
                    bestDraws = h;
                }
                if (d2 >= bestD) continue;
                bestD = d2;
                best = h;
            }
            if (bestStructure != null) return bestStructure;
            return bestDraws != null ? bestDraws : best;
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
        public static int DamageArea(Team victims, Vector3 center, float radius, float damage, TeamColorId by) =>
            DamageArea(victims, center, radius, damage, by, 1f, 1f);

        /// <summary>Area damage with linear falloff to 40 percent at the edge, times 'structureMult' on structures and
        /// mines and 'soldierMult' on soldiers and flag bearers.</summary>
        public static int DamageArea(Team victims, Vector3 center, float radius, float damage, TeamColorId by, float structureMult, float soldierMult)
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
                f *= h.kind == TargetKind.Structure || h.kind == TargetKind.Mine ? structureMult : soldierMult;
                h.TakeDamage(damage * f, center, by);
                hits++;
            }
            return hits;
        }
    }
}
