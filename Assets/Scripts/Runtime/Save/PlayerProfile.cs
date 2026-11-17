using System;
using System.Collections.Generic;
using TactileWars.Core;

namespace TactileWars.Save
{
    /// <summary>Everything persisted for the local player. Serialized with JsonUtility (fields only).
    /// Bump SaveSystem.CurrentVersion and add a step to SaveMigrations when a field changes meaning.</summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = SaveSystem.CurrentVersion;
        public string playerName = "Commander";
        public bool teamChosen;
        public TeamColorId team = TeamColorId.Red;

        public int level = 1;
        /// <summary>Total experience earned (the level curve converts it to a level).</summary>
        public int xp;
        public int gold;
        public int prisms;
        public int leaguePoints;

        public List<ResearchEntry> research = new List<ResearchEntry>();
        public List<string> ownedMercenaries = new List<string>();
        public string hiredMercenary = "";
        public List<string> defeatedBases = new List<string>();

        public SettingsData settings = new SettingsData();
        public TutorialFlags tutorial = new TutorialFlags();
        public PlayerStats stats = new PlayerStats();
        public BaseLayoutSave baseLayout = new BaseLayoutSave();

        /// <summary>The player's own base (placements of defenses). Owned by the base editor (next phase).</summary>
        public BaseLayoutSave BaseLayout => baseLayout ??= new BaseLayoutSave();

        public int GetResearch(string id)
        {
            if (research == null) return 0;
            for (int i = 0; i < research.Count; i++) if (research[i].id == id) return research[i].level;
            return 0;
        }

        public void SetResearch(string id, int lvl)
        {
            research ??= new List<ResearchEntry>();
            for (int i = 0; i < research.Count; i++)
                if (research[i].id == id) { research[i].level = lvl; return; }
            research.Add(new ResearchEntry { id = id, level = lvl });
        }

        public bool OwnsMercenary(string id) => ownedMercenaries != null && ownedMercenaries.Contains(id);

        /// <summary>Fills any null sub objects (old saves or hand edited files).</summary>
        public void Sanitize()
        {
            research ??= new List<ResearchEntry>();
            ownedMercenaries ??= new List<string>();
            defeatedBases ??= new List<string>();
            hiredMercenary ??= "";
            settings ??= new SettingsData();
            tutorial ??= new TutorialFlags();
            stats ??= new PlayerStats();
            baseLayout ??= new BaseLayoutSave();
            baseLayout.placements ??= new List<DefensePlacement>();
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "Commander";
            if (level < 1) level = 1;
            if (gold < 0) gold = 0;
            if (prisms < 0) prisms = 0;
            if (xp < 0) xp = 0;
            if (leaguePoints < 0) leaguePoints = 0;
        }
    }

    [Serializable]
    public class ResearchEntry
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class SettingsData
    {
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
        /// <summary>0 = low (no post processing), 1 = high.</summary>
        public int quality = 1;
    }

    [Serializable]
    public class TutorialFlags
    {
        public bool drawStroke;
        public bool tapToMove;
        public bool reinforcements;
    }

    [Serializable]
    public class PlayerStats
    {
        public int battles;
        public int victories;
        public int defeats;
        public int kills;
        public int structuresDestroyed;
        public int soldiersLost;
        public int goldEarned;
        public int xpEarned;
        public string bestGrade = "";
        public float fastestVictory;
    }

    /// <summary>The player's base layout: defenses placed on the lane map. Reserved for the base editor.</summary>
    [Serializable]
    public class BaseLayoutSave
    {
        public List<DefensePlacement> placements = new List<DefensePlacement>();
    }

    [Serializable]
    public struct DefensePlacement
    {
        public string defenseId;
        public float x;
        public float z;
        public float rotation;
    }
}
