using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Scale With Screen Size (1920x1080 reference, match 0.5) that measures the canvas camera's pixel
    /// size, so the layout is also correct when that camera renders into a texture (screenshots at any aspect).</summary>
    public class AdaptiveCanvasScaler : CanvasScaler
    {
        Canvas cachedCanvas;

        protected override void HandleScaleWithScreenSize()
        {
            if (cachedCanvas == null) cachedCanvas = GetComponent<Canvas>();
            Vector2 screen = cachedCanvas != null ? cachedCanvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            var cam = cachedCanvas != null ? cachedCanvas.worldCamera : null;
            if (cachedCanvas != null && cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera && cam != null)
                screen = new Vector2(cam.pixelWidth, cam.pixelHeight);
            if (screen.x <= 0f || screen.y <= 0f) return;
            float logW = Mathf.Log(screen.x / m_ReferenceResolution.x, 2f);
            float logH = Mathf.Log(screen.y / m_ReferenceResolution.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, m_MatchWidthOrHeight));
            SetScaleFactor(scale);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }
    }
}
