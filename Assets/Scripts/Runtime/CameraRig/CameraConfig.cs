using UnityEngine;

namespace TactileWars.CameraRig
{
    /// <summary>Camera tunables. Elevation and zoom derive from the reference (VISUAL_ANALYSIS section 1): the screen
    /// is about 25 to 30 soldier heights tall; elevation is an estimate between 25 and 40 degrees.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Camera Config")]
    public class CameraConfig : ScriptableObject
    {
        [Tooltip("Degrees above the horizon (estimate).")] public float elevation = 35f;
        public float yaw = 0f;
        [Tooltip("Visible screen height in soldier heights (reference: about 25 to 30).")] public float screenSoldierHeights = 33f;
        public float soldierHeight = 1f;
        public float distance = 60f;
        [Tooltip("Follow smoothing time (seconds).")] public float followSmoothTime = 0.45f;
        [Tooltip("How much the camera leans toward the squad's destination.")] public float lookAhead = 0.35f;
        public float maxLookAhead = 5f;
        [Tooltip("Shift of the focus so the squad (or, in attack battles, the army's spawn at the start) sits a little below centre and more of the map ahead is visible.")] public float forwardBias = 2.5f;
        [Tooltip("Focus bounds (x, z) for following and for panning.")] public Vector2 focusMin = new Vector2(-3f, 6f);
        [Tooltip("Focus bounds (x, z) for following and for panning.")] public Vector2 focusMax = new Vector2(3f, 100f);
        public float shakeDecay = 2.8f;
        public float shakeFrequency = 22f;
        public float maxShake = 0.6f;

        /// <summary>Orthographic size: a vertical object of height h at elevation e covers h*cos(e) on screen.</summary>
        public float OrthoSize => screenSoldierHeights * soldierHeight * Mathf.Cos(elevation * Mathf.Deg2Rad) * 0.5f;
    }
}
