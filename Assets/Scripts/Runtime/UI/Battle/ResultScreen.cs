using System.Collections;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Victory card (reference f_0086: white card, currency strip, stat icons with grade circles, medal
    /// with the grade letter) and Defeat card (our design). Rewards were already granted and saved by
    /// BattleSession; this only presents them with tick-ups and a level up moment.</summary>
    public class ResultScreen : MonoBehaviour
    {
        BattleHUD hud;
        RectTransform victory, defeat;
        UIPanelAnim vAnim, dAnim;

        // Victory widgets.
        UINumberTicker vGold, vXp, vLeague;
        TextMeshProUGUI survivorsValue, timeValue, killsValue, medalLetter, prismText;
        RectTransform[] gradeCircles = new RectTransform[2];
        TextMeshProUGUI[] gradeLetters = new TextMeshProUGUI[2];
        RectTransform medal, levelUp, prismRoot;
        TextMeshProUGUI levelUpText;

        // Defeat widgets.
        UINumberTicker dGold, dXp;
        TextMeshProUGUI dLeague, dStats;

        public bool Showing { get; private set; }
        public bool VictoryShown => victory != null && victory.gameObject.activeSelf;
        public bool DefeatShown => defeat != null && defeat.gameObject.activeSelf;
        public bool AnimationDone { get; private set; }
        public UIButton ContinueButton { get; private set; }
        public UIButton ReplayButton { get; private set; }
        public UIButton RetryButton { get; private set; }
        public UIButton HomeButton { get; private set; }

        static readonly string[] Tips =
        {
            "Tip: shoot the flag bearer, then tap its bubble to pop the whole squad at once.",
            "Tip: draw a line parallel to mines to slip past them.",
            "Tip: a tight circle keeps your soldiers together under cannon fire.",
            "Tip: tap the POW badge when it glows to call backup soldiers.",
        };

        public static ResultScreen Create(BattleHUD hud, Transform parent)
        {
            var rt = UIFactory.Rect(parent, "Results").Stretch();
            var r = rt.gameObject.AddComponent<ResultScreen>();
            r.hud = hud;
            r.BuildVictory(rt);
            r.BuildDefeat(rt);
            return r;
        }

        // ------------------------------------------------------------------ build

        void BuildVictory(RectTransform parent)
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;
            victory = UIFactory.Rect(parent, "Victory").Stretch();
            UIFactory.Dim(victory, 0.55f);
            var cardRoot = UIFactory.Rect(victory, "CardRoot").At(0.5f, 0.5f, 0f, -30f, 1240f, 760f);
            var face = UIFactory.Card(cardRoot, "Card", 1240f, 760f);
            ((RectTransform)face.parent).Stretch();
            var ribbon = UIFactory.Ribbon(cardRoot, "VICTORY!", UIColors.Team(team), 680f, 146f, 84f);
            ribbon.At(0.5f, 1f, 0f, 70f, 680f, 146f, 0.5f, 1f);

            // Currency strip.
            var strip = UIFactory.Panel(face, "Strip", UIColors.CardInner, 30f);
            var srt = ((RectTransform)strip.transform).At(0.5f, 1f, 0f, -92f, 1150f, 100f, 0.5f, 1f);
            vGold = Ticker(srt, "Gold", UIIcon.Coin, -380f, "x {0}");
            vXp = Ticker(srt, "Xp", UIIcon.Xp, -40f, "x {0}");
            vLeague = Ticker(srt, "League", UIIcon.Crown, 290f, "+{0}");
            prismRoot = UIFactory.Rect(srt, "Prism").At(1f, 0.5f, -20f, 0f, 150f, 80f, 1f, 0.5f);
            var pic = UIFactory.Icon(prismRoot, UIIcon.Prism, 64f);
            ((RectTransform)pic.transform).At(0f, 0.5f, 0f, 0f, 64f, 64f, 0f, 0.5f);
            prismText = UIFactory.Text(prismRoot, "Text", "+3", 40f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)prismText.transform).Stretch(70f, 0f, 0f, 2f);

            // Stat blocks.
            var stats = UIFactory.Rect(face, "Stats").At(0f, 1f, 50f, -216f, 760f, 300f, 0f, 1f);
            var statBg = UIFactory.Panel(stats, "Bg", UIColors.CardInner, 30f);
            ((RectTransform)statBg.transform).Stretch(0f, 40f, 0f, 92f);
            survivorsValue = StatBlock(stats, 0, UIIcon.Soldier, "SURVIVORS", true);
            timeValue = StatBlock(stats, 1, UIIcon.Clock, "TIME", true);
            killsValue = StatBlock(stats, 2, UIIcon.Skull, "KILLS", false);

            // Medal.
            medal = UIFactory.Rect(face, "Medal").At(1f, 1f, -70f, -196f, 320f, 360f, 1f, 1f);
            var tailL = UIFactory.Img(medal, "TailL", skin.rounded32, UIColors.TeamDark(team), UIFactory.Round32(10f));
            ((RectTransform)tailL.transform).At(0.5f, 0.5f, -44f, -120f, 64f, 170f);
            tailL.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            var tailR = UIFactory.Img(medal, "TailR", skin.rounded32, UIColors.Team(team), UIFactory.Round32(10f));
            ((RectTransform)tailR.transform).At(0.5f, 0.5f, 44f, -120f, 64f, 170f);
            tailR.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
            var mglow = UIFactory.Img(medal, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Gold, 0.55f));
            ((RectTransform)mglow.transform).At(0.5f, 0.5f, 0f, 30f, 420f, 420f);
            var mbase = UIFactory.Img(medal, "Base", skin.medal, UIColors.GoldDark);
            ((RectTransform)mbase.transform).At(0.5f, 0.5f, 0f, 22f, 276f, 276f);
            var mface = UIFactory.Img(medal, "Face", skin.medal, UIColors.Gold);
            ((RectTransform)mface.transform).At(0.5f, 0.5f, 0f, 30f, 262f, 262f);
            var mring = UIFactory.Img(medal, "Ring", skin.ringThin, UIColors.WithAlpha(Color.white, 0.65f));
            ((RectTransform)mring.transform).At(0.5f, 0.5f, 0f, 30f, 170f, 170f);
            medalLetter = UIFactory.Text(medal, "Letter", "A", 136f, TextStyle.Heading);
            ((RectTransform)medalLetter.transform).At(0.5f, 0.5f, 0f, 32f, 200f, 170f);
            var gl = UIFactory.Text(medal, "Label", "GRADE", 30f, TextStyle.HeadingThin);
            ((RectTransform)gl.transform).At(0.5f, 0.5f, 0f, -150f, 200f, 40f);

            // Level up banner.
            levelUp = UIFactory.Rect(face, "LevelUp").At(0.5f, 1f, -150f, -560f, 700f, 66f, 0.5f, 0.5f);
            var lbg = UIFactory.Img(levelUp, "Bg", skin.pill, UIColors.Xp, UIFactory.PillMult(70f));
            ((RectTransform)lbg.transform).Stretch();
            var lic = UIFactory.Icon(levelUp, UIIcon.Star, 84f);
            ((RectTransform)lic.transform).At(0f, 0.5f, -10f, 4f, 84f, 84f, 0f, 0.5f);
            levelUpText = UIFactory.Text(levelUp, "Text", "LEVEL UP!", 36f, TextStyle.Heading);
            ((RectTransform)levelUpText.transform).Stretch(80f, 0f, 24f, 3f);
            levelUpText.overflowMode = TextOverflowModes.Overflow;

            ReplayButton = UIFactory.Button(face, "Replay", "REPLAY", ButtonColor.Slate, 340f, 108f, UIIcon.Replay, 44f);
            ((RectTransform)ReplayButton.transform).At(0.5f, 0f, -190f, 26f, 340f, 108f, 0.5f, 0f);
            ReplayButton.onClick = Replay;
            ContinueButton = UIFactory.Button(face, "Continue", "CONTINUE", ButtonColor.Green, 360f, 108f, UIIcon.ArrowRight, 44f);
            ((RectTransform)ContinueButton.transform).At(0.5f, 0f, 190f, 26f, 360f, 108f, 0.5f, 0f);
            ContinueButton.onClick = Continue;

            victory.gameObject.AddComponent<CanvasGroup>();
            vAnim = victory.gameObject.AddComponent<UIPanelAnim>();
            vAnim.scaleTarget = cardRoot;
            vAnim.duration = 0.35f;
            victory.gameObject.SetActive(false);
        }

        UINumberTicker Ticker(RectTransform parent, string name, UIIcon icon, float x, string format)
        {
            var root = UIFactory.Rect(parent, name).At(0.5f, 0.5f, x, 0f, 260f, 80f, 0f, 0.5f);
            var ic = UIFactory.Icon(root, icon, 72f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 0f, 0f, 72f, 72f, 0f, 0.5f);
            var t = UIFactory.Text(root, "Value", "0", 50f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)t.transform).Stretch(86f, 0f, 0f, 2f);
            var tk = t.gameObject.AddComponent<UINumberTicker>();
            tk.label = t;
            tk.format = format;
            tk.SetInstant(0);
            return tk;
        }

        TextMeshProUGUI StatBlock(RectTransform parent, int i, UIIcon icon, string label, bool graded)
        {
            var skin = UIFactory.Skin;
            var root = UIFactory.Rect(parent, label).At(0f, 1f, 18f + i * 250f, 0f, 230f, 300f, 0f, 1f);
            var disc = UIFactory.Img(root, "Disc", skin.circle, Color.white);
            ((RectTransform)disc.transform).At(0.5f, 1f, 0f, 0f, 100f, 100f, 0.5f, 1f);
            var ic = UIFactory.Icon(disc.transform, icon, 70f);
            ((RectTransform)ic.transform).At(0.5f, 0.5f, 0f, 2f, 70f, 70f);
            var l = UIFactory.Text(root, "Label", label, 26f, TextStyle.HeadingDark);
            l.color = UIColors.Muted;
            ((RectTransform)l.transform).At(0.5f, 1f, 0f, -104f, 230f, 34f, 0.5f, 1f);
            var v = UIFactory.Text(root, "Value", "0", 46f, TextStyle.HeadingDark);
            ((RectTransform)v.transform).At(0.5f, 1f, 0f, -138f, 230f, 54f, 0.5f, 1f);
            if (graded)
            {
                var gc = UIFactory.Rect(root, "Grade").At(0.5f, 1f, 0f, -250f, 86f, 86f, 0.5f, 0.5f);
                var ring = UIFactory.Img(gc, "Ring", skin.ring, UIColors.Team(UIFactory.PlayerTeam));
                ((RectTransform)ring.transform).Stretch();
                var inner = UIFactory.Img(gc, "Inner", skin.circle, Color.white);
                ((RectTransform)inner.transform).Stretch(14f, 14f, 14f, 14f);
                var g = UIFactory.Text(gc, "Letter", "B", 50f, TextStyle.HeadingDark);
                g.color = UIColors.TeamDark(UIFactory.PlayerTeam);
                ((RectTransform)g.transform).Stretch(0f, 0f, 0f, 4f);
                gradeCircles[i] = gc;
                gradeLetters[i] = g;
            }
            return v;
        }

        void BuildDefeat(RectTransform parent)
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;
            defeat = UIFactory.Rect(parent, "Defeat").Stretch();
            UIFactory.Dim(defeat, 0.62f);
            var cardRoot = UIFactory.Rect(defeat, "CardRoot").At(0.5f, 0.5f, 0f, -30f, 1060f, 680f);
            var face = UIFactory.Card(cardRoot, "Card", 1060f, 680f);
            ((RectTransform)face.parent).Stretch();
            var ribbon = UIFactory.Ribbon(cardRoot, "DEFEAT", UIColors.Slate, 600f, 140f, 80f);
            ribbon.At(0.5f, 1f, 0f, 66f, 600f, 140f, 0.5f, 1f);

            var disc = UIFactory.Img(face, "Disc", skin.circle, UIColors.CardInner);
            ((RectTransform)disc.transform).At(0f, 1f, 60f, -100f, 260f, 260f, 0f, 1f);
            var bust = UIFactory.Img(disc.transform, "Bust", skin.Bust(team), new Color(0.78f, 0.78f, 0.82f, 1f));
            ((RectTransform)bust.transform).At(0.5f, 0.5f, 0f, 4f, 230f, 230f);
            bust.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            var splat = UIFactory.Img(disc.transform, "Splat", skin.splat, UIColors.WithAlpha(UIColors.Team(BattleContext.Config != null ? BattleContext.Config.enemyTeam : Core.TeamColorId.Teal), 0.9f));
            ((RectTransform)splat.transform).At(1f, 1f, -10f, -10f, 110f, 110f, 1f, 1f);
            var title = UIFactory.Text(face, "Title", "Your squad was wiped out", 46f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)title.transform).At(0f, 1f, 360f, -104f, 640f, 60f, 0f, 1f);
            var tip = UIFactory.Paragraph(face, "Tip", Tips[0], 28f, UIColors.Muted);
            ((RectTransform)tip.transform).At(0f, 1f, 360f, -176f, 620f, 110f, 0f, 1f);
            dStats = UIFactory.Text(face, "Stats", "", 28f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)dStats.transform).At(0f, 1f, 360f, -296f, 620f, 40f, 0f, 1f);

            var strip = UIFactory.Panel(face, "Strip", UIColors.CardInner, 30f);
            var srt = ((RectTransform)strip.transform).At(0.5f, 0f, 0f, 160f, 960f, 96f, 0.5f, 0f);
            var lbl = UIFactory.Text(srt, "Label", "CONSOLATION", 28f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            lbl.color = UIColors.Muted;
            ((RectTransform)lbl.transform).At(0f, 0.5f, 30f, 0f, 240f, 40f, 0f, 0.5f);
            dGold = Ticker(srt, "Gold", UIIcon.Coin, -190f, "x {0}");
            dXp = Ticker(srt, "Xp", UIIcon.Xp, 50f, "x {0}");
            var lr = UIFactory.Rect(srt, "League").At(1f, 0.5f, -26f, 0f, 160f, 80f, 1f, 0.5f);
            var lic = UIFactory.Icon(lr, UIIcon.Crown, 64f);
            ((RectTransform)lic.transform).At(0f, 0.5f, 0f, 0f, 64f, 64f, 0f, 0.5f);
            dLeague = UIFactory.Text(lr, "Value", "-0", 44f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)dLeague.transform).Stretch(72f, 0f, 0f, 2f);

            HomeButton = UIFactory.Button(face, "Home", "BACK TO BASE", ButtonColor.Slate, 400f, 108f, UIIcon.Home, 42f);
            ((RectTransform)HomeButton.transform).At(0.5f, 0f, -215f, 28f, 400f, 108f, 0.5f, 0f);
            HomeButton.onClick = Continue;
            RetryButton = UIFactory.Button(face, "Retry", "RETRY", ButtonColor.Green, 360f, 108f, UIIcon.Replay, 46f);
            ((RectTransform)RetryButton.transform).At(0.5f, 0f, 215f, 28f, 360f, 108f, 0.5f, 0f);
            RetryButton.onClick = Replay;

            defeat.gameObject.AddComponent<CanvasGroup>();
            dAnim = defeat.gameObject.AddComponent<UIPanelAnim>();
            dAnim.scaleTarget = cardRoot;
            dAnim.duration = 0.35f;
            defeat.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ show

        public void Show(RewardSummary s)
        {
            if (Showing || s == null) return;
            Showing = true;
            AnimationDone = false;
            StartCoroutine(s.victory ? VictorySequence(s) : DefeatSequence(s));
        }

        /// <summary>Battle ended without a BattleSession (should not happen in built scenes): show raw numbers.</summary>
        public void ShowFallback(BattleResult r)
        {
            StartCoroutine(FallbackAfter(r));
        }

        IEnumerator FallbackAfter(BattleResult r)
        {
            float t = 0f;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; yield return null; }
            if (!Showing) Show(new RewardSummary { result = r, victory = r.victory, gold = r.gold, xp = r.xp, grade = "B", survivorGrade = "B", speedGrade = "B" });
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator VictorySequence(RewardSummary s)
        {
            yield return Wait(1.4f); // let the squad cheer first
            var r = s.result;
            survivorsValue.text = r.soldiersSurvived + "/" + (r.soldiersSurvived + r.soldiersLost);
            timeValue.text = Clock(r.timeSeconds);
            killsValue.text = (r.kills + r.structuresDestroyed).ToString();
            gradeLetters[0].text = s.survivorGrade;
            gradeLetters[1].text = s.speedGrade;
            medalLetter.text = s.grade;
            vGold.SetInstant(0); vXp.SetInstant(0); vLeague.SetInstant(0);
            prismRoot.gameObject.SetActive(s.prisms > 0);
            prismText.text = "+" + s.prisms;
            levelUp.gameObject.SetActive(false);
            foreach (var g in gradeCircles) if (g != null) g.localScale = Vector3.zero;
            medal.localScale = Vector3.zero;
            vAnim.Show();
            yield return Wait(0.45f);
            vGold.TickTo(s.gold, 1.0f);
            vXp.TickTo(s.xp, 1.0f, 0.15f);
            vLeague.TickTo(Mathf.Max(0, s.leagueDelta), 0.8f, 0.3f);
            AudioManager.Play(SoundId.CoinPickup, Vector3.zero);
            yield return Wait(0.5f);
            AudioManager.Play(SoundId.CoinPickup, Vector3.zero);
            yield return Wait(0.6f);
            for (int i = 0; i < gradeCircles.Length; i++)
            {
                if (gradeCircles[i] == null) continue;
                UIAnim.Pop(gradeCircles[i], 0.2f, 0.3f);
                AudioManager.Play(SoundId.UiClick, Vector3.zero);
                yield return Wait(0.22f);
            }
            yield return Wait(0.15f);
            UIAnim.Run(medal, UIAnim.Tween(0.4f, k =>
            {
                float sc = Mathf.LerpUnclamped(2.2f, 1f, UIAnim.OutBack(k));
                medal.localScale = new Vector3(sc, sc, 1f);
            }, null, 0f, medal));
            AudioManager.Play(SoundId.CaptureComplete, Vector3.zero);
            yield return Wait(0.5f);
            if (s.LeveledUp)
            {
                string extra = (s.levelUpGold > 0 ? "  +" + s.levelUpGold + " gold" : "") + (s.levelUpPrisms > 0 ? "  +" + s.levelUpPrisms + " prisms" : "");
                levelUpText.text = "LEVEL UP! LEVEL " + s.levelAfter + extra;
                float w = levelUpText.GetPreferredValues(levelUpText.text).x + 130f;
                levelUp.sizeDelta = new Vector2(Mathf.Max(420f, w), 66f);
                levelUp.gameObject.SetActive(true);
                UIAnim.Pop(levelUp, 0.3f, 0.45f);
                AudioManager.Play(SoundId.LevelUp, Vector3.zero);
            }
            AnimationDone = true;
        }

        IEnumerator DefeatSequence(RewardSummary s)
        {
            yield return Wait(1.0f);
            var r = s.result;
            dStats.text = "Popped " + r.kills + " enemies in " + Clock(r.timeSeconds);
            var tipT = defeat.Find("CardRoot/Card/Face/Tip");
            if (tipT != null) tipT.GetComponent<TextMeshProUGUI>().text = Tips[Random.Range(0, Tips.Length)];
            dLeague.text = s.leagueDelta.ToString();
            dGold.SetInstant(0); dXp.SetInstant(0);
            dAnim.Show();
            yield return Wait(0.4f);
            dGold.TickTo(s.gold, 0.8f);
            dXp.TickTo(s.xp, 0.8f, 0.1f);
            yield return Wait(0.9f);
            AnimationDone = true;
        }

        // ------------------------------------------------------------------ buttons

        public void Continue()
        {
            BattleLaunch.Clear();
            ScreenTransition.Load(BattleLaunch.MenuScene, UIFactory.PlayerTeam);
        }

        public void Replay()
        {
            var b = BattleLaunch.CurrentBase;
            if (b == null && MetaGame.Db != null && MetaGame.Db.roster != null && MetaGame.Db.roster.bases.Length > 0) b = MetaGame.Db.roster.bases[0];
            if (b != null) BattleLaunch.Prepare(b);
            ScreenTransition.Load(BattleLaunch.BattleScene, UIFactory.PlayerTeam);
        }
    }
}
