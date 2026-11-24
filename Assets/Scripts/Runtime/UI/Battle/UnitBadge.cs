using System.Collections.Generic;
using TactileWars.Army;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// Screen space tag over one deployed unit: the type glyph on its accent color, always shown while the unit is on
    /// screen so unit types read apart at a glance, and next to it one pip per soldier, filled while that soldier
    /// lives (half tone below half health, hollow once dead). The pips show while the unit is selected or hurt.
    /// Created and removed by UnitBadgeLayer. Not a raycast target (so it never blocks map input); a click on it
    /// selects its unit through ArmyInput.BadgeHitTest.
    /// </summary>
    public class UnitBadge : MonoBehaviour
    {
        const float PipSize = 18f;
        static readonly Color PipFull = UIColors.Green;
        static readonly Color PipHalf = Color.Lerp(UIColors.Green, UIColors.SlateDark, 0.55f);
        static readonly Color PipEmpty = UIColors.WithAlpha(Color.black, 0.35f);

        RectTransform rt;
        CanvasGroup cg, pipGroup;
        float width;
        Image[] pips;
        Image[] pipRims;
        TextMeshProUGUI glyph;
        RectTransform rimRt;

        public PlayerUnit Unit { get; private set; }
        /// <summary>The pips (and their backing) show: the unit is selected or hurt, and on screen.</summary>
        public bool Shown { get; private set; }
        /// <summary>The glyph disc shows: the unit is on screen.</summary>
        public bool GlyphShown { get; private set; }
        public Color AccentColor { get; private set; }
        /// <summary>Pips of living soldiers (full or half tone).</summary>
        public int FilledPips { get; private set; }
        /// <summary>Pips of living soldiers below half health.</summary>
        public int HalfTonePips { get; private set; }
        public string Glyph => glyph.text;

        public static UnitBadge Create(RectTransform parent, PlayerUnit unit)
        {
            var skin = UIFactory.Skin;
            int n = Mathf.Max(1, unit.StartCount);
            float w = 62f + n * (PipSize + 6f) + 10f;
            var rt = UIFactory.Rect(parent, "Badge_" + unit.Id).At(0.5f, 0.5f, 0f, 0f, w, 46f, 0.5f, 0f);
            var b = rt.gameObject.AddComponent<UnitBadge>();
            b.rt = rt;
            b.Unit = unit;
            b.cg = rt.gameObject.AddComponent<CanvasGroup>();
            b.cg.blocksRaycasts = false;
            b.width = w;
            var pipsRt = UIFactory.Rect(rt, "Pips").Stretch();
            b.pipGroup = pipsRt.gameObject.AddComponent<CanvasGroup>();
            b.pipGroup.blocksRaycasts = false;
            var bg = UIFactory.Img(pipsRt, "Bg", skin != null ? skin.pill : null, UIColors.WithAlpha(UIColors.SlateDark, 0.85f), UIFactory.PillMult(46f));
            ((RectTransform)bg.transform).Stretch(16f, 4f, 0f, 4f);
            var accent = unit.Type != null ? unit.Type.accent : Color.white;
            b.AccentColor = accent;
            var rim = UIFactory.Img(rt, "Rim", skin != null ? skin.circle : null, Color.white);
            ((RectTransform)rim.transform).At(0f, 0.5f, 0f, 0f, 50f, 50f, 0f, 0.5f);
            b.rimRt = (RectTransform)rim.transform;
            var disc = UIFactory.Img(rim.transform, "Disc", skin != null ? skin.circle : null, accent);
            ((RectTransform)disc.transform).Stretch(4f, 4f, 4f, 4f);
            b.glyph = UIFactory.Text(rim.transform, "Letter", unit.Type != null ? unit.Type.glyph : "?", 30f, TextStyle.HeadingThin);
            ((RectTransform)b.glyph.transform).Stretch(0f, 0f, 0f, 2f);
            b.pips = new Image[n];
            b.pipRims = new Image[n];
            for (int i = 0; i < n; i++)
            {
                float x = 60f + i * (PipSize + 6f);
                b.pipRims[i] = UIFactory.Img(pipsRt, "PipRim" + i, skin != null ? skin.circle : null, UIColors.WithAlpha(Color.white, 0.9f));
                ((RectTransform)b.pipRims[i].transform).At(0f, 0.5f, x - 2f, 0f, PipSize + 4f, PipSize + 4f, 0f, 0.5f);
                b.pips[i] = UIFactory.Img(pipsRt, "Pip" + i, skin != null ? skin.circle : null, PipFull);
                ((RectTransform)b.pips[i].transform).At(0f, 0.5f, x, 0f, PipSize, PipSize, 0f, 0.5f);
            }
            rim.transform.SetAsLastSibling(); // the disc draws over the pill
            b.SetShown(false, false);
            return b;
        }

        /// <summary>True when the badge shows and the screen point lies on its disc (or on the whole tag while the
        /// pips show), with a few pixels of slack.</summary>
        public bool Contains(Vector2 screen, Camera canvasCam)
        {
            if (!GlyphShown) return false;
            var r = Shown ? rt : rimRt;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screen, canvasCam, out Vector2 local)) return false;
            var rect = r.rect;
            float slack = 6f;
            return local.x >= rect.xMin - slack && local.x <= rect.xMax + slack && local.y >= rect.yMin - slack && local.y <= rect.yMax + slack;
        }

        /// <summary>Screen point of the badge's glyph disc center (tests).</summary>
        public Vector2 DiscScreenPoint(Camera canvasCam) =>
            RectTransformUtility.WorldToScreenPoint(canvasCam, rimRt.TransformPoint(rimRt.rect.center));

        void SetShown(bool glyphOn, bool pipsOn)
        {
            GlyphShown = glyphOn;
            Shown = glyphOn && pipsOn;
            cg.alpha = glyphOn ? 1f : 0f;
            pipGroup.alpha = Shown ? 1f : 0f;
            // Glyph alone: center the disc over the unit; with pips: center the whole tag.
            rt.pivot = new Vector2(Shown ? 0.5f : 25f / width, 0f);
        }

        /// <summary>Recounts pips and places the badge over the unit; called every frame by the layer.</summary>
        public void Tick(Camera cam, RectTransform space, Camera canvasCam)
        {
            int full = 0, half = 0;
            var list = Unit.Soldiers;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive) continue;
                if (s.Health.Fraction < 0.5f) half++; else full++;
            }
            FilledPips = full + half;
            HalfTonePips = half;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].color = i < full ? PipFull : (i < full + half ? PipHalf : PipEmpty);
                pipRims[i].color = i < full + half ? UIColors.WithAlpha(Color.white, 0.9f) : UIColors.WithAlpha(Color.white, 0.35f);
            }

            bool pipsWanted = Unit.Selected || Unit.HealthFraction < 0.999f;
            bool onScreen = false;
            if (cam != null)
            {
                float lift = 1.75f * Units.Soldier.SizeScale * (Unit.Type != null ? Unit.Type.modelScale : 1f);
                Vector3 sp = cam.WorldToScreenPoint(Unit.Centroid + Vector3.up * lift);
                Vector2 local = default;
                onScreen = sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(space, sp, canvasCam, out local)
                           && Mathf.Abs(local.x) < space.rect.width * 0.5f && Mathf.Abs(local.y) < space.rect.height * 0.5f;
                if (onScreen) rt.anchoredPosition = local;
            }
            if (onScreen != GlyphShown || (onScreen && pipsWanted) != Shown) SetShown(onScreen, pipsWanted);
        }
    }
}
