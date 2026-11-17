using System.Collections.Generic;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Research: three tabbed branches of level locked upgrades bought with gold (verified mechanism;
    /// content is ours). Cards show level pips, the next effect, the cost or the lock level.</summary>
    public class ResearchScreen : MenuScreen
    {
        public override string Title => "RESEARCH";

        ResearchBranch branch = ResearchBranch.Troops;
        readonly UIButton[] tabs = new UIButton[3];
        RectTransform row;
        TextMeshProUGUI branchInfo;
        readonly Dictionary<string, UIButton> buyButtons = new Dictionary<string, UIButton>();
        readonly Dictionary<string, RectTransform> cardsById = new Dictionary<string, RectTransform>();
        string lastBought;

        public ResearchBranch Branch => branch;
        public UIButton BuyButton(string id) => buyButtons.TryGetValue(id, out var b) ? b : null;

        static readonly string[] BranchNames = { "TROOPS", "OFFENSE", "DEFENSE" };
        static readonly UIIcon[] BranchIcons = { UIIcon.Users, UIIcon.Sword, UIIcon.Shield };
        static readonly string[] BranchText =
        {
            "Bigger, tougher, faster squads.",
            "Reinforcements and your squad leader.",
            "Unlock and upgrade defenses for your own base.",
        };

        protected override void Build()
        {
            for (int i = 0; i < 3; i++)
            {
                var b = UIFactory.Button(Body, "Tab_" + BranchNames[i], BranchNames[i], ButtonColor.White, 300f, 82f, BranchIcons[i], 36f);
                ((RectTransform)b.transform).At(0f, 1f, i * 316f, 0f, 300f, 82f, 0f, 1f);
                var br = (ResearchBranch)i;
                b.onClick = () => SetBranch(br);
                tabs[i] = b;
            }
            branchInfo = UIFactory.Text(Body, "BranchInfo", "", 26f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineRight);
            ((RectTransform)branchInfo.transform).At(1f, 1f, -6f, -18f, 560f, 44f, 1f, 1f);
            row = UIFactory.Rect(Body, "Cards").Stretch(0f, 104f, 0f, 0f);
        }

        public void SetBranch(ResearchBranch b)
        {
            branch = b;
            Refresh();
        }

        public override void Refresh()
        {
            for (int i = 0; i < 3; i++) tabs[i].SetColor(i == (int)branch ? ButtonColor.Team : ButtonColor.White);
            for (int i = 0; i < 3; i++)
            {
                var l = tabs[i].Label();
                if (l != null) { l.font = UIFactory.Skin.headingFont; l.fontSharedMaterial = i == (int)branch ? UIFactory.Skin.headingOutline : UIFactory.Skin.headingFont.material; l.color = i == (int)branch ? Color.white : UIColors.Ink; }
            }
            branchInfo.text = BranchText[(int)branch];
            UIFactory.ClearChildren(row);
            buyButtons.Clear();
            cardsById.Clear();
            var tree = MetaGame.Db != null ? MetaGame.Db.research : null;
            if (tree == null) return;
            var nodes = new List<ResearchNode>();
            foreach (var n in tree.nodes) if (n.branch == branch) nodes.Add(n);
            float gap = 18f;
            float avail = 1528f;
            float w = Mathf.Min(290f, (avail - (nodes.Count - 1) * gap) / Mathf.Max(1, nodes.Count));
            float total = nodes.Count * w + (nodes.Count - 1) * gap;
            for (int i = 0; i < nodes.Count; i++) Card(nodes[i], -total / 2f + w / 2f + i * (w + gap), w);
            if (lastBought != null && cardsById.TryGetValue(lastBought, out var card)) UIAnim.Pop(card, 0.85f, 0.35f);
            lastBought = null;
        }

        void Card(ResearchNode n, float x, float w)
        {
            var skin = UIFactory.Skin;
            var p = MetaGame.Profile;
            int lvl = p.GetResearch(n.id);
            bool maxed = lvl >= n.MaxLevel;
            var state = MetaGame.CanBuyResearch(n);
            const float h = 556f;
            var root = UIFactory.Rect(row, "Node_" + n.id).At(0.5f, 0f, x, 0f, w, h, 0.5f, 0f);
            cardsById[n.id] = root;
            var face = UIFactory.Card(root, "Card", w, h, 28f, null, maxed ? UIColors.Gold : (Color?)null);
            ((RectTransform)face.parent).Stretch();

            Color accent = branch == ResearchBranch.Troops ? UIColors.Team(p.team) : branch == ResearchBranch.Offense ? UIColors.Gold : UIColors.Blue;
            bool isDefense = !string.IsNullOrEmpty(n.defenseId);
            var discBg = UIFactory.Img(face, "Disc", skin.circle, state == PurchaseResult.Locked && lvl == 0 ? UIColors.Grey : accent);
            ((RectTransform)discBg.transform).At(0.5f, 1f, 0f, -18f, 150f, 150f, 0.5f, 1f);
            var ring = UIFactory.Img(discBg.transform, "Ring", skin.ringThin, UIColors.WithAlpha(Color.white, 0.55f));
            ((RectTransform)ring.transform).Stretch(6f, 6f, 6f, 6f);
            Image icon;
            if (isDefense)
            {
                icon = UIFactory.Img(discBg.transform, "Render", skin.Defense(n.defenseId, p.team), Color.white);
                ((RectTransform)icon.transform).At(0.5f, 0.5f, 0f, 6f, 168f, 168f);
            }
            else
            {
                var key = n.icon;
                UIIcon ic = System.Enum.TryParse(key, out UIIcon parsed) ? parsed : UIIcon.Star;
                icon = UIFactory.Icon(discBg.transform, ic, 96f);
                ((RectTransform)icon.transform).At(0.5f, 0.5f, 0f, 2f, 96f, 96f);
            }

            var name = UIFactory.Text(face, "Name", n.displayName, 30f, TextStyle.HeadingDark);
            ((RectTransform)name.transform).At(0.5f, 1f, 0f, -178f, w - 24f, 40f, 0.5f, 1f);
            name.enableAutoSizing = true; name.fontSizeMin = 20f; name.fontSizeMax = 30f;

            var pips = UIFactory.Pips(face, "Pips", n.MaxLevel, 20f, out var pr);
            pr.At(0.5f, 1f, 0f, -226f, w - 30f, 24f, 0.5f, 1f);
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < lvl ? accent : UIColors.CardLine;

            var desc = UIFactory.Paragraph(face, "Desc", n.description, 22f, UIColors.Muted, TextAlignmentOptions.Top);
            ((RectTransform)desc.transform).At(0.5f, 1f, 0f, -260f, w - 34f, 96f, 0.5f, 1f);

            var eff = UIFactory.Panel(face, "Effect", UIColors.CardInner, 16f);
            ((RectTransform)eff.transform).At(0.5f, 0f, 0f, 124f, w - 34f, 62f, 0.5f, 0f);
            string now = n.FormatValue(ResearchTree.ValueAt(n, lvl));
            string next = maxed ? "MAX" : n.FormatValue(ResearchTree.ValueAt(n, lvl + 1));
            if (isDefense && n.effect == ResearchEffect.DefenseUnlock)
            {
                now = lvl > 0 ? "Lv " + lvl : "Locked";
                next = maxed ? "MAX" : (lvl == 0 ? "Unlock" : "Lv " + (lvl + 1));
            }
            var et = UIFactory.Text(eff.transform, "Text", maxed ? now : now + "  <color=#4C8A1F>> " + next + "</color>", 26f, TextStyle.HeadingDark);
            ((RectTransform)et.transform).Stretch(8f, 0f, 8f, 2f);
            et.enableAutoSizing = true; et.fontSizeMin = 18f; et.fontSizeMax = 26f;

            UIButton b;
            if (maxed)
            {
                b = UIFactory.Button(face, "Buy", "MAXED", ButtonColor.Gold, w - 36f, 88f, UIIcon.Star, 34f);
                b.SetInteractable(false);
                b.SetColors(UIColors.Gold, UIColors.GoldDark);
            }
            else
            {
                var step = n.steps[lvl];
                if (state == PurchaseResult.Locked)
                {
                    b = UIFactory.Button(face, "Buy", "LEVEL " + step.requiredLevel, ButtonColor.Grey, w - 36f, 88f, UIIcon.Lock, 32f);
                    b.SetInteractable(false);
                }
                else
                {
                    bool afford = state == PurchaseResult.Ok;
                    b = UIFactory.Button(face, "Buy", step.goldCost.ToString(), afford ? ButtonColor.Green : ButtonColor.Slate, w - 36f, 88f, UIIcon.Coin, 36f);
                    b.sound = Audio.SoundId.UiPurchase;
                    var node = n;
                    var card = root;
                    b.onClick = () => Buy(node, card);
                }
            }
            ((RectTransform)b.transform).At(0.5f, 0f, 0f, 20f, w - 36f, 88f, 0.5f, 0f);
            buyButtons[n.id] = b;
        }

        public PurchaseResult Buy(ResearchNode n, RectTransform card)
        {
            var r = MetaGame.CanBuyResearch(n);
            if (r != PurchaseResult.Ok)
            {
                Audio.AudioManager.Play(Audio.SoundId.UiError, Vector3.zero);
                if (card != null) UIAnim.Shake(card, 14f);
                Menu.ShowToast(r == PurchaseResult.NotEnoughGold ? "Not enough gold" : r == PurchaseResult.Locked ? "Reach a higher level first" : "Already maxed", UIIcon.Lock, UIColors.RedDark);
                return r;
            }
            lastBought = n.id;
            r = MetaGame.BuyResearch(n);   // saves and raises Changed, which refreshes this screen
            Menu.ShowToast(n.displayName + " level " + MetaGame.ResearchLevel(n.id) + "!", UIIcon.Flask, UIColors.GreenDark);
            return r;
        }
    }
}
