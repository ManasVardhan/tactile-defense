using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Spawns a MapLayoutData at runtime: props under a parent and the ground mask on a material clone.
    /// Shared by the battle scene (map variants, the player's base in defense battles) and the base editor.</summary>
    public static class MapApplier
    {
        static readonly int MaskId = Shader.PropertyToID("_MaskTex");
        static readonly int RectId = Shader.PropertyToID("_MapRect");

        public static int SpawnProps(MapLayoutData map, Transform parent)
        {
            if (map == null) return 0;
            int n = 0;
            for (int i = 0; i < map.props.Length; i++)
            {
                var p = map.props[i];
                if (p.prefab == null) continue;
                var go = Object.Instantiate(p.prefab, p.position, Quaternion.Euler(0f, p.yaw, 0f), parent);
                float s = p.scale > 0f ? p.scale : 1f;
                go.transform.localScale = new Vector3(s, s, s);
                n++;
            }
            return n;
        }

        /// <summary>The scene's ground plane is built over this rectangle (x, z, width, depth); FitGround stretches it to
        /// a map's own rectangle.</summary>
        public static readonly Vector4 SceneGroundRect = new Vector4(-40f, -25f, 80f, 160f);

        /// <summary>Stretches the ground plane (built over SceneGroundRect) so it covers the map's rectangle.</summary>
        public static void FitGround(MapLayoutData map, Renderer ground)
        {
            if (map == null || ground == null) return;
            Vector4 a = SceneGroundRect, b = map.mapRect;
            if (b.z <= 0f || b.w <= 0f || a == b) return;
            float sx = b.z / a.z, sz = b.w / a.w;
            var t = ground.transform;
            t.localScale = new Vector3(sx, 1f, sz);
            t.position = new Vector3(b.x - a.x * sx, t.position.y, b.y - a.y * sz);
        }

        /// <summary>Puts the map's mask and its biome's ground colors on a runtime copy of the ground material.
        /// Returns the copy (caller owns it), null when the map changes nothing.</summary>
        public static Material ApplyGround(MapLayoutData map, Renderer ground)
        {
            if (map == null || ground == null || (map.groundMask == null && map.biome == null)) return null;
            var m = new Material(ground.sharedMaterial) { name = ground.sharedMaterial.name + "_" + map.id };
            if (map.groundMask != null)
            {
                m.SetTexture(MaskId, map.groundMask);
                m.SetVector(RectId, map.mapRect);
            }
            if (map.biome != null) map.biome.ApplyGround(m);
            ground.sharedMaterial = m;
            return m;
        }
    }
}
