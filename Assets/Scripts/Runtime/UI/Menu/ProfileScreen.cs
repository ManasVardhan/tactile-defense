using System.Collections.Generic;
using TactileWars.Meta;
using TactileWars.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Player card (name edit, faction, level, league), lifetime stats, and the SIMULATED offline
    /// leaderboard served by LocalOfflineProvider (clearly labeled; there is no real multiplayer).</summary>
    public class ProfileScreen : MenuScreen
    {
        public override string Title => "PROFILE";

        TMP_InputField nameInput;
        Image disc, bust, leagueIcon;
        TextMeshProUGUI teamText, levelText, leagueText, leagueNext;
        UIProgressBar xpBar, leagueBar;
        RectTransform statsGrid, rankList;
        UIButton exchange;
        readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>(20);

        protected override void Build()
        {
            var skin = UIFactory.Skin;
            // Column 1: identity.
            var c1 = UIFactory.Panel(Body, "Identity", UIColors.CardInner, 28f);
            var r1 = ((RectTransform)c1.transform).At(0f, 0.5f, 0f, 0f, 470f, 664f, 0f, 0.5f);
            disc = UIFactory.Img(r1, "Disc", skin.circle, Color.white);
            ((RectTransform)disc.transform).At(0.5f, 1f, 0f, -22f, 190f, 190f, 0.5f, 1f);
            bust = UIFactory.Img(r1, "Bust", null, Color.white);
            ((RectTransform)bust.transform).At(0.5f, 1f, 0f, -14f, 200f, 200f, 0.5f, 1f);
            nameInput = UIFactory.Input(r1, "Name", 400f, 74f, "Commander");
            ((RectTransform)nameInput.transform).At(0.5f, 1f, 0f, -228f, 400f, 74f, 0.5f, 1f);
            nameInput.onEndEdit.AddListener(v => { MetaGame.SetName(v); Menu.ShowToast("Name saved", UIIcon.Pencil); });
            teamText = UIFactory.Text(r1, "Team", "Red faction", 30f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)teamText.transform).At(0f, 1f, 36f, -318f, 220f, 60f, 0f, 1f);
            var change = UIFactory.Button(r1, "ChangeTeam", "CHANGE", ButtonColor.Slate, 180f, 66f, UIIcon.Flag, 28f);
            ((RectTransform)change.transform).At(1f, 1f, -32f, -314f, 180f, 66f, 1f, 1f);
            change.onClick = () => Menu.TeamSelect.Open(false);
            levelText = UIFactory.Text(r1, "Level", "Level 1", 30f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)levelText.transform).At(0f, 1f, 36f, -398f, 400f, 40f, 0f, 1f);
            xpBar = UIFactory.Bar(r1, "Xp", 400f, 30f, UIColors.Xp, UIColors.WithAlpha(UIColors.Ink, 0.25f));
            ((RectTransform)xpBar.transform).At(0.5f, 1f, 0f, -442f, 400f, 30f, 0.5f, 1f);
            leagueIcon = UIFactory.Icon(r1, UIIcon.Crown, 74f);
            ((RectTransform)leagueIcon.transform).At(0f, 1f, 30f, -490f, 74f, 74f, 0f, 1f);
            leagueText = UIFactory.Text(r1, "League", "Bronze League", 30f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)leagueText.transform).At(0f, 1f, 116f, -494f, 330f, 40f, 0f, 1f);
            leagueNext = UIFactory.Text(r1, "LeagueNext", "", 22f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)leagueNext.transform).At(0f, 1f, 116f, -532f, 330f, 30f, 0f, 1f);
            leagueBar = UIFactory.Bar(r1, "LeagueBar", 400f, 26f, UIColors.Gold, UIColors.WithAlpha(UIColors.Ink, 0.25f));
            ((RectTransform)leagueBar.transform).At(0.5f, 1f, 0f, -584f, 400f, 26f, 0.5f, 1f);

            // Column 2: stats.
            var sh = UIFactory.Text(Body, "StatsHead", "STATS", 38f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)sh.transform).At(0f, 1f, 500f, -4f, 300f, 48f, 0f, 1f);
            statsGrid = UIFactory.Rect(Body, "Stats").At(0f, 1f, 500f, -64f, 480f, 470f, 0f, 1f);
            var gl = statsGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(232f, 106f);
            gl.spacing = new Vector2(16f, 14f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            var ex = UIFactory.Panel(Body, "Exchange", UIColors.CardInner, 24f);
            var exr = ((RectTransform)ex.transform).At(0f, 0f, 500f, 0f, 480f, 120f, 0f, 0f);
            var exT = UIFactory.Paragraph(exr, "Text", "", 23f, UIColors.Ink, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)exT.transform).Stretch(22f, 8f, 210f, 8f);
            var prog = MetaGame.Progression;
            exT.text = prog != null ? "Trade " + prog.exchangePrisms + " prisms for " + prog.exchangeGold + " gold" : "Trade prisms for gold";
            exchange = UIFactory.Button(exr, "Trade", "TRADE", ButtonColor.Gold, 180f, 80f, UIIcon.Prism, 30f);
            ((RectTransform)exchange.transform).At(1f, 0.5f, -16f, 2f, 180f, 80f, 1f, 0.5f);
            exchange.sound = Audio.SoundId.UiPurchase;
            exchange.onClick = () =>
            {
                var r = MetaGame.ExchangePrisms();
                if (r == PurchaseResult.Ok) Menu.ShowToast("Prisms traded for gold", UIIcon.Coin, UIColors.GreenDark);
                else { Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero); Menu.ShowToast("Not enough prisms", UIIcon.Prism, UIColors.RedDark); }
            };

            // Column 3: simulated leaderboard.
            var c3 = UIFactory.Panel(Body, "Rankings", UIColors.Hex(0x2F3442), 28f);
            var r3 = ((RectTransform)c3.transform).At(1f, 0.5f, 0f, 0f, 520f, 664f, 1f, 0.5f);
            var rh = UIFactory.Text(r3, "Head", "RANKINGS", 36f, TextStyle.Heading, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)rh.transform).At(0f, 1f, 26f, -18f, 260f, 48f, 0f, 1f);
            var tag = UIFactory.Tag(r3, "Simulated", "SIMULATED OFFLINE", UIColors.Gold, 36f, 22f);
            tag.At(1f, 1f, -22f, -24f, tag.sizeDelta.x, 36f, 1f, 1f);
            var tt = tag.GetComponentInChildren<TextMeshProUGUI>();
            tt.fontSharedMaterial = skin.headingFont.material; tt.color = UIColors.Ink;
            rankList = UIFactory.Rect(r3, "List").At(0.5f, 1f, 0f, -80f, 480f, 470f, 0.5f, 1f);
            var vl = rankList.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6f;
            vl.childControlWidth = false; vl.childControlHeight = false;
            vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;
            var note = UIFactory.Paragraph(r3, "Note", "No servers in this build: the board mixes your league points with made up AI commanders. Alliances need an online service and are not available.", 19f, UIColors.Grey, TextAlignmentOptions.Bottom);
            ((RectTransform)note.transform).At(0.5f, 0f, 0f, 16f, 470f, 90f, 0.5f, 0f);
        }

        public override void Refresh()
        {
            var p = MetaGame.Profile;
            var prog = MetaGame.Progression;
            disc.color = UIColors.Team(p.team);
            bust.sprite = UIFactory.Skin.Bust(p.team);
            if (!nameInput.isFocused) nameInput.SetTextWithoutNotify(p.playerName);
            teamText.text = UIColors.TeamName(p.team) + " faction";
            teamText.color = UIColors.TeamDark(p.team);
            int into = 0, span = 0;
            float k = prog != null ? prog.LevelProgress(p.xp, out into, out span) : 0f;
            levelText.text = "Level " + p.level + (prog != null && span > 0 ? "   <size=22><color=#7D8190>" + into + " / " + span + " XP</color></size>" : "");
            xpBar.Set(k);
            var lg = MetaGame.CurrentLeague;
            leagueText.text = lg.name;
            leagueIcon.color = Color.Lerp(Color.white, lg.color, 0.75f);
            int li = MetaGame.LeagueIndex;
            if (prog != null && li + 1 < prog.leagues.Length)
            {
                int a = prog.leagues[li].minPoints, b = prog.leagues[li + 1].minPoints;
                leagueNext.text = p.leaguePoints + " pts, " + (b - p.leaguePoints) + " to " + prog.leagues[li + 1].name;
                leagueBar.Set(Mathf.InverseLerp(a, b, p.leaguePoints));
            }
            else { leagueNext.text = p.leaguePoints + " pts, top league"; leagueBar.Set(1f); }

            UIFactory.ClearChildren(statsGrid);
            var s = p.stats;
            Stat(UIIcon.Sword, s.battles.ToString(), "Battles");
            Stat(UIIcon.Trophy, s.victories.ToString(), "Victories");
            Stat(UIIcon.Skull, s.kills.ToString(), "Pops");
            Stat(UIIcon.Soldier, s.soldiersLost.ToString(), "Soldiers lost");
            Stat(UIIcon.Coin, s.goldEarned.ToString(), "Gold earned");
            Stat(UIIcon.Medal, string.IsNullOrEmpty(s.bestGrade) ? "-" : s.bestGrade, "Best grade");
            exchange.SetInteractable(prog == null || p.prisms >= prog.exchangePrisms);

            UIFactory.ClearChildren(rankList);
            LocalOfflineProvider.Instance.GetTopEntries(7, entries);
            foreach (var e in entries) RankRow(e);
        }

        void Stat(UIIcon icon, string value, string label)
        {
            var cell = UIFactory.Rect(statsGrid, label);
            var bg = UIFactory.Panel(cell, "Bg", UIColors.CardInner, 20f);
            ((RectTransform)bg.transform).Stretch();
            var ic = UIFactory.Icon(cell, icon, 58f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 14f, 0f, 58f, 58f, 0f, 0.5f);
            var v = UIFactory.Text(cell, "Value", value, 38f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)v.transform).At(0f, 0.5f, 86f, 14f, 140f, 46f, 0f, 0.5f);
            var l = UIFactory.Text(cell, "Label", label, 20f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)l.transform).At(0f, 0.5f, 86f, -22f, 140f, 28f, 0f, 0.5f);
        }

        void RankRow(LeaderboardEntry e)
        {
            var row = UIFactory.Rect(rankList, "Rank" + e.rank);
            row.sizeDelta = new Vector2(480f, 58f);
            var bg = UIFactory.Panel(row, "Bg", e.isLocalPlayer ? UIColors.Team(e.team) : UIColors.WithAlpha(Color.white, 0.07f), 16f);
            ((RectTransform)bg.transform).Stretch();
            var r = UIFactory.Text(row, "Rank", e.rank.ToString(), 30f, TextStyle.HeadingThin);
            ((RectTransform)r.transform).At(0f, 0.5f, 14f, 0f, 50f, 50f, 0f, 0.5f);
            var dot = UIFactory.Img(row, "Dot", UIFactory.Skin.Bust(e.team), Color.white);
            ((RectTransform)dot.transform).At(0f, 0.5f, 70f, 0f, 50f, 50f, 0f, 0.5f);
            var n = UIFactory.Text(row, "Name", e.isLocalPlayer ? e.name + " (you)" : e.name, 26f, TextStyle.BodyLight, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)n.transform).Stretch(130f, 0f, 110f, 2f);
            var pts = UIFactory.Text(row, "Pts", e.points.ToString(), 28f, TextStyle.HeadingThin, TextAlignmentOptions.MidlineRight);
            ((RectTransform)pts.transform).At(1f, 0.5f, -16f, 0f, 100f, 50f, 1f, 0.5f);
        }
    }
}
