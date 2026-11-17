using System;
using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Base defense side of the meta game: caps, unlocks, editing sessions, defense battle results and
    /// simulated raids. Every change saves immediately.</summary>
    public static partial class MetaGame
    {
        public static BaseEditorConfig BaseConfig => Db != null ? Db.baseEditor : null;
        public static DefenseCatalog Defenses => Db != null ? Db.defenses : null;

        public static int BudgetCap => BaseConfig != null ? BaseConfig.BudgetCap(Profile.level) : 60;
        public static int SpaceCap => BaseConfig != null ? BaseConfig.SpaceCap(Profile.level) : 8;

        /// <summary>Research level of a defense type (always 1 for types without a research node).</summary>
        public static int DefenseLevel(string defenseId)
        {
            var cat = Defenses;
            if (cat == null || !cat.TryGet(defenseId, out var e)) return 0;
            return string.IsNullOrEmpty(e.researchId) ? 1 : ResearchLevel(e.researchId);
        }

        public static float DefenseLevelMult(string defenseId)
        {
            int l = Mathf.Max(1, DefenseLevel(defenseId));
            return BaseConfig != null ? BaseConfig.LevelMult(l) : 1f;
        }

        public static BaseEditSession BeginBaseEdit()
        {
            var cfg = BaseConfig;
            return new BaseEditSession(Profile.BaseLayout, Profile.gold, Defenses, Db != null ? Db.homeMap : null, BudgetCap, SpaceCap,
                ResearchLevel, cfg != null ? cfg.gridCell : 1f);
        }

        /// <summary>Pays the session's purchases and writes its layout. False when gold ran out meanwhile.</summary>
        public static bool CommitBaseEdit(BaseEditSession s)
        {
            if (s == null) return false;
            var p = Profile;
            if (p.gold < s.PendingGold) return false;
            p.gold -= s.PendingGold;
            s.WriteTo(p.BaseLayout);
            Commit();
            return true;
        }

        /// <summary>Moves the given placement indices (of the saved layout) to the destroyed list.</summary>
        static int Consume(List<int> indices, List<string> ids)
        {
            var layout = Profile.BaseLayout;
            if (indices == null || indices.Count == 0) return 0;
            var sorted = new List<int>(indices);
            sorted.Sort();
            int n = 0;
            for (int k = sorted.Count - 1; k >= 0; k--)
            {
                int i = sorted[k];
                if (i < 0 || i >= layout.placements.Count) continue;
                if (k < sorted.Count - 1 && sorted[k + 1] == i) continue;
                var d = layout.placements[i];
                layout.placements.RemoveAt(i);
                layout.destroyed.Add(d);
                ids?.Add(d.defenseId);
                n++;
            }
            Profile.defenseStats.defensesConsumed += n;
            return n;
        }

        /// <summary>Applies a finished defense battle. BattleResult is from the ATTACKER's side (victory means the
        /// base fell). Tests pay nothing and consume nothing.</summary>
        public static DefenseSummary ApplyDefenseResult(BattleResult r, List<int> consumed, bool test, string attackerName,
            Core.TeamColorId attackerTeam, int pointsLost, bool timedOut)
        {
            var p = Profile;
            var cfg = BaseConfig;
            var s = new DefenseSummary
            {
                held = !r.victory, test = test, timedOut = timedOut, attackerName = attackerName ?? "", attackerTeam = attackerTeam,
                attackersPopped = r.soldiersLost, attackersTotal = r.soldiersLost + r.soldiersSurvived, pointsLost = pointsLost,
                timeSeconds = r.timeSeconds, levelBefore = p.level, levelAfter = p.level,
            };
            if (test) return s;
            Consume(consumed, s.consumed);
            if (s.held)
            {
                s.gold = (cfg != null ? cfg.defenseHoldGold : 60) + (cfg != null ? cfg.defenseHoldGoldPerAttacker : 1) * r.soldiersLost;
                s.xp = cfg != null ? cfg.defenseHoldXp : 40;
                p.defenseStats.battlesHeld++;
            }
            else
            {
                s.xp = cfg != null ? cfg.defenseFallXp : 10;
                p.defenseStats.battlesLost++;
            }
            p.gold += s.gold;
            p.defenseStats.goldFromDefense += s.gold;
            AddXp(s.xp, out int lg, out int lp, false);
            s.levelAfter = p.level;
            foreach (var d in p.BaseLayout.destroyed) if (Defenses != null && Defenses.TryGet(d.defenseId, out var e)) s.rebuyCost += e.goldCost;
            Commit();
            return s;
        }

        /// <summary>Applies a simulated raid: consumed defenses become wrecks, gold changes, the record is logged.</summary>
        public static void ApplyRaid(RaidRecord rec, List<int> consumedIndices)
        {
            if (rec == null) return;
            var p = Profile;
            var cfg = BaseConfig;
            rec.consumed.Clear();
            Consume(consumedIndices, rec.consumed);
            p.gold = Mathf.Max(0, p.gold + rec.goldDelta);
            if (rec.held) { p.defenseStats.raidsHeld++; if (rec.goldDelta > 0) p.defenseStats.goldFromDefense += rec.goldDelta; }
            else p.defenseStats.raidsLost++;
            p.raidLog.Insert(0, rec);
            int max = cfg != null ? cfg.raidLogMax : 6;
            while (p.raidLog.Count > max) p.raidLog.RemoveAt(p.raidLog.Count - 1);
            Commit();
        }

        public static void MarkSeen(DateTime utcNow)
        {
            Profile.lastSeenUtcTicks = utcNow.Ticks;
            SaveSystem.Save();
        }
    }
}
