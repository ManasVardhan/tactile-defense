using System.Collections.Generic;
using System.IO;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace TactileWars.EditorTools
{
    public static partial class AssetBuilder
    {
        public class Prefabs
        {
            public Soldier soldier, hero, flagBearer;
            public FlagBubble bubble;
            public PaintProjectile projectile;
            public List<PooledVfx> vfx = new List<PooledVfx>();
            public Dictionary<string, GameObject> structures = new Dictionary<string, GameObject>();
            public Dictionary<string, GameObject> environment = new Dictionary<string, GameObject>();
        }

        static T SavePrefab<T>(GameObject root, string path) where T : Component
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved != null ? saved.GetComponent<T>() : null;
        }

        static GameObject SavePrefabGO(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject InstantiateModel(GameObject model, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Quad(string name, Transform parent, Mesh mesh, Material mat, float size, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>Simple procedural stand-in so the scene still works when a model never arrived.</summary>
        static GameObject StandIn(string name, Transform parent, Meshes meshes, Material top, Material body, float height, float width)
        {
            StandIns.Add(name);
            Debug.LogWarning("[AssetBuilder] Model missing, using STANDIN_" + name);
            var root = new GameObject("STANDIN_" + name);
            root.transform.SetParent(parent, false);
            var b = new GameObject("STANDIN_Body");
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0, height * 0.3f, 0);
            b.transform.localScale = new Vector3(width * 0.8f, height * 0.6f, width * 0.8f);
            b.AddComponent<MeshFilter>().sharedMesh = meshes.sphere;
            b.AddComponent<MeshRenderer>().sharedMaterial = body;
            var h = new GameObject("STANDIN_Top");
            h.transform.SetParent(root.transform, false);
            h.transform.localPosition = new Vector3(0, height * 0.72f, 0);
            h.transform.localScale = new Vector3(width, height * 0.55f, width);
            h.AddComponent<MeshFilter>().sharedMesh = meshes.sphere;
            h.AddComponent<MeshRenderer>().sharedMaterial = top;
            return root;
        }

        static Prefabs BuildPrefabs(Mats mats, Meshes meshes, Dictionary<string, AnimatorController> controllers, Data data)
        {
            var p = new Prefabs();
            p.soldier = BuildSoldier("Soldier", "Soldier", 0.9f, mats, meshes, controllers);
            p.hero = File.Exists(CharDir + "/Hero.fbx") ? BuildSoldier("Hero", "Hero", 0.85f, mats, meshes, controllers) : null;
            p.flagBearer = BuildSoldier(File.Exists(CharDir + "/SoldierHeavy.fbx") ? "SoldierHeavy" : "Soldier", "FlagBearer", 1.0f, mats, meshes, controllers);
            p.bubble = BuildBubble(mats, meshes);
            p.projectile = BuildProjectile(mats, meshes);
            BuildVfx(p, mats);
            BuildStructures(p, mats, meshes, data);
            BuildEnvironment(p, mats, meshes);
            return p;
        }

        static void ConfigureCharacterRenderers(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off; // blob shadows instead
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    smr.skinnedMotionVectors = false;
                }
            }
        }

        static Soldier BuildSoldier(string modelName, string prefabName, float scale, Mats mats, Meshes meshes, Dictionary<string, AnimatorController> controllers)
        {
            var root = new GameObject(prefabName);
            root.layer = Layers.PlayerUnit;
            root.AddComponent<Health>();
            var soldier = root.AddComponent<Soldier>();
            var tint = root.AddComponent<TeamTint>();
            tint.unitVariant = true;
            tint.baseTeamColor = mats.unitSlots["TeamColor"];
            tint.baseTeamColorDark = mats.unitSlots["TeamColorDark"];
            tint.basePaint = mats.unitSlots["Paint"];

            var modelAsset = LoadModel(CharDir, modelName);
            GameObject model;
            if (modelAsset != null)
            {
                model = InstantiateModel(modelAsset, root.transform, "Model");
                var anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                if (controllers.TryGetValue(modelName, out var ac)) anim.runtimeAnimatorController = ac;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                soldier.animator = anim;
            }
            else
            {
                model = StandIn(prefabName, root.transform, meshes, mats.unitSlots["TeamColor"], mats.unitSlots["Skin"], 1f, 0.6f);
                model.name = "Model";
            }
            model.transform.localScale = Vector3.one * scale;
            // Facing check: the contract says characters face +Z. Verify with the muzzle and correct if needed.
            var mz = FindDeep(model.transform, "Muzzle");
            if (mz != null)
            {
                Vector3 local = root.transform.InverseTransformPoint(mz.position);
                Debug.Log($"[AssetBuilder] {modelName} muzzle in root space {local} (expect +Z)");
                if (local.z < -0.05f)
                {
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    Debug.LogWarning($"[AssetBuilder] {modelName} faces -Z, rotated the model 180 degrees");
                }
            }
            ConfigureCharacterRenderers(model);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            tint.renderers = renderers;
            soldier.modelRoot = model.transform;
            soldier.bodyRenderers = renderers;
            soldier.muzzle = FindDeep(model.transform, "Muzzle");
            soldier.tint = tint;
            var blob = Quad("BlobShadow", root.transform, meshes.quad, mats.blobShadow, 1.05f * scale, 0.015f);
            soldier.blobShadow = blob.transform;
            return SavePrefab<Soldier>(root, PrefabDir + "/Units/" + prefabName + ".prefab");
        }

        static FlagBubble BuildBubble(Mats mats, Meshes meshes)
        {
            var root = new GameObject("FlagBubble");
            root.layer = Layers.Pickup;
            var fb = root.AddComponent<FlagBubble>();
            var col = root.AddComponent<SphereCollider>();
            col.radius = 1.15f;
            col.center = new Vector3(0f, 0.85f, 0f);
            col.isTrigger = true;
            col.enabled = false;
            fb.tapCollider = col;

            var sphere = new GameObject("Sphere");
            sphere.layer = Layers.Pickup;
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            sphere.transform.localScale = Vector3.one * 2.0f;
            sphere.AddComponent<MeshFilter>().sharedMesh = meshes.sphere;
            var sr = sphere.AddComponent<MeshRenderer>();
            sr.sharedMaterial = mats.bubble;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            fb.sphere = sphere.transform;

            var arc = Quad("Arc", root.transform, meshes.quad, mats.teams[TeamColorId.Red].arc, 2.7f, 0.03f);
            arc.layer = Layers.Pickup;
            fb.arcRenderer = arc.GetComponent<Renderer>();

            var flagAsset = LoadModel(CharDir, "Flag");
            GameObject flag;
            if (flagAsset != null)
            {
                flag = InstantiateModel(flagAsset, root.transform, "Flag");
            }
            else
            {
                flag = StandIn("Flag", root.transform, meshes, mats.slots["TeamColor"], mats.slots["Wood"], 1.6f, 0.15f);
            }
            foreach (var r in flag.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; }
            var ft = flag.AddComponent<TeamTint>();
            ft.baseTeamColor = mats.slots["TeamColor"];
            ft.baseTeamColorDark = mats.slots["TeamColorDark"];
            ft.basePaint = mats.slots["Paint"];
            ft.renderers = flag.GetComponentsInChildren<Renderer>(true);
            fb.flag = flag.transform;
            fb.flagTint = ft;
            return SavePrefab<FlagBubble>(root, PrefabDir + "/Units/FlagBubble.prefab");
        }

        static PaintProjectile BuildProjectile(Mats mats, Meshes meshes)
        {
            var root = new GameObject("PaintBall");
            root.layer = Layers.Projectile;
            root.AddComponent<MeshFilter>().sharedMesh = meshes.quad;
            var r = root.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats.teams[TeamColorId.Red].projectile;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var pp = root.AddComponent<PaintProjectile>();
            pp.meshRenderer = r;
            return SavePrefab<PaintProjectile>(root, PrefabDir + "/VFX/PaintBall.prefab");
        }

        // ------------------------------------------------------------------ VFX

        static ParticleSystem PS(Transform parent, string name, Material mat, int count, Vector2 life, Vector2 speed, Vector2 size, Color color,
            float gravity, ParticleSystemShapeType shape, float radius, bool fade = true, bool grow = false, int maxParticles = 64)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = shape;
            sh.radius = radius;
            if (shape == ParticleSystemShapeType.Hemisphere || shape == ParticleSystemShapeType.Cone) sh.rotation = new Vector3(-90f, 0f, 0f);
            if (shape == ParticleSystemShapeType.Cone) sh.angle = 35f;
            if (fade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = grow ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.35f))
                            : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.55f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortMode = ParticleSystemSortMode.None;
            return ps;
        }

        static PooledVfx VfxRoot(VfxType type, float lifetime, out GameObject root)
        {
            root = new GameObject("VFX_" + type);
            var fx = root.AddComponent<PooledVfx>();
            fx.type = type;
            fx.lifetime = lifetime;
            return fx;
        }

        static void Finish(PooledVfx fx, GameObject root, List<ParticleSystem> systems, List<bool> tint, Prefabs p)
        {
            fx.systems = systems.ToArray();
            fx.tintable = tint.ToArray();
            var saved = SavePrefab<PooledVfx>(root, PrefabDir + "/VFX/VFX_" + fx.type + ".prefab");
            p.vfx.Add(saved);
        }

        static void BuildVfx(Prefabs p, Mats mats)
        {
            var hemi = ParticleSystemShapeType.Hemisphere;
            var sphere = ParticleSystemShapeType.Sphere;

            // Hit: small paint droplets in the shooter color plus a tiny white flash.
            {
                var fx = VfxRoot(VfxType.Hit, 0.5f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Droplets", mats.pDot, 6, new Vector2(0.22f, 0.38f), new Vector2(1.6f, 3.4f), new Vector2(0.07f, 0.14f), Color.white, 1.4f, hemi, 0.05f)); t.Add(true);
                s.Add(PS(root.transform, "Flash", mats.pSoft, 1, new Vector2(0.08f, 0.1f), Vector2.zero, new Vector2(0.45f, 0.55f), new Color(1, 1, 1, 0.9f), 0f, sphere, 0.01f)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Puff: white cloud when a unit pops.
            {
                var fx = VfxRoot(VfxType.Puff, 0.7f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Cloud", mats.pPuff, 4, new Vector2(0.3f, 0.45f), new Vector2(0.4f, 1.0f), new Vector2(0.3f, 0.55f), new Color(1f, 1f, 1f, 0.85f), -0.1f, sphere, 0.12f, true, true)); t.Add(false);
                s.Add(PS(root.transform, "Flash", mats.pStar, 1, new Vector2(0.1f, 0.12f), Vector2.zero, new Vector2(0.9f, 1.0f), Color.white, 0f, sphere, 0.01f)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Ghost: translucent wisp of the victim rising.
            {
                var fx = VfxRoot(VfxType.Ghost, 1.1f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                var g = PS(root.transform, "Ghost", mats.pGhost, 1, new Vector2(0.9f, 1.0f), new Vector2(0f, 0f), new Vector2(0.75f, 0.8f), new Color(1, 1, 1, 0.65f), -0.35f, sphere, 0.01f, true, true);
                var main = g.main; main.startRotation = 0f;
                var vel = g.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f); vel.y = new ParticleSystem.MinMaxCurve(0.6f, 0.8f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                s.Add(g); t.Add(true);
                Finish(fx, root, s, t, p);
            }
            // Explosion: paint droplets in the attacker color, white starburst and dust.
            {
                var fx = VfxRoot(VfxType.Explosion, 1.0f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Paint", mats.pDot, 22, new Vector2(0.4f, 0.7f), new Vector2(3.5f, 7f), new Vector2(0.14f, 0.32f), Color.white, 2.2f, hemi, 0.3f)); t.Add(true);
                s.Add(PS(root.transform, "Star", mats.pStar, 1, new Vector2(0.14f, 0.16f), Vector2.zero, new Vector2(2.6f, 2.8f), Color.white, 0f, sphere, 0.01f)); t.Add(false);
                s.Add(PS(root.transform, "Dust", mats.pPuff, 7, new Vector2(0.45f, 0.75f), new Vector2(0.8f, 2f), new Vector2(0.8f, 1.4f), new Color(1f, 0.98f, 0.94f, 0.9f), -0.05f, hemi, 0.4f, true, true)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Muzzle: tiny white flash (kept small; soldiers fire a lot).
            {
                var fx = VfxRoot(VfxType.Muzzle, 0.15f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Flash", mats.pSoft, 1, new Vector2(0.06f, 0.08f), Vector2.zero, new Vector2(0.3f, 0.35f), Color.white, 0f, sphere, 0.01f)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Bubble pop: ring of white droplets plus a flash.
            {
                var fx = VfxRoot(VfxType.BubblePop, 0.7f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Drops", mats.pDot, 18, new Vector2(0.3f, 0.5f), new Vector2(3f, 4.5f), new Vector2(0.1f, 0.18f), Color.white, 1.2f, sphere, 0.6f)); t.Add(false);
                s.Add(PS(root.transform, "Star", mats.pStar, 1, new Vector2(0.15f, 0.18f), Vector2.zero, new Vector2(2.4f, 2.6f), Color.white, 0f, sphere, 0.01f)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Capture: fountain of the captor's paint.
            {
                var fx = VfxRoot(VfxType.Capture, 1.4f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Fountain", mats.pDot, 40, new Vector2(0.7f, 1.1f), new Vector2(4f, 7f), new Vector2(0.15f, 0.3f), Color.white, 1.6f, ParticleSystemShapeType.Cone, 0.8f, true, false, 64)); t.Add(true);
                s.Add(PS(root.transform, "Star", mats.pStar, 1, new Vector2(0.25f, 0.3f), Vector2.zero, new Vector2(4f, 4.5f), Color.white, 0f, sphere, 0.01f)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
            // Reinforce: soft white puff where a backup soldier lands.
            {
                var fx = VfxRoot(VfxType.Reinforce, 0.6f, out var root);
                var s = new List<ParticleSystem>(); var t = new List<bool>();
                s.Add(PS(root.transform, "Cloud", mats.pPuff, 3, new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0.8f), new Vector2(0.5f, 0.8f), Color.white, 0f, sphere, 0.2f, true, true)); t.Add(false);
                Finish(fx, root, s, t, p);
            }
        }

        // ------------------------------------------------------------------ structures

        static GameObject StructureModel(string name, Transform root, Mats mats, Meshes meshes, float h, float w)
        {
            var asset = LoadModel(StructDir, name);
            GameObject model = asset != null ? InstantiateModel(asset, root, "Model") : StandIn(name, root, meshes, mats.slots["TeamColor"], mats.slots["Metal"], h, w);
            if (asset == null) model.name = "Model";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return model;
        }

        static TeamTint AddTint(GameObject root, Mats mats, GameObject model)
        {
            var tint = root.AddComponent<TeamTint>();
            tint.baseTeamColor = mats.slots["TeamColor"];
            tint.baseTeamColorDark = mats.slots["TeamColorDark"];
            tint.basePaint = mats.slots["Paint"];
            tint.renderers = model.GetComponentsInChildren<Renderer>(true);
            return tint;
        }

        static void FitBox(GameObject root, GameObject model, float shrink = 0.85f)
        {
            var b = RendererBounds(model);
            var box = root.AddComponent<BoxCollider>();
            Vector3 c = root.transform.InverseTransformPoint(b.center);
            box.center = new Vector3(c.x, b.size.y * 0.5f, c.z);
            box.size = new Vector3(b.size.x * shrink, b.size.y, b.size.z * shrink);
        }

        static HealthArc AddHealthArc(GameObject root, Mats mats, Meshes meshes, float size)
        {
            var q = Quad("HealthArc", root.transform, meshes.quad, mats.teams[TeamColorId.Teal].arc, size, 0.04f);
            var ha = q.AddComponent<HealthArc>();
            ha.arc = q.GetComponent<Renderer>();
            return ha;
        }

        static void BuildStructures(Prefabs p, Mats mats, Meshes meshes, Data data)
        {
            p.structures["Turret"] = BuildDefense<Turret>("Turret", mats, meshes, data.turret, 1.8f, 1.4f, (s, model) =>
            {
                s.head = FindDeep(model.transform, "Head");
                s.muzzle = FindDeep(model.transform, "Muzzle");
            });
            p.structures["Cannon"] = BuildDefense<Cannon>("Cannon", mats, meshes, data.cannon, 1.4f, 1.4f, (s, model) =>
            {
                s.barrel = FindDeep(model.transform, "Barrel");
                s.muzzle = FindDeep(model.transform, "Muzzle");
            });
            p.structures["Bunker"] = BuildDefense<Bunker>("Bunker", mats, meshes, data.bunker, 1.8f, 3f, (s, model) =>
            {
                s.muzzle = FindDeep(model.transform, "Muzzle");
            });
            p.structures["Tank"] = BuildDefense<Tank>("Tank", mats, meshes, data.tank, 1.4f, 2.2f, (s, model) =>
            {
                s.hull = FindDeep(model.transform, "Hull");
                s.turret = FindDeep(model.transform, "Turret");
                s.muzzle = FindDeep(model.transform, "Muzzle");
                var rb = s.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            });
            p.structures["Mine"] = BuildDefense<Mine>("Mine", mats, meshes, data.mine, 0.2f, 0.7f, (s, model) =>
            {
                s.cap = FindDeep(model.transform, "Paint") ?? FindDeep(model.transform, "Cap");
            }, collider: false, arc: false);
            p.structures["CapturePoint"] = BuildCapturePoint(mats, meshes);
        }

        static GameObject BuildDefense<T>(string name, Mats mats, Meshes meshes, StructureConfig cfg, float h, float w,
            System.Action<T, GameObject> wire, bool collider = true, bool arc = true) where T : StructureBase
        {
            var root = new GameObject(name);
            root.layer = name == "Mine" ? 0 : Layers.Structure;
            root.AddComponent<Health>();
            var s = root.AddComponent<T>();
            s.config = cfg;
            s.team = Team.Enemy;
            var model = StructureModel(name, root.transform, mats, meshes, h, w);
            if (name == "Mine") foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            s.modelRoot = model.transform;
            s.tint = AddTint(root, mats, model);
            s.bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
            if (collider) FitBox(root, model);
            if (arc)
            {
                var b = RendererBounds(model);
                s.healthArc = AddHealthArc(root, mats, meshes, Mathf.Max(b.size.x, b.size.z) * 1.35f + 0.4f);
            }
            wire?.Invoke(s, model);
            if (s.muzzle != null) Debug.Log($"[AssetBuilder] {name} muzzle in root space {root.transform.InverseTransformPoint(s.muzzle.position)} forward {root.transform.InverseTransformDirection(s.muzzle.forward)}");
            return SavePrefabGO(root, PrefabDir + "/Structures/" + name + ".prefab");
        }

        static GameObject BuildCapturePoint(Mats mats, Meshes meshes)
        {
            var root = new GameObject("CapturePoint");
            var cp = root.AddComponent<CapturePoint>();
            var model = StructureModel("CapturePoint", root.transform, mats, meshes, 3f, 4f);
            var flag = FindDeep(model.transform, "Flag");
            cp.flag = flag;
            // The defender owns the point at start (enemy color); capture repaints the whole model.
            var ft = AddTint(root, mats, model);
            ft.applyOnStart = true;
            ft.startTeam = Team.Enemy;
            cp.flagTint = ft;
            // Pole should not block movement much: a thin collider on the pole only.
            var pole = FindDeep(model.transform, "Pole");
            if (pole != null)
            {
                var c = root.AddComponent<CapsuleCollider>();
                c.radius = 0.18f; c.height = 3f; c.center = root.transform.InverseTransformPoint(pole.position) + Vector3.up * 1.5f;
                root.layer = Layers.Obstacle;
            }
            var ring = Quad("ProgressRing", root.transform, meshes.quad, mats.neutralArc, 6f, 0.05f);
            cp.progressRing = ring.GetComponent<Renderer>();
            return SavePrefabGO(root, PrefabDir + "/Structures/CapturePoint.prefab");
        }

        // ------------------------------------------------------------------ environment

        static readonly string[] NoCollider = { "Bush_A", "Bush_B", "Flower_A", "Flower_B", "GrassTuft", "HexTile", "SpawnHatch" };
        static readonly string[] SmallNoShadow = { "Flower_A", "Flower_B", "GrassTuft", "HexTile", "SpawnHatch" };
        static readonly string[] ExtraStructureProps = { "Tent", "Sandbags", "Wall", "WallCorner", "SpawnHatch", "HQ", "TeslaAntenna" };

        static void BuildEnvironment(Prefabs p, Mats mats, Meshes meshes)
        {
            var names = new List<(string dir, string name)>();
            if (Directory.Exists(EnvDir))
                foreach (var f in Directory.GetFiles(EnvDir, "*.fbx")) names.Add((EnvDir, Path.GetFileNameWithoutExtension(f)));
            foreach (var n in ExtraStructureProps)
                if (File.Exists(StructDir + "/" + n + ".fbx")) names.Add((StructDir, n));

            foreach (var (dir, name) in names)
            {
                var asset = LoadModel(dir, name);
                if (asset == null) continue;
                var root = new GameObject(name);
                var model = InstantiateModel(asset, root.transform, "Model");
                bool small = System.Array.IndexOf(SmallNoShadow, name) >= 0;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = small ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                if (System.Array.IndexOf(NoCollider, name) < 0)
                {
                    root.layer = Layers.Obstacle;
                    if (name.StartsWith("Tree"))
                    {
                        var b = RendererBounds(model);
                        var c = root.AddComponent<CapsuleCollider>();
                        c.radius = Mathf.Max(0.25f, Mathf.Min(b.size.x, b.size.z) * 0.3f);
                        c.height = b.size.y;
                        c.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    }
                    else FitBox(root, model, 0.9f);
                }
                // Props that carry team slots (hatches, HQ, antenna) belong to the defender.
                bool teamed = false;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m == mats.slots["TeamColor"] || m == mats.slots["TeamColorDark"] || m == mats.slots["Paint"]) teamed = true;
                if (teamed)
                {
                    var tt = AddTint(root, mats, model);
                    tt.applyOnStart = true;
                    tt.startTeam = Team.Enemy;
                }
                root.isStatic = false;
                p.environment[name] = SavePrefabGO(root, PrefabDir + "/Environment/" + name + ".prefab");
            }
        }
    }
}
