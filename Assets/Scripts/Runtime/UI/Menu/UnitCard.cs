using System;
using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// One unit type on the Army screen's catalogue: its glyph on its accent color (the badge the battle uses), its
    /// name and one-sentence description, four stat bars compared with the Attacker (UnitStatBars, read from the
    /// type's config), and at the bottom either an ADD button or, when locked, "Clear Level N to unlock" on a dimmed
    /// card. A type unlocked since the player last opened Army carries a NEW tag; a type already in the squad shows
    /// how many times.
    /// </summary>
    public class UnitCard : MonoBehaviour
    {
        const float Inset = 12f, BandH = 96f, BarRowH = 24f, FooterH = 54f;

        readonly Dictionary<UnitStat, float> fills = new Dictionary<UnitStat, float>(4);
        TextMeshProUGUI nameText, descText, lockText;

        public UnitTypeId Type { get; private set; }
        public bool Locked { get; private set; }
        public bool IsNew { get; private set; }
        public int InSquad { get; private set; }
        /// <summary>The ADD button (null on a locked card).</summary>
        public UIButton AddButton { get; private set; }
        /// <summary>"Clear Level 3 to unlock" on a locked card, empty otherwise.</summary>
        public string LockText => lockText != null ? lockText.text : "";
        public string NameText => nameText != null ? nameText.text : "";
        public string Description => descText != null ? descText.text : "";
        /// <summary>The glyph band at the top (callouts point at it).</summary>
        public RectTransform Band { get; private set; }
        /// <summary>The fill a stat bar shows (0 to 1).</summary>
        public float BarFill(UnitStat s) => fills.TryGetValue(s, out var f) ? f : 0f;

        public static UnitCard Create(Transform parent, UnitTypeId id, UnitTypeConfig cfg, UnitTypeConfig reference, float w, float h,
            bool locked, bool isNew, int inSquad, Action<UnitTypeId> onAdd)
        {
            var root = UIFactory.Rect(parent, "Unit_" + id);
            root.sizeDelta = new Vector2(w, h);
            var c = root.gameObject.AddComponent<UnitCard>();
            c.Type = id;
            c.Locked = locked;
            c.IsNew = isNew && !locked;
            c.InSquad = inSquad;
            c.Build(root, cfg, reference, w, h, onAdd);
            return c;
        }

        void Build(RectTransform root, UnitTypeConfig cfg, UnitTypeConfig reference, float w, float h, Action<UnitTypeId> onAdd)
        {
            var skin = UIFactory.Skin;
            Color accent = cfg != null ? cfg.accent : UIColors.Grey;
            var face = UIFactory.Card(root, "Card", w, h, 26f, null, IsNew ? UIColors.Gold : (Color?)null);
            ((RectTransform)face.parent).Stretch();

            // Everything but the footer dims on a locked card.
            var body = UIFactory.Rect(face, "Body").Stretch();
            if (Locked) body.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            float iw = w - 2f * Inset;

            var band = UIFactory.Panel(body, "Band", Locked ? Color.Lerp(accent, UIColors.Grey, 0.6f) : accent, 20f);
            Band = ((RectTransform)band.transform).At(0.5f, 1f, 0f, -Inset + 2f, iw, BandH, 0.5f, 1f);
            band.gameObject.AddComponent<RectMask2D>();
            var glow = UIFactory.Img(band.transform, "Glow", skin != null ? skin.glow : null, UIColors.WithAlpha(Color.white, 0.45f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, 0f, 220f, 220f);
            var disc = SquadBuilderPanel.GlyphDisc(band.transform, "Glyph", cfg, 76f);
            disc.At(0.5f, 0.5f, 0f, 0f, 76f, 76f);

            float y = -Inset - BandH - 4f;
            nameText = UIFactory.Text(body, "Name", cfg != null ? cfg.displayName : Type.ToString(), 30f, TextStyle.HeadingDark);
            ((RectTransform)nameText.transform).At(0.5f, 1f, 0f, y, iw, 36f, 0.5f, 1f);
            y -= 36f;
            var desc = UIFactory.Paragraph(body, "Description", cfg != null ? cfg.description : "", 18f, UIColors.Ink, TextAlignmentOptions.Top);
            descText = desc;
            desc.enableAutoSizing = true; desc.fontSizeMin = 14f; desc.fontSizeMax = 18f;
            ((RectTransform)desc.transform).At(0.5f, 1f, 0f, y, iw - 6f, 80f, 0.5f, 1f);
            y -= 86f;

            var stats = UnitStatBars.Shown;
            for (int i = 0; i < stats.Length; i++) StatRow(body, cfg, reference, stats[i], y - i * BarRowH, iw);

            // Footer: ADD, or the level that unlocks the type.
            float fw = iw;
            if (Locked)
            {
                var pill = UIFactory.Panel(face, "Lock", UIColors.WithAlpha(UIColors.Ink, 0.1f), FooterH * 0.5f);
                ((RectTransform)pill.transform).At(0.5f, 0f, 0f, Inset, fw, FooterH, 0.5f, 0f);
                var ic = UIFactory.Icon(pill.transform, UIIcon.Lock, 30f);
                ((RectTransform)ic.transform).At(0f, 0.5f, 12f, 0f, 30f, 30f, 0f, 0.5f);
                lockText = UIFactory.Text(pill.transform, "Text", Progression.LockText(Type), 19f, TextStyle.HeadingDark, TextAlignmentOptions.Midline);
                lockText.enableAutoSizing = true; lockText.fontSizeMin = 13f; lockText.fontSizeMax = 19f;
                ((RectTransform)lockText.transform).Stretch(44f, 0f, 10f, 2f);
                var badge = UIFactory.Img(face, "LockBadge", skin != null ? skin.circle : null, Color.white);
                ((RectTransform)badge.transform).At(1f, 1f, -4f, -4f, 46f, 46f, 1f, 1f);
                var bdisc = UIFactory.Img(badge.transform, "Disc", skin != null ? skin.circle : null, UIColors.Slate);
                ((RectTransform)bdisc.transform).Stretch(4f, 4f, 4f, 4f);
                var bic = UIFactory.Icon(badge.transform, UIIcon.Lock, 26f);
                ((RectTransform)bic.transform).At(0.5f, 0.5f, 0f, 1f, 26f, 26f);
            }
            else
            {
                AddButton = UIFactory.Button(face, "Add", "ADD", ButtonColor.Green, fw, FooterH, UIIcon.Plus, 28f);
                ((RectTransform)AddButton.transform).At(0.5f, 0f, 0f, Inset - 4f, fw, FooterH, 0.5f, 0f);
                var id = Type;
                AddButton.onClick = () => onAdd?.Invoke(id);
            }

            if (IsNew)
            {
                var tag = UIFactory.Tag(face, "New", "NEW", UIColors.Red, 36f, 22f);
                tag.At(0f, 1f, Inset - 6f, -Inset + 10f, tag.sizeDelta.x, 36f, 0f, 1f);
                tag.localRotation = Quaternion.Euler(0f, 0f, 6f);
            }
            if (InSquad > 0 && !Locked)
            {
                // How many of this unit the squad holds: a count badge on the glyph, like a stack.
                var cnt = UIFactory.Img(disc, "InSquad", skin != null ? skin.circle : null, Color.white);
                ((RectTransform)cnt.transform).At(1f, 1f, 16f, 8f, 42f, 42f, 1f, 1f);
                var cdisc = UIFactory.Img(cnt.transform, "Disc", skin != null ? skin.circle : null, UIColors.Ink);
                ((RectTransform)cdisc.transform).Stretch(4f, 4f, 4f, 4f);
                var ct = UIFactory.Text(cnt.transform, "Count", "x" + InSquad, 19f, TextStyle.HeadingThin);
                ((RectTransform)ct.transform).Stretch(0f, 0f, 0f, 2f);
            }
        }

        void StatRow(RectTransform parent, UnitTypeConfig cfg, UnitTypeConfig reference, UnitStat s, float y, float w)
        {
            const float labelW = 74f, barH = 14f;
            var row = UIFactory.Rect(parent, "Stat_" + s).At(0.5f, 1f, 0f, y, w, BarRowH, 0.5f, 1f);
            var l = UIFactory.Text(row, "Label", UnitStatBars.Label(s), 17f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)l.transform).At(0f, 0.5f, 4f, 0f, labelW, BarRowH, 0f, 0.5f);
            float bw = w - labelW - 10f;
            float fill = UnitStatBars.Fill(cfg, s, reference);
            fills[s] = fill;
            Color accent = cfg != null ? cfg.accent : UIColors.Grey;
            var bar = UIFactory.Bar(row, "Bar", bw, barH, accent, UIColors.WithAlpha(UIColors.Ink, 0.12f));
            ((RectTransform)bar.transform).At(1f, 0.5f, -4f, 0f, bw, barH, 1f, 0.5f);
            // The Attacker's level on every track, so a bar reads as "more" or "less".
            var tick = UIFactory.Img(bar.transform, "Reference", UIFactory.Skin != null ? UIFactory.Skin.white : null, UIColors.WithAlpha(UIColors.Ink, 0.45f));
            ((RectTransform)tick.transform).At(0f, 0.5f, bw * UnitStatBars.ReferenceFill - 1f, 0f, 2f, barH + 6f, 0f, 0.5f);
            if (fill <= 0f)
            {
                var fillRt = bar.fill;
                if (fillRt != null) fillRt.gameObject.SetActive(false);
                if (s == UnitStat.Damage && UnitStatBars.Heals(cfg))
                {
                    tick.gameObject.SetActive(false);
                    var heals = UIFactory.Text(bar.transform, "Heals", "Heals", 16f, TextStyle.HeadingDark, TextAlignmentOptions.Midline);
                    heals.color = UIColors.GreenDark;
                    ((RectTransform)heals.transform).Stretch(0f, -6f, 0f, -4f);
                }
            }
            else bar.Set(fill);
        }
    }
}
