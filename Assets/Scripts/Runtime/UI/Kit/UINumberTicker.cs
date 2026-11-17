using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Counts a TMP label up or down to a target value. Uses TMP SetText with a format string, which does
    /// not allocate, so ticking every frame is garbage free.</summary>
    public class UINumberTicker : MonoBehaviour
    {
        public TMP_Text label;
        public string format = "{0}";
        public float duration = 0.8f;
        float from, to, t = 1f, delay;
        public int Value => Mathf.RoundToInt(to);
        public bool Running => t < 1f;
        public System.Action Finished;

        public void SetInstant(int v)
        {
            from = to = v; t = 1f;
            if (label != null) label.SetText(format, v);
        }

        public void TickTo(int v, float dur = -1f, float startDelay = 0f)
        {
            from = Mathf.Lerp(from, to, Ease(t));
            to = v;
            t = 0f;
            delay = startDelay;
            duration = dur > 0f ? dur : duration;
            enabled = true;
        }

        static float Ease(float k) => 1f - (1f - k) * (1f - k);

        void Update()
        {
            if (t >= 1f) { enabled = false; return; }
            if (delay > 0f) { delay -= Time.unscaledDeltaTime; return; }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            float v = Mathf.Lerp(from, to, Ease(t));
            if (label != null) label.SetText(format, Mathf.Round(v));
            if (t >= 1f) Finished?.Invoke();
        }
    }
}
