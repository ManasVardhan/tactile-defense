using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>The Army screen, in two tabs. UNITS: the squad slots and the catalogue of six unit cards
    /// (SquadBuilderPanel). HEROES: the mercenary roster (unlock, hire, dismiss). Opening the screen marks every
    /// unlocked unit as seen; the ones that were new keep their NEW tag until the screen closes.</summary>
    public class ArmyScreen : MenuScreen
    {
        public enum Tab { Units, Heroes }

        public override string Title => "ARMY";

        const float BodyW = 1528f, BodyH = 660f, MercH = 300f, TabW = 210f, TabH = 72f;

        RectTransform unitsTab, heroesTab, mercRow;
        UIButton unitsButton, heroesButton;
        readonly List<UIButton> mercButtons = new List<UIButton>();
        List<UnitTypeId> visitNew = new List<UnitTypeId>();

        public IReadOnlyList<UIButton> MercenaryButtons => mercButtons;
        public SquadBuilderPanel Builder { get; private set; }
        public Tab ActiveTab { get; private set; } = Tab.Units;
        public UIButton UnitsTabButton => unitsButton;
        public UIButton HeroesTabButton => heroesButton;
        /// <summary>Types tagged NEW on this visit.</summary>
        public IReadOnlyList<UnitTypeId> NewThisVisit => visitNew;

        protected override void Build()
        {
            var face = (RectTransform)Body.parent;
            unitsButton = UIFactory.Button(face, "TabUnits", "UNITS", ButtonColor.Team, TabW, TabH, UIIcon.Users, 30f);
            ((RectTransform)unitsButton.transform).At(1f, 1f, -TabW - 46f, -16f, TabW, TabH, 1f, 1f);
            unitsButton.onClick = () => ShowTab(Tab.Units);
            heroesButton = UIFactory.Button(face, "TabHeroes", "HEROES", ButtonColor.White, TabW, TabH, UIIcon.Mercenary, 30f);
            ((RectTransform)heroesButton.transform).At(1f, 1f, -30f, -16f, TabW, TabH, 1f, 1f);
            heroesButton.onClick = () => ShowTab(Tab.Heroes);

            unitsTab = UIFactory.Rect(Body, "UnitsTab").Stretch();
            Builder = SquadBuilderPanel.Create(unitsTab, BodyW, BodyH);
            ((RectTransform)Builder.transform).At(0.5f, 1f, 0f, 0f, BodyW, BodyH, 0.5f, 1f);

            heroesTab = UIFactory.Rect(Body, "HeroesTab").Stretch();
            var mh = UIFactory.Text(heroesTab, "MercHead", "HEROES", 40f, TextStyle.HeadingDark);
            ((RectTransform)mh.transform).At(0.5f, 1f, 0f, -116f, 600f, 52f, 0.5f, 1f);
            var msub = UIFactory.Text(heroesTab, "MercSub", "A hired hero joins after your squad. You pay the fee each battle.", 25f, TextStyle.BodyMuted);
            ((RectTransform)msub.transform).At(0.5f, 1f, 0f, -172f, 1100f, 40f, 0.5f, 1f);
            mercRow = UIFactory.Rect(heroesTab, "Mercs").At(0.5f, 1f, 0f, -244f, BodyW, MercH, 0.5f, 1f);
            ShowTab(Tab.Units);
        }

        public void ShowTab(Tab t)
        {
            ActiveTab = t;
            unitsTab.gameObject.SetActive(t == Tab.Units);
            heroesTab.gameObject.SetActive(t == Tab.Heroes);
            StyleTab(unitsButton, t == Tab.Units);
            StyleTab(heroesButton, t == Tab.Heroes);
        }

        static void StyleTab(UIButton b, bool on)
        {
            b.SetColor(on ? ButtonColor.Team : ButtonColor.White);
            var l = b.Label();
            if (l == null) return;
            l.fontSharedMaterial = on ? UIFactory.Skin.headingOutline : UIFactory.Skin.headingFont.material;
            l.color = on ? Color.white : UIColors.Ink;
        }

        public override void OnOpened()
        {
            // The NEW tags show for this visit; from now on the units count as seen (the menu dot goes out).
            visitNew = MetaGame.NewUnits;
            Builder.SetNewUnits(visitNew);
            Builder.ResetMessage();
            ShowTab(Tab.Units);
            // The "Open Army" callout is about a new unit: only a visit that shows one uses it up.
            if (visitNew.Count > 0) MetaGame.MarkGuideSeen(Progression.GuideArmy);
            MetaGame.MarkUnitsSeen();
            Refresh();
        }

        public override void OnClosed()
        {
            // "Add the Healer" is a one-time callout: once the player has been in Army with the Healer unlocked it
            // does not come back.
            if (Progression.IsUnlocked(UnitTypeId.Healer, MetaGame.LevelsCleared)) MetaGame.MarkGuideSeen(Progression.GuideAddUnit);
            visitNew = new List<UnitTypeId>();
            Builder.SetNewUnits(visitNew);
        }

        public override void Refresh()
        {
            Builder.Refresh();
            UIFactory.ClearChildren(mercRow);
            mercButtons.Clear();
            var db = MetaGame.Db;
            if (db == null) return;
            for (int i = 0; i < db.mercenaries.Length; i++) MercCard(db.mercenaries[i], i, db.mercenaries.Length);
        }

        void MercCard(MercenaryDef m, int i, int count)
        {
            if (m == null) return;
            var skin = UIFactory.Skin;
            var p = MetaGame.Profile;
            const float gap = 24f;
            float w = (BodyW - gap * (count - 1)) / Mathf.Max(1, count), h = MercH;
            var root = UIFactory.Rect(mercRow, "Merc_" + m.id).At(0f, 1f, i * (w + gap), 0f, w, h, 0f, 1f);
            bool owned = p.OwnsMercenary(m.id);
            bool hired = owned && p.hiredMercenary == m.id;
            bool locked = p.level < m.unlockLevel;
            var face = UIFactory.Card(root, "Card", w, h, 26f, null, hired ? UIColors.Green : (Color?)null);
            ((RectTransform)face.parent).Stretch();
            const float portrait = 170f;
            var top = UIFactory.Panel(face, "Portrait", locked ? UIColors.GreyDark : m.accent, 22f);
            ((RectTransform)top.transform).At(0f, 1f, 14f, -14f, portrait, portrait, 0f, 1f);
            top.gameObject.AddComponent<RectMask2D>();
            var glow = UIFactory.Img(top.transform, "Glow", skin.glow, UIColors.WithAlpha(Color.white, 0.4f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, 0f, 280f, 280f);
            var hero = UIFactory.Img(top.transform, "Hero", skin.Hero(p.team), locked ? new Color(0.5f, 0.5f, 0.55f) : Color.white);
            ((RectTransform)hero.transform).At(0.5f, 0.5f, 0f, -6f, 186f, 186f);
            float tx = portrait + 34f, tw = w - tx - 16f;
            var name = UIFactory.Text(face, "Name", m.displayName.ToUpperInvariant(), 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)name.transform).At(0f, 1f, tx, -18f, tw, 46f, 0f, 1f);
            var ab = UIFactory.Text(face, "Ability", m.ability, 25f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ab.color = UIColors.GoldDark;
            ((RectTransform)ab.transform).At(0f, 1f, tx, -68f, tw, 32f, 0f, 1f);
            var stats = UIFactory.Text(face, "Stats", "HP x" + m.healthMult.ToString("0.#") + "  DMG x" + m.damageMult.ToString("0.#"), 24f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)stats.transform).At(0f, 1f, tx, -106f, tw, 30f, 0f, 1f);
            var fee = UIFactory.Text(face, "Fee", m.hireCost + " gold per battle", 23f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)fee.transform).At(0f, 1f, tx, -142f, tw, 30f, 0f, 1f);

            float bw = w - 28f, bh = 70f;
            UIButton b;
            if (locked)
            {
                b = UIFactory.Button(face, "Action", "LEVEL " + m.unlockLevel, ButtonColor.Grey, bw, bh, UIIcon.Lock, 32f);
                b.SetInteractable(false);
            }
            else if (!owned)
            {
                b = UIFactory.Button(face, "Action", m.unlockCost.ToString(), ButtonColor.Gold, bw, bh, UIIcon.Coin, 36f);
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
                b = UIFactory.Button(face, "Action", "HIRED", ButtonColor.Green, bw, bh, UIIcon.Check, 34f);
                b.onClick = () => { MetaGame.SetHired(""); Menu.ShowToast(m.displayName + " stays home", UIIcon.Mercenary); };
            }
            else
            {
                b = UIFactory.Button(face, "Action", "HIRE", ButtonColor.Slate, bw, bh, UIIcon.Mercenary, 34f);
                b.onClick = () => { MetaGame.SetHired(m.id); Menu.ShowToast(m.displayName + " is hired", UIIcon.Mercenary, UIColors.GreenDark); };
            }
            ((RectTransform)b.transform).At(0.5f, 0f, 0f, 14f, bw, bh, 0.5f, 0f);
            mercButtons.Add(b);
        }

        void Fail(PurchaseResult r)
        {
            Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero);
            Menu.ShowToast(r == PurchaseResult.NotEnoughGold ? "Not enough gold" : "Not available yet", UIIcon.Lock, UIColors.RedDark);
        }
    }
}
