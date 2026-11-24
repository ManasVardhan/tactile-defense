using System;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Save
{
    /// <summary>Upgrades older save JSON to the current PlayerProfile layout. One step per version.
    /// Version 1 was the pre release prototype format ("name", "color", "coins", "music"/"sfx" as 0..100 ints).
    /// Version 2 to 3: the level ladder retires the Squad Size and Bigger Backup research (refunded in gold).</summary>
    public static class SaveMigrations
    {
        [Serializable]
        class VersionHeader { public int version; }

        [Serializable]
        class ProfileV1
        {
            public int version;
            public string name;
            public int color;
            public int coins;
            public int gems;
            public int level;
            public int xp;
            public int music = 70;
            public int sfx = 90;
        }

        public static int ReadVersion(string json)
        {
            try
            {
                var h = JsonUtility.FromJson<VersionHeader>(json);
                return h != null ? h.version : 0;
            }
            catch { return -1; }
        }

        /// <summary>Returns a current version profile, or null when the json cannot be read.</summary>
        public static PlayerProfile Migrate(string json, int fromVersion)
        {
            PlayerProfile p;
            if (fromVersion <= 1)
            {
                var v1 = JsonUtility.FromJson<ProfileV1>(json);
                if (v1 == null) return null;
                p = new PlayerProfile
                {
                    playerName = string.IsNullOrWhiteSpace(v1.name) ? "Commander" : v1.name,
                    team = Enum.IsDefined(typeof(TeamColorId), v1.color) ? (TeamColorId)v1.color : TeamColorId.Red,
                    teamChosen = true,
                    gold = Mathf.Max(0, v1.coins),
                    prisms = Mathf.Max(0, v1.gems),
                    level = Mathf.Max(1, v1.level),
                    xp = Mathf.Max(0, v1.xp),
                };
                p.settings.musicVolume = Mathf.Clamp01(v1.music / 100f);
                p.settings.sfxVolume = Mathf.Clamp01(v1.sfx / 100f);
            }
            else
            {
                // Version 2 has the current layout; its retired research is handled below.
                p = JsonUtility.FromJson<PlayerProfile>(json);
                if (p == null) return null;
            }
            if (fromVersion <= 2) ToVersion3(p);
            p.version = SaveSystem.CurrentVersion;
            return p;
        }

        // ------------------------------------------------------------------ 2 -> 3: the level ladder

        /// <summary>Gold cost of each level of the research nodes retired by the level ladder (squad slots replace
        /// Squad Size, a backup call restores a unit instead of adding soldiers). The tables are the nodes' last
        /// published costs, kept here because the nodes are gone from the research tree.</summary>
        static readonly (string id, int[] costs)[] RetiredResearch =
        {
            ("squad_size", new[] { 150, 320, 560, 900, 1400 }),
            ("reinforcement_size", new[] { 220, 450, 800, 1250 }),
        };

        /// <summary>Gold paid for the first 'level' levels of a retired research node (0 for other nodes).</summary>
        public static int RetiredResearchRefund(string id, int level)
        {
            foreach (var r in RetiredResearch)
            {
                if (r.id != id) continue;
                int sum = 0;
                for (int i = 0; i < level && i < r.costs.Length; i++) sum += r.costs[i];
                return sum;
            }
            return 0;
        }

        static bool IsRetired(string id)
        {
            foreach (var r in RetiredResearch) if (r.id == id) return true;
            return false;
        }

        /// <summary>Refunds and removes the retired research. The squad is filled afterwards by
        /// SaveSystem.MigratedProfileInitializer (it needs the level ladder).</summary>
        static void ToVersion3(PlayerProfile p)
        {
            if (p.research == null) return;
            for (int i = p.research.Count - 1; i >= 0; i--)
            {
                var e = p.research[i];
                if (e == null || !IsRetired(e.id)) continue;
                p.gold = Mathf.Max(0, p.gold) + RetiredResearchRefund(e.id, e.level);
                p.research.RemoveAt(i);
            }
        }
    }
}
