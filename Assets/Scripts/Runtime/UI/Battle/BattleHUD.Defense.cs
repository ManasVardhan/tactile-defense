using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Defense battle additions to the HUD: a watch speed control (1x, 2x, 4x), the optional paint strike
    /// button, a "defending" banner with a SIMULATED attacker label, and the defense result card. The squad badge
    /// counts the attackers (in their color); the capture pips show their progress on the player's points.</summary>
    public partial class BattleHUD
    {
        bool defense;
        public bool DefenseMode => defense;
        public DefenseResultCard DefenseResults { get; private set; }
        public UIButton[] SpeedButtons { get; private set; }
        public UIButton StrikeButton { get; private set; }
        DefenderStrike strike;
        TextMeshProUGUI strikeCount, strikeLabel;
        RectTransform strikeRt;
        Image strikeGlow;
        static readonly float[] Speeds = { 1f, 2f, 4f };

        void BuildDefenseControls()
        {
            var skin = UIFactory.Skin;
            var mine = enemy; // the player's color in a defense battle

            // Speed control under the squad badge.
            var sp = UIFactory.Rect(hud, "Speed").At(1f, 1f, -22f, -214f, 330f, 92f, 1f, 1f);
            var bg = UIFactory.Img(sp, "Bg", skin.pill, UIColors.WithAlpha(UIColors.SlateDark, 0.85f), UIFactory.PillMult(92f));
            ((RectTransform)bg.transform).Stretch();
            SpeedButtons = new UIButton[Speeds.Length];
            for (int i = 0; i < Speeds.Length; i++)
            {
                float m = Speeds[i];
                var b = UIFactory.Button(sp, "Speed" + m, m.ToString("0") + "x", ButtonColor.Slate, 96f, 70f, i == 0 ? (UIIcon?)UIIcon.Play : UIIcon.FastForward, 26f);
                ((RectTransform)b.transform).At(0f, 0.5f, 12f + i * 104f, 0f, 96f, 70f, 0f, 0.5f);
                b.onClick = () => SetSpeed(m);
                SpeedButtons[i] = b;
            }
            SetSpeed(1f);

            // Paint strike (bottom right).
            strike = BattleSetup.Instance != null ? BattleSetup.Instance.strike : null;
            if (strike != null && strike.enabled)
            {
                strikeRt = UIFactory.Rect(hud, "Strike").At(1f, 0f, -48f, 74f, 170f, 170f, 1f, 0f);
                strikeGlow = UIFactory.Img(strikeRt, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Team(mine), 0f));
                ((RectTransform)strikeGlow.transform).At(0.5f, 0.5f, 0f, 0f, 300f, 300f);
                StrikeButton = UIFactory.IconButton(strikeRt, "Button", UIIcon.Target, 170f, ButtonColor.Team, 0.55f);
                ((RectTransform)StrikeButton.transform).Stretch();
                StrikeButton.onClick = ToggleStrike;
                var pill = UIFactory.Img(strikeRt, "CountBg", skin.pill, UIColors.WithAlpha(UIColors.SlateDark, 0.92f), UIFactory.PillMult(48f));
                ((RectTransform)pill.transform).At(1f, 1f, 6f, 6f, 76f, 48f, 1f, 1f);
                strikeCount = UIFactory.Text(pill.transform, "Count", "x3", 30f, TextStyle.HeadingThin);
                ((RectTransform)strikeCount.transform).Stretch(0f, 0f, 0f, 2f);
                strikeLabel = UIFactory.Text(strikeRt, "Label", "PAINT STRIKE", 28f, TextStyle.Heading);
                ((RectTransform)strikeLabel.transform).At(0.5f, 0f, 0f, -30f, 300f, 36f, 0.5f, 0.5f);
            }
        }

        void ShowDefenseBanner()
        {
            var lo = BattleSetup.Instance != null ? BattleSetup.Instance.Applied : null;
            string who = lo != null && !string.IsNullOrEmpty(lo.attackerName) ? lo.attackerName : "An attacker";
            toast.Show("DEFEND YOUR BASE: " + who.ToUpperInvariant() + " ATTACKS", UIIcon.Shield, UIColors.WithAlpha(UIColors.Team(enemy), 0.95f), 2.2f, 165f);
            if (objectiveText != null) objectiveText.text = "HOLD BOTH POINTS";
            var tag = UIFactory.Tag(hud, "SimTag", lo != null && lo.defenseTest ? "TEST: SIMULATED AI ATTACKER" : "SIMULATED AI ATTACKER", UIColors.WithAlpha(UIColors.SlateDark, 0.85f), 40f, 22f);
            tag.At(0f, 1f, 170f, -28f, tag.sizeDelta.x, 40f, 0f, 1f);
        }

        public void SetSpeed(float m)
        {
            GameSpeed.Set(m);
            if (SpeedButtons == null) return;
            for (int i = 0; i < Speeds.Length; i++)
                SpeedButtons[i].SetColor(Mathf.Approximately(Speeds[i], m) ? ButtonColor.Gold : ButtonColor.Slate);
        }

        void ToggleStrike()
        {
            if (strike == null) return;
            if (strike.Armed) { strike.Disarm(); return; }
            if (!strike.Arm()) { UIAnim.Shake(strikeRt, 8f, 0.25f); return; }
            toast.Show("TAP THE GROUND TO DROP A PAINT STRIKE", UIIcon.Target, UIColors.WithAlpha(UIColors.TeamDark(enemy), 0.95f), 1.4f);
        }

        void UpdateDefense()
        {
            if (strike == null || strikeRt == null) return;
            strikeCount.SetText("x{0}", strike.Charges);
            bool ready = strike.Ready;
            StrikeButton.interactable = ready || strike.Armed;
            if (strike.Armed)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                strikeRt.localScale = Vector3.one * (1f + 0.08f * k);
                strikeGlow.color = UIColors.WithAlpha(UIColors.Team(enemy), 0.35f + 0.45f * k);
            }
            else
            {
                strikeRt.localScale = Vector3.one;
                strikeGlow.color = UIColors.WithAlpha(UIColors.Team(enemy), 0f);
            }
            if (!strikeRt.TryGetComponent(out CanvasGroup cg)) cg = strikeRt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = ready || strike.Armed ? 1f : 0.5f;
        }

        void OnDefenseRewarded(DefenseSummary s) { if (DefenseResults != null) DefenseResults.Show(s); }

        void OnEnemySquadSpawned(EnemySquad s) { if (!defense && s != null) squads.Add(s); }

        void OnCounterattack(Vector3 p)
        {
            if (defense) toast.Show("YOUR RESERVES COUNTERATTACK!", UIIcon.Users, UIColors.WithAlpha(UIColors.TeamDark(enemy), 0.95f), 1.6f);
            else toast.Show("COUNTERATTACK! HOLD THE POINT", UIIcon.Skull, UIColors.WithAlpha(UIColors.RedDark, 0.95f), 1.6f);
        }
    }
}
