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

        [Header("Tesla antenna (estimates: only the name is verified)")]
        [Tooltip("Seconds to charge between zaps.")] public float teslaChargeTime = 2.2f;
        [Tooltip("Range to the first target.")] public float teslaRange = 5.5f;
        [Tooltip("Max soldiers hit by one arc (first target included).")] public int teslaChainCount = 4;
        [Tooltip("Max jump distance between chained soldiers.")] public float teslaChainRange = 2.6f;
        public float teslaDamage = 26f;
        [Tooltip("Damage multiplier per jump.")] [Range(0.3f, 1f)] public float teslaChainFalloff = 0.8f;
        [Tooltip("Seconds the arc stays visible.")] public float teslaArcTime = 0.28f;

        [Header("Sniper (estimates: name verified, a user report places sniper towers in low level bases)")]
        public float sniperRange = 16f;
        [Tooltip("Seconds the aiming line shows before the shot.")] public float sniperAimTime = 1.3f;
        public float sniperCooldown = 2.4f;
        public float sniperDamage = 50f;
        public float sniperShotSpeed = 45f;

        [Header("Geyser (estimates: sources only say geysers explode)")]
        public float geyserTriggerRadius = 1.8f;
        [Tooltip("Rumble warning before each eruption.")] public float geyserWarnTime = 0.9f;
        public float geyserRadius = 2.8f;
        public float geyserDamage = 34f;
        [Tooltip("Eruptions before the geyser is spent (consumed).")] public int geyserCharges = 2;
        public float geyserCooldown = 4.5f;
        [Tooltip("When above 0 the geyser also erupts on this timer while attackers are within 6 units.")] public float geyserTimerInterval = 0f;

        [Header("Base editor (estimates)")]
        [Tooltip("Range shown by the editor ring. 0 uses the weapon range.")] public float displayRange = 0f;
    }
}
