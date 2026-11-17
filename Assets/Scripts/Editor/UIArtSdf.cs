using System;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>Signed distance helpers for procedural UI art. Coordinates are normalized: x and y in [-1, 1],
    /// y up. Negative distance = inside.</summary>
    public static class Sdf
    {
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float round = 0f)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(round, round);
            Vector2 m = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return m.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude - r;
        }

        /// <summary>Exact signed distance to a simple polygon (Inigo Quilez).</summary>
        public static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(1e-9f, Vector2.Dot(e, e)));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float RoundPoly(Vector2 p, Vector2[] v, float round) => Polygon(p, v) - round;

        public static float Ring(Vector2 p, Vector2 c, float r, float halfWidth) => Mathf.Abs((p - c).magnitude - r) - halfWidth;

        /// <summary>Ring segment between angles a0..a1 (degrees, counter clockwise from +x) with round caps.</summary>
        public static float Arc(Vector2 p, Vector2 c, float r, float halfWidth, float a0, float a1)
        {
            Vector2 q = p - c;
            float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
            float span = Mathf.Repeat(a1 - a0, 360f);
            float rel = Mathf.Repeat(ang - a0, 360f);
            if (rel <= span) return Mathf.Abs(q.magnitude - r) - halfWidth;
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - halfWidth;
        }

        public static Vector2[] RegularPoly(Vector2 c, float r, int n, float rotDeg)
        {
            var v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (rotDeg + i * 360f / n) * Mathf.Deg2Rad;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }

        public static Vector2[] Star(Vector2 c, float outer, float inner, int points, float rotDeg = 90f)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float a = (rotDeg + i * 180f / points) * Mathf.Deg2Rad;
                float r = (i % 2 == 0) ? outer : inner;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }

        public static Vector2 Rotate(Vector2 p, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
        }

        public static float Union(float a, float b) => Mathf.Min(a, b);
        public static float Sub(float a, float b) => Mathf.Max(a, -b);
        public static float Inter(float a, float b) => Mathf.Max(a, b);
        public static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }
    }

    /// <summary>RGBA float raster with anti aliased SDF layers composited "over".</summary>
    public class SdfCanvas
    {
        public readonly int W, H;
        public readonly Color[] Px;
        readonly float aa;      // one pixel in normalized units
        readonly float aspect;  // W / H

        public SdfCanvas(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
            aa = 2f / Mathf.Min(w, h);
            aspect = w / (float)h;
        }

        /// <summary>Normalized coordinate of a pixel: y in [-1, 1]; x in [-aspect, aspect].</summary>
        public Vector2 P(int x, int y) => new Vector2(((x + 0.5f) / W * 2f - 1f) * aspect, (y + 0.5f) / H * 2f - 1f);

        public void Layer(Func<Vector2, float> sdf, Color color, float expand = 0f, float softness = 1f)
        {
            float w = aa * softness;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = sdf(P(x, y)) - expand;
                    float a = Mathf.Clamp01(0.5f - d / w);
                    if (a <= 0f) continue;
                    Over(y * W + x, color, a);
                }
        }

        public void LayerFn(Func<Vector2, float> sdf, Func<Vector2, Color> color, float expand = 0f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = P(x, y);
                    float d = sdf(p) - expand;
                    float a = Mathf.Clamp01(0.5f - d / aa);
                    if (a <= 0f) continue;
                    Over(y * W + x, color(p), a);
                }
        }

        /// <summary>Soft drop shadow of a shape.</summary>
        public void Shadow(Func<Vector2, float> sdf, Vector2 offset, float blur, Color color)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = sdf(P(x, y) - offset);
                    float a = Mathf.Clamp01(0.5f - d / blur);
                    a = a * a * (3f - 2f * a);
                    if (a <= 0f) continue;
                    Over(y * W + x, color, a);
                }
        }

        void Over(int i, Color c, float cover)
        {
            float a = c.a * cover;
            var dst = Px[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 1e-5f) { Px[i] = Color.clear; return; }
            Color o = (c * a + dst * dst.a * (1f - a)) / outA;
            o.a = outA;
            Px[i] = o;
        }

        /// <summary>Replace the raster with a function of position (for gradients and glows).</summary>
        public void Fill(Func<Vector2, Color> f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Px[y * W + x] = f(P(x, y));
        }
    }
}
