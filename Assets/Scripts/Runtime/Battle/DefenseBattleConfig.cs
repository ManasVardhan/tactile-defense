using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Tunables for defense battles: the AI attacker's decisions and the defender's paint strike.
    /// ALL VALUES ARE ESTIMATES (the original's attacker behaviour is not documented; attacks there are made by
    /// real players, here an AI plays the attacker against the player's saved base).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Defense Battle Config")]
    public class DefenseBattleConfig : ScriptableObject
    {
        [Header("AI attacker")]
        [Tooltip("Seconds between decisions.")] public float thinkInterval = 0.4f;
        [Tooltip("Defenders within this distance of the squad are engaged.")] public float engageRange = 11f;
        [Tooltip("Engage line stands this far from the target.")] public float standOff = 5.2f;
        [Tooltip("Distance of each advance order along the route.")] public float stepDistance = 7f;
        [Tooltip("Visible mines this close to the next step make the squad spread into a wide ring.")] public float mineAlertRange = 5f;
        public float spreadRadius = 5.2f;
        [Tooltip("Ring radius when clustering on a capture pad.")] public float padClusterRadius = 1.6f;
        [Tooltip("Seconds before the AI taps a poppable flag bearer bubble.")] public float bubbleReaction = 0.8f;
        public float bubbleRange = 16f;
        [Tooltip("Re-issue the current formation at most this often.")] public float reformInterval = 2.5f;
        [Tooltip("Engaging without progress for this long makes the squad push closer.")] public float stallSeconds = 9f;
        [Tooltip("Line length per soldier, clamped.")] public float lineSpacing = 0.45f;
        public float minLineLength = 6f;
        public float maxLineLength = 15f;

        [Header("Defender paint strike (optional support ability)")]
        public int strikeCharges = 3;
        public float strikeRadius = 3f;
        public float strikeDamage = 45f;
        [Tooltip("Fall time between the tap and the impact.")] public float strikeDelay = 0.7f;
        public float strikeCooldown = 4f;

        [Header("Attacker squad (scales with the player's level)")]
        public int attackerBaseSize = 30;
        public int attackerSizePerLevel = 2;
        public int attackerMaxSize = 56;
        public float attackerHealthPerLevel = 0.04f;
        public float timeLimit = 240f;
        public int defenderSquadSize = 8;
    }
}
