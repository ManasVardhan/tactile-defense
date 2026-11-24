using System.Collections;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>The level ladder and the squad builder in the real menu and battle: only open levels can be fought,
    /// winning Level 1 opens Level 2 and unlocks the Healer and a fourth slot, the squad builder fills and clears
    /// slots, the chosen squad and a hired mercenary reach the battle's unit bar.</summary>
    public class LadderFlowTests
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
            BattleTestUtil.Cleanup();
            UITestUtil.Cleanup();
            yield return null;
        }

        static BaseDefinition Level(int n) => MetaGame.Db.roster.bases[n - 1];

        static IEnumerator OpenMenu()
        {
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Attack != null, 5f);
            yield return UITestUtil.WaitReal(0.3f);
        }

        static IEnumerator OpenScreen(MenuScreen s)
        {
            MenuRoot.Instance.Open(s);
            yield return UITestUtil.WaitReal(0.4f);
        }

        static void Defeat(params int[] levels)
        {
            foreach (int l in levels) MetaGame.Profile.defeatedBases.Add(Level(l).id);
            SaveSystem.Save();
        }

        [UnityTest]
        public IEnumerator FreshProfileOnlyLevelOneOpen()
        {
            yield return OpenMenu();
            var attack = MenuRoot.Instance.Attack;
            yield return OpenScreen(attack);
            Assert.AreEqual(Progression.LevelCount, attack.EntryCount, "five levels on the ladder");
            int fightable = 0;
            for (int i = 0; i < attack.EntryCount; i++)
            {
                var b = attack.FightButtons[i];
                if (b != null && b.interactable && b.gameObject.activeInHierarchy) fightable++;
                if (i > 0)
                {
                    Assert.AreEqual(LadderState.Locked, attack.StateOf(i), "Level " + (i + 1) + " is locked");
                    Assert.IsNull(b, "a locked level has no Fight button");
                    Assert.IsFalse(MetaGame.IsBaseUnlocked(Level(i + 1)));
                }
            }
            Assert.AreEqual(1, fightable, "exactly one Fight button");
            Assert.AreEqual(LadderState.Open, attack.StateOf(0));
            Assert.AreEqual("Unlocks Healer, +1 squad slot", attack.RewardLabel(0), "Level 1's card names its reward");
            Assert.AreEqual("", attack.RewardLabel(4), "the final level, Level 5, grants nothing new");
            Assert.AreEqual("Level 2", attack.LevelLabel(1));
            Assert.AreEqual(Level(2).displayName, attack.NameLabel(1));
        }

        [UnityTest]
        public IEnumerator WinningLevelOneUnlocksHealerSlotAndLevelTwo()
        {
            // Win Level 1 through the real launch path and battle end.
            BattleLaunch.Prepare(Level(1));
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.WaitReal(0.3f);
            BattleTestUtil.DisableEnemies();
            BattleTestUtil.DeployAll();
            var bm = BattleManager.Instance;
            bm.capturePoints[0].ForceCapture();
            yield return null;
            bm.capturePoints[1].ForceCapture();
            yield return UITestUtil.WaitUntil(() => BattleSession.Last != null, 5f);
            Assert.IsNotNull(BattleSession.Last, "rewards applied");
            Assert.AreEqual("Unlocked: Healer, +1 squad slot", BattleSession.Last.unlockText);
            var results = BattleHUD.Instance.Results;
            yield return UITestUtil.WaitUntil(() => results.AnimationDone, 12f);
            Assert.IsTrue(results.UnlockShown, "the result screen shows the unlock line");
            Assert.AreEqual("Unlocked: Healer, +1 squad slot", results.UnlockLine);

            results.ContinueButton.Click();
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Attack != null, 5f);
            yield return UITestUtil.WaitReal(0.3f);
            var menu = MenuRoot.Instance;
            yield return OpenScreen(menu.Attack);
            Assert.AreEqual(LadderState.Cleared, menu.Attack.StateOf(0));
            Assert.AreEqual(LadderState.Open, menu.Attack.StateOf(1), "Level 2 open");
            Assert.IsNotNull(menu.Attack.FightButtons[1]);
            Assert.IsTrue(menu.Attack.FightButtons[1].interactable);
            Assert.AreEqual(LadderState.Locked, menu.Attack.StateOf(2));

            yield return OpenScreen(menu.Army);
            var builder = menu.Army.Builder;
            Assert.AreEqual(4, builder.SlotButtons.Count, "four squad slots");
            Assert.AreEqual("Attacker", builder.SlotLabel(3), "the new slot arrives filled with an Attacker");
            var healer = builder.Card(UnitTypeId.Healer);
            Assert.IsFalse(healer.Locked, "the Healer is unlocked");
            Assert.IsTrue(healer.AddButton.interactable);
            builder.SlotButtons[3].Click();
            yield return null;
            builder.Card(UnitTypeId.Healer).AddButton.Click();
            yield return null;
            Assert.AreEqual("Healer", builder.SlotLabel(3), "the Healer joins the squad");
            Assert.AreEqual(UnitTypeId.Healer, MetaGame.Squad[3]);
            CollectionAssert.Contains(LoadoutBuilder.Build(MetaGame.Profile, MetaGame.Db, Level(2)).squad, UnitTypeId.Healer, "and the Level 2 loadout");
        }

        [UnityTest]
        public IEnumerator SquadBuilderFillsAndClearsSlots()
        {
            Defeat(1, 2);
            yield return OpenMenu();
            var menu = MenuRoot.Instance;
            yield return OpenScreen(menu.Army);
            var builder = menu.Army.Builder;
            Assert.AreEqual(5, builder.SlotButtons.Count, "five slots after two levels");
            Assert.AreEqual(6, builder.Cards.Count, "all six types in the catalogue");
            Assert.IsTrue(builder.Card(UnitTypeId.Vanguard).Locked, "the Vanguard is locked");
            Assert.IsNull(builder.Card(UnitTypeId.Vanguard).AddButton, "a locked card has no ADD");
            Assert.AreEqual("Clear Level 3 to unlock", builder.Card(UnitTypeId.Vanguard).LockText);
            Assert.AreEqual("Clear Level 4 to unlock", builder.Card(UnitTypeId.Demolisher).LockText);
            Assert.AreEqual("", builder.Card(UnitTypeId.Sniper).LockText, "the Sniper is unlocked");

            // A full squad: adding changes nothing.
            var before = MetaGame.Squad.ToArray();
            builder.Card(UnitTypeId.Sniper).AddButton.Click();
            yield return null;
            CollectionAssert.AreEqual(before, MetaGame.Squad, "no free slot, nothing happens");
            Assert.AreEqual("Sniper", builder.Card(UnitTypeId.Sniper).NameText, "the card names its unit");
            Assert.IsTrue(builder.Card(UnitTypeId.Sniper).Description.Contains("range"), "and shows its description: " + builder.Card(UnitTypeId.Sniper).Description);

            // Clear two slots, fill the first empty one.
            builder.SlotButtons[1].Click();
            yield return null;
            builder.SlotButtons[4].Click();
            yield return null;
            Assert.AreEqual("Empty", builder.SlotLabel(1));
            Assert.AreEqual("Empty", builder.SlotLabel(4));
            builder.Card(UnitTypeId.Sniper).AddButton.Click();
            yield return null;
            Assert.AreEqual("Sniper", builder.SlotLabel(1), "the first empty slot is filled");
            Assert.AreEqual("Empty", builder.SlotLabel(4));

            // A locked type cannot be added, even when asked directly.
            builder.OnAddClicked(UnitTypeId.Demolisher);
            yield return null;
            Assert.AreEqual("Empty", builder.SlotLabel(4), "a locked type is not added");

            // Clear down to one unit: the last one stays.
            for (int i = 0; i < 5; i++) { builder.SlotButtons[i].Click(); yield return null; }
            int filled = 0;
            for (int i = 0; i < 5; i++) if (builder.SlotLabel(i) != "Empty") filled++;
            Assert.AreEqual(1, filled, "the last filled slot cannot be cleared");
            Assert.AreEqual(1, LoadoutBuilder.Build(MetaGame.Profile, MetaGame.Db, Level(1)).squad.Count);
            Assert.AreEqual(5, SaveSystem.Reload().squad.Count, "saved with empty slots");
        }

        [UnityTest]
        public IEnumerator ChosenSquadReachesTheBattle()
        {
            Assert.IsTrue(MetaGame.SetSquadSlot(0, UnitTypeId.Tank));
            Assert.IsTrue(MetaGame.SetSquadSlot(1, UnitTypeId.Tank));
            Assert.IsTrue(MetaGame.SetSquadSlot(2, UnitTypeId.Attacker));
            yield return OpenMenu();
            MenuRoot.Instance.StartBattle(Level(1));
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.UnitBar != null && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.WaitReal(0.3f);
            var army = PlayerArmy.Instance;
            Assert.AreEqual(3, army.Squad.Count);
            var bar = BattleHUD.Instance.UnitBar;
            Assert.AreEqual(2, bar.Buttons.Count, "two types on the bar");
            Assert.AreEqual("x2", bar.CountText(UnitTypeId.Tank), "Tank x2");
            Assert.AreEqual("x1", bar.CountText(UnitTypeId.Attacker), "Attacker x1");
            Assert.AreEqual(UnitTypeId.Tank, army.Squad[0].type, "in the chosen order");
        }

        [UnityTest]
        public IEnumerator HiredMercenaryGetsItsOwnButton()
        {
            var p = MetaGame.Profile;
            var merc = MetaGame.Db.FindMercenary("brick");
            Assert.IsNotNull(merc);
            p.ownedMercenaries.Add(merc.id);
            p.hiredMercenary = merc.id;
            p.gold = 5000;
            SaveSystem.Save();
            BattleLaunch.Prepare(Level(1));
            Assert.IsFalse(BattleLaunch.LastMercenaryUnpaid);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.UnitBar != null && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.WaitReal(0.3f);
            BattleTestUtil.DisableEnemies();
            var army = PlayerArmy.Instance;
            Assert.AreEqual(4, army.Squad.Count, "three units plus the mercenary");
            var entry = army.Squad[3];
            Assert.IsTrue(entry.IsMercenary, "the mercenary comes after the squad");
            var type = army.TypeConfig(entry.type);
            Assert.IsNotNull(type);
            Assert.AreEqual(1, type.soldierCount, "a single soldier");
            Assert.AreEqual("M", type.glyph);
            var bar = BattleHUD.Instance.UnitBar;
            Assert.AreEqual(3, bar.Buttons.Count, "Attacker, Tank and the mercenary");
            Assert.AreEqual("M", bar.GlyphText(entry.type));
            Assert.AreEqual("x1", bar.CountText(entry.type));

            var u = army.Deploy(3, BattleTestUtil.FreeHexNear(army.spawnPoint));
            Assert.IsNotNull(u, "the mercenary deploys");
            Assert.AreEqual(1, u.StartCount);
            float baseHp = army.TypeConfig(UnitTypeId.Attacker) != null ? army.soldierConfig.maxHealth : 0f;
            Assert.AreEqual(baseHp * merc.healthMult, u.Soldiers[0].Health.max, 1e-3f, "health from the mercenary loadout");
            Assert.AreEqual("x0", bar.CountText(entry.type));
            Assert.IsTrue(army.CanStillFight);

            // Lost, it is never restored by backup.
            BattleTestUtil.KillAll(u);
            yield return null;
            Assert.AreEqual(EntryState.Destroyed, army.Squad[3].state);
            Assert.IsFalse(army.RestoreLastDestroyed(), "backup never brings the mercenary back");
        }
    }
}
