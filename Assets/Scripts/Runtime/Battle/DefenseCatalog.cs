using System;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Defense id to prefab table used by runtime layouts (AI bases now, the player's base editor later).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Defense Catalog")]
    public class DefenseCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public string displayName;
            public GameObject prefab;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public GameObject Get(string id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return entries[i].prefab;
            return null;
        }
    }
}
