using System.Collections.Generic;
using System.Text;
using TactileWars.Army;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>
    /// The level ladder rules, pure and static. Level N is the N-th base of the meta database's roster (b01 to b05).
    /// A level is cleared when its base id is in PlayerProfile.defeatedBases; only consecutive clears from Level 1
    /// count (ids of retired levels in an old save are ignored). Clearing Level N opens Level N + 1, and clearing
    /// Levels 1 to 4 each gives one more squad slot (up to 7) and unlocks a unit type. Attacker and Tank are the
    /// starters. Level 5 is the final level.
    /// </summary>
    public static class Progression
    {
        public const int LevelCount = 5;
        public const int StartSlots = 3;
        public const int MaxSlots = 7;
        /// <summary>Stored value of a squad slot the player emptied.</summary>
        public const int EmptySlot = -1;

        static readonly UnitTypeId[] Starter = { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank };

        /// <summary>The type each of Levels 1 to 4 unlocks (index = level - 1).</summary>
        static readonly UnitTypeId[] Unlocks = { UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher };

        /// <summary>Every fieldable type in palette order.</summary>
        public static readonly UnitTypeId[] AllTypes =
        {
            UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher,
        };

        /// <summary>Consecutive levels cleared starting at Level 1.</summary>
        public static int LevelsCleared(PlayerProfile p, MetaDatabase db)
        {
            if (p == null || p.defeatedBases == null || db == null || db.roster == null) return 0;
            var bases = db.roster.bases;
            int n = 0;
            for (int i = 0; i < bases.Length && i < LevelCount; i++)
            {
                if (bases[i] == null || !p.defeatedBases.Contains(bases[i].id)) break;
                n++;
            }
            return n;
        }

        /// <summary>The level number of a roster base (1 based), 0 when the base is not on the ladder.</summary>
        public static int LevelOf(BaseDefinition b, MetaDatabase db)
        {
            if (b == null || db == null || db.roster == null) return 0;
            int i = db.roster.IndexOf(b);
            if (i < 0)
                for (int k = 0; k < db.roster.bases.Length; k++)
                    if (db.roster.bases[k] != null && db.roster.bases[k].id == b.id) { i = k; break; }
            return i >= 0 && i < LevelCount ? i + 1 : 0;
        }

        public static bool IsLevelOpen(int level, int cleared) => level <= cleared + 1;

        public static int SquadSlots(int cleared) => Mathf.Min(MaxSlots, StartSlots + Mathf.Min(cleared, MaxSlots - StartSlots));

        /// <summary>The level whose clear unlocks the type (0 for the starters), -1 for a type outside the schedule.</summary>
        public static int UnlockLevel(UnitTypeId id)
        {
            if (id == UnitTypeId.Attacker || id == UnitTypeId.Tank) return 0;
            for (int i = 0; i < Unlocks.Length; i++) if (Unlocks[i] == id) return i + 1;
            return -1;
        }

        public static bool IsUnlocked(UnitTypeId id, int cleared)
        {
            int l = UnlockLevel(id);
            return l >= 0 && cleared >= l;
        }

        /// <summary>The type clearing this level unlocks, if any.</summary>
        public static bool UnlockOf(int level, out UnitTypeId id)
        {
            id = UnitTypeId.Attacker;
            if (level < 1 || level > Unlocks.Length) return false;
            id = Unlocks[level - 1];
            return true;
        }

        /// <summary>True when clearing this level adds a squad slot.</summary>
        public static bool GivesSlot(int level) => level >= 1 && SquadSlots(level) > SquadSlots(level - 1);

        /// <summary>What clearing the level grants, for its ladder card: "Unlocks Healer, +1 squad slot", "+1 squad
        /// slot", or empty when it grants neither.</summary>
        public static string RewardText(int level)
        {
            var sb = new StringBuilder();
            if (UnlockOf(level, out var id)) sb.Append("Unlocks ").Append(id);
            if (GivesSlot(level))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("+1 squad slot");
            }
            return sb.ToString();
        }

        /// <summary>The result screen line when the clears went from 'before' to 'after' ("Unlocked: Healer, +1 squad
        /// slot"); levels that grant nothing name the level they open. Empty when nothing new was cleared.</summary>
        public static string UnlockText(int before, int after)
        {
            if (after <= before) return "";
            var parts = new List<string>();
            int slots = 0;
            for (int l = before + 1; l <= after; l++)
            {
                if (UnlockOf(l, out var id)) parts.Add(id.ToString());
                if (GivesSlot(l)) slots++;
            }
            if (slots > 0) parts.Add("+" + slots + (slots == 1 ? " squad slot" : " squad slots"));
            if (parts.Count == 0)
                return after < LevelCount ? "Unlocked: Level " + (after + 1) : "You cleared every level!";
            return "Unlocked: " + string.Join(", ", parts);
        }

        /// <summary>A squad the player may field: locked types dropped (order kept), cut to the slot count, empty
        /// slots filled with Attackers. Null or empty gives Attacker, Attacker, Tank (padded the same way). The
        /// result always has SquadSlots(cleared) entries.</summary>
        public static List<UnitTypeId> RepairSquad(IList<UnitTypeId> squad, int cleared)
        {
            int slots = SquadSlots(cleared);
            var r = new List<UnitTypeId>(slots);
            IList<UnitTypeId> src = squad == null || squad.Count == 0 ? Starter : squad;
            for (int i = 0; i < src.Count && r.Count < slots; i++)
                if (IsUnlocked(src[i], cleared)) r.Add(src[i]);
            while (r.Count < slots) r.Add(UnitTypeId.Attacker);
            return r;
        }

        /// <summary>The stored squad (unit type ids as ints, EmptySlot for a slot the player emptied) repaired like
        /// RepairSquad, keeping empty slots where they are: unknown and locked types are dropped, the list is cut to
        /// the slot count and new slots arrive as Attackers. A list with no unit left becomes RepairSquad(null).</summary>
        public static List<int> RepairStored(IList<int> stored, int cleared)
        {
            int slots = SquadSlots(cleared);
            var r = new List<int>(slots);
            bool any = false;
            if (stored != null)
                for (int i = 0; i < stored.Count && r.Count < slots; i++)
                {
                    int v = stored[i];
                    if (v == EmptySlot) { r.Add(EmptySlot); continue; }
                    if (!IsUnlocked((UnitTypeId)v, cleared)) continue;
                    r.Add(v);
                    any = true;
                }
            if (!any)
            {
                r.Clear();
                foreach (var t in RepairSquad(null, cleared)) r.Add((int)t);
                return r;
            }
            while (r.Count < slots) r.Add((int)UnitTypeId.Attacker);
            return r;
        }

        /// <summary>The filled slots of a stored squad in order (what a battle fields).</summary>
        public static List<UnitTypeId> Fielded(IList<int> repaired)
        {
            var r = new List<UnitTypeId>(repaired.Count);
            for (int i = 0; i < repaired.Count; i++) if (repaired[i] != EmptySlot) r.Add((UnitTypeId)repaired[i]);
            return r;
        }

        // ------------------------------------------------------------ new units and menu guidance

        /// <summary>One-time menu callouts, kept in the same seen-list as the battle tutorial
        /// (PlayerProfile.tutorialSeen): "Open Army" after the first unlock, and "Add the Healer" inside Army.</summary>
        public const string GuideArmy = "menu_army", GuideAddUnit = "menu_add_unit";

        /// <summary>Seen-list id recorded when the Army screen has shown an unlocked type ("army_seen_healer").</summary>
        public static string UnitSeenId(UnitTypeId id) => "army_seen_" + id.ToString().ToLowerInvariant();

        /// <summary>Unlocked types that a level clear gave (not the starters) and the Army screen has not shown yet:
        /// they carry the NEW tag and light the dot on the menu's Army button.</summary>
        public static List<UnitTypeId> NewUnits(int cleared, ICollection<string> seen)
        {
            var r = new List<UnitTypeId>();
            foreach (var id in AllTypes)
                if (UnlockLevel(id) > 0 && IsUnlocked(id, cleared) && (seen == null || !seen.Contains(UnitSeenId(id)))) r.Add(id);
            return r;
        }

        /// <summary>The ladder card's lock line for a type: "Clear Level 3 to unlock" (empty for the starters).</summary>
        public static string LockText(UnitTypeId id)
        {
            int l = UnlockLevel(id);
            return l > 0 ? "Clear Level " + l + " to unlock" : "";
        }
    }
}
