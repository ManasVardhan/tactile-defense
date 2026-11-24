using System.Collections.Generic;
using TactileWars.Army;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// Attack battles: one button per unit type in the squad along the bottom of the HUD. Each shows the type's
    /// glyph on its accent color, its name and "xN", N being how many of that type are ready to deploy. A click arms
    /// the first ready entry of that type (or disarms when that type is armed); the armed button turns gold and
    /// stands a little taller; a type with nothing ready is greyed and does nothing. Buttons are raycast targets on
    /// the HUD canvas, so presses on them never reach the map (ArmyInput skips presses over UI).
    /// </summary>
    public class UnitBar : MonoBehaviour
    {
        const float ButtonH = 104f, MaxButtonW = 330f, Spacing = 18f;
        /// <summary>More buttons than this: each shows only the glyph disc and the count (the glyph and its accent
        /// are the unit's sign on every screen), so up to eight fit the bar at 1280x720.</summary>
        public const int CompactAbove = 5;
        const float CompactW = 176f, BarWidth = 1800f;

        class Slot
        {
            public UnitTypeId type;
            public UIButton button;
            public RectTransform rt;
            public Image disc;
            public TextMeshProUGUI glyph, name, count;
            public int ready = -1;
            public bool armed;
            public Color accent;
        }

        PlayerArmy army;
        ArmyInput input;
        RectTransform row;
        readonly List<Slot> slots = new List<Slot>(6);
        readonly List<UIButton> buttons = new List<UIButton>(6);
        readonly List<UnitTypeId> typeScratch = new List<UnitTypeId>(8);

        public IReadOnlyList<UIButton> Buttons => buttons;
        /// <summary>The type whose button shows armed, null when nothing is armed.</summary>
        public UnitTypeId? HighlightedType { get; private set; }

        /// <summary>True when the buttons show glyph and count only (more than CompactAbove types).</summary>
        public bool Compact { get; private set; }

        public static UnitBar Create(RectTransform parent, PlayerArmy army, ArmyInput input)
        {
            var rt = UIFactory.Rect(parent, "UnitBar").At(0.5f, 0f, 0f, 18f, BarWidth, ButtonH + 24f, 0.5f, 0f);
            var bar = rt.gameObject.AddComponent<UnitBar>();
            bar.army = army;
            bar.input = input;
            bar.row = rt;
            if (army != null) army.SquadChanged += bar.Refresh;
            bar.Rebuild();
            return bar;
        }

        void OnDestroy() { if (army != null) army.SquadChanged -= Refresh; }

        public UIButton ButtonFor(UnitTypeId id)
        {
            var s = SlotFor(id);
            return s != null ? s.button : null;
        }

        public string CountText(UnitTypeId id) { var s = SlotFor(id); return s != null ? s.count.text : null; }
        public string NameText(UnitTypeId id) { var s = SlotFor(id); return s != null ? s.name.text : null; }
        public string GlyphText(UnitTypeId id) { var s = SlotFor(id); return s != null ? s.glyph.text : null; }
        /// <summary>The accent color of the type's button disc (the type's UnitTypeConfig.accent).</summary>
        public Color AccentOf(UnitTypeId id) { var s = SlotFor(id); return s != null ? s.accent : Color.clear; }

        Slot SlotFor(UnitTypeId id)
        {
            for (int i = 0; i < slots.Count; i++) if (slots[i].type == id) return slots[i];
            return null;
        }

        /// <summary>Types in the squad, in order of first appearance.</summary>
        void SquadTypes(List<UnitTypeId> into)
        {
            into.Clear();
            if (army == null) return;
            var sq = army.Squad;
            for (int i = 0; i < sq.Count; i++) if (!into.Contains(sq[i].type)) into.Add(sq[i].type);
        }

        void Rebuild()
        {
            UIFactory.ClearChildren(row);
            slots.Clear();
            buttons.Clear();
            SquadTypes(typeScratch);
            int n = typeScratch.Count;
            if (n == 0) return;
            Compact = n > CompactAbove;
            float w = Compact ? Mathf.Min(CompactW, (BarWidth - (n - 1) * Spacing) / n) : Mathf.Min(MaxButtonW, (BarWidth - (n - 1) * Spacing) / n);
            float x0 = -(n * w + (n - 1) * Spacing) * 0.5f + w * 0.5f;
            var backing = UIFactory.Img(row, "Backing", UIFactory.Skin != null ? UIFactory.Skin.pill : null,
                UIColors.WithAlpha(UIColors.SlateDark, 0.55f), UIFactory.PillMult(ButtonH + 24f));
            ((RectTransform)backing.transform).At(0.5f, 0f, 0f, 0f, n * w + (n - 1) * Spacing + 24f, ButtonH + 24f, 0.5f, 0f);
            for (int i = 0; i < n; i++) slots.Add(MakeSlot(typeScratch[i], x0 + i * (w + Spacing), w));
            Refresh();
        }

        Slot MakeSlot(UnitTypeId id, float x, float w)
        {
            var cfg = army.TypeConfig(id);
            var s = new Slot { type = id, accent = cfg != null ? cfg.accent : Color.white };
            s.button = UIFactory.Button(row, "Unit_" + id, null, ButtonColor.Slate, w, ButtonH);
            s.rt = (RectTransform)s.button.transform;
            s.rt.At(0.5f, 0f, x, 12f, w, ButtonH, 0.5f, 0f);
            var content = s.button.content;
            var hl = content.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;

            var discRt = UIFactory.Rect(content, "Glyph");
            var le = discRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 66f;
            // The hired mercenary's disc has a gold rim: a hired hand, not a squad unit.
            var rim = UIFactory.Img(discRt, "Rim", UIFactory.Skin != null ? UIFactory.Skin.circle : null, id == PlayerArmy.MercenaryTypeId ? UIColors.Gold : Color.white);
            ((RectTransform)rim.transform).Stretch();
            s.disc = UIFactory.Img(discRt, "Disc", UIFactory.Skin != null ? UIFactory.Skin.circle : null, s.accent);
            ((RectTransform)s.disc.transform).Stretch(4f, 4f, 4f, 4f);
            s.glyph = UIFactory.Text(discRt, "Letter", cfg != null ? cfg.glyph : "?", 40f, TextStyle.HeadingThin);
            ((RectTransform)s.glyph.transform).Stretch(0f, 0f, 0f, 3f);

            s.name = UIFactory.Text(content, "Name", cfg != null ? cfg.displayName : id.ToString(), 34f, TextStyle.Heading);
            if (Compact) s.name.gameObject.SetActive(false);
            s.count = UIFactory.Text(content, "Count", "x0", 38f, TextStyle.Heading);
            s.count.color = UIColors.Gold;

            var type = id;
            s.button.onClick = () => OnClick(type);
            buttons.Add(s.button);
            return s;
        }

        void OnClick(UnitTypeId id)
        {
            if (input == null || army == null) return;
            int armed = input.ArmedEntry;
            var sq = army.Squad;
            if (armed >= 0 && armed < sq.Count && sq[armed].type == id) { input.Disarm(); Sync(); return; }
            for (int i = 0; i < sq.Count; i++)
                if (sq[i].type == id && sq[i].state == EntryState.Ready) { input.Arm(i); break; }
            Sync();
        }

        /// <summary>Counts and enabled state; called on SquadChanged (and rebuilds when the set of types changed).</summary>
        public void Refresh()
        {
            if (army == null) return;
            SquadTypes(typeScratch);
            bool same = typeScratch.Count == slots.Count;
            for (int i = 0; same && i < slots.Count; i++) same = slots[i].type == typeScratch[i];
            if (!same) { Rebuild(); return; }
            var sq = army.Squad;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int ready = 0;
                for (int k = 0; k < sq.Count; k++) if (sq[k].type == s.type && sq[k].state == EntryState.Ready) ready++;
                if (ready == s.ready) continue;
                bool wasZero = s.ready == 0;
                s.ready = ready;
                s.count.SetText("x{0}", ready);
                s.button.SetInteractable(ready > 0);
                Look(s);
                if (wasZero && ready > 0) UIAnim.Pop(s.rt, 0.7f, 0.35f);
            }
            Sync();
        }

        /// <summary>Highlights the armed type's button (ArmyInput has no change event, so this polls).</summary>
        void Sync()
        {
            UnitTypeId? armedType = null;
            if (input != null && army != null && input.ArmedEntry >= 0 && input.ArmedEntry < army.Squad.Count)
                armedType = army.Squad[input.ArmedEntry].type;
            HighlightedType = armedType;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                bool on = armedType.HasValue && armedType.Value == s.type;
                if (on == s.armed) continue;
                s.armed = on;
                s.button.SetColor(on ? ButtonColor.Gold : ButtonColor.Slate);
                s.rt.anchoredPosition = new Vector2(s.rt.anchoredPosition.x, on ? 24f : 12f);
                Look(s);
            }
        }

        static void Look(Slot s)
        {
            bool ready = s.ready > 0;
            s.disc.color = ready ? s.accent : UIColors.WithAlpha(UIColors.Grey, 0.9f);
            s.count.color = !ready ? UIColors.WithAlpha(Color.white, 0.75f) : (s.armed ? Color.white : UIColors.Gold);
        }

        void Update() { Sync(); }
    }
}
