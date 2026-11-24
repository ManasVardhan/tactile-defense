using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Turns the profile (squad, research levels, hired mercenary, chosen team) and an opponent into the
    /// BattleLoadout that BattleSetup applies when the battle scene starts.</summary>
    public static class LoadoutBuilder
    {
        public const int DefaultSquad = 40;
        public const int DefaultReinforcement = 10;

        public static BattleLoadout Build(PlayerProfile p, MetaDatabase db, BaseDefinition b, bool includeMercenary = true)
        {
            var lo = new BattleLoadout();
            float V(ResearchEffect e) => EffectValue(p, db, e);
            int baseSquad = db != null && db.squadConfig != null ? db.squadConfig.soldierCount : DefaultSquad;
            int baseRe = db != null && db.battleConfig != null ? db.battleConfig.reinforcementCount : DefaultReinforcement;

            lo.playerTeam = p.team;
            lo.squad = Progression.Fielded(Progression.RepairStored(p.squad, Progression.LevelsCleared(p, db)));
            lo.squadSize = baseSquad + Mathf.RoundToInt(V(ResearchEffect.SquadSize));
            lo.soldierHealthMult = 1f + V(ResearchEffect.SoldierHealth);
            lo.fireRateMult = 1f + V(ResearchEffect.FireRate);
            lo.damageMult = 1f + V(ResearchEffect.PaintDamage);
            lo.moveSpeedMult = 1f + V(ResearchEffect.MoveSpeed);
            lo.reinforcementCount = baseRe + Mathf.RoundToInt(V(ResearchEffect.ReinforcementSize));
            lo.reinforcementChargeMult = 1f + V(ResearchEffect.ReinforcementCharge);
            lo.leaderMult = 1f + V(ResearchEffect.HeroAbility);

            if (includeMercenary && db != null)
            {
                var m = db.FindMercenary(p.hiredMercenary);
                if (m != null && p.OwnsMercenary(m.id))
                    lo.mercenary = new MercenaryLoadout
                    {
                        id = m.id, displayName = m.displayName, healthMult = m.healthMult, damageMult = m.damageMult,
                        fireRateMult = m.fireRateMult, scale = m.scale, reinforcementChargeBonus = m.reinforcementChargeBonus,
                        accent = m.accent, description = m.description,
                    };
            }

            if (b != null)
            {
                lo.baseId = b.id;
                lo.baseName = b.displayName;
                lo.enemyTeam = b.enemyTeam;
                lo.layout = b.layout;
                lo.enemyHealthMult = b.enemyHealthMult;
                lo.enemyDamageMult = b.enemyDamageMult;
                lo.rewardMult = b.rewardMultiplier;
                lo.difficulty = b.difficulty;
                lo.map = b.map;
                lo.passiveEnemies = b.passiveEnemies;
                lo.counterattack = b.counterattack;
            }
            if (lo.enemyTeam == lo.playerTeam) lo.enemyTeam = AlternateEnemy(lo.playerTeam);
            return lo;
        }

        /// <summary>Defense battle against the player's saved base: an AI squad (scaled with the player's level)
        /// attacks. Roles stay as in attacks: playerTeam is the ATTACKER color, enemyTeam is the player's color.</summary>
        public static BattleLoadout BuildDefense(PlayerProfile p, MetaDatabase db, bool test, int seed)
        {
            var lo = new BattleLoadout { mode = BattleMode.Defense, defenseTest = test };
            var rng = new System.Random(seed);
            var cfg = db != null ? db.defenseBattle : null;
            var bcfg = db != null ? db.baseEditor : null;
            lo.enemyTeam = p.team;
            lo.playerTeam = RaidSimulator.PickAttackerTeam(p.team, rng);
            lo.attackerName = RaidSimulator.AttackerNames[rng.Next(RaidSimulator.AttackerNames.Length)];
            lo.baseId = "home";
            lo.baseName = "Your base";
            lo.map = db != null ? db.homeMap : null;
            int lvl = Mathf.Max(1, p.level);
            lo.squadSize = cfg != null ? Mathf.Clamp(cfg.attackerBaseSize + cfg.attackerSizePerLevel * lvl, 6, cfg.attackerMaxSize) : 32;
            lo.soldierHealthMult = 1f + (cfg != null ? cfg.attackerHealthPerLevel : 0.04f) * (lvl - 1);
            lo.reinforcementCount = 8;
            lo.defenseTimeLimit = cfg != null ? cfg.timeLimit : 240f;
            lo.defenderSquadSize = cfg != null ? cfg.defenderSquadSize : 8;
            lo.strikeCharges = cfg != null ? cfg.strikeCharges : 3;
            var placements = p.BaseLayout.placements;
            for (int i = 0; i < placements.Count; i++)
            {
                var d = placements[i];
                int rl = 1;
                if (db != null && db.defenses != null && db.defenses.TryGet(d.defenseId, out var e) && !string.IsNullOrEmpty(e.researchId))
                    rl = Mathf.Max(1, p.GetResearch(e.researchId));
                lo.defenderSpawns.Add(new DefenseSpawn
                {
                    defenseId = d.defenseId, position = new Vector3(d.x, 0f, d.z), yaw = d.rotation,
                    levelMult = bcfg != null ? bcfg.LevelMult(rl) : 1f, placementIndex = i,
                });
            }
            return lo;
        }

        public static TeamColorId AlternateEnemy(TeamColorId player) => player == TeamColorId.Teal ? TeamColorId.Purple : TeamColorId.Teal;

        public static float EffectValue(PlayerProfile p, MetaDatabase db, ResearchEffect e)
        {
            var tree = db != null ? db.research : null;
            if (tree == null) return 0f;
            float v = 0f;
            foreach (var n in tree.nodes)
                if (n.effect == e) v += ResearchTree.ValueAt(n, p.GetResearch(n.id));
            return v;
        }
    }

    /// <summary>Prepares the next battle: builds the loadout, pays the mercenary's hire fee (the mercenary stays home
    /// when the player cannot pay) and stores everything for the battle scene.</summary>
    public static class BattleLaunch
    {
        public const string BattleScene = "Battle";
        public const string MenuScene = "MainMenu";

        public const string BaseEditorScene = "BaseEditor";

        public static BaseDefinition CurrentBase { get; private set; }
        public static bool LastMercenaryUnpaid { get; private set; }
        /// <summary>True when the pending battle defends the player's base.</summary>
        public static bool IsDefense => BattleLoadout.Pending != null && BattleLoadout.Pending.mode == BattleMode.Defense;
        /// <summary>Where the result screen returns to (the base editor after a defense test).</summary>
        public static string ReturnScene => IsDefense && BattleLoadout.Pending.defenseTest ? BaseEditorScene : MenuScene;

        /// <summary>Prepares a defense battle on the player's saved base (test: no rewards, nothing consumed).</summary>
        public static BattleLoadout PrepareDefense(bool test, int seed = -1)
        {
            if (seed < 0) seed = System.Environment.TickCount & 0x7fffffff;
            var lo = LoadoutBuilder.BuildDefense(MetaGame.Profile, MetaGame.Db, test, seed);
            CurrentBase = null;
            LastMercenaryUnpaid = false;
            BattleLoadout.Pending = lo;
            return lo;
        }

        public static BattleLoadout Prepare(BaseDefinition b)
        {
            var db = MetaGame.Db;
            var p = MetaGame.Profile;
            var merc = db != null ? db.FindMercenary(p.hiredMercenary) : null;
            bool takeMerc = false;
            LastMercenaryUnpaid = false;
            if (merc != null && p.OwnsMercenary(merc.id))
            {
                if (MetaGame.TrySpendGold(merc.hireCost)) takeMerc = true;
                else LastMercenaryUnpaid = true;
            }
            var lo = LoadoutBuilder.Build(p, db, b, takeMerc);
            CurrentBase = b;
            BattleLoadout.Pending = lo;
            return lo;
        }

        /// <summary>Restart keeps the same loadout; the mercenary fee is not charged twice.</summary>
        public static void Clear()
        {
            CurrentBase = null;
            BattleLoadout.Pending = null;
        }
    }
}
