using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Turns the profile (research levels, hired mercenary, chosen team) and an opponent into the
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
            }
            if (lo.enemyTeam == lo.playerTeam) lo.enemyTeam = AlternateEnemy(lo.playerTeam);
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

        public static BaseDefinition CurrentBase { get; private set; }
        public static bool LastMercenaryUnpaid { get; private set; }

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
