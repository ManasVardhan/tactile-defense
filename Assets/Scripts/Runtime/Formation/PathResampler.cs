using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Formation
{
    /// <summary>Pure polyline helpers on the XZ plane: Chaikin smoothing, arc length, resampling,
    /// sampling by distance and closest point queries. Callers pass reusable lists so no garbage is made.</summary>
    public static class PathResampler
    {
        public static float Length(List<Vector3> pts, bool closed)
        {
            float len = 0f;
            int n = pts.Count;
            for (int i = 1; i < n; i++) len += Dist(pts[i - 1], pts[i]);
            if (closed && n > 2) len += Dist(pts[n - 1], pts[0]);
            return len;
        }

        static float Dist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Chaikin corner cutting in place. Open paths keep their endpoints.</summary>
        public static void Chaikin(List<Vector3> pts, bool closed, int iterations, List<Vector3> scratch)
        {
            for (int it = 0; it < iterations; it++)
            {
                int n = pts.Count;
                if (n < 3) return;
                scratch.Clear();
                if (!closed) scratch.Add(pts[0]);
                int segs = closed ? n : n - 1;
                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = pts[i];
                    Vector3 b = pts[(i + 1) % n];
                    scratch.Add(a * 0.75f + b * 0.25f);
                    scratch.Add(a * 0.25f + b * 0.75f);
                }
                if (!closed) scratch.Add(pts[n - 1]);
                pts.Clear();
                for (int i = 0; i < scratch.Count; i++) pts.Add(scratch[i]);
            }
        }

        /// <summary>Resample to points spaced 'step' apart along the arc (last point kept for open paths).</summary>
        public static void ResampleBySpacing(List<Vector3> src, bool closed, float step, List<Vector3> dst)
        {
            dst.Clear();
            int n = src.Count;
            if (n == 0) return;
            if (n == 1 || step <= 1e-4f) { for (int i = 0; i < n; i++) dst.Add(src[i]); return; }
            float total = Length(src, closed);
            int count = Mathf.Max(2, Mathf.RoundToInt(total / step) + (closed ? 0 : 1));
            ResampleCount(src, closed, count, dst);
        }

        /// <summary>Resample to exactly 'count' points evenly spaced by arc length.</summary>
        public static void ResampleCount(List<Vector3> src, bool closed, int count, List<Vector3> dst)
        {
            dst.Clear();
            int n = src.Count;
            if (n == 0 || count <= 0) return;
            if (n == 1) { for (int i = 0; i < count; i++) dst.Add(src[0]); return; }
            float total = Length(src, closed);
            if (total < 1e-5f) { for (int i = 0; i < count; i++) dst.Add(src[0]); return; }
            float step = closed ? total / count : total / Mathf.Max(1, count - 1);
            int seg = 0;
            float segStart = 0f;
            int segCount = closed ? n : n - 1;
            for (int k = 0; k < count; k++)
            {
                float s = k * step;
                if (!closed && k == count - 1) { dst.Add(src[n - 1]); break; }
                while (seg < segCount)
                {
                    Vector3 a = src[seg], b = src[(seg + 1) % n];
                    float l = Dist(a, b);
                    if (s <= segStart + l || seg == segCount - 1)
                    {
                        float t = l > 1e-6f ? Mathf.Clamp01((s - segStart) / l) : 0f;
                        dst.Add(Vector3.LerpUnclamped(a, b, t));
                        break;
                    }
                    segStart += l;
                    seg++;
                }
            }
        }

        public static Vector3 Centroid(List<Vector3> pts)
        {
            Vector3 c = Vector3.zero;
            int n = pts.Count;
            if (n == 0) return c;
            for (int i = 0; i < n; i++) c += pts[i];
            return c / n;
        }

        /// <summary>Signed area on XZ (positive when counter clockwise seen from above).</summary>
        public static float SignedArea(List<Vector3> pts)
        {
            float a = 0f;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = pts[i], q = pts[(i + 1) % n];
                a += p.x * q.z - q.x * p.z;
            }
            return a * 0.5f;
        }
    }

    /// <summary>Polyline with cached cumulative lengths for O(log n) sampling by arc length.</summary>
    public class PolylinePath
    {
        public readonly List<Vector3> Points;
        readonly List<float> cumulative;
        public bool Closed { get; private set; }
        public float Length { get; private set; }

        public PolylinePath(int capacity = 256)
        {
            Points = new List<Vector3>(capacity);
            cumulative = new List<float>(capacity + 1);
        }

        public void Set(List<Vector3> src, bool closed)
        {
            Points.Clear();
            for (int i = 0; i < src.Count; i++) Points.Add(src[i]);
            Closed = closed && src.Count > 2;
            Rebuild();
        }

        public void Translate(Vector3 delta)
        {
            for (int i = 0; i < Points.Count; i++) Points[i] += delta;
        }

        public void Rebuild()
        {
            cumulative.Clear();
            float acc = 0f;
            cumulative.Add(0f);
            int n = Points.Count;
            int segs = Closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 a = Points[i], b = Points[(i + 1) % n];
                float dx = a.x - b.x, dz = a.z - b.z;
                acc += Mathf.Sqrt(dx * dx + dz * dz);
                cumulative.Add(acc);
            }
            Length = acc;
        }

        public int SegmentCount => Mathf.Max(0, cumulative.Count - 1);

        /// <summary>Position and unit tangent at arc length s (wrapped for closed paths, clamped otherwise).</summary>
        public void Sample(float s, out Vector3 pos, out Vector3 tangent)
        {
            int n = Points.Count;
            if (n == 0) { pos = Vector3.zero; tangent = Vector3.forward; return; }
            if (n == 1 || Length < 1e-6f) { pos = Points[0]; tangent = Vector3.forward; return; }
            if (Closed) { s %= Length; if (s < 0f) s += Length; }
            else s = Mathf.Clamp(s, 0f, Length);
            int lo = 0, hi = SegmentCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (cumulative[mid] <= s) lo = mid; else hi = mid - 1;
            }
            Vector3 a = Points[lo], b = Points[(lo + 1) % n];
            float l = cumulative[lo + 1] - cumulative[lo];
            float t = l > 1e-6f ? (s - cumulative[lo]) / l : 0f;
            pos = Vector3.LerpUnclamped(a, b, t);
            Vector3 d = b - a; d.y = 0f;
            tangent = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        /// <summary>Arc length parameter of the closest point on the path to p.</summary>
        public float ClosestParam(Vector3 p)
        {
            int n = Points.Count;
            if (n < 2) return 0f;
            float best = float.MaxValue, bestS = 0f;
            int segs = SegmentCount;
            for (int i = 0; i < segs; i++)
            {
                Vector3 a = Points[i], b = Points[(i + 1) % n];
                float abx = b.x - a.x, abz = b.z - a.z;
                float apx = p.x - a.x, apz = p.z - a.z;
                float len2 = abx * abx + abz * abz;
                float t = len2 > 1e-8f ? Mathf.Clamp01((apx * abx + apz * abz) / len2) : 0f;
                float cx = a.x + abx * t - p.x, cz = a.z + abz * t - p.z;
                float d2 = cx * cx + cz * cz;
                if (d2 < best)
                {
                    best = d2;
                    bestS = cumulative[i] + t * (cumulative[i + 1] - cumulative[i]);
                }
            }
            return bestS;
        }
    }
}
