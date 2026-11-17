using TactileWars.Combat;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Defense tunables (one asset per defense type). All values are estimates: sources name turrets,
    /// mines, tanks, cannons and bunkers but give no stats.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Structure Config")]
    public class StructureConfig : ScriptableObject
    {
        [Header("Common (estimates)")]
        public float maxHealth = 220f;
        public float targetRadius = 1.0f;
        public float aimHeight = 0.8f;
        public WeaponConfig weapon;
        [Tooltip("Degrees per second for heads, turrets and barrels.")] public float aimTurnSpeed = 160f;
        [Tooltip("Only fire when the barrel is within this many degrees of the target.")] public float fireCone = 14f;
        public float deathExplosionRadius = 2.2f;

        [Header("Mine (estimates)")]
        public float triggerRadius = 1.4f;
        public float armDelay = 0.45f;
        public float explosionRadius = 2.6f;
        public float explosionDamage = 70f;

        [Header("Tank (estimates)")]
        public float patrolSpeed = 1.1f;
        public float patrolPause = 1.5f;
        public float hullTurnSpeed = 70f;
    }
}
