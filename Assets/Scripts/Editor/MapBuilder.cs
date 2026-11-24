using System;
using System.Collections.Generic;
using System.IO;
using TactileWars.Battle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Generates the battlefields as MapLayoutData assets (Assets/Data/Maps):
    /// - "home": the player's own base (defense battles and the base editor), the plain Meadow lane;
    /// - "lane": the data of Battle.unity's handcrafted lane (no props: the scene carries its own), the default for
    ///   battles loaded with no loadout;
    /// - "level1" to "level5": one layout per ladder level, each a labelled description below (shape, spawn, capture
    ///   points, HQ, path, terrain features) built from shared terrain helpers (border, river, wall run, compound,
    ///   hedgerow, rock spine).
    /// Each asset carries its props, ground mask (path, trampled ground, edges, water), spawn hatches and prop free
    /// sockets for the seeded enemy deployments (ProgressionBuilder). All layouts are our own designs.
    /// </summary>
    public static class MapBuilder
    {
        public const string Dir = "Assets/Data/Maps";
        static readonly Vector4 HomeRect = new Vector4(-40f, -25f, 80f, 160f);

        class Gen
        {
            public readonly List<PropPlacement> props = new List<PropPlacement>();
            public readonly List<Vector3> reserved = new List<Vector3>(); // x, z, r
            public Vector2[] path;
            public readonly List<Vector2[]> branches = new List<Vector2[]>();
            public readonly List<Vector3> tiles = new List<Vector3>(); // paved discs x, z, r
            /// <summary>Water, riverbed or ice strips: polyline and half width (painted into mask A).</summary>
            public readonly List<(Vector2[] pts, float half)> water = new List<(Vector2[], float)>();
            /// <summary>Rock spines: painted as rough edge ground (mask B) under the rocks so they read from above.</summary>
            public readonly List<(Vector2[] pts, float half)> ridges = new List<(Vector2[], float)>();
            /// <summary>Walled yards: painted as trampled ground (mask G) so a compound reads from above.</summary>
            public readonly List<Rect> yards = new List<Rect>();
            public System.Random rng;
            /// <summary>The biome's props (BiomeBuilder): every name below comes from here.</summary>
            public BiomeBuilder.PropSet set;
            public string biome;
            public float R() => (float)rng.NextDouble();
            public float Range(float a, float b) => a + (b - a) * R();

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
                float best = path != null ? DistToPoly(new Vector2(x, z), path) : 999f;
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

            public string Pick(string[] names) => names[rng.Next(names.Length)];
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

        static Vector2 V(float x, float z) => new Vector2(x, z);
        static Vector3 P(float x, float z) => new Vector3(x, 0f, z);

        /// <summary>Yaw (degrees) that turns a model's +z front toward 'dir' on the ground.</summary>
        public static float YawToward(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        /// <summary>Yaw that lays a model's local X axis along 'dir' (walls, hedges, fences).</summary>
        static float YawAlong(Vector2 dir) => Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ path queries (shared with ProgressionBuilder)

        /// <summary>Distance along the map's path of the nearest point on it, and its direction there.</summary>
        public static float PathProgress(Vector2[] path, Vector3 p, out Vector3 forward)
        {
            forward = Vector3.forward;
            if (path == null || path.Length < 2) return p.z;
            var q = new Vector2(p.x, p.z);
            float best = float.MaxValue, at = 0f, run = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 a = path[i - 1], ab = path[i] - a;
                float len = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = Vector2.Distance(q, a + ab * t);
                if (d < best && len > 1e-4f) { best = d; at = run + len * t; forward = new Vector3(ab.x / len, 0f, ab.y / len); }
                run += len;
            }
            return at;
        }

        static float PathLength(Vector2[] path)
        {
            float l = 0f;
            for (int i = 1; i < path.Length; i++) l += Vector2.Distance(path[i - 1], path[i]);
            return l;
        }

        /// <summary>Yaw for a defense at 'p' facing back down the path, toward where the player comes from.</summary>
        public static float FacingYaw(MapLayoutData m, Vector3 p)
        {
            PathProgress(m.path, p, out Vector3 fwd);
            return YawToward(-fwd);
        }

        /// <summary>Where Level 1's hand placed tutorial deployment stands on its map (ProgressionBuilder places it,
        /// the map keeps the spots free of props): the 4 grunts on circle 1, the flag bearer squad by circle 2 and the
        /// turret covering circle 2.</summary>
        public static void LevelOneDeployment(MapLayoutData m, out Vector3 guard1, out Vector3 guard2, out Vector3 turret)
        {
            Vector3 cp1 = m.capturePoints[0], cp2 = m.capturePoints[1];
            PathProgress(m.path, cp1, out Vector3 f1);
            PathProgress(m.path, cp2, out Vector3 f2);
            Vector3 side2 = new Vector3(f2.z, 0f, -f2.x); // right of the way at circle 2
            guard1 = cp1 + f1 * 1f;
            guard2 = cp2 + f2 * 3f - side2 * 4f;
            turret = cp2 + f2 * 5f + side2 * 6f;
        }

        // ------------------------------------------------------------------ entry

        [MenuItem("TactileWars/Build Map Variants")]
        public static MapLayoutData[] BuildAll()
        {
            Directory.CreateDirectory(Dir);
            // Maps of the ten level ladder and the shared canyon and ridge variants are retired.
            foreach (var old in new[] { "Map_canyon", "Map_ridge" })
                if (AssetDatabase.LoadAssetAtPath<MapLayoutData>(Dir + "/" + old + ".asset") != null) AssetDatabase.DeleteAsset(Dir + "/" + old + ".asset");
            foreach (var old in new[] { "T_GroundMask_canyon", "T_GroundMask_ridge" })
                if (File.Exists(TextureGen.Dir + "/" + old + ".png")) AssetDatabase.DeleteAsset(TextureGen.Dir + "/" + old + ".png");

            var home = BuildHome();
            var lane = BuildLane();
            var levels = new List<MapLayoutData>();
            foreach (var d in Levels) levels.Add(BuildLevel(d));
            AssetDatabase.SaveAssets();
            var all = new List<MapLayoutData> { home, lane };
            all.AddRange(levels);
            foreach (var m in levels)
                Debug.Log($"[MapBuilder] {m.id} '{m.displayName}' ({m.biome.id}): {m.props.Length} props, bounds {m.mapMin}..{m.mapMax}, " +
                          $"{m.squadSockets.Length} squad / {m.mineSockets.Length} mine / {m.defenseSockets.Length} defense sockets");
            return all.ToArray();
        }

        /// <summary>The map of ladder level 'level' (1 based) among BuildAll's result.</summary>
        public static MapLayoutData LevelMap(MapLayoutData[] maps, int level)
        {
            foreach (var m in maps) if (m != null && m.id == "level" + level) return m;
            return null;
        }

        static MapLayoutData Asset(string id)
        {
            string path = Dir + "/Map_" + id + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<MapLayoutData>(path);
            if (m == null) { m = ScriptableObject.CreateInstance<MapLayoutData>(); AssetDatabase.CreateAsset(m, path); }
            m.id = id;
            return m;
        }

        // ------------------------------------------------------------------ home base and the scene lane

        static MapLayoutData BuildHome()
        {
            var m = Asset("home");
            m.displayName = "Home Base";
            m.biome = BiomeBuilder.Meadow();
            m.mapRect = HomeRect;
            m.mapMin = new Vector2(-18f, 1f);
            m.mapMax = new Vector2(18f, 108f);
            m.focusMin = new Vector2(-1.5f, 9f);
            m.focusMax = new Vector2(1.5f, 99f);
            m.playerSpawn = new Vector3(0f, 0f, 8f);
            var g = new Gen { rng = new System.Random("home".GetHashCode() & 0x7fffffff), set = BiomeBuilder.MeadowProps, biome = "meadow" };
            HomeLayout(m, g);
            m.path = g.path;
            m.props = g.props.ToArray();
            m.groundMask = PaintMask(m, g, true);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Battle.unity's handcrafted lane as data (SceneBuilder's constants), with no props or mask: the scene
        /// default reads its path, spawn, points and hatches so everything that follows map data works there too.</summary>
        static MapLayoutData BuildLane()
        {
            var m = Asset("lane");
            m.displayName = "Classic Lane";
            m.biome = BiomeBuilder.Meadow();
            m.mapRect = HomeRect;
            m.mapMin = new Vector2(-18f, 1f);
            m.mapMax = new Vector2(18f, 108f);
            m.focusMin = new Vector2(-1.5f, 9f);
            m.focusMax = new Vector2(1.5f, 99f);
            m.playerSpawn = SceneBuilder.PlayerSpawn;
            m.capturePoints = new[] { SceneBuilder.CP1, SceneBuilder.CP2 };
            m.hq = SceneBuilder.LaneHQ;
            m.path = (Vector2[])SceneBuilder.PathPts.Clone();
            m.spawnHatches = (Vector3[])SceneBuilder.LaneHatches.Clone();
            m.props = Array.Empty<PropPlacement>();
            m.groundMask = null;
            var squads = new List<Vector3>();
            foreach (var s in SceneBuilder.SquadSockets) squads.Add(s.pos);
            m.squadSockets = squads.ToArray();
            m.mineSockets = (Vector3[])SceneBuilder.MineSockets.Clone();
            var heavy = new List<Vector3>();
            foreach (var s in SceneBuilder.DefenseSockets) heavy.Add(s.pos);
            m.defenseSockets = heavy.ToArray();
            EditorUtility.SetDirty(m);
            return m;
        }

        static void HomeLayout(MapLayoutData m, Gen g)
        {
            g.path = new[] { new Vector2(0, -6), new Vector2(0, 5), new Vector2(-2, 16), new Vector2(2, 30), new Vector2(0, 46), new Vector2(-1.5f, 60), new Vector2(2, 75), new Vector2(0, 90), new Vector2(0, 112) };
            m.capturePoints = new[] { new Vector3(0, 0, 46f), new Vector3(0, 0, 90f) };
            m.hq = new Vector3(0, 0, 105.5f);
            m.spawnHatches = new[] { new Vector3(-12f, 0, 47f), new Vector3(12f, 0, 82f), new Vector3(-11f, 0, 93f) };
            m.attackerZoneMaxZ = 18f;
            m.placeable = new Vector4(-15.5f, 18f, 15.5f, 101f);
            m.padClearance = 3.6f;
            HomeCommon(m, g);
            // A light frame of props at the edges only: the middle stays open for the player's defenses.
            Barricade(g, new Vector3(-14.5f, 0, 24f), 80f);
            Barricade(g, new Vector3(14.5f, 0, 64f), -80f);
            Clearing(g, new Vector3(-12.5f, 0, 70f));
            Clearing(g, new Vector3(12.5f, 0, 32f));
            // The same draws as before this task, so the player's base keeps every prop where it was.
            LaneScatter(g, g.set.rocks, 10, 0.9f, 0.9f, 0.7f, 1.4f, 15.5f, 12.5f);
            LaneScatter(g, g.set.shrubs, 30, 0.6f, 0f, 0.8f, 1.2f, 16.5f);
            LaneScatter(g, g.set.detail, 70, 0.3f, 0f, 0.8f, 1.3f, 17f);
            LaneScatter(g, g.set.flowers, 30, 0.3f, 0f, 0.8f, 1.2f, 17f);
            m.squadSockets = new Vector3[0];
            m.mineSockets = new Vector3[0];
            m.defenseSockets = new Vector3[0];
        }

        /// <summary>The home base frame: HQ, hatches, the tree border on both sides and the far end, two start clusters.</summary>
        static void HomeCommon(MapLayoutData m, Gen g)
        {
            g.Reserve(m.playerSpawn.x, m.playerSpawn.z, 6f);
            foreach (var c in m.capturePoints) g.Reserve(c.x, c.z, 5f);
            g.Place(g.set.hq, m.hq, 180f, 1f, 3.3f, 3.6f);
            g.tiles.Add(new Vector3(m.hq.x, m.hq.z - 1.5f, 5.5f));
            foreach (var h in m.spawnHatches) g.Place("SpawnHatch", h, 0f, 1f, 1.2f, 1.6f);
            foreach (var c in m.capturePoints) g.tiles.Add(new Vector3(c.x, c.z, 4.6f));
            string[] edge = g.set.edge;
            for (int side = -1; side <= 1; side += 2)
                for (float z = -12f; z < 124f; z += 2.1f)
                    for (int row = 0; row < 4; row++)
                    {
                        float x = side * (18.2f + row * 2.3f + g.R() * 1.6f);
                        float zz = z + g.R() * 1.8f + row * 0.9f;
                        if (row == 0 && g.R() < 0.35f) continue;
                        g.Place(edge[g.rng.Next(edge.Length)], new Vector3(x, 0, zz), g.R() * 360f, 0.95f + g.R() * 0.55f + row * 0.08f, 0.9f, 0f);
                    }
            for (float x = -24f; x <= 24f; x += 2.2f)
                for (int row = 0; row < 3; row++)
                    g.Place(edge[g.rng.Next(edge.Length)], new Vector3(x + g.R(), 0, 110f + row * 2.4f + g.R()), g.R() * 360f, 1f + g.R() * 0.5f, 0.9f, 0f);
            Cluster(g, new Vector3(-12.5f, 0, 5f), 25f);
            Cluster(g, new Vector3(12.5f, 0, 3.5f), -30f);
        }

        // ------------------------------------------------------------------ the five levels

        /// <summary>One level's layout: the shape data and the terrain features that make it.</summary>
        class LevelDesc
        {
            public int level;
            public string name, biome, teaches;
            public Vector2 min, max;
            public Vector3 spawn, hq;
            public Vector3[] points, hatches;
            public Vector2[] path;
            /// <summary>The level's own features (rivers, walls, spine, landmarks), placed before the shared dressing.</summary>
            public Action<MapLayoutData, Gen> features;
            /// <summary>Squad and defense sockets only this far along the path or more (the defended half of the map).</summary>
            public float squadFrom = 0.18f, defenseFrom = 0.2f;
            public float edgeRows = 3;
        }

        static readonly LevelDesc[] Levels =
        {
            // Level 1, Meadow, "Training Field". Teaches: deploying, moving and capturing with Attackers and a Tank.
            // A gentle S shaped track through an open field from a spawn left of centre: circle 1 left of centre past a
            // pond, circle 2 right of centre past a farmhouse, the hall at the north end.
            new LevelDesc
            {
                level = 1, name = "Training Field", biome = "meadow", teaches = "the basics: deploy, move, capture",
                min = V(-22f, 1f), max = V(22f, 110f),
                spawn = P(-6f, 8f), points = new[] { P(-10f, 42f), P(8f, 88f) }, hq = P(2f, 104f),
                path = new[] { V(-6, -6), V(-6, 8), V(-3, 18), V(-8, 29), V(-10, 42), V(-6, 53), V(2, 61), V(9, 70), V(10, 79), V(8, 88), V(3, 97), V(2, 104), V(2, 114) },
                hatches = new[] { P(-17f, 52f), P(17f, 96f) },
                features = TrainingField,
            },
            // Level 2, Meadow, "Long Road". Teaches: the Healer (a run of small fights with nowhere to rest).
            // A long winding track from a south east start through three hedgerow fields; each hedgerow (hedges and
            // dry stone walls) has one gap, on alternating sides, so the road snakes and every field is a new fight.
            new LevelDesc
            {
                level = 2, name = "Long Road", biome = "meadow", teaches = "the Healer",
                min = V(-24f, 1f), max = V(24f, 152f),
                spawn = P(14f, 8f), points = new[] { P(-2f, 58f), P(4f, 132f) }, hq = P(4f, 146f),
                path = new[] { V(14, -6), V(14, 8), V(10, 18), V(0, 24), V(-12, 30), V(-12, 40), V(-8, 48), V(-2, 58), V(6, 66), V(13, 72), V(13, 82), V(8, 90), V(-4, 98), V(-12, 106), V(-12, 116), V(-6, 124), V(4, 132), V(6, 140), V(4, 146), V(4, 156) },
                hatches = new[] { P(-19f, 64f), P(19f, 100f), P(-18f, 138f) },
                features = LongRoad,
            },
            // Level 3, Red Canyon, "Overlook". Teaches: the Sniper (long sightlines onto a defended far bank).
            // A wide open basin entered from the south west corner and crossed by a dry riverbed; both circles and the
            // stronghold sit on the far bank, left and right; mesas on the edges and rock cover lines on the near side
            // give places to shoot from.
            new LevelDesc
            {
                level = 3, name = "Overlook", biome = "canyon", teaches = "the Sniper",
                min = V(-36f, 1f), max = V(36f, 98f),
                spawn = P(-28f, 9f), points = new[] { P(-18f, 66f), P(20f, 70f) }, hq = P(2f, 88f),
                path = new[] { V(-34, -2), V(-28, 9), V(-24, 20), V(-19, 32), V(-16, 44), V(-16, 54), V(-18, 66), V(-6, 72), V(8, 73), V(20, 70), V(12, 81), V(2, 88), V(2, 100) },
                hatches = new[] { P(-31f, 78f), P(31f, 86f), P(-10f, 93f) },
                defenseFrom = 0.5f, squadFrom = 0.35f,
                features = Overlook,
            },
            // Level 4, Frost Ridge, "Twin Passes". Teaches: the Vanguard (run the lightly held pass).
            // A rock spine splits the map into two passes that rejoin in the north. The army starts on the west side;
            // circle 1 sits at the mouth of the left pass, circle 2 far up the right pass; the counterattack comes down
            // the left pass from a hatch at its top.
            new LevelDesc
            {
                level = 4, name = "Twin Passes", biome = "ridge", teaches = "the Vanguard",
                min = V(-30f, 1f), max = V(30f, 124f),
                spawn = P(-24f, 14f), points = new[] { P(-13f, 37f), P(13f, 80f) }, hq = P(0f, 114f),
                path = new[] { V(-36, 14), V(-24, 14), V(-17, 26), V(-13, 37), V(-8, 22), V(6, 20), V(12, 30), V(13, 46), V(14, 62), V(13, 80), V(12, 96), V(8, 106), V(0, 114), V(0, 128) },
                hatches = new[] { P(-14f, 92f), P(12f, 118f) },
                features = TwinPasses,
            },
            // Level 5, Meadow at Dusk, "The Fort", the final and largest level. Teaches: the Demolisher (walls,
            // bunkers and cannons). A walled compound holds both circles and the hall; its gate is behind a moat bridge
            // on the south side, and a breach opens the east wall. The army starts in the south east field.
            new LevelDesc
            {
                level = 5, name = "The Fort", biome = "dusk", teaches = "the Demolisher",
                min = V(-40f, 1f), max = V(40f, 140f),
                spawn = P(26f, 9f), points = new[] { P(-8f, 72f), P(8f, 93f) }, hq = P(0f, 107f),
                path = new[] { V(30, -4), V(26, 9), V(18, 22), V(4, 33), V(-10, 41), V(-10, 50), V(-10, 58), V(-8, 72), V(2, 84), V(8, 93), V(3, 101), V(0, 107), V(0, 116) },
                hatches = new[] { P(-16f, 108f), P(16f, 110f), P(34f, 104f) },
                defenseFrom = 0.42f, squadFrom = 0.3f, edgeRows = 3,
                features = TheFort,
            },
        };

        static MapLayoutData BuildLevel(LevelDesc d)
        {
            var m = Asset("level" + d.level);
            m.displayName = d.name;
            m.biome = BiomeBuilder.StyleFor(d.biome);
            m.mapMin = d.min;
            m.mapMax = d.max;
            // The ground covers the map plus what the camera sees past its edges.
            m.mapRect = new Vector4(d.min.x - 30f, d.min.y - 26f, d.max.x - d.min.x + 60f, d.max.y - d.min.y + 52f);
            // Pan bounds: the view is about 48 wide and reaches about 23 ahead and 14 behind the focus.
            float cx = (d.min.x + d.max.x) * 0.5f;
            float fx0 = d.min.x + 21f, fx1 = d.max.x - 21f;
            if (fx0 > fx1) fx0 = fx1 = cx;
            m.focusMin = new Vector2(Mathf.Min(fx0, cx - 1.5f), d.min.y + 8f);
            m.focusMax = new Vector2(Mathf.Max(fx1, cx + 1.5f), d.max.y - 9f);
            m.playerSpawn = d.spawn;
            m.capturePoints = d.points;
            m.hq = d.hq;
            m.spawnHatches = d.hatches;
            m.attackerZoneMaxZ = d.min.y + 17f;
            m.placeable = new Vector4(d.min.x + 2.5f, d.min.y + 17f, d.max.x - 2.5f, d.max.y - 6f);
            var g = new Gen
            {
                rng = new System.Random(1000 + d.level * 7919), set = BiomeBuilder.PropsFor(d.biome), biome = d.biome,
                path = d.path,
            };
            m.path = d.path;

            // Spawn, circles, the hall and the hatches first, so nothing else lands on them.
            g.Reserve(d.spawn.x, d.spawn.z, 6.5f);
            foreach (var c in d.points) { g.Reserve(c.x, c.z, 5.5f); g.tiles.Add(new Vector3(c.x, c.z, 4.6f)); }
            PathProgress(d.path, d.hq, out Vector3 hqFwd);
            g.Place(g.set.hq, d.hq, YawToward(-hqFwd), 1f, 3.3f, 4.2f);
            g.tiles.Add(new Vector3(d.hq.x, d.hq.z, 5.5f));
            foreach (var h in d.hatches) g.Place("SpawnHatch", h, 0f, 1f, 1.2f, 2.0f);

            d.features(m, g);
            Border(m, g, (int)d.edgeRows);
            Sockets(m, g, d.squadFrom, d.defenseFrom);
            float area = (d.max.x - d.min.x) * (d.max.y - d.min.y);
            Clumps(m, g, Mathf.RoundToInt(area / 900f));
            // Ground scatter proportional to the area (the old lane: 3850 square units, 26 rocks, 30 shrubs, 70 tufts).
            Scatter(g, g.set.rocks, Mathf.RoundToInt(area / 160f), 0.9f, 0.9f, 0.7f, 1.4f, d.min, d.max);
            Scatter(g, g.set.shrubs, Mathf.RoundToInt(area / 130f), 0.6f, 0f, 0.8f, 1.2f, d.min, d.max);
            Scatter(g, g.set.detail, Mathf.RoundToInt(area / 60f), 0.3f, 0f, 0.8f, 1.3f, d.min, d.max);
            Scatter(g, g.set.flowers, Mathf.RoundToInt(area / 140f), 0.3f, 0f, 0.8f, 1.2f, d.min, d.max);
            Scatter(g, g.set.lying, Mathf.RoundToInt(area / 700f), 1.0f, 1.0f, 0.9f, 1.1f, d.min, d.max);

            m.props = g.props.ToArray();
            m.groundMask = PaintMask(m, g, false);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Level 1: the pond by circle 1, the farmhouse with its yard by circle 2, a few fences along the track.
        static void TrainingField(MapLayoutData m, Gen g)
        {
            LevelOneDeployment(m, out _, out Vector3 guard2, out Vector3 turret);
            g.Reserve(guard2.x, guard2.z, 3.6f);
            g.Reserve(turret.x, turret.z, 2.6f);
            Landmark(g, "Pond", P(9f, 40f), 20f, 1.9f, 3.4f);
            Landmark(g, "Pond", P(13.5f, 33f), 70f, 1.1f, 2.2f);
            foreach (var p in new[] { P(5f, 37f), P(14f, 45f), P(16f, 38f) }) g.Place("Reeds", p, g.R() * 360f, 1f, 0f, 0f);
            Landmark(g, "Farmhouse", P(-12f, 76f), 95f, 1.25f, 4.5f);
            FenceRun(g, V(-18f, 70f), V(-18f, 82f), g.set.fence);
            FenceRun(g, V(-18f, 82f), V(-8f, 82f), g.set.fence);
            g.Place("HayBale", P(-15f, 72f), 30f, 1f, 0.7f);
            g.Place("HayBale", P(-16f, 74f), 80f, 1f, 0.7f);
            FenceRun(g, V(2f, 20f), V(8f, 26f), g.set.fence);
            FenceRun(g, V(-17f, 30f), V(-13f, 37f), "StoneFence");
            FenceRun(g, V(13f, 58f), V(18f, 64f), "StoneFence");
            Cluster(g, P(-16f, 6f), 25f);
            Cluster(g, P(14f, 10f), -30f);
            Cluster(g, P(16f, 102f), -100f);
            Orchard(g, P(15f, 20f), 3, 2, 3.4f);
            Orchard(g, P(-15f, 96f), 2, 3, 3.4f);
        }

        // Level 2: three hedgerows across the map with one gap each, short hedge stubs inside the fields.
        static void LongRoad(MapLayoutData m, Gen g)
        {
            Hedgerow(g, V(-25f, 36f), V(25f, 36f), new[] { -12f + 25f });
            Hedgerow(g, V(-25f, 76f), V(25f, 76f), new[] { 13f + 25f });
            Hedgerow(g, V(-25f, 112f), V(25f, 112f), new[] { -12f + 25f });
            Hedgerow(g, V(10f, 54f), V(25f, 54f), null);
            Hedgerow(g, V(-25f, 94f), V(-14f, 94f), null);
            Hedgerow(g, V(14f, 124f), V(25f, 124f), null);
            Hedgerow(g, V(-25f, 18f), V(-13f, 18f), null);
            Landmark(g, "Farmhouse", P(-16f, 9f), 80f, 1.1f, 4.2f);
            Landmark(g, "Pond", P(17f, 92f), 0f, 1.5f, 3f);
            Cluster(g, P(16f, 45f), -60f);
            Cluster(g, P(-17f, 66f), 70f);
            Cluster(g, P(15f, 140f), -80f);
            Orchard(g, P(-16f, 124f), 2, 2, 3.6f);
            Orchard(g, P(18f, 22f), 2, 2, 3.4f);
        }

        // Level 3: the dry riverbed, mesas at the edges and on the near side, boulder cover lines short of the bed,
        // and the defended far bank.
        static void Overlook(MapLayoutData m, Gen g)
        {
            River(g, new[] { V(-46, 46), V(-28, 50), V(-12, 47), V(4, 52), V(22, 48), V(46, 54) }, 4.2f, false, null);
            foreach (var c in new[] { V(-30f, 38f), V(-6f, 37f), V(11f, 40f), V(27f, 38f), V(-2f, 60f), V(30f, 60f) })
                CoverLine(g, c, 8f, g.R() * 30f - 15f);
            foreach (var p in new[] { P(-33f, 26f), P(-34f, 58f), P(33f, 22f), P(33f, 70f), P(-33f, 88f), P(30f, 92f), P(-3f, 22f), P(18f, 27f), P(-12f, 84f) })
                Butte(g, p);
            Cluster(g, P(-20f, 4f), 20f);
            Cluster(g, P(24f, 10f), -30f);
        }

        // Level 4: the central spine, the rock walls narrowing both passes, a frozen pool in the south field.
        static void TwinPasses(MapLayoutData m, Gen g)
        {
            Spine(g, new[] { V(0, 30), V(2, 44), V(-1, 58), V(1, 72), V(-1, 86), V(0, 100) }, 4.5f);
            Spine(g, new[] { V(-30, 44), V(-26, 56), V(-27, 70), V(-25, 84), V(-29, 96) }, 2.6f);
            Spine(g, new[] { V(30, 40), V(26, 52), V(27, 66), V(25, 78), V(29, 92) }, 2.6f);
            Landmark(g, "FrozenPool", P(-2f, 8f), 15f, 1.6f, 3.4f);
            Landmark(g, "FrozenPool", P(20f, 112f), 60f, 1.2f, 2.6f);
            Cluster(g, P(22f, 8f), -30f);
            Cluster(g, P(-22f, 112f), 60f);
        }

        // Level 5: the compound (gate in the south wall behind a moat bridge, a breach in the east wall), the hall
        // and both circles inside, outer earthworks, a farmhouse and an orchard in the fields.
        static void TheFort(MapLayoutData m, Gen g)
        {
            // Compound x -22..22, z 56..116: gate south at x = -10, breach east at z = 74.
            Compound(g, V(-22f, 56f), V(22f, 116f),
                gates: new[] { (0, -10f, 10f) }, breaches: new[] { (1, 74f, 10f) });
            River(g, new[] { V(-46, 47), V(-30, 49), V(-10, 50), V(6, 48), V(14, 46) }, 3f, true, new[] { (V(-10f, 50f), 10f, true) });
            // Outer earthworks: dry stone walls with gaps in the south field.
            WallRun(g, V(-38f, 30f), V(-20f, 32f), null, g.set.wall);
            WallRun(g, V(30f, 46f), V(39f, 58f), null, g.set.wall);
            WallRun(g, V(31f, 66f), V(40f, 68f), null, g.set.wall);
            Landmark(g, "Farmhouse", P(-28f, 16f), 70f, 1.3f, 4.6f);
            Orchard(g, P(-32f, 70f), 3, 3, 3.6f);
            Orchard(g, P(30f, 124f), 3, 2, 3.6f);
            Cluster(g, P(36f, 22f), -40f);
            Cluster(g, P(-34f, 128f), 60f);
            Landmark(g, "Pond", P(32f, 84f), 0f, 1.6f, 3.2f);
        }

        // ------------------------------------------------------------------ terrain helpers

        static void Landmark(Gen g, string name, Vector3 p, float yaw, float scale, float reserve)
        {
            g.Place(name, p, yaw, scale, reserve * 0.8f, reserve);
        }

        /// <summary>The biome border: rows of edge props just outside the map rectangle on the west, east and north
        /// sides (heights, sizes and gaps vary; the canyon's mesas more so, in fewer rows, so it never reads as one
        /// uniform wall).</summary>
        static void Border(MapLayoutData m, Gen g, int rows)
        {
            Vector2 a = m.mapMin, b = m.mapMax;
            bool canyon = g.biome == "canyon";
            if (canyon) rows = Mathf.Min(rows, 2);
            void Row(Vector2 from, Vector2 to, Vector2 outward)
            {
                float len = Vector2.Distance(from, to);
                Vector2 dir = (to - from) / len;
                for (float s = -6f; s < len + 6f; s += canyon ? g.Range(1.8f, 3.4f) : 2.2f)
                    for (int row = 0; row < rows; row++)
                    {
                        if (row == 0 && g.R() < 0.35f) continue;
                        if (canyon && g.R() < 0.25f) continue;
                        Vector2 q = from + dir * (s + g.R() * 1.6f + row * 0.9f) + outward * (0.4f + row * (canyon ? 3.4f : 2.4f) + g.R() * 1.6f);
                        float sc = 0.95f + g.R() * 0.55f + row * 0.08f;
                        if (canyon) sc *= g.Range(0.55f, 1.45f);
                        g.Place(g.Pick(g.set.edge), P(q.x, q.y), g.R() * 360f, sc, 0.9f, 0f);
                    }
            }
            Row(V(a.x, a.y), V(a.x, b.y), V(-1, 0));
            Row(V(b.x, a.y), V(b.x, b.y), V(1, 0));
            Row(V(a.x, b.y), V(b.x, b.y), V(0, 1));
            // No row along the south edge: the battle camera looks north, and a row there would stand in front of
            // the start view.
        }

        /// <summary>A few clumps of the biome's clump props in open ground, away from the path.</summary>
        static void Clumps(MapLayoutData m, Gen g, int count)
        {
            int made = 0;
            for (int tries = 0; tries < count * 60 && made < count; tries++)
            {
                float x = g.Range(m.mapMin.x + 3f, m.mapMax.x - 3f), z = g.Range(m.mapMin.y + 3f, m.mapMax.y - 3f);
                if (g.DistToPaths(x, z) < 7f || !g.Free(x, z, 3f)) continue;
                for (int i = 0; i < 4; i++)
                {
                    float px = x + (g.R() - 0.5f) * 3.5f, pz = z + (g.R() - 0.5f) * 3.5f;
                    if (g.Free(px, pz, 0.8f)) g.Place(g.Pick(g.set.clump), P(px, pz), g.R() * 360f, 0.85f + g.R() * 0.4f, 0.9f);
                }
                made++;
            }
        }

        /// <summary>A biome cluster (two big pieces, three small ones), as on the old tent camps.</summary>
        static void Cluster(Gen g, Vector3 c, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            g.Place(g.set.clusterBig[0], c, yaw + 180f, 1f, 2.2f, 2.4f);
            Vector3 t2 = c + q * new Vector3(3.2f, 0, 1.2f);
            g.Place(g.set.clusterBig[1], t2, yaw + 170f, 0.9f, 2.0f, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = c + q * new Vector3(-2.2f + g.R() * 1.5f, 0, -2.4f - g.R() * 1.2f);
                g.Place(g.set.clusterSmall[i], p, g.R() * 360f, 0.9f + g.R() * 0.2f, 0.7f, 0.8f);
            }
        }

        /// <summary>A small grid of the biome's trees (orchard, fir stand, cactus garden).</summary>
        static void Orchard(Gen g, Vector3 c, int cols, int rowsN, float step)
        {
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rowsN; j++)
                {
                    float x = c.x + (i - (cols - 1) * 0.5f) * step + (g.R() - 0.5f) * 0.6f;
                    float z = c.z + (j - (rowsN - 1) * 0.5f) * step + (g.R() - 0.5f) * 0.6f;
                    if (g.Free(x, z, 0.9f)) g.Place(g.set.clump[i % 2], P(x, z), g.R() * 360f, 0.9f + g.R() * 0.25f, 0.9f, 1.2f);
                }
        }

        static void Barricade(Gen g, Vector3 c, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            for (int i = -1; i <= 1; i++) g.Place(g.set.fence, c + q * new Vector3(i * 1.9f, 0, 0), yaw, 1f, 1.0f);
        }

        /// <summary>A straight run of 2 unit fence pieces from a to b.</summary>
        static void FenceRun(Gen g, Vector2 a, Vector2 b, string piece)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            Vector2 dir = (b - a) / len;
            for (int i = 0; i < n; i++)
            {
                Vector2 q = a + dir * ((i + 0.5f) * len / n);
                g.Place(piece, P(q.x, q.y), YawAlong(dir), 1f, 1.0f);
            }
        }

        /// <summary>A wall of the biome's 2 unit segments from a to b with corner pillars at the ends and beside each
        /// gap. Gaps are (distance along the run, width); a unit needs at least three hex widths (8 units).</summary>
        static void WallRun(Gen g, Vector2 a, Vector2 b, (float at, float width)[] gaps, string piece, bool endCorners = true)
        {
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / len;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            float step = len / n;
            float yaw = YawAlong(dir);
            g.ridges.Add((new[] { a, b }, 1.0f));
            for (int i = 0; i < n; i++)
            {
                float s = (i + 0.5f) * step;
                bool open = false;
                if (gaps != null) foreach (var gp in gaps) if (Mathf.Abs(s - gp.at) < gp.width * 0.5f + step * 0.5f) open = true;
                if (open) continue;
                Vector2 q = a + dir * s;
                g.Place(piece, P(q.x, q.y), yaw, step / 2f, 1.2f);
            }
            if (endCorners)
            {
                g.Place(g.set.wallCorner, P(a.x, a.y), yaw, 1f, 0.8f);
                g.Place(g.set.wallCorner, P(b.x, b.y), yaw, 1f, 0.8f);
            }
            if (gaps != null)
                foreach (var gp in gaps)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 q = a + dir * (gp.at + side * (gp.width * 0.5f + 0.3f));
                        g.Place(g.set.wallCorner, P(q.x, q.y), yaw, 1f, 0.8f);
                    }
        }

        /// <summary>A rectangular walled compound with towers on the corners. Sides: 0 south, 1 east, 2 north, 3 west;
        /// gates get a tower each side, breaches get rubble instead of a closing pillar.</summary>
        static void Compound(Gen g, Vector2 min, Vector2 max, (int side, float at, float width)[] gates, (int side, float at, float width)[] breaches)
        {
            Vector2[] c = { V(min.x, min.y), V(max.x, min.y), V(max.x, max.y), V(min.x, max.y) };
            g.yards.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = c[side], b = c[(side + 1) % 4];
                Vector2 dir = (b - a).normalized;
                var gaps = new List<(float, float)>();
                foreach (var gt in gates) if (gt.side == side) gaps.Add((Along(a, dir, gt.side, gt.at), gt.width));
                foreach (var br in breaches) if (br.side == side) gaps.Add((Along(a, dir, br.side, br.at), br.width));
                // Leave room for the towers at both corners.
                Vector2 a2 = a + dir * 1.6f, b2 = b - dir * 1.6f;
                var shifted = new List<(float, float)>();
                foreach (var gp in gaps) shifted.Add((gp.Item1 - 1.6f, gp.Item2));
                WallRun(g, a2, b2, shifted.ToArray(), g.set.wall, false);
                foreach (var gt in gates)
                    if (gt.side == side)
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Vector2 q = a + dir * (Along(a, dir, side, gt.at) + k * (gt.width * 0.5f + 1.2f));
                            g.Place(g.set.tower, P(q.x, q.y), YawToward(new Vector3(dir.y, 0f, -dir.x)), 1f, 1.6f, 1.8f);
                        }
                foreach (var br in breaches)
                    if (br.side == side)
                    {
                        Vector2 q = a + dir * Along(a, dir, side, br.at);
                        Vector2 inward = V(-dir.y, dir.x);
                        g.Place("Rubble", P(q.x + inward.x * 1.5f, q.y + inward.y * 1.5f), YawAlong(dir), 1.1f, 0f, 0f);
                        g.Place("Rubble", P(q.x - inward.x * 1.8f, q.y - inward.y * 1.8f), YawAlong(dir) + 160f, 1.0f, 0f, 0f);
                    }
            }
            foreach (var corner in c) g.Place(g.set.tower, P(corner.x, corner.y), 0f, 1f, 1.6f, 2f);
            // The walls' line is kept free of other props on both faces.
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = c[side], b = c[(side + 1) % 4];
                float len = Vector2.Distance(a, b);
                for (float s = 0f; s <= len; s += 2f) { Vector2 q = Vector2.Lerp(a, b, s / len); g.Reserve(q.x, q.y, 2.2f); }
            }
        }

        /// <summary>Distance along a compound side from its start corner for a gate given by its x (south and north
        /// sides) or z (east and west sides).</summary>
        static float Along(Vector2 a, Vector2 dir, int side, float at) =>
            side % 2 == 0 ? (at - a.x) / dir.x : (at - a.y) / dir.y;

        /// <summary>A field boundary of hedges with dry stone stretches and the odd tree, with gaps (centre distance
        /// along the run, 10 wide).</summary>
        static void Hedgerow(Gen g, Vector2 a, Vector2 b, float[] gaps)
        {
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / len;
            float yaw = YawAlong(dir);
            const float gapWidth = 10f;
            g.ridges.Add((new[] { a, b }, 1.2f));
            int stretch = 0;
            for (float s = 1f; s < len; s += 2.05f)
            {
                bool open = false;
                if (gaps != null) foreach (float gp in gaps) if (Mathf.Abs(s - gp) < gapWidth * 0.5f + 1f) open = true;
                if (open) continue;
                Vector2 q = a + dir * s;
                stretch = (stretch + 1) % 9;
                string piece = stretch < 5 ? "Hedge" : "StoneFence";
                g.Place(piece, P(q.x, q.y), yaw + (g.R() - 0.5f) * 4f, 1f, 1.0f, 1.6f);
                if (piece == "Hedge" && g.R() < 0.12f)
                    g.Place(g.Pick(g.set.edge), P(q.x + dir.y * 0.9f, q.y - dir.x * 0.9f), g.R() * 360f, 0.9f + g.R() * 0.3f, 0.9f, 0f);
            }
        }

        /// <summary>A strip of water, dry riverbed or ice along 'pts' (painted into the ground mask). Blocking water
        /// is lined with invisible blockers except at the crossings (centre, width, bridge or ford), so the nav grid
        /// and the hidden grid treat it as solid and a crossing is the only way over.</summary>
        static void River(Gen g, Vector2[] pts, float half, bool blocking, (Vector2 at, float width, bool bridge)[] crossings)
        {
            g.water.Add((pts, half));
            float step = half * 0.8f;
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float len = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / len;
                Vector2 n = V(-dir.y, dir.x);
                for (float s = 0f; s < len; s += step)
                {
                    Vector2 q = a + dir * s;
                    bool inCrossing = false;
                    if (crossings != null)
                        foreach (var c in crossings)
                            if (Vector2.Distance(q, c.at) < c.width * 0.5f + half * 0.9f) inCrossing = true;
                    g.Reserve(q.x, q.y, half + 0.8f);
                    if (blocking && !inCrossing)
                        g.props.Add(new PropPlacement { prefab = Env("Blocker"), position = P(q.x, q.y), yaw = YawAlong(dir), scale = half * 1.8f, radius = half });
                    // Bank plants or stones, sparse, never at a crossing.
                    if (!inCrossing && g.R() < 0.35f)
                    {
                        float side = g.R() < 0.5f ? -1f : 1f;
                        Vector2 bq = q + n * side * (half + 0.4f + g.R() * 0.6f);
                        g.props.Add(new PropPlacement { prefab = Env(g.Pick(g.set.bank)), position = P(bq.x, bq.y), yaw = g.R() * 360f, scale = 0.8f + g.R() * 0.4f, radius = 0f });
                    }
                }
            }
            if (crossings == null) return;
            foreach (var c in crossings)
            {
                // Lay the crossing across the river: its local z (deck length) along the river's normal there.
                Vector2 dir = Vector2.right;
                float best = float.MaxValue;
                for (int i = 1; i < pts.Length; i++)
                {
                    Vector2 a = pts[i - 1], ab = pts[i] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(c.at - a, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(c.at, a + ab * t);
                    if (d < best) { best = d; dir = ab.normalized; }
                }
                float yaw = YawToward(new Vector3(-dir.y, 0f, dir.x));
                g.props.Add(new PropPlacement { prefab = Env(c.bridge ? g.set.bridge : g.set.ford), position = P(c.at.x, c.at.y), yaw = yaw, scale = c.width / 9f, radius = 0f });
            }
        }

        /// <summary>A rock spine along 'pts': big biome rocks on both faces and the crest, with an invisible core so
        /// it is watertight for the nav grid and the hidden grid.</summary>
        static void Spine(Gen g, Vector2[] pts, float half)
        {
            g.ridges.Add((pts, half));
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float len = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / len;
                Vector2 n = V(-dir.y, dir.x);
                for (float s = 0f; s < len; s += 1.6f)
                {
                    Vector2 q = a + dir * s;
                    g.props.Add(new PropPlacement { prefab = Env("Blocker"), position = P(q.x, q.y), yaw = YawAlong(dir), scale = half * 1.5f, radius = half });
                    g.Reserve(q.x, q.y, half + 1f);
                }
                for (float s = 0f; s < len; s += 2.6f)
                {
                    Vector2 q = a + dir * (s + g.R() * 1.2f);
                    float off = (g.R() - 0.5f) * half * 1.1f;
                    Vector2 r = q + n * off;
                    g.props.Add(new PropPlacement { prefab = Env(g.Pick(g.set.spine)), position = P(r.x, r.y), yaw = g.R() * 360f, scale = g.Range(1.0f, 1.6f) * half / 4f + 0.5f, radius = half });
                }
            }
            // Rounded ends.
            foreach (var e in new[] { pts[0], pts[pts.Length - 1] })
                g.props.Add(new PropPlacement { prefab = Env(g.Pick(g.set.spine)), position = P(e.x, e.y), yaw = g.R() * 360f, scale = half / 4f + 0.6f, radius = half });
        }

        /// <summary>A short line of boulders across the way, about 'length' long, with small gaps: cover to shoot from.</summary>
        static void CoverLine(Gen g, Vector2 c, float length, float yaw)
        {
            Vector2 dir = V(Mathf.Cos(-yaw * Mathf.Deg2Rad), Mathf.Sin(-yaw * Mathf.Deg2Rad));
            for (float s = -length * 0.5f; s <= length * 0.5f; s += 1.7f)
            {
                if (g.R() < 0.18f) continue;
                Vector2 q = c + dir * s;
                if (g.DistToPaths(q.x, q.y) < 4.5f) continue;
                g.Place(g.Pick(g.set.bigRocks), P(q.x, q.y), g.R() * 360f, g.Range(1.0f, 1.4f), 0.9f, 1.2f);
            }
        }

        /// <summary>A mesa or crag group (two or three pieces of varied height).</summary>
        static void Butte(Gen g, Vector3 c)
        {
            if (!g.Free(c.x, c.z, 2.5f)) return;
            int n = 2 + g.rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c + new Vector3((g.R() - 0.5f) * 4f, 0f, (g.R() - 0.5f) * 4f);
                g.Place(g.Pick(g.set.spine), p, g.R() * 360f, g.Range(0.8f, 1.5f), 1.6f, 2.4f);
            }
        }

        /// <summary>A clearing holding the biome's landmark (home base only).</summary>
        static void Clearing(Gen g, Vector3 c)
        {
            for (int i = 0; i < 6; i++) g.rng.NextDouble();
            if (g.set.landmark != null) g.Place(g.set.landmark, c, 20f, 1f, 1.8f, 0f);
            g.Reserve(c.x, c.z, 4.5f);
        }

        /// <summary>The home base's scatter on its straight lane (unchanged draws).</summary>
        static void LaneScatter(Gen g, string[] names, int count, float radius, float block, float sMin, float sMax, float maxAbsX, float minAbsX = 0f)
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

        static void Scatter(Gen g, string[] names, int count, float radius, float block, float sMin, float sMax, Vector2 min, Vector2 max, float maxAbsX = -1f)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries < count * 40)
            {
                tries++;
                float x = Mathf.Lerp(min.x - 1f, max.x + 1f, g.R());
                float z = Mathf.Lerp(min.y - 4f, max.y + 2f, g.R());
                if (maxAbsX > 0f && Mathf.Abs(x) > maxAbsX) continue;
                if (!g.Free(x, z, radius)) continue;
                g.Place(names[g.rng.Next(names.Length)], new Vector3(x, 0, z), g.R() * 360f, Mathf.Lerp(sMin, sMax, g.R()), block, radius);
                placed++;
            }
        }

        /// <summary>Prop free sockets for the seeded enemy deployments: mines on the path, squads beside it, heavy
        /// defenses a little further off, all at least 'from' of the way along the path (the defended part).</summary>
        static void Sockets(MapLayoutData m, Gen g, float squadFrom, float defenseFrom)
        {
            var squads = new List<Vector3>();
            var mines = new List<Vector3>();
            var heavy = new List<Vector3>();
            float total = PathLength(m.path);
            float hqAt = PathProgress(m.path, m.hq, out _);
            bool NearCp(float x, float z, float r)
            {
                foreach (var c in m.capturePoints) if ((new Vector2(c.x - x, c.z - z)).magnitude < r) return true;
                return false;
            }
            bool Inside(float x, float z, float margin) =>
                x > m.mapMin.x + margin && x < m.mapMax.x - margin && z > m.mapMin.y + margin && z < m.mapMax.y - margin;
            float At(float x, float z) => PathProgress(m.path, P(x, z), out _);
            float Spawn(float x, float z) => Vector2.Distance(V(x, z), V(m.playerSpawn.x, m.playerSpawn.z));
            for (int tries = 0; tries < 6000 && mines.Count < 16; tries++)
            {
                float x = g.Range(m.mapMin.x, m.mapMax.x), z = g.Range(m.mapMin.y, m.mapMax.y);
                if (!Inside(x, z, 2f) || g.DistToPaths(x, z) > 1.4f || NearCp(x, z, 4.6f) || Spawn(x, z) < 24f) continue;
                float at = At(x, z);
                if (at < total * squadFrom || at > hqAt - 4f || !g.Free(x, z, 1.2f, -10f)) continue;
                mines.Add(P(x, z)); g.Reserve(x, z, 1.4f);
            }
            for (int tries = 0; tries < 6000 && squads.Count < 12; tries++)
            {
                float x = g.Range(m.mapMin.x, m.mapMax.x), z = g.Range(m.mapMin.y, m.mapMax.y);
                float d = g.DistToPaths(x, z);
                if (!Inside(x, z, 3.5f) || d < 2.5f || d > 8.5f || NearCp(x, z, 5.5f) || Spawn(x, z) < 32f) continue;
                if (At(x, z) < total * squadFrom || !g.Free(x, z, 3.2f, -10f)) continue;
                squads.Add(P(x, z)); g.Reserve(x, z, 3.4f);
            }
            for (int tries = 0; tries < 6000 && heavy.Count < 9; tries++)
            {
                float x = g.Range(m.mapMin.x, m.mapMax.x), z = g.Range(m.mapMin.y, m.mapMax.y);
                float d = g.DistToPaths(x, z);
                if (!Inside(x, z, 3f) || d < 4.5f || d > 11f || NearCp(x, z, 4.6f) || Spawn(x, z) < 32f) continue;
                if (At(x, z) < total * defenseFrom || !g.Free(x, z, 2.0f, -10f)) continue;
                heavy.Add(P(x, z)); g.Reserve(x, z, 2.4f);
            }
            m.squadSockets = squads.ToArray();
            m.mineSockets = mines.ToArray();
            m.defenseSockets = heavy.ToArray();
        }

        // ------------------------------------------------------------------ ground mask

        /// <summary>R path, G trampled (pads, hall, clusters), B darker edges outside the field, A 1 minus water.</summary>
        static Texture2D PaintMask(MapLayoutData m, Gen g, bool homeEdges)
        {
            Vector4 rect = m.mapRect;
            int W = homeEdges ? 256 : Mathf.Clamp(Mathf.RoundToInt(rect.z / 0.32f / 4f) * 4, 64, 512);
            int H = homeEdges ? 512 : Mathf.Clamp(Mathf.RoundToInt(rect.w / 0.32f / 4f) * 4, 64, 640);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wx = rect.x + (x + 0.5f) / W * rect.z;
                    float wz = rect.y + (y + 0.5f) / H * rect.w;
                    var p = new Vector2(wx, wz);
                    float dirt = Stroke(p, g.path, 1.5f);
                    foreach (var b in g.branches) dirt = Mathf.Max(dirt, Stroke(p, b, 1.0f));
                    float tiles = 0f;
                    foreach (var t in g.tiles) tiles = Mathf.Max(tiles, Disc(p, new Vector2(t.x, t.y), t.z));
                    foreach (var yard in g.yards)
                        if (yard.Contains(p)) tiles = Mathf.Max(tiles, 0.55f + 0.25f * Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f));
                    float edge;
                    if (homeEdges) edge = Mathf.InverseLerp(14f, 19f, Mathf.Abs(wx));
                    else
                    {
                        float inside = Mathf.Min(Mathf.Min(wx - m.mapMin.x, m.mapMax.x - wx), Mathf.Min(wz - m.mapMin.y, m.mapMax.y - wz));
                        edge = Mathf.InverseLerp(4f, -1f, inside);
                    }
                    foreach (var r in g.ridges) edge = Mathf.Max(edge, Stroke(p, r.pts, r.half * 1.6f));
                    float dark = Mathf.Clamp01(edge) * (0.5f + 0.5f * Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f));
                    float wet = 0f;
                    foreach (var w in g.water) wet = Mathf.Max(wet, Stroke(p, w.pts, w.half * 1.25f));
                    px[y * W + x] = new Color(dirt * (1f - wet), tiles, dark, 1f - wet);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = TextureGen.Dir + "/T_GroundMask_" + m.id + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = false;
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
