using TactileWars.Combat;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Soldier stats and steering. All numbers are estimates (no source gives unit stats).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Unit Config")]
    public class UnitConfig : ScriptableObject
    {
        [Header("Health (estimate)")]
        public float maxHealth = 40f;
        public float targetRadius = 0.32f;
        public float aimHeight = 0.45f;
        public WeaponConfig weapon;

        [Header("Steering (estimates)")]
        public float maxSpeed = 4.6f;
        public float acceleration = 20f;
        public float deceleration = 26f;
        public float arriveRadius = 1.4f;
        public float deadZone = 0.08f;
        public float separationRadius = 0.48f;
        public float separationStrength = 3.5f;
        [Tooltip("Degrees per second.")] public float turnSpeed = 720f;
        public float obstacleRadius = 0.38f;

        public MotorSettings ToMotor(Vector2 boundsMin, Vector2 boundsMax) => new MotorSettings
        {
            maxSpeed = maxSpeed, accel = acceleration, decel = deceleration, arriveRadius = arriveRadius,
            deadZone = deadZone, separationRadius = separationRadius, separationStrength = separationStrength,
            turnSpeed = turnSpeed, obstacleRadius = obstacleRadius, boundsMin = boundsMin, boundsMax = boundsMax
        };
    }
}
