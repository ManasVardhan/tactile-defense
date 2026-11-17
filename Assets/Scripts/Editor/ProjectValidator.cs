using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Project health check (phase 8): missing script references, renderers with missing or broken materials (the
    /// magenta case), materials whose shader is missing, errored or unsupported, missing texture references inside
    /// materials, missing meshes, and particle renderers without a material. Scans every scene in Assets/Scenes, every
    /// prefab and every material under Assets (excluding TextMesh Pro samples). Writes Logs/validation.txt.
    /// Command line: -executeMethod TactileWars.EditorTools.ProjectValidator.Run
    /// </summary>
    public static class ProjectValidator
    {
        static readonly List<string> issues = new List<string>();
        static int scannedObjects, scannedPrefabs, scannedScenes, scannedMaterials;

        [MenuItem("TactileWars/Validate Project")]
        public static void Run()
        {
            issues.Clear();
            scannedObjects = scannedPrefabs = scannedScenes = scannedMaterials = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/TextMesh Pro/Examples")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                scannedMaterials++;
                CheckMaterial(m, path);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/TextMesh Pro/Examples")) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { scannedPrefabs++; CheckHierarchy(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                scannedScenes++;
                foreach (var go in scene.GetRootGameObjects()) CheckHierarchy(go, path);
            }
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);

            var sb = new StringBuilder();
            sb.AppendLine("TactileWars project validation " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine($"Scanned: {scannedScenes} scenes, {scannedPrefabs} prefabs, {scannedObjects} game objects, {scannedMaterials} materials");
            sb.AppendLine($"Issues: {issues.Count}");
            foreach (var i in issues) sb.AppendLine(" - " + i);
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/validation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[Validator]\n" + sb);
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static void CheckHierarchy(GameObject root, string asset)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                scannedObjects++;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0) issues.Add($"{asset}: {PathOf(t)} has {missing} missing script reference(s)");

                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                            issues.Add($"{asset}: {PathOf(t)} {c.GetType().Name}.{it.propertyPath} references a missing object");
                    }
                }

                var r = go.GetComponent<Renderer>();
                if (r != null && !(r is ParticleSystemRenderer psr0 && !psr0.enabled))
                {
                    var mats = r.sharedMaterials;
                    bool isParticle = r is ParticleSystemRenderer;
                    bool isLine = r is LineRenderer || r is TrailRenderer;
                    if (mats.Length == 0 && !isLine) issues.Add($"{asset}: {PathOf(t)} {r.GetType().Name} has no materials");
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null)
                        {
                            // Line renderers whose material is assigned at runtime (team strokes) are allowed to be empty.
                            if (!isLine) issues.Add($"{asset}: {PathOf(t)} {r.GetType().Name} material slot {i} is empty (renders magenta)");
                            continue;
                        }
                        if (!isParticle) CheckShader(mats[i], $"{asset}: {PathOf(t)}");
                    }
                }
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null && go.GetComponent<TMPro.TMP_Text>() == null)
                    issues.Add($"{asset}: {PathOf(t)} MeshFilter has no mesh");
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh == null) issues.Add($"{asset}: {PathOf(t)} SkinnedMeshRenderer has no mesh");
                var img = go.GetComponent<UnityEngine.UI.Graphic>();
                if (img != null && img.material != null && img.material.shader != null && ShaderUtil.ShaderHasError(img.material.shader))
                    issues.Add($"{asset}: {PathOf(t)} UI graphic shader has errors");
            }
        }

        static void CheckShader(Material m, string where)
        {
            var sh = m.shader;
            if (sh == null) { issues.Add($"{where}: material {m.name} has no shader"); return; }
            if (sh.name == "Hidden/InternalErrorShader") { issues.Add($"{where}: material {m.name} uses the error shader (magenta)"); return; }
            if (ShaderUtil.ShaderHasError(sh)) issues.Add($"{where}: material {m.name} shader {sh.name} has compile errors");
            else if (!sh.isSupported) issues.Add($"{where}: material {m.name} shader {sh.name} is not supported on this platform");
        }

        static void CheckMaterial(Material m, string path)
        {
            if (m == null) { issues.Add($"{path}: could not load material"); return; }
            CheckShader(m, path);
            // Texture slots that point at an asset that no longer exists.
            var so = new SerializedObject(m);
            var envs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (envs == null) return;
            for (int i = 0; i < envs.arraySize; i++)
            {
                var e = envs.GetArrayElementAtIndex(i);
                var tex = e.FindPropertyRelative("second.m_Texture");
                if (tex == null) continue;
                if (tex.objectReferenceValue == null && tex.objectReferenceInstanceIDValue != 0)
                    issues.Add($"{path}: texture slot {e.FindPropertyRelative("first").stringValue} references a missing texture");
            }
        }
    }
}
