using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Static display copies of defense prefabs for the base editor: gameplay scripts, colliders and health
    /// arcs are stripped before the copy ever wakes, then the team colors are applied. "squad" shows a small cluster
    /// of soldier models around a flag bearer.</summary>
    public static class DefenseDisplay
    {
        static Transform holder;

        static Transform Holder()
        {
            if (holder == null)
            {
                var go = new GameObject("DisplayHolder");
                go.SetActive(false);
                holder = go.transform;
            }
            return holder;
        }

        public static GameObject Make(string id, DefenseCatalog catalog, BattlePrefabs prefabs, TeamVisuals visuals, TeamColorId team, Transform parent)
        {
            var root = new GameObject("Display_" + id);
            root.transform.SetParent(parent, false);
            if (id == DefenseCatalog.SquadId)
            {
                if (prefabs != null && prefabs.soldier != null)
                {
                    const int n = 7;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2f + 0.3f;
                        var s = Strip(prefabs.soldier.gameObject, root.transform, new Vector3(Mathf.Cos(a) * 1.5f, 0f, Mathf.Sin(a) * 1.3f), 180f + (i * 23 % 50) - 25f);
                        Tint(s, visuals, team);
                    }
                    var bearer = prefabs.flagBearer != null ? prefabs.flagBearer.gameObject : prefabs.soldier.gameObject;
                    var b = Strip(bearer, root.transform, Vector3.zero, 180f);
                    Tint(b, visuals, team);
                }
                return root;
            }
            var prefab = catalog != null ? catalog.Get(id) : null;
            if (prefab == null) return root;
            var copy = Strip(prefab, root.transform, Vector3.zero, 0f);
            Tint(copy, visuals, team);
            // Show the geyser's paint pool in the editor (it is hidden in battle until it wakes).
            var g = FindDeep(copy.transform, "Vent");
            if (g != null) g.gameObject.SetActive(true);
            return root;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static GameObject Strip(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
        {
            var go = Object.Instantiate(prefab, Holder());   // inactive: no Awake or Start runs
            foreach (var c in go.GetComponentsInChildren<StructureBase>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Soldier>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<PlacedDefense>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Health>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<HealthArc>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<LineRenderer>(true)) c.enabled = false;
            foreach (var c in go.GetComponentsInChildren<TeamTint>(true)) c.applyOnStart = false;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static void Tint(GameObject go, TeamVisuals visuals, TeamColorId team)
        {
            var m = visuals != null ? visuals.Get(team) : null;
            if (m == null) return;
            foreach (var t in go.GetComponentsInChildren<TeamTint>(true)) t.Apply(m);
        }

        /// <summary>Replaces every material with 'mat' (ghosts and wrecks).</summary>
        public static void SetAllMaterials(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
