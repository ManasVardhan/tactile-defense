using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>"While you were away" card for a SIMULATED offline raid (clearly labeled: not a real player).
    /// Shows the attacker, the result, the gold change and the defenses consumed.</summary>
    public class RaidReportCard : MonoBehaviour
    {
        MenuRoot menu;
        UIPanelAnim anim;
        Image bust, splat, resultPlate;
        TextMeshProUGUI attackerText, resultText, goldText, lostText;
        RectTransform lostRow;
        public UIButton OkButton { get; private set; }
        public UIButton EditButton { get; private set; }
        public bool IsOpen => anim != null && anim.IsShown;

        public static RaidReportCard Create(MenuRoot menu, Transform parent)
        {
            var rt = UIFactory.Rect(parent, "RaidReport").Stretch();
            var c = rt.gameObject.AddComponent<RaidReportCard>();
            c.menu = menu;
            c.Build(rt);
            return c;
        }

        void Build(RectTransform rt)
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;
            UIFactory.Dim(rt, 0.6f);
            var cardRoot = UIFactory.Rect(rt, "CardRoot").At(0.5f, 0.5f, 0f, -30f, 1120f, 660f);
            var face = UIFactory.Card(cardRoot, "Card", 1120f, 660f);
            ((RectTransform)face.parent).Stretch();
            var ribbon = UIFactory.Ribbon(cardRoot, "WHILE YOU WERE AWAY", UIColors.Team(team), 820f, 124f, 58f);
            ribbon.At(0.5f, 1f, 0f, 58f, 820f, 124f, 0.5f, 1f);
            var sim = UIFactory.Tag(face, "Simulated", "SIMULATED OFFLINE RAID: NOT A REAL PLAYER", UIColors.Slate, 40f, 22f);
            sim.At(0.5f, 1f, 0f, -80f, sim.sizeDelta.x, 40f, 0.5f, 1f);

            var disc = UIFactory.Img(face, "Disc", skin.circle, UIColors.CardInner);
            ((RectTransform)disc.transform).At(0f, 1f, 56f, -146f, 250f, 250f, 0f, 1f);
            bust = UIFactory.Img(disc.transform, "Bust", skin.Bust(TeamColorId.Teal), Color.white);
            ((RectTransform)bust.transform).At(0.5f, 0.5f, 0f, 6f, 220f, 220f);
            splat = UIFactory.Img(disc.transform, "Splat", skin.splat, Color.white);
            ((RectTransform)splat.transform).At(1f, 1f, -6f, -6f, 96f, 96f, 1f, 1f);

            attackerText = UIFactory.Text(face, "Attacker", "", 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)attackerText.transform).At(0f, 1f, 340f, -150f, 740f, 52f, 0f, 1f);
            resultPlate = UIFactory.Img(face, "ResultPlate", skin.pill, UIColors.Green, UIFactory.PillMult(76f));
            ((RectTransform)resultPlate.transform).At(0f, 1f, 340f, -214f, 520f, 76f, 0f, 1f);
            resultText = UIFactory.Text(resultPlate.transform, "Result", "", 40f, TextStyle.Heading);
            ((RectTransform)resultText.transform).Stretch(0f, 0f, 0f, 4f);
            goldText = UIFactory.IconValue(face, "Gold", UIIcon.Coin, "+0", 44f, TextStyle.HeadingDark, out var grt);
            grt.At(0f, 1f, 880f, -220f, 200f, 64f, 0f, 1f);

            lostText = UIFactory.Text(face, "LostHead", "", 28f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)lostText.transform).At(0f, 1f, 340f, -318f, 740f, 36f, 0f, 1f);
            lostRow = UIFactory.Rect(face, "Lost").At(0f, 1f, 340f, -360f, 740f, 92f, 0f, 1f);

            OkButton = UIFactory.Button(face, "Ok", "OK", ButtonColor.Green, 300f, 100f, UIIcon.Check, 42f);
            ((RectTransform)OkButton.transform).At(0.5f, 0f, 180f, 30f, 300f, 100f, 0.5f, 0f);
            OkButton.onClick = Close;
            EditButton = UIFactory.Button(face, "Edit", "EDIT BASE", ButtonColor.Blue, 340f, 100f, UIIcon.Pencil, 38f);
            ((RectTransform)EditButton.transform).At(0.5f, 0f, -180f, 30f, 340f, 100f, 0.5f, 0f);
            EditButton.onClick = () => { Close(); if (BaseDefenseScreen.EditorAvailable) ScreenTransition.Load(BaseDefenseScreen.EditorScene, UIFactory.PlayerTeam); };

            rt.gameObject.AddComponent<CanvasGroup>();
            anim = rt.gameObject.AddComponent<UIPanelAnim>();
            anim.scaleTarget = cardRoot;
            rt.gameObject.SetActive(false);
        }

        public void Show(RaidRecord r)
        {
            if (r == null) return;
            var skin = UIFactory.Skin;
            bust.sprite = skin.Bust(r.attackerTeam);
            splat.color = UIColors.WithAlpha(UIColors.Team(r.held ? UIFactory.PlayerTeam : r.attackerTeam), 0.9f);
            attackerText.text = r.attackerName + " (" + UIColors.TeamName(r.attackerTeam) + ") attacked";
            resultText.text = r.held ? "YOUR DEFENSE HELD!" : "YOUR BASE FELL";
            resultPlate.color = r.held ? UIColors.Green : UIColors.Red;
            goldText.text = (r.goldDelta >= 0 ? "+" : "") + r.goldDelta;
            goldText.color = r.goldDelta >= 0 ? UIColors.GreenDark : UIColors.RedDark;
            int n = r.consumed.Count;
            lostText.text = n == 0 ? "No defenses lost" : n + (n == 1 ? " defense consumed: rebuy it in the base editor" : " defenses consumed: rebuy them in the base editor");
            UIFactory.ClearChildren(lostRow);
            for (int i = 0; i < n && i < 9; i++)
            {
                var cell = UIFactory.Panel(lostRow, "L" + i, UIColors.CardInner, 16f);
                var crt = ((RectTransform)cell.transform).At(0f, 0.5f, i * 82f, 0f, 76f, 76f, 0f, 0.5f);
                string id = r.consumed[i];
                var sp = id == DefenseCatalog.SquadId ? skin.Bust(UIFactory.PlayerTeam) : skin.Defense(id, UIFactory.PlayerTeam);
                var img = UIFactory.Img(crt, "R", sp, new Color(0.62f, 0.62f, 0.66f, 1f));
                ((RectTransform)img.transform).Stretch(4f, 4f, 4f, 4f);
                var x = UIFactory.Icon(crt, UIIcon.Close, 30f);
                ((RectTransform)x.transform).At(1f, 0f, 4f, -4f, 30f, 30f, 1f, 0f);
            }
            transform.SetAsLastSibling();
            anim.Show();
            Audio.AudioManager.Play(r.held ? Audio.SoundId.CaptureComplete : Audio.SoundId.UiError, Vector3.zero);
        }

        public void Close()
        {
            anim.Hide();
            if (menu != null && menu.Current != null) menu.Current.Refresh();
        }
    }
}
