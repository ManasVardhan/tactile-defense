using TactileWars.Core;

using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Battle
{
    public enum BattleMode { Attack = 0, Defense = 1 }

    /// <summary>One defense of the player's saved base, resolved for the battle scene.</summary>
    public struct DefenseSpawn
    {
        public string defenseId;
        public Vector3 position;
        public float yaw;
        /// <summary>Research driven multiplier for health and damage (ESTIMATE: +15 percent per level above 1).</summary>
        public float levelMult;
        /// <summary>Index into the saved placements (consumption is reported per index).</summary>
        public int placementIndex;
    }
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

        /// <summary>Attack battles: the unit types the player chose, in slot order (emptied slots skipped). Null keeps
        /// the army's default squad.</summary>
        public List<Army.UnitTypeId> squad;

        // Player squad (research driven). squadSize is the legacy squad's soldier count (defense battles).
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
        /// <summary>Enemy squads wait until a player soldier is within their weapon range plus 2 (tutorial levels).</summary>
        public bool passiveEnemies;
        /// <summary>Off: no counterattack wave after the first capture.</summary>
        public bool counterattack = true;

        // Map variant. Null keeps the handcrafted dressing of Battle.unity.
        public MapLayoutData map;

        // Defense battles: the player's base is defended by AI; an AI squad (the SquadController) attacks it.
        // In this mode playerTeam is the ATTACKER's color and enemyTeam is the player's color, so every gameplay
        // system keeps its roles (Team.Player attacks, Team.Enemy defends).
        public BattleMode mode = BattleMode.Attack;
        public bool defenseTest;
        public string attackerName = "";
        public readonly List<DefenseSpawn> defenderSpawns = new List<DefenseSpawn>();
        [Tooltip("Defense battles end as held after this many seconds (attackers retreat). ESTIMATE.")] public float defenseTimeLimit = 240f;
        public int defenderSquadSize = 8;
        public int strikeCharges = 3;
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
        /// <summary>Glyph disc color of its unit bar button and badge.</summary>
        public Color accent = new Color(0.56f, 0.59f, 0.66f, 1f);
        public string description = "";
    }
}
