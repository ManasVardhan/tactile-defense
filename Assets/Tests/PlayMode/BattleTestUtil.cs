using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using System.IO;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TactileWars.Tests
{
    /// <summary>Loads the battle, drives the simulated pointer, and renders the main camera to PNG.</summary>
    public static class BattleTestUtil
    {
        public const int W = 1280, H = 720;
        static RenderTexture rt;
        public static SimulatedPointerSource Pointer;

        public static IEnumerator LoadBattle()
        {
            // Phase 6: battles save meta progress; keep tests away from the real save and skip the tutorial overlay.
            UseTestSave();
            Pointer = new SimulatedPointerSource();
            PointerInput.Source = Pointer;
            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
            var cam = Cam;
            if (rt == null)
            {
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "TestCapture" };
                rt.Create();
            }
            // Render into a fixed 1280x720 target so screen coordinates are stable in batchmode.
            cam.targetTexture = rt;
            yield return null;
        }

        static string testSaveDir;

        public static void UseTestSave()
        {
            if (TactileWars.Save.SaveSystem.DirectoryOverride != null) return;
            testSaveDir = Path.Combine(Path.GetTempPath(), "tw_battle_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testSaveDir);
            TactileWars.Save.SaveSystem.DirectoryOverride = testSaveDir;
            var p = TactileWars.Meta.MetaGame.Profile;
            p.teamChosen = true;
            MarkTutorialSeen(p);
        }

        /// <summary>Marks every tutorial step and hint as seen, so battles start without the Level 1 tutorial.</summary>
        public static void MarkTutorialSeen(TactileWars.Save.PlayerProfile p)
        {
            p.tutorialSeen ??= new List<string>();
            foreach (var id in TactileWars.UI.TutorialSteps.All)
                if (!p.tutorialSeen.Contains(id)) p.tutorialSeen.Add(id);
        }

        public static void Cleanup()
        {
            if (testSaveDir != null)
            {
                TactileWars.Save.SaveSystem.DirectoryOverride = null;
                try { Directory.Delete(testSaveDir, true); } catch (System.Exception) { }
                testSaveDir = null;
            }
            PointerInput.Source = null;
            if (Cam != null) Cam.targetTexture = null;
        }

        public static Camera Cam => BattleCamera.Instance != null ? BattleCamera.Instance.Cam : Camera.main;
        public static SquadController Squad => SquadController.Instance;
        public static PlayerArmy Army => PlayerArmy.Instance;

        /// <summary>Free hex nearest 'p' with no enemy soldier close by, at least two hexes from every hex in 'avoid'.</summary>
        public static HexCoord FreeHexNear(Vector3 p, params HexCoord[] avoid)
        {
            var g = Army.Grid;
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c) || UnitRegistry.CountWithin(Team.Enemy, g.Center(c), g.OuterRadius * 3f) > 0) continue;
                bool near = false;
                foreach (var a in avoid) if (HexGrid.Distance(a, c) < 2) { near = true; break; }
                if (near) continue;
                float d = (g.Center(c) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "a free hex exists");
            return best;
        }

        /// <summary>Deploys every ready entry of the army's squad on free hexes near 'p' (default: the army's spawn).</summary>
        public static List<PlayerUnit> DeployAll(Vector3? p = null)
        {
            var army = Army;
            Vector3 at = p ?? army.spawnPoint;
            var units = new List<PlayerUnit>();
            var used = new List<HexCoord>();
            for (int i = 0; i < army.Squad.Count; i++)
            {
                if (army.Squad[i].state != EntryState.Ready) continue;
                var hex = FreeHexNear(at, used.ToArray());
                var u = army.Deploy(i, hex);
                Assert.IsNotNull(u, "deploy entry " + i);
                used.Add(hex);
                units.Add(u);
            }
            return units;
        }

        /// <summary>Living soldiers of every deployed unit.</summary>
        public static List<TactileWars.Units.Soldier> ArmySoldiers()
        {
            var list = new List<TactileWars.Units.Soldier>();
            foreach (var u in Army.Units)
                foreach (var s in u.Soldiers) if (s != null && s.IsAlive) list.Add(s);
            return list;
        }

        /// <summary>The free hex nearest the pad whose center lies on it (the pole blocks the hexes at the very center).</summary>
        public static HexCoord PadHex(CapturePoint cp)
        {
            var g = Army.Grid;
            float radius = BattleManager.Instance.battleConfig.captureRadius;
            HexCoord hex = default;
            float best = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c)) continue;
                Vector3 d = g.Center(c) - cp.transform.position; d.y = 0f;
                if (d.magnitude <= radius && d.magnitude < best) { best = d.magnitude; hex = c; }
            }
            Assert.Less(best, float.MaxValue, "a free hex on the pad");
            return hex;
        }

        /// <summary>A hex overlapping the pad that 'unit' may stand on (free or its own), nearest the pad center,
        /// skipping 'taken'.</summary>
        public static HexCoord PadHexFree(CapturePoint cp, PlayerUnit unit, List<HexCoord> taken)
        {
            var army = Army;
            var g = army.Grid;
            Vector3 c0 = cp.transform.position; c0.y = 0f;
            HexCoord hex = unit.Hex;
            float best = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (taken.Contains(c) || !army.IsPadHex(c, cp)) continue;
                if (!g.IsFree(c) && g.OwnerOf(c) != unit.Id) continue;
                float d = (g.Center(c) - c0).sqrMagnitude;
                if (d < best) { best = d; hex = c; }
            }
            return hex;
        }

        /// <summary>Spawns an enemy squad under the battle's enemy root ('count' grunts, plus a flag bearer when asked).
        /// Its soldiers appear on its Start, the next frame.</summary>
        public static TactileWars.Units.EnemySquad SpawnEnemySquad(Vector3 at, int count, bool bearer, bool passive, bool advance = false)
        {
            var setup = BattleSetup.Instance;
            var g = new GameObject("TestSquad");
            g.transform.SetParent(setup.enemiesRoot, false);
            g.transform.position = at;
            var es = g.AddComponent<TactileWars.Units.EnemySquad>();
            es.config = setup.enemySquadConfig;
            es.soldierCountOverride = count;
            es.withFlagBearer = bearer;
            es.passive = passive;
            es.advance = advance;
            es.seed = 7;
            return es;
        }

        /// <summary>Switches off every handcrafted enemy squad and defense (call before the army's grid is first used).</summary>
        public static void DisableEnemies()
        {
            var setup = BattleSetup.Instance;
            foreach (var root in new[] { setup.enemiesRoot, setup.defensesRoot })
                for (int i = root.childCount - 1; i >= 0; i--) root.GetChild(i).gameObject.SetActive(false);
        }

        /// <summary>Captures the whole active map from straight above (north up, HUD hidden, a margin of the border
        /// around it), then puts the battle camera and the HUD back.</summary>
        public static IEnumerator CaptureTopDown(string name)
        {
            var bc = BattleCamera.Instance;
            var gc = BattleContext.Config;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bc.enabled = false;
            var cam = bc.Cam;
            Vector3 camPos = bc.transform.position;
            Quaternion camRot = bc.transform.rotation;
            float size = cam.orthographicSize;
            float far = cam.farClipPlane;
            Vector2 min = gc.mapMin - Vector2.one * 5f, max = gc.mapMax + Vector2.one * 5f;
            Vector3 mid = new Vector3((min.x + max.x) * 0.5f, 120f, (min.y + max.y) * 0.5f);
            float aspect = (float)W / H;
            cam.orthographicSize = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspect);
            cam.farClipPlane = 260f;
            bc.transform.SetPositionAndRotation(mid, Quaternion.Euler(90f, 0f, 0f));
            foreach (var c in canvases) c.enabled = false;
            yield return null;
            Capture(name);
            foreach (var c in canvases) c.enabled = true;
            bc.transform.SetPositionAndRotation(camPos, camRot);
            cam.orthographicSize = size;
            cam.farClipPlane = far;
            bc.enabled = true;
            yield return null;
        }

        /// <summary>Waits until the unit has stopped and every living soldier stands within the hex.</summary>
        public static IEnumerator WaitArrive(PlayerUnit unit, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && unit != null && !unit.IsDestroyed && (unit.IsMoving || !AllWithin(unit, unit.Hex))) yield return null;
        }

        public static bool AllWithin(PlayerUnit unit, HexCoord hex)
        {
            var g = Army.Grid;
            Vector3 c = g.Center(hex);
            foreach (var s in unit.Soldiers)
            {
                if (s == null || !s.IsAlive) continue;
                Vector3 d = s.transform.position - c; d.y = 0f;
                if (d.magnitude > g.OuterRadius) return false;
            }
            return true;
        }

        public static void KillAll(PlayerUnit unit)
        {
            foreach (var s in unit.Soldiers) if (s != null && s.IsAlive) s.Health.Kill(TeamColorId.Teal);
        }

        public static Vector2 ToScreen(Vector3 world)
        {
            var p = Cam.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        public static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Press, drag through the world points over 'seconds', release.</summary>
        public static IEnumerator Drag(Vector3[] world, float seconds, bool release = true, float stopAtFraction = 1f)
        {
            Pointer.Down = false;
            Pointer.Position = ToScreen(world[0]);
            yield return null;
            Pointer.Down = true;
            yield return null;
            float t = 0f;
            while (t < seconds * stopAtFraction)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / seconds) * (world.Length - 1);
                int i = Mathf.Min(world.Length - 2, Mathf.FloorToInt(u));
                Vector3 w = Vector3.Lerp(world[i], world[i + 1], u - i);
                Pointer.Position = ToScreen(w);
                yield return null;
            }
            if (release)
            {
                Pointer.Down = false;
                yield return null;
            }
        }

        /// <summary>Press at a screen point, move in a straight line to another over 'frames' frames, release.
        /// Screen space, so a panning camera does not change the pointer path.</summary>
        public static IEnumerator DragScreen(Vector2 from, Vector2 to, int frames = 20)
        {
            Pointer.Down = false;
            Pointer.Position = from;
            yield return null;
            Pointer.Down = true;
            yield return null;
            for (int i = 1; i <= frames; i++)
            {
                Pointer.Position = Vector2.Lerp(from, to, i / (float)frames);
                yield return null;
            }
            Pointer.Down = false;
            yield return null;
        }

        public static IEnumerator Tap(Vector3 world)
        {
            Pointer.Position = ToScreen(world);
            Pointer.Down = false;
            yield return null;
            Pointer.Down = true;
            yield return null;
            yield return null;
            Pointer.Down = false;
            yield return null;
        }

        /// <summary>A quick click: press and release inside a single frame (as a fast browser click or trackpad tap
        /// arrives at a low frame rate).</summary>
        public static IEnumerator QuickTap(Vector3 world)
        {
            Pointer.Down = false;
            Pointer.Position = ToScreen(world);
            yield return null;
            Pointer.Click(ToScreen(world));
            yield return null;
            yield return null;
        }

        public static Vector3[] LinePoints(Vector3 a, Vector3 b, int n = 24)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++) p[i] = Vector3.Lerp(a, b, i / (float)(n - 1));
            return p;
        }

        public static Vector3[] CirclePoints(Vector3 c, float r, int n = 48, float startDeg = -90f)
        {
            var p = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = (startDeg + i * 360f / n) * Mathf.Deg2Rad;
                p[i] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            }
            return p;
        }

        /// <summary>Renders the main camera (with post) into the capture target and writes a PNG.</summary>
        public static string Capture(string name)
        {
            var cam = Cam;
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Screenshots"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[Capture] wrote " + path);
            return path;
        }
    }
}
