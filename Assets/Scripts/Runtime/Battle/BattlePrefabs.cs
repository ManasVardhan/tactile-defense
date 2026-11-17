using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Runtime prefab references (built by AssetBuilder).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Battle Prefabs")]
    public class BattlePrefabs : ScriptableObject
    {
        public Soldier soldier;
        [UnityEngine.Tooltip("Optional squad leader model (Hero.fbx). One per player squad.")] public Soldier hero;
        public Soldier flagBearer;
        public FlagBubble bubble;
    }
}
