using UnityEngine;

namespace TactileWars.Core
{
    /// <summary>Watch speed for defense battles (1x, 2x, 4x). Pausing stores and restores Time.timeScale, so the
    /// multiplier survives a pause. Reset to 1 when a battle scene ends.</summary>
    public static class GameSpeed
    {
        public static float Multiplier { get; private set; } = 1f;

        public static void Set(float m)
        {
            Multiplier = Mathf.Clamp(m, 0.25f, 8f);
            if (Time.timeScale > 0f) Time.timeScale = Multiplier;
        }

        public static void Reset()
        {
            Multiplier = 1f;
            if (Time.timeScale > 0f) Time.timeScale = 1f;
        }
    }
}
