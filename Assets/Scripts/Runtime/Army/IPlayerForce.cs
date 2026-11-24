using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>What the battle rules, HUD and camera need from the player's side, whatever controls it.</summary>
    public interface IPlayerForce
    {
        /// <summary>Living soldiers right now.</summary>
        int AliveCount { get; }
        /// <summary>Soldiers ever spawned this battle.</summary>
        int StartCount { get; }
        /// <summary>Mean position of the living soldiers; the last value when none are alive.</summary>
        Vector3 Centroid { get; }
        /// <summary>False once the player has nothing left to fight with.</summary>
        bool CanStillFight { get; }
        /// <summary>Held cheer (victory) on or off.</summary>
        void SetCheer(bool on);
        /// <summary>Short cheer, unless a held cheer is on.</summary>
        void CheerFor(float seconds);
    }
}
