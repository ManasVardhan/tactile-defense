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
        /// <summary>The squad the player takes into attack battles: one unit type id (Army.UnitTypeId as int) per slot,
        /// -1 for a slot the player emptied. Repaired against the unlocked types and slots by Meta.Progression.</summary>
        public List<int> squad = new List<int>();

        public SettingsData settings = new SettingsData();
        public TutorialFlags tutorial = new TutorialFlags();
        /// <summary>Ids of the Level 1 tutorial steps and hints already shown (see UI.TutorialSteps). Never repeated.</summary>
        public List<string> tutorialSeen = new List<string>();
        public PlayerStats stats = new PlayerStats();
        public BaseLayoutSave baseLayout = new BaseLayoutSave();
        /// <summary>SIMULATED offline raids on the player's base (newest first, capped). Not real players.</summary>
        public List<RaidRecord> raidLog = new List<RaidRecord>();
        /// <summary>UTC ticks of the last time the player was in the menu (raid "time away" check).</summary>
        public long lastSeenUtcTicks;
        public DefenseStats defenseStats = new DefenseStats();

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
            squad ??= new List<int>();
            hiredMercenary ??= "";
            settings ??= new SettingsData();
            tutorial ??= new TutorialFlags();
            tutorialSeen ??= new List<string>();
            stats ??= new PlayerStats();
            baseLayout ??= new BaseLayoutSave();
            baseLayout.placements ??= new List<DefensePlacement>();
            baseLayout.destroyed ??= new List<DefensePlacement>();
            baseLayout.stock ??= new List<StockEntry>();
            raidLog ??= new List<RaidRecord>();
            defenseStats ??= new DefenseStats();
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

    /// <summary>One-off flags outside the Level 1 tutorial. Old saves may still carry the retired stroke coach
    /// flags (drawStroke, tapToMove); JsonUtility skips unknown fields, so they load without error.</summary>
    [Serializable]
    public class TutorialFlags
    {
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

    /// <summary>The player's base layout (base editor). 'placements' are live defenses; 'destroyed' were consumed
    /// in defense battles or simulated raids and wait to be rebought with gold at the same spot; 'stock' holds
    /// bought but unplaced defenses (removing a placed defense returns it to stock).</summary>
    [Serializable]
    public class BaseLayoutSave
    {
        public List<DefensePlacement> placements = new List<DefensePlacement>();
        public List<DefensePlacement> destroyed = new List<DefensePlacement>();
        public List<StockEntry> stock = new List<StockEntry>();

        public int StockOf(string id)
        {
            if (stock == null) return 0;
            for (int i = 0; i < stock.Count; i++) if (stock[i].id == id) return stock[i].count;
            return 0;
        }

        public void SetStock(string id, int count)
        {
            stock ??= new List<StockEntry>();
            for (int i = 0; i < stock.Count; i++)
                if (stock[i].id == id)
                {
                    if (count <= 0) stock.RemoveAt(i); else stock[i].count = count;
                    return;
                }
            if (count > 0) stock.Add(new StockEntry { id = id, count = count });
        }
    }

    [Serializable]
    public class StockEntry
    {
        public string id;
        public int count;
    }

    /// <summary>One simulated raid result ("While you were away"). isSimulated is always true in this build.</summary>
    [Serializable]
    public class RaidRecord
    {
        public long utcTicks;
        public string attackerName = "";
        public TeamColorId attackerTeam = TeamColorId.Teal;
        public bool held;
        public int goldDelta;
        public List<string> consumed = new List<string>();
        public bool isSimulated = true;
        public bool seen;
    }

    [Serializable]
    public class DefenseStats
    {
        public int battlesHeld;
        public int battlesLost;
        public int raidsHeld;
        public int raidsLost;
        public int defensesConsumed;
        public int goldFromDefense;
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
