using System.Collections.Generic;
using System.IO;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Meta;
using TactileWars.Squad;
using TactileWars.UI;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TactileWars.EditorTools
{
    /// <summary>Builds Battle.unity (a hand laid out linear map, about 40 x 110) and Boot.unity, and registers both
    /// in the build settings. Requires AssetBuilder output (runs it first if prefabs are missing).</summary>
    public static class SceneBuilder
    {
        public const string BattlePath = "Assets/Scenes/Battle.unity";
        public const string BootPath = "Assets/Scenes/Boot.unity";
        public const string MenuPath = "Assets/Scenes/MainMenu.unity";
        public const string BaseEditorPath = "Assets/Scenes/BaseEditor.unity";
        static readonly Vector4 MapRect = new Vector4(-40f, -25f, 80f, 160f);

        // The handcrafted lane: the scene default for battles loaded with no loadout (most PlayMode tests). Every
        // ladder level has its own MapLayoutData (MapBuilder); MapBuilder also writes this lane's data as Map_lane.
        public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, 8f);
        public static readonly Vector3 CP1 = new Vector3(0f, 0f, 43f);
        public static readonly Vector3 CP2 = new Vector3(0f, 0f, 94f);
        public static readonly Vector3 LaneHQ = new Vector3(0f, 0f, 105.5f);
        public static readonly Vector3[] LaneHatches = { new Vector3(-12f, 0, 44f), new Vector3(12.5f, 0, 81f), new Vector3(-10f, 0, 92f) };
        public static readonly Vector2[] PathPts =
        {
            new Vector2(0, -6), new Vector2(0, 4), new Vector2(-2.5f, 14), new Vector2(1.5f, 25), new Vector2(0, 36), new Vector2(0, 43),
            new Vector2(-1, 50), new Vector2(-4, 58), new Vector2(2, 66), new Vector2(4.5f, 74), new Vector2(1, 84), new Vector2(0, 94), new Vector2(0, 112)
        };
        static readonly Vector2[] BranchA = { new Vector2(-1.5f, 18), new Vector2(-6, 21), new Vector2(-9, 21) };
        static readonly Vector2[] BranchB = { new Vector2(1.5f, 25), new Vector2(6, 27), new Vector2(9, 28) };
        static readonly Vector2[] BranchC = { new Vector2(-3, 58), new Vector2(-8, 59), new Vector2(-11, 59) };

        // Level 1, the tutorial level (design spec 6.1): the straight lane with no traps, no counterattack and passive
        // squads. A squad of 4 grunts with no flag bearer stands on point 1 (so the point is not clear until it is
        // beaten, and the player meets it first); one turret and a squad of 6 (5 grunts and a flag bearer) hold point 2.
        struct SquadSpec { public Vector3 pos; public bool advance; public int count; public int seed; public bool bearer; }
        static readonly SquadSpec[] Squads =
        {
            new SquadSpec { pos = new Vector3(0f, 0, 44f), advance = false, count = 4, seed = 11, bearer = false },
            new SquadSpec { pos = new Vector3(-4f, 0, 97f), advance = true, count = 5, seed = 17, bearer = true },
        };
        static readonly Vector3 TurretCP2 = new Vector3(6.5f, 0, 99f);
        /// <summary>Level 1 counts for the base card.</summary>
        public const int LevelOneSquads = 2, LevelOneDefenses = 1;
        /// <summary>Level 1 enemy soldiers: 4 grunts, then 5 grunts and a flag bearer.</summary>
        public const int LevelOneSoldiers = 10;

        // ---------------- prop free sockets for generated AI base layouts (ProgressionBuilder)
        public struct SquadSocket { public Vector3 pos; public bool holdOnly; }
        public struct DefenseSocket { public Vector3 pos; public float yaw; public bool allowTank; }

        /// <summary>Enemy squad positions known to be clear of props: the handcrafted squads plus spots on the lane.</summary>
        public static readonly SquadSocket[] SquadSockets =
        {
            new SquadSocket { pos = new Vector3(-8.5f, 0, 21.5f), holdOnly = true },
            new SquadSocket { pos = new Vector3(8.5f, 0, 28f) },
            new SquadSocket { pos = new Vector3(-6.5f, 0, 47.5f), holdOnly = true },
            new SquadSocket { pos = new Vector3(5.5f, 0, 57f) },
            new SquadSocket { pos = new Vector3(-5.5f, 0, 69f), holdOnly = true },
            new SquadSocket { pos = new Vector3(-6.5f, 0, 88.5f), holdOnly = true },
            new SquadSocket { pos = new Vector3(6.5f, 0, 99f), holdOnly = true },
            new SquadSocket { pos = new Vector3(0f, 0, 36f) },
            new SquadSocket { pos = new Vector3(-1f, 0, 52f), holdOnly = true },
            new SquadSocket { pos = new Vector3(0f, 0, 73f), holdOnly = true },
            new SquadSocket { pos = new Vector3(1.5f, 0, 87f), holdOnly = true },
        };

        /// <summary>Mine positions: the handcrafted ones plus lane spots away from squads and capture rings.</summary>
        public static readonly Vector3[] MineSockets =
        {
            new Vector3(-3.5f, 0, 17f), new Vector3(3.5f, 0, 15.5f), new Vector3(0.5f, 0, 21f), new Vector3(-4.5f, 0, 30f), new Vector3(5f, 0, 34f),
            new Vector3(1.5f, 0, 62f), new Vector3(-1.5f, 0, 64.5f), new Vector3(4.5f, 0, 63.5f), new Vector3(7f, 0, 79f), new Vector3(-7f, 0, 79.5f),
            new Vector3(-2f, 0, 26f), new Vector3(1.5f, 0, 48f), new Vector3(-2.5f, 0, 56f), new Vector3(2f, 0, 70f), new Vector3(-1.5f, 0, 81f), new Vector3(2.5f, 0, 90f),
        };

        /// <summary>Heavy defense spots: sandbag nests, the bunker and cannon pads, the tank lane, open lane edges.</summary>
        public static readonly DefenseSocket[] DefenseSockets =
        {
            new DefenseSocket { pos = new Vector3(8.5f, 0, 46.5f), yaw = 200f },
            new DefenseSocket { pos = new Vector3(-9.5f, 0, 98.5f), yaw = 160f },
            new DefenseSocket { pos = new Vector3(-11f, 0, 59f), yaw = 150f },
            new DefenseSocket { pos = new Vector3(11f, 0, 67f), yaw = 210f },
            new DefenseSocket { pos = new Vector3(0f, 0, 83.5f), yaw = 90f, allowTank = true },
            new DefenseSocket { pos = new Vector3(-12f, 0, 52f), yaw = 140f },
            new DefenseSocket { pos = new Vector3(10.5f, 0, 90f), yaw = 220f },
        };

        [MenuItem("TactileWars/Build Scenes")]
        public static void BuildAll()
        {
            if (AssetBuilder.LastPrefabs == null || AssetBuilder.LastData == null) AssetBuilder.BuildAll();
            Directory.CreateDirectory("Assets/Scenes");
            BuildBattle();
            BuildMainMenu();
            BuildBaseEditor();
            BuildBoot();
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(BattlePath, true),
            };
            if (File.Exists(BaseEditorPath)) list.Add(new EditorBuildSettingsScene(BaseEditorPath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Built Boot, MainMenu and Battle scenes. Stand-ins: " + (AssetBuilder.StandIns.Count == 0 ? "none" : string.Join(", ", AssetBuilder.StandIns)));
        }

        /// <summary>Build everything in one go: project setup, assets, scenes.</summary>
        [MenuItem("TactileWars/Build Everything")]
        public static void BuildEverything()
        {
            ProjectSetup.Run();
            AssetBuilder.BuildAll();
            BuildAll();
        }

        public static void BuildEverythingResetData()
        {
            ProjectSetup.Run();
            AssetBuilder.BuildAllResetData();
            BuildAll();
        }

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Boot");
            go.AddComponent<BootLoader>().firstScene = "MainMenu";
            var cam = new GameObject("BootCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BiomeBuilder.Meadow().background;
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        static GameObject Place(GameObject prefab, Vector3 pos, float yaw, float scale, Transform parent, string name = null)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (name != null) go.name = name;
            return go;
        }

        static GameObject Env(string name)
        {
            var p = AssetBuilder.LastPrefabs;
            return p != null && p.environment.TryGetValue(name, out var g) ? g : null;
        }

        static GameObject Str(string name)
        {
            var p = AssetBuilder.LastPrefabs;
            return p != null && p.structures.TryGetValue(name, out var g) ? g : null;
        }

        static void BuildBattle()
        {
            var data = AssetBuilder.LastData;
            var mats = AssetBuilder.LastMats;
            var meshes = AssetBuilder.LastMeshes;
            var prefabs = AssetBuilder.LastPrefabs;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            data.game.mapMin = new Vector2(-18f, 1f);
            data.game.mapMax = new Vector2(18f, 108f);
            EditorUtility.SetDirty(data.game);
            data.camera.focusMin = new Vector2(-1.5f, 9f);
            data.camera.focusMax = new Vector2(1.5f, 99f);
            EditorUtility.SetDirty(data.camera);

            // ---------------- lighting and post
            var lighting = new GameObject("Lighting");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(lighting.transform);
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -0.82f, -0.28f).normalized);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = AssetBuilder.Hex("#FFF4E2");
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            BiomeBuilder.Meadow().ApplyLighting(sun, lighting.AddComponent<SceneLighting>(), null);
            RenderSettings.fog = false;
            RenderSettings.sun = sun;
            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(lighting.transform);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumePath);
            Lightmapping.lightingSettings = GetLightingSettings();

            // ---------------- ground
            var groundMat = mats.ground;
            var mask = PaintGroundMask();
            groundMat.SetTexture("_MaskTex", mask);
            groundMat.SetVector("_MapRect", MapRect);
            EditorUtility.SetDirty(groundMat);
            var ground = new GameObject("Ground");
            ground.layer = Layers.Ground;
            ground.AddComponent<MeshFilter>().sharedMesh = MeshGen.GroundPlane("M_GroundPlane", new Vector2(MapRect.x, MapRect.y), new Vector2(MapRect.x + MapRect.z, MapRect.y + MapRect.w), 32, 64);
            var gr = ground.AddComponent<MeshRenderer>();
            gr.sharedMaterial = groundMat;
            gr.shadowCastingMode = ShadowCastingMode.Off;
            gr.receiveShadows = true;

            // ---------------- systems
            var systems = new GameObject("BattleSystems");
            var bm = systems.AddComponent<BattleManager>();
            bm.gameConfig = data.game;
            bm.battleConfig = data.battle;
            bm.prefabs = data.prefabs;
            bm.combatConfig = data.combat;
            var recorder = systems.AddComponent<StrokeRecorder>();
            recorder.config = data.formation;
            var pm = new GameObject("Projectiles").AddComponent<ProjectileManager>();
            pm.transform.SetParent(systems.transform);
            pm.prefab = prefabs.projectile;
            pm.visuals = data.visuals;
            pm.prewarm = data.combat.projectilePrewarm;
            var sm = new GameObject("Splats").AddComponent<PaintSplatManager>();
            sm.transform.SetParent(systems.transform);
            sm.visuals = data.visuals;
            sm.variantMeshes = meshes.splatQuads;
            sm.cap = data.combat.splatCap;
            var vm = new GameObject("VFX").AddComponent<VfxManager>();
            vm.transform.SetParent(systems.transform);
            vm.prefabs = prefabs.vfx.ToArray();
            var am = new GameObject("Audio").AddComponent<AudioManager>();
            am.transform.SetParent(systems.transform);
            am.library = data.sounds;

            // ---------------- camera
            var rig = new GameObject("CameraRig");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BiomeBuilder.Meadow().background;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 160f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;
            var bc = rig.AddComponent<BattleCamera>();
            bc.config = data.camera;
            bc.cam = cam;
            recorder.cam = cam;

            // ---------------- player squad
            var squadGo = new GameObject("PlayerSquad");
            var squad = squadGo.AddComponent<SquadController>();
            squad.config = data.squad;
            squad.formation = data.formation;
            squad.soldierConfig = data.playerSoldier;
            squad.recorder = recorder;
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(squadGo.transform);
            spawn.transform.position = PlayerSpawn;
            squad.spawnPoint = spawn.transform;
            var ringRoot = new GameObject("SquadRing");
            ringRoot.transform.SetParent(squadGo.transform);
            ringRoot.transform.position = PlayerSpawn;
            var ring = ringRoot.AddComponent<SquadRing>();
            var red = data.visuals.Get(data.game.playerTeam);
            var ringQ = MakeFlat("Ring", ringRoot.transform, meshes.quad, red.ring, data.squad.ringRadius * 2f, 0.0f);
            var blotQ = MakeFlat("Blot", ringRoot.transform, meshes.quad, red.blot, 5.6f, 0.005f);
            ring.ring = ringQ.transform;
            ring.blot = blotQ.transform;
            squad.ring = ring;
            var strokeGo = new GameObject("Stroke");
            strokeGo.transform.SetParent(squadGo.transform);
            squad.strokeRenderer = strokeGo.AddComponent<StrokeRenderer>();
            bm.squad = squad;
            bm.recorder = recorder;
            bc.squad = squad;
            bc.transform.position = PlayerSpawn;

            // ---------------- player army (attack battles; BattleSetup switches off whichever side is unused)
            var armyGo = new GameObject("PlayerArmy");
            var army = armyGo.AddComponent<TactileWars.Army.PlayerArmy>();
            army.roster = data.roster;
            army.soldierConfig = data.playerSoldier;
            army.navConfig = data.squad;
            army.spawnPoint = PlayerSpawn;
            bm.army = army;
            bc.army = army;
            // Unit controls, grid overlay and selection ring live under the army, so defense battles switch them off too.
            var inputGo = new GameObject("ArmyInput");
            inputGo.transform.SetParent(armyGo.transform, false);
            var armyInput = inputGo.AddComponent<TactileWars.Army.ArmyInput>();
            armyInput.army = army;
            armyInput.cameraRig = bc;
            armyInput.cam = cam;
            var overlayGo = new GameObject("HexOverlay");
            overlayGo.transform.SetParent(armyGo.transform, false);
            var overlay = overlayGo.AddComponent<TactileWars.Army.HexOverlay>();
            overlay.army = army;
            overlay.input = armyInput;
            overlay.material = mats.ghostEdit;
            var markerGo = new GameObject("SelectionMarker");
            markerGo.transform.SetParent(armyGo.transform, false);
            var marker = markerGo.AddComponent<TactileWars.Army.SelectionMarker>();
            marker.army = army;
            marker.material = mats.ghostEdit;

            // ---------------- objectives
            var objectives = new GameObject("Objectives");
            var cp1 = Place(Str("CapturePoint"), CP1, 0f, 1f, objectives.transform, "CapturePoint_1").GetComponent<CapturePoint>();
            var cp2 = Place(Str("CapturePoint"), CP2, 0f, 1f, objectives.transform, "CapturePoint_2").GetComponent<CapturePoint>();
            bm.capturePoints = new[] { cp1, cp2 };

            // ---------------- enemies
            var enemies = new GameObject("Enemies");
            for (int i = 0; i < Squads.Length; i++)
            {
                var s = Squads[i];
                var g = new GameObject($"EnemySquad_{i + 1}{(s.advance ? "_Advance" : "_Hold")}");
                g.transform.SetParent(enemies.transform);
                g.transform.position = s.pos;
                var es = g.AddComponent<EnemySquad>();
                es.config = data.enemySquad;
                es.advance = s.advance;
                es.soldierCountOverride = s.count;
                es.seed = s.seed;
                es.withFlagBearer = s.bearer;
                es.passive = true; // Level 1; a loadout sets this per base
            }

            var defenses = new GameObject("Defenses");
            Place(Str("Turret"), TurretCP2, 200f, 1f, defenses.transform, "Turret_CP2");

            // ---------------- meta, loadout and HUD (presentation listens to BattleEvents only)
            var setup = systems.AddComponent<BattleSetup>();
            setup.battleManager = bm;
            setup.squad = squad;
            setup.army = army;
            setup.enemiesRoot = enemies.transform;
            setup.defensesRoot = defenses.transform;
            setup.catalog = AssetDatabase.LoadAssetAtPath<DefenseCatalog>(ProgressionBuilder.Dir + "/DefenseCatalog.asset");
            setup.enemySquadConfig = data.enemySquad;
            setup.groundRenderer = gr;
            setup.cameraRig = bc;
            setup.sceneCounterattack = false; // Level 1
            bm.enemiesRoot = enemies.transform;
            bm.counterSquadConfig = data.enemySquad;
            bm.counterSpawns = LaneHatches;
            setup.sceneMap = AssetDatabase.LoadAssetAtPath<MapLayoutData>(MapBuilder.Dir + "/Map_lane.asset");
            // Defense battles only (BattleSetup enables them): the AI attacker and the defender's paint strike.
            var ai = systems.AddComponent<AttackerAI>();
            ai.config = data.defense;
            ai.squad = squad;
            ai.battle = bm;
            ai.enabled = false;
            var strike = systems.AddComponent<DefenderStrike>();
            strike.config = data.defense;
            strike.cam = cam;
            strike.enabled = false;
            setup.attackerAI = ai;
            setup.strike = strike;
            systems.AddComponent<BattleSession>();
            var hud = new GameObject("BattleHUD").AddComponent<BattleHUD>();
            hud.skin = AssetDatabase.LoadAssetAtPath<UISkin>(UIArtBuilder.SkinPath);
            hud.cam = cam;
            if (hud.skin == null) Debug.LogWarning("[SceneBuilder] UISkin missing: run TactileWars/Build UI first");

            // ---------------- dressing
            var env = new GameObject("Environment");
            Dress(env.transform);
            setup.environmentRoot = env.transform;

            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;
            EditorSceneManager.SaveScene(scene, BattlePath);
        }


        // ---------------------------------------------------------------- main menu

        static void BuildMainMenu()
        {
            var data = AssetBuilder.LastData;
            var mats = AssetBuilder.LastMats;
            var meshes = AssetBuilder.LastMeshes;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var team = data.game.playerTeam;

            var lighting = new GameObject("Lighting");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(lighting.transform);
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -0.82f, -0.28f).normalized);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = AssetBuilder.Hex("#FFF4E2");
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            BiomeBuilder.Meadow().ApplyLighting(sun, lighting.AddComponent<SceneLighting>(), null);
            RenderSettings.fog = false;
            RenderSettings.sun = sun;
            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(lighting.transform);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumePath);
            Lightmapping.lightingSettings = GetLightingSettings();

            // Ground with its own mask (paving under the HQ, dirt paths).
            var rect = new Vector4(-36f, -22f, 72f, 56f);
            const string gmPath = "Assets/Art/Materials/M_Ground_Menu.mat";
            var gm = AssetDatabase.LoadAssetAtPath<Material>(gmPath);
            if (gm == null) { gm = new Material(mats.ground); AssetDatabase.CreateAsset(gm, gmPath); }
            else gm.CopyPropertiesFromMaterial(mats.ground);
            gm.SetTexture("_MaskTex", PaintMenuMask(rect));
            gm.SetVector("_MapRect", rect);
            EditorUtility.SetDirty(gm);
            var ground = new GameObject("Ground");
            ground.layer = Layers.Ground;
            ground.AddComponent<MeshFilter>().sharedMesh = MeshGen.GroundPlane("M_GroundPlane_Menu", new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.z, rect.y + rect.w), 24, 18);
            var gr = ground.AddComponent<MeshRenderer>();
            gr.sharedMaterial = gm;
            gr.shadowCastingMode = ShadowCastingMode.Off;

            // Camera: the game's orthographic 35 degree view, a little closer than battle.
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0f, 0f, 5.5f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            camGo.transform.localPosition = camGo.transform.localRotation * new Vector3(0f, 0f, -60f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BiomeBuilder.Meadow().background;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 160f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;

            var world = new GameObject("HomeBase");
            var tints = new List<TeamTint>();
            var anims = new List<Animator>();

            // HQ on a paved patch, a turret nest and a squad drilling in front of it.
            var hq = Place(Env(BiomeBuilder.MeadowProps.hq), new Vector3(4.6f, 0f, 9.5f), 200f, 1f, world.transform, "HQ");
            Strip(hq, tints);
            var turret = Place(Str("Turret"), new Vector3(11f, 0f, 4.5f), 215f, 1f, world.transform, "Turret");
            Strip(turret, tints);
            SandbagArc(world.transform, new Vector3(11f, 0f, 4.5f), 2.1f, 200f);
            var bunker = Place(Str("Cannon"), new Vector3(-0.5f, 0f, -2.6f), 160f, 1f, world.transform, "Cannon");
            Strip(bunker, tints);

            var squadRoot = new GameObject("Squad").transform;
            squadRoot.SetParent(world.transform);
            var rng = new System.Random(7);
            var spots = new List<Vector3>();
            for (int i = 0; i < 11; i++)
            {
                float u = i / 10f;
                spots.Add(new Vector3(Mathf.Lerp(-8.6f, 0.2f, u), 0f, 3.2f + Mathf.Sin(u * Mathf.PI) * 2.2f));
            }
            for (int i = 0; i < 5; i++) spots.Add(new Vector3(-6.2f + i * 1.25f, 0f, 1.1f + (i % 2) * 0.35f));
            for (int i = 0; i < spots.Count; i++)
            {
                var s = Place(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/" + (i == 5 ? "Hero" : "Soldier") + ".prefab"), spots[i], 180f + ((float)rng.NextDouble() - 0.5f) * 50f, 1f, squadRoot, "Soldier_" + i);
                if (s == null) continue;
                Strip(s, tints);
                var a = s.GetComponentInChildren<Animator>();
                if (a != null) anims.Add(a);
            }
            var ringRoot = new GameObject("SquadRing").transform;
            ringRoot.SetParent(world.transform);
            ringRoot.position = new Vector3(-4.2f, 0.02f, 3.4f);
            var tm = data.visuals.Get(team);
            var ringQ = MakeFlat("Ring", ringRoot, meshes.quad, tm.ring, 12.5f, 0f);
            var blotQ = MakeFlat("Blot", ringRoot, meshes.quad, tm.blot, 3.6f, 0.005f);

            // Props framing the base.
            var set = BiomeBuilder.MeadowProps; // the home base sits in the Meadow
            Cluster(world.transform, set, new Vector3(-11.5f, 0f, 11f), 20f, rng);
            Cluster(world.transform, set, new Vector3(13.5f, 0f, 13f), -40f, rng);
            Barricade(world.transform, set.fence, new Vector3(-13.5f, 0f, 3f), 75f);
            Place(Env("Crate"), new Vector3(1.4f, 0f, 7.2f), 20f, 1f, world.transform);
            Place(Env("Barrel"), new Vector3(2.1f, 0f, 6.3f), 0f, 1f, world.transform);
            Place(Env("Crate"), new Vector3(8.4f, 0f, 8.3f), 65f, 0.9f, world.transform);
            Place(Env("SpawnHatch"), new Vector3(-8.5f, 0f, 8.2f), 0f, 1f, world.transform);
            string[] trees = set.edge;
            for (float x = -30f; x <= 30f; x += 2.3f)
                for (int row = 0; row < 3; row++)
                {
                    float z = 18.5f + row * 2.6f + (float)rng.NextDouble() * 1.6f;
                    Place(Env(trees[rng.Next(trees.Length)]), new Vector3(x + (float)rng.NextDouble(), 0f, z), (float)rng.NextDouble() * 360f, 1f + (float)rng.NextDouble() * 0.5f, world.transform);
                }
            for (int side = -1; side <= 1; side += 2)
                for (float z = -10f; z < 18f; z += 2.2f)
                    for (int row = 0; row < 2; row++)
                    {
                        float x = side * (17.5f + row * 2.4f + (float)rng.NextDouble() * 1.5f);
                        Place(Env(trees[rng.Next(trees.Length)]), new Vector3(x, 0f, z + (float)rng.NextDouble()), (float)rng.NextDouble() * 360f, 0.95f + (float)rng.NextDouble() * 0.5f, world.transform);
                    }
            foreach (var (n, cnt) in new[] { ("Rock_A", 4), ("Rock_B", 4), ("MeadowShrub", 12), ("MeadowTuft", 26), ("Wildflowers", 10), ("Flower_B", 10) })
                for (int i = 0; i < cnt; i++)
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * 16f;
                    float z = -6f + (float)rng.NextDouble() * 22f;
                    if (Mathf.Abs(x - 4.6f) < 4.5f && Mathf.Abs(z - 9.5f) < 4.5f) continue;
                    if (x > -10f && x < 1.5f && z > -0.5f && z < 7f) continue;
                    Place(Env(n), new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.4f, world.transform);
                }

            var backdrop = world.AddComponent<MenuBackdrop>();
            backdrop.visuals = data.visuals;
            backdrop.tinted = tints.ToArray();
            backdrop.soldierAnimators = anims.ToArray();
            backdrop.cameraPivot = rig.transform;
            backdrop.ringRenderer = ringQ.GetComponent<Renderer>();
            backdrop.blotRenderer = blotQ.GetComponent<Renderer>();

            var am = new GameObject("Audio").AddComponent<AudioManager>();
            am.library = data.sounds;
            am.menuMusic = true;

            var menu = new GameObject("MainMenu").AddComponent<MenuRoot>();
            menu.skin = AssetDatabase.LoadAssetAtPath<UISkin>(UIArtBuilder.SkinPath);
            menu.cam = cam;
            menu.backdrop = backdrop;
            menu.database = AssetDatabase.LoadAssetAtPath<MetaDatabase>(ProgressionBuilder.DbPath);

            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        // ---------------------------------------------------------------- base editor

        static void BuildBaseEditor()
        {
            var data = AssetBuilder.LastData;
            var mats = AssetBuilder.LastMats;
            var meshes = AssetBuilder.LastMeshes;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lighting = new GameObject("Lighting");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(lighting.transform);
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -0.82f, -0.28f).normalized);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = AssetBuilder.Hex("#FFF4E2");
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            BiomeBuilder.Meadow().ApplyLighting(sun, lighting.AddComponent<SceneLighting>(), null);
            RenderSettings.fog = false;
            RenderSettings.sun = sun;
            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(lighting.transform);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumePath);
            Lightmapping.lightingSettings = GetLightingSettings();

            // Ground: the battle ground material; the home map's mask is applied at runtime (MapApplier).
            var ground = new GameObject("Ground");
            ground.layer = Layers.Ground;
            ground.AddComponent<MeshFilter>().sharedMesh = MeshGen.GroundPlane("M_GroundPlane", new Vector2(MapRect.x, MapRect.y), new Vector2(MapRect.x + MapRect.z, MapRect.y + MapRect.w), 32, 64);
            var gr = ground.AddComponent<MeshRenderer>();
            gr.sharedMaterial = mats.ground;
            gr.shadowCastingMode = ShadowCastingMode.Off;
            gr.receiveShadows = true;

            var rig = new GameObject("CameraRig");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 13.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BiomeBuilder.Meadow().background;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 160f;
            camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            camGo.transform.position = new Vector3(0f, 0f, 34f) - camGo.transform.rotation * Vector3.forward * 60f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;

            var am = new GameObject("Audio").AddComponent<AudioManager>();
            am.library = data.sounds;
            am.menuMusic = true;

            var world = new GameObject("World");
            var root = new GameObject("BaseEditor").AddComponent<BaseEditorRoot>();
            root.skin = AssetDatabase.LoadAssetAtPath<UISkin>(UIArtBuilder.SkinPath);
            root.cam = cam;
            root.database = AssetDatabase.LoadAssetAtPath<MetaDatabase>(ProgressionBuilder.DbPath);
            root.visuals = data.visuals;
            root.prefabs = data.prefabs;
            root.ground = gr;
            root.worldRoot = world.transform;
            root.capturePointPrefab = Str("CapturePoint");
            root.quad = meshes.quad;
            root.ghostMaterial = mats.ghostEdit;
            root.gridMaterial = mats.gridEdit;
            root.discMaterial = mats.discEdit;
            root.ringMaterial = mats.neutralArc;

            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;
            EditorSceneManager.SaveScene(scene, BaseEditorPath);
        }

        /// <summary>Menu props are decoration: unpack the prefab and drop gameplay scripts, keep tints and animators.</summary>
        static void Strip(GameObject go, List<TeamTint> tints)
        {
            if (go == null) return;
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var s in go.GetComponentsInChildren<Soldier>(true)) Object.DestroyImmediate(s);
            foreach (var s in go.GetComponentsInChildren<StructureBase>(true)) Object.DestroyImmediate(s);
            foreach (var s in go.GetComponentsInChildren<HealthArc>(true)) Object.DestroyImmediate(s.gameObject);
            foreach (var s in go.GetComponentsInChildren<Health>(true)) Object.DestroyImmediate(s);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<TeamTint>(true)) { t.applyOnStart = false; tints.Add(t); }
        }

        static Texture2D PaintMenuMask(Vector4 rect)
        {
            const int W = 256, H = 200;
            var px = new Color[W * H];
            Vector2[] pathA = { new Vector2(-30f, 2f), new Vector2(-16f, 3.5f), new Vector2(-6f, 6.5f), new Vector2(2f, 7.5f) };
            Vector2[] pathB = { new Vector2(3.5f, 6.5f), new Vector2(2.5f, 0f), new Vector2(0f, -8f), new Vector2(-2f, -20f) };
            Vector2[] pathC = { new Vector2(8f, 8f), new Vector2(14f, 7f), new Vector2(30f, 9f) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wx = rect.x + (x + 0.5f) / W * rect.z;
                    float wz = rect.y + (y + 0.5f) / H * rect.w;
                    var p = new Vector2(wx, wz);
                    float dirt = Mathf.Max(Stroke(p, pathA, 1.4f), Mathf.Max(Stroke(p, pathB, 1.3f), Stroke(p, pathC, 1.1f)));
                    float tiles = Disc(p, new Vector2(4.6f, 9.5f), 5.6f);
                    float edge = Mathf.InverseLerp(13f, 20f, Mathf.Abs(wx)) + Mathf.InverseLerp(15f, 22f, wz);
                    float dark = Mathf.Clamp01(edge) * (0.5f + 0.5f * Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f));
                    px[y * W + x] = new Color(dirt, tiles, dark, 1f);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = TextureGen.Dir + "/T_GroundMask_Menu.png";
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

        static LightingSettings GetLightingSettings()
        {
            const string path = "Assets/Settings/TW_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                ls = new LightingSettings { name = "TW_Lighting" };
                AssetDatabase.CreateAsset(ls, path);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            EditorUtility.SetDirty(ls);
            return ls;
        }

        static GameObject MakeFlat(string name, Transform parent, Mesh mesh, Material mat, float size, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------- set dressing

        static readonly List<Vector4> reserved = new List<Vector4>(); // x, z, radius

        static bool Free(float x, float z, float r)
        {
            foreach (var v in reserved)
            {
                float dx = x - v.x, dz = z - v.y;
                if (dx * dx + dz * dz < (r + v.z) * (r + v.z)) return false;
            }
            return DistToPath(x, z) > r + 1.6f;
        }

        static float DistToPath(float x, float z)
        {
            float best = 999f;
            var p = new Vector2(x, z);
            foreach (var poly in new[] { PathPts, BranchA, BranchB, BranchC })
                for (int i = 1; i < poly.Length; i++)
                {
                    Vector2 a = poly[i - 1], b = poly[i];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        static void Reserve(float x, float z, float r) => reserved.Add(new Vector4(x, z, r, 0));

        static void Dress(Transform parent)
        {
            reserved.Clear();
            Reserve(PlayerSpawn.x, PlayerSpawn.z, 6f);
            Reserve(CP1.x, CP1.z, 5f);
            Reserve(CP2.x, CP2.z, 5f);
            // Keep props off every squad and mine socket (also used by generated bases on this lane).
            for (int i = 0; i < 7; i++) Reserve(SquadSockets[i].pos.x, SquadSockets[i].pos.z, 4f);
            for (int i = 0; i < 10; i++) Reserve(MineSockets[i].x, MineSockets[i].z, 1.5f);
            Reserve(8.5f, 46.5f, 2.5f); Reserve(-9.5f, 98.5f, 2.5f); Reserve(-11f, 59f, 3.5f); Reserve(11f, 67f, 2.5f);
            for (float x = -8f; x <= 8f; x += 2f) Reserve(x, 84f, 2f);

            Reserve(10f, 13f, 4.5f); Reserve(-12f, 52f, 4.5f); Reserve(10.5f, 90f, 4.5f); // open spots kept clear (defense sockets)
            var rng = new System.Random(4242);
            float R() => (float)rng.NextDouble();
            var set = BiomeBuilder.MeadowProps; // the lane is a Meadow
            string[] trees = set.edge;

            // Round leafy woods framing both edges, denser further out.
            for (int side = -1; side <= 1; side += 2)
                for (float z = -12f; z < 124f; z += 2.1f)
                    for (int row = 0; row < 4; row++)
                    {
                        float x = side * (18.2f + row * 2.3f + R() * 1.6f);
                        float zz = z + R() * 1.8f + row * 0.9f;
                        if (row == 0 && R() < 0.35f) continue;
                        Place(Env(trees[rng.Next(trees.Length)]), new Vector3(x, 0, zz), R() * 360f, 0.95f + R() * 0.55f + row * 0.08f, parent);
                    }
            // Beyond the far end, close the map.
            for (float x = -24f; x <= 24f; x += 2.2f)
                for (int row = 0; row < 3; row++)
                    Place(Env(trees[rng.Next(trees.Length)]), new Vector3(x + R(), 0, 110f + row * 2.4f + R()), R() * 360f, 1f + R() * 0.5f, parent);

            // A few tree and hedge clumps inside the play space to break symmetry.
            Vector2[] clumps = { new Vector2(-14f, 13f), new Vector2(14.5f, 20f), new Vector2(13.5f, 52f), new Vector2(-14.5f, 72f), new Vector2(14f, 88f), new Vector2(-15f, 39f) };
            foreach (var c in clumps)
                for (int i = 0; i < 4; i++)
                {
                    float x = c.x + (R() - 0.5f) * 3.5f, z = c.y + (R() - 0.5f) * 3.5f;
                    if (Free(x, z, 0.8f)) { Place(Env(set.clump[rng.Next(set.clump.Length)]), new Vector3(x, 0, z), R() * 360f, 0.85f + R() * 0.4f, parent); Reserve(x, z, 0.9f); }
                }

            // Farm clusters (hedges and hay) where the old camps stood: two at the start, three further up.
            Cluster(parent, set, new Vector3(-12.5f, 0, 5f), 25f, rng);
            Cluster(parent, set, new Vector3(12.5f, 0, 3.5f), -30f, rng);
            Cluster(parent, set, new Vector3(-14.5f, 0, 27f), 80f, rng);
            Cluster(parent, set, new Vector3(14.5f, 0, 60f), -70f, rng);
            Cluster(parent, set, new Vector3(13.5f, 0, 101f), -100f, rng);

            // Fences near the first squads and the gate.
            Barricade(parent, set.fence, new Vector3(-11.5f, 0, 18.5f), 70f);
            Barricade(parent, set.fence, new Vector3(11.5f, 0, 31.5f), -60f);
            Barricade(parent, "StoneFence", new Vector3(9f, 0, 70.5f), 20f);
            Barricade(parent, "StoneFence", new Vector3(-3.5f, 0, 37.5f), 5f);

            // Sandbags around the turrets and the cannon.
            SandbagArc(parent, new Vector3(8.5f, 0, 46.5f), 2.1f, 200f);
            SandbagArc(parent, TurretCP2, 2.1f, 200f);
            SandbagArc(parent, new Vector3(11f, 0, 67f), 2.2f, 210f);

            // Crenellated wall with a central gate before the second point.
            for (float x = -17f; x <= -5f; x += 2f) { Place(Env(set.wall), new Vector3(x, 0, 77f), 0f, 1f, parent); Reserve(x, 77f, 1.2f); }
            for (float x = 5f; x <= 17f; x += 2f) { Place(Env(set.wall), new Vector3(x, 0, 77f), 0f, 1f, parent); Reserve(x, 77f, 1.2f); }
            Place(Env(set.wallCorner), new Vector3(-4f, 0, 77f), 0f, 1f, parent);
            Place(Env(set.wallCorner), new Vector3(4f, 0, 77f), 0f, 1f, parent);
            Place(Env(set.wallCorner), new Vector3(-18f, 0, 77f), 0f, 1f, parent);
            Place(Env(set.wallCorner), new Vector3(18f, 0, 77f), 0f, 1f, parent);

            // Enemy HQ behind the last point, antenna next to it, spawn hatches.
            Place(Env(set.hq), LaneHQ, 180f, 1f, parent); Reserve(LaneHQ.x, LaneHQ.z, 3.5f);
            Place(Env("TeslaAntenna"), new Vector3(-6.5f, 0, 105f), 0f, 1f, parent); Reserve(-6.5f, 105f, 1.2f);
            Place(Env("SpawnHatch"), new Vector3(-12f, 0, 44f), 0f, 1f, parent); Reserve(-12f, 44f, 1.5f);
            Place(Env("SpawnHatch"), new Vector3(12.5f, 0, 81f), 0f, 1f, parent); Reserve(12.5f, 81f, 1.5f);
            Place(Env("SpawnHatch"), new Vector3(-10f, 0, 92f), 0f, 1f, parent); Reserve(-10f, 92f, 1.5f);

            // No paving clusters on this lane. The random draws they used are still taken so the scattered props
            // below stay where they were.
            for (int i = 0; i < 18; i++) rng.NextDouble();

            // A pond in an open corner of the meadow, off the lane and every socket.
            foreach (var c in new[] { new Vector2(-13f, 33f), new Vector2(12.5f, 84f), new Vector2(-13.5f, 85f) })
                if (Free(c.x, c.y, 2.4f)) { Place(Env(set.landmark), new Vector3(c.x, 0f, c.y), 15f, 1f, parent); Reserve(c.x, c.y, 2.6f); break; }

            // Stones, shrubs, tufts and wildflowers scattered with rejection sampling.
            Scatter(parent, set.rocks, 26, 0.9f, 0.7f, 1.4f, rng, 15.5f);
            Scatter(parent, set.shrubs, 30, 0.6f, 0.8f, 1.2f, rng, 16.5f);
            Scatter(parent, set.detail, 70, 0.3f, 0.8f, 1.3f, rng, 17f);
            Scatter(parent, set.flowers, 30, 0.3f, 0.8f, 1.2f, rng, 17f);
            Scatter(parent, set.lying, 5, 1.0f, 0.9f, 1.1f, rng, 15f);
        }

        /// <summary>A biome cluster where a tent camp used to stand: two big pieces, three small ones.</summary>
        static void Cluster(Transform parent, BiomeBuilder.PropSet set, Vector3 c, float yaw, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Place(Env(set.clusterBig[0]), c, yaw + 180f, 1f, parent); Reserve(c.x, c.z, 2.4f);
            Vector3 t2 = c + q * new Vector3(3.2f, 0, 1.2f);
            Place(Env(set.clusterBig[1]), t2, yaw + 170f, 0.9f, parent); Reserve(t2.x, t2.z, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = c + q * new Vector3(-2.2f + R() * 1.5f, 0, -2.4f - R() * 1.2f);
                Place(Env(set.clusterSmall[i]), p, R() * 360f, 0.9f + R() * 0.2f, parent); Reserve(p.x, p.z, 0.8f);
            }
        }

        static void Barricade(Transform parent, string fence, Vector3 c, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = c + q * new Vector3(i * 1.9f, 0, 0);
                Place(Env(fence), p, yaw, 1f, parent); Reserve(p.x, p.z, 1f);
            }
        }

        static void SandbagArc(Transform parent, Vector3 c, float r, float facing)
        {
            for (int i = -2; i <= 2; i++)
            {
                float a = facing + i * 32f;
                Vector3 p = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                Place(Env("Sandbags"), p, a, 0.9f, parent);
            }
        }

        static void Scatter(Transform parent, string[] names, int count, float radius, float sMin, float sMax, System.Random rng, float maxAbsX)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries < count * 40)
            {
                tries++;
                float x = ((float)rng.NextDouble() * 2f - 1f) * maxAbsX;
                float z = -4f + (float)rng.NextDouble() * 114f;
                if (!Free(x, z, radius)) continue;
                var prefab = Env(names[rng.Next(names.Length)]);
                if (prefab == null) return;
                Place(prefab, new Vector3(x, 0, z), (float)rng.NextDouble() * 360f, Mathf.Lerp(sMin, sMax, (float)rng.NextDouble()), parent);
                Reserve(x, z, radius);
                placed++;
            }
        }

        // ---------------------------------------------------------------- ground mask

        static Texture2D PaintGroundMask()
        {
            const int W = 256, H = 512;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wx = MapRect.x + (x + 0.5f) / W * MapRect.z;
                    float wz = MapRect.y + (y + 0.5f) / H * MapRect.w;
                    var p = new Vector2(wx, wz);
                    float dirt = 0f;
                    dirt = Mathf.Max(dirt, Stroke(p, PathPts, 1.5f));
                    dirt = Mathf.Max(dirt, Stroke(p, BranchA, 1.0f));
                    dirt = Mathf.Max(dirt, Stroke(p, BranchB, 1.0f));
                    dirt = Mathf.Max(dirt, Stroke(p, BranchC, 1.0f));
                    float tiles = 0f;
                    tiles = Mathf.Max(tiles, Disc(p, new Vector2(CP1.x, CP1.z), 4.6f));
                    tiles = Mathf.Max(tiles, Disc(p, new Vector2(CP2.x, CP2.z), 4.6f));
                    tiles = Mathf.Max(tiles, Disc(p, new Vector2(0f, 104f), 5.5f));
                    tiles = Mathf.Max(tiles, Disc(p, new Vector2(-13f, 26f), 2.8f));
                    tiles = Mathf.Max(tiles, Disc(p, new Vector2(14f, 59f), 2.8f));
                    float edge = Mathf.InverseLerp(14f, 19f, Mathf.Abs(wx));
                    float dark = Mathf.Clamp01(edge) * (0.5f + 0.5f * Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f));
                    px[y * W + x] = new Color(dirt, tiles, dark, 1f);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = TextureGen.Dir + "/T_GroundMask_Battle.png";
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
            float best = 999f;
            for (int i = 1; i < poly.Length; i++)
            {
                Vector2 a = poly[i - 1], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return 1f - Mathf.Clamp01((best - halfWidth * 0.6f) / (halfWidth * 0.8f));
        }

        static float Disc(Vector2 p, Vector2 c, float r)
        {
            float d = Vector2.Distance(p, c);
            return 1f - Mathf.Clamp01((d - r * 0.75f) / (r * 0.35f));
        }
    }
}
