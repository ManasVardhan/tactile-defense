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

        /// <summary>Puts the map's mask on a runtime copy of the ground material. Returns the copy (caller owns it).</summary>
        public static Material ApplyGround(MapLayoutData map, Renderer ground)
        {
            if (map == null || ground == null || map.groundMask == null) return null;
            var m = new Material(ground.sharedMaterial) { name = ground.sharedMaterial.name + "_" + map.id };
            m.SetTexture(MaskId, map.groundMask);
            m.SetVector(RectId, map.mapRect);
            ground.sharedMaterial = m;
            return m;
        }
    }
}
