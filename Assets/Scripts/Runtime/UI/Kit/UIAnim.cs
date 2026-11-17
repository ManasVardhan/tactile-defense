using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Small tween helper on unscaled time (menus keep animating while the battle is paused).
    /// Allocates only when an animation starts, never per frame.</summary>
    public static class UIAnim
    {
        class Runner : MonoBehaviour { }

        static Runner runner;
        static readonly Dictionary<int, Coroutine> keyed = new Dictionary<int, Coroutine>();

        static Runner Get()
        {
            if (runner == null)
            {
                var go = new GameObject("UIAnimRunner");
                runner = go.AddComponent<Runner>();
                keyed.Clear();
            }
            return runner;
        }

        public static Coroutine Run(IEnumerator e) => Get().StartCoroutine(e);

        /// <summary>Runs 'e', stopping any previous animation started with the same key object.</summary>
        public static Coroutine Run(UnityEngine.Object key, IEnumerator e)
        {
            var r = Get();
            int id = key != null ? key.GetInstanceID() : 0;
            if (keyed.TryGetValue(id, out var c) && c != null) r.StopCoroutine(c);
            var nc = r.StartCoroutine(e);
            keyed[id] = nc;
            return nc;
        }

        public static void Stop(UnityEngine.Object key)
        {
            if (runner == null || key == null) return;
            if (keyed.TryGetValue(key.GetInstanceID(), out var c) && c != null) runner.StopCoroutine(c);
            keyed.Remove(key.GetInstanceID());
        }

        /// <summary>Calls step(0..1) over 'duration' seconds of unscaled time. Stops quietly if 'owner' (when given)
        /// is destroyed, for example when a screen is rebuilt mid animation.</summary>
        public static IEnumerator Tween(float duration, Action<float> step, Func<float, float> ease = null, float delay = 0f, UnityEngine.Object owner = null)
        {
            bool owned = !ReferenceEquals(owner, null);
            while (delay > 0f) { delay -= Time.unscaledDeltaTime; yield return null; }
            if (owned && owner == null) yield break;
            float t = 0f;
            if (duration <= 0f) { step(1f); yield break; }
            while (t < duration)
            {
                if (owned && owner == null) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                step(ease != null ? ease(k) : k);
                yield return null;
            }
            if (owned && owner == null) yield break;
            step(1f);
        }

        public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float InCubic(float t) => t * t * t;
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        public static float OutElastic(float t)
        {
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        public static Coroutine Punch(Transform tr, float amount = 0.14f, float duration = 0.32f)
        {
            if (tr == null) return null;
            return Run(tr, Tween(duration, k =>
            {
                float s = 1f + Mathf.Sin(k * Mathf.PI) * amount * (1f - k * 0.5f);
                tr.localScale = new Vector3(s, s, 1f);
            }, null, 0f, tr));
        }

        public static Coroutine Pop(Transform tr, float from = 0.4f, float duration = 0.35f, float delay = 0f)
        {
            if (tr == null) return null;
            tr.localScale = Vector3.one * from;
            return Run(tr, Tween(duration, k =>
            {
                float s = Mathf.LerpUnclamped(from, 1f, OutBack(k));
                tr.localScale = new Vector3(s, s, 1f);
            }, null, delay, tr));
        }

        public static Coroutine Shake(RectTransform rt, float amount = 12f, float duration = 0.35f)
        {
            if (rt == null) return null;
            Vector2 basePos = rt.anchoredPosition;
            return Run(rt, Tween(duration, k =>
            {
                float off = Mathf.Sin(k * Mathf.PI * 7f) * amount * (1f - k);
                rt.anchoredPosition = basePos + new Vector2(off, 0f);
            }, null, 0f, rt));
        }

        public static Coroutine Fade(CanvasGroup cg, float to, float duration, float delay = 0f)
        {
            if (cg == null) return null;
            float from = cg.alpha;
            return Run(cg, Tween(duration, k => cg.alpha = Mathf.Lerp(from, to, k), null, delay, cg));
        }
    }
}
