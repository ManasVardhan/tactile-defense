using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Small flat ring arc on the ground that shows a structure's health (only visible once damaged).</summary>
    public class HealthArc : MonoBehaviour
    {
        public Renderer arc;
        public bool alwaysVisible;
        MaterialPropertyBlock mpb;
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        float shown;
        float fill = 1f;

        public void SetMaterial(Material m) { if (arc != null && m != null) arc.sharedMaterial = m; }

        public void SetFill(float f) { fill = Mathf.Clamp01(f); if (f < 0.999f) shown = 1f; }

        void LateUpdate()
        {
            if (arc == null) return;
            mpb ??= new MaterialPropertyBlock();
            float a = alwaysVisible ? 1f : shown;
            arc.enabled = a > 0.01f;
            if (!arc.enabled) return;
            arc.GetPropertyBlock(mpb);
            mpb.SetFloat(FillId, fill);
            mpb.SetFloat(AlphaId, a);
            arc.SetPropertyBlock(mpb);
        }
    }
}
