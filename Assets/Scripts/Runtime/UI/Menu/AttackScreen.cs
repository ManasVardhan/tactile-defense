using System.Collections.Generic;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    public enum LadderState { Locked, Open, Cleared }

    /// <summary>The level ladder: the five AI bases (offline stand ins for player bases) as one vertical list in
    /// level order. Each entry shows "Level N", the base name and commander, the enemy faction color, what its first
    /// clear unlocks (Progression.RewardText), the estimated rewards and its state: locked (lock icon, no Fight
    /// button), open (gold rim, FIGHT!) or cleared (green check, REPLAY). The list scrolls to the open level. A strip
    /// at the bottom shows the squad that goes into battle and opens the Army screen to change it.</summary>
    public class AttackScreen : MenuScreen
    {
        public override string Title => "ATTACK";

        const float RowH = 132f, RowGap = 12f;

        class Entry
        {
            public LadderState state;
            public UIButton fight;
            public TextMeshProUGUI level, name, reward;
            public RectTransform root;
        }

        RectTransform content, squadRow;
        ScrollRect scroll;
        TextMeshProUGUI mercText;
        readonly List<UIButton> fightButtons = new List<UIButton>();
        readonly List<Entry> entries = new List<Entry>();
        /// <summary>One per level in order; null for a locked level (it has no Fight button).</summary>
        public IReadOnlyList<UIButton> FightButtons => fightButtons;
        public int EntryCount => entries.Count;
        public LadderState StateOf(int index) => entries[index].state;
        public string LevelLabel(int index) => entries[index].level.text;
        public string NameLabel(int index) => entries[index].name.text;
        /// <summary>What the level's first clear grants, empty when nothing.</summary>
        public string RewardLabel(int index) => entries[index].reward != null ? entries[index].reward.text : "";
        public RectTransform EntryRect(int index) => entries[index].root;

        protected override void Build()
        {
            var info = UIFactory.Text(Body, "Info", "Clear a level to open the next one. First clears unlock new units and squad slots.", 28f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)info.transform).At(0f, 1f, 76f, 2f, 1100f, 44f, 0f, 1f);
            var tag = UIFactory.Tag(Body, "OfflineTag", "OFFLINE: AI BASES", UIColors.Slate, 40f);
            tag.At(1f, 1f, -8f, 4f, tag.sizeDelta.x, 40f, 1f, 1f);

            scroll = UIFactory.VScroll(Body, "Ladder", RowGap, 8f, out content);
            ((RectTransform)scroll.transform).Stretch(0f, 54f, 0f, 92f);

            var strip = UIFactory.Panel(Body, "SquadStrip", UIColors.CardInner, 24f);
            var srt = ((RectTransform)strip.transform).At(0.5f, 0f, 0f, 0f, 1460f, 80f, 0.5f, 0f);
            var sl = UIFactory.Text(srt, "Label", "SQUAD", 28f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)sl.transform).At(0f, 0.5f, 26f, 0f, 120f, 40f, 0f, 0.5f);
            squadRow = UIFactory.Rect(srt, "Units").At(0f, 0.5f, 140f, 0f, 8 * 58f, 56f, 0f, 0.5f);
            var mi = UIFactory.Icon(srt, UIIcon.Mercenary, 54f);
            ((RectTransform)mi.transform).At(0f, 0.5f, 640f, 0f, 54f, 54f, 0f, 0.5f);
            mercText = UIFactory.Text(srt, "Merc", "", 26f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)mercText.transform).Stretch(706f, 0f, 290f, 2f);
            var change = UIFactory.Button(srt, "Change", "EDIT SQUAD", ButtonColor.Slate, 270f, 62f, UIIcon.Users, 30f);
            ((RectTransform)change.transform).At(1f, 0.5f, -10f, 2f, 270f, 62f, 1f, 0.5f);
            change.onClick = () => Menu.Open(Menu.Army);
        }

        public override void Refresh()
        {
            var db = MetaGame.Db;
            UIFactory.ClearChildren(content);
            fightButtons.Clear();
            entries.Clear();
            if (db == null || db.roster == null) return;
            int cleared = MetaGame.LevelsCleared;
            int n = Mathf.Min(Progression.LevelCount, db.roster.bases.Length);
            for (int i = 0; i < n; i++) BuildEntry(db.roster.bases[i], i, cleared);
            RefreshSquadStrip();
            ScrollTo(Mathf.Min(cleared, n - 1));
        }

        void RefreshSquadStrip()
        {
            UIFactory.ClearChildren(squadRow);
            var units = MetaGame.Db != null ? MetaGame.Db.units : null;
            int k = 0;
            foreach (var t in MetaGame.Squad)
            {
                if (!t.HasValue) continue;
                var d = SquadBuilderPanel.GlyphDisc(squadRow, "Unit_" + k, units != null ? units.Get(t.Value) : null, 50f);
                d.At(0f, 0.5f, k * 58f, 0f, 50f, 50f, 0f, 0.5f);
                k++;
            }
            var merc = MetaGame.HiredMercenary;
            mercText.text = merc != null
                ? "<b>" + merc.displayName + "</b> joins for " + merc.hireCost + " gold"
                : "No mercenary hired";
        }

        /// <summary>Scrolls so the entry sits in the middle of the list (after layout).</summary>
        void ScrollTo(int index)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float view = ((RectTransform)scroll.viewport).rect.height;
            float total = content.rect.height;
            if (total <= view + 1f) { scroll.verticalNormalizedPosition = 1f; return; }
            float top = 8f + index * (RowH + RowGap) - (view - RowH) * 0.5f;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / (total - view));
        }

        void BuildEntry(BaseDefinition b, int index, int cleared)
        {
            var e = new Entry();
            entries.Add(e);
            int level = index + 1;
            if (b == null) { fightButtons.Add(null); e.state = LadderState.Locked; return; }
            e.state = level <= cleared ? LadderState.Cleared : Progression.IsLevelOpen(level, cleared) ? LadderState.Open : LadderState.Locked;
            bool locked = e.state == LadderState.Locked;
            bool open = e.state == LadderState.Open;
            var skin = UIFactory.Skin;

            var root = UIFactory.Rect(content, "Level_" + level);
            root.sizeDelta = new Vector2(0f, RowH);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = RowH;
            e.root = root;
            RectTransform face;
            if (locked)
            {
                var flat = UIFactory.Panel(root, "Card", UIColors.WithAlpha(UIColors.CardLine, 0.75f), 26f);
                face = ((RectTransform)flat.transform).Stretch(6f, 2f, 6f, 6f);
            }
            else
            {
                face = UIFactory.Card(root, "Card", 100f, RowH, 26f, null, open ? UIColors.Gold : (Color?)null);
                ((RectTransform)face.parent).Stretch(6f, 2f, 6f, 4f);
            }

            // Level badge in the enemy faction's color.
            Color faction = UIColors.Team(b.enemyTeam);
            var badge = UIFactory.Img(face, "Badge", skin.circle, locked ? UIColors.GreyDark : faction);
            ((RectTransform)badge.transform).At(0f, 0.5f, 18f, 0f, 88f, 88f, 0f, 0.5f);
            var num = UIFactory.Text(badge.transform, "N", level.ToString(), 46f, TextStyle.HeadingThin);
            ((RectTransform)num.transform).Stretch(0f, 0f, 0f, 3f);
            if (locked)
            {
                var li = UIFactory.Img(face, "LockBadge", skin.circle, UIColors.Ink);
                ((RectTransform)li.transform).At(0f, 0.5f, 78f, -28f, 38f, 38f, 0f, 0.5f);
                var lic = UIFactory.Icon(li.transform, UIIcon.Lock, 26f);
                ((RectTransform)lic.transform).At(0.5f, 0.5f, 0f, 0f, 26f, 26f);
            }
            else if (!open)
            {
                var ci = UIFactory.Img(face, "CheckBadge", skin.circle, UIColors.Green);
                ((RectTransform)ci.transform).At(0f, 0.5f, 78f, -28f, 38f, 38f, 0f, 0.5f);
                var cic = UIFactory.Icon(ci.transform, UIIcon.Check, 28f);
                ((RectTransform)cic.transform).At(0.5f, 0.5f, 0f, 1f, 28f, 28f);
            }

            e.level = UIFactory.Text(face, "Level", "Level " + level, 24f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            e.level.color = locked ? UIColors.Muted : UIColors.TeamDark(b.enemyTeam);
            ((RectTransform)e.level.transform).At(0f, 1f, 128f, -8f, 200f, 28f, 0f, 1f);
            e.name = UIFactory.Text(face, "Name", b.displayName, 36f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            if (locked) e.name.color = UIColors.WithAlpha(UIColors.Ink, 0.6f);
            ((RectTransform)e.name.transform).At(0f, 1f, 128f, -34f, 440f, 40f, 0f, 1f);
            e.name.enableAutoSizing = true; e.name.fontSizeMin = 24f; e.name.fontSizeMax = 34f;
            var dot = UIFactory.Img(face, "Faction", skin.circle, locked ? UIColors.Grey : faction);
            ((RectTransform)dot.transform).At(0f, 0f, 130f, 12f, 20f, 20f, 0f, 0f);
            var cmd = UIFactory.Text(face, "Commander", b.commanderName + "  |  " + UIColors.TeamName(b.enemyTeam) + " faction", 21f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)cmd.transform).At(0f, 0f, 158f, 8f, 420f, 26f, 0f, 0f);

            // What the first clear grants.
            string reward = Progression.RewardText(level);
            if (reward.Length > 0)
            {
                bool done = e.state == LadderState.Cleared;
                var pill = UIFactory.Panel(face, "Reward", done ? UIColors.WithAlpha(UIColors.CardInner, 0.6f) : UIColors.CardInner, 22f);
                var prt = ((RectTransform)pill.transform).At(0f, 0.5f, 600f, 0f, 440f, 74f, 0f, 0.5f);
                var units = MetaGame.Db != null ? MetaGame.Db.units : null;
                if (Progression.UnlockOf(level, out var unlock))
                {
                    var d = SquadBuilderPanel.GlyphDisc(prt, "Unit", units != null ? units.Get(unlock) : null, 54f);
                    d.At(0f, 0.5f, 12f, 0f, 54f, 54f, 0f, 0.5f);
                }
                else
                {
                    var pi = UIFactory.Img(prt, "Slot", skin.circle, UIColors.Slate);
                    ((RectTransform)pi.transform).At(0f, 0.5f, 12f, 0f, 54f, 54f, 0f, 0.5f);
                    var pic = UIFactory.Icon(pi.transform, UIIcon.Plus, 34f);
                    ((RectTransform)pic.transform).At(0.5f, 0.5f, 0f, 0f, 34f, 34f);
                }
                var cap = UIFactory.Text(prt, "Caption", done ? "UNLOCKED" : "FIRST CLEAR", 17f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
                cap.color = done ? UIColors.GreenDark : UIColors.GoldDark;
                ((RectTransform)cap.transform).At(0f, 1f, 78f, -6f, 340f, 24f, 0f, 1f);
                e.reward = UIFactory.Text(prt, "Text", reward, 24f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
                ((RectTransform)e.reward.transform).At(0f, 0f, 78f, 8f, 352f, 34f, 0f, 0f);
                e.reward.enableAutoSizing = true; e.reward.fontSizeMin = 18f; e.reward.fontSizeMax = 24f;
            }

            // Estimated rewards.
            EstimateRewards(b, out int gold, out int xp);
            UIFactory.IconValue(face, "Gold", UIIcon.Coin, "~" + gold, 26f, TextStyle.HeadingDark, out var gr);
            gr.At(0f, 0.5f, 1120f, 20f, 130f, 40f, 0.5f, 0.5f);
            UIFactory.IconValue(face, "Xp", UIIcon.Xp, "~" + xp, 26f, TextStyle.HeadingDark, out var xr);
            xr.At(0f, 0.5f, 1120f, -22f, 130f, 40f, 0.5f, 0.5f);

            // State and the Fight button (only open and cleared levels have one).
            const float bw = 220f, bh = 80f;
            if (locked)
            {
                var lt = UIFactory.Text(face, "Locked", "LOCKED", 30f, TextStyle.HeadingDark, TextAlignmentOptions.Center);
                lt.color = UIColors.Muted;
                ((RectTransform)lt.transform).At(1f, 0.5f, -18f, 16f, bw, 36f, 1f, 0.5f);
                var lh = UIFactory.Text(face, "Hint", "Clear Level " + (level - 1), 22f, TextStyle.BodyMuted, TextAlignmentOptions.Center);
                ((RectTransform)lh.transform).At(1f, 0.5f, -18f, -20f, bw, 30f, 1f, 0.5f);
                fightButtons.Add(null);
                return;
            }
            var fight = UIFactory.Button(face, "Fight", open ? "FIGHT!" : "REPLAY", open ? ButtonColor.Green : ButtonColor.Slate, bw, bh, open ? UIIcon.Sword : UIIcon.Replay, 34f);
            ((RectTransform)fight.transform).At(1f, 0.5f, -18f, 0f, bw, bh, 1f, 0.5f);
            var def = b;
            fight.onClick = () => Menu.StartBattle(def);
            fightButtons.Add(fight);
        }

        /// <summary>Victory payout shown on the card, from the base's own counts: every enemy soldier (squads times
        /// the layout's average squad size) and every defense beaten, plus the victory bonus, before the grade.</summary>
        public static void EstimateRewards(BaseDefinition b, out int gold, out int xp)
        {
            var bc = MetaGame.Db != null ? MetaGame.Db.battleConfig : null;
            float kills = b.enemySquads * b.averageSquadSize;
            float g = bc != null ? bc.goldBase + bc.goldVictoryBonus + bc.goldPerKill * kills + bc.goldPerStructure * b.defenses : 300f;
            float x = bc != null ? bc.xpBase + bc.xpVictoryBonus + bc.xpPerKill * kills : 290f;
            gold = Mathf.RoundToInt(g * b.rewardMultiplier / 5f) * 5;
            xp = Mathf.RoundToInt(x * b.rewardMultiplier / 5f) * 5;
        }
    }
}
