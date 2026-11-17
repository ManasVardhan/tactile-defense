using System;
using TactileWars.Battle;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Lives in the battle scene: when the battle ends it grants the rewards to the profile (and saves)
    /// before the result screens open. Battles started without a launch (editor play, tests) count against the
    /// first roster base.</summary>
    public class BattleSession : MonoBehaviour
    {
        public static RewardSummary Last { get; private set; }
        public static event Action<RewardSummary> Rewarded;
        /// <summary>Defense battles: the outcome for the player's base (rewards applied and saved).</summary>
        public static DefenseSummary LastDefense { get; private set; }
        public static event Action<DefenseSummary> DefenseRewarded;
        readonly System.Collections.Generic.List<int> consumed = new System.Collections.Generic.List<int>();

        bool done;

        void OnEnable()
        {
            BattleEvents.Victory += OnEnd;
            BattleEvents.Defeat += OnEnd;
        }

        void OnDisable()
        {
            BattleEvents.Victory -= OnEnd;
            BattleEvents.Defeat -= OnEnd;
        }

        void Start() { Last = null; LastDefense = null; done = false; }

        void OnEnd(BattleResult r)
        {
            if (done) return;
            done = true;
            var setup = BattleSetup.Instance;
            if (setup != null && setup.DefenseMode)
            {
                var lo = setup.Applied;
                setup.CollectConsumed(consumed);
                var bm = BattleManager.Instance;
                int lost = 0;
                if (bm != null) foreach (var cp in bm.capturePoints) if (cp != null && cp.Captured) lost++;
                LastDefense = MetaGame.ApplyDefenseResult(r, consumed, lo != null && lo.defenseTest, lo != null ? lo.attackerName : "",
                    BattleContext.Config != null ? BattleContext.Config.playerTeam : Core.TeamColorId.Teal, lost, bm != null && bm.TimedOut);
                Debug.Log($"[Meta] defense {(LastDefense.held ? "held" : "fell")} test={LastDefense.test} consumed {LastDefense.consumed.Count} gold {LastDefense.gold} popped {LastDefense.attackersPopped}/{LastDefense.attackersTotal}");
                DefenseRewarded?.Invoke(LastDefense);
                return;
            }
            var b = BattleLaunch.CurrentBase;
            if (b == null && MetaGame.Db != null && MetaGame.Db.roster != null && MetaGame.Db.roster.bases.Length > 0) b = MetaGame.Db.roster.bases[0];
            Last = MetaGame.ApplyBattleResult(r, b);
            Debug.Log($"[Meta] rewards: gold {Last.gold} xp {Last.xp} grade {Last.grade} level {Last.levelBefore}->{Last.levelAfter} league {Last.leagueDelta:+0;-0}");
            Rewarded?.Invoke(Last);
        }
    }
}
