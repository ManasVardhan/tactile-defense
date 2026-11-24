using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>Kinds of player unit. Values are stable (saved in loadouts later); add new kinds at the end.</summary>
    public enum UnitTypeId { Attacker = 0, Tank = 1, Healer = 2, Sniper = 3, Vanguard = 4, Demolisher = 5 }

    /// <summary>One player unit type: how many soldiers it fields and how their base soldier stats are scaled.
    /// All numbers are first pass estimates.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Unit Type Config")]
    public class UnitTypeConfig : ScriptableObject
    {
        public UnitTypeId id;
        public string displayName = "Unit";
        [Tooltip("One letter shown on the first pass unit icon.")] public string glyph = "U";
        public int soldierCount = 3;

        [Header("Multipliers on the base soldier config")]
        public float healthMult = 1f;
        public float rangeMult = 1f;
        public float speedMult = 1f;
        public float damageMult = 1f;
        [Tooltip("Above 1 fires slower.")] public float fireIntervalMult = 1f;
        public float modelScale = 1f;

        [Header("Presentation and behavior")]
        public Color accent = Color.white;
        [Tooltip("Enemy shooters prefer these soldiers over nearer targets while they are in range.")] public bool drawsFire;
        [Tooltip("Each soldier counts this many times toward a capture point's speed bonus.")] public int captureWeight = 1;

        [Header("Abilities (always on)")]
        [Tooltip("False: the soldiers never fire (Healer).")] public bool hasWeapon = true;
        [Tooltip("Health per second the unit shares among injured friendly soldiers on its hex and the six around it, most injured first. 0 for none.")]
        public float healPerSecond;
        [Tooltip("Seconds between revives: a damaged unit on a neighboring hex gets one dead soldier back at half health. 0 for none.")]
        public float reviveInterval;
        [Tooltip("Shots are lobbed shells (the cannon's arc) that splash an area; one shell carries the whole volley's damage.")] public bool lobbed;
        [Tooltip("Area damage radius of a lobbed shell.")] public float splashRadius;
        [Tooltip("Damage multiplier against structures.")] public float structureDamageMult = 1f;
        [Tooltip("Damage multiplier against soldiers.")] public float soldierDamageMult = 1f;
        [Tooltip("Targets the nearest structure in range before any soldier.")] public bool prefersStructures;
        [Tooltip("One sentence for the intro card and the squad builder.")] [TextArea] public string description = "";
    }
}
