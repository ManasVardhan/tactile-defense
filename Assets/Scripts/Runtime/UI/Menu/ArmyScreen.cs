using System.Collections.Generic;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Squad stats (as research has upgraded them) and the mercenary roster: unlock, hire, dismiss.</summary>
    public class ArmyScreen : MenuScreen
    {
        public override string Title => "ARMY";

        RectTransform statsList, mercRow;
        Image soldierImg, disc;
        readonly List<UIButton> mercButtons = new List<UIButton>();
        public IReadOnlyList<UIButton> MercenaryButtons => mercButtons;

        protected override void Build()
        {
            var left = UIFactory.Panel(Body, "Squad", UIColors.CardInner, 28f);
            var lrt = ((RectTransform)left.transform).At(0f, 0.5f, 0f, 0f, 700f, 660f, 0f, 0.5f);
            var head = UIFactory.Text(lrt, "Head", "YOUR SQUAD", 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)head.transform).At(0f, 1f, 30f, -20f, 400f, 50f, 0f, 1f);
            disc = UIFactory.Img(lrt, "Disc", UIFactory.Skin.circle, UIColors.Team(UIFactory.PlayerTeam));
            ((RectTransform)disc.transform).At(0f, 0.5f, 18f, -10f, 232f, 232f, 0f, 0.5f);
            soldierImg = UIFactory.Img(lrt, "Soldier", UIFactory.Skin.Soldier(UIFactory.PlayerTeam), Color.white);
            ((RectTransform)soldierImg.transform).At(0f, 0.5f, 10f, 8f, 248f, 290f, 0f, 0.5f);
            statsList = UIFactory.Rect(lrt, "Stats").At(1f, 0.5f, -18f, -10f, 424f, 560f, 1f, 0.5f);
            var vl = statsList.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8f;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = false; vl.childControlHeight = false;
            vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;

            var mh = UIFactory.Text(Body, "MercHead", "MERCENARIES", 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)mh.transform).At(0f, 1f, 740f, -2f, 400f, 50f, 0f, 1f);
            var sub = UIFactory.Text(Body, "MercSub", "Contract once, then pay a fee each battle they join.", 24f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)sub.transform).At(0f, 1f, 740f, -50f, 780f, 34f, 0f, 1f);
            mercRow = UIFactory.Rect(Body, "Mercs").At(1f, 0f, 0f, 0f, 788f, 570f, 1f, 0f);
        }

        public override void Refresh()
        {
            var db = MetaGame.Db;
            var p = MetaGame.Profile;
            disc.color = UIColors.Team(p.team);
            soldierImg.sprite = UIFactory.Skin.Soldier(p.team);
            UIFactory.ClearChildren(statsList);
            var lo = LoadoutBuilder.Build(p, db, null, true);
            var unit = db != null ? db.playerSoldier : null;
            var w = unit != null ? unit.weapon : null;
            var bc = db != null ? db.battleConfig : null;
            int baseSquad = db != null && db.squadConfig != null ? db.squadConfig.soldierCount : 40;
            Row(UIIcon.Users, "Soldiers", lo.squadSize.ToString(), lo.squadSize - baseSquad > 0 ? "+" + (lo.squadSize - baseSquad) : null);
            Row(UIIcon.Heart, "Health", unit != null ? Mathf.RoundToInt(unit.maxHealth * lo.soldierHealthMult).ToString() : "-", Pct(lo.soldierHealthMult));
            Row(UIIcon.Target, "Paint damage", w != null ? (w.damage * lo.damageMult).ToString("0.0") : "-", Pct(lo.damageMult));
            Row(UIIcon.Lightning, "Shots per sec", w != null ? (w.burstCount / w.fireInterval * lo.fireRateMult).ToString("0.0") : "-", Pct(lo.fireRateMult));
            Row(UIIcon.Boot, "Move speed", unit != null ? (unit.maxSpeed * lo.moveSpeedMult).ToString("0.0") : "-", Pct(lo.moveSpeedMult));
            int baseRe = bc != null ? bc.reinforcementCount : 10;
            Row(UIIcon.Plus, "Backup per call", lo.reinforcementCount.ToString(), lo.reinforcementCount > baseRe ? "+" + (lo.reinforcementCount - baseRe) : null);
            float need = bc != null ? bc.meterRequired / Mathf.Max(0.1f, lo.reinforcementChargeMult) / Mathf.Max(0.1f, bc.perSoldierKill) : 30f;
            Row(UIIcon.Pow, "Kills to call", Mathf.CeilToInt(need).ToString(), lo.reinforcementChargeMult > 1.001f ? "-" + Mathf.RoundToInt((1f - 1f / lo.reinforcementChargeMult) * 100f) + "%" : null);

            UIFactory.ClearChildren(mercRow);
            mercButtons.Clear();
            if (db == null) return;
            for (int i = 0; i < db.mercenaries.Length; i++) MercCard(db.mercenaries[i], i);
        }

        static string Pct(float mult) => mult > 1.001f ? "+" + Mathf.RoundToInt((mult - 1f) * 100f) + "%" : null;

        void Row(UIIcon icon, string label, string value, string bonus)
        {
            var row = UIFactory.Rect(statsList, "Row_" + label);
            row.sizeDelta = new Vector2(424f, 70f);
            var bg = UIFactory.Panel(row, "Bg", UIColors.Card, 18f);
            ((RectTransform)bg.transform).Stretch();
            var ic = UIFactory.Icon(row, icon, 50f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 12f, 0f, 50f, 50f, 0f, 0.5f);
            var t = UIFactory.Text(row, "Label", label, 25f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)t.transform).Stretch(72f, 0f, 160f, 2f);
            var v = UIFactory.Text(row, "Value", value, 34f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineRight);
            ((RectTransform)v.transform).Stretch(150f, 0f, 16f, 2f);
            if (bonus != null)
            {
                var tag = UIFactory.Tag(row, "Bonus", bonus, UIColors.Green, 30f, 20f);
                tag.At(1f, 0.5f, -96f, 0f, tag.sizeDelta.x, 30f, 1f, 0.5f);
            }
        }

        void MercCard(MercenaryDef m, int i)
        {
            if (m == null) return;
            var skin = UIFactory.Skin;
            var p = MetaGame.Profile;
            const float w = 250f, h = 570f;
            var root = UIFactory.Rect(mercRow, "Merc_" + m.id).At(0f, 0f, i * (w + 19f), 0f, w, h, 0f, 0f);
            bool owned = p.OwnsMercenary(m.id);
            bool hired = owned && p.hiredMercenary == m.id;
            bool locked = p.level < m.unlockLevel;
            var face = UIFactory.Card(root, "Card", w, h, 28f, null, hired ? UIColors.Green : (Color?)null);
            ((RectTransform)face.parent).Stretch();
            var top = UIFactory.Panel(face, "Top", locked ? UIColors.GreyDark : m.accent, 22f);
            ((RectTransform)top.transform).At(0.5f, 1f, 0f, -10f, w - 26f, 170f, 0.5f, 1f);
            var glow = UIFactory.Img(top.transform, "Glow", skin.glow, UIColors.WithAlpha(Color.white, 0.4f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, 0f, 220f, 220f);
            var hero = UIFactory.Img(top.transform, "Hero", skin.Hero(p.team), locked ? new Color(0.5f, 0.5f, 0.55f) : Color.white);
            ((RectTransform)hero.transform).At(0.5f, 0.5f, 0f, -2f, 166f, 166f);
            var name = UIFactory.Text(face, "Name", m.displayName.ToUpperInvariant(), 36f, TextStyle.HeadingDark);
            ((RectTransform)name.transform).At(0.5f, 1f, 0f, -188f, w - 30f, 44f, 0.5f, 1f);
            var ab = UIFactory.Text(face, "Ability", m.ability, 22f, TextStyle.Body);
            ab.color = UIColors.GoldDark;
            ((RectTransform)ab.transform).At(0.5f, 1f, 0f, -230f, w - 30f, 30f, 0.5f, 1f);
            var desc = UIFactory.Paragraph(face, "Desc", m.description, 21f, UIColors.Muted, TextAlignmentOptions.Top);
            ((RectTransform)desc.transform).At(0.5f, 1f, 0f, -264f, w - 36f, 100f, 0.5f, 1f);
            var stats = UIFactory.Text(face, "Stats", "HP x" + m.healthMult.ToString("0.#") + "   DMG x" + m.damageMult.ToString("0.#"), 22f, TextStyle.HeadingDark);
            ((RectTransform)stats.transform).At(0.5f, 0f, 0f, 168f, w - 30f, 30f, 0.5f, 0.5f);
            var fee = UIFactory.Text(face, "Fee", m.hireCost + " gold per battle", 21f, TextStyle.BodyMuted);
            ((RectTransform)fee.transform).At(0.5f, 0f, 0f, 134f, w - 30f, 28f, 0.5f, 0.5f);

            UIButton b;
            if (locked)
            {
                b = UIFactory.Button(face, "Action", "LEVEL " + m.unlockLevel, ButtonColor.Grey, w - 40f, 86f, UIIcon.Lock, 34f);
                b.SetInteractable(false);
            }
            else if (!owned)
            {
                b = UIFactory.Button(face, "Action", m.unlockCost.ToString(), ButtonColor.Gold, w - 40f, 86f, UIIcon.Coin, 38f);
                b.sound = Audio.SoundId.UiPurchase;
                b.onClick = () =>
                {
                    var r = MetaGame.UnlockMercenary(m);
                    if (r == PurchaseResult.Ok) Menu.ShowToast(m.displayName + " joins your army!", UIIcon.Mercenary, UIColors.GreenDark);
                    else Fail(r);
                };
            }
            else if (hired)
            {
                b = UIFactory.Button(face, "Action", "HIRED", ButtonColor.Green, w - 40f, 86f, UIIcon.Check, 36f);
                b.onClick = () => { MetaGame.SetHired(""); Menu.ShowToast(m.displayName + " stays home", UIIcon.Mercenary); };
            }
            else
            {
                b = UIFactory.Button(face, "Action", "HIRE", ButtonColor.Slate, w - 40f, 86f, UIIcon.Mercenary, 36f);
                b.onClick = () => { MetaGame.SetHired(m.id); Menu.ShowToast(m.displayName + " is hired", UIIcon.Mercenary, UIColors.GreenDark); };
            }
            ((RectTransform)b.transform).At(0.5f, 0f, 0f, 20f, w - 40f, 86f, 0.5f, 0f);
            mercButtons.Add(b);
        }

        void Fail(PurchaseResult r)
        {
            Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero);
            Menu.ShowToast(r == PurchaseResult.NotEnoughGold ? "Not enough gold" : "Not available yet", UIIcon.Lock, UIColors.RedDark);
        }
    }
}
