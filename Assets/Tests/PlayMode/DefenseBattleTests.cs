using System.Collections;
using System.Collections.Generic;
using TactileWars.Army;
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
    /// counterattack wave, unit routing through a wall gate and base editor persistence.</summary>
    public class DefenseBattleTests
    {
        [SetUp]
        public void SetUp()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            BattleTestUtil.MarkTutorialSeen(p);
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

        /// <summary>Summed health of the army's living soldiers; 'damaged' counts those below full health.</summary>
        static float ArmyHealth(out int damaged)
        {
            float h = 0f; damaged = 0;
            foreach (var so in BattleTestUtil.ArmySoldiers())
            {
                h += so.Health.Current;
                if (so.Health.Current < so.Health.max - 0.01f) damaged++;
            }
            return h;
        }

        /// <summary>Deploys entries 0 and 1 on two neighboring hexes near the army's spawn (six soldiers in a cluster).</summary>
        static void DeployPair()
        {
            var army = BattleTestUtil.Army;
            var a = BattleTestUtil.FreeHexNear(army.spawnPoint);
            Assert.IsNotNull(army.Deploy(0, a));
            var around = new List<HexCoord>();
            army.Grid.Neighbors(a, around);
            foreach (var n in around)
                if (army.CanDeploy(1, n)) { Assert.IsNotNull(army.Deploy(1, n)); return; }
            Assert.Fail("no free neighbor hex for the second unit");
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
            DeployPair();
            yield return BattleTestUtil.Wait(0.5f);
            ArmyHealth(out int before);
            var nearest = BattleTestUtil.ArmySoldiers()[0].transform.position;
            var tesla = Spawn<TeslaTower>("tesla", nearest + new Vector3(0f, 0f, 2.5f));
            yield return null;
            tesla.ForceCharged();
            yield return null;
            yield return null;
            ArmyHealth(out int after);
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
            var army = BattleTestUtil.Army;
            BattleTestUtil.DeployAll();
            yield return BattleTestUtil.Wait(0.5f);
            float h0 = ArmyHealth(out _);
            int alive0 = army.AliveCount;
            var sniper = Spawn<Sniper>("sniper", army.Centroid + new Vector3(0f, 0f, 13f));
            bool sawAim = false;
            float end = Time.time + 8f;
            while (Time.time < end && sniper.ShotsFired == 0) { if (sniper.Aiming && sniper.aimLine.enabled) sawAim = true; yield return null; }
            yield return BattleTestUtil.Wait(1f);
            float h1 = ArmyHealth(out _);
            Debug.Log($"[Test] sniper shots {sniper.ShotsFired}, saw aim line {sawAim}, alive {alive0} -> {army.AliveCount}, health {h0:0} -> {h1:0}");
            Assert.GreaterOrEqual(sniper.ShotsFired, 1);
            Assert.IsTrue(sawAim, "a visible aiming line before the shot");
            Assert.IsTrue(army.AliveCount < alive0 || h1 < h0 - 1f, "the shot hurt the army");
        }

        [UnityTest]
        public IEnumerator Geyser_EruptsAndDamagesSoldiersAround()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(1.5f);
            var units = BattleTestUtil.DeployAll();
            yield return BattleTestUtil.Wait(0.5f);
            ArmyHealth(out int before);
            int startSoldiers = BattleTestUtil.ArmySoldiers().Count;
            var geyser = Spawn<Geyser>("geyser", units[1].Soldiers[0].transform.position);
            float end = Time.time + 4f;
            bool warned = false;
            while (Time.time < end && geyser.Eruptions == 0) { warned |= geyser.Warning; yield return null; }
            yield return null;
            ArmyHealth(out int after);
            int dead = startSoldiers - BattleTestUtil.ArmySoldiers().Count;
            Debug.Log($"[Test] geyser eruptions {geyser.Eruptions}, warned {warned}, damaged {before} -> {after}, dead {dead}");
            Assert.GreaterOrEqual(geyser.Eruptions, 1);
            Assert.IsTrue(warned, "a rumble warning precedes the eruption");
            Assert.GreaterOrEqual(after - before + dead, 2, "the eruption hit several soldiers");
        }

        // ------------------------------------------------------------ unit movement

        [UnityTest]
        public IEnumerator WallRouting_OrderBehindWall_UnitPassesThroughGate()
        {
            yield return BattleTestUtil.LoadBattle();
            ClearHandcraftedEnemies();
            yield return BattleTestUtil.Wait(0.5f);
            var army = BattleTestUtil.Army;
            // Start south of the crenellated wall at z = 77 (gate between x = -4 and 4), well left of the gate.
            var unit = army.Deploy(0, BattleTestUtil.FreeHexNear(new Vector3(-10f, 0f, 71f)));
            Assert.IsNotNull(unit);
            var goal = BattleTestUtil.FreeHexNear(new Vector3(-10f, 0f, 84f));
            Vector3 from = army.Grid.Center(unit.Hex), to = army.Grid.Center(goal);
            Assert.Less(from.z, 76f); Assert.Greater(to.z, 78.5f);
            Assert.Less(Mathf.Max(from.x, to.x), -5f, "start and goal are both left of the gate");
            Assert.IsFalse(army.Nav.LineClear(from, to), "the wall blocks the straight way");
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(army.OrderMove(unit, goal), "a route through the gate exists");

            // Track where each soldier crosses the wall line.
            var lastZ = new Dictionary<Soldier, float>();
            int crossings = 0, outsideGate = 0;
            float end = Time.time + 25f;
            while (Time.time < end && (unit.IsMoving || !BattleTestUtil.AllWithin(unit, goal)))
            {
                foreach (var so in unit.Soldiers)
                {
                    if (so == null || !so.IsAlive) continue;
                    Vector3 p = so.transform.position;
                    if (lastZ.TryGetValue(so, out float z0) && z0 < 77f && p.z >= 77f)
                    {
                        crossings++;
                        if (Mathf.Abs(p.x) > 4f) outsideGate++;
                    }
                    lastZ[so] = p.z;
                }
                yield return null;
            }
            Debug.Log($"[Test] wall routing: arrived {!unit.IsMoving}, crossings {crossings}, outside the gate {outsideGate}, centroid {unit.Centroid}");
            Assert.IsFalse(unit.IsMoving, "the unit arrived instead of stalling on the wall");
            Assert.IsTrue(BattleTestUtil.AllWithin(unit, goal), "every soldier stands on the goal hex");
            Assert.GreaterOrEqual(crossings, unit.AliveCount, "every soldier crossed the wall line");
            Assert.AreEqual(0, outsideGate, "nobody crossed the wall outside the gate");
        }

        [UnityTest]
        public IEnumerator Counterattack_AfterFirstCapture_MarchesOnThePoint()
        {
            // Level 1 (the scene default) has no counterattack; base two on the same lane does.
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var b = MetaGame.Db.roster.bases[1];
            Assert.IsTrue(b.counterattack, "base two sends a counterattack");
            BattleLaunch.Prepare(b);
            yield return BattleTestUtil.LoadBattle();
            BattleLaunch.Clear();
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
