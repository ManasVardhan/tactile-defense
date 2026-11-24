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
    /// ProgressionConfig, ResearchTree, three mercenaries, the defense catalog, the five ladder levels (each on its own
    /// MapBuilder map; Level 1 with a hand placed tutorial deployment, the others with a seeded deployment on their
    /// map's sockets) and Assets/Resources/MetaDatabase.
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
            catalog.entries = CatalogEntries();

            maps = MapBuilder.BuildAll();
            var baseCfg = AssetDatabase.LoadAssetAtPath<BaseEditorConfig>(Dir + "/BaseEditorConfig.asset");
            if (baseCfg == null) { baseCfg = ScriptableObject.CreateInstance<BaseEditorConfig>(); AssetDatabase.CreateAsset(baseCfg, Dir + "/BaseEditorConfig.asset"); }

            var roster = Asset<BaseRoster>(Dir + "/BaseRoster.asset");
            roster.bases = Bases();

            var db = Asset<MetaDatabase>(DbPath);
            db.progression = prog;
            db.research = research;
            db.mercenaries = mercs;
            db.roster = roster;
            db.units = AssetDatabase.LoadAssetAtPath<TactileWars.Army.UnitRoster>("Assets/Data/UnitRoster.asset");
            db.defenses = catalog;
            db.game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            db.squadConfig = AssetDatabase.LoadAssetAtPath<SquadConfig>("Assets/Data/SquadConfig.asset");
            db.battleConfig = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            db.playerSoldier = AssetDatabase.LoadAssetAtPath<UnitConfig>("Assets/Data/Unit_PlayerSoldier.asset");
            db.baseEditor = baseCfg;
            db.defenseBattle = AssetDatabase.LoadAssetAtPath<DefenseBattleConfig>("Assets/Data/DefenseBattleConfig.asset");
            db.homeMap = maps[0];
            var levelMaps = new List<MapLayoutData>();
            for (int l = 1; l <= Progression.LevelCount; l++) levelMaps.Add(MapBuilder.LevelMap(maps, l));
            db.maps = levelMaps.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuild] meta data: " + research.nodes.Length + " research nodes, " + mercs.Length + " mercenaries, " + roster.bases.Length + " bases");
            return db;
        }

        static MapLayoutData[] maps;

        static GameObject StructPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/" + name + ".prefab");

        /// <summary>Defense types with their editor economy. Names verified; ALL NUMBERS ARE ESTIMATES (budget and
        /// space points from GAMEPLAY_MECHANICS section 11, gold costs and limits are our own).</summary>
        static DefenseCatalog.Entry[] CatalogEntries()
        {
            DefenseCatalog.Entry E(string id, string name, string prefab, int gold, int budget, int space, int max, float foot, float range, string research, bool trap, string desc) =>
                new DefenseCatalog.Entry
                {
                    id = id, displayName = name, prefab = prefab != null ? StructPrefab(prefab) : null, goldCost = gold, budget = budget, space = space,
                    maxCount = max, footprint = foot, range = range, researchId = research, trap = trap, squadSize = id == "squad" ? 8 : 0, description = desc,
                };
            return new[]
            {
                E("mine", "Paint Mine", "Mine", 15, 4, 1, 10, 0.6f, 1.3f, "def_mine", true, "Hidden disc. Bursts when attackers step close. Single use."),
                E("geyser", "Geyser", "Geyser", 30, 6, 1, 6, 0.9f, 2.8f, "def_geyser", true, "Hidden vent. Erupts paint twice when attackers come near, then it is spent."),
                E("turret", "Turret", "Turret", 60, 10, 1, 4, 1.1f, 9f, "def_turret", false, "Rotating paint turret with splash bursts."),
                E("squad", "Defender Squad", null, 40, 8, 2, 3, 2.4f, 6f, "", false, "Eight pigments and a flag bearer hold this spot."),
                E("tesla", "Tesla Antenna", "Tesla", 90, 12, 1, 3, 0.8f, 5.5f, "def_tesla", false, "Charges, then a paint lightning arc chains through nearby soldiers."),
                E("sniper", "Sniper Tower", "Sniper", 100, 14, 1, 2, 0.9f, 16f, "def_sniper", false, "Very long range. Aims, then one heavy shot. Picks flag bearers first."),
                E("bunker", "Bunker", "Bunker", 110, 18, 3, 2, 1.7f, 8f, "def_bunker", false, "Sturdy block that sprays paint in every direction."),
                E("cannon", "Cannon", "Cannon", 120, 15, 2, 2, 1.0f, 12.5f, "def_cannon", false, "Long range mortar. Lobbed shells splash an area."),
                E("tank", "Tank", "Tank", 150, 20, 2, 2, 1.4f, 10f, "def_tank", false, "Patrols sideways and lobs heavy shells."),
            };
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
            // Troops (Squad Size is retired: cleared ladder levels give squad slots)
            Node("soldier_health", "Thick Helmets", "Each soldier takes more paint before popping.", ResearchBranch.Troops, ResearchEffect.SoldierHealth, "Heart", "+{0}%", 100f,
                Steps(new[] { 1, 3, 5, 7, 10 }, new[] { 120, 260, 480, 800, 1250 }, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f })),
            Node("fire_rate", "Rapid Pumps", "Paint guns fire faster.", ResearchBranch.Troops, ResearchEffect.FireRate, "Lightning", "+{0}%", 100f,
                Steps(new[] { 2, 3, 5, 8, 11 }, new[] { 180, 340, 600, 950, 1450 }, new[] { 0.08f, 0.16f, 0.24f, 0.32f, 0.4f })),
            Node("paint_damage", "Heavy Pigment", "Every paint dot hits harder.", ResearchBranch.Troops, ResearchEffect.PaintDamage, "Target", "+{0}%", 100f,
                Steps(new[] { 2, 4, 6, 9, 12 }, new[] { 200, 380, 650, 1000, 1500 }, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f })),
            Node("move_speed", "Light Boots", "The squad marches and reforms faster.", ResearchBranch.Troops, ResearchEffect.MoveSpeed, "Boot", "+{0}%", 100f,
                Steps(new[] { 3, 6, 10 }, new[] { 260, 600, 1100 }, new[] { 0.06f, 0.12f, 0.18f })),
            // Offense (Bigger Backup is retired: a backup call restores a lost unit)
            Node("reinforcement_charge", "Quick Call", "The POW meter fills faster.", ResearchBranch.Offense, ResearchEffect.ReinforcementCharge, "Pow", "+{0}%", 100f,
                Steps(new[] { 3, 5, 8, 11 }, new[] { 260, 520, 880, 1300 }, new[] { 0.12f, 0.24f, 0.36f, 0.5f })),
            Node("hero_ability", "Mercenary Training", "Your hired mercenary gets tougher and hits harder.", ResearchBranch.Offense, ResearchEffect.HeroAbility, "Mercenary", "+{0}%", 100f,
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
            Node("def_geyser", "Geyser", "Hidden vent that erupts paint on attackers.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Skull", "Lv {0}", 1f,
                Steps(new[] { 3, 6, 9 }, new[] { 350, 650, 1000 }, new[] { 1f, 2f, 3f }), "geyser"),
            Node("def_sniper", "Sniper Tower", "Long range shots that pick off flag bearers.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Target", "Lv {0}", 1f,
                Steps(new[] { 6, 9, 12 }, new[] { 700, 1100, 1600 }, new[] { 1f, 2f, 3f }), "sniper"),
            Node("def_tesla", "Tesla Antenna", "Charges, then chains lightning through nearby soldiers.", ResearchBranch.Defense, ResearchEffect.DefenseUnlock, "Lightning", "Lv {0}", 1f,
                Steps(new[] { 8, 11, 14 }, new[] { 900, 1400, 2000 }, new[] { 1f, 2f, 3f }), "tesla"),
        };

        // ------------------------------------------------------------------ AI bases

        struct BaseSpec
        {
            public string id, name, commander; public TeamColorId team; public int diff, level; public float reward, hp, dmg;
            public bool tutorial; // passive enemies, no counterattack and the hand placed deployment (Level 1)
        }

        // Phase 8 balance (ESTIMATE, tuned with the BotPlaythroughTests bot). Level N plays on MapBuilder's "levelN" map.
        // Enemy colors are picked for contrast with the level's ground: no orange or red on the warm canyon, no green
        // on the meadow.
        static readonly BaseSpec[] Specs =
        {
            new BaseSpec { id = "b01", name = "Training Field", commander = "Sgt. Puddle", team = TeamColorId.Teal, diff = 1, level = 1, reward = 1.0f, hp = 1.0f, dmg = 0.9f, tutorial = true },
            new BaseSpec { id = "b02", name = "Long Road", commander = "Cpl. Moss", team = TeamColorId.Orange, diff = 2, level = 1, reward = 1.15f, hp = 0.95f, dmg = 0.85f },
            new BaseSpec { id = "b03", name = "Overlook", commander = "Lt. Amber", team = TeamColorId.Blue, diff = 3, level = 2, reward = 1.3f, hp = 1.05f, dmg = 0.95f },
            new BaseSpec { id = "b04", name = "Twin Passes", commander = "Cpt. Violet", team = TeamColorId.Red, diff = 4, level = 3, reward = 1.5f, hp = 1.15f, dmg = 1.02f },
            new BaseSpec { id = "b05", name = "The Fort", commander = "Maj. Rosa", team = TeamColorId.Purple, diff = 5, level = 4, reward = 1.7f, hp = 1.3f, dmg = 1.1f },
        };

        /// <summary>Retired bases of the ten level ladder: their generated assets are deleted on every build.</summary>
        static readonly string[] Retired = { "b06", "b07", "b08", "b09", "b10" };

        static BaseDefinition[] Bases()
        {
            foreach (var id in Retired)
            {
                if (AssetDatabase.LoadAssetAtPath<BaseDefinition>(Dir + "/Base_" + id + ".asset") != null) AssetDatabase.DeleteAsset(Dir + "/Base_" + id + ".asset");
                if (AssetDatabase.LoadAssetAtPath<BaseLayoutData>(LayoutDir + "/Layout_" + id + ".asset") != null) AssetDatabase.DeleteAsset(LayoutDir + "/Layout_" + id + ".asset");
            }
            var list = new BaseDefinition[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                var b = Asset<BaseDefinition>(Dir + "/Base_" + s.id + ".asset");
                b.id = s.id; b.displayName = s.name; b.commanderName = s.commander; b.enemyTeam = s.team;
                b.difficulty = s.diff; b.requiredLevel = s.level; b.rewardMultiplier = s.reward;
                b.enemyHealthMult = s.hp; b.enemyDamageMult = s.dmg;
                b.map = MapBuilder.LevelMap(maps, i + 1);
                b.passiveEnemies = s.tutorial;
                b.counterattack = !s.tutorial;
                b.layout = s.tutorial ? LevelOneLayout(s, b.map) : Layout(s, 1000 + i * 37, b.map);
                b.enemySquads = b.layout.squads.Length;
                b.averageSquadSize = AverageSquadSize(b.layout);
                b.defenses = b.layout.defenses.Length;
                list[i] = b;
            }
            return list;
        }

        /// <summary>Soldiers per squad in a layout: its grunts plus the flag bearer, when it has one.</summary>
        static float AverageSquadSize(BaseLayoutData l)
        {
            if (l.squads == null || l.squads.Length == 0) return 0f;
            float sum = 0f;
            foreach (var q in l.squads) sum += q.count + (q.noFlagBearer ? 0 : 1);
            return sum / l.squads.Length;
        }

        /// <summary>Level 1, the tutorial (design spec 6.1), hand placed on its map: 4 grunts with no flag bearer
        /// standing on circle 1, then 5 grunts and a flag bearer by circle 2 with one turret covering it. The squads
        /// are passive and there are no traps and no counterattack (BaseDefinition flags).</summary>
        static BaseLayoutData LevelOneLayout(BaseSpec s, MapLayoutData map)
        {
            var l = Asset<BaseLayoutData>(LayoutDir + "/Layout_" + s.id + ".asset");
            l.seed = 0;
            MapBuilder.LevelOneDeployment(map, out Vector3 guard1, out Vector3 guard2, out Vector3 turret);
            l.squads = new[]
            {
                new EnemySquadPlacement { position = guard1, advance = false, count = 4, seed = 11, noFlagBearer = true },
                new EnemySquadPlacement { position = guard2, advance = true, count = 5, seed = 17 },
            };
            l.defenses = new[] { new DefensePlacementData { defenseId = "turret", position = turret, yaw = MapBuilder.FacingYaw(map, turret) } };
            l.counterSpawnsOverride = new Vector3[0];
            EditorUtility.SetDirty(l);
            return l;
        }

        struct SquadSock { public Vector3 pos; public bool holdOnly; }
        struct HeavySock { public Vector3 pos; public float yaw; public bool allowTank; }

        /// <summary>Seeded deployment on the prop free sockets of the level's map (MapBuilder), scaled by difficulty. Higher difficulties add geysers (d3+), sniper towers
        /// (d4+) and tesla antennas (d5+). All counts are estimates.</summary>
        static BaseLayoutData Layout(BaseSpec s, int seed, MapLayoutData map)
        {
            var l = Asset<BaseLayoutData>(LayoutDir + "/Layout_" + s.id + ".asset");
            l.seed = seed;
            var rng = new System.Random(seed);
            int d = s.diff;

            var squadSockets = new List<SquadSock>();
            var mines = new List<Vector3>();
            var heavy = new List<HeavySock>();
            Vector3 cp1 = map.capturePoints[0], cp2 = map.capturePoints[1];
            foreach (var p in map.squadSockets) squadSockets.Add(new SquadSock { pos = p, holdOnly = rng.NextDouble() < 0.4 });
            mines.AddRange(map.mineSockets);
            foreach (var p in map.defenseSockets)
            {
                // Face back down the map's path, toward where the player comes from.
                heavy.Add(new HeavySock { pos = p, yaw = MapBuilder.FacingYaw(map, p), allowTank = false });
            }
            Shuffle(squadSockets, rng);
            int squadCount = Mathf.Clamp(4 + d / 2 + (d >= 8 ? 1 : 0), 4, squadSockets.Count);
            var chosen = new List<SquadSock>();
            // Guarantee coverage of both capture points.
            foreach (var sk in squadSockets) if (Vector3.Distance(sk.pos, cp1) < 14f) { chosen.Add(sk); break; }
            foreach (var sk in squadSockets) if (Vector3.Distance(sk.pos, cp2) < 14f && !chosen.Contains(sk)) { chosen.Add(sk); break; }
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
            // Traps on the lane: mines, plus geysers from difficulty 3, tesla antennas (short range) from 5.
            Shuffle(mines, rng);
            int geysers = d >= 3 ? 1 + (d - 3) / 3 : 0;
            int teslas = d >= 5 ? 1 + (d - 5) / 3 : 0;
            int mineCount = Mathf.Clamp(3 + d, 3, Mathf.Max(3, mines.Count - geysers - teslas));
            int mi = 0;
            for (int i = 0; i < teslas && mi < mines.Count; i++) defs.Add(new DefensePlacementData { defenseId = "tesla", position = mines[mi++], yaw = 180f });
            for (int i = 0; i < geysers && mi < mines.Count; i++) defs.Add(new DefensePlacementData { defenseId = "geyser", position = mines[mi++], yaw = (float)rng.NextDouble() * 360f });
            for (int i = 0; i < mineCount && mi < mines.Count; i++) defs.Add(new DefensePlacementData { defenseId = "mine", position = mines[mi++], yaw = (float)rng.NextDouble() * 360f });
            // Heavy defenses on the structure sockets, unlocked by difficulty; one sniper tower from difficulty 4.
            Shuffle(heavy, rng);
            int heavyCount = Mathf.Clamp(1 + (d + 1) / 2, 1, heavy.Count);
            string[] pool = d < 3 ? new[] { "turret", "bunker" } : d < 6 ? new[] { "turret", "bunker", "cannon" } : new[] { "turret", "bunker", "cannon", "tank", "sniper" };
            for (int i = 0; i < heavyCount; i++)
            {
                var sk = heavy[i];
                string id = i == 0 && d >= 4 ? "sniper" : (sk.allowTank && d >= 6 && rng.NextDouble() < 0.5 ? "tank" : pool[rng.Next(pool.Length)]);
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
