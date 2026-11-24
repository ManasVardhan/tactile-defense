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
    /// Every ladder level on its own map: the right map and biome, the army, camera, grids and counterattack hatches
    /// following that map's data, a nav path from the spawn to both capture points, from every hatch to both points
    /// and to every enemy socket; the bot finding its way along a bent map; and the per level captures
    /// (Docs/Screenshots/level_N_topdown.png and level_N_start.png).
    /// </summary>
    public class LevelLayoutTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSpeed.Reset();
            BattleLaunch.Clear();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        /// <summary>Loads Level index+1 through the launch path with the earlier levels cleared.</summary>
        public static IEnumerator LoadLevel(int index)
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

        static readonly string[] Biomes = { "meadow", "meadow", "canyon", "ridge", "dusk" };

        static HexCoord NearestFree(PlayerArmy army, Vector3 p)
        {
            var g = army.Grid;
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c)) continue;
                float d = (g.Center(c) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue);
            return best;
        }

        [UnityTest]
        public IEnumerator EveryLevelHasItsOwnReachableMap()
        {
            Assert.AreEqual(5, Progression.LevelCount);
            var db = MetaDatabase.Load();
            Assert.AreEqual(Progression.LevelCount, db.roster.bases.Length, "the roster holds exactly the ladder's levels");
            var ids = new HashSet<string>();
            var path = new List<Vector3>();
            for (int index = 0; index < Progression.LevelCount; index++)
            {
                yield return LoadLevel(index);
                int level = index + 1;
                var setup = BattleSetup.Instance;
                var map = setup.Map;
                Assert.IsNotNull(map, $"Level {level} has a map");
                Assert.AreEqual("level" + level, map.id, $"Level {level} plays on its own map");
                Assert.IsTrue(ids.Add(map.id));
                Assert.AreEqual(Biomes[index], map.biome.id, $"Level {level} biome");
                Assert.Greater(map.props.Length, 300, "the map is dressed");

                // Everything follows the map's data.
                var army = PlayerArmy.Instance;
                var bm = BattleManager.Instance;
                var gc = BattleContext.Config;
                Assert.AreEqual(map.mapMin, gc.mapMin, "nav and hidden grid bounds");
                Assert.AreEqual(map.mapMax, gc.mapMax);
                Assert.AreEqual(map.playerSpawn, army.spawnPoint, "army spawn");
                var bc = BattleCamera.Instance;
                Assert.AreEqual(map.focusMin, bc.config.focusMin, "camera pan bounds");
                Assert.AreEqual(map.focusMax, bc.config.focusMax);
                Vector3 f = bc.Focus;
                Assert.Less(Vector2.Distance(new Vector2(f.x, f.z), new Vector2(map.playerSpawn.x, map.playerSpawn.z)), 16f, "the camera starts on the spawn (a corner start is clamped by the pan bounds)");
                Assert.IsTrue(bc.Cam.WorldToViewportPoint(map.playerSpawn).x is > 0.05f and < 0.95f, "the spawn is in view at the start");
                CollectionAssert.AreEqual(map.spawnHatches, bm.counterSpawns, "counterattack hatches");
                for (int k = 0; k < bm.capturePoints.Length; k++)
                    Assert.Less(Vector3.Distance(bm.capturePoints[k].transform.position, map.capturePoints[k]), 0.01f);
                Assert.AreEqual(map.path.Length, MapPath.Points.Length, "the path the bot and tutorial follow");
                Assert.Greater(MapPath.Progress(map.capturePoints[1]), MapPath.Progress(map.capturePoints[0]), "point 2 lies further along the path");
                Assert.Greater(MapPath.Progress(map.hq), MapPath.Progress(map.capturePoints[1]), "the hall lies beyond point 2");

                var g = army.Grid;
                var nav = army.Nav;
                Vector3 from = g.Center(BattleTestUtil.FreeHexNear(army.spawnPoint));
                var pads = new Vector3[bm.capturePoints.Length];
                for (int k = 0; k < bm.capturePoints.Length; k++)
                {
                    var cp = bm.capturePoints[k];
                    int open = 0;
                    HexCoord best = default;
                    float bestD = float.MaxValue;
                    foreach (var c in g.Cells)
                    {
                        if (!army.IsPadHex(c, cp) || !g.IsFree(c)) continue;
                        open++;
                        float d = (g.Center(c) - cp.transform.position).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    Assert.GreaterOrEqual(open, 6, $"Level {level} point {k + 1}: open spots on the pad");
                    pads[k] = g.Center(best);
                    Assert.IsTrue(nav.FindPath(from, pads[k], path), $"Level {level}: a path from the spawn to point {k + 1}");
                    Debug.Log($"[Test] Level {level} {map.displayName}: point {k + 1} reachable, {open} open pad spots, {path.Count} waypoints");
                }
                foreach (var h in map.spawnHatches)
                    for (int k = 0; k < pads.Length; k++)
                        Assert.IsTrue(nav.FindPath(h, pads[k], path), $"Level {level}: a path from the hatch at {h} to point {k + 1}");
                int sockets = 0;
                foreach (var list in new[] { map.squadSockets, map.defenseSockets, map.mineSockets })
                    foreach (var s in list)
                    {
                        Assert.IsTrue(nav.FindPath(from, g.Center(NearestFree(army, s)), path), $"Level {level}: the socket at {s} is reachable");
                        sockets++;
                    }
                Debug.Log($"[Test] Level {level}: {map.spawnHatches.Length} hatches and {sockets} sockets reachable");
                BattleTestUtil.Cleanup();
                yield return null;
            }
        }

        /// <summary>The bot, with the enemies removed, walks Level 4's bent route (spawn on the west side, circle 1 at
        /// the left pass's mouth, then back round the spine and up the right pass) and takes both circles: it follows
        /// the map's path, not an axis.</summary>
        [UnityTest]
        public IEnumerator BotFollowsTheBentPathOfTwinPasses()
        {
            yield return LoadLevel(3);
            BattleTestUtil.DisableEnemies();
            yield return null;
            Vector3 start = PlayerArmy.Instance.spawnPoint;
            Vector3 fwd = MapPath.Forward(start);
            Assert.Greater(fwd.x, 0.5f, "on Twin Passes the way ahead from the spawn points east, not north");
            var bot = new ArmyBot();
            yield return bot.Play(240f);
            string line = bot.Line("b04 no enemies");
            Debug.Log("[Bot] " + line);
            Assert.AreEqual(2, bot.PointsCaptured, "both circles taken: " + line);
            Assert.IsTrue(bot.Victory, line);
            Assert.AreEqual(0, bot.StuckEvents, "no unit stood stuck on its way: " + line);
        }

        // ------------------------------------------------------------------ captures

        static IEnumerator CaptureLevel(int index)
        {
            yield return LoadLevel(index);
            yield return BattleTestUtil.Wait(1f);
            BattleTestUtil.Capture("level_" + (index + 1) + "_start");
            yield return BattleTestUtil.CaptureTopDown("level_" + (index + 1) + "_topdown");
        }

        [UnityTest] public IEnumerator CaptureLevel1() { yield return CaptureLevel(0); }
        [UnityTest] public IEnumerator CaptureLevel2() { yield return CaptureLevel(1); }
        [UnityTest] public IEnumerator CaptureLevel3() { yield return CaptureLevel(2); }
        [UnityTest] public IEnumerator CaptureLevel4() { yield return CaptureLevel(3); }
        [UnityTest] public IEnumerator CaptureLevel5() { yield return CaptureLevel(4); }
    }
}
