using UnityEngine;

namespace TactileWars.Squad
{
    /// <summary>Player squad tunables. Squad size of 40 is an estimate from reference frames (30 to 60 seen).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Squad Config")]
    public class SquadConfig : ScriptableObject
    {
        [Tooltip("Estimate. Soldiers in the player squad at battle start.")] public int soldierCount = 40;
        [Tooltip("Radius of the team ring that marks the squad anchor. Reference ring is about 6 units wide in radius terms (ps_12).")]
        public float ringRadius = 5.2f;
        [Tooltip("How fast the ring glides to a new anchor.")] public float ringFollowSharpness = 6f;
        [Tooltip("Seconds after a death before the formation closes its gaps.")] public float respaceDelay = 0.6f;
        [Tooltip("Start formation: width of the U shaped chain (reference ps_12 shows a U or L chain).")] public float startWidth = 15f;
        [Tooltip("Start formation: length of the U legs.")] public float startDepth = 6.5f;
    }

    public struct MotorSettings
    {
        public float maxSpeed, accel, decel, arriveRadius, deadZone;
        public float separationRadius, separationStrength, turnSpeed, obstacleRadius;
        public Vector2 boundsMin, boundsMax;
    }
}
