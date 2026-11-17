using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.UI;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Phase 5 in play mode: defense battles, the AI attacker, the new defenses, wall routing, the
    /// counterattack wave, the stroke sequence regression and base editor persistence.</summary>
    public class DefenseBattleTests
    {
        [SetUp]
        public void SetUp()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.tutorial.drawStroke = true;
            p.tutorial.tapToMove = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSpeed.Reset();
            BattleTestUtil.Cleanup();
            UITestUtil.Cleanup();
            yield return null;
        }

        static void Place(PlayerProfile p, string id, float x, float z, float yaw = 180f) =>
            p.BaseLayout.placements.Add(new DefensePlacement { defenseId = id, x = x, z = z, rotation = yaw });

        public static IEnumerator LoadDefense(bool test, int seed)
        {
            BattleLaunch.PrepareDefense(test, seed);
            SceneManager.LoadScene("Battle");
            yield return UITestUtil.WaitForScene("Battle");
            yield return UITestUtil.WaitUntil(() => BattleManager.Instance != null && SquadController.Instance != null && SquadController.Instance.Soldiers.Count > 0, 10f);
            yield return null;
        }

        static void ClearHandcraftedEnemies()
        {
            foreach (var n in new[] { "Enemies", "Defenses" })
            {
                var go = GameObject.Find(n);
                if (go != null) for (int i = go.transform.childCount - 1; i >= 0; i--) Object.Destroy(go.transform.GetChild(i).gameObject);
            }
        }

        static T Spawn<T>(string id, Vector3 pos, float yaw = 180f) where T : StructureBase
        {
            var prefab = MetaDatabase.Load().defenses.Get(id);
            Assert.IsNotNull(prefab, "catalog prefab " + id);
            var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f));
            return go.GetComponent<T>();
        }

        static float SquadHealth(SquadController s, out int damaged)
        {
            float h = 0f; damaged = 0;
            foreach (var so in s.Soldiers)
            {
                if (so == null || !so.IsAlive) continue;
                h += so.Health.Current;
                if (so.Health.Current < so.Health.max - 0.01f) damaged++;
            }
            return h;
        }

        // ------------------------------------------------------------ defense battles

        [UnityTest]
        public IEnumerator DefenseBattle_RunsToOutcome_AndRecordsConsumption()
        {
            var p = MetaGame.Profile;
            p.level = 3;
            p.SetResearch("def_turret", 1);
            p.SetResearch("def_geyser", 1);
            Place(p, "mine", 0.5f, 24.5f); Place(p, "mine", -3.5f, 27.5f); Place(p, "mine", 3.5f, 31.5f);
            Place(p, "turret", 6.5f, 40.5f); Place(p, "turret", -6.5f, 40.5f);
            Place(p, "squad", -6.5f, 60.5f); Place(p, "geyser", 1.5f, 70.5f);
            int total = p.BaseLayout.placements.Count;
            SaveSystem.Save();
            int goldBefore = p.gold;

            yield return LoadDefense(false, 11);
            var setup = BattleSetup.Instance;
            var bm = BattleManager.Instance;
            Assert.IsTrue(setup.DefenseMode && bm.DefenseMode, "defense mode");
            Assert.IsTrue(setup.attackerAI.enabled, "AI attacker drives the squad");
            Assert.IsFalse(bm.recorder.InputEnabled, "the player cannot draw for the attackers");
            Assert.AreEqual(total, setup.Placed.Count, "every saved placement spawned");
            Assert.AreEqual(p.team, BattleContext.Config.enemyTeam, "defenses wear the player's color");
            Assert.AreNotEqual(p.team, BattleContext.Config.playerTeam, "attackers wear another color");
            Assert.AreEqual(BattleContext.Config.playerTeam, SquadController.Instance.Soldiers[0].Color);
            foreach (var t in Object.FindObjectsByType<Turret>(FindObjectsSortMode.None)) Assert.AreEqual(Team.Enemy, t.team);

            GameSpeed.Set(6f);
            float end = Time.realtimeSinceStartup + 150f;
            while (!bm.Over && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(bm.Over, "the defense battle reached an outcome");
            yield return UITestUtil.WaitUntil(() => BattleSession.LastDefense != null, 5f);
            var s = BattleSession.LastDefense;
            Assert.IsNotNull(s);
            Debug.Log($"[Test] defense {(s.held ? "HELD" : "FELL")} after {bm.Elapsed:0.0}s (timeout {bm.TimedOut}), popped {s.attackersPopped}/{s.attackersTotal}, points lost {s.pointsLost}, consumed {s.consumed.Count} [{string.Join(",", s.consumed)}], gold +{s.gold}, AI orders {setup.attackerAI.Orders} lines {setup.attackerAI.LinesFormed} rings {setup.attackerAI.RingsFormed} clusters {setup.attackerAI.ClustersFormed} bubbles {setup.attackerAI.BubblesPopped}");
            Assert.AreEqual(!bm.Result.victory, s.held, "attackers winning means the base fell");
            var disk = SaveSystem.Reload();
            Assert.AreEqual(total, disk.BaseLayout.placements.Count + disk.BaseLayout.destroyed.Count, "nothing lost: live plus wrecks");
            Assert.AreEqual(s.consumed.Count, disk.BaseLayout.destroyed.Count, "consumed defenses recorded as wrecks");
            Assert.Greater(s.consumed.Count, 0, "at least the mines fired");
            if (s.held) Assert.Greater(disk.gold, goldBefore, "a held defense pays"); else Assert.AreEqual(goldBefore, disk.gold);
            Assert.Greater(setup.attackerAI.Orders, 3, "the AI gave orders");
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance.DefenseResults != null && BattleHUD.Instance.DefenseResults.Shown, 6f);
            Assert.IsTrue(BattleHUD.Instance.DefenseResults.Shown, "defense result card shown");
        }

        [UnityTest]
        public IEnumerator AttackerAI_FormsLinesRingsClusters_AndAdvances()
        {
            var p = MetaGame.Profile;
            p.level = 2;
            Place(p, "mine", 0.5f, 25.5f); Place(p, "mine", -1.5f, 28.5f);
            Place(p, "squad", 1.5f, 36.5f);
            SaveSystem.Save();
            yield return LoadDefense(true, 3);
            var ai = BattleSetup.Instance.attackerAI;
            var squad = SquadController.Instance;
            var bm = BattleManager.Instance;
            float z0 = squad.Centroid.z;
            GameSpeed.Set(3f);
            float end = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < end && !bm.Over && (ai.ClustersFormed == 0 || ai.LinesFormed == 0 || ai.RingsFormed == 0)) yield return null;
            Debug.Log($"[Test] AI state {ai.Current}, lines {ai.LinesFormed}, rings {ai.RingsFormed}, clusters {ai.ClustersFormed}, bubbles {ai.BubblesPopped}, z {z0:0.0} -> {squad.Centroid.z:0.0}, kills {bm.Kills}, alive {squad.AliveCount}");
            Assert.GreaterOrEqual(ai.LinesFormed, 1, "a line toward the defender squad");
            Assert.GreaterOrEqual(ai.RingsFormed, 1, "a spread ring near the mines");
            Assert.GreaterOrEqual(ai.ClustersFormed, 1, "a cluster on the capture pad");
            Assert.Greater(squad.Centroid.z - z0, 20f, "the squad advanced up the lane");
        }

        // ------------------------------------------------------------ new defenses

        [UnityTest]
        public IEnumerator Tesla_ChainHitsMultipleSoldiers()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(1.5f);
            var squad = BattleTestUtil.Squad;
            SquadHealth(squad, out int before);
            var nearest = squad.Soldiers[0].transform.position;
            var tesla = Spawn<TeslaTower>("tesla", nearest + new Vector3(0f, 0f, 2.5f));
            yield return null;
            tesla.ForceCharged();
            yield return null;
            yield return null;
            SquadHealth(squad, out int after);
            Debug.Log($"[Test] tesla chain hits {tesla.LastChainHits}, damaged soldiers {before} -> {after}");
            Assert.GreaterOrEqual(tesla.LastChainHits, 3, "the arc chains through several soldiers");
            Assert.GreaterOrEqual(after - before, 3, "each chained soldier took damage");
            Assert.IsTrue(tesla.arcGlow.enabled, "the arc is visible");
        }

        [UnityTest]
        public IEnumerator Sniper_AimsShootsAndDamages()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(1.5f);
            var squad = BattleTestUtil.Squad;
            float h0 = SquadHealth(squad, out _);
            int alive0 = squad.AliveCount;
            var sniper = Spawn<Sniper>("sniper", squad.Centroid + new Vector3(0f, 0f, 13f));
            bool sawAim = false;
            float end = Time.time + 8f;
            while (Time.time < end && sniper.ShotsFired == 0) { if (sniper.Aiming && sniper.aimLine.enabled) sawAim = true; yield return null; }
            yield return BattleTestUtil.Wait(1f);
            float h1 = SquadHealth(squad, out _);
            Debug.Log($"[Test] sniper shots {sniper.ShotsFired}, saw aim line {sawAim}, alive {alive0} -> {squad.AliveCount}, health {h0:0} -> {h1:0}");
            Assert.GreaterOrEqual(sniper.ShotsFired, 1);
            Assert.IsTrue(sawAim, "a visible aiming line before the shot");
            Assert.IsTrue(squad.AliveCount < alive0 || h1 < h0 - 1f, "the shot hurt the squad");
        }

        [UnityTest]
        public IEnumerator Geyser_EruptsAndDamagesSoldiersAround()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(1.5f);
            var squad = BattleTestUtil.Squad;
            SquadHealth(squad, out int before);
            var geyser = Spawn<Geyser>("geyser", squad.Soldiers[3].transform.position);
            float end = Time.time + 4f;
            bool warned = false;
            while (Time.time < end && geyser.Eruptions == 0) { warned |= geyser.Warning; yield return null; }
            yield return null;
            SquadHealth(squad, out int after);
            int dead = 0;
            foreach (var so in squad.Soldiers) if (so != null && !so.IsAlive) dead++;
            Debug.Log($"[Test] geyser eruptions {geyser.Eruptions}, warned {warned}, damaged {before} -> {after}, dead {dead}");
            Assert.GreaterOrEqual(geyser.Eruptions, 1);
            Assert.IsTrue(warned, "a rumble warning precedes the eruption");
            Assert.GreaterOrEqual(after - before + dead, 2, "the eruption hit several soldiers");
        }

        // ------------------------------------------------------------ squad movement

        [UnityTest]
        public IEnumerator WallRouting_TapBehindWall_SquadPassesThroughGate()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(0.5f);
            var squad = BattleTestUtil.Squad;
            // Stand south of the crenellated wall at z = 77 (gate between x = -4 and 4), well left of the gate.
            squad.MoveFormationTo(new Vector3(-10f, 0f, 71f));
            squad.TeleportToSlots();
            yield return BattleTestUtil.Wait(0.6f);
            Vector3 goal = new Vector3(-10f, 0f, 84f);
            Assert.IsFalse(squad.Nav.LineClear(squad.Centroid, goal), "the wall blocks the straight way");
            yield return BattleTestUtil.Tap(goal);
            Assert.IsTrue(squad.IsRouting, "the tap starts a route through the gate");
            Debug.Log("[Test] route: " + string.Join(" ", RoutePoints(squad)));
            float end = Time.time + 25f;
            float frac = 0f;
            while (Time.time < end)
            {
                int n = 0, past = 0;
                foreach (var so in squad.Soldiers) if (so != null && so.IsAlive) { n++; if (so.transform.position.z > 78.5f) past++; }
                frac = n > 0 ? past / (float)n : 0f;
                if (frac > 0.9f && !squad.IsRouting) break;
                yield return null;
            }
            Debug.Log($"[Test] wall routing: {frac:P0} of the squad north of the wall, centroid {squad.Centroid}");
            Assert.Greater(frac, 0.8f, "the squad went through the gate instead of stalling on the wall");
        }

        static IEnumerable<string> RoutePoints(SquadController s)
        {
            foreach (var p in s.Route) yield return $"({p.x:0.0},{p.z:0.0})";
        }

        [UnityTest]
        public IEnumerator StrokeSequence_LineThenArcThenCircle_AreRecognizedEachTime()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(1.2f);
            var squad = BattleTestUtil.Squad;
            Vector3 c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-8f, 0, 3f), c + new Vector3(8f, 0, 5f)), 0.6f);
            yield return BattleTestUtil.Wait(0.4f);
            Assert.AreEqual(ShapeType.Line, squad.LastShape, "line");

            // The crescent from the UI capture script, drawn relative to the squad so it starts on open ground.
            c = squad.Centroid;
            var arc = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Lerp(160f, 20f, i / 24f) * Mathf.Deg2Rad;
                arc.Add(c + new Vector3(0f, 0, 6f) + new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 3.0f));
            }
            Assert.IsFalse(PointerInput.IsOverUI(BattleTestUtil.ToScreen(arc[0])), "arc starts on the ground");
            yield return BattleTestUtil.Drag(arc.ToArray(), 0.8f);
            yield return BattleTestUtil.Wait(0.4f);
            Assert.AreEqual(ShapeType.Free, squad.LastShape, "the arc right after a line is a free shape, not a line");

            c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.CirclePoints(c + new Vector3(0, 0, 1f), 4.2f), 0.9f);
            yield return BattleTestUtil.Wait(0.4f);
            Assert.AreEqual(ShapeType.Circle, squad.LastShape, "circle");

            // Second line after the circle: recorder state is clean between strokes.
            c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-7f, 0, 4f), c + new Vector3(7f, 0, 4f)), 0.5f);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual(ShapeType.Line, squad.LastShape, "line again");
        }

        [UnityTest]
        public IEnumerator Counterattack_AfterFirstCapture_MarchesOnThePoint()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(0.5f);
            var bm = BattleManager.Instance;
            var cp = bm.capturePoints[0];
            cp.ForceCapture();
            float delay = bm.battleConfig.counterattackDelay;
            yield return BattleTestUtil.Wait(delay + 0.6f);
            Assert.AreEqual(1, bm.CounterattacksLaunched, "one wave after the first capture");
            var wave = bm.LastCounterattack;
            Assert.IsNotNull(wave);
            Assert.AreEqual(bm.battleConfig.counterattackSize, wave.AliveCount);
            float d0 = Vector3.Distance(wave.Center, cp.transform.position);
            yield return BattleTestUtil.Wait(2.5f);
            float d1 = Vector3.Distance(wave.Center, cp.transform.position);
            Debug.Log($"[Test] counterattack distance to point {d0:0.0} -> {d1:0.0}");
            Assert.Less(d1, d0 - 2f, "the wave marches on the captured point");
        }

        // ------------------------------------------------------------ base editor

        [UnityTest]
        public IEnumerator BaseEditor_PlaceValidateSave_SurvivesRestart()
        {
            var p = MetaGame.Profile;
            p.gold = 600;
            p.SetResearch("def_turret", 1);
            SaveSystem.Save();
            SceneManager.LoadScene("BaseEditor");
            yield return UITestUtil.WaitForScene("BaseEditor");
            yield return UITestUtil.WaitUntil(() => BaseEditorRoot.Instance != null && BaseEditorRoot.Instance.Hud != null, 5f);
            var root = BaseEditorRoot.Instance;
            Assert.AreEqual(MetaGame.Defenses.entries.Length, root.Hud.Cards.Count, "one tray card per defense type");
            Assert.AreEqual(PlacementError.AttackerZone, root.ShowGhost("turret", new Vector3(0f, 0, 10f)));
            Assert.IsFalse(root.GhostValid);
            Assert.AreEqual(PlacementError.None, root.ShowGhost("turret", new Vector3(-5f, 0, 36f)));
            Assert.IsTrue(root.GhostValid);
            root.HideGhost();
            Assert.AreEqual(PlacementError.None, root.TryPlace("mine", new Vector3(-4f, 0, 28f)));
            Assert.AreEqual(PlacementError.None, root.TryPlace("turret", new Vector3(6f, 0, 36f)));
            Assert.AreEqual(PlacementError.Locked, root.TryPlace("tank", new Vector3(-6f, 0, 50f)), "tank not researched");
            root.RotateSelected();
            Assert.IsTrue(root.Session.Dirty);
            int pending = root.Session.PendingGold;
            Assert.IsTrue(root.Save());
            Assert.AreEqual(600 - pending, MetaGame.Profile.gold);

            // Restart: drop the cached profile and reload the editor scene from disk.
            SaveSystem.DirectoryOverride = UITestUtil.SaveDir;
            SceneManager.LoadScene("BaseEditor");
            yield return UITestUtil.WaitForScene("BaseEditor");
            yield return UITestUtil.WaitUntil(() => BaseEditorRoot.Instance != null && BaseEditorRoot.Instance != root && BaseEditorRoot.Instance.Hud != null, 5f);
            root = BaseEditorRoot.Instance;
            Assert.AreEqual(2, root.Session.Placements.Count, "layout loaded from disk");
            Assert.AreEqual("turret", root.Session.Placements[1].defenseId);
            Assert.AreEqual(225f, root.Session.Placements[1].rotation, 1e-3f, "rotation saved (180 + 45)");
            Assert.AreEqual(600 - pending, MetaGame.Profile.gold);
            Assert.IsFalse(root.Session.Dirty);
        }
    }
}
