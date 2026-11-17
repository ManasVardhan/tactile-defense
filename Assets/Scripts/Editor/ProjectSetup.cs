using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TactileWars.EditorTools
{
    /// <summary>One shot project configuration: URP assets tuned for mobile, color space, layers, physics matrix,
    /// input handling, company and product names, global post processing profile.</summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/Settings";
        public const string PipelinePath = SettingsDir + "/TW_URP_Mobile.asset";
        public const string RendererPath = SettingsDir + "/TW_URP_Renderer.asset";
        public const string VolumePath = SettingsDir + "/TW_BattleVolume.asset";

        static readonly string[] LayerNames = { null, null, null, null, null, null, "Ground", "PlayerUnit", "EnemyUnit", "Structure", "Obstacle", "Projectile", "Pickup" };

        [MenuItem("TactileWars/Project Setup")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            PlayerSettings.companyName = "TW Recreation";
            PlayerSettings.productName = "Paint Squad Wars (Tactile Wars study)";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SetInputHandlingBoth();
            SetupLayers();
            SetupPhysics();
            var pipeline = SetupPipeline();
            SetupVolumeProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done. Pipeline: " + (pipeline != null ? pipeline.name : "none"));
        }

        static void SetInputHandlingBoth()
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps == null || ps.Length == 0) return;
            var so = new SerializedObject(ps[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue != 2) { p.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetupLayers()
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tm == null || tm.Length == 0) return;
            var so = new SerializedObject(tm[0]);
            var layers = so.FindProperty("layers");
            for (int i = 6; i < LayerNames.Length; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                el.stringValue = LayerNames[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupPhysics()
        {
            // Units have no colliders; avoidance queries are explicit. Keep the matrix minimal.
            for (int a = 0; a < 32; a++)
                for (int b = 0; b < 32; b++)
                    Physics.IgnoreLayerCollision(a, b, false);
            int[] units = { 7, 8 };
            foreach (int u in units)
            {
                Physics.IgnoreLayerCollision(u, 7, true);
                Physics.IgnoreLayerCollision(u, 8, true);
                Physics.IgnoreLayerCollision(u, 11, true);
                Physics.IgnoreLayerCollision(u, 12, true);
            }
            Physics.IgnoreLayerCollision(11, 11, true);
            Physics.IgnoreLayerCollision(11, 12, true);
            Physics.IgnoreLayerCollision(12, 12, true);
            Physics.IgnoreLayerCollision(12, 6, true);
            Physics.IgnoreLayerCollision(12, 9, true);
            Physics.IgnoreLayerCollision(12, 10, true);
            Physics.IgnoreLayerCollision(9, 10, true);
            Physics.IgnoreLayerCollision(10, 10, true);
            Physics.IgnoreLayerCollision(9, 9, true);
            Physics.queriesHitTriggers = false;
        }

        static UniversalRenderPipelineAsset SetupPipeline()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }
            var rso = new SerializedObject(rendererData);
            SetInt(rso, "m_RenderingMode", 0); // Forward
            SetInt(rso, "m_DepthPrimingMode", 0);
            SetInt(rso, "m_CopyDepthMode", 0);
            rso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.shadowDistance = 55f;
            asset.shadowCascadeCount = 1;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            var so = new SerializedObject(asset);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 2); // medium
            SetInt(so, "m_AdditionalLightsRenderingMode", 0); // disabled: one sun only
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetBool(so, "m_UseSRPBatcher", true);
            SetBool(so, "m_SupportsDynamicBatching", false);
            SetInt(so, "m_HDRColorBufferPrecision", 0);
            SetFloat(so, "m_ShadowDepthBias", 1.2f);
            SetFloat(so, "m_ShadowNormalBias", 0.8f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int levels = QualitySettings.names.Length;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 0; // MSAA comes from the URP asset
            }
            QualitySettings.SetQualityLevel(current, false);
            return asset;
        }

        static void SetupVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            // Gentle grading: a touch of contrast and saturation, slight vignette, subtle bloom for paint glow.
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.55f);
            bloom.highQualityFiltering.Override(false);

            var ca = GetOrAdd<ColorAdjustments>(profile);
            ca.contrast.Override(8f);
            ca.saturation.Override(6f);
            ca.postExposure.Override(0.05f);

            var vig = GetOrAdd<Vignette>(profile);
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.18f, 0.14f, 0.1f));

            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.None);

            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.temperature.Override(4f);

            EditorUtility.SetDirty(profile);
        }

        static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T c)) return c;
            c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[ProjectSetup] property not found: " + name); return; }
            p.intValue = v;
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[ProjectSetup] property not found: " + name); return; }
            p.boolValue = v;
        }

        static void SetFloat(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[ProjectSetup] property not found: " + name); return; }
            p.floatValue = v;
        }
    }
}
