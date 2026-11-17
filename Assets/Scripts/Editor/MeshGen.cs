using System.IO;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>Small generated meshes: decal quads (optionally mapped to an atlas cell), a bubble sphere,
    /// the ground plane, and clearly named stand-in shapes used only when an FBX is missing.</summary>
    public static class MeshGen
    {
        public const string Dir = "Assets/Art/Meshes";

        static Mesh SaveMesh(Mesh m, string name)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(m, existing);
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Unit quad in the XY plane (rotate 90 on X to lay it on the ground). Normal -Z.</summary>
        public static Mesh Quad(string name, Rect uv)
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return SaveMesh(m, name);
        }

        public static Mesh Sphere(string name, int lon = 28, int lat = 18)
        {
            var m = new Mesh();
            var verts = new Vector3[(lon + 1) * (lat + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            int i = 0;
            for (int y = 0; y <= lat; y++)
                for (int x = 0; x <= lon; x++)
                {
                    float th = y / (float)lat * Mathf.PI, ph = x / (float)lon * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    verts[i] = n * 0.5f; norms[i] = n; uvs[i] = new Vector2(x / (float)lon, 1f - y / (float)lat); i++;
                }
            var tris = new int[lon * lat * 6];
            int t = 0;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + lon + 1;
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
                }
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return SaveMesh(m, name);
        }

        /// <summary>Flat ground plane on XZ (normal up), subdivided a little so shadows and fog behave.</summary>
        public static Mesh GroundPlane(string name, Vector2 min, Vector2 max, int segX, int segZ)
        {
            var m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var verts = new Vector3[(segX + 1) * (segZ + 1)];
            var uvs = new Vector2[verts.Length];
            var norms = new Vector3[verts.Length];
            int i = 0;
            for (int z = 0; z <= segZ; z++)
                for (int x = 0; x <= segX; x++)
                {
                    float fx = x / (float)segX, fz = z / (float)segZ;
                    verts[i] = new Vector3(Mathf.Lerp(min.x, max.x, fx), 0f, Mathf.Lerp(min.y, max.y, fz));
                    uvs[i] = new Vector2(fx, fz);
                    norms[i] = Vector3.up;
                    i++;
                }
            var tris = new int[segX * segZ * 6];
            int t = 0;
            for (int z = 0; z < segZ; z++)
                for (int x = 0; x < segX; x++)
                {
                    int a = z * (segX + 1) + x, b = a + segX + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            m.vertices = verts; m.uv = uvs; m.normals = norms; m.triangles = tris;
            m.RecalculateBounds();
            return SaveMesh(m, name);
        }
    }
}
