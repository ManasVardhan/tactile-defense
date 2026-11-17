using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.CameraRig
{
    /// <summary>Orthographic follow camera: tracks the squad centroid with look ahead, clamps to map bounds,
    /// pauses while a stroke is drawn (so the stroke does not slide under the finger), supports shake.</summary>
    public class BattleCamera : MonoBehaviour
    {
        public static BattleCamera Instance { get; private set; }
        public CameraConfig config;
        public Camera cam;
        public SquadController squad;

        Vector3 focus;
        Vector3 vel;
        float shake;
        float seed;
        bool initialized;

        public Camera Cam => cam;
        public Vector3 Focus => focus;

        void Awake()
        {
            Instance = this;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            seed = Random.value * 100f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Shake(float amount)
        {
            if (Instance == null || Instance.config == null) return;
            Instance.shake = Mathf.Min(Instance.config.maxShake, Instance.shake + amount);
        }

        public void SnapTo(Vector3 f)
        {
            focus = Clamp(f);
            vel = Vector3.zero;
            initialized = true;
            Apply(0f);
        }

        Vector3 Clamp(Vector3 f)
        {
            if (config == null) return f;
            f.x = Mathf.Clamp(f.x, config.focusMin.x, config.focusMax.x);
            f.z = Mathf.Clamp(f.z, config.focusMin.y, config.focusMax.y);
            f.y = 0f;
            return f;
        }

        Vector3 DesiredFocus()
        {
            if (squad == null || squad.AliveCount == 0) return focus;
            Vector3 c = squad.Centroid;
            Vector3 ahead = Vector3.ClampMagnitude((squad.Anchor - c) * config.lookAhead, config.maxLookAhead);
            return c + ahead + Vector3.forward * config.forwardBias;
        }

        void LateUpdate()
        {
            if (config == null || cam == null) return;
            if (!initialized) { SnapTo(DesiredFocus()); }
            if (squad == null || !squad.IsDrawing)
                focus = Vector3.SmoothDamp(focus, Clamp(DesiredFocus()), ref vel, config.followSmoothTime, Mathf.Infinity, Time.deltaTime);
            Apply(Time.deltaTime);
        }

        void Apply(float dt)
        {
            cam.orthographic = true;
            cam.orthographicSize = config.OrthoSize;
            Quaternion rot = Quaternion.Euler(config.elevation, config.yaw, 0f);
            Vector3 pos = focus - rot * Vector3.forward * config.distance;
            if (shake > 0.001f)
            {
                float t = Time.time * config.shakeFrequency;
                Vector3 off = new Vector3(Mathf.PerlinNoise(seed, t) - 0.5f, Mathf.PerlinNoise(seed + 7f, t) - 0.5f, 0f) * 2f * shake;
                pos += rot * off;
                shake = Mathf.MoveTowards(shake, 0f, config.shakeDecay * dt * Mathf.Max(0.2f, shake));
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
