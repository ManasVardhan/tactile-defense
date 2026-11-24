using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Auto targets the nearest enemy in range, preferring draws-fire targets in range (unless built with
    /// preferDrawsFire false, as lobbed area weapons are), with staggered rescans (no per frame search).</summary>
    public class Targeting
    {
        public Health Current { get; private set; }
        /// <summary>Pick the nearest structure in range before any soldier (player Demolishers).</summary>
        public bool PreferStructures { get; set; }
        float nextScan;
        readonly float interval;
        readonly bool preferDrawsFire;

        public Targeting(float scanInterval, bool preferDrawsFire = true)
        {
            interval = Mathf.Max(0.02f, scanInterval);
            this.preferDrawsFire = preferDrawsFire;
            nextScan = Time.time + Random.value * interval;
        }

        public Health Tick(Team self, Vector3 pos, float range)
        {
            if (Current != null && (!Current.IsAlive || !InRange(Current, pos, range * 1.08f))) Current = null;
            if (Time.time >= nextScan || Current == null && Time.time >= nextScan - interval * 0.5f)
            {
                nextScan = Time.time + interval;
                Current = UnitRegistry.FindNearest(UnitRegistry.Opponent(self), pos, range, preferDrawsFire, PreferStructures);
            }
            return Current;
        }

        public void Clear() { Current = null; }

        static bool InRange(Health h, Vector3 pos, float range)
        {
            Vector3 p = h.transform.position;
            float dx = p.x - pos.x, dz = p.z - pos.z;
            float r = range + h.targetRadius;
            return dx * dx + dz * dz <= r * r;
        }
    }
}
