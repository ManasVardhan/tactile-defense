using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Core;
using TactileWars.Meta;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>
    /// The look captures for each biome (the whole map top-down, the battle camera with a unit, the hover marker on
    /// a valid and an invalid spot, the capture ring glowing) written as Docs/Screenshots/look_biome_what.png.
    /// </summary>
    public class BiomeLookTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleLaunch.Clear();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        /// <summary>Loads Level index+1 through the launch path with the earlier levels cleared.</summary>
        static IEnumerator LoadLevel(int index)
        {
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            BattleTestUtil.MarkTutorialSeen(p);
            var b = MetaGame.Db.roster.bases[index];
            p.defeatedBases.Clear();
            for (int i = 0; i < index; i++) p.defeatedBases.Add(MetaGame.Db.roster.bases[i].id);
            p.squad.Clear();
            p.level = Mathf.Max(1, b.requiredLevel);
            BattleLaunch.Prepare(b);
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.4f);
        }

        // Reachability on every level's map is proven in LevelLayoutTests.

        // ------------------------------------------------------------------ captures

        static HexCoord BlockedNear(Vector3 at)
        {
            var g = PlayerArmy.Instance.Grid;
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsBlocked(c)) continue;
                Vector2 s = BattleTestUtil.ToScreen(g.Center(c));
                if (s.x < 80f || s.x > BattleTestUtil.W - 80f || s.y < 140f || s.y > BattleTestUtil.H - 120f) continue;
                float d = (g.Center(c) - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "a blocked spot on screen");
            return best;
        }

        static IEnumerator Hover(Vector3 world)
        {
            BattleTestUtil.Pointer.Position = BattleTestUtil.ToScreen(world);
            yield return null;
            yield return null;
        }

        static IEnumerator CaptureBiome(int index, string biome)
        {
            yield return LoadLevel(index);
            PointerInput.UIHitTest = null;
            var army = PlayerArmy.Instance;
            var input = ArmyInput.Instance;
            var overlay = Object.FindFirstObjectByType<HexOverlay>();
            var bc = BattleCamera.Instance;
            var gc = BattleContext.Config;

            // The whole map from above, HUD hidden, nothing armed.
            yield return BattleTestUtil.CaptureTopDown("look_" + biome + "_topdown");

            // The battle camera at the start with a unit deployed.
            Vector3 focus = bc.Focus;
            var spot = BattleTestUtil.FreeHexNear(focus + MapPath.Forward(focus) * 1.5f);
            Assert.IsNotNull(army.Deploy(0, spot));
            yield return BattleTestUtil.Wait(1.2f);
            BattleTestUtil.Capture("look_" + biome + "_start");

            // Armed: the hover marker on an open spot, then on a blocked one.
            input.Arm(1);
            var open = BattleTestUtil.FreeHexNear(focus + Vector3.right * 4f + Vector3.forward * 2f, spot);
            yield return Hover(army.Grid.Center(open));
            Assert.AreEqual(HexOverlay.MarkerKind.Valid, overlay.HoverKind);
            yield return BattleTestUtil.Wait(0.2f);
            BattleTestUtil.Capture("look_" + biome + "_hover_valid");
            var blocked = BlockedNear(focus + Vector3.forward * 3f);
            yield return Hover(army.Grid.Center(blocked));
            Assert.AreEqual(HexOverlay.MarkerKind.Invalid, overlay.HoverKind);
            BattleTestUtil.Capture("look_" + biome + "_hover_invalid");

            // The first capture circle glowing while armed.
            var cp = BattleManager.Instance.capturePoints[0];
            bc.SnapTo(cp.transform.position + Vector3.back * 2f);
            yield return BattleTestUtil.Wait(0.3f);
            var pad = BattleTestUtil.PadHex(cp);
            yield return Hover(army.Grid.Center(pad) + Vector3.left * 0.2f);
            Assert.IsTrue(overlay.PadGlowing, "the capture ring glows while armed");
            BattleTestUtil.Capture("look_" + biome + "_capture");
            input.Disarm();
        }

        [UnityTest]
        public IEnumerator CaptureMeadowLook() { yield return CaptureBiome(0, "meadow"); }

        [UnityTest]
        public IEnumerator CaptureCanyonLook() { yield return CaptureBiome(2, "canyon"); }

        [UnityTest]
        public IEnumerator CaptureRidgeLook() { yield return CaptureBiome(3, "ridge"); }

        [UnityTest]
        public IEnumerator CaptureDuskLook() { yield return CaptureBiome(4, "dusk"); }
    }
}
