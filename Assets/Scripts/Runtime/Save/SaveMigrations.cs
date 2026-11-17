using System;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Save
{
    /// <summary>Upgrades older save JSON to the current PlayerProfile layout. One step per version.
    /// Version 1 was the pre release prototype format ("name", "color", "coins", "music"/"sfx" as 0..100 ints).</summary>
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
            if (fromVersion <= 1)
            {
                var v1 = JsonUtility.FromJson<ProfileV1>(json);
                if (v1 == null) return null;
                var p = new PlayerProfile
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
                p.version = SaveSystem.CurrentVersion;
                return p;
            }
            // Future steps go here (2 -> 3 ...). Unknown newer versions are read as is.
            var cur = JsonUtility.FromJson<PlayerProfile>(json);
            if (cur != null) cur.version = SaveSystem.CurrentVersion;
            return cur;
        }
    }
}
