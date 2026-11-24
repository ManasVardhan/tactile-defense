using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Structures;
using TactileWars.UI;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Level 1 as a tutorial level (spec 6.1): no traps, passive enemies, the tutorial's Tank step before its
    /// capture step; and direct-fire defenses shooting Tank units first.</summary>
    public class LevelOneTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleLaunch.Clear();
            GameSpeed.Reset();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        /// <summary>Loads Level 1 through the launch path (its BaseDefinition and loadout).</summary>
        static IEnumerator LoadLevelOne(bool freshTutorial, params string[] seen)
        {
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var p = MetaGame.Profile;
            if (freshTutorial) { p.tutorialSeen.Clear(); p.tutorialSeen.AddRange(seen); }
            BattleLaunch.Prepare(MetaGame.Db.roster.bases[0]);
            yield return BattleTestUtil.LoadBattle();
            Assert.AreEqual("b01", BattleSetup.Instance.Applied.baseId);
        }

        static PlayerUnit DeployType(UnitTypeId type, HexCoord hex)
        {
            var army = BattleTestUtil.Army;
            for (int i = 0; i < army.Squad.Count; i++)
                if (army.Squad[i].type == type && army.Squad[i].state == EntryState.Ready) return army.Deploy(i, hex);
            Assert.Fail("no ready " + type);
            return null;
        }

        /// <summary>The free hex whose center is nearest 'dist' units from 'from' along 'dir' (flat).</summary>
        static HexCoord HexAt(Vector3 from, Vector3 dir, float dist, params HexCoord[] avoid)
        {
            var g = BattleTestUtil.Army.Grid;
            Vector3 want = from + dir.normalized * dist;
            HexCoord best = default;
            float bestErr = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c)) continue;
                bool skip = false;
                foreach (var a in avoid) if (HexGrid.Distance(a, c) < 2) skip = true;
                if (skip) continue;
                float err = (g.Center(c) - want).sqrMagnitude;
                if (err < bestErr) { bestErr = err; best = c; }
            }
            Assert.Less(bestErr, 2f, "a free hex near " + want); // within one outer radius (1.4)
            return best;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        // ------------------------------------------------------------ structures prefer Tanks

        [UnityTest]
        public IEnumerator StructuresPreferTankInRange([Values("turret", "sniper")] string id)
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            // An open stretch of the lane; the defense stands up the lane, the units south of it.
            Vector3 at = new Vector3(0f, 0f, 30f);
            var attackerHex = HexAt(at, Vector3.back + Vector3.left * 0.3f, 4f);
            var tankHex = HexAt(at, Vector3.back + Vector3.right * 0.6f, 6f, attackerHex);
            var attacker = DeployType(UnitTypeId.Attacker, attackerHex);
            var tank = DeployType(UnitTypeId.Tank, tankHex);
            yield return BattleTestUtil.Wait(0.6f); // settle on the hexes

            var prefab = MetaDatabase.Load().defenses.Get(id);
            Assert.IsNotNull(prefab, "catalog prefab " + id);
            var go = Object.Instantiate(prefab, at, Quaternion.Euler(0f, 180f, 0f));
            var structure = go.GetComponent<StructureBase>();
            Assert.IsNotNull(structure);
            float dA = Flat(attacker.Centroid, at), dT = Flat(tank.Centroid, at);
            Debug.Log($"[Test] {id}: attacker at {dA:0.0}, tank at {dT:0.0}");
            Assert.Less(dA, dT, "the Attacker unit is the nearer one");

            yield return BattleTestUtil.Wait(2f);
            Health target = structure is Sniper sn ? sn.LastTarget : structure.CurrentTarget;
            Assert.IsNotNull(target, id + " picked a target");
            var tankSoldiers = new List<Health>();
            foreach (var s in tank.Soldiers) if (s != null) tankSoldiers.Add(s.Health);
            Assert.IsTrue(target.drawsFire, id + " aims at a draws-fire soldier");
            CollectionAssert.Contains(tankSoldiers, target, id + " aims at a Tank soldier, not the nearer Attacker");
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator CannonKeepsNearestTarget()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            Vector3 at = new Vector3(0f, 0f, 30f);
            var attackerHex = HexAt(at, Vector3.back + Vector3.left * 0.3f, 5f);
            var tankHex = HexAt(at, Vector3.back + Vector3.right * 0.6f, 9f, attackerHex);
            var attacker = DeployType(UnitTypeId.Attacker, attackerHex);
            DeployType(UnitTypeId.Tank, tankHex);
            yield return BattleTestUtil.Wait(0.6f);
            var go = Object.Instantiate(MetaDatabase.Load().defenses.Get("cannon"), at, Quaternion.Euler(0f, 180f, 0f));
            var cannon = go.GetComponent<Cannon>();
            yield return BattleTestUtil.Wait(1f);
            var target = cannon.CurrentTarget;
            Assert.IsNotNull(target, "the cannon picked a target");
            Assert.IsFalse(target.drawsFire, "lobbed shells keep the nearest target");
            var attackerSoldiers = new List<Health>();
            foreach (var s in attacker.Soldiers) if (s != null) attackerSoldiers.Add(s.Health);
            CollectionAssert.Contains(attackerSoldiers, target);
            Object.Destroy(go);
        }

        // ------------------------------------------------------------ Level 1 layout

        [UnityTest]
        public IEnumerator LevelOneHasNoTrapsAndIsPassive()
        {
            yield return LoadLevelOne(false);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual(0, Object.FindObjectsByType<Mine>(FindObjectsSortMode.None).Length, "no mines");
            Assert.AreEqual(0, Object.FindObjectsByType<Geyser>(FindObjectsSortMode.None).Length, "no geysers");
            Assert.AreEqual(0, Object.FindObjectsByType<TeslaTower>(FindObjectsSortMode.None).Length, "no tesla");
            Assert.AreEqual(0, Object.FindObjectsByType<Cannon>(FindObjectsSortMode.None).Length, "no cannon");
            Assert.AreEqual(0, Object.FindObjectsByType<Bunker>(FindObjectsSortMode.None).Length, "no bunker");
            Assert.AreEqual(0, Object.FindObjectsByType<Tank>(FindObjectsSortMode.None).Length, "no tank structure");
            Assert.AreEqual(1, Object.FindObjectsByType<Turret>(FindObjectsSortMode.None).Length, "one turret");
            Assert.IsFalse(BattleManager.Instance.battleConfig.counterattackEnabled, "no counterattack on Level 1");

            var bm = BattleManager.Instance;
            var cp1 = bm.capturePoints[0].transform.position;
            var cp2 = bm.capturePoints[1].transform.position;
            var squads = Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None);
            Assert.AreEqual(2, squads.Length, "two enemy squads");
            EnemySquad guard1 = null, guard2 = null;
            foreach (var s in squads)
            {
                Assert.IsTrue(s.passive, s.name + " is passive");
                if (Flat(s.transform.position, cp1) < 4f) guard1 = s;
                if (Flat(s.transform.position, cp2) < 8f) guard2 = s;
            }
            Assert.IsNotNull(guard1, "a squad guards point 1");
            Assert.IsNotNull(guard2, "a squad guards point 2");
            Assert.AreEqual(4, guard1.AliveCount, "four grunts at point 1");
            Assert.IsNull(guard1.Bearer, "no flag bearer at point 1");
            Assert.IsNotNull(guard2.Bearer, "a flag bearer at point 2");
            Assert.AreEqual(6, guard2.AliveCount + 1, "a squad of 6 with its flag bearer at point 2");
            float turretToCp2 = Flat(Object.FindObjectsByType<Turret>(FindObjectsSortMode.None)[0].transform.position, cp2);
            Assert.Less(turretToCp2, 9f, "the turret covers point 2");

            // Nothing deployed: nobody moves.
            var start = new Dictionary<Health, Vector3>();
            foreach (var h in UnitRegistry.Of(Team.Enemy)) if (h.kind != TargetKind.Structure && h.kind != TargetKind.Mine) start[h] = h.transform.position;
            Assert.AreEqual(10, start.Count, "ten enemy soldiers in all");
            yield return BattleTestUtil.Wait(10f);
            float worst = 0f;
            foreach (var kv in start) if (kv.Key != null) worst = Mathf.Max(worst, Flat(kv.Key.transform.position, kv.Value));
            Debug.Log($"[Test] Level 1 idle: furthest enemy move {worst:0.00}");
            Assert.LessOrEqual(worst, 1f, "no enemy moved more than 1 unit with nothing deployed");
        }

        [UnityTest]
        public IEnumerator LevelOneSquadsWakeOnlyWithinWeaponRangePlusTwo()
        {
            yield return LoadLevelOne(false);
            yield return BattleTestUtil.Wait(0.3f);
            var bm = BattleManager.Instance;
            EnemySquad guard = null;
            foreach (var s in Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None))
                if (Flat(s.transform.position, bm.capturePoints[0].transform.position) < 4f) guard = s;
            Assert.IsNotNull(guard);
            float wake = guard.config.soldier.weapon.range + 2f;
            // A unit parked a little outside the wake distance of every guard soldier.
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            HexCoord hex = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!army.CanDeploy(0, c)) continue;
                float d = float.MaxValue;
                foreach (var h in UnitRegistry.Of(Team.Enemy)) if (h.kind != TargetKind.Structure) d = Mathf.Min(d, Flat(g.Center(c), h.transform.position));
                if (d < wake + 2f) continue;
                if (d < bestD) { bestD = d; hex = c; }
            }
            Assert.Less(bestD, wake + 4f, "a hex just outside the wake distance");
            army.Deploy(0, hex);
            yield return BattleTestUtil.Wait(2f);
            Assert.IsFalse(guard.Engaged, "a passive squad ignores a unit outside its weapon range plus 2");
            var unit = army.Units[0];
            var close = BattleTestUtil.FreeHexNear(guard.Center + (g.Center(hex) - guard.Center).normalized * (wake - 2f));
            army.OrderMove(unit, close);
            yield return BattleTestUtil.WaitArrive(unit, 8f);
            yield return BattleTestUtil.Wait(0.5f);
            Assert.IsTrue(guard.Engaged, "it wakes once a soldier is within weapon range plus 2");
        }

        // ------------------------------------------------------------ tutorial order on the real layout

        [UnityTest]
        public IEnumerator TutorialTankStepComesBeforeCaptureStep()
        {
            // Steps 1 to 6 are about deploying, moving, panning and advancing; this test is about what comes after.
            yield return LoadLevelOne(true, TutorialSteps.Pick, TutorialSteps.Deploy, TutorialSteps.Unit, TutorialSteps.Move, TutorialSteps.Pan, TutorialSteps.Advance);
            var tut = BattleHUD.Instance.Tutorial;
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(tut.Active, "the tutorial runs on Level 1 with steps 7 and 8 unseen");
            Assert.IsNull(tut.CurrentStep, "nothing to show before enemies are near");
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll(army.spawnPoint + Vector3.forward * 4f);
            var bm = BattleManager.Instance;
            var cp = bm.capturePoints[0];
            var order = new List<string>();
            GameSpeed.Set(2f);
            float end = Time.time + 90f;
            float nextOrder = 0f;
            var taken = new List<HexCoord>();
            while (Time.time < end && !cp.Captured && !order.Contains(TutorialSteps.Capture))
            {
                string cur = tut.CurrentStep;
                if (cur != null && (order.Count == 0 || order[order.Count - 1] != cur)) order.Add(cur);
                if (Time.time >= nextOrder)
                {
                    // Walk every unit at point 1, Tank in front.
                    nextOrder = Time.time + 1.5f;
                    taken.Clear();
                    foreach (var u in army.Units)
                    {
                        var dest = BattleTestUtil.PadHexFree(cp, u, taken);
                        taken.Add(dest);
                        if (u.Hex != dest) army.OrderMove(u, dest);
                    }
                }
                yield return null;
            }
            GameSpeed.Reset();
            Debug.Log("[Test] tutorial steps in order: " + string.Join(", ", order));
            int tank = order.IndexOf(TutorialSteps.Tank), capture = order.IndexOf(TutorialSteps.Capture);
            Assert.GreaterOrEqual(tank, 0, "the Tank step showed");
            Assert.GreaterOrEqual(capture, 0, "the capture step showed");
            Assert.Less(tank, capture, "the Tank step comes before the capture step");
        }

        // The Level 1 captures (level_1_start, level_1_topdown) are written by LevelLayoutTests.
    }
}
