using UnityEngine;
using UnityEngine.Rendering;

namespace TactileWars.Army
{
    /// <summary>A flat ring under the selected unit: on its hex at rest, following the soldiers' centroid while it
    /// moves. Presentation only.</summary>
    public class SelectionMarker : MonoBehaviour
    {
        public PlayerArmy army;
        [Tooltip("Transparent unlit material tinted by vertex color (TW/Particle).")] public Material material;
        public Color color = new Color(1f, 0.95f, 0.55f, 0.9f);
        [Tooltip("Ring radius as a fraction of the hex outer radius.")] public float radiusFraction = 0.86f;
        public float thickness = 0.15f;

        const float Height = 0.045f;
        const int Segments = 48;

        MeshRenderer ring;
        Mesh mesh;

        /// <summary>True while the ring is drawn (a unit is selected).</summary>
        public bool Shown => ring != null && ring.enabled;

        void Awake()
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            ring = go.AddComponent<MeshRenderer>();
            ring.sharedMaterial = material;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }

        void LateUpdate()
        {
            var unit = army != null ? army.Selected : null;
            bool show = unit != null && !unit.IsDestroyed && unit.AliveCount > 0;
            ring.enabled = show;
            if (!show) return;
            var g = army.Grid;
            if (mesh == null)
            {
                mesh = BuildRing(g.OuterRadius * radiusFraction, thickness, color);
                ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            Vector3 p = unit.IsMoving ? unit.Centroid : g.Center(unit.Hex);
            p.y = Height;
            ring.transform.position = p;
        }

        static Mesh BuildRing(float radius, float width, Color c)
        {
            var v = new Vector3[Segments * 2];
            var col = new Color[Segments * 2];
            var tris = new int[Segments * 6];
            float ri = radius - width * 0.5f, ro = radius + width * 0.5f;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * ri;
                v[i * 2 + 1] = d * ro;
                col[i * 2] = col[i * 2 + 1] = c;
                int n = (i + 1) % Segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "SelectionRing (runtime)" };
            m.vertices = v;
            m.colors = col;
            m.uv = new Vector2[v.Length];
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
