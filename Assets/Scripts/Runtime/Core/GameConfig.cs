using UnityEngine;

namespace TactileWars.Core
{
    /// <summary>Root config: team choice plus references to all tunable assets and runtime prefabs.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public TeamColorId playerTeam = TeamColorId.Red;
        public TeamColorId enemyTeam = TeamColorId.Teal;
        public TeamPalette palette;
        public TeamVisuals visuals;

        [Header("Map (estimates, tuned for the milestone level)")]
        public Vector2 mapMin = new Vector2(-18f, 0f);
        public Vector2 mapMax = new Vector2(18f, 110f);

        public static GameConfig Active { get; set; }
    }
}
