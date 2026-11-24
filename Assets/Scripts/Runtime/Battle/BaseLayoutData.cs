using System;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>An enemy base deployment on a level's map: enemy squads and defenses spawned at battle start by
    /// BattleSetup (replacing the handcrafted deployment in Battle.unity). Written by the editor ProgressionBuilder
    /// (seeded from the map's prop free sockets, or hand placed for Level 1) so props never overlap.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Base Layout Data")]
    public class BaseLayoutData : ScriptableObject
    {
        public int seed;
        public EnemySquadPlacement[] squads = Array.Empty<EnemySquadPlacement>();
        public DefensePlacementData[] defenses = Array.Empty<DefensePlacementData>();
        [Tooltip("Optional: counterattack hatches for this layout (empty keeps the map's).")] public Vector3[] counterSpawnsOverride = Array.Empty<Vector3>();
    }

    [Serializable]
    public struct EnemySquadPlacement
    {
        public Vector3 position;
        public bool advance;
        public int count;
        public int seed;
        [Tooltip("A squad without a flag bearer (Level 1's first guard).")] public bool noFlagBearer;
    }

    [Serializable]
    public struct DefensePlacementData
    {
        [Tooltip("DefenseCatalog id: turret, mine, tank, cannon, bunker, tesla, sniper, geyser.")] public string defenseId;
        public Vector3 position;
        public float yaw;
        [Tooltip("Tank only: patrol end points.")] public Vector3 patrolA, patrolB;
    }
}
