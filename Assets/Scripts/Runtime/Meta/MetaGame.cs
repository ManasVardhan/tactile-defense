using System;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    public enum PurchaseResult { Ok, Locked, NotEnoughGold, Maxed, Invalid, NotEnoughPrisms }

    /// <summary>Summary of one battle's rewards, produced by MetaGame.ApplyBattleResult and shown by the result screens.</summary>
    public class RewardSummary
    {
        public BattleResult result;
        public string baseId = "";
        public string baseName = "";
        public bool victory;
        public string grade = "D";
        public string survivorGrade = "D";
        public string speedGrade = "D";
        public float score;
        public int gold;
        public int xp;
        public int prisms;
        public int leagueDelta;
        public int levelBefore;
        public int levelAfter;
        public int levelUpGold;
        public int levelUpPrisms;
        public bool firstWin;
        public bool LeveledUp => levelAfter > levelBefore;
    }

    /// <summary>All profile changing operations of the meta game. Every change saves immediately (autosave) and
    /// raises Changed so open screens refresh.</summary>
    public static partial class MetaGame
    {
        static MetaDatabase db;
        public static event Action Changed;

        public static MetaDatabase Db
        {
            get { if (db == null) db = MetaDatabase.Load(); EnsureHook(); return db; }
            set { db = value; EnsureHook(); }
        }

        static bool hooked;
        static void EnsureHook()
        {
            if (hooked) return;
            hooked = true;
            SaveSystem.NewProfileInitializer = InitNewProfile;
        }

        public static PlayerProfile Profile { get { EnsureHook(); return SaveSystem.Profile; } }
        public static ProgressionConfig Progression => Db != null ? Db.progression : null;

        static void InitNewProfile(PlayerProfile p)
        {
            var prog = db != null ? db.progression : null;
            p.gold = prog != null ? prog.startGold : 400;
            p.prisms = prog != null ? prog.startPrisms : 10;
            // The Mine is a starter defense: unlocked for the base editor from the beginning.
            p.SetResearch("def_mine", 1);
        }

        public static void Commit()
        {
            SaveSystem.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ identity

        public static void SetTeam(TeamColorId team)
        {
            var p = Profile;
            p.team = team;
            p.teamChosen = true;
            Commit();
        }

        public static void SetName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (name.Length > 16) name = name.Substring(0, 16);
            Profile.playerName = name;
            Commit();
        }

        // ------------------------------------------------------------ levels and leagues

        public static int Level => Profile.level;

        /// <summary>Adds XP, applies level ups with their rewards. Returns the number of levels gained.</summary>
        public static int AddXp(int amount, out int rewardGold, out int rewardPrisms, bool commit = true)
        {
            rewardGold = 0; rewardPrisms = 0;
            var p = Profile;
            var prog = Progression;
            p.xp = Mathf.Max(0, p.xp + Mathf.Max(0, amount));
            p.stats.xpEarned += Mathf.Max(0, amount);
            int before = p.level;
            int target = prog != null ? prog.LevelForXp(p.xp) : p.level;
            while (p.level < target)
            {
                p.level++;
                if (prog != null)
                {
                    var r = prog.RewardFor(p.level);
                    rewardGold += r.gold;
                    rewardPrisms += r.prisms;
                }
            }
            p.gold += rewardGold;
            p.prisms += rewardPrisms;
            if (commit) Commit();
            return p.level - before;
        }

        public static int LeagueIndex => Progression != null ? Progression.LeagueIndex(Profile.leaguePoints) : 0;

        public static League CurrentLeague
        {
            get
            {
                var prog = Progression;
                if (prog == null || prog.leagues.Length == 0) return new League { name = "League", minPoints = 0, color = Color.white };
                return prog.leagues[prog.LeagueIndex(Profile.leaguePoints)];
            }
        }

        // ------------------------------------------------------------ gold and prisms

        public static bool TrySpendGold(int amount, bool commit = true)
        {
            var p = Profile;
            if (amount < 0 || p.gold < amount) return false;
            p.gold -= amount;
            if (commit) Commit();
            return true;
        }

        public static PurchaseResult ExchangePrisms()
        {
            var prog = Progression;
            if (prog == null) return PurchaseResult.Invalid;
            var p = Profile;
            if (p.prisms < prog.exchangePrisms) return PurchaseResult.NotEnoughPrisms;
            p.prisms -= prog.exchangePrisms;
            p.gold += prog.exchangeGold;
            Commit();
            return PurchaseResult.Ok;
        }

        // ------------------------------------------------------------ research

        public static int ResearchLevel(string id) => Profile.GetResearch(id);

        public static PurchaseResult CanBuyResearch(ResearchNode n)
        {
            if (n == null) return PurchaseResult.Invalid;
            int lvl = ResearchLevel(n.id);
            if (lvl >= n.MaxLevel) return PurchaseResult.Maxed;
            var step = n.steps[lvl];
            if (Profile.level < step.requiredLevel) return PurchaseResult.Locked;
            if (Profile.gold < step.goldCost) return PurchaseResult.NotEnoughGold;
            return PurchaseResult.Ok;
        }

        public static PurchaseResult BuyResearch(ResearchNode n)
        {
            var r = CanBuyResearch(n);
            if (r != PurchaseResult.Ok) return r;
            var p = Profile;
            int lvl = p.GetResearch(n.id);
            p.gold -= n.steps[lvl].goldCost;
            p.SetResearch(n.id, lvl + 1);
            Commit();
            return PurchaseResult.Ok;
        }

        /// <summary>Sum of the researched values of every node with this effect.</summary>
        public static float EffectValue(ResearchEffect e)
        {
            var tree = Db != null ? Db.research : null;
            if (tree == null) return 0f;
            float v = 0f;
            for (int i = 0; i < tree.nodes.Length; i++)
                if (tree.nodes[i].effect == e) v += ResearchTree.ValueAt(tree.nodes[i], ResearchLevel(tree.nodes[i].id));
            return v;
        }

        // ------------------------------------------------------------ mercenaries

        public static PurchaseResult CanUnlockMercenary(MercenaryDef m)
        {
            if (m == null) return PurchaseResult.Invalid;
            if (Profile.OwnsMercenary(m.id)) return PurchaseResult.Maxed;
            if (Profile.level < m.unlockLevel) return PurchaseResult.Locked;
            if (Profile.gold < m.unlockCost) return PurchaseResult.NotEnoughGold;
            return PurchaseResult.Ok;
        }

        public static PurchaseResult UnlockMercenary(MercenaryDef m)
        {
            var r = CanUnlockMercenary(m);
            if (r != PurchaseResult.Ok) return r;
            Profile.gold -= m.unlockCost;
            Profile.ownedMercenaries.Add(m.id);
            Profile.hiredMercenary = m.id;
            Commit();
            return PurchaseResult.Ok;
        }

        /// <summary>Select the mercenary that joins the next battles ("" for none). Must be owned.</summary>
        public static bool SetHired(string id)
        {
            id ??= "";
            if (id.Length > 0 && !Profile.OwnsMercenary(id)) return false;
            Profile.hiredMercenary = id;
            Commit();
            return true;
        }

        public static MercenaryDef HiredMercenary => Db != null ? Db.FindMercenary(Profile.hiredMercenary) : null;

        // ------------------------------------------------------------ bases

        public static bool IsBaseUnlocked(BaseDefinition b) => b != null && Profile.level >= b.requiredLevel;
        public static bool IsBaseDefeated(BaseDefinition b) => b != null && Profile.defeatedBases.Contains(b.id);

        // ------------------------------------------------------------ battle results

        public static float GradeScore(BattleResult r, ProgressionConfig prog, out float survivorRatio, out float timeScore)
        {
            int total = r.soldiersSurvived + r.soldiersLost;
            survivorRatio = total > 0 ? r.soldiersSurvived / (float)total : 0f;
            float par = prog != null ? prog.parTimeSeconds : 150f;
            timeScore = Mathf.Clamp01((2f * par - r.timeSeconds) / (1.5f * par));
            float w = prog != null ? prog.survivorWeight : 0.7f;
            return survivorRatio * w + timeScore * (1f - w);
        }

        /// <summary>Grants the battle's gold and XP (gold and XP already include the opponent's reward multiplier),
        /// applies the grade bonus, league points, first win prisms and stats, then saves.</summary>
        public static RewardSummary ApplyBattleResult(BattleResult r, BaseDefinition b)
        {
            var p = Profile;
            var prog = Progression;
            var s = new RewardSummary { result = r, victory = r.victory, levelBefore = p.level };
            if (b != null) { s.baseId = b.id; s.baseName = b.displayName; }

            s.score = GradeScore(r, prog, out float surv, out float time);
            if (prog != null)
            {
                s.grade = r.victory ? prog.Grade(s.score) : "D";
                s.survivorGrade = prog.Grade(surv);
                s.speedGrade = r.victory ? prog.Grade(time) : "D";
            }
            float mult = 1f;
            if (r.victory && prog != null) mult = prog.gradeMultipliers[ProgressionConfig.GradeIndex(s.grade)];
            s.gold = Mathf.Max(0, Mathf.RoundToInt(r.gold * mult));
            s.xp = Mathf.Max(0, Mathf.RoundToInt(r.xp * mult));

            if (r.victory)
            {
                int diff = b != null ? b.difficulty : 1;
                s.leagueDelta = prog != null ? prog.leaguePointsPerWin + prog.leaguePointsPerDifficulty * diff : 20;
                if (b != null && !p.defeatedBases.Contains(b.id))
                {
                    p.defeatedBases.Add(b.id);
                    s.firstWin = true;
                    s.prisms = prog != null ? prog.firstWinPrisms : 3;
                }
            }
            else s.leagueDelta = -Mathf.Min(p.leaguePoints, prog != null ? prog.leaguePointsLostOnDefeat : 8);

            p.gold += s.gold;
            p.prisms += s.prisms;
            p.leaguePoints = Mathf.Max(0, p.leaguePoints + s.leagueDelta);
            var st = p.stats;
            st.battles++;
            if (r.victory) st.victories++; else st.defeats++;
            st.kills += r.kills;
            st.structuresDestroyed += r.structuresDestroyed;
            st.soldiersLost += r.soldiersLost;
            st.goldEarned += s.gold;
            if (r.victory)
            {
                if (string.IsNullOrEmpty(st.bestGrade) || ProgressionConfig.GradeIndex(s.grade) < ProgressionConfig.GradeIndex(st.bestGrade)) st.bestGrade = s.grade;
                if (st.fastestVictory <= 0f || r.timeSeconds < st.fastestVictory) st.fastestVictory = r.timeSeconds;
            }
            AddXp(s.xp, out s.levelUpGold, out s.levelUpPrisms, false);
            s.levelAfter = p.level;
            Commit();
            return s;
        }
    }
}
