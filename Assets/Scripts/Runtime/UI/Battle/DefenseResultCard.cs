using System.Collections;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Result of a defense battle: held or fell, attackers popped, points lost, time, defenses consumed and
    /// the rewards (already granted and saved). Tests show the result only (no rewards, nothing consumed).</summary>
    public class DefenseResultCard : MonoBehaviour
    {
        BattleHUD hud;
        UIPanelAnim anim;
        RectTransform ribbonRt, lostRow;
        Image ribbon, bust, splat;
        TextMeshProUGUI ribbonText, tagText, headText, statsText, lostText, goldText, xpText;
        RectTransform tagRt, rewards;
        public UIButton ContinueButton { get; private set; }
        public UIButton ReplayButton { get; private set; }
        public bool Shown => anim != null && anim.IsShown;
        public DefenseSummary Summary { get; private set; }

        public static DefenseResultCard Create(BattleHUD hud, Transform parent)
        {
            var rt = UIFactory.Rect(parent, "DefenseResult").Stretch();
            var c = rt.gameObject.AddComponent<DefenseResultCard>();
            c.hud = hud;
            c.Build(rt);
            return c;
        }

        void Build(RectTransform rt)
        {
            var skin = UIFactory.Skin;
            UIFactory.Dim(rt, 0.58f);
            var cardRoot = UIFactory.Rect(rt, "CardRoot").At(0.5f, 0.5f, 0f, -30f, 1180f, 700f);
            var face = UIFactory.Card(cardRoot, "Card", 1180f, 700f);
            ((RectTransform)face.parent).Stretch();
            ribbonRt = UIFactory.Ribbon(cardRoot, "DEFENSE HELD!", UIColors.Team(UIFactory.PlayerTeam), 760f, 140f, 76f);
            ribbonRt.At(0.5f, 1f, 0f, 66f, 760f, 140f, 0.5f, 1f);
            ribbon = ribbonRt.GetComponent<Image>();
            ribbonText = ribbonRt.GetComponentInChildren<TextMeshProUGUI>();
            tagRt = UIFactory.Tag(face, "Tag", "SIMULATED AI ATTACKER", UIColors.Slate, 40f, 22f);
            tagRt.At(0.5f, 1f, 0f, -88f, tagRt.sizeDelta.x, 40f, 0.5f, 1f);
            tagText = tagRt.GetComponentInChildren<TextMeshProUGUI>();

            var disc = UIFactory.Img(face, "Disc", skin.circle, UIColors.CardInner);
            ((RectTransform)disc.transform).At(0f, 1f, 60f, -150f, 240f, 240f, 0f, 1f);
            bust = UIFactory.Img(disc.transform, "Bust", skin.Bust(TeamColorId.Teal), Color.white);
            ((RectTransform)bust.transform).At(0.5f, 0.5f, 0f, 6f, 210f, 210f);
            splat = UIFactory.Img(disc.transform, "Splat", skin.splat, Color.white);
            ((RectTransform)splat.transform).At(1f, 1f, -6f, -6f, 92f, 92f, 1f, 1f);

            headText = UIFactory.Text(face, "Head", "", 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)headText.transform).At(0f, 1f, 340f, -150f, 780f, 52f, 0f, 1f);
            statsText = UIFactory.Paragraph(face, "Stats", "", 29f, UIColors.Muted);
            ((RectTransform)statsText.transform).At(0f, 1f, 342f, -210f, 780f, 120f, 0f, 1f);
            lostText = UIFactory.Text(face, "LostHead", "", 27f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)lostText.transform).At(0f, 1f, 340f, -336f, 780f, 36f, 0f, 1f);
            lostRow = UIFactory.Rect(face, "Lost").At(0f, 1f, 340f, -376f, 780f, 84f, 0f, 1f);

            rewards = UIFactory.Rect(face, "Rewards").At(0.5f, 0f, 0f, 150f, 600f, 80f, 0.5f, 0f);
            var rb = UIFactory.Panel(rewards, "Bg", UIColors.CardInner, 28f);
            ((RectTransform)rb.transform).Stretch();
            goldText = UIFactory.IconValue(rewards, "Gold", UIIcon.Coin, "+0", 40f, TextStyle.HeadingDark, out var grt);
            grt.At(0.3f, 0.5f, 0f, 0f, 200f, 70f, 0.5f, 0.5f);
            xpText = UIFactory.IconValue(rewards, "Xp", UIIcon.Xp, "+0", 40f, TextStyle.HeadingDark, out var xrt);
            xrt.At(0.7f, 0.5f, 0f, 0f, 200f, 70f, 0.5f, 0.5f);

            ContinueButton = UIFactory.Button(face, "Continue", "BACK TO BASE", ButtonColor.Green, 420f, 104f, UIIcon.Home, 38f);
            ((RectTransform)ContinueButton.transform).At(0.5f, 0f, -230f, 28f, 420f, 104f, 0.5f, 0f);
            ContinueButton.onClick = Continue;
            ReplayButton = UIFactory.Button(face, "Replay", "REPLAY", ButtonColor.Blue, 340f, 104f, UIIcon.Replay, 40f);
            ((RectTransform)ReplayButton.transform).At(0.5f, 0f, 230f, 28f, 340f, 104f, 0.5f, 0f);
            ReplayButton.onClick = Replay;

            rt.gameObject.AddComponent<CanvasGroup>();
            anim = rt.gameObject.AddComponent<UIPanelAnim>();
            anim.scaleTarget = cardRoot;
            anim.duration = 0.35f;
            rt.gameObject.SetActive(false);
        }

        public void Show(DefenseSummary s)
        {
            if (s == null) return;
            Summary = s;
            // This card is inactive until shown: run the sequence on the (active) HUD.
            if (hud != null) hud.StartCoroutine(Sequence(s));
            else { gameObject.SetActive(true); StartCoroutine(Sequence(s)); }
        }

        IEnumerator Sequence(DefenseSummary s)
        {
            float t = 0f;
            while (t < 1.2f) { t += Time.unscaledDeltaTime; yield return null; }
            var skin = UIFactory.Skin;
            var mine = UIFactory.PlayerTeam;
            ribbonText.text = s.held ? "DEFENSE HELD!" : "BASE FELL";
            ribbon.color = s.held ? UIColors.Team(mine) : UIColors.Slate;
            tagText.text = s.test ? "TEST ATTACK: NO REWARDS, NOTHING CONSUMED" : "SIMULATED AI ATTACKER";
            tagRt.sizeDelta = new Vector2(tagText.GetPreferredValues(tagText.text).x + 40f, 40f);
            bust.sprite = skin.Bust(s.attackerTeam);
            splat.color = UIColors.WithAlpha(UIColors.Team(s.held ? mine : s.attackerTeam), 0.9f);
            headText.text = s.attackerName + " attacked with " + s.attackersTotal + " soldiers";
            int sec = Mathf.RoundToInt(s.timeSeconds);
            statsText.text = "Attackers popped " + s.attackersPopped + " / " + s.attackersTotal + "\nPoints lost " + s.pointsLost + " / 2" +
                "   Time " + (sec / 60) + ":" + (sec % 60).ToString("00") + (s.timedOut ? "  (attackers retreated)" : "");
            int n = s.consumed.Count;
            lostText.text = s.test ? "Test attack: your defenses are untouched" : (n == 0 ? "No defenses consumed" : n + " consumed: rebuy for " + s.rebuyCost + " gold in the base editor");
            UIFactory.ClearChildren(lostRow);
            for (int i = 0; i < n && i < 9; i++)
            {
                var cell = UIFactory.Panel(lostRow, "L" + i, UIColors.CardInner, 16f);
                var crt = ((RectTransform)cell.transform).At(0f, 0.5f, i * 86f, 0f, 80f, 80f, 0f, 0.5f);
                string id = s.consumed[i];
                var img = UIFactory.Img(crt, "R", id == DefenseCatalog.SquadId ? skin.Bust(mine) : skin.Defense(id, mine), new Color(0.62f, 0.62f, 0.66f, 1f));
                ((RectTransform)img.transform).Stretch(4f, 4f, 4f, 4f);
            }
            rewards.gameObject.SetActive(!s.test);
            goldText.text = "+" + s.gold;
            xpText.text = "+" + s.xp;
            var l = ContinueButton.Label();
            if (l != null) l.text = s.test ? "BACK TO EDITOR" : "BACK TO BASE";
            transform.SetAsLastSibling();
            anim.Show();
            Audio.AudioManager.Play(s.held ? Audio.SoundId.CaptureComplete : Audio.SoundId.Defeat, Vector3.zero);
        }

        public void Continue()
        {
            string scene = BattleLaunch.ReturnScene;
            BattleLaunch.Clear();
            ScreenTransition.Load(scene, UIFactory.PlayerTeam);
        }

        public void Replay()
        {
            bool test = Summary != null && Summary.test;
            BattleLaunch.PrepareDefense(test);
            ScreenTransition.Load(BattleLaunch.BattleScene, UIFactory.PlayerTeam);
        }
    }
}
