using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Fits this rect to Screen.safeArea (notches, rounded corners). Ignored while the canvas camera
    /// renders into a texture (automated captures).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        public Camera canvasCamera;
        Rect last;
        Vector2Int lastSize;

        void OnEnable() { Apply(true); }
        void Update() { Apply(false); }

        void Apply(bool force)
        {
            var rt = (RectTransform)transform;
            if (canvasCamera != null && canvasCamera.targetTexture != null)
            {
                if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; }
                return;
            }
            Rect sa = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && sa == last && size == lastSize) return;
            last = sa; lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;
            Vector2 min = sa.position, max = sa.position + sa.size;
            min.x /= size.x; min.y /= size.y; max.x /= size.x; max.y /= size.y;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
