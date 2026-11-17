using System.Collections.Generic;
using System.IO;
using TactileWars.Battle;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Generates map variants as MapLayoutData assets (Assets/Data/Maps): "home" (the player's own base: open
    /// meadow for building), "canyon" (S shaped pass between rock ridges, a wall with an off centre gate) and
    /// "ridge" (two walls with gates on alternating sides). Same linear format as the handcrafted lane: spawn at
    /// the south end, two capture points, HQ at the north end. Each asset carries its props, ground mask, spawn
    /// hatches and prop free sockets for generated AI layouts. All layouts are our own designs (estimates).
    /// </summary>
    public static class MapBuilder
    {
        public const string Dir = "Assets/Data/Maps";
        static readonly Vector4 MapRect = new Vector4(-40f, -25f, 80f, 160f);

        class Gen
        {
            public readonly List<PropPlacement> props = new List<PropPlacement>();
            public readonly List<Vector3> reserved = new List<Vector3>(); // x, z, r
            public Vector2[] path;
            public readonly List<Vector2[]> branches = new List<Vector2[]>();
            public readonly List<Vector3> tiles = new List<Vector3>(); // paved discs x, z, r
            public System.Random rng;
            public float R() => (float)rng.NextDouble();

            public bool Free(float x, float z, float r, float pathClear = 1.6f)
            {
                foreach (var v in reserved)
                {
                    float dx = x - v.x, dz = z - v.y;
                    if (dx * dx + dz * dz < (r + v.z) * (r + v.z)) return false;
                }
                return DistToPaths(x, z) > r + pathClear;
            }

            public float DistToPaths(float x, float z)
            {
                float best = DistToPoly(new Vector2(x, z), path);
                foreach (var b in branches) best = Mathf.Min(best, DistToPoly(new Vector2(x, z), b));
                return best;
            }

            public void Reserve(float x, float z, float r) => reserved.Add(new Vector3(x, z, r));

            public void Place(string name, Vector3 pos, float yaw, float scale, float blockRadius, float reserve = -1f)
            {
                var prefab = Env(name);
                if (prefab == null) { Debug.LogWarning("[MapBuilder] missing prefab " + name); return; }
                props.Add(new PropPlacement { prefab = prefab, position = pos, yaw = yaw, scale = scale, radius = blockRadius });
                if (reserve != 0f) Reserve(pos.x, pos.z, reserve > 0f ? reserve : Mathf.Max(0.4f, blockRadius));
            }
        }

        static GameObject Env(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/" + name + ".prefab");

        static float DistToPoly(Vector2 p, Vector2[] poly)
        {
            float best = 999f;
            for (int i = 1; i < poly.Length; i++)
            {
                Vector2 a = poly[i - 1], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        [MenuItem("TactileWars/Build Map Variants")]
        public static MapLayoutData[] BuildAll()
        {
            Directory.CreateDirectory(Dir);
            var home = Build("home", "Home Base", HomeLayout);
            var canyon = Build("canyon", "Canyon Pass", CanyonLayout);
            var ridge = Build("ridge", "Fort Ridge", RidgeLayout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapBuilder] maps: home {home.props.Length} props, canyon {canyon.props.Length} props / {canyon.squadSockets.Length} squad sockets, ridge {ridge.props.Length} props / {ridge.squadSockets.Length} squad sockets");
            return new[] { home, canyon, ridge };
        }

        delegate void Layout(MapLayoutData m, Gen g);

        static MapLayoutData Build(string id, string display, Layout layout)
        {
            string path = Dir + "/Map_" + id + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<MapLayoutData>(path);
            if (m == null) { m = ScriptableObject.CreateInstance<MapLayoutData>(); AssetDatabase.CreateAsset(m, path); }
            m.id = id;
            m.displayName = display;
            m.mapRect = MapRect;
            m.mapMin = new Vector2(-18f, 1f);
            m.mapMax = new Vector2(18f, 108f);
            m.focusMin = new Vector2(-1.5f, 9f);
            m.focusMax = new Vector2(1.5f, 99f);
            m.playerSpawn = new Vector3(0f, 0f, 8f);
            var g = new Gen { rng = new System.Random(id.GetHashCode() & 0x7fffffff) };
            layout(m, g);
            m.path = g.path;
            m.props = g.props.ToArray();
            m.groundMask = PaintMask(id, g, m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ shared dressing

        static void Common(MapLayoutData m, Gen g, int forestRows = 4, bool interiorClumps = true)
        {
            g.Reserve(m.playerSpawn.x, m.playerSpawn.z, 6f);
            foreach (var c in m.capturePoints) g.Reserve(c.x, c.z, 5f);
            // HQ at the north end and the defender's spawn hatches.
            g.Place("HQ", m.hq, 180f, 1f, 3.3f, 3.6f);
            g.tiles.Add(new Vector3(m.hq.x, m.hq.z - 1.5f, 5.5f));
            foreach (var h in m.spawnHatches) g.Place("SpawnHatch", h, 0f, 1f, 1.2f, 1.6f);
            foreach (var c in m.capturePoints) g.tiles.Add(new Vector3(c.x, c.z, 4.6f));

            string[] trees = { "Tree_A", "Tree_B", "Tree_C" };
            for (int side = -1; side <= 1; side += 2)
                for (float z = -12f; z < 124f; z += 2.1f)
                    for (int row = 0; row < forestRows; row++)
                    {
                        float x = side * (18.2f + row * 2.3f + g.R() * 1.6f);
                        float zz = z + g.R() * 1.8f + row * 0.9f;
                        if (row == 0 && g.R() < 0.35f) continue;
                        g.Place(trees[g.rng.Next(3)], new Vector3(x, 0, zz), g.R() * 360f, 0.95f + g.R() * 0.55f + row * 0.08f, 0.9f, 0f);
                    }
            for (float x = -24f; x <= 24f; x += 2.2f)
                for (int row = 0; row < 3; row++)
                    g.Place(trees[g.rng.Next(3)], new Vector3(x + g.R(), 0, 110f + row * 2.4f + g.R()), g.R() * 360f, 1f + g.R() * 0.5f, 0.9f, 0f);
            // Start camps (attacker side).
            Camp(g, new Vector3(-12.5f, 0, 5f), 25f);
            Camp(g, new Vector3(12.5f, 0, 3.5f), -30f);
            if (!interiorClumps) return;
            Vector2[] clumps = { new Vector2(-14f, 13f), new Vector2(14.5f, 20f), new Vector2(13.5f, 52f), new Vector2(-14.5f, 72f), new Vector2(14f, 88f), new Vector2(-15f, 39f) };
            foreach (var c in clumps)
                for (int i = 0; i < 4; i++)
                {
                    float x = c.x + (g.R() - 0.5f) * 3.5f, z = c.y + (g.R() - 0.5f) * 3.5f;
                    if (g.Free(x, z, 0.8f)) g.Place(trees[g.rng.Next(3)], new Vector3(x, 0, z), g.R() * 360f, 0.85f + g.R() * 0.4f, 0.9f);
                }
        }

        static void Camp(Gen g, Vector3 c, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            g.Place("Tent", c, yaw + 180f, 1f, 2.2f, 2.4f);
            Vector3 t2 = c + q * new Vector3(3.2f, 0, 1.2f);
            g.Place("Tent", t2, yaw + 170f, 0.9f, 2.0f, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = c + q * new Vector3(-2.2f + g.R() * 1.5f, 0, -2.4f - g.R() * 1.2f);
                g.Place(i == 1 ? "Barrel" : "Crate", p, g.R() * 360f, 0.9f + g.R() * 0.2f, 0.7f, 0.8f);
            }
        }

        static void Barricade(Gen g, Vector3 c, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            for (int i = -1; i <= 1; i++) g.Place("Fence", c + q * new Vector3(i * 1.9f, 0, 0), yaw, 1f, 1.0f);
        }

        static void SandbagArc(Gen g, Vector3 c, float r, float facing)
        {
            for (int i = -2; i <= 2; i++)
            {
                float a = facing + i * 32f;
                g.Place("Sandbags", c + Quaternion.Euler(0, a, 0) * Vector3.forward * r, a, 0.9f, 0.7f, 0f);
            }
        }

        /// <summary>A crenellated wall across the map at z with a gate between gateMin and gateMax (x).</summary>
        static void WallWithGate(Gen g, float z, float gateMin, float gateMax)
        {
            for (float x = -17f; x <= 17.01f; x += 2f)
            {
                if (x + 1f > gateMin && x - 1f < gateMax) continue;
                g.Place("Wall", new Vector3(x, 0, z), 0f, 1f, 1.2f);
            }
            g.Place("WallCorner", new Vector3(gateMin - 0.4f, 0, z), 0f, 1f, 0.8f);
            g.Place("WallCorner", new Vector3(gateMax + 0.4f, 0, z), 0f, 1f, 0.8f);
            g.Place("WallCorner", new Vector3(-18f, 0, z), 0f, 1f, 0.8f);
            g.Place("WallCorner", new Vector3(18f, 0, z), 0f, 1f, 0.8f);
        }

        static void HexCluster(Gen g, Vector3 c)
        {
            Vector3[] offs = { Vector3.zero, new Vector3(2.6f, 0, 1.5f), new Vector3(-2.6f, 0, 1.5f), new Vector3(0, 0, 3f), new Vector3(0, 0, -3f), new Vector3(2.6f, 0, -1.5f) };
            foreach (var o in offs)
            {
                if (g.rng.NextDouble() < 0.2) continue;
                g.Place("HexTile", c + o, 30f, 1f, 0f, 0f);
            }
            g.Reserve(c.x, c.z, 4.5f);
        }

        static void Scatter(Gen g, string[] names, int count, float radius, float block, float sMin, float sMax, float maxAbsX, float minAbsX = 0f)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries < count * 40)
            {
                tries++;
                float x = ((float)g.rng.NextDouble() * 2f - 1f) * maxAbsX;
                if (Mathf.Abs(x) < minAbsX) continue;
                float z = -4f + (float)g.rng.NextDouble() * 114f;
                if (!g.Free(x, z, radius)) continue;
                g.Place(names[g.rng.Next(names.Length)], new Vector3(x, 0, z), (float)g.rng.NextDouble() * 360f, Mathf.Lerp(sMin, sMax, (float)g.rng.NextDouble()), block, radius);
                placed++;
            }
        }

        static void Decor(Gen g, float rockMinAbsX = 0f, int rocks = 26)
        {
            Scatter(g, new[] { "Rock_A", "Rock_B", "Rock_C" }, rocks, 0.9f, 0.9f, 0.7f, 1.4f, 15.5f, rockMinAbsX);
            Scatter(g, new[] { "Bush_A", "Bush_B" }, 30, 0.6f, 0f, 0.8f, 1.2f, 16.5f);
            Scatter(g, new[] { "GrassTuft" }, 70, 0.3f, 0f, 0.8f, 1.3f, 17f);
            Scatter(g, new[] { "Flower_A", "Flower_B" }, 30, 0.3f, 0f, 0.8f, 1.2f, 17f);
        }

        /// <summary>Prop free sockets for generated AI layouts.</summary>
        static void Sockets(MapLayoutData m, Gen g)
        {
            var squads = new List<Vector3>();
            var mines = new List<Vector3>();
            var heavy = new List<Vector3>();
            bool NearCp(float x, float z, float r)
            {
                foreach (var c in m.capturePoints) if ((new Vector2(c.x - x, c.z - z)).magnitude < r) return true;
                return false;
            }
            for (int tries = 0; tries < 4000 && mines.Count < 16; tries++)
            {
                float z = 14f + g.R() * 86f;
                float x = (g.R() * 2f - 1f) * 14f;
                if (g.DistToPaths(x, z) > 1.4f || NearCp(x, z, 4.6f) || !g.Free(x, z, 1.2f, -10f)) continue;
                mines.Add(new Vector3(x, 0, z)); g.Reserve(x, z, 1.4f);
            }
            for (int tries = 0; tries < 4000 && squads.Count < 12; tries++)
            {
                float z = 18f + g.R() * 84f;
                float x = (g.R() * 2f - 1f) * 12.5f;
                float d = g.DistToPaths(x, z);
                if (d < 2.5f || d > 8.5f || NearCp(x, z, 5.5f) || !g.Free(x, z, 3.2f, -10f)) continue;
                squads.Add(new Vector3(x, 0, z)); g.Reserve(x, z, 3.4f);
            }
            for (int tries = 0; tries < 4000 && heavy.Count < 9; tries++)
            {
                float z = 20f + g.R() * 82f;
                float x = (g.R() * 2f - 1f) * 13.5f;
                float d = g.DistToPaths(x, z);
                if (d < 4.5f || d > 10f || NearCp(x, z, 4.6f) || !g.Free(x, z, 2.0f, -10f)) continue;
                heavy.Add(new Vector3(x, 0, z)); g.Reserve(x, z, 2.4f);
                float facing = Mathf.Atan2(-x, -6f) * Mathf.Rad2Deg + 180f;
                if (g.R() < 0.6f) SandbagArc(g, new Vector3(x, 0, z), 2.1f, facing);
            }
            m.squadSockets = squads.ToArray();
            m.mineSockets = mines.ToArray();
            m.defenseSockets = heavy.ToArray();
        }

        // ------------------------------------------------------------------ the three maps

        static void HomeLayout(MapLayoutData m, Gen g)
        {
            g.path = new[] { new Vector2(0, -6), new Vector2(0, 5), new Vector2(-2, 16), new Vector2(2, 30), new Vector2(0, 46), new Vector2(-1.5f, 60), new Vector2(2, 75), new Vector2(0, 90), new Vector2(0, 112) };
            m.capturePoints = new[] { new Vector3(0, 0, 46f), new Vector3(0, 0, 90f) };
            m.hq = new Vector3(0, 0, 105.5f);
            m.spawnHatches = new[] { new Vector3(-12f, 0, 47f), new Vector3(12f, 0, 82f), new Vector3(-11f, 0, 93f) };
            m.attackerZoneMaxZ = 18f;
            m.placeable = new Vector4(-15.5f, 18f, 15.5f, 101f);
            m.padClearance = 3.6f;
            Common(m, g, 4, false);
            // A light frame of props at the edges only: the middle stays open for the player's defenses.
            Barricade(g, new Vector3(-14.5f, 0, 24f), 80f);
            Barricade(g, new Vector3(14.5f, 0, 64f), -80f);
            HexCluster(g, new Vector3(-12.5f, 0, 70f));
            HexCluster(g, new Vector3(12.5f, 0, 32f));
            Decor(g, 12.5f, 10);
            m.squadSockets = new Vector3[0];
            m.mineSockets = new Vector3[0];
            m.defenseSockets = new Vector3[0];
        }

        static void CanyonLayout(MapLayoutData m, Gen g)
        {
            g.path = new[] { new Vector2(0, -6), new Vector2(0, 6), new Vector2(-4, 18), new Vector2(-7, 30), new Vector2(-6, 45), new Vector2(-1, 56), new Vector2(6, 66), new Vector2(8, 78), new Vector2(5, 92), new Vector2(0, 104), new Vector2(0, 112) };
            g.branches.Add(new[] { new Vector2(-6, 45), new Vector2(-12, 50) });
            m.capturePoints = new[] { new Vector3(-6f, 0, 45f), new Vector3(5f, 0, 92f) };
            m.hq = new Vector3(0, 0, 105.5f);
            m.spawnHatches = new[] { new Vector3(-13f, 0, 52f), new Vector3(13f, 0, 84f), new Vector3(-6f, 0, 98f) };
            Common(m, g, 3, false);
            // A wall across the pass with an off centre gate on the path (placed first so rocks keep clear).
            WallWithGate(g, 71f, 3f, 10.5f);
            // Rock ridges lining the pass, with a few gaps (soldiers slide around single rocks).
            string[] rocks = { "Rock_A", "Rock_B", "Rock_C" };
            for (float z = 14f; z < 100f; z += 2.2f)
            {
                float px = PathX(g.path, z);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (g.R() < 0.22f) continue;
                    float x = px + side * (8.2f + g.R() * 1.8f);
                    if (Mathf.Abs(x) > 16.5f) continue;
                    if (!g.Free(x, z, 1.1f, 2.5f)) continue;
                    g.Place(rocks[g.rng.Next(3)], new Vector3(x, 0, z), g.R() * 360f, 1.4f + g.R() * 0.7f, 1.2f, 1.3f);
                }
            }
            Camp(g, new Vector3(-13.5f, 0, 26f), 80f);
            Camp(g, new Vector3(13f, 0, 46f), -70f);
            Barricade(g, new Vector3(-1f, 0, 37f), 20f);
            Scatter(g, new[] { "Log" }, 8, 1.0f, 1.0f, 0.9f, 1.1f, 15f);
            Sockets(m, g);
            HexCluster(g, new Vector3(10f, 0, 14f));
            Decor(g, 0f, 18);
        }

        static void RidgeLayout(MapLayoutData m, Gen g)
        {
            g.path = new[] { new Vector2(0, -6), new Vector2(0, 8), new Vector2(-4, 18), new Vector2(-9, 30), new Vector2(-5, 38), new Vector2(3, 46), new Vector2(6, 56), new Vector2(9, 66), new Vector2(5, 78), new Vector2(-3, 90), new Vector2(0, 104), new Vector2(0, 112) };
            g.branches.Add(new[] { new Vector2(3, 46), new Vector2(12, 44) });
            m.capturePoints = new[] { new Vector3(3f, 0, 46f), new Vector3(-3f, 0, 90f) };
            m.hq = new Vector3(0, 0, 105.5f);
            m.spawnHatches = new[] { new Vector3(13f, 0, 40f), new Vector3(-13f, 0, 80f), new Vector3(10f, 0, 96f) };
            Common(m, g, 4, true);
            // Two walls, gates on alternating sides: the lane zig-zags through them.
            WallWithGate(g, 30f, -12.5f, -5.5f);
            WallWithGate(g, 66f, 5.5f, 12.5f);
            Camp(g, new Vector3(13.5f, 0, 22f), -80f);
            Camp(g, new Vector3(-13.5f, 0, 58f), 90f);
            Camp(g, new Vector3(13f, 0, 100f), -100f);
            Barricade(g, new Vector3(-10f, 0, 74f), 30f);
            Barricade(g, new Vector3(9f, 0, 84f), -25f);
            Sockets(m, g);
            HexCluster(g, new Vector3(-11f, 0, 47f));
            Decor(g, 0f, 22);
        }

        static float PathX(Vector2[] path, float z)
        {
            for (int i = 1; i < path.Length; i++)
                if (z >= path[i - 1].y && z <= path[i].y)
                    return Mathf.Lerp(path[i - 1].x, path[i].x, Mathf.InverseLerp(path[i - 1].y, path[i].y, z));
            return 0f;
        }

        // ------------------------------------------------------------------ ground mask

        static Texture2D PaintMask(string id, Gen g, MapLayoutData m)
        {
            const int W = 256, H = 512;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wx = MapRect.x + (x + 0.5f) / W * MapRect.z;
                    float wz = MapRect.y + (y + 0.5f) / H * MapRect.w;
                    var p = new Vector2(wx, wz);
                    float dirt = Stroke(p, g.path, 1.5f);
                    foreach (var b in g.branches) dirt = Mathf.Max(dirt, Stroke(p, b, 1.0f));
                    float tiles = 0f;
                    foreach (var t in g.tiles) tiles = Mathf.Max(tiles, Disc(p, new Vector2(t.x, t.y), t.z));
                    float edge = Mathf.InverseLerp(14f, 19f, Mathf.Abs(wx));
                    float dark = Mathf.Clamp01(edge) * (0.5f + 0.5f * Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f));
                    px[y * W + x] = new Color(dirt, tiles, dark, 1f);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = TextureGen.Dir + "/T_GroundMask_" + id + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Stroke(Vector2 p, Vector2[] poly, float halfWidth)
        {
            float best = DistToPoly(p, poly);
            return 1f - Mathf.Clamp01((best - halfWidth * 0.6f) / (halfWidth * 0.8f));
        }

        static float Disc(Vector2 p, Vector2 c, float r)
        {
            float d = Vector2.Distance(p, c);
            return 1f - Mathf.Clamp01((d - r * 0.75f) / (r * 0.35f));
        }
    }
}
