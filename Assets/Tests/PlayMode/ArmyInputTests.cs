using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TactileWars.Tests
{
    /// <summary>Unit controls in attack battles through the simulated pointer: arm and click to deploy (also with a
    /// press and release inside one frame), click to select and move (re-clicking keeps the selection), drag to pan,
    /// the round ground markers for the hidden grid (hover validity and snapping, the capture ring glow, no grid drawn)
    /// and selection ring, refused orders, and input switched off
    /// once the battle is over.</summary>
    public class ArmyInputTests
    {
        Func<Vector2, bool> savedHitTest;
        bool hitTestSaved;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hitTestSaved) PointerInput.UIHitTest = savedHitTest;
            hitTestSaved = false;
            BattleTestUtil.Cleanup();
            yield return null;
        }

        static PlayerArmy Army => BattleTestUtil.Army;
        static ArmyInput Input => ArmyInput.Instance;

        IEnumerator Setup()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsNotNull(Input, "attack battles have unit input");
            Assert.IsTrue(Input.isActiveAndEnabled);
            // The HUD's hit test would swallow taps that land under its panels; tests aim at the map.
            savedHitTest = PointerInput.UIHitTest;
            hitTestSaved = true;
            PointerInput.UIHitTest = null;
        }

        static void AssertOnScreen(Vector3 world, string what)
        {
            Vector2 s = BattleTestUtil.ToScreen(world);
            Assert.That(s.x, Is.InRange(30f, BattleTestUtil.W - 30f), what + " on screen (x)");
            Assert.That(s.y, Is.InRange(30f, BattleTestUtil.H - 30f), what + " on screen (y)");
        }

        static Vector3 Center(HexCoord c) => Army.Grid.Center(c);

        static HexCoord FreeHex(Vector3 near, params HexCoord[] avoid)
        {
            var h = BattleTestUtil.FreeHexNear(near, avoid);
            AssertOnScreen(Center(h), h.ToString());
            return h;
        }

        /// <summary>A blocked, in-bounds hex on screen, nearest the view center.</summary>
        static HexCoord BlockedHexOnScreen()
        {
            var g = Army.Grid;
            Vector3 focus = BattleCamera.Instance.Focus;
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsBlocked(c)) continue;
                Vector2 s = BattleTestUtil.ToScreen(g.Center(c));
                if (s.x < 60f || s.x > BattleTestUtil.W - 60f || s.y < 60f || s.y > BattleTestUtil.H - 60f) continue;
                float d = (g.Center(c) - focus).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "a blocked hex on screen");
            return best;
        }

        /// <summary>A free hex exactly 'steps' from 'from' with a nav route, on screen.</summary>
        static HexCoord FreeHexAtDistance(HexCoord from, int steps)
        {
            var g = Army.Grid;
            var path = new List<Vector3>();
            foreach (var c in g.Cells)
            {
                if (HexGrid.Distance(from, c) != steps || !g.IsFree(c)) continue;
                if (UnitRegistry.CountWithin(Team.Enemy, g.Center(c), g.OuterRadius * 3f) > 0) continue;
                Vector2 s = BattleTestUtil.ToScreen(g.Center(c));
                if (s.x < 60f || s.x > BattleTestUtil.W - 60f || s.y < 60f || s.y > BattleTestUtil.H - 60f) continue;
                if (!Army.Nav.FindPath(g.Center(from), g.Center(c), path)) continue;
                return c;
            }
            Assert.Fail("no free hex " + steps + " away from " + from);
            return default;
        }

        [UnityTest]
        public IEnumerator ArmThenTapDeploys()
        {
            yield return Setup();
            var hex = FreeHex(Army.spawnPoint + Vector3.forward * 4f);
            Input.Arm(0);
            Assert.AreEqual(0, Input.ArmedEntry);
            yield return BattleTestUtil.Tap(Center(hex));
            Assert.AreEqual(-1, Input.ArmedEntry, "deploying disarms");
            var unit = Army.UnitAt(hex);
            Assert.IsNotNull(unit, "a unit stands on the tapped hex");
            Assert.AreEqual(hex, unit.Hex);
            Assert.AreEqual(EntryState.Deployed, Army.Squad[0].state);
            Assert.AreSame(unit, Army.Squad[0].unit, "the armed entry deployed");
            Assert.AreEqual(1, Army.Units.Count);
        }

        [UnityTest]
        public IEnumerator TapOnBlockedOccupiedOrOffMapWhileArmedIsRejected()
        {
            yield return Setup();
            var army = Army;
            var other = army.Deploy(1, FreeHex(army.spawnPoint));
            Assert.IsNotNull(other);
            var rejected = new List<HexCoord>();
            Action<HexCoord> onRejected = h => rejected.Add(h);
            army.OrderRejected += onRejected;
            try
            {
                Input.Arm(0);
                Assert.AreEqual(0, Input.ArmedEntry);

                var blocked = BlockedHexOnScreen();
                yield return BattleTestUtil.Tap(Center(blocked));
                Assert.AreEqual(1, rejected.Count, "a blocked hex is refused");
                Assert.AreEqual(blocked, rejected[0]);
                Assert.AreEqual(0, Input.ArmedEntry, "still armed");

                yield return BattleTestUtil.Tap(Center(other.Hex));
                Assert.AreEqual(2, rejected.Count, "an occupied hex is refused");
                Assert.AreEqual(other.Hex, rejected[1]);
                Assert.AreEqual(0, Input.ArmedEntry, "still armed");
                Assert.IsNull(army.Selected, "an armed tap on a unit does not select it");

                var gc = BattleContext.Config;
                Vector3 off = new Vector3(gc.mapMax.x + 3f, 0f, army.spawnPoint.z + 4f);
                Assert.IsFalse(army.Grid.TryWorldToHex(off, out _), "the point is off the map");
                AssertOnScreen(off, "off map point");
                yield return BattleTestUtil.Tap(off);
                Assert.AreEqual(3, rejected.Count, "a tap off the map is refused too");

                Assert.AreEqual(0, Input.ArmedEntry, "armed entry unchanged");
                Assert.AreEqual(EntryState.Ready, army.Squad[0].state, "entry still ready");
                Assert.AreEqual(1, army.Units.Count, "no new unit");
                LogAssert.NoUnexpectedReceived();
            }
            finally { army.OrderRejected -= onRejected; }
        }

        [UnityTest]
        public IEnumerator SameFrameClickIsSeenAsPressAndRelease()
        {
            // Drives the simulated source only; the device path (wasPressedThisFrame / wasReleasedThisFrame) is
            // covered by the browser playthrough check.
            var sim = new SimulatedPointerSource();
            PointerInput.Source = sim;
            yield return null;
            Assert.IsFalse(PointerInput.PressedThisFrame);
            Assert.IsFalse(PointerInput.ReleasedThisFrame);
            var at = new Vector2(321f, 234f);
            sim.Click(at);
            yield return null;
            Assert.IsTrue(PointerInput.PressedThisFrame, "the press is seen");
            Assert.IsTrue(PointerInput.ReleasedThisFrame, "and so is the release, in the same frame");
            Assert.IsFalse(PointerInput.IsDown, "nothing is held after a quick click");
            Assert.AreEqual(at, PointerInput.Position, "at the click position");
            yield return null;
            Assert.IsFalse(PointerInput.PressedThisFrame, "edges last one frame");
            Assert.IsFalse(PointerInput.ReleasedThisFrame);

            // Held presses still give their edges a frame apart.
            sim.Down = true;
            yield return null;
            Assert.IsTrue(PointerInput.PressedThisFrame);
            Assert.IsFalse(PointerInput.ReleasedThisFrame);
            Assert.IsTrue(PointerInput.IsDown);
            yield return null;
            Assert.IsFalse(PointerInput.PressedThisFrame);
            sim.Down = false;
            yield return null;
            Assert.IsTrue(PointerInput.ReleasedThisFrame);
            Assert.IsFalse(PointerInput.PressedThisFrame);
        }

        [UnityTest]
        public IEnumerator SameFrameClickDeploysArmedUnit()
        {
            yield return Setup();
            var army = Army;
            var hex = FreeHex(army.spawnPoint + Vector3.forward * 4f);
            Input.Arm(0);
            yield return BattleTestUtil.QuickTap(Center(hex));
            Assert.AreEqual(-1, Input.ArmedEntry, "a quick click deploys and disarms");
            var unit = army.UnitAt(hex);
            Assert.IsNotNull(unit, "a unit stands on the clicked hex");
            Assert.AreSame(unit, army.Squad[0].unit);

            // Quick clicks select and order too.
            yield return BattleTestUtil.QuickTap(Center(hex));
            Assert.AreSame(unit, army.Selected, "a quick click selects");
            var dest = FreeHexAtDistance(hex, 3);
            yield return BattleTestUtil.QuickTap(Center(dest));
            Assert.AreEqual(dest, unit.Destination, "a quick click orders the move");
            Assert.IsTrue(unit.IsMoving);
        }

        [UnityTest]
        public IEnumerator TapUnitSelectsTapHexMovesTapAgainKeepsSelection()
        {
            yield return Setup();
            var army = Army;
            var start = FreeHex(army.spawnPoint + Vector3.forward * 3f);
            var unit = army.Deploy(0, start);
            Assert.IsNotNull(unit);
            yield return null;

            yield return BattleTestUtil.Tap(Center(start));
            Assert.AreSame(unit, army.Selected, "tapping a unit selects it");
            Assert.IsTrue(unit.Selected);

            var dest = FreeHexAtDistance(start, 3);
            yield return BattleTestUtil.Tap(Center(dest));
            Assert.AreEqual(dest, unit.Destination, "tapping a free hex orders the move");
            Assert.IsTrue(unit.IsMoving);
            Assert.AreSame(unit, army.Selected, "still selected after the order");

            yield return BattleTestUtil.WaitArrive(unit, 10f);
            Assert.IsFalse(unit.IsMoving, "arrived");
            yield return BattleTestUtil.Tap(Center(dest));
            Assert.AreSame(unit, army.Selected, "tapping the selected unit keeps it selected");
            Assert.IsTrue(unit.Selected);
            Assert.AreEqual(dest, unit.Destination, "re-clicking does not move it");
            Assert.IsFalse(unit.IsMoving);

            // So "click the unit, click a hex" works again for the next order.
            var next = FreeHexAtDistance(dest, 2);
            yield return BattleTestUtil.Tap(Center(next));
            Assert.AreEqual(next, unit.Destination, "the next order after a re-click is obeyed");

            // Escape is the way to deselect (not simulated here); the army API clears it the same way.
            army.Select(null);
            Assert.IsNull(army.Selected);
            Assert.IsFalse(unit.Selected);
        }

        [UnityTest]
        public IEnumerator TapOtherUnitSwitchesSelection()
        {
            yield return Setup();
            var army = Army;
            var ha = FreeHex(army.spawnPoint);
            var hb = FreeHex(army.spawnPoint + Vector3.forward * 5f, ha);
            var a = army.Deploy(0, ha);
            var b = army.Deploy(1, hb);
            yield return null;
            yield return BattleTestUtil.Tap(Center(ha));
            Assert.AreSame(a, army.Selected);
            yield return BattleTestUtil.Tap(Center(hb));
            Assert.AreSame(b, army.Selected, "tapping another unit selects it");
            Assert.IsFalse(a.Selected);
            Assert.IsTrue(b.Selected);
            Assert.AreEqual(ha, a.Hex, "switching does not move either unit");
            Assert.AreEqual(hb, b.Hex);
            Assert.IsFalse(a.IsMoving || b.IsMoving);

            // A tap right next to one of its soldiers picks that unit.
            Vector3 soldier = a.Soldiers[0].transform.position;
            yield return BattleTestUtil.Tap(soldier + new Vector3(0.3f, 0f, 0f));
            Assert.AreSame(a, army.Selected, "a tap within reach of a soldier selects its unit");

            // A tap on the top of a soldier's helmet (its ground point lies well behind its feet) picks that unit.
            yield return BattleTestUtil.Tap(HelmetTop(b.Soldiers[1]));
            Assert.AreSame(b, army.Selected, "a tap on a helmet selects its unit");
            Assert.IsFalse(a.IsMoving, "and does not move the previously selected unit");
        }

        /// <summary>A point just under the top of the soldier as drawn (its renderers' bounds), above its feet.</summary>
        static Vector3 HelmetTop(TactileWars.Units.Soldier s)
        {
            Bounds b = default;
            bool any = false;
            foreach (var r in s.GetComponentsInChildren<Renderer>())
            {
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || r.transform == s.blobShadow || (s.blobShadow != null && r.transform.IsChildOf(s.blobShadow))) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Assert.IsTrue(any, "the soldier is drawn");
            Vector3 feet = s.transform.position;
            float top = b.max.y - feet.y;
            Debug.Log($"[Test] {s.name} drawn height {top:F2} (pick box {ArmyInput.SoldierBodyHeight * TactileWars.Units.Soldier.SizeScale * s.transform.lossyScale.y:F2})");
            return new Vector3(feet.x, feet.y + top * 0.95f, feet.z);
        }

        /// <summary>True when any living soldier of the unit covers the screen point as drawn.</summary>
        static bool Covers(PlayerUnit u, Vector3 world)
        {
            Vector2 sp = BattleTestUtil.ToScreen(world);
            foreach (var s in u.Soldiers)
                if (s != null && s.IsAlive && ArmyInput.BodyCovers(BattleTestUtil.Cam, s.transform, sp, out _)) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator ClickingAnotherUnitsBodySelectsItInsteadOfMoving()
        {
            yield return Setup();
            var army = Army;
            var cam = BattleTestUtil.Cam;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            var hx = FreeHex(army.spawnPoint);
            var hy = FreeHex(army.spawnPoint + Vector3.right * 5f, hx);
            // The Tank stands directly behind Y as seen from the camera.
            var ht = FreeHex(Center(hy) + fwd * (army.Grid.OuterRadius * 3.5f), hx, hy);
            var x = army.Deploy(0, hx);
            var y = army.Deploy(1, hy);
            var t = army.Deploy(2, ht);
            Assert.IsNotNull(x); Assert.IsNotNull(y); Assert.IsNotNull(t);
            Assert.AreEqual(UnitTypeId.Tank, t.Type.id);
            yield return BattleTestUtil.Wait(0.2f);

            yield return BattleTestUtil.Tap(HelmetTop(x.Soldiers[0]));
            Assert.AreSame(x, army.Selected, "a helmet tap selects X");
            var destX = x.Destination;

            Vector3 helmY = HelmetTop(y.Soldiers[0]);
            Assert.IsTrue(PointerInput.ScreenToGround(cam, BattleTestUtil.ToScreen(helmY), out Vector3 groundY));
            Debug.Log($"[Test] helmet ground point {(groundY - y.Soldiers[0].transform.position).magnitude:F2} from the feet");
            yield return BattleTestUtil.Tap(helmY);
            Assert.AreSame(y, army.Selected, "tapping Y's helmet selects Y");
            Assert.AreEqual(destX, x.Destination, "X got no move order");
            Assert.IsFalse(x.IsMoving);

            army.Select(x);
            Vector3 helmT = HelmetTop(t.Soldiers[0]);
            Assert.IsFalse(Covers(y, helmT), "the Tank's helmet shows above Y");
            yield return BattleTestUtil.Tap(helmT);
            Assert.AreSame(t, army.Selected, "tapping the Tank's helmet behind Y selects the Tank");
            Assert.AreEqual(destX, x.Destination);
            Assert.IsFalse(x.IsMoving);

            // A tap on Y's body where it is drawn in front of the Tank picks Y (drawn in front wins).
            army.Select(x);
            Vector3 bodyY = Vector3.Lerp(y.Soldiers[0].transform.position, helmY, 0.5f);
            yield return BattleTestUtil.Tap(bodyY);
            Assert.AreSame(y, army.Selected, "a body tap selects Y");
            Assert.IsFalse(x.IsMoving);
        }

        [UnityTest]
        public IEnumerator ClickingGroundNextToAUnitStillMoves()
        {
            yield return Setup();
            var army = Army;
            var hx = FreeHex(army.spawnPoint);
            var hy = FreeHex(army.spawnPoint + Vector3.right * 5f, hx);
            var x = army.Deploy(0, hx);
            var y = army.Deploy(1, hy);
            yield return BattleTestUtil.Wait(0.2f);
            army.Select(x);
            HexCoord dest = default;
            bool found = false;
            foreach (var c in army.Grid.Cells)
            {
                if (HexGrid.Distance(c, hy) != 2 || HexGrid.Distance(c, hx) < 2 || !army.Grid.IsFree(c)) continue;
                Vector2 s = BattleTestUtil.ToScreen(Center(c));
                if (s.x < 60f || s.x > BattleTestUtil.W - 60f || s.y < 60f || s.y > BattleTestUtil.H - 60f) continue;
                if (Covers(x, Center(c)) || Covers(y, Center(c))) continue;
                if (!army.Nav.FindPath(Center(hx), Center(c), new List<Vector3>())) continue;
                dest = c; found = true; break;
            }
            Assert.IsTrue(found, "open ground two spots from Y");
            yield return BattleTestUtil.Tap(Center(dest));
            Assert.AreEqual(dest, x.Destination, "a tap on open ground next to a unit moves the selected unit");
            Assert.AreSame(x, army.Selected);
        }

        [UnityTest]
        public IEnumerator HoverOverAnotherUnitShowsSelectCue()
        {
            yield return Setup();
            var army = Army;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var hx = FreeHex(army.spawnPoint);
            var hy = FreeHex(army.spawnPoint + Vector3.right * 5f, hx);
            var x = army.Deploy(0, hx);
            var y = army.Deploy(1, hy);
            yield return BattleTestUtil.Wait(0.2f);
            army.Select(x);
            yield return HoverAt(HelmetTop(y.Soldiers[0]));
            Assert.AreSame(y, Input.HoverUnit, "the click here would select Y");
            Assert.AreSame(y, overlay.SelectCueUnit, "a soft ring under Y");
            Assert.AreEqual(HexOverlay.MarkerKind.None, overlay.HoverKind, "no move marker over Y");
            Assert.IsTrue(overlay.transform.Find("SelectCue").GetComponent<MeshRenderer>().enabled);
            BattleTestUtil.Capture("overlay_select_cue");
            yield return HoverAt(HelmetTop(x.Soldiers[0]));
            Assert.IsNull(overlay.SelectCueUnit, "no cue over the selected unit itself");
            Assert.AreEqual(HexOverlay.MarkerKind.None, overlay.HoverKind);
            var open = FreeHexAtDistance(hx, 3);
            if (Covers(y, Center(open)) || Covers(x, Center(open))) open = FreeHexAtDistance(hy, 4);
            yield return HoverAt(Center(open));
            Assert.IsNull(overlay.SelectCueUnit, "no cue over open ground");
            Assert.AreEqual(HexOverlay.MarkerKind.Valid, overlay.HoverKind, "the move marker is back");
            Assert.IsFalse(overlay.transform.Find("SelectCue").GetComponent<MeshRenderer>().enabled);
        }

        [UnityTest]
        public IEnumerator DragPansAndDoesNotCommand()
        {
            yield return Setup();
            var army = Army;
            var bc = BattleCamera.Instance;
            var unit = army.Deploy(0, FreeHex(army.spawnPoint + Vector3.forward * 3f));
            army.Select(unit);
            var dest0 = unit.Destination;
            Vector3 f0 = bc.Focus;
            float cx = BattleTestUtil.W * 0.5f;

            yield return BattleTestUtil.DragScreen(new Vector2(cx, 520f), new Vector2(cx, 220f), 20);
            Debug.Log($"[Test] focus {f0} -> {bc.Focus}");
            Assert.Greater(bc.Focus.z, f0.z + 3f, "a 300 px drag pans the view up the map");
            Assert.AreEqual(dest0, unit.Destination, "the drag is not a move order");
            Assert.IsFalse(unit.IsMoving);
            Assert.AreSame(unit, army.Selected, "selection unchanged");

            // A press that starts over a HUD rect and releases over the map does nothing.
            PointerInput.UIHitTest = s => s.y > BattleTestUtil.H - 90f;
            Vector3 f1 = bc.Focus;
            yield return BattleTestUtil.DragScreen(new Vector2(cx, BattleTestUtil.H - 40f), new Vector2(cx, 300f), 20);
            Assert.AreEqual(f1, bc.Focus, "no pan from a press over the HUD");
            Assert.AreEqual(dest0, unit.Destination, "no order from a press over the HUD");
            Assert.AreSame(unit, army.Selected);
            yield return BattleTestUtil.DragScreen(new Vector2(cx, BattleTestUtil.H - 40f), new Vector2(cx, BattleTestUtil.H - 40f), 2);
            Assert.AreEqual(dest0, unit.Destination, "a tap on the HUD is not a map click");
            Assert.AreSame(unit, army.Selected);
        }

        [UnityTest]
        public IEnumerator OverlayVisibleOnlyWhenArmedOrSelected()
        {
            yield return Setup();
            var army = Army;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var marker = Object.FindFirstObjectByType<SelectionMarker>();
            Assert.IsNotNull(overlay);
            Assert.IsNotNull(marker);
            Assert.IsFalse(overlay.Visible, "hidden at the start");
            Assert.IsFalse(overlay.PadGlowing);

            Input.Arm(0);
            yield return null;
            Assert.IsTrue(overlay.Visible, "visible while armed");
            Assert.IsTrue(overlay.PadGlowing, "the capture ring glows while armed");
            Assert.IsFalse(marker.Shown);
            Input.Disarm();
            yield return null;
            Assert.IsFalse(overlay.Visible, "hidden once disarmed");
            Assert.IsFalse(overlay.PadGlowing);

            var unit = army.Deploy(0, FreeHex(army.spawnPoint));
            yield return null;
            Assert.IsFalse(overlay.Visible, "a deployed unit alone shows no markers");
            army.Select(unit);
            yield return null;
            Assert.IsTrue(overlay.Visible, "visible while a unit is selected");
            Assert.IsTrue(overlay.PadGlowing);
            Assert.IsTrue(marker.Shown, "selection ring shown");
            Vector3 ring = marker.transform.Find("Ring").position;
            Vector3 c = army.Grid.Center(unit.Hex);
            Assert.Less(new Vector2(ring.x - c.x, ring.z - c.z).magnitude, 0.01f, "the ring sits on the unit's hex");

            Input.Arm(1);
            Assert.IsNull(army.Selected, "arming clears the selection");
            Input.Disarm();
            yield return null;
            Assert.IsFalse(overlay.Visible);
            Assert.IsFalse(marker.Shown, "no ring without a selection");
        }

        /// <summary>Points the simulated pointer at a ground point and lets the input and the overlay update.</summary>
        static IEnumerator HoverAt(Vector3 world)
        {
            BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(world);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoveringABlockedSpotShowsTheInvalidMarker()
        {
            yield return Setup();
            var army = Army;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var blocked = BlockedHexOnScreen();
            var free = FreeHex(army.spawnPoint + Vector3.forward * 3f);
            var held = FreeHex(army.spawnPoint + Vector3.forward * 8f, free);
            var unit = army.Deploy(1, held);
            Assert.IsNotNull(unit);
            Assert.AreEqual(HexOverlay.HexStyle.Blocked, overlay.StyleOf(blocked), "a spot blocked by props or structures");
            Assert.AreEqual(HexOverlay.HexStyle.Normal, overlay.StyleOf(free));
            Assert.AreEqual(HexOverlay.HexStyle.Normal, overlay.StyleOf(held), "an occupied spot is not blocked (it holds a friendly unit)");
            yield return HoverAt(Center(blocked));
            Assert.AreEqual(HexOverlay.MarkerKind.None, overlay.HoverKind, "no marker while nothing is armed or selected");

            Input.Arm(0);
            yield return HoverAt(Center(blocked));
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind, "hovering a blocked spot shows the invalid marker");
            Assert.IsTrue(overlay.InvalidShown);
            yield return HoverAt(Center(free));
            Assert.AreEqual(HexOverlay.MarkerKind.Valid, overlay.HoverKind, "hovering an open spot shows the valid marker");
            Assert.IsFalse(overlay.InvalidShown);
            yield return HoverAt(Center(held));
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind, "a taken spot refuses a deploy");
            BattleTestUtil.Capture("overlay_invalid_spot");
            Input.Disarm();
            yield return null;
            Assert.AreEqual(HexOverlay.MarkerKind.None, overlay.HoverKind, "the marker goes once disarmed");
        }

        [UnityTest]
        public IEnumerator HoverMarkerSnapsAndShowsValidity()
        {
            yield return Setup();
            var army = Army;
            var g = army.Grid;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var a = FreeHex(army.spawnPoint + Vector3.forward * 4f);
            Vector3 ca = Center(a);
            Input.Arm(0);
            // Anywhere inside a spot, the marker sits on the spot's center.
            float r = g.OuterRadius * 0.55f;
            foreach (var off in new[] { Vector3.zero, new Vector3(r, 0, 0), new Vector3(-r, 0, 0), new Vector3(0, 0, r), new Vector3(r * 0.6f, 0, -r * 0.6f) })
            {
                yield return HoverAt(ca + off);
                Assert.AreEqual(a, Input.Hover, "pointer " + off + " is inside " + a);
                Assert.AreEqual(HexOverlay.MarkerKind.Valid, overlay.HoverKind);
                Assert.Less(new Vector2(overlay.HoverPoint.x - ca.x, overlay.HoverPoint.z - ca.z).magnitude, 1e-3f, "the marker snaps to the spot");
            }
            // Moving one spot over jumps the marker by one spot spacing.
            var list = new List<HexCoord>();
            g.Neighbors(a, list);
            HexCoord b = default;
            bool found = false;
            foreach (var n in list) if (g.IsFree(n)) { b = n; found = true; break; }
            Assert.IsTrue(found, "a free neighbor");
            yield return HoverAt(Center(b));
            Assert.Less((overlay.HoverPoint - Center(b)).magnitude, 1e-3f, "snapped to the next spot");
            Assert.AreEqual(Mathf.Sqrt(3f) * g.OuterRadius, (Center(b) - ca).magnitude, 1e-3f, "one spot spacing");
            // The marker is round and about one unit footprint wide: it covers the unit's three soldier slots.
            Assert.Greater(overlay.MarkerRadius, g.OuterRadius * HexGrid.SlotFraction + 0.3f);
            Assert.Less(overlay.MarkerRadius, g.OuterRadius);

            // An enemy standing on a spot makes it invalid for a deploy.
            var near = FreeHex(army.spawnPoint + Vector3.forward * 9f + Vector3.right * 3f, a, b);
            BattleTestUtil.SpawnEnemySquad(Center(near), 1, false, true);
            yield return BattleTestUtil.Wait(0.3f);
            Health enemy = null;
            foreach (var t in UnitRegistry.Of(Team.Enemy))
                if (t.kind != TargetKind.Structure && (enemy == null || (t.transform.position - Center(near)).sqrMagnitude < (enemy.transform.position - Center(near)).sqrMagnitude)) enemy = t;
            Assert.IsNotNull(enemy, "the enemy soldier");
            Assert.IsTrue(g.TryWorldToHex(enemy.transform.position, out HexCoord enemyAt));
            Assert.IsFalse(army.CanDeploy(0, enemyAt), "a deploy where an enemy stands is refused");
            yield return HoverAt(Center(enemyAt));
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind, "an enemy standing there");

            // Off the field: red marker at the pointer.
            Vector3 outside = new Vector3(BattleContext.Config.mapMax.x + 3f, 0f, ca.z);
            yield return HoverAt(outside);
            Assert.IsTrue(Input.HasHover && !Input.HoverOnMap, "pointer off the field");
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind, "off the field");
            Assert.Less(new Vector2(overlay.HoverPoint.x - outside.x, overlay.HoverPoint.z - outside.z).magnitude, 0.2f, "at the pointer");

            // Selected: open spots are valid, the unit's own spot shows nothing (a click keeps the selection).
            Input.Disarm();
            var unit = army.Deploy(0, a);
            Assert.IsNotNull(unit);
            army.Select(unit);
            yield return HoverAt(Center(b));
            Assert.AreEqual(HexOverlay.MarkerKind.Valid, overlay.HoverKind, "a move to an open spot");
            yield return HoverAt(ca);
            Assert.AreEqual(HexOverlay.MarkerKind.None, overlay.HoverKind, "no marker over the selected unit");
            yield return HoverAt(Center(BlockedHexOnScreen()));
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind, "a move onto a blocked spot");

            // A refused click flashes the crossed ring at the refused spot.
            Input.Arm(1);
            int refused = 0;
            System.Action<HexCoord> onRejected = _ => refused++;
            army.OrderRejected += onRejected;
            var blocked = BlockedHexOnScreen();
            yield return BattleTestUtil.Tap(Center(blocked));
            yield return null; // the overlay draws the flash in the frame's LateUpdate
            army.OrderRejected -= onRejected;
            Assert.AreEqual(1, refused, "the deploy on a blocked spot is refused");
            Assert.IsTrue(overlay.FlashShown, "reject flash");
            var fl = overlay.transform.Find("Flash").position;
            Assert.Less(new Vector2(fl.x - Center(blocked).x, fl.z - Center(blocked).z).magnitude, 1e-3f, "at the refused spot");
            yield return BattleTestUtil.Wait(0.45f);
            Assert.IsFalse(overlay.FlashShown, "the flash lasts 0.3 s");
        }

        [UnityTest]
        public IEnumerator NoGridIsDrawnInBattle()
        {
            yield return Setup();
            var army = Army;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var spot = FreeHex(army.spawnPoint + Vector3.forward * 4f);
            var allowed = new HashSet<string> { "Hover", "SelectCue", "Flash", "Suggested", "PadGlow" };

            void AssertOnlyRoundMarkers(string when)
            {
                int enabled = 0;
                foreach (var r in overlay.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsTrue(allowed.Contains(r.name), when + ": overlay renderer '" + r.name + "' is a round marker");
                    if (!r.enabled) continue;
                    enabled++;
                    var mf = r.GetComponent<MeshFilter>();
                    Assert.IsNotNull(mf.sharedMesh, r.name);
                    string mesh = mf.sharedMesh.name;
                    Assert.IsFalse(mesh.ToLowerInvariant().Contains("hex"), when + ": no hex mesh (" + mesh + ")");
                    if (r.name == "Suggested") continue; // several discs in one mesh
                    // A round marker's footprint is as wide as it is deep; a pointy-top hex is not (sqrt 3 to 2).
                    var e = mf.sharedMesh.bounds.extents;
                    Assert.AreEqual(e.x, e.z, e.x * 0.02f, when + ": " + r.name + " is round");
                }
                Assert.Greater(enabled, 0, when + ": the markers show");
                // Nothing else in the scene draws a grid or hex tiles.
                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                    if (mf.sharedMesh != null && mf.GetComponent<Renderer>() != null && mf.GetComponent<Renderer>().enabled)
                        Assert.IsFalse(mf.sharedMesh.name.ToLowerInvariant().Contains("hex"), when + ": " + mf.name + " draws " + mf.sharedMesh.name);
                Assert.IsFalse(BattleSetup.Instance.groundRenderer.sharedMaterial.HasProperty("_HexSize"), "the ground shader has no hex pattern");
            }

            Input.Arm(0);
            yield return HoverAt(Center(spot));
            AssertOnlyRoundMarkers("armed");
            yield return BattleTestUtil.Tap(Center(spot));
            var unit = army.UnitAt(spot);
            Assert.IsNotNull(unit);
            army.Select(unit);
            yield return HoverAt(Center(spot) + Vector3.forward * 5f);
            AssertOnlyRoundMarkers("selected");
        }

        [UnityTest]
        public IEnumerator ActivePadRingGlowsOnlyWhileArmedOrSelected()
        {
            yield return Setup();
            var army = Army;
            var bm = BattleManager.Instance;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var cp = bm.capturePoints[0];
            Assert.IsTrue(cp.Active && !cp.Captured, "point 1 is the active point");
            Assert.IsFalse(overlay.PadGlowing, "no glow while nothing is armed or selected");

            var unit = army.Deploy(0, FreeHex(army.spawnPoint));
            yield return null;
            Assert.IsFalse(overlay.PadGlowing, "a deployed unit alone does not light the ring");
            army.Select(unit);
            yield return null;
            Assert.IsTrue(overlay.PadGlowing, "the active ring glows while a unit is selected");
            Assert.AreSame(cp, overlay.PadGlowPoint, "on point 1");
            var glow = overlay.transform.Find("PadGlow");
            Vector3 gp = glow.position, c = cp.transform.position;
            Assert.Less(new Vector2(gp.x - c.x, gp.z - c.z).magnitude, 1e-3f, "centered on the capture circle");
            var ext = glow.GetComponent<MeshFilter>().sharedMesh.bounds.extents;
            Assert.That(ext.x, Is.InRange(cp.DrawnRadius * 0.97f, cp.DrawnRadius * 1.12f), "laid over the drawn ring");

            army.Select(null);
            yield return null;
            Assert.IsFalse(overlay.PadGlowing, "the glow goes with the selection");

            // Once point 1 is taken the glow moves to point 2.
            cp.ForceCapture();
            yield return null;
            yield return null;
            var cp2 = bm.capturePoints[1];
            Assert.IsTrue(cp2.Active, "point 2 opens");
            Input.Arm(1);
            yield return null;
            Assert.IsTrue(overlay.PadGlowing);
            Assert.AreSame(cp2, overlay.PadGlowPoint, "the glow is on point 2");
            gp = glow.position; c = cp2.transform.position;
            Assert.Less(new Vector2(gp.x - c.x, gp.z - c.z).magnitude, 1e-3f);
            Input.Disarm();
            yield return null;
            Assert.IsFalse(overlay.PadGlowing);
        }

        [UnityTest]
        public IEnumerator NoInputAfterBattleEnds()
        {
            yield return Setup();
            var army = Army;
            var bm = BattleManager.Instance;
            yield return BattleTestUtil.Wait(0.4f);
            foreach (var u in BattleTestUtil.DeployAll()) BattleTestUtil.KillAll(u);
            float end = Time.time + 2f;
            while (Time.time < end && !bm.Over) yield return null;
            Assert.IsTrue(bm.Over, "defeat");
            PointerInput.UIHitTest = null; // the result screen's hit test must not be what blocks the taps

            // Put an entry back to Ready so arming would otherwise be allowed.
            Assert.IsTrue(army.RestoreLastDestroyed());
            int ready = -1;
            for (int i = 0; i < army.Squad.Count; i++) if (army.Squad[i].state == EntryState.Ready) ready = i;
            Assert.GreaterOrEqual(ready, 0);
            var hex = BattleTestUtil.FreeHexNear(army.spawnPoint);

            Input.Arm(ready);
            Assert.AreEqual(-1, Input.ArmedEntry, "arming is ignored after the battle");
            yield return BattleTestUtil.Tap(Center(hex));
            Assert.AreEqual(0, army.Units.Count, "nothing deployed");

            // Even with the army's input switched back on, a finished battle ignores input.
            army.InputEnabled = true;
            Input.Arm(ready);
            Assert.AreEqual(-1, Input.ArmedEntry, "arming is ignored once the battle is over");
            yield return BattleTestUtil.Tap(Center(hex));
            Assert.AreEqual(0, army.Units.Count, "nothing deployed");
            Assert.AreEqual(EntryState.Ready, army.Squad[ready].state);
        }

        [UnityTest]
        public IEnumerator BubbleTapStillPopsWhileArmed()
        {
            yield return Setup();
            var army = Army;
            // Level 1's only flag bearer squad is far up the lane; bring one into view (where base one's first squad stood).
            var sq = BattleTestUtil.SpawnEnemySquad(new Vector3(-8.5f, 0f, 21.5f), 10, true, false);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.Greater(sq.AliveCount, 0);
            sq.Bearer.Health.Kill(TeamColorId.Red);
            yield return BattleTestUtil.Wait(0.2f);
            Assert.IsTrue(sq.Bubble.CanTap, "bubble left behind and tappable");
            Vector3 at = sq.Bubble.transform.position + Vector3.up * 0.8f;
            AssertOnScreen(at, "bubble");

            Input.Arm(0);
            yield return BattleTestUtil.Tap(at);
            yield return BattleTestUtil.Wait(0.6f);
            Assert.IsTrue(sq.Wiped, "the tap pops the bubble");
            Assert.AreEqual(0, sq.AliveCount);
            Assert.AreEqual(0, army.Units.Count, "the tap deploys nothing");
            Assert.AreEqual(EntryState.Ready, army.Squad[0].state);
            Assert.AreEqual(0, Input.ArmedEntry, "the entry stays armed");
        }
    }
}
