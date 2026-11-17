using System.Collections;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Eased show and hide (scale plus fade) for panels and screens. Uses unscaled time.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanelAnim : MonoBehaviour
    {
        public float duration = 0.24f;
        public float fromScale = 0.9f;
        public RectTransform scaleTarget;
        CanvasGroup cg;
        public bool IsShown { get; private set; }

        CanvasGroup Group => cg != null ? cg : (cg = GetComponent<CanvasGroup>());

        public void Show(bool instant = false)
        {
            gameObject.SetActive(true);
            IsShown = true;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            var tr = scaleTarget != null ? scaleTarget : (RectTransform)transform;
            if (instant) { Group.alpha = 1f; tr.localScale = Vector3.one; return; }
            Group.alpha = 0f;
            UIAnim.Run(this, Anim(tr, true));
        }

        public void Hide(bool instant = false)
        {
            IsShown = false;
            if (!gameObject.activeInHierarchy || instant) { gameObject.SetActive(false); return; }
            Group.interactable = false;
            Group.blocksRaycasts = false;
            var tr = scaleTarget != null ? scaleTarget : (RectTransform)transform;
            UIAnim.Run(this, Anim(tr, false));
        }

        IEnumerator Anim(Transform tr, bool show)
        {
            float t = 0f;
            float d = show ? duration : duration * 0.7f;
            while (t < d)
            {
                if (this == null || tr == null) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / d);
                float s = show ? Mathf.LerpUnclamped(fromScale, 1f, UIAnim.OutBack(k)) : Mathf.Lerp(1f, fromScale, UIAnim.InCubic(k));
                tr.localScale = new Vector3(s, s, 1f);
                Group.alpha = show ? Mathf.Clamp01(k * 1.6f) : 1f - k;
                yield return null;
            }
            if (this == null || tr == null) yield break;
            tr.localScale = Vector3.one;
            Group.alpha = show ? 1f : 0f;
            if (!show) gameObject.SetActive(false);
        }
    }
}
