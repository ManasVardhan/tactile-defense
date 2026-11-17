using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Home top bar: crest, player name, level with XP bar, league, gold and prisms (tick-up on change),
    /// settings button.</summary>
    public class TopBar : MonoBehaviour
    {
        MenuRoot menu;
        Image crest, crestRing;
        Image bust;
        TextMeshProUGUI nameText, levelText, xpText, leagueText;
        UIProgressBar xpBar;
        UINumberTicker gold, prisms;
        RectTransform goldRoot, prismRoot;
        Image leagueIcon;
        int lastGold = -1, lastPrisms = -1;

        public UIButton Settings { get; private set; }
        /// <summary>League pill: home only (faded while a screen is open so it never collides with screen cards).</summary>
        public CanvasGroup LeagueGroup { get; private set; }
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
            crestRing = UIFactory.Img(plate, "CrestRim", skin.hex, UIColors.Card);
            ((RectTransform)crestRing.transform).At(0f, 0.5f, 0f, 0f, 132f, 132f, 0f, 0.5f);
            crest = UIFactory.Img(plate, "Crest", skin.hex, UIColors.Team(UIFactory.PlayerTeam));
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

            // League badge under the plate.
            var league = UIFactory.Rect(rt, "League").At(0f, 1f, 160f, -150f, 360f, 46f);
            LeagueGroup = league.gameObject.AddComponent<CanvasGroup>();
            var lbg = UIFactory.Img(league, "Bg", skin.pill, UIColors.WithAlpha(UIColors.Ink, 0.62f), UIFactory.PillMult(46f));
            ((RectTransform)lbg.transform).Stretch();
            leagueIcon = UIFactory.Icon(league, UIIcon.Crown, 40f);
            ((RectTransform)leagueIcon.transform).At(0f, 0.5f, 12f, 1f, 40f, 40f, 0f, 0.5f);
            leagueText = UIFactory.Text(league, "Text", "Bronze League", 26f, TextStyle.BodyOutline, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)leagueText.transform).Stretch(60f, 0f, 14f, 2f);

            // Currencies (top right).
            var g = UIFactory.Chip(rt, "Gold", UIIcon.Coin, 250f, 66f, out goldRoot);
            goldRoot.At(1f, 1f, -152f, -34f, 250f, 66f, 1f, 1f);
            gold = g.gameObject.AddComponent<UINumberTicker>();
            gold.label = g;
            var p = UIFactory.Chip(rt, "Prisms", UIIcon.Prism, 200f, 66f, out prismRoot);
            prismRoot.At(1f, 1f, -432f, -34f, 200f, 66f, 1f, 1f);
            prisms = p.gameObject.AddComponent<UINumberTicker>();
            prisms.label = p;
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
            var lg = MetaGame.CurrentLeague;
            leagueText.text = lg.name + "  <color=#FFD56A>" + p.leaguePoints + "</color>";
            leagueIcon.color = Color.Lerp(Color.white, lg.color, 0.7f);
            if (animate && lastGold >= 0 && p.gold != lastGold)
            {
                gold.TickTo(p.gold, 0.7f);
                UIAnim.Punch(goldRoot, 0.12f);
            }
            else gold.SetInstant(p.gold);
            if (animate && lastPrisms >= 0 && p.prisms != lastPrisms)
            {
                prisms.TickTo(p.prisms, 0.7f);
                UIAnim.Punch(prismRoot, 0.12f);
            }
            else prisms.SetInstant(p.prisms);
            lastGold = p.gold;
            lastPrisms = p.prisms;
        }
    }
}
