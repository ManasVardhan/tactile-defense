using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// The Units tab of the Army screen. Top: "Your squad", a row of the player's squad slots (glyph and name, or
    /// "Empty"; the slots still to earn show a lock and "Level N"). Below: "Units", a catalogue of six UnitCards in
    /// unlock order. ADD on an unlocked card puts the unit in the first empty slot; with no empty slot the hint line
    /// says "Squad is full. Click a slot to remove a unit first." A locked card reads "Clear Level N to unlock".
    /// Clicking a filled slot empties it, unless it is the last filled one. Rules live in MetaGame and Progression.
    /// </summary>
    public class SquadBuilderPanel : MonoBehaviour
    {
        public const string FullMessage = "Squad is full. Click a slot to remove a unit first.";
        public const string HintMessage = "ADD puts a unit in your squad. Click a slot to remove it.";

        const float SquadH = 172f, SlotW = 150f, SlotH = 104f, SlotGap = 14f, CardGap = 16f, CardsTop = 236f;

        RectTransform slotRow, cardRow;
        TextMeshProUGUI countText, hintText;
        float width, height;
        bool fullWarning;
        ICollection<UnitTypeId> newUnits;
        readonly List<UIButton> slotButtons = new List<UIButton>(8);
        readonly List<TextMeshProUGUI> slotLabels = new List<TextMeshProUGUI>(8);
        readonly List<UnitCard> cards = new List<UnitCard>(6);

        public IReadOnlyList<UIButton> SlotButtons => slotButtons;
        /// <summary>Unit cards in Progression.AllTypes order (Attacker, Tank, Healer, Sniper, Vanguard, Demolisher).</summary>
        public IReadOnlyList<UnitCard> Cards => cards;
        /// <summary>The hint line in the squad panel (the full-squad message after a refused ADD).</summary>
        public string Message => hintText != null ? hintText.text : "";
        public bool ShowingFullMessage => fullWarning;

        public UnitCard Card(UnitTypeId id)
        {
            foreach (var c in cards) if (c != null && c.Type == id) return c;
            return null;
        }

        /// <summary>The slot's caption: the unit's name, or "Empty".</summary>
        public string SlotLabel(int i) => i >= 0 && i < slotLabels.Count ? slotLabels[i].text : null;

        /// <summary>Types that carry the NEW tag on this visit (set by the Army screen when it opens).</summary>
        public void SetNewUnits(ICollection<UnitTypeId> types) { newUnits = types; }

        public static SquadBuilderPanel Create(RectTransform parent, float w, float h)
        {
            var rt = UIFactory.Rect(parent, "SquadBuilder");
            rt.sizeDelta = new Vector2(w, h);
            var p = rt.gameObject.AddComponent<SquadBuilderPanel>();
            p.width = w;
            p.height = h;
            p.Build(rt, w);
            return p;
        }

        void Build(RectTransform rt, float w)
        {
            var squad = UIFactory.Panel(rt, "Squad", UIColors.CardInner, 28f);
            var srt = ((RectTransform)squad.transform).At(0.5f, 1f, 0f, 0f, w, SquadH, 0.5f, 1f);
            var head = UIFactory.Text(srt, "Head", "YOUR SQUAD", 36f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)head.transform).At(0f, 1f, 26f, -8f, 260f, 46f, 0f, 1f);
            countText = UIFactory.Text(srt, "Count", "", 24f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)countText.transform).At(0f, 1f, 262f, -11f, 240f, 40f, 0f, 1f);
            hintText = UIFactory.Text(srt, "Hint", HintMessage, 23f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineRight);
            ((RectTransform)hintText.transform).At(1f, 1f, -26f, -11f, w - 560f, 40f, 1f, 1f);
            slotRow = UIFactory.Rect(srt, "Slots").At(0.5f, 1f, 0f, -58f, w - 40f, SlotH, 0.5f, 1f);

            var uh = UIFactory.Text(rt, "UnitsHead", "UNITS", 36f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)uh.transform).At(0f, 1f, 10f, -SquadH - 12f, 160f, 46f, 0f, 1f);
            var us = UIFactory.Text(rt, "UnitsSub", "Clear levels to unlock new units.", 23f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)us.transform).At(0f, 1f, 150f, -SquadH - 15f, 600f, 40f, 0f, 1f);
            cardRow = UIFactory.Rect(rt, "Units").At(0.5f, 1f, 0f, -CardsTop, w, height - CardsTop, 0.5f, 1f);
        }

        // ------------------------------------------------------------------ shared look

        /// <summary>A unit's sign on every screen: its glyph in white on its accent color, in a white rim. 'locked'
        /// shows a grey disc with a lock instead.</summary>
        public static RectTransform GlyphDisc(Transform parent, string name, UnitTypeConfig type, float size, bool locked = false)
        {
            var skin = UIFactory.Skin;
            var root = UIFactory.Rect(parent, name);
            root.sizeDelta = new Vector2(size, size);
            var rim = UIFactory.Img(root, "Rim", skin != null ? skin.circle : null, Color.white);
            ((RectTransform)rim.transform).Stretch();
            float inset = Mathf.Max(3f, size * 0.06f);
            var disc = UIFactory.Img(root, "Disc", skin != null ? skin.circle : null, locked || type == null ? UIColors.GreyDark : type.accent);
            ((RectTransform)disc.transform).Stretch(inset, inset, inset, inset);
            if (locked)
            {
                var ic = UIFactory.Icon(root, UIIcon.Lock, size * 0.56f);
                ((RectTransform)ic.transform).At(0.5f, 0.5f, 0f, 0f, size * 0.56f, size * 0.56f);
            }
            else
            {
                var t = UIFactory.Text(root, "Letter", type != null ? type.glyph : "?", size * 0.6f, TextStyle.HeadingThin);
                ((RectTransform)t.transform).Stretch(0f, 0f, 0f, size * 0.05f);
            }
            return root;
        }

        static UnitTypeConfig TypeOf(UnitTypeId id)
        {
            var units = MetaGame.Db != null ? MetaGame.Db.units : null;
            return units != null ? units.Get(id) : null;
        }

        /// <summary>Gives a kit button a rounded rectangle face instead of the pill (square tiles).</summary>
        public static void RoundRect(UIButton b, float radius)
        {
            var skin = UIFactory.Skin;
            if (skin == null) return;
            foreach (var img in new[] { b.faceImage, b.lipImage })
            {
                if (img == null) continue;
                img.sprite = skin.rounded32;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = UIFactory.Round32(radius);
            }
            var shine = b.face != null ? b.face.Find("Shine") : null;
            if (shine != null) shine.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ refresh

        public void Refresh()
        {
            int cleared = MetaGame.LevelsCleared;
            var squad = MetaGame.Squad;
            BuildSlots(squad);
            BuildCards(squad, cleared);
            int filled = 0;
            foreach (var s in squad) if (s.HasValue) filled++;
            countText.text = filled + " of " + squad.Count + " slots filled";
            hintText.text = fullWarning ? FullMessage : HintMessage;
            hintText.color = fullWarning ? UIColors.RedDark : UIColors.Muted;
        }

        void BuildSlots(List<UnitTypeId?> squad)
        {
            UIFactory.ClearChildren(slotRow);
            slotButtons.Clear();
            slotLabels.Clear();
            int total = Progression.MaxSlots;
            float x0 = -(total * SlotW + (total - 1) * SlotGap) * 0.5f + SlotW * 0.5f;
            for (int i = 0; i < total; i++)
            {
                float x = x0 + i * (SlotW + SlotGap);
                if (i < squad.Count) MakeSlot(i, squad[i], x);
                else MakeLockedSlot(i, x);
            }
        }

        void MakeSlot(int i, UnitTypeId? type, float x)
        {
            var cfg = type.HasValue ? TypeOf(type.Value) : null;
            var b = UIFactory.Button(slotRow, "Slot_" + i, null, ButtonColor.White, SlotW, SlotH);
            ((RectTransform)b.transform).At(0.5f, 0.5f, x, 0f, SlotW, SlotH);
            RoundRect(b, 22f);
            UIFactory.ClearChildren(b.content);
            var face = b.face;
            if (cfg != null)
            {
                var d = GlyphDisc(face, "Glyph", cfg, 56f);
                d.At(0.5f, 1f, 0f, -7f, 56f, 56f, 0.5f, 1f);
            }
            else
            {
                var ring = UIFactory.Img(face, "EmptyRing", UIFactory.Skin != null ? UIFactory.Skin.ringThin : null, UIColors.CardRim);
                ((RectTransform)ring.transform).At(0.5f, 1f, 0f, -7f, 56f, 56f, 0.5f, 1f);
                var plus = UIFactory.Icon(face, UIIcon.Plus, 28f);
                plus.color = UIColors.CardRim;
                ((RectTransform)plus.transform).At(0.5f, 1f, 0f, -21f, 28f, 28f, 0.5f, 1f);
            }
            var label = UIFactory.Text(face, "Label", cfg != null ? cfg.displayName : "Empty", 21f, cfg != null ? TextStyle.HeadingDark : TextStyle.BodyMuted);
            ((RectTransform)label.transform).At(0.5f, 0f, 0f, 4f, SlotW - 10f, 30f, 0.5f, 0f);
            label.enableAutoSizing = true; label.fontSizeMin = 15f; label.fontSizeMax = 21f;
            int index = i;
            b.onClick = () => OnSlotClicked(index);
            slotButtons.Add(b);
            slotLabels.Add(label);
        }

        void MakeLockedSlot(int i, float x)
        {
            var root = UIFactory.Panel(slotRow, "LockedSlot_" + i, UIColors.WithAlpha(UIColors.CardLine, 0.7f), 22f);
            ((RectTransform)root.transform).At(0.5f, 0.5f, x, 0f, SlotW, SlotH);
            var ic = UIFactory.Icon(root.transform, UIIcon.Lock, 38f);
            ic.color = UIColors.WithAlpha(Color.white, 0.85f);
            ((RectTransform)ic.transform).At(0.5f, 1f, 0f, -16f, 38f, 38f, 0.5f, 1f);
            // Slot i (0 based) is earned by clearing Level i - 2.
            var t = UIFactory.Text(root.transform, "Level", "Level " + (i - Progression.StartSlots + 1), 20f, TextStyle.BodyMuted);
            ((RectTransform)t.transform).At(0.5f, 0f, 0f, 10f, SlotW - 8f, 28f, 0.5f, 0f);
        }

        void BuildCards(List<UnitTypeId?> squad, int cleared)
        {
            UIFactory.ClearChildren(cardRow);
            cards.Clear();
            var all = Progression.AllTypes;
            int n = all.Length;
            float cw = (width - (n - 1) * CardGap) / n, ch = height - CardsTop;
            var reference = TypeOf(UnitTypeId.Attacker);
            for (int i = 0; i < n; i++)
            {
                var id = all[i];
                int inSquad = 0;
                foreach (var s in squad) if (s == id) inSquad++;
                bool isNew = newUnits != null && newUnits.Contains(id);
                var card = UnitCard.Create(cardRow, id, TypeOf(id), reference, cw, ch, !Progression.IsUnlocked(id, cleared), isNew, inSquad, OnAddClicked);
                ((RectTransform)card.transform).At(0f, 1f, i * (cw + CardGap), 0f, cw, ch, 0f, 1f);
                cards.Add(card);
            }
        }

        // ------------------------------------------------------------------ input

        void OnSlotClicked(int i)
        {
            var squad = MetaGame.Squad;
            if (i < 0 || i >= squad.Count || !squad[i].HasValue) return;
            fullWarning = false;
            if (!MetaGame.ClearSquadSlot(i))
            {
                Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero);
                if (i < slotButtons.Count) UIAnim.Shake((RectTransform)slotButtons[i].transform, 8f, 0.25f);
                Refresh();
            }
            // A cleared slot commits, and the screen refreshes on MetaGame.Changed.
        }

        /// <summary>ADD on a card: the first empty slot, or the full-squad message.</summary>
        public void OnAddClicked(UnitTypeId id)
        {
            if (!Progression.IsUnlocked(id, MetaGame.LevelsCleared)) return;
            var squad = MetaGame.Squad;
            int empty = squad.FindIndex(s => !s.HasValue);
            if (empty >= 0)
            {
                fullWarning = false;
                MetaGame.SetSquadSlot(empty, id); // commits: the screen refreshes on MetaGame.Changed
                return;
            }
            fullWarning = true;
            Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero);
            Refresh();
            if (slotRow != null) UIAnim.Shake(slotRow, 6f, 0.2f);
            if (hintText != null) UIAnim.Punch((RectTransform)hintText.transform, 0.1f);
        }

        /// <summary>Clears the full-squad message (the Army screen calls it when it opens).</summary>
        public void ResetMessage() { fullWarning = false; }
    }
}
