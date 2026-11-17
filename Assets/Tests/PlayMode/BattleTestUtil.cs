using System.Collections;
using System.IO;
using TactileWars.Battle;
using TactileWars.CameraRig;
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

        public static void Cleanup()
        {
            PointerInput.Source = null;
            if (Cam != null) Cam.targetTexture = null;
        }

        public static Camera Cam => BattleCamera.Instance != null ? BattleCamera.Instance.Cam : Camera.main;
        public static SquadController Squad => SquadController.Instance;

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
