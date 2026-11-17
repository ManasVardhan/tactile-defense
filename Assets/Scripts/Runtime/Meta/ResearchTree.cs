using System;
using UnityEngine;

namespace TactileWars.Meta
{
    public enum ResearchBranch { Troops = 0, Offense = 1, Defense = 2 }

    public enum ResearchEffect
    {
        SquadSize, SoldierHealth, FireRate, PaintDamage, MoveSpeed,
        ReinforcementSize, ReinforcementCharge, HeroAbility,
        DefenseUnlock
    }

    /// <summary>One research step: needs a player level (verified mechanic: research is level locked and bought
    /// with gold, it cannot be bought early) and costs gold. 'value' is the cumulative effect at this level.</summary>
    [Serializable]
    public struct ResearchStep
    {
        public int requiredLevel;
        public int goldCost;
        public float value;
    }

    [Serializable]
    public class ResearchNode
    {
        public string id;
        public string displayName;
        [Tooltip("Effect text; {0} is replaced by the next level value.")] public string description;
        public ResearchBranch branch;
        public ResearchEffect effect;
        [Tooltip("UI icon key (see TactileWars.UI.UIIcon names) or a render key like def_turret.")] public string icon;
        [Tooltip("Defense id for Defense branch nodes (turret, mine, tank, cannon, bunker, tesla).")] public string defenseId;
        [Tooltip("Display text for a value; {0} is value times displayScale, rounded.")] public string valueFormat = "+{0}";
        public float displayScale = 1f;
        public ResearchStep[] steps = Array.Empty<ResearchStep>();
        public int MaxLevel => steps.Length;

        public string FormatValue(float v) => string.Format(valueFormat, Mathf.RoundToInt(v * displayScale));
    }

    /// <summary>Research tree with three branches. Names, costs, locks and effects are ESTIMATES (sources verify the
    /// mechanism only: level locked items bought with gold that improve troops, offense and defenses).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Research Tree")]
    public class ResearchTree : ScriptableObject
    {
        public ResearchNode[] nodes = Array.Empty<ResearchNode>();

        public ResearchNode Find(string id)
        {
            for (int i = 0; i < nodes.Length; i++) if (nodes[i].id == id) return nodes[i];
            return null;
        }

        /// <summary>Cumulative effect value of a node at a level (0 when not researched).</summary>
        public static float ValueAt(ResearchNode n, int level)
        {
            if (n == null || level <= 0 || n.steps.Length == 0) return 0f;
            return n.steps[Mathf.Min(level, n.steps.Length) - 1].value;
        }
    }
}
