using System.IO;
using TactileWars.Battle;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// The battlefield biomes, one section each: the palette written to a BiomeStyle asset (ground, water, light, sky)
    /// and the prop set the map builders place. Meadow dresses Levels 1 and 2, the handcrafted lane, the player's
    /// home base and the menus; Red Canyon Level 3; Frost Ridge Level 4; Meadow at Dusk (Meadow props under a low
    /// evening sun) Level 5. All palettes and props are our own designs.
    /// </summary>
    public static class BiomeBuilder
    {
        public const string Dir = "Assets/Data/Biomes";

        /// <summary>Prefab names (Assets/Prefabs/Environment) a map builder uses for one biome. Blocking props keep
        /// the Obstacle layer and a collider (AssetBuilder), so NavGrid and the hidden grid treat them as before.</summary>
        public class PropSet
        {
            /// <summary>The dense border that frames the field: trees or a rock wall (blocking).</summary>
            public string[] edge;
            /// <summary>Clumps inside the field that break symmetry (blocking).</summary>
            public string[] clump;
            /// <summary>Scattered rocks (blocking) and, on the canyon, the rows lining the pass.</summary>
            public string[] rocks;
            /// <summary>Big rocks for the rows lining a pass (blocking).</summary>
            public string[] bigRocks;
            /// <summary>Soft low plants (decorative, do not block).</summary>
            public string[] shrubs;
            /// <summary>Tiny ground detail: tufts (decorative).</summary>
            public string[] detail;
            /// <summary>Small accents: flowers and the like (decorative).</summary>
            public string[] flowers;
            /// <summary>Fence segment about 2 units long along x (blocking).</summary>
            public string fence;
            /// <summary>A cluster that replaces the old tent camps: two big pieces where the tents stood, three small
            /// ones where the crates stood (blocking, a similar or smaller footprint).</summary>
            public string[] clusterBig, clusterSmall;
            /// <summary>Lying props scattered like the old logs (blocking).</summary>
            public string[] lying;
            /// <summary>A feature prop for an open clearing off the lane: pond, frozen pool (blocking), or null.</summary>
            public string landmark;
            /// <summary>Gameplay walls of this biome: a 2.0 segment, a corner pillar and a 2.2 tower (blocking).</summary>
            public string wall, wallCorner, tower;
            /// <summary>The enemy headquarters building of this biome.</summary>
            public string hq;
            /// <summary>Big pieces for a rock spine dividing a map (blocking; the spine also gets an invisible core).</summary>
            public string[] spine;
            /// <summary>Walkable plants or stones along a river bank.</summary>
            public string[] bank;
            /// <summary>Crossing props: a bridge and a ford (both walkable).</summary>
            public string bridge = "Bridge", ford = "Ford";
        }

        // ------------------------------------------------------------------ Meadow

        /// <summary>Fresh green grass with soft sunlit patches and wildflower specks, a worn dirt track.</summary>
        static void MeadowPalette(BiomeStyle b)
        {
            b.displayName = "Meadow";
            b.baseColor1 = H("#6F9B4F"); b.baseColor2 = H("#638E47"); b.noiseScale = 0.055f;
            b.patchColor = H("#86AC5A"); b.patchScale = 0.12f; b.patchCoverage = 0.36f; b.patchSoftness = 0.14f;
            b.crackColor = H("#4E6A34"); b.crackStrength = 0f; b.crackScale = 0.9f;
            b.speckleColor = H("#F4E9A6"); b.speckleAmount = 0.045f; b.speckleScale = 2.4f;
            b.pathColor = H("#B79868"); b.pathStrength = 0.95f;
            b.wornColor = H("#93A059"); b.wornStrength = 0.55f;
            b.edgeColor = H("#4F7A3A");
            b.waterColor = H("#5FA9C4"); b.waterDeep = H("#4A8FB0"); b.bankColor = H("#7C8F52"); b.waterStrength = 1f;
            b.shadowColor = H("#7E8E9C"); b.shadowStrength = 0.78f;
            b.sunColor = H("#FFF3DC"); b.sunIntensity = 1.05f;
            b.ambientSky = H("#C6D8E6"); b.ambientEquator = H("#B5BE9E"); b.ambientGround = H("#6B6A50");
            b.background = H("#4F7A3A");
        }

        public static readonly PropSet MeadowProps = new PropSet
        {
            edge = new[] { "RoundTree_A", "RoundTree_B", "RoundTree_A" },
            clump = new[] { "RoundTree_A", "RoundTree_B", "Hedge" },
            rocks = new[] { "Rock_A", "Rock_B", "Rock_C" },
            bigRocks = new[] { "Rock_B", "Rock_C" },
            shrubs = new[] { "MeadowShrub", "MeadowShrub" },
            detail = new[] { "MeadowTuft" },
            flowers = new[] { "Wildflowers", "Flower_A", "Flower_B" },
            fence = "RailFence",
            clusterBig = new[] { "Hedge", "HayBale" },
            clusterSmall = new[] { "HayBale", "StoneFence", "HayBale" },
            lying = new[] { "Log", "HayBale" },
            landmark = "Pond",
            wall = "Wall_Meadow", wallCorner = "WallCorner_Meadow", tower = "Tower_Meadow", hq = "HQ_Meadow",
            spine = new[] { "Rock_C", "Rock_B", "RoundTree_B" },
            bank = new[] { "Reeds", "Reeds", "MeadowShrub" },
        };

        // ------------------------------------------------------------------ Meadow at Dusk

        /// <summary>The Meadow under a low amber evening sun: deeper greens, a warm dusty track, long cool shadows.</summary>
        static void DuskPalette(BiomeStyle b)
        {
            MeadowPalette(b);
            b.displayName = "Meadow at Dusk";
            b.baseColor1 = H("#5C8546"); b.baseColor2 = H("#527A3F");
            b.patchColor = H("#7C9A4E"); b.speckleColor = H("#F2C98A");
            b.pathColor = H("#B08A5C"); b.wornColor = H("#86884E");
            b.edgeColor = H("#3F6534");
            b.waterColor = H("#6A8FB0"); b.waterDeep = H("#516F94"); b.bankColor = H("#6E7A46");
            b.shadowColor = H("#6E6A90"); b.shadowStrength = 0.82f;
            b.sunColor = H("#FFC98A"); b.sunIntensity = 1.08f;
            b.ambientSky = H("#B4A9C8"); b.ambientEquator = H("#C79C80"); b.ambientGround = H("#5A4C46");
            b.background = H("#3D5A36");
        }

        // ------------------------------------------------------------------ Red Canyon

        /// <summary>Warm sandstone ground with pale cracked clay patches and rust edges, a pale sand track.</summary>
        static void CanyonPalette(BiomeStyle b)
        {
            b.displayName = "Red Canyon";
            // A little darker and less saturated than the first pass, so soldiers in warm team colors still read.
            b.baseColor1 = H("#C99B78"); b.baseColor2 = H("#BD8E6C"); b.noiseScale = 0.05f;
            b.patchColor = H("#E6C9A1"); b.patchScale = 0.14f; b.patchCoverage = 0.36f; b.patchSoftness = 0.12f;
            b.crackColor = H("#B37A56"); b.crackStrength = 0.6f; b.crackScale = 0.6f;
            b.speckleColor = H("#93553A"); b.speckleAmount = 0.05f; b.speckleScale = 2.8f;
            b.pathColor = H("#EDD6B0"); b.pathStrength = 0.9f;
            b.wornColor = H("#DDB088"); b.wornStrength = 0.55f;
            b.edgeColor = H("#A26A4E");
            b.waterColor = H("#B98F6C"); b.waterDeep = H("#A87D5E"); b.bankColor = H("#E0C49E"); b.waterStrength = 1f;
            b.shadowColor = H("#8C6E76"); b.shadowStrength = 0.8f;
            b.sunColor = H("#FFE4BC"); b.sunIntensity = 1.12f;
            b.ambientSky = H("#EACBAA"); b.ambientEquator = H("#D4A47F"); b.ambientGround = H("#7A4A33");
            b.background = H("#A65F3E");
        }

        public static readonly PropSet CanyonProps = new PropSet
        {
            edge = new[] { "Mesa_A", "Mesa_B", "Mesa_C", "Mesa_D", "Boulder_C", "Mesa_B" },
            clump = new[] { "Cactus_A", "Boulder_B", "Cactus_A" },
            rocks = new[] { "Boulder_A", "Boulder_B", "Boulder_C" },
            bigRocks = new[] { "Boulder_B", "Boulder_C" },
            shrubs = new[] { "DryShrub", "Cactus_B" },
            detail = new[] { "DryTuft" },
            flowers = new[] { "Cactus_B", "DryTuft", "DryShrub" },
            fence = "RopeFence",
            clusterBig = new[] { "Mesa_B", "Boulder_C" },
            clusterSmall = new[] { "Bones", "WagonWheel", "Cactus_A" },
            lying = new[] { "WagonWheel", "Bones" },
            landmark = "Mesa_A",
            wall = "Wall_Canyon", wallCorner = "WallCorner_Canyon", tower = "Tower_Canyon", hq = "HQ_Canyon",
            spine = new[] { "Mesa_A", "Mesa_C", "Mesa_B", "Boulder_C" },
            bank = new[] { "Boulder_A", "DryShrub", "Bones" },
        };

        // ------------------------------------------------------------------ Frost Ridge

        /// <summary>Cool slate rock with soft snow patches and snowy edges, a dark gravel track.</summary>
        static void RidgePalette(BiomeStyle b)
        {
            b.displayName = "Frost Ridge";
            b.baseColor1 = H("#8E9BA7"); b.baseColor2 = H("#808E9B"); b.noiseScale = 0.05f;
            b.patchColor = H("#E6EDF3"); b.patchScale = 0.11f; b.patchCoverage = 0.4f; b.patchSoftness = 0.09f;
            b.crackColor = H("#6C7884"); b.crackStrength = 0f; b.crackScale = 0.9f;
            b.speckleColor = H("#5F6A76"); b.speckleAmount = 0.04f; b.speckleScale = 2.6f;
            b.pathColor = H("#7A6F66"); b.pathStrength = 0.9f;
            b.wornColor = H("#A3ADB7"); b.wornStrength = 0.5f;
            b.edgeColor = H("#EEF3F7");
            b.waterColor = H("#BFE2EE"); b.waterDeep = H("#9CCFE2"); b.bankColor = H("#E8EFF4"); b.waterStrength = 1f;
            b.shadowColor = H("#7F8FB0"); b.shadowStrength = 0.8f;
            b.sunColor = H("#EEF4FF"); b.sunIntensity = 1.0f;
            b.ambientSky = H("#C9D8EE"); b.ambientEquator = H("#AAB5C5"); b.ambientGround = H("#5D6670");
            b.background = H("#D9E2EA");
        }

        public static readonly PropSet RidgeProps = new PropSet
        {
            edge = new[] { "Fir_A", "Fir_B", "Fir_A" },
            clump = new[] { "Fir_A", "Fir_B", "Slate_C" },
            rocks = new[] { "Slate_A", "Slate_B", "Slate_C" },
            bigRocks = new[] { "Slate_B", "Slate_C" },
            shrubs = new[] { "FrostShrub", "FrostShrub" },
            detail = new[] { "FrostTuft" },
            flowers = new[] { "FrostTuft", "FrostShrub", "FrostTuft" },
            fence = "StoneFence",
            clusterBig = new[] { "Slate_C", "Fir_B" },
            clusterSmall = new[] { "Cairn", "IceShard", "Slate_A" },
            lying = new[] { "Cairn", "IceShard" },
            landmark = "FrozenPool",
            wall = "Wall_Ridge", wallCorner = "WallCorner_Ridge", tower = "Tower_Ridge", hq = "HQ_Ridge",
            spine = new[] { "Crag_A", "Crag_B", "Slate_C", "Fir_B" },
            bank = new[] { "IceShard", "FrostTuft", "Slate_A" },
        };

        // ------------------------------------------------------------------ assets

        public static BiomeStyle Meadow() => Style("meadow", MeadowPalette);
        public static BiomeStyle Canyon() => Style("canyon", CanyonPalette);
        public static BiomeStyle Ridge() => Style("ridge", RidgePalette);
        public static BiomeStyle Dusk() => Style("dusk", DuskPalette);

        /// <summary>The biome style of an id ("canyon", "ridge", "dusk"; anything else is Meadow).</summary>
        public static BiomeStyle StyleFor(string biomeId) => biomeId == "canyon" ? Canyon() : biomeId == "ridge" ? Ridge() : biomeId == "dusk" ? Dusk() : Meadow();

        /// <summary>The prop set of a biome id ("canyon", "ridge"; Meadow and Meadow at Dusk share the Meadow set).</summary>
        public static PropSet PropsFor(string biomeId) => biomeId == "canyon" ? CanyonProps : biomeId == "ridge" ? RidgeProps : MeadowProps;

        static BiomeStyle Style(string id, System.Action<BiomeStyle> palette)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "/Biome_" + id + ".asset";
            var b = AssetDatabase.LoadAssetAtPath<BiomeStyle>(path);
            if (b == null) { b = ScriptableObject.CreateInstance<BiomeStyle>(); AssetDatabase.CreateAsset(b, path); }
            b.id = id;
            palette(b);
            EditorUtility.SetDirty(b);
            return b;
        }

        static Color H(string hex) => AssetBuilder.Hex(hex);
    }
}
