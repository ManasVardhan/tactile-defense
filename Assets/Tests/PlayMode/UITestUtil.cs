using System.Collections;
using System.IO;
using TactileWars.Meta;
using TactileWars.Save;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TactileWars.Tests
{
    /// <summary>Helpers for meta and UI play mode tests: temporary save folder, scene waits, and captures of the
    /// scene camera (whose Screen Space Camera canvas renders the UI) into a render texture of any size.</summary>
    public static class UITestUtil
    {
        static RenderTexture rt;
        public static string SaveDir;

        public static void UseTempSave()
        {
            SaveDir = Path.Combine(Path.GetTempPath(), "tw_play_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(SaveDir);
            SaveSystem.DirectoryOverride = SaveDir;
            MetaGame.Db = MetaDatabase.Load();
        }

        public static void Cleanup()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SaveSystem.DirectoryOverride = null;
            BattleLaunch.Clear();
            MetaGame.BaseFeaturesEnabled = false; // a capture may switch the base features on
            if (SaveDir != null && Directory.Exists(SaveDir)) Directory.Delete(SaveDir, true);
            SaveDir = null;
            if (Camera.main != null) Camera.main.targetTexture = null;
        }

        public static IEnumerator WaitForScene(string name, float timeout = 20f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            yield return null;
        }

        public static IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        public static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Points the main camera at a render texture of the given size and lets the canvases re-layout.</summary>
        public static IEnumerator SetTarget(int w, int h)
        {
            var cam = Camera.main;
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { if (cam != null && cam.targetTexture == rt) cam.targetTexture = null; rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "UICapture" };
                rt.Create();
            }
            if (cam != null) cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
        }

        public static string Capture(string name)
        {
            var cam = Camera.main;
            if (cam == null || rt == null) { Debug.LogError("[Capture] no camera or target"); return null; }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
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
