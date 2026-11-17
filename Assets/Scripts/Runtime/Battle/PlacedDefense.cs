using TactileWars.Structures;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Marks a defense spawned from the player's saved layout so the defense battle can report which
    /// placements were consumed (destroyed structures, fired traps, wiped defender squads).</summary>
    public class PlacedDefense : MonoBehaviour
    {
        public int placementIndex;
        public string defenseId;
        public StructureBase structure;
        public EnemySquad squad;

        public bool Consumed
        {
            get
            {
                if (this == null) return true;
                if (structure != null) return structure.Destroyed || !structure.gameObject.activeInHierarchy;
                if (squad != null) return squad.Wiped;
                return false;
            }
        }
    }
}
