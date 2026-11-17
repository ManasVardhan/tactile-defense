using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Persistent paint splats on the ground. Fixed size ring buffer of pooled quads: the oldest
    /// splat is recycled once the cap is reached. Uses per team shared materials and atlas variant meshes.</summary>
    public class PaintSplatManager : MonoBehaviour
    {
        public static PaintSplatManager Instance { get; private set; }

        public TeamVisuals visuals;
        public Mesh[] variantMeshes;       // quads mapped to atlas cells
        public int cap = 420;
        public float growTime = 0.12f;

        MeshFilter[] filters;
        MeshRenderer[] renderers;
        Transform[] transforms;
        float[] targetScale;
        float[] spawnTime;
        int next;
        int used;
        public int Count => used;

        void Awake()
        {
            Instance = this;
            filters = new MeshFilter[cap];
            renderers = new MeshRenderer[cap];
            transforms = new Transform[cap];
            targetScale = new float[cap];
            spawnTime = new float[cap];
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void EnsureSlot(int i)
        {
            if (transforms[i] != null) return;
            var go = new GameObject("Splat");
            go.layer = 0;
            go.transform.SetParent(transform, false);
            filters[i] = go.AddComponent<MeshFilter>();
            renderers[i] = go.AddComponent<MeshRenderer>();
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            renderers[i].lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderers[i].reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            transforms[i] = go.transform;
        }

        public void Spawn(Vector3 pos, TeamColorId color, float size, bool big)
        {
            if (cap <= 0 || visuals == null) return;
            var mats = visuals.Get(color);
            if (mats == null || mats.splat == null) return;
            int i = next;
            next = (next + 1) % cap;
            if (used < cap) used++;
            EnsureSlot(i);
            if (variantMeshes != null && variantMeshes.Length > 0)
                filters[i].sharedMesh = variantMeshes[big ? 0 : Random.Range(0, variantMeshes.Length)];
            renderers[i].sharedMaterial = mats.splat;
            pos.y = 0.012f + (i % 16) * 0.0006f;
            transforms[i].gameObject.SetActive(true);
            transforms[i].SetPositionAndRotation(pos, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            targetScale[i] = size;
            spawnTime[i] = Time.time;
            transforms[i].localScale = Vector3.one * size * 0.5f;
        }

        void Update()
        {
            // Only the newest splats can still be growing; walk back a small window.
            float now = Time.time;
            for (int k = 1; k <= 24 && k <= used; k++)
            {
                int i = (next - k + cap) % cap;
                if (transforms[i] == null) continue;
                float a = (now - spawnTime[i]) / growTime;
                if (a > 1.5f) break;
                float s = targetScale[i] * Mathf.Lerp(0.5f, 1f, EaseOutBack(Mathf.Clamp01(a)));
                transforms[i].localScale = new Vector3(s, s, s);
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        public void ClearAll()
        {
            for (int i = 0; i < cap; i++) if (transforms[i] != null) transforms[i].gameObject.SetActive(false);
            used = 0; next = 0;
        }
    }
}
