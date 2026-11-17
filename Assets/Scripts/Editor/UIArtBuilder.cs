using System;
using System.Collections.Generic;
using System.IO;
using TactileWars.Core;
using TactileWars.UI;
using UnityEditor;
using UnityEngine;
using static TactileWars.EditorTools.Sdf;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Generates all UI art into Assets/Art/UI: 9 sliced shapes (rounded panels, pills, tab, ribbon, shadows),
    /// an icon family drawn with signed distance shapes (white or colored fills, shared dark ink outline and soft
    /// drop shadow), and renders of the real models (soldier busts and full bodies, hero busts, defenses, HQ) in
    /// every team color through UIArtRenderer. Writes Assets/Data/UISkin.asset.
    /// </summary>
    public static partial class UIArtBuilder
    {
        public const string Dir = "Assets/Art/UI";
        public const string ShapesDir = Dir + "/Shapes";
        public const string IconsDir = Dir + "/Icons";
        public const string SkinPath = "Assets/Data/UISkin.asset";
        const int IconSize = 192;
        const float OL = 0.075f;

        static readonly Color Ink = H("#2A2D3A"), W = Color.white, Gold = H("#F6B12B"), GoldD = H("#C27A14"), GoldL = H("#FFD978"),
            Red = H("#E04A2F"), RedD = H("#A02D1C"), Blue = H("#3E8EDB"), BlueL = H("#86C6F7"), Green = H("#76BD36"),
            Xp = H("#3FB4E5"), XpL = H("#A5E4FF"), Steel = H("#8E97A8"), SteelL = H("#E3E7EE"), SteelD = H("#5F6878"),
            Brown = H("#8A5A33"), Cream = H("#FFF6E6"), Pink = H("#F07AB4"), Yellow = H("#FFD34A"), Dark = H("#5A3A10");

        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color A(Color c, float a) { c.a = a; return c; }

        public static UISkin Build(FontBuilder.Result fonts)
        {
            Directory.CreateDirectory(ShapesDir);
            Directory.CreateDirectory(IconsDir);
            var skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UISkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            if (fonts != null)
            {
                skin.headingFont = fonts.heading;
                skin.bodyFont = fonts.body;
                skin.headingOutline = fonts.headingOutline;
                skin.headingOutlineThin = fonts.headingOutlineThin;
                skin.bodyOutline = fonts.bodyOutline;
            }
            BuildShapes(skin);
            BuildIcons(skin);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            // Rendering opens a fresh scene, which unloads in-memory objects: reload the skin afterwards.
            var renders = UIArtRenderer.RenderAll();
            skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
            skin.busts = renders.busts;
            skin.soldiers = renders.soldiers;
            skin.heroes = renders.heroes;
            skin.renders = renders.named;
            skin.palette = AssetDatabase.LoadAssetAtPath<TeamPalette>("Assets/Data/TeamPalette.asset");
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] UI art built: " + skin.icons.Length + " icons, " + skin.renders.Length + " renders");
            return skin;
        }

        // ------------------------------------------------------------------ IO

        public static Sprite Save(string path, int w, int h, Color[] px, Vector4 border, FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.spriteBorder = border;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.filterMode = filter;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 1024;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite Shape(string name, int w, int h, Action<SdfCanvas> draw, Vector4 border)
        {
            var c = new SdfCanvas(w, h);
            draw(c);
            return Save(ShapesDir + "/" + name + ".png", w, h, c.Px, border);
        }

        // ------------------------------------------------------------------ shapes

        static void BuildShapes(UISkin s)
        {
            s.rounded16 = Shape("rounded16", 64, 64, c => c.Layer(p => Box(p, Vector2.zero, new Vector2(0.985f, 0.985f), 0.5f), W), new Vector4(18, 18, 18, 18));
            s.rounded32 = Shape("rounded32", 128, 128, c => c.Layer(p => Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), 0.625f), W), new Vector4(44, 44, 44, 44));
            s.pill = Shape("pill", 128, 128, c => c.Layer(p => Circle(p, Vector2.zero, 0.99f), W), new Vector4(63, 63, 63, 63));
            s.pillShine = Shape("pill_shine", 128, 128, c => c.LayerFn(
                p => Inter(Circle(p, Vector2.zero, 0.86f), 0.12f - p.y),
                p => A(W, Mathf.Lerp(0.15f, 0.9f, Mathf.InverseLerp(0.1f, 0.85f, p.y)))), new Vector4(63, 63, 63, 63));
            s.circle = Shape("circle", 256, 256, c => c.Layer(p => Circle(p, Vector2.zero, 0.985f), W), Vector4.zero);
            s.circleShine = Shape("circle_shine", 256, 256, c => c.LayerFn(
                p => Sub(Circle(p, Vector2.zero, 0.84f), Circle(p, new Vector2(0f, -0.32f), 0.98f)),
                p => A(W, Mathf.Lerp(0.2f, 0.85f, Mathf.InverseLerp(0.2f, 0.8f, p.y)))), Vector4.zero);
            s.ring = Shape("ring", 256, 256, c => c.Layer(p => Ring(p, Vector2.zero, 0.865f, 0.12f), W), Vector4.zero);
            s.ringThin = Shape("ring_thin", 256, 256, c => c.Layer(p => Ring(p, Vector2.zero, 0.955f, 0.033f), W), Vector4.zero);
            var hex = RegularPoly(Vector2.zero, 0.9f, 6, 90f);
            s.hex = Shape("hex", 256, 256, c => c.Layer(p => RoundPoly(p, hex, 0.07f), W), Vector4.zero);
            var hexIn = RegularPoly(Vector2.zero, 0.88f, 6, 90f);
            s.hexRing = Shape("hex_ring", 256, 256, c => c.Layer(p => Mathf.Abs(RoundPoly(p, hexIn, 0.06f)) - 0.035f, W), Vector4.zero);
            s.tab = Shape("tab", 128, 128, c => c.Layer(p =>
            {
                float r = (p.x > 0f && p.y < 0f) ? 0.75f : 0.16f;
                return Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), r);
            }, W), new Vector4(52, 52, 52, 52));
            s.softShadow = Shape("soft_shadow", 128, 128, c => c.Shadow(p => Box(p, Vector2.zero, new Vector2(0.5f, 0.5f), 0.35f), Vector2.zero, 0.42f, W), new Vector4(60, 60, 60, 60));
            s.glow = Shape("glow", 256, 256, c => c.Fill(p => { float r = Mathf.Clamp01(p.magnitude); float a = Mathf.Pow(1f - r, 2.2f); return A(W, a); }), Vector4.zero);
            s.splat = Shape("splat", 512, 512, DrawSplat, Vector4.zero);
            s.ribbon = Shape("ribbon", 512, 128, DrawRibbon, new Vector4(100, 0, 100, 0));
            s.gradientV = Shape("gradient_v", 8, 128, c => c.Fill(p => A(W, (p.y + 1f) * 0.5f)), Vector4.zero);
            s.dash = Shape("dash", 64, 16, c => c.Layer(p => Box(p, new Vector2(-1.2f, 0f), new Vector2(2.2f, 0.62f), 0.6f), W), Vector4.zero);
            var rosette = Star(Vector2.zero, 0.98f, 0.86f, 18);
            s.medal = Shape("medal", 256, 256, c => c.Layer(p => RoundPoly(p, rosette, 0.015f), W), Vector4.zero);
            s.white = Shape("white", 8, 8, c => c.Fill(p => W), Vector4.zero);
        }

        static void DrawSplat(SdfCanvas c)
        {
            var rng = new System.Random(77);
            var blobs = new List<Vector3>();
            blobs.Add(new Vector3(0f, 0f, 0.5f));
            for (int i = 0; i < 11; i++)
            {
                float a = i / 11f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                float d = 0.36f + (float)rng.NextDouble() * 0.22f;
                blobs.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.16f + (float)rng.NextDouble() * 0.14f));
            }
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = 0.72f + (float)rng.NextDouble() * 0.2f;
                blobs.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.035f + (float)rng.NextDouble() * 0.05f));
            }
            c.Layer(p =>
            {
                float d = 99f;
                for (int i = 0; i < blobs.Count; i++)
                {
                    var b = blobs[i];
                    float di = Circle(p, new Vector2(b.x, b.y), b.z);
                    d = i < 12 ? SmoothUnion(d, di, 0.12f) : Mathf.Min(d, di);
                }
                return d;
            }, W);
        }

        static void DrawRibbon(SdfCanvas c)
        {
            // Normalized x spans [-4, 4] (64 px per unit), y [-1, 1].
            Color tail = new Color(0.7f, 0.7f, 0.7f, 1f), fold = new Color(0.45f, 0.45f, 0.45f, 1f);
            var lt = new[] { new Vector2(-3.98f, 0.46f), new Vector2(-2.9f, 0.46f), new Vector2(-2.9f, -0.94f), new Vector2(-3.98f, -0.94f), new Vector2(-3.5f, -0.24f) };
            var rt = new Vector2[lt.Length];
            for (int i = 0; i < lt.Length; i++) rt[i] = new Vector2(-lt[lt.Length - 1 - i].x, lt[lt.Length - 1 - i].y);
            c.Layer(p => RoundPoly(p, lt, 0.03f), tail);
            c.Layer(p => RoundPoly(p, rt, 0.03f), tail);
            var lf = new[] { new Vector2(-3.24f, -0.66f), new Vector2(-2.9f, -0.66f), new Vector2(-2.9f, -0.94f) };
            var rf = new[] { new Vector2(2.9f, -0.94f), new Vector2(2.9f, -0.66f), new Vector2(3.24f, -0.66f) };
            c.Layer(p => Polygon(p, lf), fold);
            c.Layer(p => Polygon(p, rf), fold);
            c.LayerFn(p => Box(p, new Vector2(0f, 0.1f), new Vector2(3.26f, 0.78f), 0.2f),
                p => Color.Lerp(new Color(0.86f, 0.86f, 0.86f, 1f), W, Mathf.InverseLerp(-0.55f, -0.35f, p.y)));
        }

        // ------------------------------------------------------------------ icons

        struct Part
        {
            public Func<Vector2, float> sdf;
            public Color color;
            public Func<Vector2, Color> colorFn;
            public bool noOutline;   // decorative detail that should not grow the silhouette
        }

        static Part P(Func<Vector2, float> f, Color c) => new Part { sdf = f, color = c };
        static Part PF(Func<Vector2, float> f, Func<Vector2, Color> c) => new Part { sdf = f, colorFn = c };
        static Part D(Func<Vector2, float> f, Color c) => new Part { sdf = f, color = c, noOutline = true };

        static Sprite Icon(string name, params Part[] parts)
        {
            var c = new SdfCanvas(IconSize, IconSize);
            Func<Vector2, float> union = p =>
            {
                float d = 99f;
                for (int i = 0; i < parts.Length; i++) if (!parts[i].noOutline) d = Mathf.Min(d, parts[i].sdf(p));
                return d;
            };
            c.Shadow(p => union(p) - OL, new Vector2(0f, -0.07f), 0.07f, A(Ink, 0.4f));
            c.Layer(union, Ink, OL);
            foreach (var part in parts)
            {
                if (part.colorFn != null) c.LayerFn(part.sdf, part.colorFn);
                else c.Layer(part.sdf, part.color);
            }
            return Save(IconsDir + "/icon_" + name + ".png", IconSize, IconSize, c.Px, Vector4.zero);
        }

        static Func<Vector2, Color> VGrad(Color top, Color bottom, float y0 = -0.8f, float y1 = 0.8f) =>
            p => Color.Lerp(bottom, top, Mathf.InverseLerp(y0, y1, p.y));

        static void BuildIcons(UISkin skin)
        {
            int n = Enum.GetValues(typeof(UIIcon)).Length;
            var icons = new Sprite[n];
            void Set(UIIcon i, Sprite s) => icons[(int)i] = s;
            var z = Vector2.zero;

            var coinStar = Star(new Vector2(0f, 0.01f), 0.42f, 0.19f, 5);
            Set(UIIcon.Coin, Icon("coin",
                P(p => Circle(p, z, 0.82f), GoldD),
                PF(p => Circle(p, new Vector2(0f, 0.04f), 0.7f), VGrad(GoldL, Gold, -0.2f, 0.7f)),
                D(p => RoundPoly(p, coinStar, 0.03f), Dark),
                D(p => Arc(p, new Vector2(0f, 0.04f), 0.57f, 0.045f, 110f, 160f), A(W, 0.7f))));

            var hexV = RegularPoly(z, 0.84f, 6, 90f);
            Color[] facet = { H("#F06EB0"), H("#9A6BD8"), H("#4CB6E8"), H("#7CCB52"), H("#F7D84A"), H("#F7A23A") };
            var prismParts = new List<Part> { P(p => RoundPoly(p, hexV, 0.03f), facet[0]) };
            for (int i = 0; i < 6; i++)
            {
                var tri = new[] { z, hexV[i], hexV[(i + 1) % 6] };
                Color col = facet[i];
                prismParts.Add(D(p => Polygon(p, tri) - 0.008f, col));
            }
            var hexIn = RegularPoly(new Vector2(0f, 0.02f), 0.36f, 6, 90f);
            prismParts.Add(D(p => RoundPoly(p, hexIn, 0.02f), A(W, 0.92f)));
            Set(UIIcon.Prism, Icon("prism", prismParts.ToArray()));

            var xpStar = Star(z, 0.86f, 0.44f, 5);
            var xpIn = Star(new Vector2(0f, 0.05f), 0.56f, 0.28f, 5);
            Set(UIIcon.Xp, Icon("xp", P(p => RoundPoly(p, xpStar, 0.05f), Xp), D(p => RoundPoly(p, xpIn, 0.04f), XpL), D(p => Circle(p, new Vector2(-0.12f, 0.2f), 0.07f), W)));

            Func<Vector2, float> dome = p => Inter(Circle(p, new Vector2(0f, 0.02f), 0.62f), -0.04f - p.y);
            Set(UIIcon.Soldier, Icon("soldier",
                P(p => Inter(Circle(p, new Vector2(0f, -0.3f), 0.42f), p.y + 0.05f - 0.0f), W),
                P(dome, H("#5A6A8C")),
                P(p => Box(p, new Vector2(0f, -0.07f), new Vector2(0.72f, 0.09f), 0.08f), H("#45526E")),
                D(p => Mathf.Min(Circle(p, new Vector2(-0.15f, -0.33f), 0.075f), Circle(p, new Vector2(0.15f, -0.33f), 0.075f)), Ink)));

            Set(UIIcon.Pause, Icon("pause", P(p => Box(p, new Vector2(-0.27f, 0f), new Vector2(0.15f, 0.58f), 0.08f), W), P(p => Box(p, new Vector2(0.27f, 0f), new Vector2(0.15f, 0.58f), 0.08f), W)));

            Func<Vector2, float> gear = p =>
            {
                float d = Circle(p, z, 0.6f);
                for (int k = 0; k < 8; k++) d = Mathf.Min(d, Box(Rotate(p, -k * 45f), new Vector2(0f, 0.68f), new Vector2(0.15f, 0.17f), 0.05f));
                return Sub(d, Circle(p, z, 0.24f));
            };
            Set(UIIcon.Gear, Icon("gear", PF(gear, VGrad(W, SteelL))));

            var speaker = new[] { new Vector2(-0.64f, 0.2f), new Vector2(-0.34f, 0.2f), new Vector2(0.04f, 0.56f), new Vector2(0.04f, -0.56f), new Vector2(-0.34f, -0.2f), new Vector2(-0.64f, -0.2f) };
            Set(UIIcon.Sound, Icon("sound", P(p => RoundPoly(p, speaker, 0.06f), W),
                P(p => Arc(p, new Vector2(0.04f, 0f), 0.4f, 0.07f, -45f, 45f), W), P(p => Arc(p, new Vector2(0.04f, 0f), 0.68f, 0.07f, -50f, 50f), W)));

            var beam = new[] { new Vector2(-0.14f, 0.62f), new Vector2(0.72f, 0.84f), new Vector2(0.72f, 0.58f), new Vector2(-0.14f, 0.36f) };
            Set(UIIcon.Music, Icon("music",
                P(p => Circle(p, new Vector2(-0.33f, -0.48f), 0.26f), W), P(p => Circle(p, new Vector2(0.45f, -0.3f), 0.26f), W),
                P(p => Box(p, new Vector2(-0.12f, 0.12f), new Vector2(0.065f, 0.6f)), W), P(p => Box(p, new Vector2(0.65f, 0.26f), new Vector2(0.065f, 0.58f)), W),
                P(p => Polygon(p, beam), W)));

            Set(UIIcon.Lock, Icon("lock",
                P(p => Mathf.Min(Arc(p, new Vector2(0f, 0.2f), 0.33f, 0.09f, 0f, 180f), Mathf.Min(Box(p, new Vector2(-0.33f, 0.1f), new Vector2(0.09f, 0.12f)), Box(p, new Vector2(0.33f, 0.1f), new Vector2(0.09f, 0.12f)))), SteelL),
                PF(p => Box(p, new Vector2(0f, -0.3f), new Vector2(0.56f, 0.42f), 0.14f), VGrad(GoldL, Gold, -0.7f, 0.1f)),
                D(p => Mathf.Min(Circle(p, new Vector2(0f, -0.22f), 0.11f), Box(p, new Vector2(0f, -0.4f), new Vector2(0.05f, 0.14f), 0.03f)), Dark)));

            Set(UIIcon.Check, Icon("check", P(p => Mathf.Min(Segment(p, new Vector2(-0.55f, 0.02f), new Vector2(-0.16f, -0.38f), 0.16f), Segment(p, new Vector2(-0.16f, -0.38f), new Vector2(0.58f, 0.46f), 0.16f)), W)));

            Func<Vector2, float> arrowR = p => Mathf.Min(Segment(p, new Vector2(-0.6f, 0f), new Vector2(0.15f, 0f), 0.17f), RoundPoly(p, new[] { new Vector2(0.02f, 0.56f), new Vector2(0.66f, 0f), new Vector2(0.02f, -0.56f) }, 0.07f));
            Set(UIIcon.ArrowRight, Icon("arrow_right", P(arrowR, W)));
            Set(UIIcon.ArrowLeft, Icon("arrow_left", P(p => arrowR(new Vector2(-p.x, p.y)), W)));
            Set(UIIcon.ArrowUp, Icon("arrow_up", P(p => arrowR(new Vector2(p.y, -p.x)), W)));

            var blade = new[] { new Vector2(-0.13f, -0.1f), new Vector2(0.13f, -0.1f), new Vector2(0.13f, 0.6f), new Vector2(0f, 0.9f), new Vector2(-0.13f, 0.6f) };
            Func<Vector2, Vector2> sw = p => Rotate(p, 45f) + new Vector2(0f, 0.04f);
            Set(UIIcon.Sword, Icon("sword",
                P(p => RoundPoly(sw(p), blade, 0.02f), H("#E3E8EF")),
                D(p => Box(sw(p), new Vector2(0.04f, 0.3f), new Vector2(0.03f, 0.38f)), H("#B9C3CF")),
                P(p => Box(sw(p), new Vector2(0f, -0.42f), new Vector2(0.075f, 0.2f), 0.03f), Brown),
                P(p => Box(sw(p), new Vector2(0f, -0.16f), new Vector2(0.36f, 0.075f), 0.05f), Gold),
                P(p => Circle(sw(p), new Vector2(0f, -0.68f), 0.11f), Gold)));

            var shield = new[] { new Vector2(-0.62f, 0.66f), new Vector2(0.62f, 0.66f), new Vector2(0.62f, 0.08f), new Vector2(0f, -0.84f), new Vector2(-0.62f, 0.08f) };
            var shieldIn = new Vector2[shield.Length];
            for (int i = 0; i < shield.Length; i++) shieldIn[i] = shield[i] * 0.68f + new Vector2(0f, 0.04f);
            var shStar = Star(new Vector2(0f, 0.12f), 0.28f, 0.13f, 5);
            Set(UIIcon.Shield, Icon("shield", P(p => RoundPoly(p, shield, 0.08f), Blue), D(p => RoundPoly(p, shieldIn, 0.06f), BlueL), D(p => RoundPoly(p, shStar, 0.02f), W)));

            Set(UIIcon.Flask, Icon("flask",
                P(p => Box(p, new Vector2(0f, 0.46f), new Vector2(0.15f, 0.26f), 0.04f), H("#EAF6FF")),
                P(p => Box(p, new Vector2(0f, 0.72f), new Vector2(0.25f, 0.075f), 0.05f), SteelL),
                P(p => Circle(p, new Vector2(0f, -0.28f), 0.52f), H("#EAF6FF")),
                D(p => Inter(Circle(p, new Vector2(0f, -0.28f), 0.44f), p.y + 0.18f), Green),
                D(p => Mathf.Min(Circle(p, new Vector2(0.14f, -0.36f), 0.075f), Circle(p, new Vector2(-0.12f, -0.52f), 0.05f)), A(W, 0.85f)),
                D(p => Arc(p, new Vector2(0f, -0.28f), 0.36f, 0.04f, 115f, 160f), A(W, 0.9f))));

            var spike = new[] { new Vector2(-0.16f, 0.42f), new Vector2(0f, 0.94f), new Vector2(0.16f, 0.42f) };
            Set(UIIcon.Mercenary, Icon("mercenary",
                P(p => RoundPoly(p, spike, 0.03f), Gold),
                PF(p => Inter(Circle(p, new Vector2(0f, -0.08f), 0.62f), -0.1f - p.y), VGrad(SteelL, Steel, -0.1f, 0.5f)),
                P(p => Box(p, new Vector2(0f, -0.14f), new Vector2(0.76f, 0.11f), 0.08f), SteelD),
                D(p => Box(p, new Vector2(0f, 0.12f), new Vector2(0.42f, 0.085f), 0.07f), Ink),
                D(p => Mathf.Min(Circle(p, new Vector2(-0.17f, 0.12f), 0.045f), Circle(p, new Vector2(0.17f, 0.12f), 0.045f)), W),
                P(p => Box(p, new Vector2(0f, -0.45f), new Vector2(0.3f, 0.22f), 0.12f), H("#45526E"))));

            var roof = new[] { new Vector2(-0.8f, 0.08f), new Vector2(0f, 0.74f), new Vector2(0.8f, 0.08f) };
            Set(UIIcon.Home, Icon("home",
                P(p => Box(p, new Vector2(0.43f, 0.52f), new Vector2(0.09f, 0.18f), 0.02f), RedD),
                P(p => Box(p, new Vector2(0f, -0.32f), new Vector2(0.55f, 0.45f), 0.06f), Cream),
                P(p => RoundPoly(p, roof, 0.06f), Red),
                D(p => Box(p, new Vector2(0f, -0.5f), new Vector2(0.15f, 0.27f), 0.07f), Brown)));

            var cup = new[] { new Vector2(-0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.42f, 0.2f), new Vector2(0.18f, -0.08f), new Vector2(-0.18f, -0.08f), new Vector2(-0.42f, 0.2f) };
            var tStar = Star(new Vector2(0f, 0.36f), 0.2f, 0.09f, 5);
            Set(UIIcon.Trophy, Icon("trophy",
                P(p => Mathf.Min(Ring(p, new Vector2(-0.52f, 0.42f), 0.2f, 0.065f), Ring(p, new Vector2(0.52f, 0.42f), 0.2f, 0.065f)), GoldD),
                P(p => Box(p, new Vector2(0f, -0.26f), new Vector2(0.09f, 0.2f)), GoldD),
                PF(p => RoundPoly(p, cup, 0.06f), VGrad(GoldL, Gold, -0.1f, 0.7f)),
                P(p => Box(p, new Vector2(0f, -0.6f), new Vector2(0.4f, 0.12f), 0.06f), Gold),
                D(p => RoundPoly(p, tStar, 0.02f), W)));

            Set(UIIcon.Close, Icon("close", P(p => Mathf.Min(Segment(p, new Vector2(-0.46f, -0.46f), new Vector2(0.46f, 0.46f), 0.17f), Segment(p, new Vector2(-0.46f, 0.46f), new Vector2(0.46f, -0.46f), 0.17f)), W)));
            Set(UIIcon.Plus, Icon("plus", P(p => Mathf.Min(Segment(p, new Vector2(-0.55f, 0f), new Vector2(0.55f, 0f), 0.17f), Segment(p, new Vector2(0f, -0.55f), new Vector2(0f, 0.55f), 0.17f)), W)));
            Set(UIIcon.Minus, Icon("minus", P(p => Segment(p, new Vector2(-0.55f, 0f), new Vector2(0.55f, 0f), 0.17f), W)));

            var flag = new[] { new Vector2(-0.44f, 0.8f), new Vector2(0.68f, 0.6f), new Vector2(0.42f, 0.36f), new Vector2(0.68f, 0.12f), new Vector2(-0.44f, 0.18f) };
            Set(UIIcon.Flag, Icon("flag",
                P(p => Segment(p, new Vector2(-0.5f, -0.84f), new Vector2(-0.5f, 0.78f), 0.075f), SteelL),
                P(p => RoundPoly(p, flag, 0.04f), W),
                P(p => Circle(p, new Vector2(-0.5f, 0.84f), 0.1f), Gold)));

            var nose = new[] { new Vector2(-0.08f, -0.18f), new Vector2(0.08f, -0.18f), new Vector2(0f, -0.05f) };
            Set(UIIcon.Skull, Icon("skull",
                P(p => SmoothUnion(Circle(p, new Vector2(0f, 0.12f), 0.6f), Box(p, new Vector2(0f, -0.4f), new Vector2(0.36f, 0.22f), 0.1f), 0.08f), W),
                D(p => Mathf.Min(Circle(p, new Vector2(-0.23f, 0.04f), 0.16f), Circle(p, new Vector2(0.23f, 0.04f), 0.16f)), Ink),
                D(p => RoundPoly(p, nose, 0.02f), Ink),
                D(p => Mathf.Min(Box(p, new Vector2(-0.12f, -0.5f), new Vector2(0.03f, 0.1f)), Box(p, new Vector2(0.12f, -0.5f), new Vector2(0.03f, 0.1f))), A(Ink, 0.8f))));

            Set(UIIcon.Clock, Icon("clock",
                P(p => Circle(p, z, 0.82f), Blue),
                D(p => Circle(p, z, 0.63f), W),
                D(p => Mathf.Min(Segment(p, z, new Vector2(0f, 0.42f), 0.065f), Segment(p, z, new Vector2(0.3f, -0.08f), 0.065f)), Ink),
                D(p => Circle(p, z, 0.08f), Red)));

            Func<Vector2, Vector2> hand = p => Rotate(p, 14f);
            Set(UIIcon.Finger, Icon("finger",
                P(p => Box(hand(p), new Vector2(0.12f, -0.86f), new Vector2(0.34f, 0.12f), 0.06f), Blue),
                P(p => Segment(hand(p), new Vector2(-0.3f, -0.32f), new Vector2(-0.48f, -0.04f), 0.14f), Cream),
                P(p => Box(hand(p), new Vector2(0.1f, -0.42f), new Vector2(0.37f, 0.34f), 0.2f), Cream),
                P(p => Mathf.Min(Segment(hand(p), new Vector2(0.1f, -0.06f), new Vector2(0.1f, 0.1f), 0.13f), Segment(hand(p), new Vector2(0.3f, -0.1f), new Vector2(0.3f, 0.02f), 0.12f)), Cream),
                P(p => Segment(hand(p), new Vector2(-0.1f, -0.1f), new Vector2(-0.1f, 0.72f), 0.16f), Cream)));

            Func<Vector2, Vector2> pen = p => Rotate(p, 45f);
            var penTip = new[] { new Vector2(-0.17f, -0.48f), new Vector2(0.17f, -0.48f), new Vector2(0f, -0.84f) };
            var penLead = new[] { new Vector2(-0.06f, -0.72f), new Vector2(0.06f, -0.72f), new Vector2(0f, -0.84f) };
            Set(UIIcon.Pencil, Icon("pencil",
                P(p => Box(pen(p), new Vector2(0f, 0.04f), new Vector2(0.17f, 0.54f)), Gold),
                P(p => RoundPoly(pen(p), penTip, 0.01f), Cream),
                D(p => Polygon(pen(p), penLead), Ink),
                P(p => Box(pen(p), new Vector2(0f, 0.7f), new Vector2(0.17f, 0.12f), 0.05f), Pink),
                P(p => Box(pen(p), new Vector2(0f, 0.55f), new Vector2(0.17f, 0.045f)), Steel)));

            var bigStar = Star(z, 0.88f, 0.43f, 5);
            var bigStarIn = Star(new Vector2(0f, 0.05f), 0.56f, 0.27f, 5);
            Set(UIIcon.Star, Icon("star", P(p => RoundPoly(p, bigStar, 0.05f), Gold), D(p => RoundPoly(p, bigStarIn, 0.04f), GoldL)));

            var play = new[] { new Vector2(-0.42f, 0.62f), new Vector2(0.66f, 0f), new Vector2(-0.42f, -0.62f) };
            Set(UIIcon.Play, Icon("play", P(p => RoundPoly(p, play, 0.1f), W)));

            float ra = 100f * Mathf.Deg2Rad;
            Vector2 e0 = new Vector2(Mathf.Cos(ra), Mathf.Sin(ra)) * 0.52f;
            Vector2 tan = new Vector2(-Mathf.Sin(ra), Mathf.Cos(ra)) * -1f;
            Vector2 nrm = new Vector2(Mathf.Cos(ra), Mathf.Sin(ra));
            var head = new[] { e0 + tan * 0.3f, e0 - tan * 0.06f + nrm * 0.26f, e0 - tan * 0.06f - nrm * 0.26f };
            Set(UIIcon.Replay, Icon("replay", P(p => Arc(p, z, 0.52f, 0.13f, 115f, 20f), W), P(p => RoundPoly(p, head, 0.04f), W)));

            var crown = new[] { new Vector2(-0.72f, -0.4f), new Vector2(-0.78f, 0.42f), new Vector2(-0.38f, 0.06f), new Vector2(0f, 0.62f), new Vector2(0.38f, 0.06f), new Vector2(0.78f, 0.42f), new Vector2(0.72f, -0.4f) };
            Set(UIIcon.Crown, Icon("crown",
                PF(p => RoundPoly(p, crown, 0.05f), VGrad(GoldL, Gold, -0.4f, 0.6f)),
                P(p => Mathf.Min(Circle(p, new Vector2(-0.78f, 0.46f), 0.1f), Mathf.Min(Circle(p, new Vector2(0f, 0.66f), 0.1f), Circle(p, new Vector2(0.78f, 0.46f), 0.1f))), Gold),
                P(p => Box(p, new Vector2(0f, -0.48f), new Vector2(0.75f, 0.13f), 0.05f), GoldD),
                D(p => Circle(p, new Vector2(0f, -0.48f), 0.08f), Red),
                D(p => Circle(p, new Vector2(-0.44f, -0.48f), 0.065f), Blue),
                D(p => Circle(p, new Vector2(0.44f, -0.48f), 0.065f), Green)));

            var bolt = new[] { new Vector2(0.12f, 0.9f), new Vector2(-0.5f, -0.06f), new Vector2(-0.04f, -0.06f), new Vector2(-0.18f, -0.9f), new Vector2(0.5f, 0.12f), new Vector2(0.04f, 0.12f) };
            Set(UIIcon.Lightning, Icon("lightning", PF(p => RoundPoly(p, bolt, 0.04f), VGrad(H("#FFF0A0"), Yellow, -0.6f, 0.8f))));

            var heartTri = new[] { new Vector2(-0.64f, 0.14f), new Vector2(0.64f, 0.14f), new Vector2(0f, -0.74f) };
            Set(UIIcon.Heart, Icon("heart",
                P(p => SmoothUnion(Mathf.Min(Circle(p, new Vector2(-0.31f, 0.26f), 0.37f), Circle(p, new Vector2(0.31f, 0.26f), 0.37f)), RoundPoly(p, heartTri, 0.05f), 0.1f), Red),
                D(p => Circle(p, new Vector2(-0.36f, 0.36f), 0.11f), A(W, 0.7f))));

            var boot = new[] { new Vector2(-0.44f, 0.72f), new Vector2(0.08f, 0.72f), new Vector2(0.08f, -0.08f), new Vector2(0.64f, -0.24f), new Vector2(0.7f, -0.58f), new Vector2(-0.46f, -0.58f) };
            Set(UIIcon.Boot, Icon("boot",
                P(p => RoundPoly(p, boot, 0.08f), Brown),
                P(p => Box(p, new Vector2(0.12f, -0.66f), new Vector2(0.62f, 0.1f), 0.06f), H("#4A3322")),
                P(p => Box(p, new Vector2(-0.18f, 0.68f), new Vector2(0.3f, 0.1f), 0.05f), H("#B07A47"))));

            Set(UIIcon.Target, Icon("target",
                P(p => Circle(p, z, 0.82f), Red),
                D(p => Circle(p, z, 0.62f), W),
                D(p => Circle(p, z, 0.42f), Red),
                D(p => Circle(p, z, 0.2f), W)));

            Set(UIIcon.Users, Icon("users",
                P(p => Mathf.Min(Circle(p, new Vector2(-0.44f, 0.28f), 0.2f), Circle(p, new Vector2(0.44f, 0.28f), 0.2f)), SteelL),
                P(p => Mathf.Min(Box(p, new Vector2(-0.44f, -0.3f), new Vector2(0.28f, 0.24f), 0.2f), Box(p, new Vector2(0.44f, -0.3f), new Vector2(0.28f, 0.24f), 0.2f)), SteelL),
                P(p => Circle(p, new Vector2(0f, 0.2f), 0.27f), W),
                P(p => Box(p, new Vector2(0f, -0.46f), new Vector2(0.37f, 0.28f), 0.25f), W)));

            Set(UIIcon.Globe, Icon("globe",
                P(p => Circle(p, z, 0.8f), Blue),
                D(p => Inter(Circle(p, z, 0.76f), Mathf.Min(Mathf.Min(Box(p, z, new Vector2(0.05f, 0.8f)), Box(p, z, new Vector2(0.8f, 0.045f))),
                    Mathf.Min(Ring(new Vector2(p.x / 0.45f, p.y), z, 0.78f, 0.1f) * 0.45f, Mathf.Min(Box(p, new Vector2(0f, 0.4f), new Vector2(0.8f, 0.04f)), Box(p, new Vector2(0f, -0.4f), new Vector2(0.8f, 0.04f)))))), BlueL)));

            var burst = Star(z, 0.9f, 0.62f, 10);
            var burstIn = Star(z, 0.62f, 0.42f, 10);
            Set(UIIcon.Pow, Icon("pow", P(p => RoundPoly(p, burst, 0.03f), Yellow), D(p => RoundPoly(p, burstIn, 0.03f), Red), D(p => Circle(p, z, 0.2f), W)));

            var ros = Star(z, 0.86f, 0.74f, 16);
            var mStar = Star(z, 0.34f, 0.16f, 5);
            Set(UIIcon.Medal, Icon("medal",
                P(p => Mathf.Min(Box(p, new Vector2(-0.28f, -0.62f), new Vector2(0.14f, 0.3f), 0.04f), Box(p, new Vector2(0.28f, -0.62f), new Vector2(0.14f, 0.3f), 0.04f)), Red),
                P(p => RoundPoly(p, ros, 0.02f) + 0.12f, GoldD),
                D(p => Circle(p, new Vector2(0f, 0.05f), 0.52f), Gold),
                D(p => RoundPoly(p - new Vector2(0f, 0.05f), mStar, 0.02f), W)));

            Set(UIIcon.Info, Icon("info",
                P(p => Circle(p, z, 0.8f), Blue),
                D(p => Mathf.Min(Circle(p, new Vector2(0f, 0.38f), 0.12f), Box(p, new Vector2(0f, -0.18f), new Vector2(0.1f, 0.34f), 0.05f)), W)));

            for (int i = 0; i < n; i++) if (icons[i] == null) Debug.LogWarning("[UIBuild] icon missing: " + (UIIcon)i);
            skin.icons = icons;
        }
    }
}
