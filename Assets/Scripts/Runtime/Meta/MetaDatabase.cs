using TactileWars.Battle;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>Root index of every meta game asset. Lives in Assets/Resources so any scene (and the next phase's
    /// base editor) can reach it with MetaDatabase.Load().</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Meta Database")]
    public class MetaDatabase : ScriptableObject
    {
        public ProgressionConfig progression;
        public ResearchTree research;
        public MercenaryDef[] mercenaries = new MercenaryDef[0];
        public BaseRoster roster;
        public DefenseCatalog defenses;
        public GameConfig game;
        [Tooltip("Base squad stats that research adds to.")] public Squad.SquadConfig squadConfig;
        public BattleConfig battleConfig;
        public Units.UnitConfig playerSoldier;

        public const string ResourcePath = "MetaDatabase";
        static MetaDatabase cached;

        public static MetaDatabase Load()
        {
            if (cached == null) cached = Resources.Load<MetaDatabase>(ResourcePath);
            return cached;
        }

        public MercenaryDef FindMercenary(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < mercenaries.Length; i++) if (mercenaries[i] != null && mercenaries[i].id == id) return mercenaries[i];
            return null;
        }
    }
}
