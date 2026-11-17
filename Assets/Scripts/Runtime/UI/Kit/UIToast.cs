using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Short pop-up message in a pill (purchase feedback, "+10 reinforcements", errors).</summary>
    public class UIToast : MonoBehaviour
    {
        public TextMeshProUGUI label;
        public Image bg;
        public Image icon;
        CanvasGroup cg;
        Coroutine running;
        float homeY;

        public static UIToast Create(Transform parent, float y)
        {
            var rt = UIFactory.Rect(parent, "Toast").At(0.5f, 0.5f, 0f, y, 600f, 84f);
            var t = rt.gameObject.AddComponent<UIToast>();
            t.homeY = y;
            t.cg = rt.gameObject.AddComponent<CanvasGroup>();
            t.cg.blocksRaycasts = false;
            t.cg.interactable = false;
            t.bg = UIFactory.Img(rt, "Bg", UIFactory.Skin != null ? UIFactory.Skin.pill : null, UIColors.WithAlpha(UIColors.Ink, 0.88f), UIFactory.PillMult(84f));
            ((RectTransform)t.bg.transform).Stretch();
            t.icon = UIFactory.Icon(rt, UIIcon.Check, 64f);
            ((RectTransform)t.icon.transform).At(0f, 0.5f, 44f, 2f, 64f, 64f, 0.5f, 0.5f);
            t.label = UIFactory.Text(rt, "Text", "", 40f, TextStyle.Heading);
            ((RectTransform)t.label.transform).Stretch(84f, 0f, 34f, 4f);
            t.label.overflowMode = TextOverflowModes.Overflow;
            rt.gameObject.SetActive(false);
            return t;
        }

        public void Show(string text, UIIcon? ic = null, Color? tint = null, float hold = 1.6f, float? y = null)
        {
            gameObject.SetActive(true);
            var rtSelf = (RectTransform)transform;
            rtSelf.anchoredPosition = new Vector2(0f, y ?? homeY);
            label.text = text;
            float w = label.GetPreferredValues(text).x;
            bool hasIcon = ic.HasValue && UIFactory.Skin != null;
            icon.gameObject.SetActive(hasIcon);
            if (hasIcon) icon.sprite = UIFactory.Skin.Icon(ic.Value);
            ((RectTransform)label.transform).Stretch(hasIcon ? 84f : 34f, 0f, 34f, 4f);
            ((RectTransform)transform).sizeDelta = new Vector2(w + (hasIcon ? 118f : 68f), 84f);
            bg.color = tint ?? UIColors.WithAlpha(UIColors.Ink, 0.88f);
            transform.SetAsLastSibling();
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(hold));
        }

        IEnumerator Run(float hold)
        {
            cg.alpha = 1f;
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.LerpUnclamped(0.5f, 1f, UIAnim.OutBack(Mathf.Clamp01(t / 0.28f)));
                transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - t / 0.3f;
                yield return null;
            }
            gameObject.SetActive(false);
            running = null;
        }
    }
}
