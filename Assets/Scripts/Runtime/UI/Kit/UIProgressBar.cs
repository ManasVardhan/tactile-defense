using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Pill progress bar; the fill keeps its rounded ends by resizing instead of image filling.</summary>
    public class UIProgressBar : MonoBehaviour
    {
        public RectTransform fill;
        public Image fillImage;
        float value = -1f, shown, target, speed;

        public void Set(float v, bool animate = false, float duration = 0.6f)
        {
            target = Mathf.Clamp01(v);
            if (!animate) { shown = target; Apply(); }
            else { speed = Mathf.Abs(target - shown) / Mathf.Max(0.05f, duration); enabled = true; }
            value = target;
        }

        public float Value => value;

        void Update()
        {
            if (Mathf.Approximately(shown, target)) { enabled = false; return; }
            shown = Mathf.MoveTowards(shown, target, speed * Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            if (fill == null) return;
            var parent = (RectTransform)fill.parent;
            float w = parent.rect.width - 6f;
            float h = parent.rect.height - 6f;
            if (w <= 0f) w = parent.sizeDelta.x - 6f;
            if (h <= 0f) h = parent.sizeDelta.y - 6f;
            float fw = Mathf.Max(h, w * shown);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(3f + fw, -3f);
            if (fillImage != null) fillImage.enabled = shown > 0.001f;
        }
    }
}
