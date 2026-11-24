using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>
    /// The look of one battlefield biome: the ground's colors and amounts (TW/Ground) and the light and sky tint.
    /// Values are written by the editor BiomeBuilder (one section per biome); the prop set lives there too, since
    /// props are placed when the maps are built. Applied at runtime by MapApplier and BattleSetup.
    /// </summary>
    [CreateAssetMenu(menuName = "TactileWars/Biome Style")]
    public class BiomeStyle : ScriptableObject
    {
        public string id;
        public string displayName;

        [Header("Ground (TW/Ground)")]
        public Color baseColor1 = new Color(0.55f, 0.68f, 0.36f);
        public Color baseColor2 = new Color(0.5f, 0.63f, 0.33f);
        public float noiseScale = 0.06f;
        public Color patchColor = new Color(0.62f, 0.72f, 0.4f);
        public float patchScale = 0.16f;
        [Range(0, 1)] public float patchCoverage = 0.35f;
        [Range(0.01f, 0.5f)] public float patchSoftness = 0.12f;
        public Color crackColor = new Color(0.4f, 0.25f, 0.18f);
        [Range(0, 1)] public float crackStrength;
        public float crackScale = 0.9f;
        public Color speckleColor = new Color(0.95f, 0.92f, 0.7f);
        [Range(0, 0.3f)] public float speckleAmount = 0.04f;
        public float speckleScale = 2.2f;
        public Color pathColor = new Color(0.66f, 0.56f, 0.4f);
        [Range(0, 1)] public float pathStrength = 0.9f;
        public Color wornColor = new Color(0.6f, 0.6f, 0.42f);
        [Range(0, 1)] public float wornStrength = 0.6f;
        public Color edgeColor = new Color(0.38f, 0.5f, 0.3f);
        [Tooltip("Water, riverbed or ice painted where the map mask's alpha is below 1.")] public Color waterColor = new Color(0.36f, 0.62f, 0.74f);
        public Color waterDeep = new Color(0.27f, 0.5f, 0.64f);
        public Color bankColor = new Color(0.5f, 0.55f, 0.4f);
        [Range(0, 1)] public float waterStrength = 1f;
        public Color shadowColor = new Color(0.62f, 0.6f, 0.66f);
        [Range(0, 1)] public float shadowStrength = 0.75f;

        [Header("Light and sky")]
        public Color sunColor = new Color(1f, 0.96f, 0.89f);
        public float sunIntensity = 1.05f;
        public Color ambientSky = new Color(0.78f, 0.8f, 0.86f);
        public Color ambientEquator = new Color(0.72f, 0.69f, 0.62f);
        public Color ambientGround = new Color(0.5f, 0.46f, 0.4f);
        [Tooltip("Camera clear color beyond the ground.")] public Color background = new Color(0.45f, 0.58f, 0.32f);

        static readonly int Base1 = Shader.PropertyToID("_BaseColor1"), Base2 = Shader.PropertyToID("_BaseColor2");
        static readonly int Noise = Shader.PropertyToID("_NoiseScale");
        static readonly int Patch = Shader.PropertyToID("_PatchColor"), PatchSc = Shader.PropertyToID("_PatchScale");
        static readonly int PatchCov = Shader.PropertyToID("_PatchCoverage"), PatchSoft = Shader.PropertyToID("_PatchSoftness");
        static readonly int Crack = Shader.PropertyToID("_CrackColor"), CrackStr = Shader.PropertyToID("_CrackStrength"), CrackSc = Shader.PropertyToID("_CrackScale");
        static readonly int Speck = Shader.PropertyToID("_SpeckleColor"), SpeckAmt = Shader.PropertyToID("_SpeckleAmount"), SpeckSc = Shader.PropertyToID("_SpeckleScale");
        static readonly int PathC = Shader.PropertyToID("_PathColor"), PathStr = Shader.PropertyToID("_PathStrength");
        static readonly int Worn = Shader.PropertyToID("_WornColor"), WornStr = Shader.PropertyToID("_WornStrength");
        static readonly int Dark = Shader.PropertyToID("_DarkColor");
        static readonly int WaterC = Shader.PropertyToID("_WaterColor"), WaterD = Shader.PropertyToID("_WaterDeep");
        static readonly int Bank = Shader.PropertyToID("_BankColor"), WaterStr = Shader.PropertyToID("_WaterStrength");
        static readonly int Shadow = Shader.PropertyToID("_ShadowColor"), ShadowStr = Shader.PropertyToID("_ShadowStrength");

        /// <summary>Writes the ground colors and amounts into a TW/Ground material.</summary>
        public void ApplyGround(Material m)
        {
            if (m == null) return;
            m.SetColor(Base1, baseColor1);
            m.SetColor(Base2, baseColor2);
            m.SetFloat(Noise, noiseScale);
            m.SetColor(Patch, patchColor);
            m.SetFloat(PatchSc, patchScale);
            m.SetFloat(PatchCov, patchCoverage);
            m.SetFloat(PatchSoft, patchSoftness);
            m.SetColor(Crack, crackColor);
            m.SetFloat(CrackStr, crackStrength);
            m.SetFloat(CrackSc, crackScale);
            m.SetColor(Speck, speckleColor);
            m.SetFloat(SpeckAmt, speckleAmount);
            m.SetFloat(SpeckSc, speckleScale);
            m.SetColor(PathC, pathColor);
            m.SetFloat(PathStr, pathStrength);
            m.SetColor(Worn, wornColor);
            m.SetFloat(WornStr, wornStrength);
            m.SetColor(Dark, edgeColor);
            m.SetColor(WaterC, waterColor);
            m.SetColor(WaterD, waterDeep);
            m.SetColor(Bank, bankColor);
            m.SetFloat(WaterStr, waterStrength);
            m.SetColor(Shadow, shadowColor);
            m.SetFloat(ShadowStr, shadowStrength);
        }

        /// <summary>Sun color and strength, ambient light and the camera clear color. Any argument may be null.</summary>
        public void ApplyLighting(Light sun, SceneLighting ambient, Camera cam)
        {
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
            if (ambient != null)
            {
                ambient.sky = ambientSky;
                ambient.equator = ambientEquator;
                ambient.ground = ambientGround;
                ambient.Apply();
            }
            if (cam != null) cam.backgroundColor = background;
        }
    }
}
