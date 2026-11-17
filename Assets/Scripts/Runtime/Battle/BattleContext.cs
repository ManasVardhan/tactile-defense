using TactileWars.Combat;
using TactileWars.Core;

namespace TactileWars.Battle
{
    /// <summary>Static access to the active battle's configuration, set by BattleManager.Awake.</summary>
    public static class BattleContext
    {
        public static GameConfig Config;
        public static BattleConfig Battle;
        public static BattlePrefabs Prefabs;
        public static CombatConfig Combat;
        public static TeamVisuals Visuals => Config != null ? Config.visuals : null;
        public static TeamPalette Palette => Config != null ? Config.palette : null;
    }
}
