using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TactileWars.Core
{
    public struct PointerSample
    {
        public bool isDown;
        public Vector2 position;
    }

    /// <summary>Source of raw pointer state. Swap for a simulated source in tests.</summary>
    public interface IPointerSource
    {
        PointerSample Read();
        bool ReinforcePressed();
    }

    /// <summary>Mouse and touch through the Input System package (first touch wins).</summary>
    public class DevicePointerSource : IPointerSource
    {
        public PointerSample Read()
        {
            var s = new PointerSample();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                s.isDown = true;
                s.position = ts.primaryTouch.position.ReadValue();
                return s;
            }
            var m = Mouse.current;
            if (m != null)
            {
                s.isDown = m.leftButton.isPressed;
                s.position = m.position.ReadValue();
            }
#endif
            return s;
        }

        public bool ReinforcePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.rKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }

    /// <summary>Scriptable pointer for tests and automated captures.</summary>
    public class SimulatedPointerSource : IPointerSource
    {
        public bool Down;
        public Vector2 Position;
        public bool Reinforce;

        public PointerSample Read() => new PointerSample { isDown = Down, position = Position };

        public bool ReinforcePressed()
        {
            bool r = Reinforce;
            Reinforce = false;
            return r;
        }
    }

    /// <summary>Unified pointer state (mouse and touch share one path). Edge flags are computed once per frame.</summary>
    public static class PointerInput
    {
        static IPointerSource source;
        static int lastFrame = -1;
        static PointerSample current, previous;
        static bool reinforce;

        public static IPointerSource Source
        {
            get { return source ??= new DevicePointerSource(); }
            set { source = value; lastFrame = -1; previous = default; current = default; }
        }

        static void Refresh()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            previous = current;
            current = Source.Read();
            reinforce = Source.ReinforcePressed();
        }

        public static bool IsDown { get { Refresh(); return current.isDown; } }
        public static bool PressedThisFrame { get { Refresh(); return current.isDown && !previous.isDown; } }
        public static bool ReleasedThisFrame { get { Refresh(); return !current.isDown && previous.isDown; } }
        public static Vector2 Position { get { Refresh(); return current.isDown || !previous.isDown ? current.position : previous.position; } }
        public static bool ReinforceRequested { get { Refresh(); return reinforce; } }

        /// <summary>Optional hit test installed by the presentation layer: returns true when a screen point is over
        /// an interactive UI element. Gameplay input ignores presses that start over UI. Core never references UI.</summary>
        public static Func<Vector2, bool> UIHitTest;

        public static bool IsOverUI(Vector2 screen) => UIHitTest != null && UIHitTest(screen);

        /// <summary>Ray from the camera through a screen point onto the ground plane y = 0.</summary>
        public static bool ScreenToGround(Camera cam, Vector2 screen, out Vector3 world)
        {
            world = default;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            world = ray.origin + ray.direction * t;
            world.y = 0f;
            return true;
        }
    }
}
