using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Applies BattleLoadout.Pending before any other battle script wakes: team colors, research driven
    /// squad and reinforcement stats, the hired mercenary, enemy difficulty and (for generated bases) the enemy
    /// deployment from a BaseLayoutData. Without a pending loadout the scene runs as authored, as an attack battle.
    /// Picks the player's side first: attack battles use the PlayerArmy (the legacy squad and stroke input are
    /// switched off), defense battles the legacy squad driven by the AI (the army is switched off).
    /// Configs are cloned at runtime so ScriptableObject assets are never modified.</summary>
    [DefaultExecutionOrder(-1000)]
    public class BattleSetup : MonoBehaviour
    {
        public BattleManager battleManager;
        public SquadController squad;
        public PlayerArmy army;
        public Transform enemiesRoot;
        public Transform defensesRoot;
        public DefenseCatalog catalog;
        public EnemySquadConfig enemySquadConfig;
        [Header("Map variants and defense battles")]
        [Tooltip("Handcrafted set dressing, replaced when the loadout carries a map.")] public Transform environmentRoot;
        public Renderer groundRenderer;
        public CameraRig.BattleCamera cameraRig;
        [Tooltip("Drives the attacking squad in defense battles (disabled otherwise).")] public AttackerAI attackerAI;
        [Tooltip("The defender's limited paint strike in defense battles (disabled otherwise).")] public DefenderStrike strike;
        [Header("Scene default (no loadout): the handcrafted lane with the classic tutorial deployment")]
        [Tooltip("Data of the handcrafted lane (path, spawn, points, hatches); never applied, its props are the scene's own.")] public MapLayoutData sceneMap;
        [Tooltip("Whether the handcrafted deployment sends a counterattack wave after the first capture.")] public bool sceneCounterattack = true;

        public static BattleSetup Instance { get; private set; }
        public BattleLoadout Applied { get; private set; }
        public MapLayoutData Map { get; private set; }
        public bool DefenseMode { get; private set; }
        readonly List<PlacedDefense> placed = new List<PlacedDefense>();
        public IReadOnlyList<PlacedDefense> Placed => placed;
        Material groundClone;

        readonly Dictionary<Object, Object> clones = new Dictionary<Object, Object>();

        void Awake()
        {
            Instance = this;
            var lo = BattleLoadout.Pending;
            ApplyMode(lo != null && lo.mode == BattleMode.Defense);
            MapPath.Set(lo != null && lo.map != null ? lo.map.path : sceneMap != null ? sceneMap.path : null);
            if (battleManager == null) return;
            if (lo == null)
            {
                Map = sceneMap;
                // The scene as authored is Level 1: its squads carry their own passive flag; the counterattack is set here.
                ApplyCounterattack(sceneCounterattack);
                return;
            }
            Applied = lo;
            ApplyTeams(lo);
            if (lo.map != null) ApplyMap(lo.map);
            ApplyPlayer(lo);
            if (lo.mode == BattleMode.Defense) ApplyDefense(lo);
            else
            {
                ApplyEnemies(lo);
                ApplyCounterattack(lo.counterattack);
            }
        }

        /// <summary>Per-base rule (attack battles): switches the counterattack wave off on a runtime copy of the config.</summary>
        void ApplyCounterattack(bool on)
        {
            var bc = battleManager.battleConfig;
            if (bc == null || bc.counterattackEnabled == on) return;
            bc = Clone(bc);
            bc.counterattackEnabled = on;
            battleManager.battleConfig = bc;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; MapPath.Clear(); }
            if (groundClone != null) Destroy(groundClone);
            foreach (var kv in clones) if (kv.Value != null) Destroy(kv.Value);
            clones.Clear();
        }

        /// <summary>Switches off the side this battle does not use and points the camera at the one it does.</summary>
        void ApplyMode(bool defense)
        {
            DefenseMode = defense;
            if (defense)
            {
                if (army != null) army.gameObject.SetActive(false);
                if (cameraRig != null) cameraRig.PanMode = false;
                return;
            }
            if (squad != null) squad.gameObject.SetActive(false);
            if (battleManager != null && battleManager.recorder != null) battleManager.recorder.enabled = false;
            if (cameraRig != null) cameraRig.PanMode = army != null;
        }

        T Clone<T>(T src) where T : Object
        {
            if (src == null) return null;
            if (clones.TryGetValue(src, out var c)) return (T)c;
            if (clones.ContainsValue(src)) return src; // already a runtime copy
            var copy = Instantiate(src);
            copy.name = src.name + "_Runtime";
            clones[src] = copy;
            return copy;
        }

        void ApplyTeams(BattleLoadout lo)
        {
            var gc = Clone(battleManager.gameConfig);
            if (gc == null) return;
            gc.playerTeam = lo.playerTeam;
            gc.enemyTeam = lo.enemyTeam == lo.playerTeam ? (lo.playerTeam == TeamColorId.Teal ? TeamColorId.Purple : TeamColorId.Teal) : lo.enemyTeam;
            battleManager.gameConfig = gc;
            // The squad ring and blot were baked in the default color: swap to the chosen team's shared materials.
            if (squad != null && squad.ring != null && gc.visuals != null)
            {
                var m = gc.visuals.Get(gc.playerTeam);
                if (m != null)
                {
                    SetMat(squad.ring.ring, m.ring);
                    SetMat(squad.ring.blot, m.blot);
                }
            }
        }

        static void SetMat(Transform t, Material m)
        {
            if (t == null || m == null) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        void ApplyPlayer(BattleLoadout lo)
        {
            var bc = Clone(battleManager.battleConfig);
            if (bc != null)
            {
                bc.reinforcementCount = Mathf.Max(1, lo.reinforcementCount);
                float charge = Mathf.Max(0.2f, lo.reinforcementChargeMult * (1f + (lo.mercenary != null ? lo.mercenary.reinforcementChargeBonus : 0f)));
                bc.meterRequired = Mathf.Max(5f, bc.meterRequired / charge);
                bc.goldBase = Mathf.RoundToInt(bc.goldBase * lo.rewardMult);
                bc.goldPerKill = Mathf.Max(1, Mathf.RoundToInt(bc.goldPerKill * lo.rewardMult));
                bc.goldPerStructure = Mathf.RoundToInt(bc.goldPerStructure * lo.rewardMult);
                bc.goldVictoryBonus = Mathf.RoundToInt(bc.goldVictoryBonus * lo.rewardMult);
                bc.xpBase = Mathf.RoundToInt(bc.xpBase * lo.rewardMult);
                bc.xpPerKill = Mathf.Max(1, Mathf.RoundToInt(bc.xpPerKill * lo.rewardMult));
                bc.xpVictoryBonus = Mathf.RoundToInt(bc.xpVictoryBonus * lo.rewardMult);
                battleManager.battleConfig = bc;
            }
            if (!DefenseMode)
            {
                // Attack: every unit type scales this soldier. Squad size and the leader are legacy squad stats and
                // do not apply to the army. The chosen squad replaces the default; a hired mercenary joins after it.
                if (army == null) return;
                if (army.soldierConfig != null)
                    army.soldierConfig = ScaledUnit(army.soldierConfig, lo.soldierHealthMult, lo.damageMult, lo.fireRateMult, lo.moveSpeedMult, "_Army");
                if (lo.squad != null && lo.squad.Count > 0) army.SetSquad(lo.squad);
                if (lo.mercenary != null) army.SetMercenary(MercenaryType(lo));
                return;
            }
            if (squad == null) return;
            var sc = Clone(squad.config);
            if (sc != null) { sc.soldierCount = Mathf.Clamp(lo.squadSize, 1, 120); squad.config = sc; }

            var baseUnit = squad.soldierConfig;
            if (baseUnit == null) return;
            var unit = ScaledUnit(baseUnit, lo.soldierHealthMult, lo.damageMult, lo.fireRateMult, lo.moveSpeedMult, "_Squad");
            squad.soldierConfig = unit;
            if (!Mathf.Approximately(lo.leaderMult, 1f))
                squad.leaderConfig = ScaledUnit(unit, lo.leaderMult, lo.leaderMult, 1f, 1f, "_Leader");
            if (lo.mercenary != null)
            {
                var m = lo.mercenary;
                squad.mercenaryConfig = ScaledUnit(unit, m.healthMult * lo.leaderMult, m.damageMult * lo.leaderMult, m.fireRateMult, 1f, "_Merc_" + m.id);
                squad.mercenaryScale = m.scale;
                squad.mercenaryName = m.displayName;
            }
        }

        /// <summary>The hired mercenary as a one soldier unit type: health and damage as the legacy squad's mercenary
        /// (its multipliers times Mercenary Training), on top of the research scaled army soldier.</summary>
        UnitTypeConfig MercenaryType(BattleLoadout lo)
        {
            var m = lo.mercenary;
            var t = ScriptableObject.CreateInstance<UnitTypeConfig>();
            t.name = "UnitType_Mercenary_" + m.id;
            clones[t] = t;
            t.id = PlayerArmy.MercenaryTypeId;
            t.displayName = string.IsNullOrEmpty(m.displayName) ? "Mercenary" : m.displayName;
            t.glyph = "M";
            t.soldierCount = 1;
            t.healthMult = m.healthMult * lo.leaderMult;
            t.damageMult = m.damageMult * lo.leaderMult;
            t.fireIntervalMult = 1f / Mathf.Max(0.1f, m.fireRateMult);
            t.modelScale = m.scale;
            t.accent = m.accent;
            t.description = m.description;
            return t;
        }

        /// <summary>Copy of a unit config (and its weapon) with multipliers applied. Not cached: each call is distinct.</summary>
        UnitConfig ScaledUnit(UnitConfig src, float health, float damage, float fireRate, float speed, string suffix)
        {
            var u = Instantiate(src);
            u.name = src.name + suffix;
            clones[u] = u;
            u.maxHealth = src.maxHealth * health;
            u.maxSpeed = src.maxSpeed * speed;
            if (src.weapon != null)
            {
                var w = Instantiate(src.weapon);
                w.name = src.weapon.name + suffix;
                clones[w] = w;
                w.damage = src.weapon.damage * damage;
                w.fireInterval = src.weapon.fireInterval / Mathf.Max(0.1f, fireRate);
                u.weapon = w;
            }
            return u;
        }

        void ApplyEnemies(BattleLoadout lo)
        {
            bool scale = !Mathf.Approximately(lo.enemyHealthMult, 1f) || !Mathf.Approximately(lo.enemyDamageMult, 1f);
            EnemySquadConfig squadCfg = enemySquadConfig;
            if (scale && squadCfg != null)
            {
                squadCfg = Clone(squadCfg);
                if (squadCfg.soldier != null) squadCfg.soldier = ScaledUnit(enemySquadConfig.soldier, lo.enemyHealthMult, lo.enemyDamageMult, 1f, 1f, "_Enemy");
                if (squadCfg.flagBearer != null) squadCfg.flagBearer = ScaledUnit(enemySquadConfig.flagBearer, lo.enemyHealthMult, lo.enemyDamageMult, 1f, 1f, "_Bearer");
            }

            if (squadCfg != null) battleManager.counterSquadConfig = squadCfg;
            if (lo.layout != null)
            {
                Clear(enemiesRoot);
                Clear(defensesRoot);
                SpawnLayout(lo.layout, squadCfg);
            }
            else if (scale && enemiesRoot != null)
            {
                foreach (var es in enemiesRoot.GetComponentsInChildren<EnemySquad>(true)) es.config = squadCfg;
            }
            // Per-base rule: passive squads wait until approached. Applies to the scene's squads and spawned ones.
            if (enemiesRoot != null)
                foreach (var es in enemiesRoot.GetComponentsInChildren<EnemySquad>(true)) es.passive = lo.passiveEnemies;

            if (scale && defensesRoot != null)
            {
                foreach (var s in defensesRoot.GetComponentsInChildren<StructureBase>(true))
                {
                    if (s.config == null) continue;
                    var sc = Clone(s.config);
                    sc.maxHealth = s.config.maxHealth * lo.enemyHealthMult;
                    sc.explosionDamage = s.config.explosionDamage * lo.enemyDamageMult;
                    if (s.config.weapon != null)
                    {
                        var w = Clone(s.config.weapon);
                        w.damage = s.config.weapon.damage * lo.enemyDamageMult;
                        sc.weapon = w;
                    }
                    s.config = sc;
                }
            }
        }

        // ------------------------------------------------------------ map variants

        void ApplyMap(MapLayoutData map)
        {
            Map = map;
            if (environmentRoot != null)
            {
                Clear(environmentRoot);
                MapApplier.SpawnProps(map, environmentRoot);
            }
            groundClone = MapApplier.ApplyGround(map, groundRenderer);
            MapApplier.FitGround(map, groundRenderer);
            if (map.biome != null)
                map.biome.ApplyLighting(RenderSettings.sun, FindFirstObjectByType<Core.SceneLighting>(), cameraRig != null ? cameraRig.Cam : null);
            var cps = battleManager.capturePoints;
            for (int i = 0; i < cps.Length && i < map.capturePoints.Length; i++)
                if (cps[i] != null) cps[i].transform.position = map.capturePoints[i];
            if (squad != null && squad.spawnPoint != null) squad.spawnPoint.position = map.playerSpawn;
            if (squad != null && squad.ring != null) squad.ring.transform.position = map.playerSpawn;
            if (army != null) army.spawnPoint = map.playerSpawn;
            var gc = Clone(battleManager.gameConfig);
            gc.mapMin = map.mapMin;
            gc.mapMax = map.mapMax;
            battleManager.gameConfig = gc;
            if (cameraRig != null && cameraRig.config != null)
            {
                var cc = Clone(cameraRig.config);
                cc.focusMin = map.focusMin;
                cc.focusMax = map.focusMax;
                cameraRig.config = cc;
                cameraRig.transform.position = map.playerSpawn;
            }
            battleManager.counterSpawns = map.spawnHatches;
            if (squad != null) squad.InvalidateNav();
        }

        // ------------------------------------------------------------ defense battles

        void ApplyDefense(BattleLoadout lo)
        {
            DefenseMode = true;
            Clear(enemiesRoot);
            Clear(defensesRoot);
            battleManager.DefenseMode = true;
            battleManager.TimeLimit = lo.defenseTimeLimit;
            if (battleManager.recorder != null) battleManager.recorder.InputEnabled = false;
            if (attackerAI != null) { attackerAI.squad = squad; attackerAI.battle = battleManager; attackerAI.enabled = true; }
            if (strike != null) { strike.charges = lo.strikeCharges; strike.enabled = lo.strikeCharges > 0; }
            for (int i = 0; i < lo.defenderSpawns.Count; i++)
            {
                var d = lo.defenderSpawns[i];
                PlacedDefense pd;
                if (d.defenseId == DefenseCatalog.SquadId)
                {
                    var g = new GameObject("Defenders_" + i);
                    g.transform.SetParent(enemiesRoot != null ? enemiesRoot : defensesRoot, false);
                    g.transform.position = d.position;
                    var es = g.AddComponent<EnemySquad>();
                    es.config = ScaledSquad(d.levelMult);
                    es.advance = false;
                    es.soldierCountOverride = Mathf.Max(1, lo.defenderSquadSize);
                    es.seed = 300 + i * 13;
                    pd = g.AddComponent<PlacedDefense>();
                    pd.squad = es;
                }
                else
                {
                    var prefab = catalog != null ? catalog.Get(d.defenseId) : null;
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, d.position, Quaternion.Euler(0f, d.yaw, 0f), defensesRoot);
                    go.name = d.defenseId + "_" + i;
                    var sb = go.GetComponent<StructureBase>();
                    if (sb != null) { sb.levelMultiplier = d.levelMult; sb.team = Team.Enemy; }
                    var tank = go.GetComponent<Tank>();
                    if (tank != null)
                    {
                        Vector3 side = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.right * 3f;
                        tank.patrolA = d.position - side;
                        tank.patrolB = d.position + side;
                    }
                    pd = go.AddComponent<PlacedDefense>();
                    pd.structure = sb;
                }
                pd.placementIndex = d.placementIndex;
                pd.defenseId = d.defenseId;
                placed.Add(pd);
            }
        }

        readonly Dictionary<float, EnemySquadConfig> squadByLevel = new Dictionary<float, EnemySquadConfig>();

        EnemySquadConfig ScaledSquad(float mult)
        {
            if (enemySquadConfig == null) return null;
            if (Mathf.Approximately(mult, 1f)) return enemySquadConfig;
            if (squadByLevel.TryGetValue(mult, out var c)) return c;
            c = Instantiate(enemySquadConfig);
            clones[c] = c;
            if (enemySquadConfig.soldier != null) c.soldier = ScaledUnit(enemySquadConfig.soldier, mult, mult, 1f, 1f, "_Def");
            if (enemySquadConfig.flagBearer != null) c.flagBearer = ScaledUnit(enemySquadConfig.flagBearer, mult, mult, 1f, 1f, "_DefBearer");
            squadByLevel[mult] = c;
            return c;
        }

        /// <summary>Placement indices of the player's defenses consumed so far (destroyed, fired or wiped).</summary>
        public void CollectConsumed(List<int> into)
        {
            into.Clear();
            for (int i = 0; i < placed.Count; i++)
                if (placed[i] == null || placed[i].Consumed) into.Add(placed[i] != null ? placed[i].placementIndex : -1);
        }

        static void Clear(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i).gameObject;
                c.SetActive(false);   // stops Start from running this frame
                Destroy(c);
            }
        }

        void SpawnLayout(BaseLayoutData layout, EnemySquadConfig squadCfg)
        {
            for (int i = 0; i < layout.squads.Length; i++)
            {
                var s = layout.squads[i];
                var g = new GameObject($"EnemySquad_{i + 1}{(s.advance ? "_Advance" : "_Hold")}");
                g.transform.SetParent(enemiesRoot, false);
                g.transform.position = s.position;
                var es = g.AddComponent<EnemySquad>();
                es.config = squadCfg;
                es.advance = s.advance;
                es.soldierCountOverride = s.count;
                es.seed = s.seed;
                es.withFlagBearer = !s.noFlagBearer;
            }
            if (catalog == null) return;
            if (layout.counterSpawnsOverride != null && layout.counterSpawnsOverride.Length > 0) battleManager.counterSpawns = layout.counterSpawnsOverride;
            for (int i = 0; i < layout.defenses.Length; i++)
            {
                var d = layout.defenses[i];
                var prefab = catalog.Get(d.defenseId);
                if (prefab == null) continue;
                var go = Instantiate(prefab, d.position, Quaternion.Euler(0f, d.yaw, 0f), defensesRoot);
                go.name = d.defenseId + "_" + i;
                var tank = go.GetComponent<Tank>();
                if (tank != null && d.patrolA != d.patrolB) { tank.patrolA = d.patrolA; tank.patrolB = d.patrolB; }
            }
        }
    }
}
