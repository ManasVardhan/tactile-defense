using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>
    /// SIMULATED offline raids (no server: these are not real players). A documented statistical model, not a
    /// battle simulation (ESTIMATE, BaseEditorConfig.raid*):
    ///   strength = sum over placed defenses of budget points x strengthPerBudget x research level bonus
    ///   attack   = (attackBase + attackPerLevel x level) x U(0.7, 1.3)
    ///   P(hold)  = strength / (strength + attack)
    /// Each trap fires (and is consumed) with raidTrapChance; every other defense is destroyed with
    /// raidStructureLossHeld or raidStructureLossFallen. Holding pays raidHoldGold x U(0.8, 1.2); falling loses
    /// min(gold x raidLossFraction, raidLossCap). Deterministic for a given seed.
    /// </summary>
    public static class RaidSimulator
    {
        /// <summary>Fake commander names for simulated attackers (labeled simulated in the UI).</summary>
        public static readonly string[] AttackerNames =
        {
            "Sgt. Puddle", "Major Drip", "Cpt. Swirl", "Lt. Smudge", "Col. Blot", "Gen. Splash", "Pvt. Dab", "Cpl. Stroke",
            "Ace Brushwood", "Mira Tint", "Otto Gloss", "Ruby Daub", "Kip Pastel", "Nova Hue", "Zed Primer", "Lola Wash",
        };

        public static readonly TeamColorId[] Factions = { TeamColorId.Red, TeamColorId.Blue, TeamColorId.Green, TeamColorId.Orange, TeamColorId.Pink, TeamColorId.Purple };

        public static TeamColorId PickAttackerTeam(TeamColorId player, System.Random rng)
        {
            for (int k = 0; k < 8; k++)
            {
                var t = Factions[rng.Next(Factions.Length)];
                if (t != player) return t;
            }
            return player == TeamColorId.Teal ? TeamColorId.Purple : TeamColorId.Teal;
        }

        public static float Strength(BaseLayoutSave layout, DefenseCatalog catalog, BaseEditorConfig cfg, System.Func<string, int> level)
        {
            float s = 0f;
            if (layout == null || layout.placements == null || catalog == null) return 0f;
            foreach (var p in layout.placements)
            {
                if (!catalog.TryGet(p.defenseId, out var e)) continue;
                int lvl = string.IsNullOrEmpty(e.researchId) ? 1 : Mathf.Max(1, level != null ? level(e.researchId) : 1);
                s += e.budget * (cfg != null ? cfg.raidStrengthPerBudget : 1f) * (cfg != null ? cfg.LevelMult(lvl) : 1f);
            }
            return s;
        }

        public static RaidRecord Simulate(PlayerProfile p, DefenseCatalog catalog, BaseEditorConfig cfg, System.Func<string, int> level,
            int seed, List<int> consumedIndices)
        {
            consumedIndices.Clear();
            var rng = new System.Random(seed);
            float U(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var layout = p.BaseLayout;
            float strength = Strength(layout, catalog, cfg, level);
            float attack = ((cfg != null ? cfg.raidAttackBase : 40f) + (cfg != null ? cfg.raidAttackPerLevel : 12f) * p.level) * U(0.7f, 1.3f);
            float pHold = strength / Mathf.Max(1e-3f, strength + attack);
            bool held = rng.NextDouble() < pHold;

            var rec = new RaidRecord
            {
                attackerName = AttackerNames[rng.Next(AttackerNames.Length)],
                attackerTeam = PickAttackerTeam(p.team, rng),
                held = held,
                isSimulated = true,
            };
            float trap = cfg != null ? cfg.raidTrapChance : 0.55f;
            float loss = held ? (cfg != null ? cfg.raidStructureLossHeld : 0.12f) : (cfg != null ? cfg.raidStructureLossFallen : 0.45f);
            for (int i = 0; i < layout.placements.Count; i++)
            {
                var d = layout.placements[i];
                bool isTrap = catalog != null && catalog.TryGet(d.defenseId, out var e) && e.trap;
                if (rng.NextDouble() < (isTrap ? trap : loss))
                {
                    consumedIndices.Add(i);
                    rec.consumed.Add(d.defenseId);
                }
            }
            if (held) rec.goldDelta = Mathf.RoundToInt((cfg != null ? cfg.raidHoldGold : 45) * U(0.8f, 1.2f));
            else rec.goldDelta = -Mathf.Min(Mathf.RoundToInt(p.gold * (cfg != null ? cfg.raidLossFraction : 0.08f)), cfg != null ? cfg.raidLossCap : 120);
            return rec;
        }
    }

    /// <summary>Outcome of a defense battle for the result card (rewards already applied and saved).</summary>
    public class DefenseSummary
    {
        public bool held;
        public bool test;
        public bool timedOut;
        public string attackerName = "";
        public TeamColorId attackerTeam;
        public int attackersPopped;
        public int attackersTotal;
        public int pointsLost;
        public float timeSeconds;
        public readonly List<string> consumed = new List<string>();
        public int gold;
        public int xp;
        public int levelBefore;
        public int levelAfter;
        public int rebuyCost;
    }
}
