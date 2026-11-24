using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Meta;
using TactileWars.UI;
using UnityEditor;

namespace TactileWars.Tests
{
    /// <summary>The deployment grid is invisible, so nothing the player reads may mention hexes: tutorial steps,
    /// hints, unit intro and builder descriptions, result tips, level and mercenary texts.</summary>
    public class PlayerTextTests
    {
        static IEnumerable<(string where, string text)> PlayerFacingStrings()
        {
            foreach (var id in TutorialSteps.All) yield return ("tutorial " + id, TutorialSteps.Copy(id));
            foreach (var tip in ResultScreen.Tips) yield return ("result tip", tip);
            var roster = AssetDatabase.LoadAssetAtPath<UnitRoster>("Assets/Data/UnitRoster.asset");
            Assert.IsNotNull(roster, "unit roster");
            foreach (var t in roster.types)
            {
                yield return (t.id + " name", t.displayName);
                yield return (t.id + " description (intro card, squad builder)", t.description);
            }
            var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>("Assets/Resources/MetaDatabase.asset");
            Assert.IsNotNull(db, "meta database");
            foreach (var m in db.mercenaries)
            {
                yield return (m.id + " mercenary", m.displayName + " " + m.description + " " + m.ability);
            }
            foreach (var b in db.roster.bases) yield return (b.id + " level card", b.displayName + " " + b.commanderName);
            for (int level = 1; level <= Progression.LevelCount; level++)
                yield return ("level " + level + " reward", Progression.RewardText(level));
        }

        [Test]
        public void PlayerFacingTextHasNoHexWording()
        {
            int n = 0;
            foreach (var (where, text) in PlayerFacingStrings())
            {
                n++;
                if (string.IsNullOrEmpty(text)) continue;
                string low = text.ToLowerInvariant();
                Assert.IsFalse(low.Contains("hex"), where + " mentions hexes: \"" + text + "\"");
            }
            Assert.Greater(n, 30, "every tutorial step, hint, tip and description was checked");
        }

        [Test]
        public void TutorialCopyUsesSpots()
        {
            Assert.AreEqual("Now click a glowing spot to drop them in.", TutorialSteps.Copy(TutorialSteps.Deploy));
            Assert.AreEqual("Click your unit, then click where it should go.", TutorialSteps.Copy(TutorialSteps.Move));
            Assert.AreEqual("You can't go there. Pick an open spot.", TutorialSteps.Copy(TutorialSteps.HintReject));
        }
    }
}
