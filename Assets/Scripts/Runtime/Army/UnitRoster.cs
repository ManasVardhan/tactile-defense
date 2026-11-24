using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>Every unit type the player can field.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Unit Roster")]
    public class UnitRoster : ScriptableObject
    {
        public UnitTypeConfig[] types;

        /// <summary>The type with this id, or null when the roster has none.</summary>
        public UnitTypeConfig Get(UnitTypeId id)
        {
            if (types == null) return null;
            for (int i = 0; i < types.Length; i++)
                if (types[i] != null && types[i].id == id) return types[i];
            return null;
        }
    }
}
