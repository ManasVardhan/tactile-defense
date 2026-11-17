using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Shared combat presentation tunables.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Tooltip("Max persistent ground splats; the oldest is recycled.")] public int splatCap = 420;
        public Vector2 splatSizeRange = new Vector2(0.45f, 0.9f);
        public float deathSplatSize = 1.3f;
        public float explosionSplatSize = 3.2f;
        public float hitFlashDuration = 0.08f;
        public float squashAmount = 0.28f;
        public float squashRecover = 10f;
        [Tooltip("Seconds between target rescans per shooter (staggered).")] public float targetScanInterval = 0.25f;
        public int projectilePrewarm = 256;
        public float dieAnimTime = 0.55f;

        [Header("Soldier presentation (visual only, gameplay facing is unchanged)")]
        [Tooltip("Degrees the visual model leans back toward the camera so faces read under the oblique camera (ESTIMATE).")]
        public float visualLean = 30f;
        [Tooltip("Max visual yaw away from the camera-facing direction; soldiers never fully show their backs (ESTIMATE).")]
        public float visualMaxYaw = 80f;
        [Tooltip("Visual yaw turn rate (degrees per second).")] public float visualTurnSpeed = 900f;
        [Tooltip("Hop height when a formation is set.")] public float hopHeight = 0.32f;
        public float hopTime = 0.28f;
        [Tooltip("Max random delay before each soldier hops (ripple).")] public float hopStagger = 0.18f;
        [Tooltip("Squash on landing (formation hop or arriving at a slot).")] public float landSquash = 0.22f;
        [Tooltip("Backward kick of the model per shot (units).")] public float recoilKick = 0.08f;
        [Tooltip("Chance that a soldier shot shows a muzzle puff.")] [Range(0, 1)] public float muzzleFlashChance = 0.6f;
        public float muzzleFlashScale = 1.3f;
    }
}
