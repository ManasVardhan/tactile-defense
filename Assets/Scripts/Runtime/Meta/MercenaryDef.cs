using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>A hireable mercenary. Verified: legendary mercenaries exist and can be hired or rented for battle
    /// boosts. Names, costs and stats are ESTIMATES. Each one spawns the Hero model variant next to the squad with
    /// its own stat multipliers (relative to a researched squad soldier).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Mercenary")]
    public class MercenaryDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public string ability;
        public int unlockLevel = 2;
        [Tooltip("One time contract cost in gold.")] public int unlockCost = 400;
        [Tooltip("Gold paid each battle while hired.")] public int hireCost = 60;
        public float healthMult = 3f;
        public float damageMult = 1.5f;
        public float fireRateMult = 1f;
        public float scale = 1.2f;
        [Tooltip("Extra reinforcement charge speed while this mercenary is in the battle (0.25 = 25 percent faster).")]
        public float reinforcementChargeBonus;
        public Color accent = Color.white;
    }
}
