using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// A cream card with a short message, a small header chip, a bobbing arrow and a pulsing ring that point at a
    /// spot on the screen. The card takes the side of the spot (above, below, right or left) that leaves it the most
    /// free screen space: inside the allowed area and clear of the spot and of every area registered with Avoid
    /// (the selected unit, suggested hexes), so it never covers what it talks about or what the player commands.
    /// Positions are in the parent's local space (centered). Nothing here is a raycast target, so map clicks pass
    /// through.
    /// </summary>
    public class Callout : MonoBehaviour
    {
        public enum Side { Auto, Below, Left, Above, Right }

        const float Pad = 26f, ArrowSize = 72f, Gap = 14f;
        /// <summary>Cards keep above this band at the bottom (the unit bar) unless pointing into it.</summary>
        const float BottomBand = 150f;

        RectTransform parent, root, card, arrow, ring;
        RectTransform header;
        TextMeshProUGUI text, headerText;
        Image ringImg;
        CanvasGroup cg;
        float width;
        float t;
        Vector2 target;
        float targetRadius;
        bool hasTarget, showRing;
        Side side;
        readonly System.Collections.Generic.List<Vector3> avoid = new System.Collections.Generic.List<Vector3>(8); // x, y, radius
        static readonly Side[] Order = { Side.Above, Side.Below, Side.Right, Side.Left };

        public string Text => text.text;
        /// <summary>The header chip's label (for example "STEP 6 OF 8").</summary>
        public string Header => headerText.text;
        public bool Showing => root.gameObject.activeSelf;
        /// <summary>Card rect in the parent's space (for tests and layout checks).</summary>
        public Rect CardRect { get; private set; }

        public static Callout Create(RectTransform parent, string name, float width, Color headerColor)
        {
            var root = UIFactory.Rect(parent, name).Stretch();
            var c = root.gameObject.AddComponent<Callout>();
            c.parent = parent;
            c.root = root;
            c.width = width;
            c.cg = root.gameObject.AddComponent<CanvasGroup>();
            c.cg.blocksRaycasts = false;
            c.cg.interactable = false;
            var skin = UIFactory.Skin;

            c.ring = UIFactory.Rect(root, "Ring").At(0.5f, 0.5f, 0f, 0f, 100f, 100f);
            c.ringImg = UIFactory.Img(c.ring, "Img", skin != null ? skin.ringThin : null, UIColors.Gold);
            ((RectTransform)c.ringImg.transform).Stretch();

            var face = UIFactory.Card(root, "Card", width, 140f, 30f);
            c.card = (RectTransform)face.parent;
            c.card.anchorMin = c.card.anchorMax = c.card.pivot = new Vector2(0.5f, 0.5f);
            c.text = UIFactory.Paragraph(face, "Text", "", 36f, UIColors.Ink, TextAlignmentOptions.Center);
            ((RectTransform)c.text.transform).Stretch(Pad, Pad + 4f, Pad, Pad - 6f);
            c.text.lineSpacing = 0f;

            c.header = UIFactory.Tag(c.card, "Header", "TIP", headerColor, 40f, 24f);
            c.header.At(0f, 1f, 22f, 18f, c.header.sizeDelta.x, 40f, 0f, 0.5f);
            c.headerText = c.header.GetComponentInChildren<TextMeshProUGUI>();

            c.arrow = UIFactory.Rect(root, "Arrow").At(0.5f, 0.5f, 0f, 0f, ArrowSize, ArrowSize);
            var a = UIFactory.Icon(c.arrow, UIIcon.ArrowUp, ArrowSize);
            ((RectTransform)a.transform).Stretch();
            root.gameObject.SetActive(false);
            return c;
        }

        public void Show(string label, string message)
        {
            bool fresh = !root.gameObject.activeSelf || text.text != message;
            root.gameObject.SetActive(true);
            text.text = message;
            headerText.text = label;
            float tw = headerText.GetPreferredValues(label).x;
            header.sizeDelta = new Vector2(tw + 40f, header.sizeDelta.y);
            float h = text.GetPreferredValues(message, width - 2f * Pad, 0f).y + 2f * Pad + 12f;
            card.sizeDelta = new Vector2(width, Mathf.Max(110f, h));
            if (fresh)
            {
                t = 0f;
                cg.alpha = 1f;
                UIAnim.Pop(card, 0.85f, 0.3f);
            }
        }

        public void Hide() { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); }

        /// <summary>Forgets every area registered with Avoid.</summary>
        public void ClearAvoid() { avoid.Clear(); }

        /// <summary>Keeps the card off a circle around 'local' (parent space) when another side allows it.</summary>
        public void Avoid(Vector2 local, float radius) { avoid.Add(new Vector3(local.x, local.y, Mathf.Max(10f, radius))); }

        /// <summary>True when the card's rect overlaps the square around 'local' with half size 'radius' (tests).</summary>
        public bool Covers(Vector2 local, float radius) => Overlap(CardRect, local, radius) > 0f;

        /// <summary>Hides or shows without forgetting the message (used while a hint takes the stage).</summary>
        public void SetVisible(bool on) { cg.alpha = on ? 1f : 0f; }

        /// <summary>Points at 'local' (parent space); 'radius' is how far the card and arrow keep from it. The ring
        /// circles world targets; HUD buttons have their own outline, so they skip it.</summary>
        public void PointAt(Vector2 local, float radius, Side preferred = Side.Auto, bool ring = true)
        {
            target = local;
            targetRadius = radius;
            hasTarget = true;
            side = preferred;
            showRing = ring;
        }

        /// <summary>No target: the card sits at 'local' with no arrow or ring.</summary>
        public void PlaceAt(Vector2 local)
        {
            target = local;
            hasTarget = false;
        }

        static readonly float[] NoTargetOffsets = { 260f, -260f, 420f };

        void LateUpdate()
        {
            if (parent == null) return;
            t += Time.unscaledDeltaTime;
            Vector2 size = parent.rect.size;
            Vector2 half = size * 0.5f;
            Vector2 cs = card.sizeDelta;
            Vector2 pos;
            Vector2 dir = Vector2.zero; // from the card toward the target
            if (!hasTarget)
            {
                // No target: the asked spot, or straight up or down from it when that is clearer of the avoided areas.
                pos = Clamp(target, cs, half);
                float best = Penalty(new Rect(pos - cs * 0.5f, cs), half, false);
                for (int i = 0; i < NoTargetOffsets.Length; i++)
                {
                    Vector2 p = Clamp(target + new Vector2(0f, NoTargetOffsets[i]), cs, half);
                    float sc = Penalty(new Rect(p - cs * 0.5f, cs), half, false) + 1f;
                    if (sc < best) { best = sc; pos = p; }
                }
            }
            else
            {
                float bestScore = float.MaxValue;
                pos = target;
                bool intoBand = target.y - targetRadius < -half.y + BottomBand;
                foreach (var sd in Order)
                {
                    Vector2 p = Place(sd, cs, out Vector2 toTarget);
                    Vector2 c = Clamp(p, cs, half);
                    var r = new Rect(c - cs * 0.5f, cs);
                    // Off the allowed area, over the spot, over avoided areas; a small bias keeps the usual order and
                    // the asked side when everything else is equal.
                    float sc = Penalty(r, half, !intoBand) + 4f * Overlap(r, target, targetRadius) + (c - p).magnitude * cs.y * 0.5f;
                    sc += System.Array.IndexOf(Order, sd) * 50f;
                    if (sd == side) sc -= 400f;
                    if (sc < bestScore) { bestScore = sc; pos = c; dir = toTarget; }
                }
            }
            card.anchoredPosition = pos;
            CardRect = new Rect(pos - cs * 0.5f, cs);

            arrow.gameObject.SetActive(hasTarget);
            ring.gameObject.SetActive(hasTarget && showRing);
            if (!hasTarget) return;
            float bob = Mathf.Sin(t * 6f) * 8f;
            if (dir == Vector2.zero) dir = Vector2.down;
            Vector2 tip = target - dir * (targetRadius + Gap * 0.5f);
            arrow.anchoredPosition = tip - dir * (ArrowSize * 0.5f + bob);
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            float k = Mathf.Repeat(t, 1.1f) / 1.1f;
            float d = Mathf.Max(60f, targetRadius * 2f) * Mathf.Lerp(0.9f, 1.25f, k);
            ring.anchoredPosition = target;
            ring.sizeDelta = new Vector2(d, d);
            ringImg.color = UIColors.WithAlpha(UIColors.Gold, 1f - k);
        }

        /// <summary>Card center on side 'sd' of the target, and the arrow direction from the card to the target.</summary>
        Vector2 Place(Side sd, Vector2 cs, out Vector2 dir)
        {
            float reach = targetRadius + ArrowSize + Gap;
            switch (sd)
            {
                case Side.Below: dir = Vector2.up; return target + new Vector2(0f, -(reach + cs.y * 0.5f));
                case Side.Left: dir = Vector2.right; return target + new Vector2(-(reach + cs.x * 0.5f), 0f);
                case Side.Right: dir = Vector2.left; return target + new Vector2(reach + cs.x * 0.5f, 0f);
                default: dir = Vector2.down; return target + new Vector2(0f, reach + cs.y * 0.5f);
            }
        }

        static Vector2 Clamp(Vector2 pos, Vector2 cs, Vector2 half)
        {
            const float m = 20f;
            pos.x = Mathf.Clamp(pos.x, -half.x + cs.x * 0.5f + m, half.x - cs.x * 0.5f - m);
            pos.y = Mathf.Clamp(pos.y, -half.y + cs.y * 0.5f + m, half.y - cs.y * 0.5f - m);
            return pos;
        }

        /// <summary>Area of 'r' outside the allowed band (below the top HUD, above the unit bar unless pointing into
        /// it) plus its overlap with the avoided areas.</summary>
        float Penalty(Rect r, Vector2 half, bool keepOffBottom)
        {
            float topLimit = half.y - 120f, bottomLimit = -half.y + BottomBand;
            float p = Mathf.Max(0f, r.yMax - topLimit) * r.width;
            if (keepOffBottom) p += Mathf.Max(0f, bottomLimit - r.yMin) * r.width;
            for (int i = 0; i < avoid.Count; i++) p += Overlap(r, new Vector2(avoid[i].x, avoid[i].y), avoid[i].z);
            return p;
        }

        /// <summary>Overlap area of 'r' and the square of half size 'radius' around 'c'.</summary>
        static float Overlap(Rect r, Vector2 c, float radius)
        {
            float w = Mathf.Min(r.xMax, c.x + radius) - Mathf.Max(r.xMin, c.x - radius);
            float h = Mathf.Min(r.yMax, c.y + radius) - Mathf.Max(r.yMin, c.y - radius);
            return w > 0f && h > 0f ? w * h : 0f;
        }
    }
}
