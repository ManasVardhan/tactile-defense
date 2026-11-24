using System.Collections.Generic;
using TactileWars.Army;
using Ladder = TactileWars.Meta.Progression;

namespace TactileWars.Meta
{
    /// <summary>Army screen side of the meta game: which unlocked units are still NEW, and the two one-time menu
    /// callouts. Both are kept in the profile's seen-list (the one the battle tutorial uses), so no save change.
    /// Also the switch that keeps the base features (raids, base editor, defense) out of normal play.</summary>
    public static partial class MetaGame
    {
        /// <summary>Off: no simulated raids are generated, and the menu offers no Base, Research or Profile screen.
        /// Tests that exercise the base code turn it on (or call RaidSimulator directly).</summary>
        public static bool BaseFeaturesEnabled = false;

        public static bool IsSeen(string id) => Profile.tutorialSeen != null && Profile.tutorialSeen.Contains(id);

        /// <summary>Adds an id to the seen-list and saves. False when it was already there.</summary>
        public static bool MarkGuideSeen(string id)
        {
            var p = Profile;
            p.tutorialSeen ??= new List<string>();
            if (p.tutorialSeen.Contains(id)) return false;
            p.tutorialSeen.Add(id);
            Commit();
            return true;
        }

        /// <summary>Unlocked types the Army screen has not shown yet (they carry the NEW tag).</summary>
        public static List<UnitTypeId> NewUnits => Ladder.NewUnits(LevelsCleared, Profile.tutorialSeen);

        public static bool HasNewUnits => NewUnits.Count > 0;

        /// <summary>The Army screen was opened: every unlocked type counts as seen from now on. Saves only when
        /// something changed.</summary>
        public static void MarkUnitsSeen()
        {
            var p = Profile;
            p.tutorialSeen ??= new List<string>();
            bool changed = false;
            foreach (var id in NewUnits) { p.tutorialSeen.Add(Ladder.UnitSeenId(id)); changed = true; }
            if (changed) Commit();
        }

        /// <summary>"You unlocked a new unit. Open Army.": after a clear unlocks a type the player has not seen yet,
        /// until the Army screen is opened once.</summary>
        public static bool ShowArmyGuide => LevelsCleared >= 1 && !IsSeen(Ladder.GuideArmy) && HasNewUnits;

        /// <summary>"Add the Healer to your squad.": inside Army, once the Healer is unlocked and not yet in the
        /// squad, until the player adds it or leaves the screen.</summary>
        public static bool ShowAddUnitGuide
            => Ladder.IsUnlocked(UnitTypeId.Healer, LevelsCleared) && !IsSeen(Ladder.GuideAddUnit) && !Squad.Contains(UnitTypeId.Healer);
    }
}
