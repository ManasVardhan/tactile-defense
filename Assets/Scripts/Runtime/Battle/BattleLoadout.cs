using TactileWars.Core;

namespace TactileWars.Battle
{
    /// <summary>Everything the meta game decides about a battle before it starts (research levels, hired mercenary,
    /// chosen opponent). Built by Meta.LoadoutBuilder from the player profile; applied by BattleSetup at scene start.
    /// Gameplay only reads it, it never references the meta layer.</summary>
    public class BattleLoadout
    {
        /// <summary>Loadout for the next (or current, on restart) battle. Null means "use the scene defaults".</summary>
        public static BattleLoadout Pending;

        public string baseId = "";
        public string baseName = "";
        public TeamColorId playerTeam = TeamColorId.Red;
        public TeamColorId enemyTeam = TeamColorId.Teal;

        // Player squad (research driven).
        public int squadSize = 40;
        public float soldierHealthMult = 1f;
        public float fireRateMult = 1f;
        public float damageMult = 1f;
        public float moveSpeedMult = 1f;
        public float leaderMult = 1f;

        // Reinforcements (research driven).
        public int reinforcementCount = 10;
        public float reinforcementChargeMult = 1f;

        // Hired mercenary (Hero model variant). Null when none.
        public MercenaryLoadout mercenary;

        // Opponent.
        public BaseLayoutData layout;
        public float enemyHealthMult = 1f;
        public float enemyDamageMult = 1f;
        public float rewardMult = 1f;
        public int difficulty = 1;
    }

    public class MercenaryLoadout
    {
        public string id = "";
        public string displayName = "";
        public float healthMult = 3f;
        public float damageMult = 1.5f;
        public float fireRateMult = 1f;
        public float scale = 1.2f;
        public float reinforcementChargeBonus;
    }
}
