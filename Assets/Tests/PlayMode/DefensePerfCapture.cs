using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.Units;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Frame timing of a heavy defense battle (same routine as PerfCapture): a full player base with every
    /// defense type, the home map's props spawned at runtime, and a large AI attacking squad. Writes Logs/perf_defense.txt.</summary>
    [Category("Perf")]
    public class DefensePerfCapture
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleTestUtil.Cleanup();
            UITestUtil.Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrameTime_HeavyDefenseBattle()
        {
            UITestUtil.UseTempSave();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.level = 14;
            foreach (var id in new[] { "def_mine", "def_turret", "def_bunker", "def_tank", "def_cannon", "def_tesla", "def_sniper", "def_geyser" }) p.SetResearch(id, 2);
            void Pl(string id, float x, float z) => p.BaseLayout.placements.Add(new DefensePlacement { defenseId = id, x = x, z = z, rotation = 180f });
            for (int i = 0; i < 8; i++) Pl("mine", -9f + i * 2.5f, 24f + (i % 2) * 3f);
            Pl("geyser", -2f, 30f); Pl("geyser", 3f, 33f); Pl("geyser", -6f, 52f);
            Pl("turret", -7f, 34f); Pl("turret", 7f, 34f); Pl("turret", -8f, 62f); Pl("turret", 8f, 70f);
            Pl("tesla", -3f, 38f); Pl("tesla", 3f, 58f); Pl("tesla", -3f, 80f);
            Pl("sniper", -12f, 40f); Pl("sniper", 12f, 64f);
            Pl("bunker", 9f, 50f); Pl("bunker", -9f, 76f);
            Pl("cannon", -10f, 56f); Pl("cannon", 10f, 84f);
            Pl("tank", 0f, 66f); Pl("tank", 0f, 98f);
            Pl("squad", 6f, 42f); Pl("squad", -6f, 66f); Pl("squad", 6f, 86f);
            SaveSystem.Save();

            yield return DefenseBattleTests.LoadDefense(true, 99);
            BattleTestUtil.Pointer = new Core.SimulatedPointerSource();
            Core.PointerInput.Source = BattleTestUtil.Pointer;
            var cam = BattleTestUtil.Cam;
            var rt = new RenderTexture(BattleTestUtil.W, BattleTestUtil.H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var squad = SquadController.Instance;
            var bm = BattleManager.Instance;
            int defenders = 0;
            foreach (var s in Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None)) defenders += s.AliveCount;
            int structures = Object.FindObjectsByType<StructureBase>(FindObjectsSortMode.None).Length;
            int props = BattleSetup.Instance.environmentRoot != null ? BattleSetup.Instance.environmentRoot.childCount : 0;
            // Let the attackers reach the first defenses so everything is firing.
            float end = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < end && !bm.Over && (bm.Elapsed < 7f || ProjectileManager.Instance.ActiveCount < 12)) yield return null;
            int players = squad.AliveCount;

            int maxProjectiles = 0;
            var sampler = new PerfSampler();
            yield return sampler.Run(cam, 300, () => maxProjectiles = Mathf.Max(maxProjectiles, ProjectileManager.Instance.ActiveCount));
            string report =
                $"Editor play mode (batchmode, Apple M2), Battle.unity in DEFENSE mode on the home map ({props} runtime props), 1280x720 render target\n" +
                $"Attackers alive at window start: {players}, defender soldiers at load: {defenders}, structures and traps: {structures}, AI state {BattleSetup.Instance.attackerAI.Current}\n" +
                sampler.Report +
                $"Peak live projectiles: {maxProjectiles}, splats: {PaintSplatManager.Instance.Count}, kills so far: {bm.Kills}, battle time {bm.Elapsed:0.0}s\n";
            Debug.Log("[Perf]\n" + report);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf_defense.txt")), report);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Assert.Pass(report);
        }
    }
}
