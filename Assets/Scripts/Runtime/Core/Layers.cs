using UnityEngine;

namespace TactileWars.Core
{
    /// <summary>Physics layer indices from the architecture contract.</summary>
    public static class Layers
    {
        public const int Ground = 6;
        public const int PlayerUnit = 7;
        public const int EnemyUnit = 8;
        public const int Structure = 9;
        public const int Obstacle = 10;
        public const int Projectile = 11;
        public const int Pickup = 12;

        public const int GroundMask = 1 << Ground;
        public const int ObstacleMask = (1 << Obstacle) | (1 << Structure);
        public const int PickupMask = 1 << Pickup;
    }
}
