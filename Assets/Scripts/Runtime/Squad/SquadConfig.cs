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

        [Header("Routing around walls (estimates)")]
        [Tooltip("Route taps that are blocked by walls through gates on a coarse grid.")] public bool useRouting = true;
        public float navCell = 1f;
        [Tooltip("Extra clearance around obstacles when baking the grid.")] public float navClearance = 0.3f;
        [Tooltip("Spacing of the compact blob the squad uses while routing.")] public float routeBlobSpacing = 0.5f;
        [Tooltip("Advance to the next waypoint when the squad centroid is this close.")] public float routeAdvanceDistance = 2.2f;
    }

    public struct MotorSettings
    {
        public float maxSpeed, accel, decel, arriveRadius, deadZone;
        public float separationRadius, separationStrength, turnSpeed, obstacleRadius;
        public Vector2 boundsMin, boundsMax;
    }
}
