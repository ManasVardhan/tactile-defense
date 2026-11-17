using System.Collections.Generic;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Formation
{
    /// <summary>Painted stroke feedback: a soft glow ribbon under a dashed team colored line, lying on the ground.
    /// Shows the live stroke while dragging, then the smoothed result, then fades out.</summary>
    public class StrokeRenderer : MonoBehaviour
    {
        public float width = 0.3f;
        public float glowWidth = 1.0f;
        public float dashTiling = 1.6f;
        public float holdTime = 0.5f;
        public float fadeTime = 0.6f;
        public float lift = 0.035f;

        LineRenderer core, glow;
        Vector3[] buffer = new Vector3[1024];
        float fadeTimer = -1f;
        readonly Color coreColor = Color.white, glowColor = Color.white; // tint comes from the material, vertex alpha fades
        readonly List<Vector3> smoothTmp = new List<Vector3>(1024);
        readonly List<Vector3> scratch = new List<Vector3>(2048);

        public void Init(TeamMaterials mats)
        {
            glow = MakeLine("StrokeGlow", mats != null ? mats.strokeGlow : null, glowWidth, 0);
            core = MakeLine("StrokeCore", mats != null ? mats.stroke : null, width, 1);
            core.textureMode = LineTextureMode.Tile;
            core.textureScale = new Vector2(dashTiling, 1f);
            Hide();
        }

        LineRenderer MakeLine(string n, Material m, float w, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.sharedMaterial = m;
            lr.widthMultiplier = w;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = order;
            lr.positionCount = 0;
            return lr;
        }

        public void ShowLive(List<Vector3> raw)
        {
            if (core == null) return;
            smoothTmp.Clear();
            for (int i = 0; i < raw.Count; i++) smoothTmp.Add(raw[i]);
            PathResampler.Chaikin(smoothTmp, false, 1, scratch);
            Apply(smoothTmp, false);
            fadeTimer = -1f;
            SetAlpha(1f);
        }

        public void ShowFinal(List<Vector3> pts, bool closed)
        {
            if (core == null) return;
            Apply(pts, closed);
            fadeTimer = holdTime + fadeTime;
            SetAlpha(1f);
        }

        public void Hide()
        {
            if (core == null) return;
            core.positionCount = 0;
            glow.positionCount = 0;
            fadeTimer = -1f;
        }

        void Apply(List<Vector3> pts, bool closed)
        {
            int n = Mathf.Min(pts.Count, buffer.Length - 1);
            for (int i = 0; i < n; i++) { Vector3 p = pts[i]; p.y = lift; buffer[i] = p; }
            if (closed && n > 2) { buffer[n] = buffer[0]; n++; }
            core.positionCount = n;
            core.SetPositions(buffer);
            glow.positionCount = n;
            glow.SetPositions(buffer);
        }

        void SetAlpha(float a)
        {
            Color c = coreColor; c.a *= a;
            core.startColor = c; core.endColor = c;
            Color g = glowColor; g.a *= a;
            glow.startColor = g; glow.endColor = g;
        }

        void Update()
        {
            if (fadeTimer < 0f) return;
            fadeTimer -= Time.deltaTime;
            if (fadeTimer <= 0f) { Hide(); return; }
            SetAlpha(Mathf.Clamp01(fadeTimer / fadeTime));
        }
    }
}
