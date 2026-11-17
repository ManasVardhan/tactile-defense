using System.Collections.Generic;
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
    /// deployment from a BaseLayoutData. Without a pending loadout the scene runs exactly as authored.
    /// Configs are cloned at runtime so ScriptableObject assets are never modified.</summary>
    [DefaultExecutionOrder(-1000)]
    public class BattleSetup : MonoBehaviour
    {
        public BattleManager battleManager;
        public SquadController squad;
        public Transform enemiesRoot;
        public Transform defensesRoot;
        public DefenseCatalog catalog;
        public EnemySquadConfig enemySquadConfig;

        public BattleLoadout Applied { get; private set; }

        readonly Dictionary<Object, Object> clones = new Dictionary<Object, Object>();

        void Awake()
        {
            var lo = BattleLoadout.Pending;
            if (lo == null || battleManager == null) return;
            Applied = lo;
            ApplyTeams(lo);
            ApplyPlayer(lo);
            ApplyEnemies(lo);
        }

        void OnDestroy()
        {
            foreach (var kv in clones) if (kv.Value != null) Destroy(kv.Value);
            clones.Clear();
        }

        T Clone<T>(T src) where T : Object
        {
            if (src == null) return null;
            if (clones.TryGetValue(src, out var c)) return (T)c;
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
            }
            if (catalog == null) return;
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
