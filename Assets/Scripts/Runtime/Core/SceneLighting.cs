using UnityEngine;
using UnityEngine.Rendering;

namespace TactileWars.Core
{
    /// <summary>Soft ambient for the stylized look. Pushes a flat ambient probe at runtime so lighting does
    /// not depend on baked environment data.</summary>
    [ExecuteAlways]
    public class SceneLighting : MonoBehaviour
    {
        public Color sky = new Color(0.78f, 0.8f, 0.86f);
        public Color equator = new Color(0.72f, 0.69f, 0.62f);
        public Color ground = new Color(0.5f, 0.46f, 0.4f);
        [Range(0, 2)] public float intensity = 1f;

        void OnEnable() { Apply(); }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * intensity;
            RenderSettings.ambientEquatorColor = equator * intensity;
            RenderSettings.ambientGroundColor = ground * intensity;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(equator.linear * intensity);
            sh.AddDirectionalLight(Vector3.up, ((sky - equator) * 0.6f).linear * intensity, 1f);
            RenderSettings.ambientProbe = sh;
        }
    }
}
