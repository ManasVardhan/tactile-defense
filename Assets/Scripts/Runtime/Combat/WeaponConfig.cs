using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Weapon tunables. All numbers are estimates: no source documents unit stats.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Weapon Config")]
    public class WeaponConfig : ScriptableObject
    {
        [Tooltip("Estimate. Firing range in units.")] public float range = 6.5f;
        [Tooltip("Estimate. Seconds between shots.")] public float fireInterval = 0.9f;
        [Tooltip("Random extra delay added to each shot so squads do not fire in lockstep.")] public float intervalJitter = 0.35f;
        [Tooltip("Estimate. Damage per hit.")] public float damage = 4f;
        [Tooltip("Projectile speed in units per second.")] public float projectileSpeed = 15f;
        [Tooltip("Aim error in degrees.")] public float spreadDegrees = 4f;
        [Tooltip("Extra radius around the target that still counts as a hit.")] public float hitRadius = 0.25f;
        [Tooltip("Area damage radius. 0 means single target.")] public float splashRadius = 0f;
        [Tooltip("Area damage multiplier against structures (player Demolisher shells).")] public float structureDamageMult = 1f;
        [Tooltip("Area damage multiplier against soldiers (player Demolisher shells).")] public float soldierDamageMult = 1f;
        [Tooltip("Visual size of the paint ball.")] public float projectileScale = 0.2f;
        [Tooltip("Arc height for lobbed shots (cannon, tank).")] public float arcHeight = 0f;
        [Tooltip("Shots per trigger pull, fired as a short dotted stream.")] public int burstCount = 1;
        [Tooltip("Delay between burst shots.")] public float burstGap = 0.08f;
        [Tooltip("Chance that a hit leaves a ground splat in the shooter color.")] [Range(0, 1)] public float hitSplatChance = 0.35f;
        [Tooltip("Chance that a miss leaves a ground splat.")] [Range(0, 1)] public float missSplatChance = 0.6f;
        [Tooltip("Ground splat size multiplier for this weapon.")] public float splatSize = 1f;
        [Tooltip("Camera shake on impact (0 for none).")] public float impactShake = 0f;
        public bool heavy;
        public TactileWars.Audio.SoundId fireSound = TactileWars.Audio.SoundId.Shoot;
    }
}
