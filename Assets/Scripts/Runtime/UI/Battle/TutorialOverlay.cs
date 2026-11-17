using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>First battle coach: an animated finger draws a line ("Draw to place your squad"); after the first
    /// recognized stroke it shows "Tap to move" with a tap pulse, then fades out. Remembered in the save.
    /// Never blocks input (no raycast targets).</summary>
    public class TutorialOverlay : MonoBehaviour
    {
        RectTransform card, finger, trail, tapRing;
        TextMeshProUGUI text;
        Image trailImg, tapImg;
        CanvasGroup cg;
        int step = -1;
        float t, hideTimer;
        public bool Active => step >= 0;
        public int Step => step;

        public static TutorialOverlay Create(Transform parent, Camera cam)
        {
            var rt = UIFactory.Rect(parent, "Tutorial").Stretch();
            var o = rt.gameObject.AddComponent<TutorialOverlay>();
            o.cg = rt.gameObject.AddComponent<CanvasGroup>();
            o.cg.blocksRaycasts = false;
            o.cg.interactable = false;
            var skin = UIFactory.Skin;
            o.trail = UIFactory.Rect(rt, "Trail").At(0.5f, 0.5f, -260f, -170f, 0f, 14f, 0f, 0.5f);
            o.trailImg = UIFactory.Img(o.trail, "Dash", skin.dash, UIColors.WithAlpha(Color.white, 0.95f));
            o.trailImg.type = Image.Type.Tiled;
            ((RectTransform)o.trailImg.transform).Stretch();
            o.tapRing = UIFactory.Rect(rt, "TapRing").At(0.5f, 0.5f, 120f, -140f, 120f, 120f);
            o.tapImg = UIFactory.Img(o.tapRing, "Ring", skin.ringThin, Color.white);
            ((RectTransform)o.tapImg.transform).Stretch();
            o.finger = UIFactory.Rect(rt, "Finger").At(0.5f, 0.5f, 0f, 0f, 150f, 150f, 0.3f, 0.92f);
            var f = UIFactory.Icon(o.finger, UIIcon.Finger, 150f);
            ((RectTransform)f.transform).Stretch();
            o.card = UIFactory.Rect(rt, "Card").At(0.5f, 0f, 0f, 60f, 900f, 104f, 0.5f, 0f);
            var bg = UIFactory.Img(o.card, "Bg", skin.pill, UIColors.WithAlpha(UIColors.SlateDark, 0.88f), UIFactory.PillMult(104f));
            ((RectTransform)bg.transform).Stretch();
            var ic = UIFactory.Icon(o.card, UIIcon.Pencil, 68f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 22f, 2f, 68f, 68f, 0f, 0.5f);
            o.text = UIFactory.Text(o.card, "Text", "", 40f, TextStyle.Heading);
            ((RectTransform)o.text.transform).Stretch(100f, 0f, 40f, 4f);
            rt.gameObject.SetActive(false);
            return o;
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            step = 0;
            t = 0f;
            cg.alpha = 1f;
            text.text = "DRAW A LINE TO PLACE YOUR SQUAD";
            tapRing.gameObject.SetActive(false);
            trail.gameObject.SetActive(true);
        }

        public void OnStroke()
        {
            if (step != 0) return;
            var p = MetaGame.Profile;
            p.tutorial.drawStroke = true;
            SaveSystem.Save();
            step = 1;
            t = 0f;
            hideTimer = 4.5f;
            text.text = "NICE! NOW TAP THE GROUND TO MOVE";
            trail.gameObject.SetActive(false);
            tapRing.gameObject.SetActive(true);
            UIAnim.Pop(card, 0.8f, 0.3f);
        }

        public void Hide()
        {
            if (step == 1)
            {
                var p = MetaGame.Profile;
                if (!p.tutorial.tapToMove) { p.tutorial.tapToMove = true; SaveSystem.Save(); }
            }
            step = -1;
            gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (step == 0)
            {
                // Finger sweeps left to right along a gentle arc, the dashed trail grows behind it, then repeats.
                float cycle = 2.4f;
                float k = Mathf.Repeat(t, cycle) / cycle;
                float u = Mathf.Clamp01((k - 0.12f) / 0.62f);
                float e = UIAnim.InOutSine(u);
                float x = Mathf.Lerp(-260f, 260f, e);
                float y = -170f + Mathf.Sin(e * Mathf.PI) * 40f;
                finger.anchoredPosition = new Vector2(x, y);
                float press = k < 0.12f ? k / 0.12f : (k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f);
                float s = Mathf.Lerp(1.15f, 1f, press);
                finger.localScale = new Vector3(s, s, 1f);
                trail.sizeDelta = new Vector2(Mathf.Max(0f, x + 260f), 14f);
                trailImg.color = UIColors.WithAlpha(Color.white, 0.95f * Mathf.Clamp01(press));
            }
            else if (step == 1)
            {
                float k = Mathf.Repeat(t, 1.2f) / 1.2f;
                finger.anchoredPosition = new Vector2(150f, -150f + (k < 0.2f ? (1f - k / 0.2f) * 30f : 0f));
                float rs = Mathf.Lerp(0.3f, 1.6f, k);
                tapRing.localScale = new Vector3(rs, rs, 1f);
                tapImg.color = UIColors.WithAlpha(Color.white, 1f - k);
                hideTimer -= dt;
                if (hideTimer < 0.5f) cg.alpha = Mathf.Clamp01(hideTimer / 0.5f);
                if (hideTimer <= 0f) Hide();
            }
        }
    }
}
