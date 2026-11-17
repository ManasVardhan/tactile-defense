using System;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Defense id to prefab table used by runtime layouts (AI bases and the player's base), plus the base
    /// editor economy per type. Names are verified (RESEARCH_MECHANICS section 4); ALL NUMBERS ARE ESTIMATES
    /// (GAMEPLAY_MECHANICS sections 11 to 13 give the budget and space points used here).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Defense Catalog")]
    public class DefenseCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public string displayName;
            [Tooltip("Structure prefab. Null for 'squad' (spawns an EnemySquad of defender soldiers).")] public GameObject prefab;
            [Tooltip("ESTIMATE. Gold to buy (and to rebuy after it is consumed).")] public int goldCost;
            [Tooltip("ESTIMATE. Budget points (verified concept: each defense consumes budget points).")] public int budget;
            [Tooltip("ESTIMATE. Space points (verified concept: bases have a space limit).")] public int space;
            [Tooltip("ESTIMATE. Max copies in one base.")] public int maxCount;
            [Tooltip("Footprint radius for overlap tests in the editor.")] public float footprint;
            [Tooltip("Range shown by the editor ring (0 for none).")] public float range;
            [Tooltip("Research node that unlocks it (Defense branch). Empty means always unlocked.")] public string researchId;
            [Tooltip("Single use traps are consumed when they fire (mines, spent geysers).")] public bool trap;
            [Tooltip("Defender soldiers for 'squad'.")] public int squadSize;
            [TextArea] public string description;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public GameObject Get(string id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return entries[i].prefab;
            return null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return i;
            return -1;
        }

        public bool TryGet(string id, out Entry e)
        {
            int i = IndexOf(id);
            e = i >= 0 ? entries[i] : default;
            return i >= 0;
        }

        public const string SquadId = "squad";
    }
}
