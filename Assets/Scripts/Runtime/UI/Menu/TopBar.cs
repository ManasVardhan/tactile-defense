using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Home top bar: round crest, player name, level with XP bar, gold (ticks up on change), settings
    /// button. Prisms and the league are not shown: nothing on the menu spends or ranks them any more.</summary>
    public class TopBar : MonoBehaviour
    {
        MenuRoot menu;
        Image crest, crestRing;
        Image bust;
        TextMeshProUGUI nameText, levelText, xpText;
        UIProgressBar xpBar;
        UINumberTicker gold;
        RectTransform goldRoot;
        int lastGold = -1;

        public UIButton Settings { get; private set; }
        public RectTransform GoldChip => goldRoot;

        public static TopBar Build(MenuRoot menu, RectTransform parent)
        {
            var rt = UIFactory.Rect(parent, "TopBar").Stretch();
            var bar = rt.gameObject.AddComponent<TopBar>();
            bar.menu = menu;
            bar.BuildInternal(rt);
            return bar;
        }

        void BuildInternal(RectTransform rt)
        {
            var skin = UIFactory.Skin;
            // Player plate (top left).
            var plate = UIFactory.Rect(rt, "PlayerPlate").At(0f, 1f, 24f, -18f, 560f, 132f);
            var plateBg = UIFactory.Img(plate, "Bg", skin.pill, UIColors.WithAlpha(UIColors.Ink, 0.8f), UIFactory.PillMult(104f));
            ((RectTransform)plateBg.transform).At(0f, 0.5f, 60f, 0f, 500f, 104f, 0f, 0.5f);
            crestRing = UIFactory.Img(plate, "CrestRim", skin.circle, UIColors.Card);
            ((RectTransform)crestRing.transform).At(0f, 0.5f, 0f, 0f, 132f, 132f, 0f, 0.5f);
            crest = UIFactory.Img(plate, "Crest", skin.circle, UIColors.Team(UIFactory.PlayerTeam));
            ((RectTransform)crest.transform).At(0f, 0.5f, 9f, 0f, 114f, 114f, 0f, 0.5f);
            bust = UIFactory.Img(plate, "Bust", skin.Bust(UIFactory.PlayerTeam), Color.white);
            ((RectTransform)bust.transform).At(0f, 0.5f, 14f, 2f, 104f, 104f, 0f, 0.5f);
            nameText = UIFactory.Text(plate, "Name", "Commander", 40f, TextStyle.Heading, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)nameText.transform).At(0f, 0.5f, 148f, 20f, 380f, 50f, 0f, 0.5f);
            var lvlPill = UIFactory.Img(plate, "LevelPill", skin.pill, UIColors.Xp, UIFactory.PillMult(40f));
            ((RectTransform)lvlPill.transform).At(0f, 0.5f, 148f, -26f, 92f, 40f, 0f, 0.5f);
            levelText = UIFactory.Text(lvlPill.transform, "Level", "Lv 1", 28f, TextStyle.HeadingThin);
            ((RectTransform)levelText.transform).Stretch(0f, 0f, 0f, 2f);
            xpBar = UIFactory.Bar(plate, "XpBar", 260f, 28f, UIColors.Xp, UIColors.WithAlpha(Color.black, 0.45f));
            ((RectTransform)xpBar.transform).At(0f, 0.5f, 252f, -26f, 260f, 28f, 0f, 0.5f);
            xpText = UIFactory.Text(xpBar.transform, "XpText", "", 20f, TextStyle.BodyOutline);
            ((RectTransform)xpText.transform).Stretch(0f, 0f, 0f, 1f);

            // Gold (top right).
            var g = UIFactory.Chip(rt, "Gold", UIIcon.Coin, 250f, 66f, out goldRoot);
            goldRoot.At(1f, 1f, -152f, -34f, 250f, 66f, 1f, 1f);
            gold = g.gameObject.AddComponent<UINumberTicker>();
            gold.label = g;
            Settings = UIFactory.IconButton(rt, "Settings", UIIcon.Gear, 96f, ButtonColor.Slate, 0.62f);
            ((RectTransform)Settings.transform).At(1f, 1f, -26f, -18f, 96f, 96f, 1f, 1f);
        }

        public void Refresh(bool animate)
        {
            var p = MetaGame.Profile;
            var prog = MetaGame.Progression;
            nameText.text = p.playerName;
            levelText.SetText("Lv {0}", p.level);
            int into = 0, span = 0;
            float k = prog != null ? prog.LevelProgress(p.xp, out into, out span) : 0f;
            if (prog != null && span > 0) xpText.SetText("{0} / {1}", into, span); else xpText.text = "MAX";
            xpBar.Set(k, animate);
            crest.color = UIColors.Team(p.team);
            bust.sprite = UIFactory.Skin.Bust(p.team);
            if (animate && lastGold >= 0 && p.gold != lastGold)
            {
                gold.TickTo(p.gold, 0.7f);
                UIAnim.Punch(goldRoot, 0.12f);
            }
            else gold.SetInstant(p.gold);
            lastGold = p.gold;
        }
    }
}
