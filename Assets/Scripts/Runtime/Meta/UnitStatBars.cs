using TactileWars.Army;
using UnityEngine;

namespace TactileWars.Meta
{
    public enum UnitStat { Health, Damage, Range, Speed }

    /// <summary>
    /// The four stat bars on the Army screen's unit cards, derived from a UnitTypeConfig's multipliers and compared
    /// with a reference type (the Attacker). Ratio is 1 for the reference. Damage is damage per second against
    /// soldiers (damage, soldier damage and fire interval multipliers), 0 for a type with no weapon. A bar's fill is
    /// ratio / (1 + ratio): the Attacker sits at half, three times the Attacker fills three quarters, and a bar never
    /// runs off its track.
    /// </summary>
    public static class UnitStatBars
    {
        public static readonly UnitStat[] Shown = { UnitStat.Health, UnitStat.Damage, UnitStat.Range, UnitStat.Speed };

        /// <summary>Where the reference type's bar ends (the tick on every track).</summary>
        public const float ReferenceFill = 0.5f;

        public static string Label(UnitStat s)
        {
            switch (s)
            {
                case UnitStat.Health: return "Health";
                case UnitStat.Damage: return "Damage";
                case UnitStat.Range: return "Range";
                default: return "Speed";
            }
        }

        /// <summary>The raw multiplier a stat bar shows for a type.</summary>
        public static float Raw(UnitTypeConfig t, UnitStat s)
        {
            if (t == null) return 0f;
            switch (s)
            {
                case UnitStat.Health: return t.healthMult;
                case UnitStat.Damage: return t.hasWeapon ? t.damageMult * t.soldierDamageMult / Mathf.Max(0.01f, t.fireIntervalMult) : 0f;
                case UnitStat.Range: return t.rangeMult;
                default: return t.speedMult;
            }
        }

        /// <summary>The stat compared with 'reference' (1 = same). Without a reference the raw multiplier is used,
        /// which is the same thing when the reference's multipliers are all 1, as the Attacker's are.</summary>
        public static float Ratio(UnitTypeConfig t, UnitStat s, UnitTypeConfig reference = null)
        {
            float v = Raw(t, s);
            float r = reference != null ? Raw(reference, s) : 1f;
            return r > 0.0001f ? Mathf.Max(0f, v / r) : v;
        }

        /// <summary>The bar's fill, 0 to 1 (ReferenceFill for the reference type, 0 for no weapon).</summary>
        public static float Fill(UnitTypeConfig t, UnitStat s, UnitTypeConfig reference = null)
        {
            float r = Ratio(t, s, reference);
            return r / (1f + r);
        }

        /// <summary>A type with no weapon: its Damage bar is empty and reads "Heals".</summary>
        public static bool Heals(UnitTypeConfig t) => t != null && !t.hasWeapon;
    }
}
