using System.Collections;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.UI;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Meta game in play mode: generated bases deploy their layout and teams, and the full Boot to MainMenu
    /// to Battle to MainMenu flow, won by marching army units onto the points, with rewards saved to disk.</summary>
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
            var army = PlayerArmy.Instance;
            var unit = army.Deploy(0, BattleTestUtil.FreeHexNear(army.spawnPoint));
            Assert.IsNotNull(unit, "the army deploys on a generated base");
            Assert.AreEqual(TeamColorId.Orange, unit.Soldiers[0].Color, "units wear the chosen team");
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
            yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && BattleHUD.Instance != null && PlayerArmy.Instance != null, 10f);
            var bm = BattleManager.Instance;
            var army = PlayerArmy.Instance;
            Assert.AreSame(army, bm.Force, "the attack runs on the army");
            yield return UITestUtil.WaitReal(0.5f);

            // Win for real: deploy the army and march one unit onto each point (enemies switched off so the walk is
            // deterministic; the second unit waits on the locked point until the first is taken).
            BattleTestUtil.DisableEnemies();
            yield return null;
            var units = BattleTestUtil.DeployAll();
            Assert.AreEqual(TeamColorId.Green, units[0].Soldiers[0].Color, "player units use the chosen color");
            Assert.IsTrue(army.OrderMove(units[0], BattleTestUtil.PadHex(bm.capturePoints[0])), "ordered onto point 1");
            Assert.IsTrue(army.OrderMove(units[1], BattleTestUtil.PadHex(bm.capturePoints[1])), "ordered onto point 2");
            yield return UITestUtil.WaitUntil(() => BattleSession.Last != null, 60f);
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
    }
}
