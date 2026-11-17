using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Phase 5 captures at 1280x720 into Docs/Screenshots: base editor (empty, placing, full), defense
    /// battle, raid report, defenses showcase and the two map variants. Category "DefenseScreenshots".</summary>
    [Category("DefenseScreenshots")]
    public class DefenseScreenshotCapture
    {
        const int W = 1280, H = 720;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSpeed.Reset();
            BattleTestUtil.Cleanup();
            UITestUtil.Cleanup();
            yield return null;
        }

        static void Profile(PlayerProfile p)
        {
            p.teamChosen = true;
            p.playerName = "Captain Dab";
            p.team = TeamColorId.Red;
            p.level = 9;
            p.xp = 3700;
            p.gold = 2600;
            p.tutorial.drawStroke = true;
            p.tutorial.tapToMove = true;
            foreach (var id in new[] { "def_mine", "def_turret", "def_bunker", "def_tank", "def_cannon", "def_tesla", "def_sniper", "def_geyser" }) p.SetResearch(id, 1);
            p.SetResearch("def_mine", 2);
            p.SetResearch("def_turret", 2);
        }

        static IEnumerator LoadEditor()
        {
            SceneManager.LoadScene("BaseEditor");
            yield return UITestUtil.WaitForScene("BaseEditor");
            yield return UITestUtil.WaitUntil(() => BaseEditorRoot.Instance != null && BaseEditorRoot.Instance.Hud != null, 5f);
            yield return UITestUtil.SetTarget(W, H);
            yield return UITestUtil.WaitReal(0.4f);
        }

        [UnityTest]
        public IEnumerator CaptureDefenseViews()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            Profile(p);
            SaveSystem.Save();

            // ---- base editor: empty
            yield return LoadEditor();
            var root = BaseEditorRoot.Instance;
            root.FocusOn(new Vector3(0f, 0f, 30f));
            yield return UITestUtil.WaitReal(0.3f);
            UITestUtil.Capture("base_editor_empty");

            // ---- placing: a pinned valid ghost next to an invalid one over the attacker zone
            root.TryPlace("turret", new Vector3(-6f, 0, 34f));
            root.TryPlace("mine", new Vector3(2f, 0, 25f));
            root.Deselect();
            yield return UITestUtil.WaitReal(2.2f); // let placement toasts fade
            root.ShowGhost("tesla", new Vector3(5f, 0, 30f));
            var pinned = PinGhost(root);
            root.ShowGhost("bunker", new Vector3(-5.5f, 0, 33f));
            yield return UITestUtil.WaitReal(0.3f);
            Debug.Log($"[Capture] invalid ghost reason: {BaseEditSession.Describe(root.LastError)}");
            UITestUtil.Capture("base_editor_placing");
            foreach (var g in pinned) Object.Destroy(g);
            root.HideGhost();

            // ---- full layout with a selected sniper's range ring
            void T(string id, float x, float z) { var e = root.TryPlace(id, new Vector3(x, 0, z)); if (e != PlacementError.None) Debug.Log($"[Capture] {id} at {x},{z}: {e}"); }
            T("mine", -2f, 22f); T("mine", 4f, 21f); T("mine", -6f, 27f); T("mine", 6f, 28f);
            T("geyser", 0f, 31f); T("geyser", -9f, 38f);
            T("squad", 8f, 40f); T("squad", -9f, 55f);
            T("bunker", 7f, 52f);
            T("tesla", -3f, 40f); T("tesla", 3f, 60f);
            T("sniper", -11f, 30f);
            T("cannon", 9f, 66f);
            T("tank", -5f, 75f);
            T("turret", 6f, 80f);
            root.Save();
            int sel = -1;
            for (int i = 0; i < root.Session.Placements.Count; i++) if (root.Session.Placements[i].defenseId == "tesla" && sel < 0) sel = i;
            root.Select(sel);
            root.FocusOn(new Vector3(0f, 0f, 44f));
            yield return UITestUtil.WaitReal(2.4f); // "Base saved" toast fades
            Debug.Log($"[Capture] full layout: {root.Session.Placements.Count} defenses, budget {root.Session.BudgetUsed}/{root.Session.BudgetCap}, space {root.Session.SpaceUsed}/{root.Session.SpaceCap}, gold {MetaGame.Profile.gold}");
            UITestUtil.Capture("base_editor_full");
            root.Deselect();
            root.SetOverview(true);
            root.SnapZoom();
            yield return UITestUtil.WaitReal(0.4f);
            UITestUtil.Capture("base_editor_overview");
            root.SetOverview(false);

            // ---- defense battle mid fight
            BattleLaunch.PrepareDefense(false, 21);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && BattleHUD.Instance != null && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 10f);
            yield return UITestUtil.SetTarget(W, H);
            var bm = BattleManager.Instance;
            GameSpeed.Set(2f);
            float end = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < end && !bm.Over && (bm.Elapsed < 12f || ProjectileManager.Instance.ActiveCount < 25)) yield return null;
            BattleHUD.Instance.SetSpeed(2f);
            yield return null;
            Debug.Log($"[Capture] defense battle at {bm.Elapsed:0.0}s: projectiles {ProjectileManager.Instance.ActiveCount}, attackers {SquadController.Instance.AliveCount}, AI {BattleSetup.Instance.attackerAI.Current}");
            UITestUtil.Capture("base_defense_battle");
            GameSpeed.Set(6f);
            end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end && !bm.Over) yield return null;
            GameSpeed.Reset();
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance.DefenseResults != null && BattleHUD.Instance.DefenseResults.Shown, 8f);
            yield return UITestUtil.WaitReal(0.8f);
            UITestUtil.Capture("base_defense_result");

            // ---- simulated raid report on return to the menu
            p = MetaGame.Profile;
            p.lastSeenUtcTicks = System.DateTime.UtcNow.AddHours(-3).Ticks;
            SaveSystem.Save();
            SceneManager.LoadScene("MainMenu");
            yield return UITestUtil.WaitForScene("MainMenu");
            yield return UITestUtil.WaitUntil(() => MenuRoot.Instance != null && MenuRoot.Instance.RaidCard != null, 5f);
            yield return UITestUtil.SetTarget(W, H);
            yield return UITestUtil.WaitReal(0.8f);
            Debug.Log($"[Capture] raid: {(MenuRoot.Instance.LastRaid != null ? MenuRoot.Instance.LastRaid.attackerName + " held=" + MenuRoot.Instance.LastRaid.held : "none")}");
            UITestUtil.Capture("base_raid_report");
            MenuRoot.Instance.RaidCard.Close();
            MenuRoot.Instance.Open(MenuRoot.Instance.BaseDefense);
            yield return UITestUtil.WaitReal(0.6f);
            UITestUtil.Capture("ui_base_defense");
            Assert.Pass();
        }

        static GameObject[] PinGhost(BaseEditorRoot root)
        {
            // Leaves a copy of the current ghost (and its footprint disc) so one image shows valid and invalid.
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (Transform t in root.worldRoot)
                if (t.gameObject.activeSelf && (t.name.StartsWith("Display_") || t.name == "GhostDisc" || t.name == "RangeRing"))
                {
                    var c = Object.Instantiate(t.gameObject, t.parent);
                    c.name = "Pinned_" + t.name;
                    root.ColorRenderers(c, t.name == "RangeRing" ? new Color(1f, 1f, 1f, 0.9f) : BaseEditorRoot.ValidColor);
                    list.Add(c);
                }
            return list.ToArray();
        }

        [UnityTest]
        public IEnumerator CaptureShowcaseAndMaps()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            Profile(p);
            // Every defense type close to the attackers' approach, so they all fire in one frame.
            void Pl(string id, float x, float z, float yaw = 180f) => p.BaseLayout.placements.Add(new DefensePlacement { defenseId = id, x = x, z = z, rotation = yaw });
            Pl("mine", 1f, 21f); Pl("mine", -3f, 23f); Pl("geyser", 2.5f, 25f); Pl("geyser", -1.5f, 27f);
            Pl("tesla", -4f, 28f); Pl("turret", 5f, 29f); Pl("sniper", -9f, 33f); Pl("bunker", 7f, 36f);
            Pl("cannon", -6f, 39f); Pl("tank", 2f, 36f, 180f); Pl("squad", -2f, 33f);
            p.level = 2;   // a smaller attacking squad so the showcase lasts
            foreach (var id in new[] { "def_mine", "def_turret", "def_bunker", "def_tank", "def_cannon", "def_tesla", "def_sniper", "def_geyser" }) p.SetResearch(id, 3);
            SaveSystem.Save();
            BattleLaunch.PrepareDefense(true, 5);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && BattleHUD.Instance != null && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 10f);
            yield return UITestUtil.SetTarget(W, H);
            var bm = BattleManager.Instance;
            var cam = BattleCamera.Instance;
            int zaps = 0, shots = 0, eruptions = 0;
            float end = Time.realtimeSinceStartup + 45f;
            float best = 0f;
            bool captured = false;
            while (Time.realtimeSinceStartup < end && !bm.Over && bm.Elapsed < 30f)
            {
                var tesla = Object.FindAnyObjectByType<TeslaTower>();
                var sn = Object.FindAnyObjectByType<Sniper>();
                bool arc = tesla != null && tesla.arcGlow != null && tesla.arcGlow.enabled;
                bool aim = sn != null && sn.aimLine != null && sn.aimLine.enabled;
                int proj = ProjectileManager.Instance.ActiveCount;
                int alive = 0;
                foreach (var sb in Object.FindObjectsByType<StructureBase>(FindObjectsSortMode.None)) if (!sb.Destroyed) alive++;
                float score = (arc ? 30f : 0f) + (aim ? 20f : 0f) + Mathf.Min(proj, 40) + alive * 3f;
                if (bm.Elapsed > 4f && score > best + 4f)
                {
                    best = score;
                    UITestUtil.Capture("defenses_showcase");
                    captured = true;
                }
                if (tesla != null) zaps = tesla.TotalZaps;
                if (sn != null) shots = sn.ShotsFired;
                foreach (var g in Object.FindObjectsByType<Geyser>(FindObjectsSortMode.None)) eruptions = Mathf.Max(eruptions, g.Eruptions);
                yield return null;
            }
            if (!captured) UITestUtil.Capture("defenses_showcase");
            Debug.Log($"[Capture] showcase score {best} at {bm.Elapsed:0.0}s: tesla zaps {zaps}, sniper shots {shots}, geyser eruptions {eruptions}");
            GameSpeed.Reset();
            BattleLaunch.Clear();

            // ---- map variants (AI bases b03 canyon and b04 ridge)
            foreach (var (baseIndex, name) in new[] { (2, "map_canyon"), (3, "map_ridge") })
            {
                var b = MetaGame.Db.roster.bases[baseIndex];
                Assert.IsNotNull(b.map, b.id + " uses a map variant");
                BattleLaunch.Prepare(b);
                SceneManager.LoadScene("Battle");
                yield return UITestUtil.WaitForScene("Battle");
                yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 10f);
                yield return UITestUtil.SetTarget(W, H);
                yield return UITestUtil.WaitReal(2.6f); // start banner fades
                UITestUtil.Capture(name);
                cam = BattleCamera.Instance;
                var c = cam.Cam;
                cam.enabled = false;
                float ortho = c.orthographicSize;
                c.orthographicSize = 31f;
                var rot = c.transform.rotation;
                c.transform.position = new Vector3(0f, 0f, 56f) - rot * Vector3.forward * 60f;
                var hud = BattleHUD.Instance;
                if (hud != null) hud.SetHudVisible(false);
                yield return null;
                UITestUtil.Capture(name + "_overview");
                Debug.Log($"[Capture] {name}: {b.displayName} on {b.map.displayName}, squads {b.layout.squads.Length}, defenses {b.layout.defenses.Length}");
                c.orthographicSize = ortho;
                cam.enabled = true;
                BattleLaunch.Clear();
            }
            Assert.Pass();
        }
    }
}
