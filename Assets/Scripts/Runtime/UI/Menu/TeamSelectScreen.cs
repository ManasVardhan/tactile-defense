using TactileWars.Core;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>First run (and later from Profile) faction pick: six colors, each showing the real soldier model
    /// rendered in that color. Stores the team in the profile and recolors the squad and UI accents.</summary>
    public class TeamSelectScreen : MonoBehaviour
    {
        MenuRoot menu;
        UIPanelAnim anim;
        readonly RectTransform[] cards = new RectTransform[6];
        readonly RectTransform[] visuals = new RectTransform[6];
        readonly Image[] rims = new Image[6];
        readonly GameObject[] checks = new GameObject[6];
        TMP_InputField nameInput;
        RectTransform nameRow;
        UIButton confirm, close;
        TextMeshProUGUI subtitle;
        TeamColorId selected;
        bool firstRun;

        public TeamColorId Selected => selected;
        public UIButton ConfirmButton => confirm;
        public bool IsOpen => anim != null && anim.IsShown;

        public static TeamSelectScreen Create(MenuRoot menu, Transform parent)
        {
            var rt = UIFactory.Rect(parent, "TeamSelect").Stretch();
            var s = rt.gameObject.AddComponent<TeamSelectScreen>();
            s.menu = menu;
            s.BuildUI(rt);
            rt.gameObject.SetActive(false);
            return s;
        }

        void BuildUI(RectTransform rt)
        {
            var skin = UIFactory.Skin;
            var dim = UIFactory.Dim(rt, 0.7f);
            var content = UIFactory.Rect(rt, "Content").At(0.5f, 0.5f, 0f, 0f, 1800f, 1000f);
            var ribbon = UIFactory.Ribbon(content, "CHOOSE YOUR COLOR", UIColors.Slate, 900f, 124f, 62f);
            ribbon.At(0.5f, 1f, 0f, -18f, 900f, 124f, 0.5f, 1f);
            subtitle = UIFactory.Text(content, "Subtitle", "Your faction paints your squad, your ring and your banners.", 32f, TextStyle.BodyOutline);
            ((RectTransform)subtitle.transform).At(0.5f, 1f, 0f, -150f, 1400f, 44f, 0.5f, 1f);

            float w = 250f, h = 380f, gap = 28f;
            float total = 6 * w + 5 * gap;
            for (int i = 0; i < 6; i++)
            {
                var team = UIColors.Factions[i];
                var cr = UIFactory.Rect(content, "Card_" + team).At(0.5f, 0.5f, -total / 2f + w / 2f + i * (w + gap), 40f, w, h);
                var face = UIFactory.Card(cr, "Card", w, h, 32f);
                ((RectTransform)face.parent).Stretch();
                rims[i] = face.parent.Find("Rim").GetComponent<Image>();
                visuals[i] = (RectTransform)face.parent;
                var disc = UIFactory.Img(face, "Disc", skin.circle, UIColors.Team(team));
                ((RectTransform)disc.transform).At(0.5f, 1f, 0f, -26f, 196f, 196f, 0.5f, 1f);
                var ring = UIFactory.Img(disc.transform, "Ring", skin.ringThin, UIColors.WithAlpha(Color.white, 0.6f));
                ((RectTransform)ring.transform).Stretch(8f, 8f, 8f, 8f);
                var sol = UIFactory.Img(face, "Soldier", skin.Soldier(team), Color.white);
                ((RectTransform)sol.transform).At(0.5f, 1f, 0f, -6f, 236f, 270f, 0.5f, 1f);
                var label = UIFactory.Text(face, "Name", UIColors.TeamName(team).ToUpperInvariant(), 44f, TextStyle.HeadingDark);
                ((RectTransform)label.transform).At(0.5f, 0f, 0f, 60f, 230f, 56f, 0.5f, 0.5f);
                label.color = UIColors.TeamDark(team);
                var pill = UIFactory.Img(face, "Swatch", skin.pill, UIColors.Team(team), UIFactory.PillMult(16f));
                ((RectTransform)pill.transform).At(0.5f, 0f, 0f, 22f, 120f, 16f, 0.5f, 0.5f);
                var check = UIFactory.Img(face.parent, "Check", skin.circle, UIColors.Green);
                ((RectTransform)check.transform).At(1f, 1f, 6f, 6f, 76f, 76f, 0.5f, 0.5f);
                var ci = UIFactory.Icon(check.transform, UIIcon.Check, 56f);
                ((RectTransform)ci.transform).At(0.5f, 0.5f, 0f, 2f, 56f, 56f);
                checks[i] = check.gameObject;
                var hit = cr.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0f);
                hit.raycastTarget = true;
                var b = cr.gameObject.AddComponent<UIButton>();
                int idx = i;
                b.onClick = () => Select(UIColors.Factions[idx]);
                cards[i] = cr;
            }

            nameRow = UIFactory.Rect(content, "NameRow").At(0.5f, 0f, 0f, 200f, 900f, 80f, 0.5f, 0.5f);
            var nl = UIFactory.Text(nameRow, "Label", "COMMANDER NAME", 34f, TextStyle.Heading, TextAlignmentOptions.MidlineRight);
            ((RectTransform)nl.transform).At(0.5f, 0.5f, -30f, 0f, 400f, 60f, 1f, 0.5f);
            nameInput = UIFactory.Input(nameRow, "NameInput", 440f, 76f, "Commander");
            ((RectTransform)nameInput.transform).At(0.5f, 0.5f, -10f, 0f, 440f, 76f, 0f, 0.5f);

            confirm = UIFactory.Button(content, "Confirm", "READY!", ButtonColor.Green, 460f, 124f, UIIcon.Check, 56f);
            ((RectTransform)confirm.transform).At(0.5f, 0f, 0f, 46f, 460f, 124f, 0.5f, 0f);
            confirm.sound = Audio.SoundId.UiPurchase;
            confirm.onClick = Confirm;
            close = UIFactory.IconButton(content, "Close", UIIcon.Close, 96f, ButtonColor.Slate);
            ((RectTransform)close.transform).At(1f, 1f, -60f, -30f, 96f, 96f, 0.5f, 0.5f);
            close.sound = Audio.SoundId.UiBack;
            close.onClick = () => anim.Hide();

            rt.gameObject.AddComponent<CanvasGroup>();
            anim = rt.gameObject.AddComponent<UIPanelAnim>();
            anim.scaleTarget = content;
        }

        public void Open(bool isFirstRun, bool instant = false)
        {
            firstRun = isFirstRun;
            var p = MetaGame.Profile;
            nameRow.gameObject.SetActive(isFirstRun);
            close.gameObject.SetActive(!isFirstRun);
            nameInput.text = p.playerName;
            subtitle.text = isFirstRun ? "Pick a faction. It paints your squad, your ring and your banners." : "Switch faction. Your progress stays with you.";
            selected = p.teamChosen ? p.team : TeamColorId.Red;
            Select(selected, false);
            transform.SetAsLastSibling();
            anim.Show(instant);
        }

        public void Select(TeamColorId team, bool feedback = true)
        {
            selected = team;
            for (int i = 0; i < 6; i++)
            {
                bool on = UIColors.Factions[i] == team;
                checks[i].SetActive(on);
                rims[i].color = on ? UIColors.Team(team) : UIColors.CardRim;
                visuals[i].localScale = on ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
                visuals[i].anchoredPosition = new Vector2(0f, on ? 16f : 0f);
            }
            if (feedback)
            {
                int k = System.Array.IndexOf(UIColors.Factions, team);
                if (k >= 0) UIAnim.Pop(checks[k].transform, 0.3f, 0.3f);
            }
            confirm.SetColors(UIColors.Team(team), UIColors.TeamDark(team));
        }

        public void Confirm()
        {
            if (firstRun && nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text)) MetaGame.SetName(nameInput.text);
            bool changed = MetaGame.Profile.team != selected || !MetaGame.Profile.teamChosen;
            MetaGame.SetTeam(selected);
            anim.Hide(true);
            if (changed) menu.RebuildForTeam();
            menu.ShowToast("Welcome to the " + UIColors.TeamName(selected) + " faction!", UIIcon.Flag, UIColors.TeamDark(selected));
        }
    }
}
