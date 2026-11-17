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
    }
}
