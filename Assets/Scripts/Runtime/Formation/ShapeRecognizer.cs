using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Formation
{
    public enum ShapeType { None, Line, Circle, Square, Free }

    public struct ShapeResult
    {
        public ShapeType type;
        public bool closed;
        public Vector3 center;
        public float radius;     // mean radius for closed shapes
        public int corners;
        public float straightness;
    }

    /// <summary>Classifies a drawn stroke as line, circle (ring), square or free shape.
    /// Thresholds are estimates (the source only says lines, circles and squares exist).</summary>
    public static class ShapeRecognizer
    {
        static readonly List<Vector3> buffer = new List<Vector3>(64);

        public struct Settings
        {
            public float closedThreshold;
            public float circleTolerance;
            public float cornerAngle;
            public float lineStraightness;

            public static Settings Default => new Settings
            {
                closedThreshold = 0.2f, circleTolerance = 0.16f, cornerAngle = 55f, lineStraightness = 0.93f
            };
        }

        public static bool IsClosed(List<Vector3> pts, float threshold)
        {
            if (pts.Count < 4) return false;
            float len = PathResampler.Length(pts, false);
            if (len < 1e-3f) return false;
            Vector3 d = pts[pts.Count - 1] - pts[0]; d.y = 0f;
            return d.magnitude <= threshold * len && len > 2f * d.magnitude;
        }

        public static ShapeResult Recognize(List<Vector3> raw, Settings s)
        {
            var r = new ShapeResult { type = ShapeType.None };
            if (raw == null || raw.Count < 2) return r;
            float len = PathResampler.Length(raw, false);
            if (len < 1e-3f) return r;

            Vector3 ends = raw[raw.Count - 1] - raw[0]; ends.y = 0f;
            r.straightness = ends.magnitude / len;
            r.closed = IsClosed(raw, s.closedThreshold);

            if (!r.closed)
            {
                r.type = r.straightness >= s.lineStraightness ? ShapeType.Line : ShapeType.Free;
                r.center = (raw[0] + raw[raw.Count - 1]) * 0.5f;
                return r;
            }

            PathResampler.ResampleCount(raw, true, 48, buffer);
            Vector3 c = PathResampler.Centroid(buffer);
            float mean = 0f;
            for (int i = 0; i < buffer.Count; i++) { Vector3 d = buffer[i] - c; d.y = 0f; mean += d.magnitude; }
            mean /= buffer.Count;
            float var = 0f;
            for (int i = 0; i < buffer.Count; i++)
            {
                Vector3 d = buffer[i] - c; d.y = 0f;
                float e = d.magnitude - mean;
                var += e * e;
            }
            float cv = mean > 1e-4f ? Mathf.Sqrt(var / buffer.Count) / mean : 1f;
            r.center = c;
            r.radius = mean;
            r.corners = CountCorners(buffer, s.cornerAngle);

            if (cv <= s.circleTolerance && r.corners < 3) r.type = ShapeType.Circle;
            else if (r.corners >= 3 && r.corners <= 5 && cv < 0.35f) r.type = ShapeType.Square;
            else if (cv <= s.circleTolerance) r.type = ShapeType.Circle;
            else r.type = ShapeType.Free;
            return r;
        }

        /// <summary>Counts sharp turns on a closed resampled loop using a +-2 sample window, merging neighbours.</summary>
        static int CountCorners(List<Vector3> loop, float cornerAngle)
        {
            int n = loop.Count;
            int corners = 0;
            int lastCorner = -100;
            int firstCorner = -1;
            const int w = 2;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = loop[(i - w + n) % n], b = loop[i], c = loop[(i + w) % n];
                Vector3 d1 = b - a, d2 = c - b; d1.y = 0f; d2.y = 0f;
                if (d1.sqrMagnitude < 1e-8f || d2.sqrMagnitude < 1e-8f) continue;
                float ang = Vector3.Angle(d1, d2);
                if (ang >= cornerAngle)
                {
                    if (i - lastCorner > w + 1)
                    {
                        corners++;
                        if (firstCorner < 0) firstCorner = i;
                    }
                    lastCorner = i;
                }
            }
            if (corners > 1 && firstCorner >= 0 && (firstCorner + n) - lastCorner <= w + 1) corners--;
            return corners;
        }
    }
}
