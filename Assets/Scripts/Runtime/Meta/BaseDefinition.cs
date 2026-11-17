using TactileWars.Battle;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>An AI opponent base (offline stand in for real players' bases). Difficulty, multipliers and level
    /// locks are ESTIMATES. Verified: higher ranked opponents give better rewards.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Base Definition")]
    public class BaseDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string commanderName;
        public TeamColorId enemyTeam = TeamColorId.Teal;
        [Range(1, 10)] public int difficulty = 1;
        public int requiredLevel = 1;
        public float rewardMultiplier = 1f;
        public float enemyHealthMult = 1f;
        public float enemyDamageMult = 1f;
        [Tooltip("Null keeps the handcrafted deployment of Battle.unity (base number one).")] public BaseLayoutData layout;
        [Tooltip("Map variant. Null keeps the handcrafted lane of Battle.unity.")] public MapLayoutData map;
        [Tooltip("Display only: rough enemy count for the card.")] public int enemySquads = 7;
        public int defenses = 15;
    }
}
