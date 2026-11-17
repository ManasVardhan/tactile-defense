using System.Collections.Generic;
using System.IO;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Squad;
using TactileWars.Units;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Writes the meta game data (ALL VALUES ARE ESTIMATES, see Docs/ARCHITECTURE_CONTRACT.md Deviations):
    /// ProgressionConfig, ResearchTree, three mercenaries, the defense catalog, ten AI bases with generated
    /// BaseLayoutData (base one keeps the handcrafted Battle.unity deployment) and Assets/Resources/MetaDatabase.
    /// Existing assets are overwritten so the data always matches this builder.
    /// </summary>
    public static class ProgressionBuilder
    {
        public const string Dir = "Assets/Data/Meta";
        public const string LayoutDir = Dir + "/Layouts";
        public const string DbPath = "Assets/Resources/MetaDatabase.asset";

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            EditorUtility.SetDirty(a);
            return a;
        }

        public static MetaDatabase Build()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(LayoutDir);
            Directory.CreateDirectory("Assets/Resources");

            var prog = Asset<ProgressionConfig>(Dir + "/ProgressionConfig.asset");
            var fresh = ScriptableObject.CreateInstance<ProgressionConfig>();
            EditorUtility.CopySerialized(fresh, prog);
            Object.DestroyImmediate(fresh);
            var rewards = new List<LevelReward>();
            for (int l = 2; l <= prog.MaxLevel; l++) rewards.Add(new LevelReward { level = l, gold = 60 + 40 * l, prisms = l % 5 == 0 ? 5 : 1 });
            prog.levelRewards = rewards.ToArray();
            prog.name = "ProgressionConfig";

            var research = Asset<ResearchTree>(Dir + "/ResearchTree.asset");
            research.nodes = ResearchNodes();

            var mercs = new[]
            {
                Merc("brick", "Brick", "Shield wall", "A walking bunker. Soaks up enemy paint so the squad lives longer.", 2, 400, 60, 5f, 1.4f, 0.8f, 1.3f, 0f, "#8E97A8"),
                Merc("spark", "Spark", "Rapid fire", "Twin paint pumps that shred squads and flag bearers fast.", 4, 750, 90, 3f, 1.3f, 2.6f, 1.2f, 0f, "#F6B12B"),
                Merc("banner", "Banner", "Rally call", "Carries the colors: backup charges 30 percent faster.", 6, 1100, 110, 3.5f, 1.2f, 1.2f, 1.22f, 0.3f, "#76BD36"),
            };

            var catalog = Asset<DefenseCatalog>(Dir + "/DefenseCatalog.asset");
            var entries = new List<DefenseCatalog.Entry>();
            foreach (var (id, name, prefab) in new[] { ("turret", "Turret", "Turret"), ("mine", "Paint Mine", "Mine"), ("tank", "Tank", "Tank"), ("cannon", "Cannon", "Cannon"), ("bunker", "Bunker", "Bunker") })
                entries.Add(new DefenseCatalog.Entry { id = id, displayName = name, prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/" + prefab + ".prefab") });
            entries.Add(new DefenseCatalog.Entry { id = "tesla", displayName = "Tesla Antenna", prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/TeslaAntenna.prefab") });
            catalog.entries = entries.ToArray();

            var roster = Asset<BaseRoster>(Dir + "/BaseRoster.asset");
            roster.bases = Bases();

            var db = Asset<MetaDatabase>(DbPath);
            db.progression = prog;
            db.research = research;
            db.mercenaries = mercs;
            db.roster = roster;
            db.defenses = catalog;
            db.game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            db.squadConfig = AssetDatabase.LoadAssetAtPath<SquadConfig>("Assets/Data/SquadConfig.asset");
            db.battleConfig = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            db.playerSoldier = AssetDatabase.LoadAssetAtPath<UnitConfig>("Assets/Data/Unit_PlayerSoldier.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] meta data: " + research.nodes.Length + " research nodes, " + mercs.Length + " mercenaries, " + roster.bases.Length + " bases");
            return db;
        }

        static MercenaryDef Merc(string id, string name, string ability, string desc, int lvl, int unlock, int hire, float hp, float dmg, float rate, float scale, float charge, string accent)
        {
            var m = Asset<MercenaryDef>(Dir + "/Mercenary_" + name + ".asset");
            m.id = id; m.displayName = name; m.ability = ability; m.description = desc;
            m.unlockLevel = lvl; m.unlockCost = unlock; m.hireCost = hire;
            m.healthMult = hp; m.damageMult = dmg; m.fireRateMult = rate; m.scale = scale; m.reinforcementChargeBonus = charge;
            ColorUtility.TryParseHtmlString(accent, out m.accent);
            return m;
        }

        static ResearchStep[] Steps(int[] levels, int[] costs, float[] values)
        {
            var s = new ResearchStep[levels.Length];
            for (int i = 0; i < s.Length; i++) s[i] = new ResearchStep { requiredLevel = levels[i], goldCost = costs[i], value = values[i] };
            return s;
        }

        static ResearchNode Node(string id, string name, string desc, ResearchBranch b, ResearchEffect e, string icon, string fmt, float scale, ResearchStep[] steps, string defenseId = null) =>
            new ResearchNode { id = id, displayName = name, description = desc, branch = b, effect = e, icon = icon, valueFormat = fmt, displayScale = scale, steps = steps, defenseId = defenseId };

        static ResearchNode[] ResearchNodes() => new[]
        {
            // Troops
            Node("squad_size", "Squad Size", "More pigments march with you.", ResearchBranch.Troops, ResearchEffect.SquadSize, "Users", "+{0}", 1f,
                Steps(new[] { 1, 2, 4, 6, 9 }, new[] { 150, 320, 560, 900, 1400 }, new[] { 4f, 8f, 12f, 16f, 20f })),
            Node("soldier_health", "Thick Helmets", "Each soldier takes more paint before popping.", ResearchBranch.Troops, ResearchEffect.SoldierHealth, "Heart", "+{0}%", 100f,
                Steps(new[] { 1, 3, 5, 7, 10 }, new[] { 120, 260, 480, 800, 1250 }, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f })),
            Node("fire_rate", "Rapid Pumps", "Paint guns fire faster.", ResearchBranch.Troops, ResearchEffect.FireRate, "Lightning", "+{0}%", 100f,
                Steps(new[] { 2, 3, 5, 8, 11 }, new[] { 180, 340, 600, 950, 1450 }, new[] { 0.08f, 0.16f, 0.24f, 0.32f, 0.4f })),
            Node("paint_damage", "Heavy Pigment", "Every paint dot hits harder.", ResearchBranch.Troops, ResearchEffect.PaintDamage, "Target", "+{0}%", 100f,
                Steps(new[] { 2, 4, 6, 9, 12 }, new[] { 200, 380, 650, 1000, 1500 }, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f })),
            Node("move_speed", "Light Boots", "The squad marches and reforms faster.", ResearchBranch.Troops, ResearchEffect.MoveSpeed, "Boot", "+{0}%", 100f,
                Steps(new[] { 3, 6, 10 }, new[] { 260, 600, 1100 }, new[] { 0.06f, 0.12f, 0.18f })),
            // Offense
            Node("reinforcement_size", "Bigger Backup", "More soldiers arrive per reinforcement call.", ResearchBranch.Offense, ResearchEffect.ReinforcementSize, "Plus", "+{0}", 1f,
                Steps(new[] { 2, 4, 7, 10 }, new[] { 220, 450, 800, 1250 }, new[] { 3f, 6f, 9f, 12f })),
            Node("reinforcement_charge", "Quick Call", "The POW meter fills faster.", ResearchBranch.Offense, ResearchEffect.ReinforcementCharge, "Pow", "+{0}%", 100f,
                Steps(new[] { 3, 5, 8, 11 }, new[] { 260, 520, 880, 1300 }, new[] { 0.12f, 0.24f, 0.36f, 0.5f })),
            Node("hero_ability", "Leader Training", "Your squad leader and mercenary get tougher and hit harder.", ResearchBranch.Offense, ResearchEffect.HeroAbility, "Mercenary", "+{0}%", 100f,
                Steps(new[] { 4, 7, 12 }, new[] { 400, 850, 1500 }, new[] { 0.2f, 0.4f, 0.6f })),
            // Defense (for the player's own base, used by the base editor)
            Node("def_mine", "Paint Mine", "Hidden trap. Pops soldiers that walk over it.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Skull", "Lv {0}", 1f,
                Steps(new[] { 1, 3, 6 }, new[] { 0, 250, 600 }, new[] { 1f, 2f, 3f }), "mine"),
            Node("def_turret", "Turret", "Rotating paint turret with long range.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Target", "Lv {0}", 1f,
                Steps(new[] { 2, 4, 7 }, new[] { 300, 550, 900 }, new[] { 1f, 2f, 3f }), "turret"),
            Node("def_bunker", "Bunker", "Sturdy block that sprays paint.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Shield", "Lv {0}", 1f,
                Steps(new[] { 4, 6, 9 }, new[] { 450, 750, 1100 }, new[] { 1f, 2f, 3f }), "bunker"),
            Node("def_tank", "Tank", "Patrols and lobs heavy paint shells.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Shield", "Lv {0}", 1f,
                Steps(new[] { 5, 8, 11 }, new[] { 600, 950, 1400 }, new[] { 1f, 2f, 3f }), "tank"),
            Node("def_cannon", "Cannon", "Long range mortar. Squads must dodge.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Target", "Lv {0}", 1f,
                Steps(new[] { 7, 10, 13 }, new[] { 800, 1200, 1700 }, new[] { 1f, 2f, 3f }), "cannon"),
            Node("def_tesla", "Tesla Antenna", "Short range zap trap.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Lightning", "Lv {0}", 1f,
                Steps(new[] { 9, 12, 15 }, new[] { 1000, 1500, 2100 }, new[] { 1f, 2f, 3f }), "tesla"),
        };

        // ------------------------------------------------------------------ AI bases

        struct BaseSpec
        {
            public string id, name, commander; public TeamColorId team; public int diff, level; public float reward, hp, dmg;
        }

        static readonly BaseSpec[] Specs =
        {
            new BaseSpec { id = "b01", name = "Training Grounds", commander = "Sgt. Puddle", team = TeamColorId.Teal, diff = 1, level = 1, reward = 1.0f, hp = 1f, dmg = 1f },
            new BaseSpec { id = "b02", name = "Pine Outpost", commander = "Cpl. Moss", team = TeamColorId.Green, diff = 2, level = 1, reward = 1.15f, hp = 1f, dmg = 1f },
            new BaseSpec { id = "b03", name = "Dusty Crossing", commander = "Lt. Amber", team = TeamColorId.Orange, diff = 3, level = 2, reward = 1.3f, hp = 1.05f, dmg = 1.05f },
            new BaseSpec { id = "b04", name = "Splat Ridge", commander = "Cpt. Violet", team = TeamColorId.Purple, diff = 4, level = 3, reward = 1.5f, hp = 1.1f, dmg = 1.1f },
            new BaseSpec { id = "b05", name = "Magenta Bastion", commander = "Maj. Rosa", team = TeamColorId.Pink, diff = 5, level = 4, reward = 1.7f, hp = 1.18f, dmg = 1.15f },
            new BaseSpec { id = "b06", name = "Cobalt Trench", commander = "Col. Indigo", team = TeamColorId.Blue, diff = 6, level = 5, reward = 1.95f, hp = 1.26f, dmg = 1.2f },
            new BaseSpec { id = "b07", name = "Crimson Gate", commander = "Gen. Scarlet", team = TeamColorId.Red, diff = 7, level = 6, reward = 2.2f, hp = 1.35f, dmg = 1.28f },
            new BaseSpec { id = "b08", name = "Jade Fortress", commander = "Gen. Celadon", team = TeamColorId.Teal, diff = 8, level = 8, reward = 2.5f, hp = 1.45f, dmg = 1.36f },
            new BaseSpec { id = "b09", name = "Thunder Camp", commander = "Marshal Ochre", team = TeamColorId.Orange, diff = 9, level = 10, reward = 2.85f, hp = 1.55f, dmg = 1.45f },
            new BaseSpec { id = "b10", name = "Prism Citadel", commander = "Warlord Lilac", team = TeamColorId.Purple, diff = 10, level = 12, reward = 3.3f, hp = 1.7f, dmg = 1.55f },
        };

        static BaseDefinition[] Bases()
        {
            var list = new BaseDefinition[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                var b = Asset<BaseDefinition>(Dir + "/Base_" + s.id + ".asset");
                b.id = s.id; b.displayName = s.name; b.commanderName = s.commander; b.enemyTeam = s.team;
                b.difficulty = s.diff; b.requiredLevel = s.level; b.rewardMultiplier = s.reward;
                b.enemyHealthMult = s.hp; b.enemyDamageMult = s.dmg;
                if (i == 0)
                {
                    b.layout = null; // handcrafted deployment of Battle.unity
                    b.enemySquads = SceneBuilder.SquadSockets.Length;
                    b.defenses = SceneBuilder.MineSockets.Length + 5;
                }
                else
                {
                    b.layout = Layout(s, 1000 + i * 37);
                    b.enemySquads = b.layout.squads.Length;
                    b.defenses = b.layout.defenses.Length;
                }
                list[i] = b;
            }
            return list;
        }

        /// <summary>Seeded deployment on prop free sockets of the lane (from SceneBuilder), scaled by difficulty.</summary>
        static BaseLayoutData Layout(BaseSpec s, int seed)
        {
            var l = Asset<BaseLayoutData>(LayoutDir + "/Layout_" + s.id + ".asset");
            l.seed = seed;
            var rng = new System.Random(seed);
            int d = s.diff;

            // Squads: more and bigger with difficulty; always one near each capture point.
            var squadSockets = new List<SceneBuilder.SquadSocket>(SceneBuilder.SquadSockets);
            Shuffle(squadSockets, rng);
            int squadCount = Mathf.Clamp(4 + d / 2 + (d >= 8 ? 1 : 0), 4, squadSockets.Count);
            // Guarantee coverage of both capture points.
            var chosen = new List<SceneBuilder.SquadSocket>();
            foreach (var sk in squadSockets) if (sk.pos.z > 40f && sk.pos.z < 60f) { chosen.Add(sk); break; }
            foreach (var sk in squadSockets) if (sk.pos.z > 85f) { chosen.Add(sk); break; }
            foreach (var sk in squadSockets) { if (chosen.Count >= squadCount) break; if (!chosen.Contains(sk)) chosen.Add(sk); }
            var squads = new List<EnemySquadPlacement>();
            for (int i = 0; i < chosen.Count; i++)
            {
                int count = Mathf.Clamp(7 + d + rng.Next(-1, 2), 6, 18);
                bool advance = rng.NextDouble() < 0.35 + d * 0.04;
                squads.Add(new EnemySquadPlacement { position = chosen[i].pos, advance = advance && !chosen[i].holdOnly, count = count, seed = seed + i * 7 });
            }
            l.squads = squads.ToArray();

            var defs = new List<DefensePlacementData>();
            // Mines: a few on the lane, more with difficulty.
            var mines = new List<Vector3>(SceneBuilder.MineSockets);
            Shuffle(mines, rng);
            int mineCount = Mathf.Clamp(3 + d, 3, mines.Count);
            for (int i = 0; i < mineCount; i++) defs.Add(new DefensePlacementData { defenseId = "mine", position = mines[i], yaw = (float)rng.NextDouble() * 360f });
            // Heavy defenses on the authored structure sockets (sandbag nests and pads), unlocked by difficulty.
            var heavy = new List<SceneBuilder.DefenseSocket>(SceneBuilder.DefenseSockets);
            Shuffle(heavy, rng);
            int heavyCount = Mathf.Clamp(1 + (d + 1) / 2, 1, heavy.Count);
            string[] pool = d < 3 ? new[] { "turret", "bunker" } : d < 6 ? new[] { "turret", "bunker", "cannon" } : new[] { "turret", "bunker", "cannon", "tank" };
            for (int i = 0; i < heavyCount; i++)
            {
                var sk = heavy[i];
                string id = sk.allowTank && d >= 6 && rng.NextDouble() < 0.5 ? "tank" : pool[rng.Next(pool.Length)];
                if (id == "tank" && !sk.allowTank) id = "turret";
                var p = new DefensePlacementData { defenseId = id, position = sk.pos, yaw = sk.yaw };
                if (id == "tank") { p.patrolA = sk.pos + new Vector3(-6f, 0f, 0f); p.patrolB = sk.pos + new Vector3(6f, 0f, 0f); }
                defs.Add(p);
            }
            l.defenses = defs.ToArray();
            EditorUtility.SetDirty(l);
            return l;
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
