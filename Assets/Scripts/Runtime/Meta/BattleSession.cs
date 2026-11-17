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

        void Start() { Last = null; done = false; }

        void OnEnd(BattleResult r)
        {
            if (done) return;
            done = true;
            var b = BattleLaunch.CurrentBase;
            if (b == null && MetaGame.Db != null && MetaGame.Db.roster != null && MetaGame.Db.roster.bases.Length > 0) b = MetaGame.Db.roster.bases[0];
            Last = MetaGame.ApplyBattleResult(r, b);
            Debug.Log($"[Meta] rewards: gold {Last.gold} xp {Last.xp} grade {Last.grade} level {Last.levelBefore}->{Last.levelAfter} league {Last.leagueDelta:+0;-0}");
            Rewarded?.Invoke(Last);
        }
    }
}
