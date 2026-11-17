using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Base editor UI built from the UI kit: header (back, title, budget and space meters, gold, Save,
    /// Test, overview), rebuy banner, bottom tray of defense cards, selection panel, toasts and dialogs.</summary>
    public class BaseEditorHud : MonoBehaviour
    {
        BaseEditorRoot root;
        public UIRoot Root { get; private set; }
        public ConfirmDialog Dialog { get; private set; }
        public UIButton SaveButton { get; private set; }
        public UIButton TestButton { get; private set; }
        public UIButton BackButton { get; private set; }
        public UIButton OverviewButton { get; private set; }
        public UIButton RebuyAllButton { get; private set; }
        public bool ModalOpen => Dialog != null && Dialog.IsOpen;
        public readonly List<CardView> Cards = new List<CardView>();

        UIToast toast;
        UIProgressBar budgetBar, spaceBar;
        TextMeshProUGUI budgetText, spaceText, goldText, pendingText, hintText, attackerLabel;
        RectTransform rebuyBanner, selPanel, wreckPanel, trayContent, attackerLabelRt;
        TextMeshProUGUI rebuyText, selName, selInfo, wreckName, wreckInfo;
        Image selImage, wreckImage;
        UIButton rotateBtn, moveBtn, removeBtn, rebuyBtn, dismissBtn;
        CanvasGroup hintGroup;

        public class CardView
        {
            public string id;
            public RectTransform root;
            public Image render, highlight, lockIcon;
            public TextMeshProUGUI cost, count, level, points;
            public RectTransform freeTag;
        }

        public static BaseEditorHud Create(BaseEditorRoot r)
        {
            var root = UIRoot.Create("EditorCanvas", r.cam, r.skin, 10);
            var hud = root.gameObject.AddComponent<BaseEditorHud>();
            hud.root = r;
            hud.Root = root;
            hud.Build();
            return hud;
        }

        void Build()
        {
            var safe = Root.Safe;
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;

            // ---- header left: back + title
            BackButton = UIFactory.IconButton(safe, "Back", UIIcon.ArrowLeft, 96f, ButtonColor.Slate);
            ((RectTransform)BackButton.transform).At(0f, 1f, 24f, -18f, 96f, 96f, 0f, 1f);
            BackButton.sound = Audio.SoundId.UiBack;
            BackButton.onClick = root.RequestBack;
            var title = UIFactory.Text(safe, "Title", "MY BASE", 58f, TextStyle.Heading, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)title.transform).At(0f, 1f, 138f, -20f, 420f, 64f, 0f, 1f);
            var sub = UIFactory.Text(safe, "Subtitle", "Defend your lane: AI attackers raid it", 26f, TextStyle.BodyOutline, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)sub.transform).At(0f, 1f, 140f, -82f, 520f, 34f, 0f, 1f);

            // ---- header center: budget and space meters
            var stats = UIFactory.Rect(safe, "Stats").At(0.5f, 1f, -40f, -18f, 640f, 100f, 0.5f, 1f);
            var sbg = UIFactory.Img(stats, "Bg", skin.pill, UIColors.WithAlpha(UIColors.Ink, 0.8f), UIFactory.PillMult(100f));
            ((RectTransform)sbg.transform).Stretch();
            Meter(stats, "Budget", UIIcon.Lightning, "BUDGET", -150f, UIColors.Gold, out budgetBar, out budgetText);
            Meter(stats, "Space", UIIcon.Grid, "SPACE", 150f, UIColors.Xp, out spaceBar, out spaceText);

            // ---- header right: Save, Test, overview; gold under them
            OverviewButton = UIFactory.IconButton(safe, "Overview", UIIcon.Globe, 92f, ButtonColor.White, 0.62f);
            ((RectTransform)OverviewButton.transform).At(1f, 1f, -24f, -20f, 92f, 92f, 1f, 1f);
            OverviewButton.onClick = root.ToggleOverview;
            TestButton = UIFactory.Button(safe, "Test", "TEST", ButtonColor.Blue, 200f, 92f, UIIcon.Play, 38f);
            ((RectTransform)TestButton.transform).At(1f, 1f, -132f, -20f, 200f, 92f, 1f, 1f);
            TestButton.onClick = root.RequestTest;
            SaveButton = UIFactory.Button(safe, "Save", "SAVE", ButtonColor.Green, 200f, 92f, UIIcon.Save, 38f);
            ((RectTransform)SaveButton.transform).At(1f, 1f, -348f, -20f, 200f, 92f, 1f, 1f);
            SaveButton.onClick = () => root.Save();
            goldText = UIFactory.Chip(safe, "Gold", UIIcon.Coin, 250f, 62f, out var goldRt);
            goldRt.At(1f, 1f, -24f, -130f, 250f, 62f, 1f, 1f);
            pendingText = UIFactory.Text(safe, "Pending", "", 26f, TextStyle.BodyOutline, TextAlignmentOptions.MidlineRight);
            ((RectTransform)pendingText.transform).At(1f, 1f, -290f, -146f, 360f, 34f, 1f, 1f);

            // ---- rebuy banner
            rebuyBanner = UIFactory.Rect(safe, "Rebuy").At(0.5f, 1f, -40f, -134f, 720f, 76f, 0.5f, 1f);
            var rbg = UIFactory.Img(rebuyBanner, "Bg", skin.pill, UIColors.WithAlpha(UIColors.RedDark, 0.9f), UIFactory.PillMult(76f));
            ((RectTransform)rbg.transform).Stretch();
            var ri = UIFactory.Icon(rebuyBanner, UIIcon.Skull, 52f);
            ((RectTransform)ri.transform).At(0f, 0.5f, 18f, 0f, 52f, 52f, 0f, 0.5f);
            rebuyText = UIFactory.Text(rebuyBanner, "Text", "", 28f, TextStyle.HeadingThin, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)rebuyText.transform).Stretch(80f, 0f, 270f, 2f);
            RebuyAllButton = UIFactory.Button(rebuyBanner, "RebuyAll", "REBUY ALL", ButtonColor.Gold, 250f, 64f, UIIcon.Coin, 26f);
            ((RectTransform)RebuyAllButton.transform).At(1f, 0.5f, -8f, 0f, 250f, 64f, 1f, 0.5f);
            RebuyAllButton.onClick = () => root.RebuyAll();

            // ---- bottom tray
            var tray = UIFactory.Rect(safe, "Tray").At(0.5f, 0f, 0f, 0f, 1920f, 262f, 0.5f, 0f);
            tray.anchorMin = new Vector2(0f, 0f); tray.anchorMax = new Vector2(1f, 0f);
            tray.offsetMin = new Vector2(16f, 8f); tray.offsetMax = new Vector2(-16f, 262f);
            var tbg = UIFactory.Panel(tray, "Bg", UIColors.WithAlpha(UIColors.Ink, 0.78f), 36f);
            ((RectTransform)tbg.transform).Stretch();
            tbg.raycastTarget = true;
            var scroll = UIFactory.HScroll(tray, "Cards", 16f, 18f, out trayContent);
            ((RectTransform)scroll.transform).Stretch(6f, 10f, 6f, 10f);
            BuildCards();

            // ---- hint
            hintText = UIFactory.Text(safe, "Hint", "Drag a defense onto your base, or tap a card then the ground", 30f, TextStyle.BodyOutline);
            ((RectTransform)hintText.transform).At(0.5f, 0f, 0f, 286f, 1200f, 40f, 0.5f, 0f);
            hintGroup = hintText.gameObject.AddComponent<CanvasGroup>();

            BuildSelection(safe);

            attackerLabelRt = UIFactory.Tag(safe, "AttackerLabel", "ATTACKER START: NO BUILD", UIColors.WithAlpha(UIColors.RedDark, 0.88f), 46f, 26f);
            attackerLabelRt.anchorMin = attackerLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            attackerLabelRt.SetSiblingIndex(0);

            toast = UIToast.Create(safe, 220f);
            Dialog = ConfirmDialog.Create(Root.CanvasRect);
        }

        void Meter(RectTransform parent, string name, UIIcon icon, string label, float x, Color fill, out UIProgressBar bar, out TextMeshProUGUI value)
        {
            var g = UIFactory.Rect(parent, name).At(0.5f, 0.5f, x, 0f, 290f, 90f);
            var ic = UIFactory.Icon(g, icon, 58f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 6f, 0f, 58f, 58f, 0f, 0.5f);
            var l = UIFactory.Text(g, "Label", label, 24f, TextStyle.HeadingThin, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)l.transform).At(0f, 0.5f, 72f, 18f, 120f, 30f, 0f, 0.5f);
            value = UIFactory.Text(g, "Value", "0 / 0", 26f, TextStyle.Heading, TextAlignmentOptions.MidlineRight);
            ((RectTransform)value.transform).At(1f, 0.5f, -10f, 18f, 130f, 30f, 1f, 0.5f);
            bar = UIFactory.Bar(g, "Bar", 200f, 20f, fill, UIColors.WithAlpha(Color.white, 0.18f));
            ((RectTransform)bar.transform).At(0f, 0.5f, 72f, -16f, 206f, 20f, 0f, 0.5f);
        }

        void BuildCards()
        {
            UIFactory.ClearChildren(trayContent);
            Cards.Clear();
            var cat = MetaGame.Defenses;
            if (cat == null) return;
            foreach (var e in cat.entries) Cards.Add(BuildCard(e));
        }

        CardView BuildCard(DefenseCatalog.Entry e)
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;
            const float w = 186f, h = 226f;
            var cv = new CardView { id = e.id };
            var rt = UIFactory.Rect(trayContent, "Card_" + e.id);
            rt.sizeDelta = new Vector2(w, h);
            cv.root = rt;
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            rt.gameObject.AddComponent<BaseEditorCard>().Init(root, e.id);
            cv.highlight = UIFactory.Panel(rt, "Highlight", UIColors.Team(team), 32f);
            ((RectTransform)cv.highlight.transform).Stretch(-7f, -7f, -7f, -2f);
            var face = UIFactory.Card(rt, "Card", w, h, 28f);
            ((RectTransform)face.parent).Stretch();
            var glow = UIFactory.Img(face, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Team(team), 0.18f));
            ((RectTransform)glow.transform).At(0.5f, 1f, 0f, 10f, 190f, 160f, 0.5f, 1f);
            Sprite sp = e.id == DefenseCatalog.SquadId ? skin.Soldier(team) : skin.Defense(e.id, team);
            cv.render = UIFactory.Img(face, "Render", sp, Color.white);
            ((RectTransform)cv.render.transform).At(0.5f, 1f, 0f, -4f, 128f, 128f, 0.5f, 1f);
            var name = UIFactory.Text(face, "Name", e.displayName, 25f, TextStyle.HeadingDark);
            ((RectTransform)name.transform).At(0.5f, 1f, 0f, -128f, w - 20f, 32f, 0.5f, 1f);
            name.enableAutoSizing = true; name.fontSizeMin = 18f; name.fontSizeMax = 25f;
            cv.cost = UIFactory.IconValue(face, "Cost", UIIcon.Coin, e.goldCost.ToString(), 24f, TextStyle.HeadingDark, out var crt);
            crt.At(0f, 0f, 12f, 8f, 90f, 40f, 0f, 0f);
            cv.points = UIFactory.IconValue(face, "Points", UIIcon.Lightning, e.budget.ToString(), 24f, TextStyle.HeadingDark, out var prt);
            prt.At(1f, 0f, -10f, 8f, 70f, 40f, 1f, 0f);
            cv.freeTag = UIFactory.Tag(face, "Free", "IN STOCK", UIColors.GreenDark, 32f, 20f);
            cv.freeTag.At(0f, 0f, 8f, 12f, cv.freeTag.sizeDelta.x, 32f, 0f, 0f);
            var lvBg = UIFactory.Img(face, "LevelBg", skin.pill, UIColors.WithAlpha(UIColors.Slate, 0.9f), UIFactory.PillMult(32f));
            ((RectTransform)lvBg.transform).At(0f, 1f, 8f, -8f, 64f, 32f, 0f, 1f);
            cv.level = UIFactory.Text(lvBg.transform, "Lv", "Lv 1", 20f, TextStyle.HeadingThin);
            ((RectTransform)cv.level.transform).Stretch(0f, 0f, 0f, 2f);
            cv.count = UIFactory.Text(face, "Count", "0/4", 22f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineRight);
            cv.count.color = UIColors.Muted;
            ((RectTransform)cv.count.transform).At(1f, 1f, -12f, -10f, 80f, 30f, 1f, 1f);
            cv.lockIcon = UIFactory.Icon(face, UIIcon.Lock, 64f);
            ((RectTransform)cv.lockIcon.transform).At(0.5f, 1f, 0f, -38f, 64f, 64f, 0.5f, 1f);
            return cv;
        }

        void BuildSelection(RectTransform safe)
        {
            // Placed defense.
            selPanel = UIFactory.Rect(safe, "Selection").At(0f, 0f, 24f, 280f, 700f, 190f, 0f, 0f);
            var face = UIFactory.Card(selPanel, "Card", 700f, 190f, 30f);
            ((RectTransform)face.parent).Stretch();
            var disc = UIFactory.Img(face, "Disc", UIFactory.Skin.circle, UIColors.CardInner);
            ((RectTransform)disc.transform).At(0f, 0.5f, 14f, 0f, 150f, 150f, 0f, 0.5f);
            selImage = UIFactory.Img(disc.transform, "Render", null, Color.white);
            ((RectTransform)selImage.transform).Stretch(4f, 4f, 4f, 4f);
            selName = UIFactory.Text(face, "Name", "", 34f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)selName.transform).At(0f, 1f, 180f, -12f, 500f, 44f, 0f, 1f);
            selInfo = UIFactory.Text(face, "Info", "", 24f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)selInfo.transform).At(0f, 1f, 182f, -56f, 500f, 30f, 0f, 1f);
            rotateBtn = UIFactory.Button(face, "Rotate", "ROTATE", ButtonColor.Slate, 160f, 76f, UIIcon.Rotate, 24f);
            ((RectTransform)rotateBtn.transform).At(0f, 0f, 180f, 12f, 160f, 76f, 0f, 0f);
            rotateBtn.onClick = root.RotateSelected;
            moveBtn = UIFactory.Button(face, "Move", "MOVE", ButtonColor.Blue, 160f, 76f, UIIcon.Move, 24f);
            ((RectTransform)moveBtn.transform).At(0f, 0f, 350f, 12f, 160f, 76f, 0f, 0f);
            moveBtn.onClick = root.BeginMoveSelected;
            removeBtn = UIFactory.Button(face, "Remove", "REMOVE", ButtonColor.Red, 160f, 76f, UIIcon.Trash, 24f);
            ((RectTransform)removeBtn.transform).At(0f, 0f, 520f, 12f, 160f, 76f, 0f, 0f);
            removeBtn.onClick = root.RemoveSelected;

            // Wreck of a consumed defense.
            wreckPanel = UIFactory.Rect(safe, "Wreck").At(0f, 0f, 24f, 280f, 700f, 190f, 0f, 0f);
            var wf = UIFactory.Card(wreckPanel, "Card", 700f, 190f, 30f);
            ((RectTransform)wf.parent).Stretch();
            var wd = UIFactory.Img(wf, "Disc", UIFactory.Skin.circle, UIColors.CardLine);
            ((RectTransform)wd.transform).At(0f, 0.5f, 14f, 0f, 150f, 150f, 0f, 0.5f);
            wreckImage = UIFactory.Img(wd.transform, "Render", null, new Color(0.45f, 0.45f, 0.5f, 0.8f));
            ((RectTransform)wreckImage.transform).Stretch(4f, 4f, 4f, 4f);
            wreckName = UIFactory.Text(wf, "Name", "", 34f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)wreckName.transform).At(0f, 1f, 180f, -12f, 500f, 44f, 0f, 1f);
            wreckInfo = UIFactory.Text(wf, "Info", "Destroyed in a defense. Rebuy it here?", 24f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)wreckInfo.transform).At(0f, 1f, 182f, -56f, 500f, 30f, 0f, 1f);
            rebuyBtn = UIFactory.Button(wf, "Rebuy", "REBUY", ButtonColor.Gold, 250f, 76f, UIIcon.Coin, 26f);
            ((RectTransform)rebuyBtn.transform).At(0f, 0f, 180f, 12f, 250f, 76f, 0f, 0f);
            rebuyBtn.onClick = root.RebuySelectedWreck;
            dismissBtn = UIFactory.Button(wf, "Dismiss", "CLEAR", ButtonColor.Slate, 200f, 76f, UIIcon.Close, 26f);
            ((RectTransform)dismissBtn.transform).At(0f, 0f, 446f, 12f, 200f, 76f, 0f, 0f);
            dismissBtn.onClick = root.DismissSelectedWreck;
        }

        public void Toast(string text, UIIcon icon, Color color) { if (toast != null) toast.Show(text, icon, color, 1.6f); }

        public void Refresh()
        {
            var s = root.Session;
            if (s == null) return;
            int bu = s.BudgetUsed, sp = s.SpaceUsed;
            budgetBar.Set(s.BudgetCap > 0 ? bu / (float)s.BudgetCap : 0f);
            spaceBar.Set(s.SpaceCap > 0 ? sp / (float)s.SpaceCap : 0f);
            budgetText.text = bu + " / " + s.BudgetCap;
            spaceText.text = sp + " / " + s.SpaceCap;
            budgetText.color = bu >= s.BudgetCap ? UIColors.Gold : Color.white;
            spaceText.color = sp >= s.SpaceCap ? UIColors.Gold : Color.white;
            goldText.text = s.GoldAvailable.ToString();
            pendingText.text = s.PendingGold > 0 ? "-" + s.PendingGold + " gold on save" : (s.Dirty ? "Unsaved changes" : "");
            pendingText.color = s.PendingGold > 0 ? UIColors.Lighter(UIColors.Red, 0.35f) : Color.white;
            SaveButton.SetInteractable(s.Dirty);

            rebuyBanner.gameObject.SetActive(s.Destroyed.Count > 0);
            if (s.Destroyed.Count > 0)
            {
                int n = s.Destroyed.Count;
                rebuyText.text = n + (n == 1 ? " DEFENSE" : " DEFENSES") + " DESTROYED";
                var l = RebuyAllButton.Label();
                if (l != null) l.text = "REBUY " + s.RebuyAllCost();
                RebuyAllButton.SetInteractable(s.RebuyAllCost() <= s.GoldAvailable);
            }

            foreach (var cv in Cards) RefreshCard(cv, s);

            bool sel = root.SelectedIndex >= 0, wreck = root.SelectedWreck >= 0;
            selPanel.gameObject.SetActive(sel);
            wreckPanel.gameObject.SetActive(wreck);
            var team = UIFactory.PlayerTeam;
            if (sel)
            {
                var d = s.Placements[root.SelectedIndex];
                s.TryEntry(d.defenseId, out var e);
                selName.text = e.displayName;
                int lvl = MetaGame.DefenseLevel(d.defenseId);
                string range = e.range > 0f ? "  |  Range " + e.range.ToString("0") : "";
                selInfo.text = (string.IsNullOrEmpty(e.researchId) ? "" : "Lv " + lvl + "  |  ") + e.budget + " budget, " + e.space + " space" + range;
                selImage.sprite = d.defenseId == DefenseCatalog.SquadId ? UIFactory.Skin.Bust(team) : UIFactory.Skin.Defense(d.defenseId, team);
                selImage.preserveAspect = true;
                moveBtn.SetColor(root.MoveMode ? ButtonColor.Gold : ButtonColor.Blue);
            }
            if (wreck)
            {
                var d = s.Destroyed[root.SelectedWreck];
                s.TryEntry(d.defenseId, out var e);
                wreckName.text = e.displayName + " (wreck)";
                wreckImage.sprite = d.defenseId == DefenseCatalog.SquadId ? UIFactory.Skin.Bust(team) : UIFactory.Skin.Defense(d.defenseId, team);
                wreckImage.preserveAspect = true;
                var l = rebuyBtn.Label();
                if (l != null) l.text = "REBUY " + s.RebuyCost(root.SelectedWreck);
            }
            hintText.text = root.ArmedId != null ? "Tap the ground to place " + root.NameOf(root.ArmedId) : "Drag a defense onto your base, or tap a card then the ground";
            hintGroup.alpha = sel || wreck ? 0f : 1f;
            var ov = OverviewButton.content != null ? OverviewButton.content.GetComponentInChildren<Image>() : null;
            OverviewButton.SetColor(root.Overview ? ButtonColor.Gold : ButtonColor.White);
        }

        void RefreshCard(CardView cv, BaseEditSession s)
        {
            s.TryEntry(cv.id, out var e);
            bool unlocked = s.IsUnlocked(cv.id);
            int count = s.CountOf(cv.id);
            int stock = s.StockOf(cv.id);
            cv.render.color = unlocked ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.55f);
            cv.lockIcon.gameObject.SetActive(!unlocked);
            int lvl = MetaGame.DefenseLevel(cv.id);
            cv.level.text = !unlocked ? "LOCK" : (string.IsNullOrEmpty(e.researchId) ? "Lv -" : "Lv " + lvl);
            cv.count.text = count + "/" + e.maxCount;
            cv.count.color = count >= e.maxCount ? UIColors.Red : UIColors.Muted;
            bool free = stock > 0;
            cv.freeTag.gameObject.SetActive(free && unlocked);
            cv.cost.transform.parent.gameObject.SetActive(!free || !unlocked);
            cv.cost.color = e.goldCost > s.GoldAvailable ? UIColors.Red : UIColors.Ink;
            bool fits = unlocked && count < e.maxCount && s.BudgetUsed + e.budget <= s.BudgetCap && s.SpaceUsed + e.space <= s.SpaceCap;
            cv.points.color = s.BudgetUsed + e.budget > s.BudgetCap ? UIColors.Red : UIColors.Ink;
            cv.highlight.gameObject.SetActive(root.ArmedId == cv.id || root.GhostId == cv.id && root.GhostValid && false);
            if (!cv.root.TryGetComponent(out CanvasGroup cg)) cg = cv.root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = fits || !unlocked ? 1f : 0.62f;
        }

        void LateUpdate()
        {
            // World anchored label for the attacker start zone (no allocations).
            var map = root.Map;
            if (map == null || attackerLabelRt == null || root.cam == null) return;
            Vector3 w = new Vector3(0f, 0f, map.attackerZoneMaxZ - 3f);
            Vector3 vp = root.cam.WorldToViewportPoint(w);
            bool on = vp.z > 0f && vp.y > 0.28f && vp.y < 0.85f;
            if (attackerLabelRt.gameObject.activeSelf != on) attackerLabelRt.gameObject.SetActive(on);
            if (!on) return;
            Vector2 size = Root.Safe.rect.size;
            attackerLabelRt.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        }
    }
}
