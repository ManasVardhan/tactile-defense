using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Squad;
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
            p.SetResearch("squad_size", 2);
            p.SetResearch("soldier_health", 1);
            p.SetResearch("fire_rate", 1);
            p.SetResearch("reinforcement_size", 1);
            p.SetResearch("def_turret", 1);
            p.ownedMercenaries.Add("brick");
            p.hiredMercenary = "brick";
            p.defeatedBases.Add("b01");
            p.defeatedBases.Add("b02");
            p.stats.battles = 9; p.stats.victories = 6; p.stats.defeats = 3; p.stats.kills = 412; p.stats.soldiersLost = 96;
            p.stats.goldEarned = 3210; p.stats.bestGrade = "A";
            p.tutorial.drawStroke = false;
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
            menu.Research.SetBranch(ResearchBranch.Troops);
            yield return Shot(menu, menu.Research, "ui_research");
            menu.Research.SetBranch(ResearchBranch.Defense);
            yield return Shot(menu, menu.Research, "ui_research_defense");
            yield return Shot(menu, menu.BaseDefense, "ui_base_defense");
            yield return Shot(menu, menu.Profile, "ui_profile");
            yield return Shot(menu, menu.Settings, "ui_settings");

            yield return UITestUtil.SetTarget(2340, 1080);
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("ui_main_menu_tall");
            yield return Shot(menu, menu.Research, "ui_research_tall");
            yield return UITestUtil.SetTarget(1280, 720);

            // Battle: second base, live combat with the HUD and the first battle tutorial.
            var db = MetaGame.Db;
            menu.StartBattle(db.roster.bases[1]);
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null, 5f);
            BattleTestUtil.Pointer = new SimulatedPointerSource();
            PointerInput.Source = BattleTestUtil.Pointer;
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(1.2f);
            UITestUtil.Capture("ui_tutorial");

            var squad = SquadController.Instance;
            Vector3 c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-7f, 0, 4f), c + new Vector3(7f, 0, 5f)), 0.6f);
            yield return UITestUtil.WaitReal(0.25f);
            UITestUtil.Capture("ui_shape_hint");
            // Advance toward the first enemies and fight.
            var arc = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Lerp(160f, 20f, i / 24f) * Mathf.Deg2Rad;
                arc.Add(new Vector3(0f, 0, 15.5f) + new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 3.0f));
            }
            Debug.Log($"[Capture] arc start over UI: {PointerInput.IsOverUI(BattleTestUtil.ToScreen(arc[0]))}");
            yield return BattleTestUtil.Drag(arc.ToArray(), 0.8f);
            Debug.Log($"[Capture] after arc drag: shape {squad.LastShape}");
            var bm = BattleManager.Instance;
            // March the formation toward the nearest enemy squad so the HUD capture shows a firefight.
            TactileWars.Units.EnemySquad nearest = null;
            float best = float.MaxValue;
            foreach (var es in Object.FindObjectsByType<TactileWars.Units.EnemySquad>(FindObjectsSortMode.None))
            {
                float d = (es.Center - squad.Centroid).sqrMagnitude;
                if (d < best) { best = d; nearest = es; }
            }
            if (nearest != null) squad.MoveFormationTo(nearest.Center + (squad.Centroid - nearest.Center).normalized * 6.5f);
            float deadline = Time.time + 14f;
            while (Time.time < deadline && bm.Kills < 8) yield return null;
            bm.Meter.Add(bm.Meter.Required * 0.62f - bm.Meter.Value);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("battle_hud");
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
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance != hud && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 8f);
            yield return UITestUtil.SetTarget(1280, 720);
            yield return UITestUtil.WaitReal(1.0f);
            foreach (var s in SquadController.Instance.Soldiers) if (s != null && s.IsAlive) s.Health.Kill(TeamColorId.Teal);
            var hud2 = BattleHUD.Instance;
            yield return UITestUtil.WaitUntil(() => hud2.Results.AnimationDone, 12f);
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("ui_defeat");
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
