using TactileWars.Army;
using TactileWars.Battle;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>
    /// Attack battles: when every unit is lost, nothing is left to deploy and the backup meter is full, the battle
    /// waits for the player to call backup. This shows a line under the backup badge saying so (with an arrow at
    /// the badge) and asks the HUD to pulse the badge harder. It goes as soon as backup is used. Not a raycast target.
    /// </summary>
    public class BackupPrompt : MonoBehaviour
    {
        public const string Message = "All units lost. Click Backup to bring one back.";

        PlayerArmy army;
        BattleManager bm;
        RectTransform rt, arrow;
        TextMeshProUGUI text;
        CanvasGroup cg;
        float t, arrowX;

        public bool Visible { get; private set; }
        public string Text => text.text;
        /// <summary>Extra pulse for the backup badge, 0 when the prompt is hidden.</summary>
        public float Pulse => Visible ? 1f : 0f;

        /// <summary>Builds the prompt under 'badge' (the backup badge, anchored top right), arrow pointing up at it.</summary>
        public static BackupPrompt Create(RectTransform parent, RectTransform badge, PlayerArmy army, BattleManager bm)
        {
            const float w = 480f, h = 104f;
            // Badge center and bottom in the top right corner's space; the badge's caption sits just under it.
            float cx = badge.anchoredPosition.x + badge.sizeDelta.x * (0.5f - badge.pivot.x);
            float bottom = badge.anchoredPosition.y - badge.sizeDelta.y * badge.pivot.y;
            float top = bottom - 118f;
            var holder = UIFactory.Rect(parent, "BackupPrompt").At(1f, 1f, -22f, top, w, h, 1f, 1f);
            var p = holder.gameObject.AddComponent<BackupPrompt>();
            p.army = army;
            p.bm = bm;
            p.rt = UIFactory.Rect(holder, "Body").Stretch();
            p.cg = p.rt.gameObject.AddComponent<CanvasGroup>();
            p.cg.blocksRaycasts = false;
            var skin = UIFactory.Skin;
            var bg = UIFactory.Img(p.rt, "Bg", skin != null ? skin.rounded32 : null, UIColors.WithAlpha(UIColors.RedDark, 0.95f), UIFactory.Round32(26f));
            ((RectTransform)bg.transform).Stretch();
            var ic = UIFactory.Icon(p.rt, UIIcon.Users, 58f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 18f, 0f, 58f, 58f, 0f, 0.5f);
            p.text = UIFactory.Text(p.rt, "Text", Message, 30f, TextStyle.HeadingThin, TextAlignmentOptions.MidlineLeft);
            p.text.textWrappingMode = TextWrappingModes.Normal;
            ((RectTransform)p.text.transform).Stretch(90f, 8f, 18f, 10f);
            // Arrow above the box, under the badge's center.
            float ax = cx - (-22f - w); // from the box's left edge
            p.arrow = UIFactory.Rect(p.rt, "Arrow").At(0f, 1f, ax, 4f, 50f, 50f, 0.5f, 0f);
            var a = UIFactory.Icon(p.arrow, UIIcon.ArrowUp, 50f);
            ((RectTransform)a.transform).Stretch();
            p.arrowX = ax;
            p.rt.gameObject.SetActive(false);
            return p;
        }

        /// <summary>True when the battle can only go on through backup.</summary>
        public static bool Needed(PlayerArmy army, BattleManager bm) =>
            army != null && bm != null && !bm.Over && bm.Meter != null && bm.Meter.Ready
            && army.Units.Count == 0 && army.ReadyCount == 0 && army.Squad.Count > 0;

        void Update()
        {
            bool on = Needed(army, bm);
            if (on != Visible)
            {
                Visible = on;
                rt.gameObject.SetActive(on);
                if (on) { t = 0f; UIAnim.Pop(rt, 0.7f, 0.35f); }
            }
            if (!on) return;
            t += Time.unscaledDeltaTime;
            arrow.anchoredPosition = new Vector2(arrowX, 4f + Mathf.Abs(Mathf.Sin(t * 6f)) * 10f);
        }
    }
}
