using System.Collections.Generic;
using TactileWars.Core;

namespace TactileWars.Online
{
    // Interfaces only. This build has NO server and NO real multiplayer: the only implementation is
    // LocalOfflineProvider, which serves AI bases and a simulated local leaderboard. Any UI that shows this data
    // must label it as offline / simulated.

    public struct AccountInfo
    {
        public string playerId;
        public string displayName;
        public TeamColorId team;
        public bool isOffline;
    }

    public interface IAccountService
    {
        bool IsOnline { get; }
        AccountInfo CurrentAccount { get; }
    }

    public struct LeaderboardEntry
    {
        public int rank;
        public string name;
        public TeamColorId team;
        public int points;
        public bool isLocalPlayer;
        public bool isSimulated;
    }

    public interface ILeaderboardService
    {
        bool IsSimulated { get; }
        /// <summary>Fills 'into' with the top entries plus the local player.</summary>
        void GetTopEntries(int count, List<LeaderboardEntry> into);
    }

    public struct AllianceInfo
    {
        public string name;
        public TeamColorId team;
        public int members;
        public bool isSimulated;
    }

    public interface IAllianceService
    {
        bool IsAvailable { get; }
        /// <summary>Simulated alliances of the player's color (no gameplay effect).</summary>
        void GetAlliances(TeamColorId team, List<AllianceInfo> into);
    }

    public struct OpponentBase
    {
        public string baseId;
        public string ownerName;
        public TeamColorId team;
        public int difficulty;
        public bool isAI;
    }

    public interface IBaseMatchmakingService
    {
        bool IsOffline { get; }
        /// <summary>Opponents available to attack (AI bases from the roster in this build).</summary>
        void GetOpponents(List<OpponentBase> into);
    }

    /// <summary>Incoming raids on the player's base. Offline build: SIMULATED results only (LocalOfflineProvider),
    /// generated from a documented statistical model, never real players.</summary>
    public interface IRaidService
    {
        bool IsSimulated { get; }
        /// <summary>Returns a raid that happened while the player was away (already applied to the profile), or null.</summary>
        TactileWars.Save.RaidRecord CheckForRaid(System.DateTime utcNow);
    }
}
