using System;
using UnityEngine;

namespace TactileWars.Meta
{
    [CreateAssetMenu(menuName = "TactileWars/Meta/Base Roster")]
    public class BaseRoster : ScriptableObject
    {
        public BaseDefinition[] bases = Array.Empty<BaseDefinition>();

        public BaseDefinition Find(string id)
        {
            for (int i = 0; i < bases.Length; i++) if (bases[i] != null && bases[i].id == id) return bases[i];
            return null;
        }

        public int IndexOf(BaseDefinition b) => Array.IndexOf(bases, b);
    }
}
