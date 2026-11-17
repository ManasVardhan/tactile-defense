using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>Procedural textures for decals and effects (no external textures). Encoding for TW/Decal:
    /// R = tone mix (0 core color, 1 base color), G = shading, A = alpha.</summary>
    public static class TextureGen
    {
        public const string Dir = "Assets/Art/Textures";

        public static Texture2D Write(string name, int w, int h, Func<float, float, Color> f, bool srgb = false, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = true, int supersample = 2)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color[w * h];
            float inv = 1f / supersample;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < supersample; sy++)
                        for (int sx = 0; sx < supersample; sx++)
                        {
                            float u = (x + (sx + 0.5f) * inv) / w;
                            float v = (y + (sy + 0.5f) * inv) / h;
                            acc += f(u, v);
                        }
                    px[y * w + x] = acc / (supersample * supersample);
                }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = mips;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 2;
            ti.maxTextureSize = Mathf.Max(w, h);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float Hash(float n) => Frac(Mathf.Sin(n) * 43758.5453f);
        static float Frac(float x) => x - Mathf.Floor(x);

        // ---------- splats ----------

        struct Drop { public float x, y, r; }

        /// <summary>2 x 2 atlas of paint splats: a lobed blob with satellite drops and a glossy highlight.</summary>
        public static Texture2D SplatAtlas()
        {
            var cells = new SplatShape[4];
            for (int i = 0; i < 4; i++) cells[i] = new SplatShape(17 + i * 31, i == 0);
            return Write("T_SplatAtlas", 1024, 1024, (u, v) =>
            {
                int cx = u < 0.5f ? 0 : 1, cy = v < 0.5f ? 0 : 1;
                float lu = (u - cx * 0.5f) * 2f, lv = (v - cy * 0.5f) * 2f;
                return cells[cy * 2 + cx].Sample(lu * 2f - 1f, lv * 2f - 1f);
            }, false, TextureWrapMode.Clamp, true, 1);
        }

        class SplatShape
        {
            readonly float[] amp = new float[7], phase = new float[7];
            readonly Drop[] drops;
            readonly Drop[] lobes;   // overlapping sub blobs: clumpy paint like the reference splats (ps_12)
            readonly float baseR;

            public SplatShape(int seed, bool big)
            {
                var rng = new System.Random(seed);
                baseR = big ? 0.46f : 0.36f;
                for (int k = 0; k < 7; k++)
                {
                    amp[k] = (float)rng.NextDouble() * (k < 2 ? 0.16f : 0.07f);
                    phase[k] = (float)rng.NextDouble() * 6.283f;
                }
                int nl = 3 + rng.Next(3);
                lobes = new Drop[nl];
                for (int i = 0; i < nl; i++)
                {
                    float a = (float)rng.NextDouble() * 6.283f;
                    float d = baseR * (0.55f + (float)rng.NextDouble() * 0.45f);
                    float r = baseR * (0.32f + (float)rng.NextDouble() * 0.28f);
                    lobes[i] = new Drop { x = Mathf.Cos(a) * d, y = Mathf.Sin(a) * d, r = r };
                }
                int n = big ? 14 : 9 + rng.Next(4);
                drops = new Drop[n];
                for (int i = 0; i < n; i++)
                {
                    float a = (float)rng.NextDouble() * 6.283f;
                    float d = baseR * (1.15f + (float)rng.NextDouble() * 0.6f);
                    float r = 0.015f + (float)rng.NextDouble() * 0.05f;
                    if (d + r > 0.97f) d = 0.97f - r;
                    drops[i] = new Drop { x = Mathf.Cos(a) * d, y = Mathf.Sin(a) * d, r = r };
                }
            }

            public Color Sample(float x, float y)
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x);
                float rr = baseR;
                for (int k = 0; k < 7; k++) rr += baseR * amp[k] * Mathf.Sin((k + 3) * a + phase[k]);
                float aa = 0.012f;
                float body = 1f - Smooth(rr - aa, rr + aa, r);
                float edge = Smooth(rr * 0.55f, rr, r);
                // Smooth union with the lobes (metaball style) so the outline is a clump, not a disc.
                float field = Mathf.Exp(-Mathf.Max(0f, r - rr) * 30f);
                for (int i = 0; i < lobes.Length; i++)
                {
                    float lx = x - lobes[i].x, ly = y - lobes[i].y;
                    float ld = Mathf.Sqrt(lx * lx + ly * ly);
                    field += Mathf.Exp(-Mathf.Max(0f, ld - lobes[i].r) * 30f);
                }
                body = Mathf.Max(body, Smooth(0.95f, 1.05f, field));
                float alpha = body;
                for (int i = 0; i < drops.Length; i++)
                {
                    float dx = x - drops[i].x, dy = y - drops[i].y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Max(alpha, 1f - Smooth(drops[i].r - aa, drops[i].r + aa, d));
                }
                // Gloss: soft highlight up-left, slightly darker rim.
                float hx = x + baseR * 0.3f, hy = y - baseR * 0.3f;
                float hl = Mathf.Exp(-(hx * hx + hy * hy) / (baseR * baseR * 0.12f));
                float g = 0.86f - edge * 0.12f + hl * 0.14f;
                return new Color(1f, Mathf.Clamp01(g), 0f, alpha * 0.96f);
            }
        }

        // ---------- squad ring and spiral blot ----------

        public static Texture2D Ring()
        {
            return Write("T_SquadRing", 1024, 1024, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x);
                float thick = 0.048f + 0.012f * Mathf.Sin(a * 3f + 1.3f) + 0.006f * Mathf.Sin(a * 7f + 0.2f);
                float center = 0.95f - thick + 0.006f * Mathf.Sin(a * 5f + 2f);
                float d = Mathf.Abs(r - center);
                float aa = 0.004f;
                float alpha = 1f - Smooth(thick - aa, thick + aa, d);
                float shade = 0.92f + 0.08f * Mathf.Sin(a * 11f);
                return new Color(1f, shade, 0f, alpha * 0.95f);
            }, false, TextureWrapMode.Clamp, true, 1);
        }

        public static Texture2D SpiralBlot()
        {
            var shape = new SplatShape(5, true);
            return Write("T_SpiralBlot", 512, 512, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                Color s = shape.Sample(x * 0.95f, y * 0.95f);
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x) / (2f * Mathf.PI);
                // Archimedean spiral grooves like a thumbprint.
                float spiral = Frac(r * 9f - a);
                float groove = Smooth(0.0f, 0.18f, spiral) * (1f - Smooth(0.42f, 0.6f, spiral));
                float inside = 1f - Smooth(0.36f, 0.46f, r);
                float g = Mathf.Lerp(s.g, 0.5f, groove * inside);
                return new Color(1f, g, 0f, s.a);
            }, false, TextureWrapMode.Clamp, true, 2);
        }

        // ---------- stroke ----------

        public static Texture2D Dash()
        {
            return Write("T_StrokeDash", 128, 32, (u, v) =>
            {
                // Rounded dash (capsule) occupying about 55 percent of the period, in pixel space.
                float px = u * 128f, py = v * 32f;
                float cx = Mathf.Clamp(px, 14f, 62f);
                float dx = px - cx, dy = py - 16f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Smooth(10f, 12.5f, d);
                return new Color(1f, 1f, 0f, alpha);
            }, false, TextureWrapMode.Repeat, true, 2);
        }

        public static Texture2D Glow()
        {
            return Write("T_StrokeGlow", 32, 64, (u, v) =>
            {
                float y = (v - 0.5f) / 0.24f;
                float a = Mathf.Exp(-y * y);
                return new Color(1f, 1f, 0f, a);
            }, false, TextureWrapMode.Clamp, true, 1);
        }

        // ---------- dots, circles, effects ----------

        public static Texture2D Dot()
        {
            return Write("T_PaintDot", 64, 64, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float alpha = 1f - Smooth(0.86f, 0.98f, r);
                float rim = Smooth(0.42f, 0.62f, r);
                return new Color(rim, 1f, 0f, alpha);
            }, false, TextureWrapMode.Clamp, true, 2);
        }

        public static Texture2D SoftCircle()
        {
            return Write("T_SoftCircle", 128, 128, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
                return new Color(1f, 1f, 1f, a);
            }, false, TextureWrapMode.Clamp, true, 1);
        }

        public static Texture2D Puff()
        {
            var blobs = new Vector3[] { new Vector3(0f, 0f, 0.55f), new Vector3(-0.32f, -0.1f, 0.42f), new Vector3(0.33f, -0.05f, 0.4f), new Vector3(0.05f, 0.3f, 0.42f), new Vector3(-0.2f, 0.25f, 0.35f), new Vector3(0.25f, 0.28f, 0.33f) };
            return Write("T_Puff", 128, 128, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float a = 0f, shade = 0f;
                foreach (var b in blobs)
                {
                    float dx = x - b.x, dy = y - b.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / b.z;
                    float m = 1f - Smooth(0.85f, 1f, d);
                    if (m > a) { a = m; shade = 1f - Smooth(0.2f, 1f, d) * 0.25f + (dy > 0 ? 0.05f : -0.05f); }
                }
                return new Color(shade, shade, shade, a);
            }, true, TextureWrapMode.Clamp, true, 2);
        }

        public static Texture2D Ghost()
        {
            return Write("T_Ghost", 128, 128, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                // Helmet dome head on top, wispy tapering body below.
                float hx = x, hy = y - 0.25f;
                float head = 1f - Smooth(0.5f, 0.56f, Mathf.Sqrt(hx * hx + hy * hy * 1.3f));
                float bodyW = Mathf.Lerp(0.42f, 0.05f, Smooth(0.1f, -0.95f, y)) + 0.06f * Mathf.Sin(y * 9f);
                float body = (1f - Smooth(bodyW - 0.04f, bodyW + 0.04f, Mathf.Abs(x + 0.08f * Mathf.Sin(y * 4f)))) * Smooth(-1f, -0.2f, y) * (1f - Smooth(0.2f, 0.35f, y));
                float a = Mathf.Max(head, body * Mathf.Lerp(0.3f, 1f, Smooth(-0.95f, 0.2f, y)));
                // Two dark eye dots.
                float e1 = Mathf.Sqrt((x + 0.16f) * (x + 0.16f) + (y - 0.18f) * (y - 0.18f));
                float e2 = Mathf.Sqrt((x - 0.16f) * (x - 0.16f) + (y - 0.18f) * (y - 0.18f));
                float eye = 1f - Smooth(0.06f, 0.09f, Mathf.Min(e1, e2));
                float c = 1f - eye * 0.75f;
                return new Color(c, c, c, a);
            }, true, TextureWrapMode.Clamp, true, 2);
        }

        /// <summary>Gold star coin (victory coin burst; reference f_0037, f_0077 star coin tokens).</summary>
        public static Texture2D Coin()
        {
            return Write("T_Coin", 64, 64, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x) + Mathf.PI * 0.5f;
                float alpha = 1f - Smooth(0.9f, 0.98f, r);
                // Five point star: radius oscillates between inner and outer.
                float k = Mathf.Cos(5f * a);
                float starR = Mathf.Lerp(0.26f, 0.55f, (k + 1f) * 0.5f);
                float star = 1f - Smooth(starR - 0.04f, starR + 0.04f, r);
                float rim = Smooth(0.72f, 0.8f, r);
                Color gold = new Color(1f, 0.78f, 0.22f), dark = new Color(0.78f, 0.5f, 0.1f), light = new Color(1f, 0.93f, 0.6f);
                Color c = Color.Lerp(gold, dark, rim);
                c = Color.Lerp(c, dark, star * 0.85f);
                c = Color.Lerp(c, light, Mathf.Clamp01(0.4f - (x + y) * 0.6f) * (1f - rim) * 0.5f);
                c.a = alpha;
                return c;
            }, true, TextureWrapMode.Clamp, true, 2);
        }

        public static Texture2D Star()
        {
            return Write("T_Star", 128, 128, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x);
                float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 6f);
                float reach = Mathf.Lerp(0.35f, 0.98f, rays);
                float m = 1f - Smooth(reach * 0.8f, reach, r);
                float core = 1f - Smooth(0.25f, 0.4f, r);
                return new Color(1f, 1f, 1f, Mathf.Max(m, core));
            }, true, TextureWrapMode.Clamp, true, 2);
        }
    }
}
