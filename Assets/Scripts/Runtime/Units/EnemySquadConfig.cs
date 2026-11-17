using UnityEngine;

namespace TactileWars.Units
{
    [CreateAssetMenu(menuName = "TactileWars/Enemy Squad Config")]
    public class EnemySquadConfig : ScriptableObject
    {
        [Tooltip("Estimate. Reference shows enemy groups of 8 to 30.")] public int soldierCount = 10;
        [Tooltip("Loose ring radius around the flag bearer.")] public float clusterRadius = 2.4f;
        public float clusterJitter = 0.45f;
        [Tooltip("Estimate. Player distance that wakes the squad.")] public float aggroRange = 10f;
        [Tooltip("How far an advancing squad may leave its post.")] public float leashDistance = 7f;
        public float advanceSpeed = 1.3f;
        [Tooltip("Advancing squads stop at this fraction of weapon range from the nearest player soldier.")] public float standOffFraction = 0.75f;
        public UnitConfig soldier;
        public UnitConfig flagBearer;
    }
}
