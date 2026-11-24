using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.CameraRig;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Readability check for the soldier presentation (lean and visual yaw clamp): eight soldiers facing the
    /// eight compass directions, captured at game zoom and at 4x zoom. Writes Docs/Screenshots/soldier_lineup*.png.</summary>
    [Category("Lineup")]
    public class SoldierLineupCapture
    {
        [UnityTearDown]
        public IEnumerator TearDown() { BattleTestUtil.Cleanup(); yield return null; }

        [UnityTest]
        public IEnumerator CaptureLineup()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.5f);
            var army = BattleTestUtil.Army;
            BattleTestUtil.DeployAll();
            yield return null;
            var list = BattleTestUtil.ArmySoldiers();
            Vector3 c = army.Centroid + new Vector3(0f, 0f, -3f);
            int shown = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                s.HoldFire = true;
                s.Motor.Enabled = false;
                if (i < 8)
                {
                    s.transform.SetPositionAndRotation(c + new Vector3((i - 3.5f) * 1.4f, 0f, 0f), Quaternion.Euler(0f, i * 45f, 0f));
                    shown++;
                }
                else s.transform.position = c + new Vector3((i % 16 - 8) * 0.9f, 0f, -4f - (i / 16) * 1.2f);
            }
            var bc = BattleCamera.Instance;
            bc.enabled = false;
            bc.SnapTo(c);
            yield return BattleTestUtil.Wait(0.6f);
            BattleTestUtil.Capture("soldier_lineup");
            var cam = BattleTestUtil.Cam;
            float size = cam.orthographicSize;
            cam.orthographicSize = size / 4f;
            yield return null;
            BattleTestUtil.Capture("soldier_lineup_zoom");
            var cc = BattleContext.Combat;
            if (System.Environment.GetEnvironmentVariable("TW_LEAN_SWEEP") == "1" && cc != null)
            {
                float keep = cc.visualLean;
                foreach (var l in new[] { -30f, 0f, 30f })
                {
                    cc.visualLean = l;
                    yield return null; yield return null;
                    BattleTestUtil.Capture("soldier_lineup_lean" + (int)l);
                }
                cc.visualLean = keep;
            }
            cam.orthographicSize = size;
            for (int i = 0; i < 8 && i < list.Count; i++)
                Debug.Log($"[Lineup] soldier {i} root yaw {i * 45} visual yaw {list[i].View.VisualYaw:0}");
            bc.enabled = true;
            Assert.AreEqual(8, shown);
        }
    }
}
