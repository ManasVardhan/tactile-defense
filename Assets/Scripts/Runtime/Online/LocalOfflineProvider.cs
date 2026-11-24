using System.Collections.Generic;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Online
{
    /// <summary>
    /// OFFLINE / SIMULATED provider. Serves the AI base roster as matchmaking results, a deterministic simulated
    /// leaderboard (fake commander names, seeded points) with the local player inserted by league points, and a
    /// simulated alliance list. Nothing here talks to a network. The UI labels all of it as offline / simulated.
    /// </summary>
    public class LocalOfflineProvider : IAccountService, ILeaderboardService, IAllianceService, IBaseMatchmakingService, IRaidService
    {
        static LocalOfflineProvider instance;
        public static LocalOfflineProvider Instance => instance ??= new LocalOfflineProvider();

        static readonly string[] Names =
        {
            "Sgt. Puddle", "Major Drip", "Cpt. Swirl", "Lt. Smudge", "Col. Blot", "Gen. Splash", "Pvt. Dab", "Cpl. Stroke",
            "Ace Brushwood", "Mira Tint", "Otto Gloss", "Ruby Daub", "Kip Pastel", "Nova Hue", "Zed Primer", "Lola Wash",
        };

        public bool IsOnline => false;
        public bool IsSimulated => true;
        public bool IsAvailable => true;
        public bool IsOffline => true;

        public AccountInfo CurrentAccount
        {
            get
            {
                var p = SaveSystem.Profile;
                return new AccountInfo { playerId = "local", displayName = p.playerName, team = p.team, isOffline = true };
            }
        }

        public void GetTopEntries(int count, List<LeaderboardEntry> into)
        {
            into.Clear();
            var p = SaveSystem.Profile;
            var rng = new System.Random(1234);
            var list = new List<LeaderboardEntry>(Names.Length + 1);
            for (int i = 0; i < Names.Length; i++)
            {
                int pts = 2400 - i * 150 - rng.Next(0, 90);
                list.Add(new LeaderboardEntry { name = Names[i], team = (TeamColorId)(rng.Next(0, 6)), points = Mathf.Max(0, pts), isSimulated = true });
            }
            list.Add(new LeaderboardEntry { name = p.playerName, team = p.team, points = p.leaguePoints, isLocalPlayer = true });
            list.Sort((a, b) => b.points.CompareTo(a.points));
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                e.rank = i + 1;
                if (i < count || e.isLocalPlayer) into.Add(e);
            }
        }

        public void GetAlliances(TeamColorId team, List<AllianceInfo> into)
        {
            into.Clear();
            string[] a = { "Wet Paint Club", "Brush Brigade", "The Splatterers" };
            for (int i = 0; i < a.Length; i++) into.Add(new AllianceInfo { name = a[i], team = team, members = 12 + i * 7, isSimulated = true });
        }

        // ------------------------------------------------------------ simulated raids

        /// <summary>Raids generated in this app session (capped by BaseEditorConfig.raidMaxPerSession).</summary>
        public int RaidsThisSession { get; private set; }
        readonly List<int> consumedScratch = new List<int>();

        /// <summary>When the player returns to the menu after at least raidMinAwayMinutes, simulates one raid on the
        /// saved base (at most a few per session; an empty base is never raided). The result is applied and saved.
        /// Never raids while MetaGame.BaseFeaturesEnabled is off. Always updates the "last seen" time.</summary>
        public RaidRecord CheckForRaid(System.DateTime utcNow)
        {
            var p = SaveSystem.Profile;
            var cfg = MetaGame.BaseConfig;
            RaidRecord rec = null;
            long last = p.lastSeenUtcTicks;
            int maxPerSession = cfg != null ? cfg.raidMaxPerSession : 2;
            // Base features are off in this build: the player has no base to see or edit, so nothing raids it.
            if (MetaGame.BaseFeaturesEnabled && last > 0 && RaidsThisSession < maxPerSession && p.BaseLayout.placements.Count > 0)
            {
                double away = (utcNow - new System.DateTime(last, System.DateTimeKind.Utc)).TotalMinutes;
                if (away >= (cfg != null ? cfg.raidMinAwayMinutes : 30f))
                    rec = SimulateRaid(unchecked((int)(utcNow.Ticks / System.TimeSpan.TicksPerMinute)), utcNow);
            }
            MetaGame.MarkSeen(utcNow);
            return rec;
        }

        /// <summary>Simulates and applies one raid now (also used by tests and captures with a fixed seed).</summary>
        public RaidRecord SimulateRaid(int seed, System.DateTime utcNow)
        {
            var p = SaveSystem.Profile;
            if (p.BaseLayout.placements.Count == 0) return null;
            var rec = RaidSimulator.Simulate(p, MetaGame.Defenses, MetaGame.BaseConfig, MetaGame.ResearchLevel, seed, consumedScratch);
            rec.utcTicks = utcNow.Ticks;
            MetaGame.ApplyRaid(rec, consumedScratch);
            RaidsThisSession++;
            return rec;
        }

        public void GetOpponents(List<OpponentBase> into)
        {
            into.Clear();
            var roster = MetaGame.Db != null ? MetaGame.Db.roster : null;
            if (roster == null) return;
            foreach (var b in roster.bases)
                if (b != null) into.Add(new OpponentBase { baseId = b.id, ownerName = b.commanderName, team = b.enemyTeam, difficulty = b.difficulty, isAI = true });
        }
    }
}
