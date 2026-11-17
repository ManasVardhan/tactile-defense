using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Battle;
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
            var squad = BattleTestUtil.Squad;
            yield return BattleTestUtil.Wait(1.2f);
            BattleTestUtil.Capture("battle_start");

            // Mid drag: a sweeping curve ahead of the squad, captured before release.
            Vector3 c = squad.Centroid;
            var curve = new List<Vector3>();
            for (int i = 0; i <= 30; i++)
            {
                float u = i / 30f;
                curve.Add(c + new Vector3(-8f + 16f * u, 0, 4.5f + Mathf.Sin(u * Mathf.PI) * 3.5f));
            }
            yield return BattleTestUtil.Drag(curve.ToArray(), 1.4f, false, 0.7f);
            yield return null;
            BattleTestUtil.Capture("battle_stroke_drawing");
            yield return BattleTestUtil.Drag(curve.GetRange(20, 11).ToArray(), 0.4f, true);
            yield return BattleTestUtil.Wait(2.2f);

            // Line formation.
            c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-8f, 0, 3f), c + new Vector3(8f, 0, 5f)), 0.6f);
            yield return BattleTestUtil.Wait(1.0f);
            yield return BattleTestUtil.Wait(1.4f);
            BattleTestUtil.Capture("battle_formation_line");

            // Circle formation.
            c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.CirclePoints(c + new Vector3(0, 0, 1f), 4.2f), 0.9f);
            yield return BattleTestUtil.Wait(2.6f);
            BattleTestUtil.Capture("battle_formation_circle");

            // Firefight: arc toward the first two enemy squads.
            c = squad.Centroid;
            var arc = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Lerp(160f, 20f, i / 24f) * Mathf.Deg2Rad;
                arc.Add(new Vector3(0f, 0, 15.5f) + new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 3.0f));
            }
            yield return BattleTestUtil.Drag(arc.ToArray(), 0.8f);
            float fightStart = Time.time;
            float deadline = Time.time + 12f;
            while (Time.time < deadline && (Time.time - fightStart < 3.5f || ProjectileManager.Instance.ActiveCount < 22 || PaintSplatManager.Instance.Count < 40)) yield return null;
            BattleTestUtil.Capture("battle_combat");
            Debug.Log($"[Capture] combat: fired {ProjectileManager.Instance.TotalFired} kills {BattleManager.Instance.Kills} splats {PaintSplatManager.Instance.Count} alive {squad.AliveCount}");

            // Clear the way, then stand on the first capture point.
            yield return BattleTestUtil.Wait(3f);
            var cp = BattleManager.Instance.capturePoints[0];
            Vector3 p = cp.transform.position;
            yield return BattleTestUtil.Drag(BattleTestUtil.CirclePoints(p + new Vector3(0, 0, -0.5f), 2.2f, 40), 0.8f);
            float capDeadline = Time.time + 14f;
            while (Time.time < capDeadline && cp.Progress < 0.55f) yield return null;
            BattleTestUtil.Capture("battle_capture");
            Debug.Log($"[Capture] capture progress {cp.Progress:0.00} inside {cp.PlayersInside} alive {squad.AliveCount}");

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

    /// <summary>Editor play mode frame timing with 40 player soldiers and every enemy active.</summary>
    [Category("Perf")]
    public class PerfCapture
    {
        [UnityTearDown]
        public IEnumerator TearDown() { BattleTestUtil.Cleanup(); yield return null; }

        [UnityTest]
        public IEnumerator FrameTime_FullBattle()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.5f);
            var squad = BattleTestUtil.Squad;
            var squads = Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None);
            int enemySoldiers = 0;
            foreach (var s in squads) enemySoldiers += s.AliveCount;
            int players = squad.AliveCount;

            // Start a firefight so targeting, projectiles, VFX and splats are all busy.
            yield return BattleTestUtil.Tap(new Vector3(0f, 0, 17f));
            yield return BattleTestUtil.Wait(2.5f);

            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var cam = BattleTestUtil.Cam;
            const int frames = 240;
            var cpu = new List<double>(frames);
            var main = new List<double>(frames);
            var sw = new System.Diagnostics.Stopwatch();
            int maxProjectiles = 0;
            for (int i = 0; i < frames; i++)
            {
                sw.Restart();
                cam.Render(); // include rendering cost: batchmode does not present frames on its own
                yield return null;
                sw.Stop();
                cpu.Add(sw.Elapsed.TotalMilliseconds);
                if (mainThread.Valid && mainThread.LastValue > 0) main.Add(mainThread.LastValue * 1e-6);
                maxProjectiles = Mathf.Max(maxProjectiles, ProjectileManager.Instance.ActiveCount);
            }
            mainThread.Dispose();
            cpu.Sort(); main.Sort();
            double avg = 0; foreach (var v in cpu) avg += v; avg /= cpu.Count;
            double mavg = 0; foreach (var v in main) mavg += v; if (main.Count > 0) mavg /= main.Count;
            string report =
                $"Editor play mode (batchmode, Apple M2), {frames} frames, Battle.unity\n" +
                $"Player soldiers at start of window: {players}, enemy soldiers alive at load: {enemySoldiers} in {squads.Length} squads, all defenses active\n" +
                $"Wall time per frame incl. extra camera render: avg {avg:0.00} ms, median {cpu[cpu.Count / 2]:0.00} ms, p95 {cpu[(int)(cpu.Count * 0.95f)]:0.00} ms\n" +
                (main.Count > 0 ? $"Profiler 'Main Thread': avg {mavg:0.00} ms, median {main[main.Count / 2]:0.00} ms, p95 {main[(int)(main.Count * 0.95f)]:0.00} ms\n" : "Profiler 'Main Thread' recorder unavailable\n") +
                $"Peak live projectiles: {maxProjectiles}, splats: {PaintSplatManager.Instance.Count}, kills so far: {BattleManager.Instance.Kills}\n";
            Debug.Log("[Perf]\n" + report);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf_battle.txt"));
            File.WriteAllText(path, report);
            Assert.Pass(report);
        }
    }
}
