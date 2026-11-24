using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Units;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Scripted play session that saves the milestone screenshots to Docs/Screenshots.</summary>
    [Category("Screenshots")]
    public class ScreenshotCapture
    {
        [UnityTearDown]
        public IEnumerator TearDown() { BattleTestUtil.Cleanup(); yield return null; }

        [UnityTest]
        public IEnumerator CaptureAll()
        {
            yield return BattleTestUtil.LoadBattle();
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            yield return BattleTestUtil.Wait(1.2f);
            BattleTestUtil.Capture("battle_start");

            // Firefight: every unit advances on Level 1's squad guarding point 1.
            var taken = new List<HexCoord>();
            var fronts = new[] { new Vector3(-4f, 0, 37f), new Vector3(0f, 0, 37.5f), new Vector3(4f, 0, 37f) };
            for (int i = 0; i < units.Count; i++)
            {
                var dest = BattleTestUtil.FreeHexNear(fronts[i % fronts.Length], taken.ToArray());
                army.OrderMove(units[i], dest);
                taken.Add(dest);
            }
            float fightStart = Time.time;
            float deadline = Time.time + 20f;
            while (Time.time < deadline && (Time.time - fightStart < 3.5f || ProjectileManager.Instance.ActiveCount < 8 || PaintSplatManager.Instance.Count < 20)) yield return null;
            BattleCamera.Instance.SnapTo(army.Centroid + Vector3.forward * 3f);
            yield return null;
            BattleTestUtil.Capture("battle_combat");
            Debug.Log($"[Capture] combat: fired {ProjectileManager.Instance.TotalFired} kills {BattleManager.Instance.Kills} splats {PaintSplatManager.Instance.Count} alive {army.AliveCount}");

            // Clear the way, then send a unit onto the first capture point.
            float clearBy = Time.time + 12f;
            while (Time.time < clearBy && TactileWars.UI.TutorialSteps.FirstPointClear(BattleManager.Instance) == false) yield return null;
            var cp = BattleManager.Instance.capturePoints[0];
            PlayerUnit runner = null;
            foreach (var u in army.Units) { runner = u; break; }
            if (runner != null) army.OrderMove(runner, BattleTestUtil.PadHex(cp));
            float capDeadline = Time.time + 14f;
            while (Time.time < capDeadline && cp.Progress < 0.55f) yield return null;
            BattleTestUtil.Capture("battle_capture");
            Debug.Log($"[Capture] capture progress {cp.Progress:0.00} inside {cp.PlayersInside} alive {army.AliveCount}");

            // Extra overview frames of the later map sections (level dressing check, camera parked).
            var bc = TactileWars.CameraRig.BattleCamera.Instance;
            bc.enabled = false;
            foreach (var z in new[] { 64f, 92f })
            {
                bc.SnapTo(new Vector3(0f, 0f, z));
                yield return null;
                BattleTestUtil.Capture("overview_z" + (int)z);
            }
            bc.enabled = true;
            Assert.Pass();
        }
    }

    /// <summary>Editor play mode frame timing with the default army deployed and every enemy active.</summary>
    [Category("Perf")]
    public class PerfCapture
    {
        [UnityTearDown]
        public IEnumerator TearDown() { TactileWars.Meta.BattleLaunch.Clear(); BattleTestUtil.Cleanup(); yield return null; }

        [UnityTest]
        public IEnumerator FrameTime_FullBattle()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.5f);
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            var squads = Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None);
            int enemySoldiers = 0;
            foreach (var s in squads) enemySoldiers += s.AliveCount;
            int players = army.AliveCount;

            // Start a firefight so targeting, projectiles, VFX and splats are all busy.
            var taken = new List<HexCoord>();
            foreach (var u in units)
            {
                var dest = BattleTestUtil.FreeHexNear(new Vector3(0f, 0, 37f), taken.ToArray());
                army.OrderMove(u, dest);
                taken.Add(dest);
            }
            yield return BattleTestUtil.Wait(8f);

            var cam = BattleTestUtil.Cam;
            int maxProjectiles = 0;
            var sampler = new PerfSampler();
            yield return sampler.Run(cam, 300, () => maxProjectiles = Mathf.Max(maxProjectiles, ProjectileManager.Instance.ActiveCount));
            string report =
                $"Editor play mode (batchmode, Apple M2), Battle.unity attack (handcrafted base one deployment), 1280x720 render target\n" +
                $"Player soldiers at start of window: {players}, enemy soldiers alive at load: {enemySoldiers} in {squads.Length} squads, all defenses active\n" +
                sampler.Report +
                $"Peak live projectiles: {maxProjectiles}, splats: {PaintSplatManager.Instance.Count}, kills so far: {BattleManager.Instance.Kills}\n";
            Debug.Log("[Perf]\n" + report);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf_battle.txt"));
            File.WriteAllText(path, report);
            Assert.Pass(report);
        }

        /// <summary>The largest level (Level 5, The Fort) at its start view with the squad deployed and marching:
        /// shows the new maps do not make a battle heavier than the classic lane.</summary>
        [UnityTest]
        public IEnumerator FrameTime_LargestLevel()
        {
            yield return LevelLayoutTests.LoadLevel(TactileWars.Meta.Progression.LevelCount - 1);
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            var cp = BattleManager.Instance.capturePoints[0];
            foreach (var u in units) army.OrderMove(u, BattleTestUtil.FreeHexNear(cp.transform.position));
            yield return BattleTestUtil.Wait(3f);
            var cam = BattleTestUtil.Cam;
            var sampler = new PerfSampler();
            yield return sampler.Run(cam, 300, () => { });
            var map = BattleSetup.Instance.Map;
            string report = $"Editor play mode (batchmode, Apple M2), Battle.unity attack on Level 5 '{map.displayName}' ({map.props.Length} map props, " +
                            $"{map.mapMax.x - map.mapMin.x:0} x {map.mapMax.y - map.mapMin.y:0}), 1280x720 render target\n" + sampler.Report;
            Debug.Log("[Perf]\n" + report);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf_level5.txt")), report);
            Assert.Pass(report);
        }
    }
}
