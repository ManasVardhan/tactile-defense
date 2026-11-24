using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>The trimmed main menu (Attack and Army only, no Profile, Research or Base) and the Army screen built
    /// around units: the squad slots, six unit cards with lock states and the NEW tag, ADD filling a slot, the
    /// full-squad message, the Army button's dot and the two one-time callouts.</summary>
    public class ArmyScreenTests
    {
        [SetUp]
        public void SetUp()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            BattleTestUtil.MarkTutorialSeen(p);
            SaveSystem.Save();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UITestUtil.Cleanup();
            yield return null;
        }

        static IEnumerator OpenMenu()
        {
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Army != null, 5f);
            yield return UITestUtil.WaitReal(0.3f);
        }

        static IEnumerator OpenArmy()
        {
            MenuRoot.Instance.Open(MenuRoot.Instance.Army);
            yield return UITestUtil.WaitReal(0.4f);
        }

        static IEnumerator CloseArmy()
        {
            MenuRoot.Instance.CloseScreen();
            yield return UITestUtil.WaitReal(0.3f);
        }

        static void ClearLevel(int n)
        {
            MetaGame.Profile.defeatedBases.Add(MetaGame.Db.roster.bases[n - 1].id);
            MetaGame.Commit();
        }

        static List<string> ActiveTexts(Transform root)
        {
            var r = new List<string>();
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(false))
                if (t.gameObject.activeInHierarchy) r.Add(t.text);
            return r;
        }

        [UnityTest]
        public IEnumerator MainMenuHasNoProfileResearchOrBaseButtons()
        {
            yield return OpenMenu();
            var menu = MenuRoot.Instance;
            string[] gone = { "PROFILE", "RESEARCH", "BASE" };
            foreach (var b in Object.FindObjectsByType<UIButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!b.gameObject.activeInHierarchy) continue;
                foreach (var g in gone)
                {
                    Assert.IsFalse(b.name.ToUpperInvariant() == "NAV_" + g, "no " + g + " button on the menu");
                    var l = b.Label();
                    Assert.IsFalse(l != null && l.text.ToUpperInvariant() == g, "no button labeled " + g);
                }
            }
            foreach (var text in ActiveTexts(menu.Root.transform))
                foreach (var g in gone) Assert.AreNotEqual(g, text.Trim().ToUpperInvariant(), "no " + g + " label on the menu");
            Assert.IsTrue(menu.AttackButton != null && menu.AttackButton.gameObject.activeInHierarchy, "Attack is on the menu");
            Assert.IsTrue(menu.ArmyButton != null && menu.ArmyButton.gameObject.activeInHierarchy, "Army is on the menu");
            Assert.IsTrue(menu.Bar.Settings.gameObject.activeInHierarchy, "settings stay");
            Assert.IsFalse(MetaGame.BaseFeaturesEnabled, "base features are switched off");
            Assert.IsNull(menu.LastRaid, "no raid on the way in");
            Assert.IsFalse(menu.RaidCard.IsOpen, "no raid report");
            Assert.IsNull(menu.Current, "no screen open");
        }

        [UnityTest]
        public IEnumerator ArmyScreenHasNoSoldierStatsPanel()
        {
            yield return OpenMenu();
            yield return OpenArmy();
            var army = MenuRoot.Instance.Army;
            foreach (var text in ActiveTexts(army.transform))
            {
                Assert.IsFalse(text.ToUpperInvariant().Contains("SOLDIER STATS"), "no Soldier Stats panel: " + text);
                Assert.IsFalse(text.Contains("Research"), "nothing about research: " + text);
                Assert.IsFalse(text.Contains("Kills to backup") || text.Contains("Shots per sec") || text.Contains("Paint damage"), "no stat rows: " + text);
            }
            Assert.IsNull(army.Body.Find("Stats"), "the stats panel is gone");
            Assert.AreEqual(ArmyScreen.Tab.Units, army.ActiveTab, "Army opens on its units");
            Assert.AreEqual(3, army.Builder.SlotButtons.Count, "the squad row");
            Assert.AreEqual(6, army.Builder.Cards.Count, "the unit catalogue");

            // Heroes sit behind their own tab.
            army.HeroesTabButton.Click();
            yield return UITestUtil.WaitReal(0.2f);
            Assert.AreEqual(ArmyScreen.Tab.Heroes, army.ActiveTab);
            Assert.Greater(army.MercenaryButtons.Count, 0, "the hero roster");
            Assert.IsTrue(army.MercenaryButtons[0].gameObject.activeInHierarchy);
            Assert.IsFalse(army.Builder.Cards[0].gameObject.activeInHierarchy, "the units tab is hidden");
        }

        [UnityTest]
        public IEnumerator ArmyShowsSixUnitCardsWithLockStates()
        {
            yield return OpenMenu();
            yield return OpenArmy();
            var b = MenuRoot.Instance.Army.Builder;
            Assert.AreEqual(6, b.Cards.Count);
            var order = new List<UnitTypeId>();
            foreach (var c in b.Cards) order.Add(c.Type);
            CollectionAssert.AreEqual(Progression.AllTypes, order, "in unlock order");
            foreach (var id in new[] { UnitTypeId.Attacker, UnitTypeId.Tank })
            {
                var c = b.Card(id);
                Assert.IsFalse(c.Locked, id + " is unlocked from the start");
                Assert.IsNotNull(c.AddButton, id + " can be added");
                Assert.IsTrue(c.AddButton.interactable);
                Assert.IsFalse(c.IsNew, "starters are never new");
            }
            var locked = new[] { UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher };
            for (int i = 0; i < locked.Length; i++)
            {
                var c = b.Card(locked[i]);
                Assert.IsTrue(c.Locked, locked[i] + " is locked");
                Assert.IsNull(c.AddButton, "a locked card has no ADD");
                Assert.AreEqual("Clear Level " + (i + 1) + " to unlock", c.LockText);
                Assert.IsFalse(c.IsNew);
            }
            // Bars come from the unit's config.
            Assert.Greater(b.Card(UnitTypeId.Tank).BarFill(UnitStat.Health), b.Card(UnitTypeId.Attacker).BarFill(UnitStat.Health));
            Assert.AreEqual(0f, b.Card(UnitTypeId.Healer).BarFill(UnitStat.Damage), "the Healer does not shoot");
            yield return CloseArmy();

            ClearLevel(1);
            yield return OpenArmy();
            b = MenuRoot.Instance.Army.Builder;
            var healer = b.Card(UnitTypeId.Healer);
            Assert.IsFalse(healer.Locked, "clearing Level 1 unlocks the Healer");
            Assert.IsNotNull(healer.AddButton);
            Assert.IsTrue(healer.IsNew, "and tags it NEW");
            Assert.IsTrue(b.Card(UnitTypeId.Sniper).Locked);
            Assert.AreEqual("Clear Level 2 to unlock", b.Card(UnitTypeId.Sniper).LockText);
            Assert.IsFalse(b.Card(UnitTypeId.Attacker).IsNew);
            yield return CloseArmy();

            // The tag lasts one visit.
            yield return OpenArmy();
            Assert.IsFalse(MenuRoot.Instance.Army.Builder.Card(UnitTypeId.Healer).IsNew, "NEW is gone on the next visit");
        }

        [UnityTest]
        public IEnumerator AddingAUnitFromItsCardFillsASlot()
        {
            ClearLevel(1);
            yield return OpenMenu();
            yield return OpenArmy();
            var b = MenuRoot.Instance.Army.Builder;
            Assert.AreEqual(4, b.SlotButtons.Count);
            b.SlotButtons[1].Click();
            yield return null;
            Assert.AreEqual("Empty", b.SlotLabel(1));
            int goldBefore = MetaGame.Profile.gold;
            b.Card(UnitTypeId.Healer).AddButton.Click();
            yield return null;
            b = MenuRoot.Instance.Army.Builder;
            Assert.AreEqual("Healer", b.SlotLabel(1), "ADD fills the first empty slot");
            Assert.AreEqual(UnitTypeId.Healer, MetaGame.Squad[1]);
            Assert.AreEqual(UnitTypeId.Healer, (UnitTypeId)SaveSystem.Reload().squad[1], "saved");
            Assert.AreEqual(goldBefore, MetaGame.Profile.gold, "units cost nothing");
            Assert.AreEqual(1, b.Card(UnitTypeId.Healer).InSquad, "the card counts it");
            Assert.IsFalse(b.ShowingFullMessage);

            b.SlotButtons[0].Click();
            yield return null;
            MenuRoot.Instance.Army.Builder.Card(UnitTypeId.Tank).AddButton.Click();
            yield return null;
            Assert.AreEqual("Tank", MenuRoot.Instance.Army.Builder.SlotLabel(0), "the first empty slot again");
        }

        [UnityTest]
        public IEnumerator FullSquadShowsMessage()
        {
            yield return OpenMenu();
            yield return OpenArmy();
            var b = MenuRoot.Instance.Army.Builder;
            Assert.AreNotEqual(SquadBuilderPanel.FullMessage, b.Message, "no warning before trying");
            var before = MetaGame.Squad.ToArray();
            b.Card(UnitTypeId.Attacker).AddButton.Click();
            yield return null;
            CollectionAssert.AreEqual(before, MetaGame.Squad, "a full squad is unchanged");
            Assert.AreEqual("Squad is full. Click a slot to remove a unit first.", b.Message);
            Assert.IsTrue(b.ShowingFullMessage);

            // Removing a unit clears the message, and ADD works again.
            b.SlotButtons[2].Click();
            yield return null;
            Assert.AreNotEqual(SquadBuilderPanel.FullMessage, b.Message);
            b.Card(UnitTypeId.Attacker).AddButton.Click();
            yield return null;
            Assert.AreEqual("Attacker", b.SlotLabel(2));
        }

        [UnityTest]
        public IEnumerator ArmyButtonShowsDotForNewUnit()
        {
            yield return OpenMenu();
            var menu = MenuRoot.Instance;
            Assert.IsFalse(menu.ArmyDotShown, "no dot on a fresh save");
            Assert.IsNull(menu.Guide.Showing, "no callout on a fresh save");
            // A visit with nothing new does not use up the callout.
            yield return OpenArmy();
            yield return CloseArmy();
            Assert.IsFalse(MetaGame.IsSeen(Progression.GuideArmy));

            // Back from clearing Level 1: the dot and the "Open Army" callout.
            ClearLevel(1);
            yield return OpenMenu();
            menu = MenuRoot.Instance;
            yield return null;
            Assert.IsTrue(menu.ArmyDotShown, "the dot appears after clearing Level 1");
            Assert.AreEqual(Progression.GuideArmy, menu.Guide.Showing, "the callout points at Army");
            Assert.AreEqual(MenuGuide.ArmyText, menu.Guide.Callout.Text);

            // Inside Army: the Healer callout; the dot goes out.
            yield return OpenArmy();
            yield return null;
            Assert.AreEqual(Progression.GuideAddUnit, menu.Guide.Showing, "inside Army the callout points at the Healer");
            Assert.AreEqual(MenuGuide.AddUnitText, menu.Guide.Callout.Text);
            Assert.IsTrue(MetaGame.IsSeen(Progression.GuideArmy), "the Army callout is used up");
            menu.Army.Builder.SlotButtons[3].Click();
            yield return null;
            menu.Army.Builder.Card(UnitTypeId.Healer).AddButton.Click();
            yield return null;
            yield return null;
            Assert.IsNull(menu.Guide.Showing, "adding the Healer ends the callout");
            yield return CloseArmy();
            yield return null;
            Assert.IsFalse(menu.ArmyDotShown, "the dot disappears after Army has been opened");
            Assert.IsNull(menu.Guide.Showing, "and the home callout does not come back");

            // Persisted: a fresh menu has neither, and the Healer callout does not return either.
            yield return OpenMenu();
            menu = MenuRoot.Instance;
            yield return null;
            Assert.IsFalse(menu.ArmyDotShown);
            Assert.IsNull(menu.Guide.Showing);
            Assert.IsTrue(SaveSystem.Reload().tutorialSeen.Contains(Progression.UnitSeenId(UnitTypeId.Healer)));
            yield return OpenArmy();
            yield return null;
            Assert.IsNull(menu.Guide.Showing, "one-time");
        }
    }
}
