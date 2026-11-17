using System.Collections.Generic;
using System.IO;
using TactileWars.Core;
using TactileWars.Structures;
using TactileWars.UI;
using TactileWars.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TactileWars.EditorTools
{
    /// <summary>Renders the project's own prefabs (Soldier, Hero, defenses, HQ) into transparent sprites for the UI,
    /// in every team color. Each shot is rendered twice (black and white background) to recover exact alpha
    /// regardless of shader alpha output, at 2x resolution then box filtered down.</summary>
    public static class UIArtRenderer
    {
        public const string RenderDir = UIArtBuilder.Dir + "/Renders";
        static readonly TeamColorId[] AllTeams = { TeamColorId.Blue, TeamColorId.Green, TeamColorId.Orange, TeamColorId.Red, TeamColorId.Pink, TeamColorId.Purple, TeamColorId.Teal };

        static Camera cam;
        static RenderTexture rt;
        static TeamVisuals visuals;

        public class Output
        {
            public Sprite[] busts, soldiers, heroes;
            public NamedSprite[] named;
        }

        public static Output RenderAll()
        {
            Directory.CreateDirectory(RenderDir);
            visuals = AssetDatabase.LoadAssetAtPath<TeamVisuals>("Assets/Data/TeamVisuals.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, -0.8f, -0.5f).normalized);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            var lighting = new GameObject("Lighting").AddComponent<SceneLighting>();
            lighting.intensity = 1.05f;
            lighting.Apply();

            var camGo = new GameObject("RenderCam");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.enabled = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;

            var soldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Soldier.prefab");
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Hero.prefab");
            int nTeams = 7;
            var busts = new Sprite[nTeams];
            var soldiers = new Sprite[nTeams];
            var heroes = new Sprite[nTeams];
            var renders = new List<NamedSprite>();

            foreach (var team in AllTeams)
            {
                int k = (int)team;
                if (soldierPrefab != null)
                {
                    busts[k] = Shoot(soldierPrefab, team, "bust_" + team, 256, 256, Shot.Bust, 12f, 205f);
                    soldiers[k] = Shoot(soldierPrefab, team, "soldier_" + team, 256, 296, Shot.Full, 14f, 212f);
                }
                if (heroPrefab != null) heroes[k] = Shoot(heroPrefab, team, "hero_" + team, 256, 256, Shot.Bust, 12f, 205f);
                foreach (var def in new[] { "Turret", "Tank", "Cannon", "Mine", "Bunker", "Tesla", "Sniper", "Geyser" })
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/" + def + ".prefab");
                    if (p != null) renders.Add(new NamedSprite { key = "def_" + def.ToLowerInvariant() + "_" + team, sprite = Shoot(p, team, "def_" + def.ToLowerInvariant() + "_" + team, 256, 256, Shot.Prop, 32f, 215f) });
                }
                var hq = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/HQ.prefab");
                if (hq != null) renders.Add(new NamedSprite { key = "hq_" + team, sprite = Shoot(hq, team, "hq_" + team, 512, 512, Shot.Prop, 30f, 215f) });
            }
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); rt = null; }
            AssetDatabase.SaveAssets();
            return new Output { busts = busts, soldiers = soldiers, heroes = heroes, named = renders.ToArray() };
        }

        enum Shot { Bust, Full, Prop }

        static Sprite Shoot(GameObject prefab, TeamColorId team, string name, int w, int h, Shot shot, float pitch, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            Prepare(go, team, shot);

            var bounds = Bounds(go);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.rotation = rot;
            Vector3 center = bounds.center;
            float ortho;
            float aspect = w / (float)h;
            if (shot == Shot.Bust)
            {
                // Head and shoulders: the top 62 percent of the model.
                float top = bounds.max.y, bottom = bounds.min.y + bounds.size.y * 0.36f;
                center = new Vector3(bounds.center.x, (top + bottom) * 0.5f - bounds.size.y * 0.01f, bounds.center.z);
                float halfW = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.92f;
                ortho = Mathf.Max((top - bottom) * 0.5f, halfW / aspect) * 1.1f;
            }
            else
            {
                Vector3 ext = bounds.extents;
                float r = Mathf.Max(ext.x, ext.z);
                float vh = ext.y * Mathf.Cos(pitch * Mathf.Deg2Rad) + r * Mathf.Sin(pitch * Mathf.Deg2Rad);
                ortho = Mathf.Max(vh, r / aspect) * (shot == Shot.Full ? 1.06f : 1.1f);
            }
            cam.orthographicSize = ortho;
            cam.transform.position = center - rot * Vector3.forward * 20f;

            int sw = w * 2, sh = h * 2;
            var black = Render(sw, sh, Color.black);
            var white = Render(sw, sh, Color.white);
            Object.DestroyImmediate(go);

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int i = (y * 2 + dy) * sw + (x * 2 + dx);
                            Color b = black[i], wt = white[i];
                            float a = 1f - ((wt.r - b.r) + (wt.g - b.g) + (wt.b - b.b)) / 3f;
                            a = Mathf.Clamp01(a);
                            Color c = a > 0.003f ? new Color(b.r / a, b.g / a, b.b / a, 1f) : Color.clear;
                            acc += new Color(c.r * a, c.g * a, c.b * a, a);
                        }
                    acc /= 4f;
                    px[y * w + x] = acc.a > 0.003f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : Color.clear;
                }
            return UIArtBuilder.Save(RenderDir + "/" + name + ".png", w, h, px, Vector4.zero);
        }

        static void Prepare(GameObject go, TeamColorId team, Shot shot)
        {
            var m = visuals != null ? visuals.Get(team) : null;
            foreach (var t in go.GetComponentsInChildren<TeamTint>(true)) if (m != null) t.Apply(m);
            foreach (var a in go.GetComponentsInChildren<HealthArc>(true)) a.gameObject.SetActive(false);
            var soldier = go.GetComponent<Soldier>();
            if (soldier != null && soldier.blobShadow != null) soldier.blobShadow.gameObject.SetActive(shot == Shot.Full);
            // Hide flat decals (blob shadows, rings) on props so they do not enlarge the bounds.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                if (shot != Shot.Full && r.bounds.size.y < 0.02f) r.enabled = false;
            }
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name.EndsWith("Idle"))
                    {
                        clip.SampleAnimation(anim.gameObject, 0.4f);
                        break;
                    }
                }
            }
        }

        static Bounds Bounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) continue;
                if (r.bounds.size.y < 0.02f) continue; // flat decals
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (go.GetComponentInChildren<SkinnedMeshRenderer>() is SkinnedMeshRenderer smr)
            {
                // Skinned bounds can be loose: use the shared mesh bounds transformed instead.
                smr.updateWhenOffscreen = true;
                var mb = smr.sharedMesh != null ? smr.sharedMesh.bounds : smr.localBounds;
                var c = smr.transform.TransformPoint(mb.center);
                var e = Vector3.Scale(mb.extents, smr.transform.lossyScale);
                var sb = new Bounds(c, e * 2f);
                if (!any) b = sb; else { b = sb; foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) if (r.enabled && r.bounds.size.y > 0.02f) b.Encapsulate(r.bounds); }
            }
            return b;
        }

        static Color[] Render(int w, int h, Color bg)
        {
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                rt.Create();
            }
            cam.backgroundColor = bg;
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }
    }
}
