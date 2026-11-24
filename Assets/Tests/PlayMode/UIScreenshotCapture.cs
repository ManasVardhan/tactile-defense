using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Army;
using TactileWars.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Captures every UI screen at 1280x720 (plus 2340x1080 tall phone captures of the menu and HUD) into
    /// Docs/Screenshots. Uses a temporary save seeded with mid game progress so screens show real content.</summary>
    [Category("UIScreenshots")]
    public class UIScreenshotCapture
    {
        [SetUp]
        public void SetUp() { UITestUtil.UseTempSave(); }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleTestUtil.Cleanup();
            UITestUtil.Cleanup();
            yield return null;
        }

        static void SeedProfile()
        {
            var p = MetaGame.Profile;
            p.playerName = "Captain Dab";
            p.level = 4;
            p.xp = 820;
            p.gold = 1840;
            p.prisms = 23;
            p.leaguePoints = 186;
            p.SetResearch("soldier_health", 1);
            p.SetResearch("fire_rate", 1);
            p.SetResearch("def_turret", 1);
            p.ownedMercenaries.Add("brick");
            p.hiredMercenary = "brick";
            p.defeatedBases.Add("b01");
            p.defeatedBases.Add("b02");
            p.stats.battles = 9; p.stats.victories = 6; p.stats.defeats = 3; p.stats.kills = 412; p.stats.soldiersLost = 96;
            p.stats.goldEarned = 3210; p.stats.bestGrade = "A";
            Save.SaveSystem.Save();
        }

        [UnityTest]
        public IEnumerator CaptureAllScreens()
        {
            SeedProfile();
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.TeamSelect != null, 5f);
            yield return UITestUtil.SetTarget(1280, 720);
            var menu = MenuRoot.Instance;
            yield return UITestUtil.WaitReal(0.6f);
            UITestUtil.Capture("ui_team_select");

            menu.TeamSelect.Select(TeamColorId.Red, false);
            menu.TeamSelect.Confirm();
            yield return UITestUtil.WaitReal(2.6f); // let the welcome toast fade
            menu = MenuRoot.Instance;
            UITestUtil.Capture("ui_main_menu");

            yield return Shot(menu, menu.Attack, "ui_attack");
            yield return Shot(menu, menu.Army, "ui_army");
            menu.Open(menu.Army);
            menu.Army.ShowTab(ArmyScreen.Tab.Heroes);
            yield return UITestUtil.WaitReal(0.65f);
            UITestUtil.Capture("ui_army_heroes");
            menu.CloseScreen();
            yield return UITestUtil.WaitReal(0.3f);
            yield return Shot(menu, menu.Settings, "ui_settings");

            yield return UITestUtil.SetTarget(2340, 1080);
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("ui_main_menu_tall");
            yield return Shot(menu, menu.Army, "ui_army_tall");
            yield return UITestUtil.SetTarget(1280, 720);

            // Battle: second base, live combat with the HUD (the stroke tutorial no longer shows in attack battles).
            var db = MetaGame.Db;
            menu.StartBattle(db.roster.bases[1]);
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null, 5f);
            BattleTestUtil.Pointer = new SimulatedPointerSource();
            PointerInput.Source = BattleTestUtil.Pointer;
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(1.2f);

            // Unit bar before any deploy, then armed with a hex hovered.
            var army = PlayerArmy.Instance;
            var hud0 = BattleHUD.Instance;
            UITestUtil.Capture("hud_unit_bar");
            hud0.UnitBar.ButtonFor(UnitTypeId.Attacker).Click();
            var hoverHex = BattleTestUtil.FreeHexNear(army.spawnPoint + Vector3.forward * 6f);
            BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(army.Grid.Center(hoverHex));
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("hud_unit_armed");
            ArmyInput.Instance.Disarm();

            // Deploy the army and march it toward the nearest enemy squad so the HUD capture shows a firefight.
            var units = BattleTestUtil.DeployAll();
            yield return UITestUtil.WaitReal(0.25f);
            var bm = BattleManager.Instance;
            TactileWars.Units.EnemySquad nearest = null;
            float best = float.MaxValue;
            foreach (var es in Object.FindObjectsByType<TactileWars.Units.EnemySquad>(FindObjectsSortMode.None))
            {
                float d = (es.Center - army.Centroid).sqrMagnitude;
                if (d < best) { best = d; nearest = es; }
            }
            if (nearest != null)
            {
                Vector3 front = nearest.Center + (army.Centroid - nearest.Center).normalized * 6.5f;
                var taken = new System.Collections.Generic.List<HexCoord>();
                foreach (var u in units)
                {
                    var dest = BattleTestUtil.FreeHexNear(front, taken.ToArray());
                    army.OrderMove(u, dest);
                    taken.Add(dest);
                }
            }
            float deadline = Time.time + 14f;
            while (Time.time < deadline && bm.Kills < 8) yield return null;
            bm.Meter.Add(bm.Meter.Required * 0.62f - bm.Meter.Value);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("battle_hud");
            // A selected unit: ring, grid, its badge with pips, a hovered hex next to it.
            PlayerUnit sel = army.Units.Count > 0 ? army.Units[0] : null;
            if (sel != null)
            {
                army.Select(sel);
                var near = BattleTestUtil.FreeHexNear(sel.Centroid + Vector3.back * 3f);
                BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(army.Grid.Center(near));
                yield return UITestUtil.WaitReal(0.3f);
                UITestUtil.Capture("hud_unit_selected");
                army.Select(null);
            }
            yield return UITestUtil.SetTarget(2340, 1080);
            yield return UITestUtil.WaitReal(0.2f);
            UITestUtil.Capture("battle_hud_tall");
            yield return UITestUtil.SetTarget(1280, 720);

            // Full POW badge.
            bm.Meter.Add(1000f);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("ui_pow_ready");

            var hud = BattleHUD.Instance;
            hud.Pause.Open();
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("ui_pause");
            hud.Pause.Resume();
            yield return UITestUtil.WaitReal(0.3f);

            bm.capturePoints[0].ForceCapture();
            yield return null;
            bm.capturePoints[1].ForceCapture();
            yield return UITestUtil.WaitUntil(() => hud.Results.AnimationDone, 12f);
            yield return UITestUtil.WaitReal(0.6f);
            UITestUtil.Capture("ui_victory");

            hud.Results.ReplayButton.Click();
            yield return UITestUtil.WaitReal(1.5f);
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance != hud && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(1.0f);
            foreach (var u in BattleTestUtil.DeployAll()) BattleTestUtil.KillAll(u);
            var hud2 = BattleHUD.Instance;
            yield return UITestUtil.WaitUntil(() => hud2.Results.AnimationDone, 12f);
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("ui_defeat");
            Assert.Pass();
        }

        /// <summary>The Level 1 tutorial on a fresh profile: one capture per guided step, two hints, and the backup
        /// prompt once every unit is lost.</summary>
        [UnityTest]
        public IEnumerator CaptureTutorial()
        {
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.playerName = "Captain Dab";
            Save.SaveSystem.Save();
            BattleLaunch.Prepare(MetaGame.Db.roster.bases[0]);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null && PlayerArmy.Instance != null, 5f);
            BattleTestUtil.Pointer = new SimulatedPointerSource();
            PointerInput.Source = BattleTestUtil.Pointer;
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(2.2f); // let the start banner fade
            var hud = BattleHUD.Instance;
            var tut = hud.Tutorial;
            var army = PlayerArmy.Instance;
            var bm = BattleManager.Instance;
            var overlay = army.GetComponentInChildren<HexOverlay>();
            Assert.IsTrue(tut.Active, "Level 1 tutorial");
            Assert.AreEqual("pick", tut.CurrentStep);
            UITestUtil.Capture("tut_1_pick");

            hud.UnitBar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return UITestUtil.WaitReal(0.6f);
            Assert.AreEqual("deploy", tut.CurrentStep);
            BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(army.Grid.Center(overlay.Suggested[1]));
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("tut_2_deploy");

            yield return BattleTestUtil.Tap(army.Grid.Center(overlay.Suggested[0]));
            yield return UITestUtil.WaitReal(0.8f);
            Assert.AreEqual("unit", tut.CurrentStep);
            UITestUtil.Capture("tut_3_unit");

            yield return UITestUtil.WaitUntil(() => tut.CurrentStep == "move", 8f);
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("tut_4_move");
            var unit = army.Units[0];
            army.Select(unit);
            var spot = overlay.Suggested[0];
            BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(army.Grid.Center(spot));
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("tut_4_move_selected");

            army.OrderMove(unit, spot);
            yield return UITestUtil.WaitReal(0.8f);
            Assert.AreEqual("pan", tut.CurrentStep);
            UITestUtil.Capture("tut_5_pan"); // the unit is still selected, as after a real move order
            army.Select(null);
            BattleCamera.Instance.Pan(new Vector2(0f, -120f));
            yield return UITestUtil.WaitReal(0.3f);

            // Step 6: move up the lane (one suggested hex), with the unit selected so the grid shows blocked hexes.
            yield return UITestUtil.WaitUntil(() => tut.CurrentStep == "advance", 4f);
            Assert.AreEqual("advance", tut.CurrentStep);
            army.Select(unit);
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("tut_6_advance");
            army.Select(null);

            // Step 7: walk the unit toward the nearest enemy squad until one is in range.
            TactileWars.Units.EnemySquad nearest = null;
            float best = float.MaxValue;
            foreach (var es in Object.FindObjectsByType<TactileWars.Units.EnemySquad>(FindObjectsSortMode.None))
            {
                float d = (es.Center - unit.Centroid).sqrMagnitude;
                if (d < best) { best = d; nearest = es; }
            }
            if (nearest != null)
            {
                var dest = BattleTestUtil.FreeHexNear(nearest.Center + (unit.Centroid - nearest.Center).normalized * 10f);
                army.OrderMove(unit, dest);
                yield return UITestUtil.WaitUntil(() => tut.CurrentStep == "tank", 25f);
            }
            Assert.AreEqual("tank", tut.CurrentStep, "enemies in range start step 6");
            BattleCamera.Instance.SnapTo(unit.Centroid + Vector3.forward * 2f);
            yield return UITestUtil.WaitUntil(() => tut.CurrentHint == null, 8f);
            hud.UnitBar.ButtonFor(UnitTypeId.Tank).Click(); // armed: the grid and the two glowing Tank hexes
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("tut_7_tank");
            Assert.Greater(overlay.Suggested.Count, 0, "hexes suggested for the Tank");
            army.Deploy(ArmyInput.Instance.ArmedEntry, overlay.Suggested[0]);
            yield return UITestUtil.WaitReal(0.3f);

            // Step 8: clear the first point and look at it, with a unit selected so the pad hexes are tinted.
            var cp = bm.capturePoints[0];
            float clear = bm.battleConfig.captureRadius + 3f;
            foreach (var h in new System.Collections.Generic.List<TactileWars.Combat.Health>(TactileWars.Combat.UnitRegistry.Of(Team.Enemy)))
                if (h != null && h.IsAlive && h.kind != TactileWars.Combat.TargetKind.Structure && h.kind != TactileWars.Combat.TargetKind.Mine
                    && (h.transform.position - cp.transform.position).magnitude < clear) h.Kill(TeamColorId.Red);
            yield return UITestUtil.WaitUntil(() => tut.CurrentStep == "capture", 6f);
            Assert.AreEqual("capture", tut.CurrentStep);
            BattleCamera.Instance.SnapTo(cp.transform.position + Vector3.back * 2f);
            yield return UITestUtil.WaitUntil(() => tut.CurrentHint == null, 8f);
            army.Select(unit);
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("tut_8_capture");
            army.Select(null);

            // Hints: a refused order, then the backup meter filling.
            army.Grid.TryWorldToHex(cp.transform.position + new Vector3(4f, 0f, -3f), out HexCoord refused);
            army.NotifyRejected(refused);
            yield return UITestUtil.WaitReal(0.5f);
            if (tut.CurrentHint != null) UITestUtil.Capture("tut_hint_" + tut.CurrentHint.Replace("hint_", ""));
            yield return UITestUtil.WaitUntil(() => tut.CurrentHint == null, 8f);
            bm.Meter.Add(1000f);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            yield return UITestUtil.WaitUntil(() => tut.CurrentHint == "hint_backup", 4f);
            yield return UITestUtil.WaitReal(0.5f);
            UITestUtil.Capture("tut_hint_backup");

            // Every unit lost with a full meter: the battle waits and the backup prompt says why.
            foreach (var u in BattleTestUtil.DeployAll()) BattleTestUtil.KillAll(u);
            foreach (var u in new System.Collections.Generic.List<PlayerUnit>(army.Units)) BattleTestUtil.KillAll(u);
            yield return UITestUtil.WaitReal(1.2f);
            Assert.IsTrue(hud.Backup.Visible, "backup prompt");
            UITestUtil.Capture("hud_backup_prompt");
            Assert.Pass();
        }

        /// <summary>One unit of each of the six types side by side at the normal battle camera, none selected: the
        /// always-on glyph badges, the accent helmets and the type sizes (units_all_six). Also the Attacker and Tank
        /// pair alone (units_attacker_vs_tank).</summary>
        [UnityTest]
        public IEnumerator CaptureUnitTypes()
        {
            var p = MetaGame.Profile;
            p.teamChosen = true;
            BattleTestUtil.MarkTutorialSeen(p);
            Save.SaveSystem.Save();
            BattleLaunch.Prepare(MetaGame.Db.roster.bases[0]);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null && PlayerArmy.Instance != null, 5f);
            BattleTestUtil.Pointer = new SimulatedPointerSource();
            PointerInput.Source = BattleTestUtil.Pointer;
            yield return UITestUtil.SetTarget(1280, 720);
            var army = PlayerArmy.Instance;
            var types = new[] { UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher };
            army.SetSquad(types);
            yield return UITestUtil.WaitReal(2.2f);
            var g = army.Grid;
            Vector3 at = BattleCamera.Instance.Focus;
            var ha = BattleTestUtil.FreeHexNear(at + Vector3.left * 1.6f);
            var ht = BattleTestUtil.FreeHexNear(at + Vector3.right * 1.6f, ha);
            Assert.IsNotNull(army.Deploy(0, ha));
            Assert.IsNotNull(army.Deploy(1, ht));
            yield return UITestUtil.WaitReal(1.0f);
            UITestUtil.Capture("units_attacker_vs_tank");

            // The other four in the same row, two hexes apart.
            var used = new System.Collections.Generic.List<HexCoord> { ha, ht };
            float[] xs = { -7f, -3.8f, 4.6f, 8f };
            for (int i = 2; i < types.Length; i++)
            {
                var h = BattleTestUtil.FreeHexNear(at + Vector3.right * xs[i - 2], used.ToArray());
                used.Add(h);
                Assert.IsNotNull(army.Deploy(i, h), "deploy " + types[i]);
            }
            yield return UITestUtil.WaitReal(1.0f);
            UITestUtil.Capture("units_all_six");
            Assert.Pass();
        }

        /// <summary>The level ladder on a fresh save, the Level 1 win with its unlock line, the ladder and squad
        /// builder after it, and the battle unit bar with a full seven unit squad of all six types plus a mercenary.</summary>
        [UnityTest]
        public IEnumerator CaptureLadderAndSquad()
        {
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.playerName = "Captain Dab";
            BattleTestUtil.MarkTutorialSeen(p);
            Save.SaveSystem.Save();
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Attack != null, 5f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(0.6f);
            var menu = MenuRoot.Instance;
            yield return Shot(menu, menu.Attack, "ui_ladder_fresh");

            // Win Level 1: the victory card shows the unlock line.
            menu.StartBattle(MetaGame.Db.roster.bases[0]);
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(0.5f);
            BattleTestUtil.DisableEnemies();
            BattleTestUtil.DeployAll();
            var bm = BattleManager.Instance;
            bm.capturePoints[0].ForceCapture();
            yield return null;
            bm.capturePoints[1].ForceCapture();
            var hud = BattleHUD.Instance;
            yield return UITestUtil.WaitUntil(() => hud.Results.AnimationDone, 12f);
            yield return UITestUtil.WaitReal(0.8f);
            Assert.IsTrue(hud.Results.UnlockShown, "unlock line on the first clear");
            UITestUtil.Capture("ui_victory_unlock");
            hud.Results.ContinueButton.Click();
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Attack != null, 5f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(2.6f); // let the toasts fade
            menu = MenuRoot.Instance;
            yield return Shot(menu, menu.Attack, "ui_ladder_cleared1");
            menu.Open(menu.Army);
            yield return UITestUtil.WaitReal(0.8f);
            UITestUtil.Capture("ui_squad_builder");
            menu.CloseScreen();
            yield return UITestUtil.WaitReal(0.3f);

            // Every level cleared: seven slots, every type, and a hired mercenary.
            p = MetaGame.Profile;
            for (int i = 1; i < Progression.LevelCount; i++) p.defeatedBases.Add(MetaGame.Db.roster.bases[i].id);
            p.ownedMercenaries.Add("brick");
            p.hiredMercenary = "brick";
            p.gold = 5000;
            Save.SaveSystem.Save();
            var full = new[] { UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher, UnitTypeId.Tank };
            for (int i = 0; i < full.Length; i++) Assert.IsTrue(MetaGame.SetSquadSlot(i, full[i]), "slot " + i);
            yield return Shot(menu, menu.Army, "ui_squad_builder_full");
            yield return Shot(menu, menu.Attack, "ui_ladder_five");

            menu.StartBattle(MetaGame.Db.roster.bases[1]);
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.UnitBar != null && PlayerArmy.Instance != null, 8f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(2.4f); // let the start banner fade
            var bar = BattleHUD.Instance.UnitBar;
            Assert.AreEqual(8, PlayerArmy.Instance.Squad.Count, "seven units and the mercenary");
            Assert.AreEqual(7, bar.Buttons.Count, "six types and the mercenary");
            UITestUtil.Capture("hud_unit_bar_full");
            bar.ButtonFor(UnitTypeId.Healer).Click();
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("hud_unit_bar_full_armed");
            Assert.Pass();
        }

        /// <summary>The trimmed main menu and the Army screen: fresh save, after the Level 1 clear (Healer NEW, the
        /// Army dot and both callouts), and a full squad with its message.</summary>
        [UnityTest]
        public IEnumerator CaptureMenuAndArmy()
        {
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.playerName = "Captain Dab";
            BattleTestUtil.MarkTutorialSeen(p);
            Save.SaveSystem.Save();
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Army != null, 5f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(0.8f);
            var menu = MenuRoot.Instance;
            UITestUtil.Capture("ui_menu_trimmed");
            yield return Shot(menu, menu.Army, "ui_army_fresh");

            // Back from clearing Level 1.
            MetaGame.Profile.defeatedBases.Add(MetaGame.Db.roster.bases[0].id);
            MetaGame.Profile.gold += 180;
            Save.SaveSystem.Save();
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.Army != null, 5f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(0.8f);
            menu = MenuRoot.Instance;
            Assert.IsTrue(menu.ArmyDotShown, "dot on Army");
            UITestUtil.Capture("ui_menu_new_unit");
            menu.Open(menu.Army);
            yield return UITestUtil.WaitReal(0.8f);
            Assert.IsTrue(menu.Army.Builder.Card(UnitTypeId.Healer).IsNew);
            UITestUtil.Capture("ui_army_new_unit");
            menu.CloseScreen();
            yield return UITestUtil.WaitReal(0.3f);

            // A later visit: the Healer in the squad, the squad full, and ADD refused with the message.
            menu.Open(menu.Army);
            yield return UITestUtil.WaitReal(0.5f);
            var b = menu.Army.Builder;
            b.SlotButtons[1].Click();
            yield return null;
            b.Card(UnitTypeId.Healer).AddButton.Click();
            yield return null;
            b.Card(UnitTypeId.Tank).AddButton.Click();
            yield return UITestUtil.WaitReal(0.5f);
            Assert.IsTrue(b.ShowingFullMessage);
            UITestUtil.Capture("ui_army_full");
            menu.CloseScreen();
            Assert.Pass();
        }

        static IEnumerator Shot(MenuRoot menu, MenuScreen screen, string name)
        {
            menu.Open(screen);
            yield return UITestUtil.WaitReal(0.65f);
            UITestUtil.Capture(name);
            menu.CloseScreen();
            yield return UITestUtil.WaitReal(0.3f);
        }
    }
}
