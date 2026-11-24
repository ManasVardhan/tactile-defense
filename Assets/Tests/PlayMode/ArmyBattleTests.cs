using System.Collections;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Attack battles run on the PlayerArmy: the legacy squad is off, defeat means nothing left to fight
    /// with (and waits for a ready meter), reinforcements restore a lost unit, units capture points, and the camera
    /// pans instead of following.</summary>
    public class ArmyBattleTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleTestUtil.Cleanup();
            yield return null;
        }

        static int PlayerSoldiersInScene()
        {
            int n = 0;
            foreach (var t in UnitRegistry.Of(Team.Player)) if (t.kind != TargetKind.Structure) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator AttackBattleUsesArmyNotSquad()
        {
            yield return BattleTestUtil.LoadBattle();
            var army = PlayerArmy.Instance;
            var bm = BattleManager.Instance;
            Assert.IsNotNull(army, "the scene has a PlayerArmy");
            Assert.IsTrue(army.isActiveAndEnabled, "army active in an attack battle");
            Assert.AreSame(army, bm.army);
            Assert.AreSame(army, bm.Force, "the battle rules read the army");
            Assert.IsNotNull(army.roster, "roster wired");
            Assert.IsNotNull(army.soldierConfig, "soldier config wired");
            var squad = SquadController.Instance;
            Assert.IsTrue(squad == null || !squad.gameObject.activeInHierarchy, "legacy squad is off");
            foreach (var sc in Object.FindObjectsByType<SquadController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.IsFalse(sc.gameObject.activeInHierarchy, "every SquadController object is inactive");
            Assert.IsFalse(bm.recorder.enabled, "stroke recorder disabled");
            Assert.IsTrue(BattleCamera.Instance.PanMode, "camera pans in attack battles");
            Assert.AreEqual(0, PlayerSoldiersInScene(), "no player soldiers before a deploy");

            yield return BattleTestUtil.Wait(2f);
            Assert.AreEqual(0, PlayerSoldiersInScene());
            Assert.IsFalse(bm.Over, "nothing deployed is not a defeat");

            var u = army.Deploy(0, BattleTestUtil.FreeHexNear(army.spawnPoint));
            Assert.IsNotNull(u);
            yield return null;
            Assert.AreEqual(3, PlayerSoldiersInScene(), "a deploy spawns the unit's soldiers");
        }

        [UnityTest]
        public IEnumerator HoldingUndeployedUnitsIsNotDefeat()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(3f);
            Assert.IsFalse(BattleManager.Instance.Over);
            Assert.AreEqual(3, BattleTestUtil.Army.ReadyCount);
        }

        [UnityTest]
        public IEnumerator AllUnitsDestroyedIsDefeat()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.6f);
            var bm = BattleManager.Instance;
            var units = BattleTestUtil.DeployAll();
            Assert.AreEqual(3, units.Count);
            yield return null;
            Assert.IsFalse(bm.Over);
            Assert.IsFalse(bm.Meter.Ready);
            foreach (var u in units) BattleTestUtil.KillAll(u);
            float end = Time.time + 1f;
            while (Time.time < end && !bm.Over) yield return null;
            Assert.IsTrue(bm.Over, "every unit destroyed and none ready ends the battle");
            Assert.IsFalse(bm.Result.victory);
            Assert.AreEqual(9, bm.Result.soldiersLost);
            Assert.AreEqual(0, bm.Result.soldiersSurvived);
            Assert.IsFalse(BattleTestUtil.Army.InputEnabled, "input off after the battle");
        }

        [UnityTest]
        public IEnumerator MeterReadyPostponesDefeatAndRestoresAUnit()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.6f);
            var bm = BattleManager.Instance;
            var army = BattleTestUtil.Army;
            bm.Meter.Add(1000f);
            Assert.IsTrue(bm.Meter.Ready);
            int reinforced = 0, reinforcedN = 0;
            System.Action<int> onR = n => { reinforced++; reinforcedN = n; };
            BattleEvents.Reinforced += onR;
            try
            {
                foreach (var u in BattleTestUtil.DeployAll()) BattleTestUtil.KillAll(u);
                yield return BattleTestUtil.Wait(1f);
                Assert.IsFalse(army.CanStillFight);
                Assert.IsFalse(bm.Over, "a ready meter postpones the defeat");

                Assert.IsTrue(bm.CallReinforcements(), "reinforcements restore a destroyed unit");
                Assert.AreEqual(1, army.ReadyCount);
                Assert.IsFalse(bm.Meter.Ready, "the charge is used");
                Assert.AreEqual(1, reinforced);
                Assert.AreEqual(3, reinforcedN);
                yield return BattleTestUtil.Wait(0.6f);
                Assert.IsFalse(bm.Over, "a ready entry can still fight");
                Assert.IsNotNull(army.Deploy(2, BattleTestUtil.FreeHexNear(army.spawnPoint)), "the restored entry deploys");
            }
            finally { BattleEvents.Reinforced -= onR; }
        }

        [UnityTest]
        public IEnumerator CallReinforcementsWithNothingLostKeepsCharge()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.3f);
            var bm = BattleManager.Instance;
            var army = BattleTestUtil.Army;
            Assert.IsFalse(bm.CallReinforcements(), "meter starts empty");
            bm.Meter.Add(1000f);
            Assert.IsFalse(bm.CallReinforcements(), "nothing destroyed: nothing to restore");
            Assert.IsTrue(bm.Meter.Ready, "the charge is kept");
            army.Deploy(0, BattleTestUtil.FreeHexNear(army.spawnPoint));
            Assert.IsFalse(bm.CallReinforcements(), "a deployed, living unit is not lost");
            Assert.IsTrue(bm.Meter.Ready);
            Assert.AreEqual(2, army.ReadyCount);
            Assert.AreEqual(0, bm.Meter.Calls);
        }

        [UnityTest]
        public IEnumerator DeployedUnitCapturesPoint()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var bm = BattleManager.Instance;
            var army = BattleTestUtil.Army;
            var cp = bm.capturePoints[0];
            Assert.IsTrue(cp.Active);
            Assert.AreEqual(0f, cp.Progress);

            var hex = BattleTestUtil.PadHex(cp);
            var unit = army.Deploy(0, hex);
            Assert.IsNotNull(unit);
            yield return BattleTestUtil.Wait(1.5f);
            Debug.Log($"[Test] unit capture progress {cp.Progress:0.00} inside={cp.PlayersInside} contested={cp.Contested}");
            Assert.Greater(cp.PlayersInside, 0, "the unit's soldiers stand on the pad");
            Assert.Greater(cp.Progress, 0.15f, "capture progress rises");
        }

        /// <summary>Hex center distance from the pad center up to which a hex counts as overlapping the pad.</summary>
        static float PadReach(HexGrid g) => BattleManager.Instance.battleConfig.captureRadius + g.OuterRadius * 0.5f;

        static float PadDistance(HexGrid g, HexCoord c, CapturePoint cp)
        {
            Vector3 d = g.Center(c) - cp.transform.position; d.y = 0f;
            return d.magnitude;
        }

        [UnityTest]
        public IEnumerator UnitCanStandOnCapturePad()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            var cp = BattleManager.Instance.capturePoints[0];
            float reach = PadReach(g);

            // Every hex overlapping the pad is open, including the ones around the flag pole at the very center.
            HexCoord center = default, other = default;
            float bestCenter = float.MaxValue, bestOther = float.MaxValue;
            int overlapping = 0;
            foreach (var c in g.Cells)
            {
                float d = PadDistance(g, c, cp);
                if (d > reach) continue;
                overlapping++;
                Assert.IsFalse(g.IsBlocked(c), c + " overlaps the pad (" + d.ToString("0.00") + " from its center) and is not blocked");
                if (d < bestCenter) { bestOther = bestCenter; other = center; bestCenter = d; center = c; }
                else if (d < bestOther) { bestOther = d; other = c; }
            }
            Assert.Greater(overlapping, 6, "several hexes overlap the pad");
            Assert.Less(bestCenter, g.OuterRadius * 1.01f, "a hex touches the pad center");

            var onPad = army.Deploy(0, center);
            Assert.IsNotNull(onPad, "a unit deploys on the hex at the pad center");

            var walker = army.Deploy(1, BattleTestUtil.FreeHexNear(cp.transform.position + new Vector3(0f, 0f, -9f), center, other));
            Assert.IsNotNull(walker);
            Assert.IsTrue(army.OrderMove(walker, other), "a unit can be ordered onto another pad hex");
            yield return BattleTestUtil.WaitArrive(walker, 12f);
            Assert.IsFalse(walker.IsMoving, "the unit arrives on the pad");
            Assert.AreEqual(other, walker.Hex);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual(6, BattleManager.Instance.capturePoints[0].PlayersInside, "both units count on the pad");
        }

        /// <summary>Deploys entry 0 on the overlapping pad hex farthest from the pad center, kills soldiers down to
        /// 'alive', and returns (players counted inside, capture progress per second over two seconds).</summary>
        static IEnumerator MeasurePadCapture(int alive, float[] result)
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            var cp = BattleManager.Instance.capturePoints[0];
            float reach = PadReach(g);
            HexCoord far = default;
            float farD = -1f;
            foreach (var c in g.Cells)
            {
                float d = PadDistance(g, c, cp);
                if (d <= reach && d > farD && g.IsFree(c)) { farD = d; far = c; }
            }
            Assert.Greater(farD, BattleManager.Instance.battleConfig.captureRadius, "a pad hex whose center lies past the pad radius");
            var unit = army.Deploy(0, far);
            Assert.IsNotNull(unit);
            for (int i = 0; i < unit.Soldiers.Count && unit.AliveCount > alive; i++)
                unit.Soldiers[i].Health.Kill(TeamColorId.Teal);
            Assert.AreEqual(alive, unit.AliveCount);
            yield return BattleTestUtil.Wait(0.3f);
            int outside = 0;
            foreach (var s in unit.Soldiers)
            {
                if (s == null || !s.IsAlive) continue;
                Vector3 d = s.transform.position - cp.transform.position; d.y = 0f;
                if (d.magnitude > BattleManager.Instance.battleConfig.captureRadius) outside++;
            }
            Assert.Greater(outside, 0, "some soldiers stand past the pad radius, so the count comes from the hex, not the circle");
            float p0 = cp.Progress, t0 = Time.time;
            yield return BattleTestUtil.Wait(2f);
            float rate = (cp.Progress - p0) / (Time.time - t0);
            Debug.Log($"[Test] pad hex {far} at {farD:0.00}: alive {alive}, outside the radius {outside}, inside={cp.PlayersInside}, progress {p0:0.000} -> {cp.Progress:0.000}, rate {rate:0.0000}/s");
            result[0] = cp.PlayersInside;
            result[1] = rate;
            result[2] = cp.Progress;
        }

        [UnityTest]
        public IEnumerator UnitOnPadCountsAllThreeSoldiers()
        {
            var full = new float[3];
            yield return MeasurePadCapture(3, full);
            Assert.AreEqual(3, (int)full[0], "every living soldier of a unit on a pad hex counts");
            BattleTestUtil.Cleanup();
            var one = new float[3];
            yield return MeasurePadCapture(1, one);
            Assert.AreEqual(1, (int)one[0]);
            Assert.Greater(full[1], one[1] * 1.04f, "three soldiers capture faster than one");
            Assert.Greater(full[2], one[2], "and get further in two seconds");
        }

        [UnityTest]
        public IEnumerator UnitOnEdgePadHexCaptures()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            var cp = BattleManager.Instance.capturePoints[0];
            float radius = BattleManager.Instance.battleConfig.captureRadius;
            float reach = PadReach(g);
            // The outermost pad hex on the near side (the side a unit walks in from).
            HexCoord edge = default;
            float edgeD = -1f;
            foreach (var c in g.Cells)
            {
                float d = PadDistance(g, c, cp);
                if (d <= reach && d > edgeD && g.IsFree(c) && g.Center(c).z < cp.transform.position.z) { edgeD = d; edge = c; }
            }
            Assert.Greater(edgeD, radius, "the edge hex's center lies past the pad radius");
            var unit = army.Deploy(0, BattleTestUtil.FreeHexNear(g.Center(edge) + new Vector3(0f, 0f, -9f)));
            Assert.IsNotNull(unit);
            yield return null;
            Assert.IsTrue(army.OrderMove(unit, edge), "ordered onto the edge pad hex");

            // Walking on: once a soldier is within the pad radius plus one hex, every soldier counts. Checked on every
            // walking frame through the counting rule itself (the point rescans every 0.1 s, too coarse for a short walk).
            float near = radius + g.OuterRadius;
            int closeFrames = 0, countedFrames = 0;
            float end = Time.time + 12f;
            while (Time.time < end && unit.IsMoving)
            {
                bool close = false;
                foreach (var s in unit.Soldiers)
                {
                    if (s == null || !s.IsAlive) continue;
                    Vector3 d = s.transform.position - cp.transform.position; d.y = 0f;
                    if (d.magnitude <= near) close = true;
                }
                if (close)
                {
                    closeFrames++;
                    if (army.PadCount(cp, out _) == unit.AliveCount) countedFrames++;
                }
                yield return null;
            }
            Debug.Log($"[Test] edge pad hex {edge} at {edgeD:0.00}: close while walking on {closeFrames} frames, counted in full {countedFrames}, inside now {cp.PlayersInside}, progress {cp.Progress:0.000}");
            Assert.Greater(closeFrames, 0, "the unit walked onto the pad (a soldier came within the radius plus a hex while moving)");
            Assert.AreEqual(closeFrames, countedFrames, "a unit walking onto a pad hex counts all its soldiers on every such frame");

            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual(3, cp.PlayersInside, "resting on the edge pad hex counts all three soldiers");
            float p0 = cp.Progress;
            yield return BattleTestUtil.Wait(1.5f);
            Assert.IsFalse(cp.Contested);
            Assert.Greater(cp.Progress, p0 + 0.05f, "the capture makes progress from the edge hex");
        }

        /// <summary>Capture progress over two seconds of a fresh unit deployed on 'hex' (from zero, after the first
        /// rescan). The unit is killed afterwards, which frees the hex.</summary>
        static IEnumerator ProgressFrom(HexCoord hex, CapturePoint cp, float[] result)
        {
            var army = BattleTestUtil.Army;
            int entry = -1;
            for (int i = 0; i < army.Squad.Count; i++) if (army.Squad[i].state == EntryState.Ready) { entry = i; break; }
            Assert.GreaterOrEqual(entry, 0, "a ready entry");
            var u = army.Deploy(entry, hex);
            Assert.IsNotNull(u, "deploy on " + hex);
            yield return BattleTestUtil.Wait(0.25f);
            // Two seconds at the full rate would capture the point, so measure in two one-second halves.
            float made = 0f, took = 0f;
            for (int half = 0; half < 2; half++)
            {
                cp.SetProgress(0f);
                float t0 = Time.time;
                yield return BattleTestUtil.Wait(1f);
                Assert.Less(cp.Progress, 1f, "not captured mid measure");
                made += cp.Progress;
                took += Time.time - t0;
            }
            result[0] = made * 2f / took;
            result[1] = cp.PlayersInside;
            BattleTestUtil.KillAll(u);
            yield return BattleTestUtil.Wait(0.15f);
        }

        /// <summary>Width of a soldier resting face forward (+z): the x extent of its posed mesh vertices (skinned
        /// meshes baked in their current pose), gun included.</summary>
        static float SoldierWidth(Units.Soldier s)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            var baked = new Mesh();
            foreach (var r in s.bodyRenderers)
            {
                if (r == null) continue;
                Mesh m;
                if (r is SkinnedMeshRenderer smr) { smr.BakeMesh(baked); m = baked; }
                else { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; m = mf.sharedMesh; }
                // Baked vertices are already scaled; place them by the renderer's position and rotation only.
                bool skinned = r is SkinnedMeshRenderer;
                foreach (var v in m.vertices)
                {
                    float x = skinned ? (r.transform.position + r.transform.rotation * v).x : r.transform.TransformPoint(v).x;
                    lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x);
                }
            }
            Object.Destroy(baked);
            Assert.Less(lo, hi, s.name + " has body vertices");
            return hi - lo;
        }

        [UnityTest]
        public IEnumerator SoldiersInAUnitDoNotOverlap()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            var army = BattleTestUtil.Army;
            var types = new[] { UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher };
            army.SetSquad(types);
            yield return null;
            var used = new System.Collections.Generic.List<HexCoord>();
            var units = new System.Collections.Generic.List<PlayerUnit>();
            for (int i = 0; i < types.Length; i++)
            {
                var h = BattleTestUtil.FreeHexNear(army.spawnPoint + new Vector3(-8f + i * 3.2f, 0f, 6f), used.ToArray());
                used.Add(h);
                var u = army.Deploy(i, h);
                Assert.IsNotNull(u, "deploy " + types[i]);
                units.Add(u);
            }
            yield return BattleTestUtil.Wait(1.5f);
            foreach (var u in units)
            {
                Assert.IsFalse(u.IsMoving, u.Type.id + " rests");
                var list = u.Soldiers;
                Assert.AreEqual(3, list.Count);
                float width = 0f;
                foreach (var s in list) width = Mathf.Max(width, SoldierWidth(s));
                float minGap = float.MaxValue;
                for (int a = 0; a < list.Count; a++)
                    for (int b = a + 1; b < list.Count; b++)
                    {
                        Vector3 d = list[a].transform.position - list[b].transform.position; d.y = 0f;
                        minGap = Mathf.Min(minGap, d.magnitude);
                    }
                Debug.Log($"[Test] {u.Type.id}: soldier width {width:0.00}, closest centres {minGap:0.00}");
                Assert.GreaterOrEqual(minGap, width, u.Type.id + ": the resting soldiers' centres are at least one soldier width apart");
            }
            // Types keep their size differences: Tank bulkier, Sniper slighter than the Attacker.
            float wa = SoldierWidth(units[0].Soldiers[0]), wt = SoldierWidth(units[1].Soldiers[0]), ws = SoldierWidth(units[3].Soldiers[0]);
            Assert.Greater(wt, wa * 1.2f, "the Tank is clearly bigger");
            Assert.Less(ws, wa * 0.97f, "the Sniper is a little smaller");
            // Every soldier is about 20 percent bigger than its first-pass size.
            Assert.AreEqual(1.2f, Units.Soldier.SizeScale, 1e-4f);
            Assert.AreEqual(0.9f * Units.Soldier.SizeScale, units[0].Soldiers[0].modelRoot.localScale.x, 1e-3f, "the Attacker's model scale");
        }

        [UnityTest]
        public IEnumerator UnitOnAnyTintedPadHexCapturesAtFullRate()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            var army = BattleTestUtil.Army;
            var many = new UnitTypeId[40];
            for (int i = 0; i < many.Length; i++) many[i] = UnitTypeId.Attacker;
            army.SetSquad(many);
            yield return null;
            var g = army.Grid;
            var bm = BattleManager.Instance;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var r = new float[2];
            for (int p = 0; p < 2; p++)
            {
                var cp = bm.capturePoints[p];
                if (p == 1) { bm.capturePoints[0].ForceCapture(); yield return null; yield return null; }
                Assert.IsTrue(cp.Active && !cp.Captured, "point " + (p + 1) + " is the active point");
                var tinted = new System.Collections.Generic.List<HexCoord>();
                foreach (var c in g.Cells)
                {
                    if (!army.IsPadHex(c, cp)) continue;
                    Assert.AreEqual(HexOverlay.HexStyle.Pad, overlay.StyleOf(c), c + " is a pad hex, so the overlay reports it as a pad spot");
                    if (g.IsFree(c)) tinted.Add(c);
                }
                Assert.Greater(tinted.Count, 6, "point " + (p + 1) + " has tinted pad hexes");
                yield return ProgressFrom(BattleTestUtil.PadHex(cp), cp, r);
                float center = r[0];
                Debug.Log($"[Test] point {p + 1}: center rate {center:0.000} per 2 s, inside {r[1]}, {tinted.Count} tinted free hexes");
                Assert.AreEqual(3, (int)r[1]);
                foreach (var h in tinted)
                {
                    yield return ProgressFrom(h, cp, r);
                    Vector3 d = g.Center(h) - cp.transform.position; d.y = 0f;
                    Debug.Log($"[Test] point {p + 1} hex {h} at {d.magnitude:0.00}: progress {r[0]:0.000} in 2 s, inside {r[1]}");
                    Assert.AreEqual(3, (int)r[1], h + " counts all three soldiers");
                    Assert.AreEqual(center, r[0], center * 0.1f, h + " captures at the full rate");
                }
            }
        }

        [UnityTest]
        public IEnumerator UnitWithASoldierInsideTheDrawnRingCounts()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            var cp = BattleManager.Instance.capturePoints[0];
            Vector3 c0 = cp.transform.position; c0.y = 0f;
            Assert.AreEqual(cp.Radius * CapturePoint.RingRadiusFactor * 2f, cp.progressRing.transform.localScale.x, 1e-3f,
                "the drawn ring's size comes from DrawnRadius");

            // A unit passes by the pad: it walks from a hex left of the pad to one right of it, both off the pad, on a
            // line that crosses the drawn ring. While a soldier is inside the ring the whole unit counts.
            HexCoord from = BattleTestUtil.FreeHexNear(c0 + new Vector3(-7f, 0f, -1.5f));
            HexCoord to = BattleTestUtil.FreeHexNear(c0 + new Vector3(7f, 0f, -1.5f), from);
            Assert.IsFalse(army.IsPadHex(from, cp) || army.IsPadHex(to, cp), "both ends are off the pad");
            var u = army.Deploy(0, from);
            Assert.IsNotNull(u);
            yield return null;
            Assert.IsTrue(army.OrderMove(u, to));
            int inside = 0, counted = 0, scanned = 0;
            float end = Time.time + 10f;
            while (Time.time < end && u.IsMoving)
            {
                bool any = false;
                foreach (var s in u.Soldiers)
                {
                    if (s == null || !s.IsAlive) continue;
                    Vector3 d = s.transform.position - c0; d.y = 0f;
                    if (d.magnitude <= cp.DrawnRadius) any = true;
                }
                if (any)
                {
                    inside++;
                    if (army.PadCount(cp, out _) == u.AliveCount) counted++;
                    if (cp.PlayersInside == u.AliveCount) scanned++;
                }
                yield return null;
            }
            Debug.Log($"[Test] passing unit: {inside} frames with a soldier inside the drawn ring, counted in full {counted}, point saw 3 on {scanned}");
            Assert.Greater(inside, 3, "the route crosses the drawn ring");
            Assert.AreEqual(inside, counted, "a soldier inside the drawn ring makes the whole unit count");
            Assert.Greater(scanned, 0, "and the capture point counts it");

            // Any hex with a slot inside the drawn ring is a pad hex (tinted), even past the half-hex overlap rule.
            float reach = cp.Radius + g.OuterRadius * 0.5f;
            foreach (var c in g.Cells)
            {
                if (g.IsBlocked(c)) continue;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 d = g.SlotPosition(c, k) - c0; d.y = 0f;
                    if (d.magnitude <= cp.DrawnRadius) Assert.IsTrue(army.IsPadHex(c, cp), c + " has a slot inside the drawn ring");
                }
                Vector3 dc = g.Center(c) - c0; dc.y = 0f;
                if (dc.magnitude <= reach) Assert.IsTrue(army.IsPadHex(c, cp), c + " overlaps the pad");
            }
        }

        [UnityTest]
        public IEnumerator UntintedHexNextToPadDoesNotCount()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return null;
            var army = BattleTestUtil.Army;
            var g = army.Grid;
            var cp = BattleManager.Instance.capturePoints[0];
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            Vector3 c0 = cp.transform.position; c0.y = 0f;
            // The free untinted hex nearest the pad center that touches a tinted hex.
            HexCoord best = default;
            float bestD = float.MaxValue;
            var near = new System.Collections.Generic.List<HexCoord>(6);
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c) || army.IsPadHex(c, cp)) continue;
                near.Clear();
                g.Neighbors(c, near);
                bool touches = false;
                foreach (var n in near) if (army.IsPadHex(n, cp)) touches = true;
                if (!touches) continue;
                Vector3 d = g.Center(c) - c0; d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "an untinted hex next to the pad");
            Assert.AreNotEqual(HexOverlay.HexStyle.Pad, overlay.StyleOf(best), "it is not a pad spot");
            var u = army.Deploy(0, best);
            Assert.IsNotNull(u);
            yield return BattleTestUtil.Wait(1f);
            Debug.Log($"[Test] untinted hex {best} at {bestD:0.00}: inside {cp.PlayersInside}, progress {cp.Progress:0.000}");
            Assert.AreEqual(0, cp.PlayersInside, "a unit next to the pad does not count");
            Assert.AreEqual(0f, cp.Progress, "and makes no progress");
            foreach (var s in u.Soldiers)
            {
                Vector3 d = s.transform.position - c0; d.y = 0f;
                Assert.Greater(d.magnitude, cp.DrawnRadius, "its soldiers stand outside the drawn ring");
            }
        }

        static bool GroundPoint(Camera cam, Vector2 screen, out Vector3 p)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            p = default;
            if (!plane.Raycast(ray, out float t)) return false;
            p = ray.GetPoint(t);
            return true;
        }

        [UnityTest]
        public IEnumerator CameraPansAndClamps()
        {
            yield return BattleTestUtil.LoadBattle();
            var bc = BattleCamera.Instance;
            var army = BattleTestUtil.Army;
            var cfg = bc.config;
            Assert.IsTrue(bc.PanMode);
            // The start leans along the map's path from the spawn (MapPath), not along an axis.
            Vector3 start = army.spawnPoint + MapPath.Forward(army.spawnPoint) * cfg.forwardBias;
            Assert.AreEqual(Mathf.Clamp(start.z, cfg.focusMin.y, cfg.focusMax.y), bc.Focus.z, 0.01f, "starts on the army's spawn plus the forward bias");
            Assert.AreEqual(Mathf.Clamp(start.x, cfg.focusMin.x, cfg.focusMax.x), bc.Focus.x, 0.01f);

            // No follow: a deployed unit walking away does not drag the camera.
            Vector3 f0 = bc.Focus;
            yield return BattleTestUtil.Wait(0.5f);
            Assert.AreEqual(f0, bc.Focus, "the camera holds still without input");

            bc.Pan(new Vector2(100000f, 100000f));
            Assert.That(bc.Focus.x, Is.InRange(cfg.focusMin.x - 1e-3f, cfg.focusMax.x + 1e-3f));
            Assert.That(bc.Focus.z, Is.InRange(cfg.focusMin.y - 1e-3f, cfg.focusMax.y + 1e-3f));
            bc.Pan(new Vector2(-100000f, -100000f));
            Assert.That(bc.Focus.x, Is.InRange(cfg.focusMin.x - 1e-3f, cfg.focusMax.x + 1e-3f));
            Assert.That(bc.Focus.z, Is.InRange(cfg.focusMin.y - 1e-3f, cfg.focusMax.y + 1e-3f));
            Assert.AreEqual(cfg.focusMax.y, bc.Focus.z, 1e-3f, "dragging down far pans to the far end");

            // A small drag keeps the ground point under the pointer.
            bc.SnapTo(new Vector3(0f, 0f, 50f));
            var cam = bc.Cam;
            Vector2 p0 = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);
            Vector2 delta = new Vector2(20f, -60f);
            Assert.IsTrue(GroundPoint(cam, p0, out Vector3 w0));
            Vector3 before = bc.Focus;
            bc.Pan(delta);
            Assert.Greater(bc.Focus.z, before.z + 0.5f, "dragging down moves the view up the map");
            Assert.Less(bc.Focus.x, before.x, "dragging right moves the view left");
            Assert.IsTrue(GroundPoint(cam, p0 + delta, out Vector3 w1));
            Assert.Less(Vector3.Distance(w0, w1), 0.05f, "the ground point stays under the pointer");
        }
    }
}
