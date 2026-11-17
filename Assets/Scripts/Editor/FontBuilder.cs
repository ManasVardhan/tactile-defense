using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TactileWars.EditorTools
{
    /// <summary>Creates TextMeshPro SDF font assets from the SIL OFL fonts in Assets/Art/Fonts plus outline and
    /// shadow material presets (named "&lt;font&gt; SDF ..." so the TMP inspector lists them as presets).</summary>
    public static class FontBuilder
    {
        public const string Dir = "Assets/Art/Fonts";
        const string Chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        public class Result
        {
            public TMP_FontAsset heading, body;
            public Material headingOutline, headingOutlineThin, bodyOutline;
        }

        static readonly Color Ink = new Color(0.165f, 0.176f, 0.227f, 1f);

        public static Result Build()
        {
            var r = new Result
            {
                heading = Make("LilitaOne-Regular.ttf", "LilitaOne SDF"),
                body = Make("Fredoka-SemiBold.ttf", "Fredoka SDF"),
            };
            r.headingOutline = Preset(r.heading, "LilitaOne SDF Outline", 0.26f, 0.12f, new Vector2(0f, -1.1f), 0.55f);
            r.headingOutlineThin = Preset(r.heading, "LilitaOne SDF Outline Thin", 0.17f, 0.06f, new Vector2(0f, -0.7f), 0.4f);
            r.bodyOutline = Preset(r.body, "Fredoka SDF Outline", 0.24f, 0.08f, new Vector2(0f, -0.8f), 0.45f);

            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultFontAsset");
                if (p != null) { p.objectReferenceValue = r.body; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] fonts ready: " + r.heading.name + ", " + r.body.name);
            return r;
        }

        static TMP_FontAsset Make(string ttf, string name)
        {
            string path = Dir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(Dir + "/" + ttf);
            if (font == null) { Debug.LogError("[UIBuild] missing font file " + ttf); return null; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            var atlas = fa.atlasTextures[0];
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fa);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(Chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[UIBuild] " + name + " missing glyphs: " + missing);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Material Preset(TMP_FontAsset fa, string name, float outline, float faceDilate, Vector2 shadowOffset, float shadowAlpha)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(fa.material);
            else m.CopyPropertiesFromMaterial(fa.material);
            m.name = name;
            m.shader = fa.material.shader;
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            m.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(Ink.r, Ink.g, Ink.b, shadowAlpha));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset.x);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffset.y);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline * 2f + 0.1f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
            ShaderUtilities.UpdateShaderRatios(m);
            if (fresh) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
