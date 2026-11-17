using System;
using System.Collections.Generic;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Formation
{
    /// <summary>Turns pointer input into world space strokes on the ground plane, or taps.
    /// Gameplay listeners subscribe to the events; this class knows nothing about soldiers.</summary>
    [DefaultExecutionOrder(-100)]
    public class StrokeRecorder : MonoBehaviour
    {
        public FormationConfig config;
        public Camera cam;

        public event Action<List<Vector3>> StrokeBegan;
        public event Action<List<Vector3>> StrokeChanged;
        public event Action<List<Vector3>> StrokeEnded;
        public event Action<Vector3, Vector2> Tapped;
        public event Action StrokeCancelled;

        readonly List<Vector3> raw = new List<Vector3>(1024);
        bool pressing;
        bool isStroke;
        float pressTime;
        Vector2 pressScreen;
        float maxTravelPx;

        public bool IsDrawing => pressing && isStroke;
        public bool InputEnabled { get; set; } = true;
        public List<Vector3> RawPoints => raw;

        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || config == null) return;

            if (!InputEnabled)
            {
                if (pressing) { pressing = false; if (isStroke) StrokeCancelled?.Invoke(); isStroke = false; }
                return;
            }

            if (PointerInput.PressedThisFrame) Begin();

            if (pressing && PointerInput.IsDown) Track();

            if (pressing && PointerInput.ReleasedThisFrame) End();
        }

        void Begin()
        {
            pressing = true;
            isStroke = false;
            pressTime = Time.unscaledTime;
            pressScreen = PointerInput.Position;
            maxTravelPx = 0f;
            raw.Clear();
            if (PointerInput.ScreenToGround(cam, pressScreen, out Vector3 w)) raw.Add(w);
        }

        void Track()
        {
            Vector2 sp = PointerInput.Position;
            maxTravelPx = Mathf.Max(maxTravelPx, (sp - pressScreen).magnitude);
            if (!PointerInput.ScreenToGround(cam, sp, out Vector3 w)) return;
            if (raw.Count == 0) { raw.Add(w); return; }
            Vector3 last = raw[raw.Count - 1];
            float d = Vector3.Distance(last, w);
            if (d < config.sampleDistance) return;
            // Fill large jumps so fast drags still give even samples.
            int steps = Mathf.Min(16, Mathf.FloorToInt(d / config.sampleDistance));
            for (int i = 1; i <= steps && raw.Count < config.maxStrokePoints; i++)
                raw.Add(Vector3.Lerp(last, w, i / (float)steps));

            if (!isStroke && PathResampler.Length(raw, false) >= config.minStrokeLength && maxTravelPx > TapTravelPx())
            {
                isStroke = true;
                StrokeBegan?.Invoke(raw);
            }
            if (isStroke) StrokeChanged?.Invoke(raw);
        }

        float TapTravelPx() => config.tapMaxTravel * Mathf.Max(1, cam.pixelHeight);

        void End()
        {
            pressing = false;
            float dur = Time.unscaledTime - pressTime;
            if (isStroke)
            {
                isStroke = false;
                StrokeEnded?.Invoke(raw);
                return;
            }
            if (maxTravelPx <= TapTravelPx() * 2f && dur <= config.tapMaxDuration * 3f || PathResampler.Length(raw, false) < config.minStrokeLength)
            {
                Vector2 sp = PointerInput.Position;
                if (PointerInput.ScreenToGround(cam, sp, out Vector3 w)) Tapped?.Invoke(w, sp);
            }
        }
    }
}
