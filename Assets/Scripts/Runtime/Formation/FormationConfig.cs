using UnityEngine;

namespace TactileWars.Formation
{
    /// <summary>Formation drawing tunables. All values are estimates unless noted (the reference shows
    /// soldiers about 1.0 to 1.3 widths apart along a stroke, VISUAL_ANALYSIS section 2).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Formation Config")]
    public class FormationConfig : ScriptableObject
    {
        [Tooltip("Estimate. Minimum distance between neighbouring soldiers along the stroke (units).")]
        public float minSpacing = 0.7f;
        [Tooltip("Estimate. Maximum spacing when the stroke is longer than the squad needs.")]
        public float maxSpacing = 1.15f;
        [Tooltip("Estimate. Distance between parallel rows when the stroke is too short.")]
        public float rowSpacing = 0.72f;
        [Tooltip("Chaikin smoothing iterations applied on release.")]
        public int smoothingIterations = 3;
        [Tooltip("World distance between raw stroke samples.")]
        public float sampleDistance = 0.2f;
        [Tooltip("Resample step used for shape analysis and solving.")]
        public float resampleStep = 0.25f;
        [Tooltip("Estimate. Shorter strokes count as taps.")]
        public float minStrokeLength = 1.0f;
        [Tooltip("Estimate. Max press duration for a tap (seconds).")]
        public float tapMaxDuration = 0.3f;
        [Tooltip("Estimate. Max pointer travel for a tap, as a fraction of screen height.")]
        public float tapMaxTravel = 0.025f;
        [Tooltip("Seconds between live re-solves while dragging (the squad chases the stroke).")]
        public float liveSolveInterval = 0.08f;
        [Tooltip("A stroke whose end is closer than this fraction of its length to its start is closed.")]
        public float closedThreshold = 0.2f;
        [Tooltip("Coefficient of variation of the radius below which a closed stroke is a circle.")]
        public float circleTolerance = 0.16f;
        [Tooltip("Turn angle (degrees) that counts as a corner for square detection.")]
        public float cornerAngle = 55f;
        [Tooltip("Endpoint distance / length ratio above which an open stroke is a line.")]
        public float lineStraightness = 0.93f;
        [Tooltip("Estimate (GAMEPLAY_MECHANICS 2.5: 0.08). Max deviation from the chord, as a fraction of length, for a line.")]
        public float lineMaxDeviation = 0.08f;
        [Tooltip("Snap recognized lines and circles to their ideal shape.")]
        public bool snapRecognizedShapes = true;
        [Tooltip("Max number of stroke samples kept while drawing.")]
        public int maxStrokePoints = 1024;

        public FormationParams ToParams() => new FormationParams
        {
            minSpacing = minSpacing,
            maxSpacing = maxSpacing,
            rowSpacing = rowSpacing,
        };
    }

    public struct FormationParams
    {
        public float minSpacing;
        public float maxSpacing;
        public float rowSpacing;

        public static FormationParams Default => new FormationParams { minSpacing = 0.7f, maxSpacing = 1.15f, rowSpacing = 0.72f };
    }
}
