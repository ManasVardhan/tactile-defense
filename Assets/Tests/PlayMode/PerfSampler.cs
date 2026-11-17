using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>Phase 8 frame sampler shared by the perf captures. Removes the frame cap (targetFrameRate -1, vSync off,
    /// otherwise every frame reads as 16.67 ms) and records per frame: wall time including one explicit camera render
    /// (batchmode presents nothing on its own), the profiler "Main Thread" time, GC allocation, and the render counters
    /// (batches, SetPass calls, draw calls, triangles) plus texture and total memory. Editor numbers only.</summary>
    public class PerfSampler
    {
        public string Report { get; private set; }

        static string Stats(List<double> v, string unit, string fmt = "0.00")
        {
            if (v.Count == 0) return "unavailable";
            v.Sort();
            double avg = 0; foreach (var x in v) avg += x; avg /= v.Count;
            return $"avg {avg.ToString(fmt)} {unit}, median {v[v.Count / 2].ToString(fmt)} {unit}, p95 {v[(int)(v.Count * 0.95f)].ToString(fmt)} {unit}, max {v[v.Count - 1].ToString(fmt)} {unit}";
        }

        public IEnumerator Run(Camera cam, int frames, System.Action perFrame = null)
        {
            int keepFps = Application.targetFrameRate, keepVsync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            for (int i = 0; i < 10; i++) yield return null; // settle after uncapping

            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
            var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            var texMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1);
            var totalMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);

            var wall = new List<double>(frames);
            var main = new List<double>(frames);
            var gcs = new List<double>(frames);
            var bat = new List<double>(frames);
            var sp = new List<double>(frames);
            var dc = new List<double>(frames);
            var tr = new List<double>(frames);
            var sw = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < frames; i++)
            {
                sw.Restart();
                cam.Render();
                yield return null;
                sw.Stop();
                wall.Add(sw.Elapsed.TotalMilliseconds);
                if (mainThread.Valid && mainThread.LastValue > 0) main.Add(mainThread.LastValue * 1e-6);
                if (gc.Valid) gcs.Add(gc.LastValue);
                if (batches.Valid && batches.LastValue > 0) bat.Add(batches.LastValue);
                if (setpass.Valid && setpass.LastValue > 0) sp.Add(setpass.LastValue);
                if (draws.Valid && draws.LastValue > 0) dc.Add(draws.LastValue);
                if (tris.Valid && tris.LastValue > 0) tr.Add(tris.LastValue);
                perFrame?.Invoke();
            }
            double tex = texMem.Valid ? texMem.LastValue / (1024.0 * 1024.0) : -1;
            double total = totalMem.Valid ? totalMem.LastValue / (1024.0 * 1024.0) : -1;
            foreach (var r in new[] { mainThread, gc, batches, setpass, draws, tris, texMem, totalMem }) r.Dispose();
            Application.targetFrameRate = keepFps;
            QualitySettings.vSyncCount = keepVsync;

            var sb = new StringBuilder();
            sb.AppendLine($"Frame cap removed for the window (Application.targetFrameRate -1, vSync off; previous {keepFps} / {keepVsync}). {frames} frames.");
            sb.AppendLine("Wall time per frame incl. one extra camera render: " + Stats(wall, "ms"));
            sb.AppendLine("Profiler 'Main Thread' (CPU main thread): " + Stats(main, "ms"));
            sb.AppendLine("GC allocated per frame (editor play mode, includes editor and test overhead): " + Stats(gcs, "bytes", "0"));
            sb.AppendLine("Batches: " + Stats(bat, "", "0") + " | SetPass calls: " + Stats(sp, "", "0") + " | Draw calls: " + Stats(dc, "", "0"));
            sb.AppendLine("Triangles: " + Stats(tr, "", "0"));
            sb.AppendLine($"Texture memory: {(tex >= 0 ? tex.ToString("0.0") + " MB" : "unavailable")}, total used memory (editor process view): {(total >= 0 ? total.ToString("0") + " MB" : "unavailable")}");
            sb.AppendLine("Note: the render counters cover every camera render in the frame; batchmode has no game view, so that is the explicit cam.Render() of the scene camera (the HUD canvas renders with it).");
            Report = sb.ToString();
        }
    }
}
