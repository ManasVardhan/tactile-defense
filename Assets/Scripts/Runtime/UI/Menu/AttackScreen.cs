using System.Collections.Generic;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Opponent list: AI bases (offline stand ins for player bases) as cards with faction, difficulty,
    /// estimated rewards, lock state and a Fight button.</summary>
    public class AttackScreen : MenuScreen
    {
        public override string Title => "ATTACK";

        RectTransform content;
        ScrollRect scroll;
        TextMeshProUGUI mercText;
        readonly List<UIButton> fightButtons = new List<UIButton>();
        public IReadOnlyList<UIButton> FightButtons => fightButtons;

        protected override void Build()
        {
            var info = UIFactory.Text(Body, "Info", "Pick a base to attack. Tougher commanders pay more gold and XP.", 30f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)info.transform).At(0f, 1f, 76f, 2f, 1000f, 44f, 0f, 1f);
            var tag = UIFactory.Tag(Body, "OfflineTag", "OFFLINE: AI BASES", UIColors.Slate, 40f);
            tag.At(1f, 1f, -8f, 4f, tag.sizeDelta.x, 40f, 1f, 1f);

            scroll = UIFactory.HScroll(Body, "List", 26f, 12f, out content);
            ((RectTransform)scroll.transform).Stretch(-10f, 50f, -10f, 88f);

            var strip = UIFactory.Panel(Body, "MercStrip", UIColors.CardInner, 24f);
            ((RectTransform)strip.transform).At(0.5f, 0f, 0f, 0f, 1180f, 78f, 0.5f, 0f);
            var mi = UIFactory.Icon(strip.transform, UIIcon.Mercenary, 56f);
            ((RectTransform)mi.transform).At(0f, 0.5f, 20f, 0f, 56f, 56f, 0f, 0.5f);
            mercText = UIFactory.Text(strip.transform, "Text", "", 28f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)mercText.transform).Stretch(92f, 0f, 260f, 2f);
            var change = UIFactory.Button(strip.transform, "Change", "ARMY", ButtonColor.Slate, 220f, 62f, UIIcon.Users, 30f);
            ((RectTransform)change.transform).At(1f, 0.5f, -10f, 2f, 220f, 62f, 1f, 0.5f);
            change.onClick = () => Menu.Open(Menu.Army);
        }

        public override void Refresh()
        {
            var db = MetaGame.Db;
            UIFactory.ClearChildren(content);
            fightButtons.Clear();
            if (db == null || db.roster == null) return;
            for (int i = 0; i < db.roster.bases.Length; i++) BuildCard(db.roster.bases[i], i);
            var merc = MetaGame.HiredMercenary;
            mercText.text = merc != null
                ? "Mercenary: <b>" + merc.displayName + "</b> joins for " + merc.hireCost + " gold per battle"
                : "No mercenary hired. Hire one in the Army screen.";
            scroll.horizontalNormalizedPosition = 0f;
        }

        void BuildCard(BaseDefinition b, int index)
        {
            if (b == null) { fightButtons.Add(null); return; }
            var skin = UIFactory.Skin;
            bool unlocked = MetaGame.IsBaseUnlocked(b);
            bool beaten = MetaGame.IsBaseDefeated(b);
            const float w = 300f, h = 500f;
            var root = UIFactory.Rect(content, "Base_" + b.id);
            root.sizeDelta = new Vector2(w, h);
            var face = UIFactory.Card(root, "Card", w, h, 30f);
            ((RectTransform)face.parent).Stretch();

            var head = UIFactory.Panel(face, "Header", unlocked ? UIColors.Team(b.enemyTeam) : UIColors.GreyDark, 24f);
            ((RectTransform)head.transform).At(0.5f, 1f, 0f, -10f, w - 30f, 142f, 0.5f, 1f);
            head.gameObject.AddComponent<RectMask2D>();
            var glow = UIFactory.Img(head.transform, "Glow", skin.glow, UIColors.WithAlpha(Color.white, 0.4f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, -6f, 240f, 240f);
            var bust = UIFactory.Img(head.transform, "Bust", skin.Bust(b.enemyTeam), unlocked ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f));
            ((RectTransform)bust.transform).At(0.5f, 0f, 0f, -4f, 140f, 140f, 0.5f, 0f);
            var num = UIFactory.Img(head.transform, "Num", skin.circle, UIColors.WithAlpha(UIColors.Ink, 0.8f));
            ((RectTransform)num.transform).At(0f, 1f, 12f, -12f, 52f, 52f, 0f, 1f);
            var numT = UIFactory.Text(num.transform, "N", (index + 1).ToString(), 30f, TextStyle.HeadingThin);
            ((RectTransform)numT.transform).Stretch(0f, 0f, 0f, 2f);

            var name = UIFactory.Text(face, "Name", b.displayName, 34f, TextStyle.HeadingDark);
            ((RectTransform)name.transform).At(0.5f, 1f, 0f, -158f, w - 34f, 44f, 0.5f, 1f);
            name.enableAutoSizing = true; name.fontSizeMin = 24f; name.fontSizeMax = 34f;
            var cmd = UIFactory.Text(face, "Commander", b.commanderName + "  |  " + UIColors.TeamName(b.enemyTeam), 22f, TextStyle.BodyMuted);
            ((RectTransform)cmd.transform).At(0.5f, 1f, 0f, -200f, w - 30f, 30f, 0.5f, 1f);

            // Difficulty: five skulls, half a skull per difficulty point.
            var diff = UIFactory.Rect(face, "Difficulty").At(0.5f, 1f, 0f, -234f, 240f, 40f, 0.5f, 1f);
            int skulls = Mathf.CeilToInt(b.difficulty / 2f);
            for (int s = 0; s < 5; s++)
            {
                var ic = UIFactory.Icon(diff, UIIcon.Skull, 38f);
                ((RectTransform)ic.transform).At(0.5f, 0.5f, (s - 2) * 44f, 0f, 38f, 38f);
                ic.color = s < skulls ? Color.white : new Color(1f, 1f, 1f, 0.22f);
            }

            var rewards = UIFactory.Panel(face, "Rewards", UIColors.CardInner, 18f);
            ((RectTransform)rewards.transform).At(0.5f, 1f, 0f, -278f, w - 40f, 54f, 0.5f, 1f);
            EstimateRewards(b, out int gold, out int xp);
            UIFactory.IconValue(rewards.transform, "Gold", UIIcon.Coin, "~" + gold, 28f, TextStyle.HeadingDark, out var gr);
            gr.At(0.27f, 0.5f, 0f, 0f, 120f, 50f, 0.5f, 0.5f);
            UIFactory.IconValue(rewards.transform, "Xp", UIIcon.Xp, "~" + xp, 28f, TextStyle.HeadingDark, out var xr);
            xr.At(0.73f, 0.5f, 0f, 0f, 120f, 50f, 0.5f, 0.5f);

            var fight = UIFactory.Button(face, "Fight", unlocked ? "FIGHT!" : "LOCKED", unlocked ? ButtonColor.Green : ButtonColor.Grey, w - 56f, 82f, unlocked ? UIIcon.Sword : UIIcon.Lock, 38f);
            ((RectTransform)fight.transform).At(0.5f, 0f, 0f, 16f, w - 56f, 82f, 0.5f, 0f);
            fight.SetInteractable(unlocked);
            var def = b;
            fight.onClick = () => Menu.StartBattle(def);
            fightButtons.Add(fight);

            if (!unlocked)
            {
                var lockTag = UIFactory.Tag(face, "LockTag", "REACH LEVEL " + b.requiredLevel, UIColors.Ink, 40f, 24f);
                lockTag.At(0.5f, 0f, 0f, 124f, lockTag.sizeDelta.x, 40f, 0.5f, 0.5f);
            }
            else if (beaten)
            {
                var badge = UIFactory.Img(face, "Beaten", skin.circle, UIColors.Green);
                ((RectTransform)badge.transform).At(1f, 1f, -14f, -16f, 58f, 58f, 1f, 1f);
                var ci = UIFactory.Icon(badge.transform, UIIcon.Check, 42f);
                ((RectTransform)ci.transform).At(0.5f, 0.5f, 0f, 2f, 42f, 42f);
                var bt = UIFactory.Tag(face, "BeatenTag", "BEATEN", UIColors.GreenDark, 36f, 22f);
                bt.At(0.5f, 0f, 0f, 124f, bt.sizeDelta.x, 36f, 0.5f, 0.5f);
            }
            else
            {
                var pts = UIFactory.Tag(face, "League", "+" + LeaguePoints(b) + " LEAGUE PTS", UIColors.Slate, 36f, 22f);
                pts.At(0.5f, 0f, 0f, 124f, pts.sizeDelta.x, 36f, 0.5f, 0.5f);
            }
        }

        static int LeaguePoints(BaseDefinition b)
        {
            var prog = MetaGame.Progression;
            return prog != null ? prog.leaguePointsPerWin + prog.leaguePointsPerDifficulty * b.difficulty : 20;
        }

        /// <summary>Rough victory payout shown on the card (estimate: about 40 kills and 4 structures).</summary>
        public static void EstimateRewards(BaseDefinition b, out int gold, out int xp)
        {
            var bc = MetaGame.Db != null ? MetaGame.Db.battleConfig : null;
            float g = bc != null ? bc.goldBase + bc.goldVictoryBonus + bc.goldPerKill * 40 + bc.goldPerStructure * 4 : 300f;
            float x = bc != null ? bc.xpBase + bc.xpVictoryBonus + bc.xpPerKill * 40 : 290f;
            gold = Mathf.RoundToInt(g * b.rewardMultiplier / 5f) * 5;
            xp = Mathf.RoundToInt(x * b.rewardMultiplier / 5f) * 5;
        }
    }
}
