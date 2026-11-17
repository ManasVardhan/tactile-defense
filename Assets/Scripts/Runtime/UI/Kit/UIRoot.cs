using System.Collections.Generic;
using TactileWars.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>One per scene: a Screen Space Camera canvas on the scene camera (so captures into render textures
    /// include the UI), 1920x1080 reference scaling with match 0.5, a safe area child, the event system, and the
    /// pointer hit test that keeps UI presses away from gameplay strokes.</summary>
    public class UIRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform CanvasRect { get; private set; }
        /// <summary>Safe area rect: put everything here unless it must bleed to the screen edges.</summary>
        public RectTransform Safe { get; private set; }
        public Camera Camera { get; private set; }

        static readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        static PointerEventData ped;

        public static UIRoot Create(string name, Camera cam, UISkin skin, int sortingOrder = 10)
        {
            UIFactory.Skin = skin;
            if (skin != null) UIColors.Palette = skin.palette;
            var go = new GameObject(name, typeof(RectTransform)) { layer = UIFactory.UILayer };
            var root = go.AddComponent<UIRoot>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam != null ? Mathf.Max(cam.nearClipPlane + 0.4f, 1.5f) : 10f;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<AdaptiveCanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            root.Canvas = canvas;
            root.CanvasRect = (RectTransform)go.transform;
            root.Camera = cam;
            var safe = UIFactory.Rect(go.transform, "SafeArea").Stretch();
            safe.gameObject.AddComponent<SafeArea>().canvasCamera = cam;
            root.Safe = safe;
            EnsureEventSystem();
            return root;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<EventSystem>().sendNavigationEvents = false;
        }

        void OnEnable() { PointerInput.UIHitTest = HitTest; }

        void OnDisable() { if (PointerInput.UIHitTest == (System.Func<Vector2, bool>)HitTest) PointerInput.UIHitTest = null; }

        static bool HitTest(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (ped == null || ped.currentInputModule == null) ped = new PointerEventData(es);
            ped.position = screen;
            hits.Clear();
            es.RaycastAll(ped, hits);
            return hits.Count > 0;
        }

        /// <summary>Canvas space size (after scaling).</summary>
        public Vector2 Size => CanvasRect.rect.size;
    }
}
