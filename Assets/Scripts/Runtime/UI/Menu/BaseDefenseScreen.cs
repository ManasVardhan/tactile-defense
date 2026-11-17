using System;
using TactileWars.Battle;
using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Base hub: a summary of the player's base, EDIT BASE (loads BaseEditor.unity), DEFEND (a defense battle
    /// against a simulated AI attacker) and the log of SIMULATED offline raids. Falls back to an "under construction"
    /// card when the editor scene is not in the build.</summary>
    public class BaseDefenseScreen : MenuScreen
    {
        public const string EditorScene = "BaseEditor";
        public override string Title => "BASE DEFENSE";
        protected override Vector2 CardSize => new Vector2(1400f, 770f);

        RectTransform defenseRow, raidList;
        TextMeshProUGUI headText, statsText, rebuyText;
        public UIButton EditButton { get; private set; }
        public UIButton DefendButton { get; private set; }

        public static bool EditorAvailable => Application.CanStreamedLevelBeLoaded(EditorScene);

        protected override void Build()
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;
            var left = UIFactory.Panel(Body, "Art", UIColors.CardInner, 28f);
            var lrt = ((RectTransform)left.transform).At(0f, 0.5f, 0f, 0f, 500f, 630f, 0f, 0.5f);
            var glow = UIFactory.Img(lrt, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Team(team), 0.35f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, 60f, 520f, 520f);
            var hq = UIFactory.Img(lrt, "HQ", skin.Render("hq_" + team) ?? skin.Render("hq_Red"), Color.white);
            ((RectTransform)hq.transform).At(0.5f, 0.5f, 0f, 70f, 420f, 420f);
            defenseRow = UIFactory.Rect(lrt, "Defenses").At(0.5f, 0f, 0f, 22f, 470f, 120f, 0.5f, 0f);

            headText = UIFactory.Text(Body, "Head", "Your base", 46f, TextStyle.HeadingDark, TextAlignmentOptions.TopLeft);
            ((RectTransform)headText.transform).At(0f, 1f, 540f, -6f, 780f, 60f, 0f, 1f);
            statsText = UIFactory.Paragraph(Body, "Stats", "", 29f, UIColors.Muted);
            ((RectTransform)statsText.transform).At(0f, 1f, 540f, -70f, 780f, 120f, 0f, 1f);
            rebuyText = UIFactory.Text(Body, "Rebuy", "", 27f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            rebuyText.color = UIColors.RedDark;
            ((RectTransform)rebuyText.transform).At(0f, 1f, 540f, -182f, 780f, 36f, 0f, 1f);

            EditButton = UIFactory.Button(Body, "EditBase", "EDIT BASE", ButtonColor.Green, 380f, 104f, UIIcon.Pencil, 40f);
            ((RectTransform)EditButton.transform).At(0f, 1f, 540f, -232f, 380f, 104f, 0f, 1f);
            EditButton.sound = Audio.SoundId.UiOpen;
            EditButton.onClick = OpenEditor;
            DefendButton = UIFactory.Button(Body, "Defend", "DEFEND!", ButtonColor.Team, 380f, 104f, UIIcon.Shield, 40f);
            ((RectTransform)DefendButton.transform).At(0f, 1f, 940f, -232f, 380f, 104f, 0f, 1f);
            DefendButton.sound = Audio.SoundId.UiOpen;
            DefendButton.onClick = StartDefense;
            var cap = UIFactory.Text(Body, "DefendCaption", "An AI squad attacks your saved base", 22f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)cap.transform).At(0f, 1f, 944f, -344f, 380f, 30f, 0f, 1f);

            var rh = UIFactory.Text(Body, "RaidHead", "WHILE YOU WERE AWAY", 30f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)rh.transform).At(0f, 1f, 540f, -392f, 500f, 40f, 0f, 1f);
            var tag = UIFactory.Tag(Body, "SimTag", "SIMULATED OFFLINE RAIDS", UIColors.Slate, 36f, 20f);
            tag.At(0f, 1f, 900f, -394f, tag.sizeDelta.x, 36f, 0f, 1f);
            raidList = UIFactory.Rect(Body, "Raids").At(0f, 1f, 540f, -440f, 780f, 200f, 0f, 1f);
        }

        void OpenEditor()
        {
            if (!EditorAvailable) { Menu.ShowToast("The base editor is not in this build", UIIcon.Lock, UIColors.RedDark); return; }
            ScreenTransition.Load(EditorScene, MetaGame.Profile.team);
        }

        void StartDefense()
        {
            if (ScreenTransition.Busy) return;
            if (MetaGame.Profile.BaseLayout.placements.Count == 0)
            {
                Menu.ShowToast("Place some defenses in your base first", UIIcon.Shield, UIColors.RedDark);
                return;
            }
            BattleLaunch.PrepareDefense(false);
            if (Menu.backdrop != null) Menu.backdrop.CheerAll();
            ScreenTransition.Load(BattleLaunch.BattleScene, MetaGame.Profile.team);
        }

        public override void Refresh()
        {
            var p = MetaGame.Profile;
            var layout = p.BaseLayout;
            var cat = MetaGame.Defenses;
            int budget = 0, space = 0;
            foreach (var d in layout.placements) if (cat != null && cat.TryGet(d.defenseId, out var e)) { budget += e.budget; space += e.space; }
            headText.text = p.playerName + "'s base";
            int n = layout.placements.Count;
            statsText.text = (n == 0 ? "No defenses yet. Open the editor and place mines and turrets on your lane." : n + (n == 1 ? " defense placed" : " defenses placed")) +
                "\nBudget " + budget + " / " + MetaGame.BudgetCap + "   Space " + space + " / " + MetaGame.SpaceCap +
                "\nDefenses held " + p.defenseStats.battlesHeld + "   raids held " + p.defenseStats.raidsHeld;
            int wrecks = layout.destroyed.Count;
            rebuyText.text = wrecks > 0 ? wrecks + (wrecks == 1 ? " defense was" : " defenses were") + " destroyed: rebuy in the editor" : "";
            EditButton.SetInteractable(EditorAvailable);
            DefendButton.SetInteractable(n > 0);

            // Defense type icons (unlocked ones bright).
            UIFactory.ClearChildren(defenseRow);
            var skin = UIFactory.Skin;
            if (cat != null)
            {
                int i = 0, total = 0;
                foreach (var e in cat.entries) if (e.id != DefenseCatalog.SquadId) total++;
                float cell = Mathf.Min(64f, 470f / Mathf.Max(1, total));
                foreach (var e in cat.entries)
                {
                    if (e.id == DefenseCatalog.SquadId) continue;
                    bool on = MetaGame.DefenseLevel(e.id) > 0;
                    var img = UIFactory.Img(defenseRow, e.id, skin.Defense(e.id, p.team), on ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.5f));
                    ((RectTransform)img.transform).At(0f, 0.5f, i * cell, 0f, cell, cell, 0f, 0.5f);
                    i++;
                }
            }

            // Raid log (simulated).
            UIFactory.ClearChildren(raidList);
            if (p.raidLog.Count == 0)
            {
                var t = UIFactory.Text(raidList, "None", "No raids yet. Simulated attackers visit while you are away.", 26f, TextStyle.BodyMuted, TextAlignmentOptions.TopLeft);
                ((RectTransform)t.transform).At(0f, 1f, 4f, 0f, 760f, 34f, 0f, 1f);
                return;
            }
            for (int k = 0; k < p.raidLog.Count && k < 3; k++)
            {
                var r = p.raidLog[k];
                var row = UIFactory.Panel(raidList, "Raid" + k, UIColors.CardInner, 18f);
                var rr = ((RectTransform)row.transform).At(0f, 1f, 0f, -k * 64f, 780f, 56f, 0f, 1f);
                var ic = UIFactory.Icon(rr, r.held ? UIIcon.Shield : UIIcon.Skull, 42f);
                ((RectTransform)ic.transform).At(0f, 0.5f, 10f, 0f, 42f, 42f, 0f, 0.5f);
                string when = Ago(r.utcTicks);
                var t = UIFactory.Text(rr, "Text", (r.held ? "Held vs " : "Fell to ") + r.attackerName + "  " + (r.goldDelta >= 0 ? "+" : "") + r.goldDelta + " gold, " + r.consumed.Count + " lost  (" + when + ")", 24f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
                ((RectTransform)t.transform).Stretch(62f, 0f, 10f, 2f);
            }
        }

        static string Ago(long ticks)
        {
            if (ticks <= 0) return "earlier";
            var span = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (span.TotalMinutes < 2) return "just now";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " min ago";
            if (span.TotalDays < 1) return (int)span.TotalHours + " h ago";
            return (int)span.TotalDays + " d ago";
        }
    }
}
