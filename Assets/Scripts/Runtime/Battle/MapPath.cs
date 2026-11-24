using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>
    /// The active battlefield's main path (MapLayoutData.path: a polyline from the player's spawn past both capture
    /// points to the enemy HQ), as the one sense of "forward" for everything that needs it: the camera start, the
    /// tutorial's "up the path" suggestions and the test bot. Progress is the distance along the path of the nearest
    /// point on it; Forward its direction there. Set by BattleSetup; with no path it is the old straight lane
    /// (progress = z, forward = +z), so nothing assumes an axis.
    /// </summary>
    public static class MapPath
    {
        static Vector2[] points;
        static float[] cumulative;

        /// <summary>The active path, or null for the straight default.</summary>
        public static Vector2[] Points => points;

        public static void Set(Vector2[] path)
        {
            if (path == null || path.Length < 2) { points = null; cumulative = null; return; }
            points = (Vector2[])path.Clone();
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        public static void Clear() { points = null; cumulative = null; }

        /// <summary>Total length of the path (0 for the straight default).</summary>
        public static float Length => cumulative != null ? cumulative[cumulative.Length - 1] : 0f;

        /// <summary>Distance along the path of the point on it nearest 'p' (z on the straight default).</summary>
        public static float Progress(Vector3 p)
        {
            if (points == null) return p.z;
            Nearest(new Vector2(p.x, p.z), out int seg, out float t, out _);
            return cumulative[seg] + (cumulative[seg + 1] - cumulative[seg]) * t;
        }

        /// <summary>Unit direction of travel along the path at its point nearest 'p' (+z on the straight default).</summary>
        public static Vector3 Forward(Vector3 p)
        {
            if (points == null) return Vector3.forward;
            Nearest(new Vector2(p.x, p.z), out int seg, out _, out _);
            Vector2 d = points[seg + 1] - points[seg];
            if (d.sqrMagnitude < 1e-6f) return Vector3.forward;
            d.Normalize();
            return new Vector3(d.x, 0f, d.y);
        }

        /// <summary>Distance from 'p' to the path (0 on the straight default).</summary>
        public static float DistanceTo(Vector3 p)
        {
            if (points == null) return 0f;
            Nearest(new Vector2(p.x, p.z), out _, out _, out float d);
            return d;
        }

        /// <summary>The point at distance 's' along the path (clamped), on the ground.</summary>
        public static Vector3 PointAt(float s)
        {
            if (points == null) return new Vector3(0f, 0f, s);
            s = Mathf.Clamp(s, 0f, Length);
            for (int i = 1; i < points.Length; i++)
                if (s <= cumulative[i] || i == points.Length - 1)
                {
                    float len = cumulative[i] - cumulative[i - 1];
                    float t = len > 1e-5f ? (s - cumulative[i - 1]) / len : 0f;
                    Vector2 q = Vector2.Lerp(points[i - 1], points[i], Mathf.Clamp01(t));
                    return new Vector3(q.x, 0f, q.y);
                }
            return Vector3.zero;
        }

        static void Nearest(Vector2 p, out int seg, out float t, out float dist)
        {
            seg = 0; t = 0f; dist = float.MaxValue;
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 a = points[i - 1], ab = points[i] - a;
                float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, a + ab * tt);
                if (d < dist) { dist = d; seg = i - 1; t = tt; }
            }
        }
    }
}
