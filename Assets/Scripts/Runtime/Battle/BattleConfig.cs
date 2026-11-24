using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Battle rules. Verified: two capture points in sequence, win when both are captured, lose when all
    /// units die, reinforcement bar fills from kills. All numbers are estimates.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [Header("Capture (estimates)")]
        public float captureRadius = 2.9f;
        [Tooltip("Seconds to capture with the squad on the pad.")] public float captureTime = 4f;
        [Tooltip("Progress lost per second when nobody holds the pad.")] public float captureDecay = 0.08f;
        [Tooltip("Legacy squad (defense battles): extra speed per soldier on the pad, capped.")] public float perSoldierBonus = 0.03f;
        public int maxSoldierBonus = 10;
        [Tooltip("Attack battles (an army of 9): extra speed per soldier on the pad. With 1/3 a full unit of 3 captures 1.5 times as fast as one soldier, and 9 soldiers twice as fast as 3.")]
        public float armyPerSoldierBonus = 1f / 3f;
        [Tooltip("Attack battles: soldiers counted for the bonus, at most.")] public int armyMaxSoldierBonus = 9;

        [Header("Deployment grid (hidden: the player sees round markers, never the hexes)")]
        [Tooltip("Outer radius (center to corner) of a deployment hex in world units.")] public float hexOuterRadius = DefaultHexOuterRadius;
        /// <summary>The one source of the deployment hex size: the field default, the asset builder and every fallback
        /// read it.</summary>
        public const float DefaultHexOuterRadius = 1.5f;

        [Header("Reinforcements (estimates)")]
        public float meterRequired = 30f;
        [Tooltip("Each reinforcement call raises the next meter requirement by this fraction (ESTIMATE, phase 8 balance: without it researched squads got near endless backup).")]
        public float meterGrowthPerCall = 0.3f;
        public float perSoldierKill = 1f;
        public float perStructureKill = 4f;
        public float perSquadWipe = 3f;
        public int reinforcementCount = 10;

        [Header("Counterattack after the first capture (ESTIMATE: multiplayer.it mentions defending against waves)")]
        public bool counterattackEnabled = true;
        [Tooltip("Seconds after the first capture before the wave leaves the nearest spawn hatch.")] public float counterattackDelay = 3f;
        public int counterattackSize = 6;
        [Tooltip("March speed of the wave's anchor.")] public float counterattackSpeed = 2.2f;

        [Header("Rewards (estimates)")]
        public int goldBase = 40;
        public int goldPerKill = 2;
        public int goldPerStructure = 20;
        public int goldVictoryBonus = 120;
        public int xpBase = 25;
        public int xpPerKill = 3;
        public int xpVictoryBonus = 150;
        [Range(0, 1)] public float defeatRewardFactor = 0.35f;
    }
}
