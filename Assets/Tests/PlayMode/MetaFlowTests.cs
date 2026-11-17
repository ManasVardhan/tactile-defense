using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Squad;
using TactileWars.UI;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Meta game in play mode: research changes the spawned squad, generated bases deploy their layout,
    /// and the full Boot to MainMenu to Battle to MainMenu flow with rewards saved to disk.</summary>
    public class MetaFlowTests
    {
        [SetUp]
        public void SetUp() { UITestUtil.UseTempSave(); }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UITestUtil.Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SquadSizeUpgrade_ChangesSpawnedSoldierCount()
        {
            var db = MetaGame.Db;
            Assert.IsNotNull(db, "MetaDatabase in Resources");
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.gold = 10000;
            var node = db.research.Find("squad_size");
            Assert.IsNotNull(node);
            Assert.AreEqual(PurchaseResult.Ok, MetaGame.BuyResearch(node));
            int expected = db.squadConfig.soldierCount + Mathf.RoundToInt(ResearchTree.ValueAt(node, 1));

            BattleLaunch.Prepare(db.roster.bases[0]);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitReal(0.3f);
            var squad = SquadController.Instance;
            Assert.IsNotNull(squad);
            Debug.Log($"[Test] squad size with research: {squad.Soldiers.Count} (expected {expected})");
            Assert.AreEqual(expected, squad.Soldiers.Count, "squad size research adds soldiers to the battle");
            Assert.AreEqual(expected, squad.StartCount);
        }

        [UnityTest]
        public IEnumerator GeneratedBase_DeploysLayoutAndTeams()
        {
            var db = MetaGame.Db;
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.team = TeamColorId.Orange;
            var b = db.roster.bases[4];
            Assert.IsNotNull(b.layout, "generated base has a layout");
            BattleLaunch.Prepare(b);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitReal(0.3f);
            var squads = Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None);
            Debug.Log($"[Test] base {b.displayName}: {squads.Length} squads (layout {b.layout.squads.Length}), enemy {BattleContext.Config.enemyTeam}");
            Assert.AreEqual(b.layout.squads.Length, squads.Length, "layout squads spawned, handcrafted ones removed");
            Assert.AreEqual(TeamColorId.Orange, BattleContext.Config.playerTeam);
            Assert.AreEqual(b.enemyTeam, BattleContext.Config.enemyTeam);
            Assert.AreEqual(TeamColorId.Orange, SquadController.Instance.Soldiers[0].Color, "squad wears the chosen team");
            var mines = Object.FindObjectsByType<TactileWars.Structures.Mine>(FindObjectsSortMode.None);
            int layoutMines = 0;
            foreach (var d in b.layout.defenses) if (d.defenseId == "mine") layoutMines++;
            Assert.AreEqual(layoutMines, mines.Length, "layout mines spawned");
        }

        [UnityTest]
        public IEnumerator FullFlow_BootMenuBattleVictoryRewardsReturn()
        {
            SceneManager.LoadScene("Boot");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.TeamSelect != null, 5f);
            var menu = MenuRoot.Instance;
            Assert.IsNotNull(menu, "main menu loaded from Boot");
            Assert.IsTrue(menu.TeamSelect.IsOpen, "first run shows team selection");

            menu.TeamSelect.Select(TeamColorId.Green);
            menu.TeamSelect.ConfirmButton.Click();
            yield return null;
            yield return null;
            Assert.AreEqual(TeamColorId.Green, MetaGame.Profile.team);
            Assert.IsTrue(SaveSystem.Reload().teamChosen, "team choice saved");

            menu = MenuRoot.Instance;
            int goldBefore = MetaGame.Profile.gold;
            menu.OpenAttack();
            yield return UITestUtil.WaitReal(0.4f);
            Assert.Greater(menu.Attack.FightButtons.Count, 0);
            menu.Attack.FightButtons[0].Click();
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && BattleHUD.Instance != null && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 10f);
            var bm = BattleManager.Instance;
            Assert.AreEqual(TeamColorId.Green, SquadController.Instance.Soldiers[0].Color, "player squad uses the chosen color");

            yield return UITestUtil.WaitReal(0.5f);
            bm.capturePoints[0].ForceCapture();
            yield return null;
            bm.capturePoints[1].ForceCapture();
            yield return UITestUtil.WaitUntil(() => BattleSession.Last != null, 5f);
            Assert.IsTrue(bm.Over);
            Assert.IsNotNull(BattleSession.Last, "rewards granted");
            Assert.IsTrue(BattleSession.Last.victory);
            var disk = SaveSystem.Reload();
            Debug.Log($"[Test] gold {goldBefore} -> {disk.gold}, xp {disk.xp}, level {disk.level}, victories {disk.stats.victories}");
            Assert.Greater(disk.gold, goldBefore, "gold saved to disk");
            Assert.Greater(disk.xp, 0, "xp saved to disk");
            Assert.AreEqual(1, disk.stats.victories);

            var results = BattleHUD.Instance.Results;
            yield return UITestUtil.WaitUntil(() => results.VictoryShown, 6f);
            Assert.IsTrue(results.VictoryShown, "victory screen shown");
            results.ContinueButton.Click();
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null, 5f);
            Assert.IsNotNull(MenuRoot.Instance, "back in the main menu");
            Assert.IsFalse(MenuRoot.Instance.TeamSelect.IsOpen, "no team selection after the first run");
        }

        [UnityTest]
        public IEnumerator PointerOverHud_DoesNotStartStroke()
        {
            MetaGame.Profile.teamChosen = true;
            MetaGame.Profile.tutorial.drawStroke = true;
            yield return BattleTestUtil.LoadBattle();
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null, 3f);
            yield return null;
            var squad = BattleTestUtil.Squad;
            var hudCam = BattleTestUtil.Cam;
            // Drag starting on the pause tab (top left corner of the render target).
            var p = BattleTestUtil.Pointer;
            Vector2 start = new Vector2(30f, hudCam.pixelHeight - 25f);
            Assert.IsTrue(PointerInput.IsOverUI(start), "pause tab is hit tested as UI");
            var before = squad.Anchor;
            p.Position = start; p.Down = false;
            yield return null;
            p.Down = true;
            yield return null;
            for (int i = 0; i < 20; i++) { p.Position = start + new Vector2(i * 25f, -i * 20f); yield return null; }
            Assert.IsFalse(squad.IsDrawing, "a press that starts on the HUD never draws");
            p.Down = false;
            yield return null;
            yield return null;
            Assert.AreEqual(before, squad.Anchor, "and does not move the squad");
            BattleTestUtil.Cleanup();
        }
    }
}
