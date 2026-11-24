using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace TactileWars.Army
{
    /// <summary>
    /// Draws the round ground markers for the hidden deployment grid. The grid itself is never drawn: the player sees
    /// an open field and learns that positions are discrete from the hover marker snapping from spot to spot.
    /// - Hover (while an entry is armed or a unit is selected): a soft white-gold ring about one unit wide at the
    ///   snapped spot when a click there would be obeyed, a red ring with a cross when it would be refused (blocked,
    ///   taken, an enemy standing there, off the field). Nothing over a friendly unit (a click selects it).
    /// - Suggested spots (set by the tutorial): pulsing gold discs with a bright rim, shown whether or not armed.
    /// - Reject flash: the red crossed ring at the refused spot for flashSeconds, with the Reject sound.
    /// - Capture: while armed or selected, a gold glow over the active capture circle's own ring ("stand here").
    /// Presentation only: it reads the army and the input, it never decides anything.
    /// </summary>
    public class HexOverlay : MonoBehaviour
    {
        /// <summary>What a spot is under the rules (not how it is drawn): Blocked (no unit can stand there), Pad (a
        /// spot of the active capture point, PlayerArmy.IsPadHex), Normal for the rest, occupied spots included.</summary>
        public enum HexStyle { Normal, Blocked, Pad }

        /// <summary>The hover marker as drawn.</summary>
        public enum MarkerKind { None, Valid, Invalid }

        public PlayerArmy army;
        public ArmyInput input;
        [Tooltip("Transparent unlit material tinted by vertex color and _BaseColor (TW/Particle).")] public Material material;
        [Tooltip("Hover ring where a click would be obeyed.")] public Color validColor = new Color(1f, 0.93f, 0.62f, 0.95f);
        [Tooltip("Hover ring and cross where a click would be refused, and the reject flash.")] public Color invalidColor = new Color(1f, 0.27f, 0.2f, 0.95f);
        [Tooltip("Suggested spots (pulsing): a saturated gold disc, a bright rim and a thin dark contour.")]
        public Color suggestedFill = new Color(1f, 0.62f, 0.08f, 0.62f);
        public Color suggestedRim = new Color(1f, 0.97f, 0.6f, 1f);
        public Color suggestedEdge = new Color(0.32f, 0.13f, 0f, 0.6f);
        [Tooltip("Gold glow laid over the active capture circle's ring while armed or selected.")]
        public Color padGlowColor = new Color(1f, 0.86f, 0.3f, 0.9f);
        [Tooltip("Marker radius as a fraction of the grid's outer radius (about one unit footprint).")] public float markerRadiusFraction = 0.84f;
        [Tooltip("Width of the hover ring's solid band (world units).")] public float ringWidth = 0.2f;
        public float flashSeconds = 0.3f;

        const float Height = 0.035f;
        const int Segments = 40;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        MeshRenderer hover, selectCue, flash, suggested, padGlow;
        Mesh validMesh, invalidMesh, suggestMesh, padMesh;
        MeshFilter hoverFilter;
        readonly List<HexCoord> suggestedHexes = new List<HexCoord>(4);
        CapturePoint padPoint;
        float padMeshRadius;
        MaterialPropertyBlock mpb;
        float flashLeft;
        bool subscribed;

        /// <summary>True while an entry is armed or a unit is selected (the hover marker and the capture glow show).</summary>
        public bool Visible => army != null && input != null && (input.ArmedEntry >= 0 || army.Selected != null);
        /// <summary>Spots glowing as suggestions (empty when none).</summary>
        public IReadOnlyList<HexCoord> Suggested => suggestedHexes;
        /// <summary>The hover marker drawn this frame (None when hidden).</summary>
        public MarkerKind HoverKind { get; private set; }
        /// <summary>Ground point of the hover marker: the snapped spot's center, or the pointer off the field.</summary>
        public Vector3 HoverPoint { get; private set; }
        /// <summary>True while the invalid hover marker shows.</summary>
        public bool InvalidShown => HoverKind == MarkerKind.Invalid;
        /// <summary>The unit under the pointer that a click would select (a soft ring under it, no move marker); null
        /// when none shows. Never the selected unit itself.</summary>
        public PlayerUnit SelectCueUnit { get; private set; }
        [Tooltip("Soft ring under a friendly unit a click would select.")] public Color selectCueColor = new Color(1f, 1f, 1f, 0.55f);
        /// <summary>True while the reject flash shows.</summary>
        public bool FlashShown => flash != null && flash.enabled;
        /// <summary>True while the active capture circle's ring glows gold.</summary>
        public bool PadGlowing => padGlow != null && padGlow.enabled;
        /// <summary>The capture point whose ring glows (null while none does).</summary>
        public CapturePoint PadGlowPoint => PadGlowing ? padPoint : null;
        /// <summary>Radius of the round markers (hover, flash, suggested) in world units.</summary>
        public float MarkerRadius => army != null ? army.HexOuterRadius * markerRadiusFraction : 1f;

        /// <summary>What the spot is under the rules (see HexStyle).</summary>
        public HexStyle StyleOf(HexCoord hex)
        {
            if (army == null) return HexStyle.Normal;
            if (army.Grid.IsBlocked(hex)) return HexStyle.Blocked;
            var cp = ActivePoint();
            return cp != null && army.IsPadHex(hex, cp) ? HexStyle.Pad : HexStyle.Normal;
        }

        /// <summary>The hover marker the spot would show now: Valid where a click would be obeyed (a deploy of the
        /// armed entry, or a move of the selected unit), None over a friendly unit (a click selects it), else Invalid.
        /// None while nothing is armed or selected.</summary>
        public MarkerKind MarkerFor(HexCoord hex)
        {
            if (!Visible) return MarkerKind.None;
            var g = army.Grid;
            if (!g.InBounds(hex)) return MarkerKind.Invalid;
            if (input.ArmedEntry >= 0) return army.CanDeploy(input.ArmedEntry, hex) ? MarkerKind.Valid : MarkerKind.Invalid;
            if (g.OwnerOf(hex) != 0) return MarkerKind.None;
            return g.IsFree(hex) ? MarkerKind.Valid : MarkerKind.Invalid;
        }

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            // Sorting orders keep the layers in this order whatever the camera distance to each mesh's bounds.
            padGlow = MakeRenderer("PadGlow", 0);
            hover = MakeRenderer("Hover", 1);
            selectCue = MakeRenderer("SelectCue", 1);
            suggested = MakeRenderer("Suggested", 2);
            flash = MakeRenderer("Flash", 3);
            hoverFilter = hover.GetComponent<MeshFilter>();
        }

        void Start()
        {
            if (army == null) return;
            float r = MarkerRadius;
            validMesh = BuildRing("MarkerValid (runtime)", r, ringWidth, false);
            invalidMesh = BuildRing("MarkerInvalid (runtime)", r, ringWidth, true);
            hoverFilter.sharedMesh = validMesh;
            selectCue.GetComponent<MeshFilter>().sharedMesh = validMesh;
            flash.GetComponent<MeshFilter>().sharedMesh = invalidMesh;
            Subscribe(true);
        }

        void OnEnable() { if (validMesh != null) Subscribe(true); }
        void OnDisable() { Subscribe(false); }

        void OnDestroy()
        {
            if (validMesh != null) Destroy(validMesh);
            if (invalidMesh != null) Destroy(invalidMesh);
            if (suggestMesh != null) Destroy(suggestMesh);
            if (padMesh != null) Destroy(padMesh);
        }

        /// <summary>Makes these spots glow (out of bounds ones are dropped). Null or empty clears the glow.</summary>
        public void SetSuggested(IList<HexCoord> hexes)
        {
            suggestedHexes.Clear();
            if (hexes != null && army != null)
            {
                var g = army.Grid;
                for (int i = 0; i < hexes.Count; i++)
                    if (g.InBounds(hexes[i]) && !suggestedHexes.Contains(hexes[i])) suggestedHexes.Add(hexes[i]);
            }
            if (suggestMesh != null) { Destroy(suggestMesh); suggestMesh = null; }
            if (suggestedHexes.Count > 0)
            {
                suggestMesh = BuildSuggested(army.Grid);
                suggested.GetComponent<MeshFilter>().sharedMesh = suggestMesh;
                suggested.transform.position = Vector3.up * (Height + 0.004f);
            }
            suggested.enabled = suggestedHexes.Count > 0;
        }

        void Subscribe(bool on)
        {
            if (army == null || on == subscribed) return;
            if (on) army.OrderRejected += OnRejected;
            else army.OrderRejected -= OnRejected;
            subscribed = on;
        }

        MeshRenderer MakeRenderer(string n, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        void OnRejected(HexCoord hex)
        {
            AudioManager.Play(SoundId.Reject, transform.position);
            var g = army.Grid;
            Vector3 at;
            if (g.InBounds(hex)) at = g.Center(hex);
            else if (input != null && input.HasHover) at = input.HoverWorld; // off the field: where the pointer is
            else return;
            at.y = Height + 0.006f;
            flash.transform.position = at;
            SetColor(flash, invalidColor);
            flashLeft = flashSeconds;
        }

        void LateUpdate()
        {
            if (army == null || validMesh == null) return;
            bool vis = Visible;

            // Hover marker.
            HoverKind = MarkerKind.None;
            var cueUnit = vis && input.ArmedEntry < 0 ? input.HoverUnit : null;
            SelectCueUnit = cueUnit != null && cueUnit != army.Selected ? cueUnit : null;
            selectCue.enabled = SelectCueUnit != null;
            if (selectCue.enabled)
            {
                Vector3 cc = SelectCueUnit.Centroid;
                selectCue.transform.position = new Vector3(cc.x, Height + 0.002f, cc.z);
                SetColor(selectCue, selectCueColor);
            }
            if (vis && input.HasHover && cueUnit == null)
            {
                HoverKind = input.HoverOnMap ? MarkerFor(input.Hover) : MarkerKind.Invalid;
                HoverPoint = input.HoverOnMap ? army.Grid.Center(input.Hover) : input.HoverWorld;
            }
            hover.enabled = HoverKind != MarkerKind.None;
            if (hover.enabled)
            {
                bool ok = HoverKind == MarkerKind.Valid;
                hoverFilter.sharedMesh = ok ? validMesh : invalidMesh;
                hover.transform.position = new Vector3(HoverPoint.x, Height + 0.002f, HoverPoint.z);
                Color c = ok ? validColor : invalidColor;
                if (ok) c.a *= Mathf.Lerp(0.82f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                SetColor(hover, c);
            }

            // Capture glow on the active point's ring.
            var cp = vis ? ActivePoint() : null;
            if (cp != null) EnsurePadMesh(cp);
            padGlow.enabled = cp != null;
            if (cp != null)
            {
                Vector3 p = cp.transform.position;
                padGlow.transform.position = new Vector3(p.x, p.y + 0.06f, p.z); // just above the ring quad (0.05)
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
                Color g = padGlowColor;
                g.a *= Mathf.Lerp(0.55f, 1f, k);
                SetColor(padGlow, g);
            }

            if (flashLeft > 0f) flashLeft -= Time.deltaTime;
            flash.enabled = flashLeft > 0f;

            if (suggested.enabled)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                SetColor(suggested, new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, k)));
            }
        }

        void SetColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>The active, not yet captured point (the one the player should stand on), null when none.</summary>
        static CapturePoint ActivePoint()
        {
            var bm = BattleManager.Instance;
            if (bm == null || bm.capturePoints == null) return null;
            for (int i = 0; i < bm.capturePoints.Length; i++)
            {
                var cp = bm.capturePoints[i];
                if (cp != null && cp.Active && !cp.Captured) return cp;
            }
            return null;
        }

        /// <summary>The glow band sits over the capture ring as drawn (TW/RingArc band 0.84 to 0.97 of DrawnRadius).</summary>
        void EnsurePadMesh(CapturePoint cp)
        {
            if (padMesh != null && cp == padPoint && Mathf.Approximately(padMeshRadius, cp.DrawnRadius)) return;
            padPoint = cp;
            padMeshRadius = cp.DrawnRadius;
            if (padMesh != null) Destroy(padMesh);
            float R = cp.DrawnRadius;
            padMesh = BuildBands("CaptureGlow (runtime)", 64, new[] { R * 0.74f, R * 0.84f, R * 0.97f, R * 1.08f },
                new[] { new Color(1, 1, 1, 0f), Color.white, Color.white, new Color(1, 1, 1, 0f) });
            padGlow.GetComponent<MeshFilter>().sharedMesh = padMesh;
        }

        // ---------------------------------------------------------------- meshes (flat, y = 0, vertex colored)

        /// <summary>Concentric rings: ring i at radii[i] with colors[i]; quads between neighbors.</summary>
        static Mesh BuildBands(string name, int seg, float[] radii, Color[] colors)
        {
            var v = new List<Vector3>(); var col = new List<Color>(); var tris = new List<int>();
            AddBands(v, col, tris, Vector3.zero, seg, radii, colors);
            return Finish(name, v, col, tris);
        }

        static void AddBands(List<Vector3> v, List<Color> col, List<int> tris, Vector3 center, int seg, float[] radii, Color[] colors)
        {
            int start = v.Count;
            for (int ring = 0; ring < radii.Length; ring++)
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radii[ring]);
                    col.Add(colors[ring]);
                }
            for (int ring = 0; ring + 1 < radii.Length; ring++)
            {
                int a0 = start + ring * seg, b0 = start + (ring + 1) * seg;
                for (int i = 0; i < seg; i++)
                {
                    int n = (i + 1) % seg;
                    tris.Add(a0 + i); tris.Add(b0 + n); tris.Add(b0 + i);
                    tris.Add(a0 + i); tris.Add(a0 + n); tris.Add(b0 + n);
                }
            }
        }

        /// <summary>A filled disc of radius r in 'fill' (center fan).</summary>
        static void AddDisc(List<Vector3> v, List<Color> col, List<int> tris, Vector3 center, int seg, float r, Color fill)
        {
            int c = v.Count;
            v.Add(center); col.Add(fill);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                col.Add(fill);
            }
            for (int i = 0; i < seg; i++) { tris.Add(c); tris.Add(c + 1 + (i + 1) % seg); tris.Add(c + 1 + i); }
        }

        static void AddBar(List<Vector3> v, List<Color> col, List<int> tris, float angleDeg, float halfLength, float halfWidth, Color c)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var along = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var across = new Vector3(-along.z, 0f, along.x);
            int s = v.Count;
            v.Add(-along * halfLength - across * halfWidth);
            v.Add(along * halfLength - across * halfWidth);
            v.Add(along * halfLength + across * halfWidth);
            v.Add(-along * halfLength + across * halfWidth);
            for (int i = 0; i < 4; i++) col.Add(c);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
        }

        /// <summary>The hover ring: a faint footprint disc, a soft inner glow, a solid band and a crisp outer edge.
        /// 'cross' adds an X through the middle (the invalid marker).</summary>
        static Mesh BuildRing(string name, float r, float w, bool cross)
        {
            var v = new List<Vector3>(); var col = new List<Color>(); var tris = new List<int>();
            // The thin dark contour keeps the ring readable on pale ground (snow, sand); _BaseColor tints the rest.
            var clear = new Color(0f, 0f, 0f, 0f);
            var faint = new Color(1f, 1f, 1f, 0.18f);
            // A thick dark contour (alpha 0.8) just outside the band, so the ring reads on pale sand and snow as well as
            // on grass.
            var dark = new Color(0.08f, 0.06f, 0.05f, 0.8f);
            AddDisc(v, col, tris, Vector3.zero, Segments, r - w * 2.2f, faint);
            AddBands(v, col, tris, Vector3.zero, Segments,
                new[] { r - w * 2.2f, r - w * 0.5f, r + w * 0.5f, r + w * 0.5f + 0.005f, r + w * 0.5f + 0.16f, r + w * 0.5f + 0.22f },
                new[] { faint, Color.white, Color.white, dark, dark, clear });
            if (cross)
            {
                float half = (r - w) * 0.62f;
                AddBar(v, col, tris, 45f, half, w * 0.55f, Color.white);
                AddBar(v, col, tris, -45f, half, w * 0.55f, Color.white);
            }
            return Finish(name, v, col, tris);
        }

        /// <summary>Each suggested spot: a saturated disc, a bright rim and a thin dark contour just outside it
        /// (colors in the vertices; _BaseColor pulses the alpha).</summary>
        Mesh BuildSuggested(HexGrid g)
        {
            var v = new List<Vector3>(); var col = new List<Color>(); var tris = new List<int>();
            float r = MarkerRadius, rim = 0.3f;
            var edgeOut = new Color(suggestedEdge.r, suggestedEdge.g, suggestedEdge.b, 0f);
            for (int i = 0; i < suggestedHexes.Count; i++)
            {
                Vector3 p = g.Center(suggestedHexes[i]);
                AddDisc(v, col, tris, p, Segments, r - rim, suggestedFill);
                AddBands(v, col, tris, p, Segments, new[] { r - rim, r - rim + 0.02f, r, r + 0.02f, r + 0.12f },
                    new[] { suggestedFill, suggestedRim, suggestedRim, suggestedEdge, edgeOut });
            }
            return Finish("SuggestedSpots (runtime)", v, col, tris);
        }

        static Mesh Finish(string name, List<Vector3> v, List<Color> col, List<int> tris)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetColors(col);
            m.SetUVs(0, new Vector2[v.Count]);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
