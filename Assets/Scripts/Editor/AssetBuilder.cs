using System.Collections.Generic;
using System.IO;
using System.Linq;
using TactileWars.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Builds every generated asset: textures, meshes, materials, FBX import settings and material remaps,
    /// animator controllers, prefabs, VFX, ScriptableObjects and the sound library. Idempotent. Missing FBX
    /// files are replaced by clearly named STANDIN_ objects and reported.
    /// </summary>
    public static partial class AssetBuilder
    {
        public const string MatDir = "Assets/Art/Materials";
        public const string AnimDir = "Assets/Art/Animations";
        public const string CharDir = "Assets/Art/Models/Characters";
        public const string StructDir = "Assets/Art/Models/Structures";
        public const string EnvDir = "Assets/Art/Models/Environment";
        public const string DataDir = "Assets/Data";
        public const string PrefabDir = "Assets/Prefabs";

        public static readonly List<string> StandIns = new List<string>();

        // Palette (sRGB). Red, Teal, Pink, Green sampled from references; the rest are estimates.
        public static readonly (TeamColorId id, string primary, string dark, string paint)[] TeamHex =
        {
            (TeamColorId.Red, "#DA4029", "#A3261B", "#EA4A2E"),
            (TeamColorId.Teal, "#229A8A", "#167064", "#25B5A1"),
            (TeamColorId.Blue, "#3B7FD8", "#2556A0", "#3F8EF2"),
            (TeamColorId.Green, "#8DB52B", "#5E7D1A", "#9CCB2E"),
            (TeamColorId.Orange, "#F0A020", "#B8700F", "#FFA300"),
            (TeamColorId.Pink, "#DB4793", "#A52D6B", "#EE4FA0"),
            (TeamColorId.Purple, "#9B6FB0", "#6C4A84", "#A97BC6"),
        };

        static readonly Dictionary<string, string> SlotHex = new Dictionary<string, string>
        {
            { "TeamColor", "#3B7FD8" }, { "TeamColorDark", "#2556A0" }, { "Paint", "#3F8EF2" },
            { "Skin", "#F7F2EA" }, { "Dark", "#2C2F3C" }, { "Metal", "#A9AEB6" }, { "Accent", "#F4EFE2" },
            { "Wood", "#A3743F" }, { "Leaf", "#1E5249" }, { "LeafLight", "#2F6E60" }, { "Trunk", "#6E4C33" },
            { "Rock", "#B2A386" }, { "Sand", "#AE9A6C" }, { "Glass", "#9FE3F5" }, { "Canvas", "#E8DDBD" },
            { "Stone", "#CFC09C" },
        };

        [MenuItem("TactileWars/Build Assets")]
        public static void BuildAll() => Build(false);

        [MenuItem("TactileWars/Build Assets (reset tunables)")]
        public static void BuildAllResetData() => Build(true);

        static void Build(bool resetData)
        {
            StandIns.Clear();
            foreach (var d in new[] { MatDir, AnimDir, DataDir, PrefabDir + "/Units", PrefabDir + "/Structures", PrefabDir + "/Environment", PrefabDir + "/VFX", "Assets/Art/VFX" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            var tex = BuildTextures();
            var meshes = BuildMeshes();
            var mats = BuildMaterials(tex);
            ImportAudio();
            ImportModels(mats);
            var controllers = BuildControllers();
            var data = BuildData(resetData, mats);
            var prefabs = BuildPrefabs(mats, meshes, controllers, data);
            WireData(data, prefabs, mats, meshes);
            BuildSoundLibrary(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (StandIns.Count == 0) Debug.Log("[AssetBuilder] Done. No stand-ins needed.");
            else Debug.LogWarning("[AssetBuilder] Done. STAND-INS IN USE: " + string.Join(", ", StandIns));
        }

        // ------------------------------------------------------------------ textures and meshes

        public class Tex { public Texture2D splat, ring, blot, dash, glow, dot, soft, puff, ghost, star, beam, grid, white, coin; }

        static Tex BuildTextures()
        {
            return new Tex
            {
                splat = TextureGen.SplatAtlas(),
                ring = TextureGen.Ring(),
                blot = TextureGen.SpiralBlot(),
                dash = TextureGen.Dash(),
                glow = TextureGen.Glow(),
                dot = TextureGen.Dot(),
                soft = TextureGen.SoftCircle(),
                puff = TextureGen.Puff(),
                ghost = TextureGen.Ghost(),
                star = TextureGen.Star(),
                coin = TextureGen.Coin(),
                beam = TextureGen.Write("T_Beam", 16, 64, (u, v) => { float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f); a = a * a * (3f - 2f * a); return new Color(1f, 1f, 1f, a); }),
                grid = TextureGen.Write("T_EditorGrid", 64, 64, (u, v) => { float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); float a = e < 0.03f ? 0.9f : (e < 0.06f ? 0.35f : 0f); return new Color(1f, 1f, 1f, a); }, false, TextureWrapMode.Repeat),
                white = TextureGen.Write("T_White", 8, 8, (u, v) => Color.white),
            };
        }

        public class Meshes { public Mesh quad; public Mesh[] splatQuads; public Mesh sphere; }

        static Meshes BuildMeshes()
        {
            var m = new Meshes
            {
                quad = MeshGen.Quad("M_Quad", new Rect(0, 0, 1, 1)),
                sphere = MeshGen.Sphere("M_Sphere"),
                splatQuads = new Mesh[4],
            };
            for (int i = 0; i < 4; i++)
                m.splatQuads[i] = MeshGen.Quad("M_SplatQuad_" + i, new Rect((i % 2) * 0.5f, (i / 2) * 0.5f, 0.5f, 0.5f));
            return m;
        }

        // ------------------------------------------------------------------ materials

        public class Mats
        {
            public Dictionary<string, Material> slots = new Dictionary<string, Material>();      // props and structures
            public Dictionary<string, Material> unitSlots = new Dictionary<string, Material>();  // outlined, for characters
            public Dictionary<TeamColorId, TeamMaterials> teams = new Dictionary<TeamColorId, TeamMaterials>();
            public Material flash, blobShadow, ground, bubble, neutralArc;
            public Material pDot, pPuff, pGhost, pStar, pSoft, pCoin;
            public Material zap, beam, ghostEdit, gridEdit, discEdit;
        }

        static Shader Find(string n)
        {
            var s = Shader.Find(n);
            if (s == null) Debug.LogError("[AssetBuilder] Shader not found: " + n);
            return s;
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            setup?.Invoke(m);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Unit outlines (phase 8 readability pass): the team colored helmet and body keep a white sticker edge like the
        // reference (f_0022, ps_12); face, gun, boots and hands get a thin dark edge so the white face pops off the ground.
        static readonly Color UnitDarkOutline = new Color(0.16f, 0.14f, 0.19f, 1f);

        static void ToonSetup(Material m, Color c, bool outline, float emission = 0f, float rim = 0.22f, bool darkOutline = false)
        {
            m.SetColor("_BaseColor", c);
            m.SetColor("_ShadeColor", new Color(0.7f, 0.68f, 0.8f));
            m.SetFloat("_ShadeThreshold", -0.05f);
            m.SetFloat("_ShadeSoftness", 0.3f);
            m.SetColor("_RimColor", Color.white);
            m.SetFloat("_RimStrength", rim);
            m.SetFloat("_RimPower", 3f);
            m.SetColor("_OutlineColor", darkOutline ? UnitDarkOutline : Color.white);
            m.SetFloat("_OutlineWidth", outline ? (darkOutline ? 1.0f : 1.35f) : 0f);
            m.SetFloat("_Emission", emission);
            m.SetFloat("_ShadowStrength", 0.6f);
            m.SetFloat("_Ambient", 0.5f);
        }

        static void DecalSetup(Material m, Texture2D tex, Color baseC, Color core, int queue, float shade = 0.35f, float emission = 0f)
        {
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_CoreColor", core);
            m.SetFloat("_ShadeAmount", shade);
            m.SetFloat("_Emission", emission);
            m.renderQueue = queue;
        }

        static Mats BuildMaterials(Tex t)
        {
            var toon = Find("TW/Toon");
            var toonO = Find("TW/ToonOutline");
            var decal = Find("TW/Decal");
            var arc = Find("TW/RingArc");
            var particle = Find("TW/Particle");
            var mats = new Mats();

            foreach (var kv in SlotHex)
            {
                var c = Hex(kv.Value);
                float em = kv.Key == "Glass" ? 0.35f : kv.Key == "Paint" ? 0.25f : 0f;
                mats.slots[kv.Key] = Mat("M_" + kv.Key, toon, m => ToonSetup(m, c, false, em, kv.Key == "Leaf" || kv.Key == "LeafLight" ? 0.12f : 0.18f));
                bool darkEdge = kv.Key != "TeamColor" && kv.Key != "TeamColorDark" && kv.Key != "Accent";
                mats.unitSlots[kv.Key] = Mat("M_U_" + kv.Key, toonO, m => ToonSetup(m, c, true, em, 0.28f, darkEdge));
            }

            foreach (var th in TeamHex)
            {
                string n = th.id.ToString();
                Color p = Hex(th.primary), d = Hex(th.dark), pt = Hex(th.paint);
                var tm = new TeamMaterials { id = th.id };
                tm.teamColor = Mat($"M_Team_{n}_Color", toon, m => ToonSetup(m, p, false));
                tm.teamColorDark = Mat($"M_Team_{n}_Dark", toon, m => ToonSetup(m, d, false));
                tm.paint = Mat($"M_Team_{n}_Paint", toon, m => ToonSetup(m, pt, false, 0.35f));
                tm.unitTeamColor = Mat($"M_Team_{n}_UColor", toonO, m => ToonSetup(m, p, true, 0f, 0.3f));
                tm.unitTeamColorDark = Mat($"M_Team_{n}_UDark", toonO, m => ToonSetup(m, d, true, 0f, 0.3f));
                tm.unitPaint = Mat($"M_Team_{n}_UPaint", toonO, m => ToonSetup(m, pt, true, 0.35f, 0.3f, true));
                tm.splat = Mat($"M_Team_{n}_Splat", decal, m => DecalSetup(m, t.splat, pt, pt, 3000, 0.6f));
                tm.ring = Mat($"M_Team_{n}_Ring", decal, m => DecalSetup(m, t.ring, p, p, 3002, 0.3f));
                tm.blot = Mat($"M_Team_{n}_Blot", decal, m => DecalSetup(m, t.blot, pt, pt, 3002, 1.0f));
                tm.projectile = Mat($"M_Team_{n}_Dot", decal, m => { DecalSetup(m, t.dot, pt, Color.white, 3010, 0f, 0.9f); m.SetFloat("_ZTest", 8f); });
                tm.stroke = Mat($"M_Team_{n}_Stroke", decal, m => DecalSetup(m, t.dash, p, p, 3004, 0f, 0.1f));
                tm.strokeGlow = Mat($"M_Team_{n}_StrokeGlow", decal, m => DecalSetup(m, t.glow, new Color(pt.r, pt.g, pt.b, 0.55f), pt, 3003, 0f, 0.35f));
                tm.arc = Mat($"M_Team_{n}_Arc", arc, m =>
                {
                    m.SetColor("_BaseColor", p);
                    m.SetColor("_BackColor", new Color(1f, 1f, 1f, 0.55f));
                    m.SetFloat("_Inner", 0.84f); m.SetFloat("_Outer", 0.97f);
                    m.SetFloat("_Fill", 1f); m.SetFloat("_Alpha", 1f); m.SetFloat("_Dashes", 30f);
                    m.renderQueue = 3002;
                });
                mats.teams[th.id] = tm;
            }

            mats.flash = Mat("M_Flash", toonO, m => { ToonSetup(m, Color.white, true, 1f); });
            mats.blobShadow = Mat("M_BlobShadow", decal, m => DecalSetup(m, t.soft, new Color(0.16f, 0.12f, 0.08f, 0.42f), new Color(0.16f, 0.12f, 0.08f, 1f), 3001, 0f));
            mats.neutralArc = Mat("M_NeutralArc", arc, m =>
            {
                m.SetColor("_BaseColor", Color.white); m.SetColor("_BackColor", new Color(1, 1, 1, 0.5f));
                m.SetFloat("_Inner", 0.84f); m.SetFloat("_Outer", 0.97f); m.renderQueue = 3002;
            });
            mats.bubble = Mat("M_Bubble", Find("TW/Bubble"), m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
                m.SetFloat("_CenterAlpha", 0.1f); m.SetFloat("_RimAlpha", 0.95f); m.SetFloat("_RimPower", 2.2f); m.SetFloat("_Highlight", 0.9f);
                m.SetColor("_BackColor", new Color(0.93f, 0.93f, 0.9f, 0.62f));
                m.SetColor("_BackEdgeColor", new Color(0.66f, 0.66f, 0.64f, 0.85f));
            });
            mats.ground = Mat("M_Ground", Find("TW/Ground"), m =>
            {
                m.SetColor("_SandColor", Hex("#C3B48E"));
                m.SetColor("_SandColor2", Hex("#B9AC88"));
                m.SetColor("_HexLineColor", Hex("#958666"));
                m.SetColor("_PathColor", Hex("#BBAB84"));
                m.SetColor("_TileColor", Hex("#D8CAA6"));
                m.SetColor("_DarkColor", Hex("#A9A17F"));
                m.SetColor("_ShadowColor", Hex("#9C9AA6"));
                m.SetFloat("_HexSize", 5.2f);
                m.SetFloat("_HexLineWidth", 0.028f);
                m.SetFloat("_HexLineStrength", 0.62f);
                m.SetFloat("_HexTint", 0.045f);
                m.SetFloat("_PathStrength", 0.55f);
                m.SetFloat("_NoiseScale", 0.08f);
                m.SetFloat("_ShadowStrength", 0.8f);
            });
            mats.pDot = Mat("M_P_Droplet", particle, m => { m.SetTexture("_MainTex", t.dot); m.SetFloat("_Emission", 0.15f); m.renderQueue = 3010; });
            mats.pPuff = Mat("M_P_Puff", particle, m => { m.SetTexture("_MainTex", t.puff); m.renderQueue = 3011; });
            mats.pGhost = Mat("M_P_Ghost", particle, m => { m.SetTexture("_MainTex", t.ghost); m.renderQueue = 3012; });
            mats.pStar = Mat("M_P_Star", particle, m => { m.SetTexture("_MainTex", t.star); m.SetFloat("_Emission", 0.8f); m.renderQueue = 3013; });
            mats.pSoft = Mat("M_P_Soft", particle, m => { m.SetTexture("_MainTex", t.soft); m.renderQueue = 3011; });
            mats.pCoin = Mat("M_P_Coin", particle, m => { m.SetTexture("_MainTex", t.coin); m.SetFloat("_Emission", 0.2f); m.renderQueue = 3013; });
            mats.zap = Mat("M_Zap", particle, m => { m.SetTexture("_MainTex", t.beam); m.SetFloat("_Emission", 1.2f); m.renderQueue = 3014; });
            mats.beam = Mat("M_AimBeam", particle, m => { m.SetTexture("_MainTex", t.beam); m.SetFloat("_Emission", 0.4f); m.renderQueue = 3014; });
            mats.ghostEdit = Mat("M_EditorGhost", particle, m => { m.SetTexture("_MainTex", t.white); m.renderQueue = 3015; });
            mats.gridEdit = Mat("M_EditorGrid", particle, m => { m.SetTexture("_MainTex", t.grid); m.renderQueue = 3001; });
            mats.discEdit = Mat("M_EditorDisc", particle, m => { m.SetTexture("_MainTex", t.soft); m.renderQueue = 3002; });
            return mats;
        }

        // ------------------------------------------------------------------ audio import

        static void ImportAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null) continue;
                bool music = path.Contains("/Music/");
                var s = ai.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.7f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = !music;
                ai.loadInBackground = music;
                ai.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ models

        static string Normalize(string n)
        {
            n = n.Trim();
            int dot = n.IndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);
            return n;
        }

        static bool IsCharacter(string path) => path.StartsWith(CharDir) && !Path.GetFileNameWithoutExtension(path).StartsWith("Flag");

        static void ImportModels(Mats mats)
        {
            var fbxs = new List<string>();
            foreach (var dir in new[] { CharDir, StructDir, EnvDir })
                if (Directory.Exists(dir)) fbxs.AddRange(Directory.GetFiles(dir, "*.fbx").Select(p => p.Replace('\\', '/')));
            foreach (var path in fbxs)
            {
                AssetDatabase.ImportAsset(path);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                bool character = IsCharacter(path);
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importVisibility = false;
                mi.importBlendShapes = false;
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                if (character)
                {
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.importAnimation = true;
                    mi.optimizeGameObjects = false;
                    var defs = mi.defaultClipAnimations;
                    for (int i = 0; i < defs.Length; i++)
                    {
                        string clean = CleanClip(defs[i].name);
                        defs[i].name = clean;
                        defs[i].loopTime = clean == "Idle" || clean == "Run" || clean == "Cheer";
                        defs[i].loopPose = false;
                        defs[i].keepOriginalPositionY = true;
                        defs[i].keepOriginalPositionXZ = true;
                        defs[i].keepOriginalOrientation = true;
                    }
                    mi.clipAnimations = defs;
                }
                else
                {
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.importAnimation = false;
                }

                // Material remap by slot name. Source names come from the existing map and the embedded materials.
                var sources = new HashSet<string>();
                foreach (var kv in mi.GetExternalObjectMap())
                    if (kv.Key.type == typeof(Material)) sources.Add(kv.Key.name);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && AssetDatabase.GetAssetPath(m) == path) sources.Add(m.name);
                var table = character ? mats.unitSlots : mats.slots;
                foreach (var src in sources)
                {
                    string key = Normalize(src);
                    if (table.TryGetValue(key, out var target))
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src), target);
                    else
                        Debug.LogWarning($"[AssetBuilder] {Path.GetFileName(path)}: unknown material slot '{src}', left embedded");
                }
                mi.SaveAndReimport();
            }
        }

        static string CleanClip(string n)
        {
            int bar = n.LastIndexOf('|');
            if (bar >= 0) n = n.Substring(bar + 1);
            return n.Trim();
        }

        // ------------------------------------------------------------------ animator controllers

        static Dictionary<string, AnimatorController> BuildControllers()
        {
            var result = new Dictionary<string, AnimatorController>();
            foreach (var name in new[] { "Soldier", "SoldierHeavy", "Hero" })
            {
                string path = CharDir + "/" + name + ".fbx";
                if (!File.Exists(path)) continue;
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                var byName = new Dictionary<string, AnimationClip>();
                foreach (var c in clips) byName[CleanClip(c.name)] = c;
                if (!byName.ContainsKey("Idle") || !byName.ContainsKey("Run"))
                {
                    Debug.LogWarning($"[AssetBuilder] {name}.fbx has no Idle/Run takes (found: {string.Join(",", byName.Keys)}); no controller");
                    continue;
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                result[name] = BuildController(name, byName, model);
            }
            return result;
        }

        static AnimatorController BuildController(string name, Dictionary<string, AnimationClip> clips, GameObject model)
        {
            string path = AnimDir + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Cheer", AnimatorControllerParameterType.Bool);

            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Run"], 0.35f);
            sm.defaultState = loco;

            if (clips.TryGetValue("Cheer", out var cheerClip))
            {
                var cheer = sm.AddState("Cheer");
                cheer.motion = cheerClip;
                var t1 = loco.AddTransition(cheer); t1.AddCondition(AnimatorConditionMode.If, 0, "Cheer"); t1.hasExitTime = false; t1.duration = 0.15f;
                var t2 = cheer.AddTransition(loco); t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Cheer"); t2.hasExitTime = false; t2.duration = 0.15f;
            }
            if (clips.TryGetValue("Die", out var dieClip))
            {
                var die = sm.AddState("Die");
                die.motion = dieClip;
                var td = sm.AddAnyStateTransition(die);
                td.AddCondition(AnimatorConditionMode.If, 0, "Die");
                td.hasExitTime = false; td.duration = 0.05f; td.canTransitionToSelf = false;
            }

            // Upper body action layer (Shoot, Hit) masked to the spine chain so legs keep running.
            var mask = BuildUpperMask(name, model);
            var layer = new AnimatorControllerLayer
            {
                name = "Actions",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine { name = "Actions", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ac);
            var empty = layer.stateMachine.AddState("Empty");
            layer.stateMachine.defaultState = empty;
            if (clips.TryGetValue("Shoot", out var shootClip))
            {
                var shoot = layer.stateMachine.AddState("Shoot");
                shoot.motion = shootClip;
                var a = empty.AddTransition(shoot); a.AddCondition(AnimatorConditionMode.If, 0, "Shoot"); a.hasExitTime = false; a.duration = 0.03f;
                var a2 = shoot.AddTransition(shoot); a2.AddCondition(AnimatorConditionMode.If, 0, "Shoot"); a2.hasExitTime = false; a2.duration = 0.02f;
                var b = shoot.AddTransition(empty); b.hasExitTime = true; b.exitTime = 0.95f; b.duration = 0.06f;
            }
            if (clips.TryGetValue("Hit", out var hitClip))
            {
                var hit = layer.stateMachine.AddState("Hit");
                hit.motion = hitClip;
                var a = empty.AddTransition(hit); a.AddCondition(AnimatorConditionMode.If, 0, "Hit"); a.hasExitTime = false; a.duration = 0.02f;
                var b = hit.AddTransition(empty); b.hasExitTime = true; b.exitTime = 0.95f; b.duration = 0.05f;
            }
            ac.AddLayer(layer);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static AvatarMask BuildUpperMask(string name, GameObject model)
        {
            string path = AnimDir + "/" + name + "_Upper.mask";
            AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask();
            if (model != null)
            {
                var all = model.GetComponentsInChildren<Transform>(true);
                mask.transformCount = all.Length;
                for (int i = 0; i < all.Length; i++)
                {
                    string p = AnimationUtility.CalculateTransformPath(all[i], model.transform);
                    mask.SetTransformPath(i, p);
                    bool upper = p.Contains("Spine") && !p.EndsWith("Hips");
                    mask.SetTransformActive(i, upper);
                }
            }
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        // ------------------------------------------------------------------ helpers

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static GameObject LoadModel(string dir, string name)
        {
            string path = dir + "/" + name + ".fbx";
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }
    }
}
