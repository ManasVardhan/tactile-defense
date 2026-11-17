using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Base editing, defense rewards and simulated raids. VERIFIED concepts (RESEARCH_MECHANICS section 5):
    /// a base has a space limit and a budget cap that rises with level; placed defenses are consumed and must be
    /// rebought with gold. EVERY NUMBER HERE IS AN ESTIMATE.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Base Editor Config")]
    public class BaseEditorConfig : ScriptableObject
    {
        [Header("Caps by player level (index = level - 1). Budget from GAMEPLAY_MECHANICS 13; space raised from its 8 + 2 per 3 levels (traps cost space too)")]
        public int[] budgetCapByLevel = { 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250 };
        public int[] spaceCapByLevel = { 10, 10, 12, 12, 14, 14, 16, 16, 18, 18, 20, 20, 22, 22, 24, 24, 26, 26, 28, 28 };

        [Header("Placement")]
        [Tooltip("Square grid cell (GAMEPLAY_MECHANICS: 1.0 unit cells, unverified).")] public float gridCell = 1f;
        [Tooltip("New defenses face the attackers (south, -Z).")] public float defaultYaw = 180f;
        public float rotateStep = 45f;
        [Tooltip("Battle health and damage bonus per research level above 1.")] public float levelBonus = 0.15f;

        [Header("Defense battle rewards (Defend from the Base screen; tests pay nothing)")]
        public int defenseHoldGold = 60;
        public int defenseHoldGoldPerAttacker = 1;
        public int defenseHoldXp = 40;
        public int defenseFallXp = 10;

        [Header("Simulated offline raids (labeled SIMULATED in the UI, not real players)")]
        [Tooltip("Minutes away from the menu before a raid can be generated.")] public float raidMinAwayMinutes = 30f;
        public int raidMaxPerSession = 2;
        [Tooltip("Attack power = base + perLevel * level, randomized x0.7 to x1.3.")] public float raidAttackBase = 40f;
        public float raidAttackPerLevel = 12f;
        [Tooltip("Defense strength per budget point of a placed defense, times its level bonus.")] public float raidStrengthPerBudget = 1f;
        [Tooltip("Chance that each trap fires (and is consumed) during a raid.")] [Range(0, 1)] public float raidTrapChance = 0.55f;
        [Range(0, 1)] public float raidStructureLossHeld = 0.12f;
        [Range(0, 1)] public float raidStructureLossFallen = 0.45f;
        public int raidHoldGold = 45;
        [Range(0, 1)] public float raidLossFraction = 0.08f;
        public int raidLossCap = 120;
        public int raidLogMax = 6;

        public int BudgetCap(int level) => budgetCapByLevel.Length == 0 ? 60 : budgetCapByLevel[Mathf.Clamp(level - 1, 0, budgetCapByLevel.Length - 1)];
        public int SpaceCap(int level) => spaceCapByLevel.Length == 0 ? 8 : spaceCapByLevel[Mathf.Clamp(level - 1, 0, spaceCapByLevel.Length - 1)];
        public float LevelMult(int researchLevel) => 1f + levelBonus * Mathf.Max(0, researchLevel - 1);
    }
}
