using System.Collections.Generic;
using System.IO;
using System.Linq;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEditor;
using UnityEngine;

namespace TactileWars.EditorTools
{
    public static partial class AssetBuilder
    {
        public class Data
        {
            public GameConfig game;
            public TeamPalette palette;
            public TeamVisuals visuals;
            public FormationConfig formation;
            public SquadConfig squad;
            public UnitConfig playerSoldier, enemySoldier, flagBearer;
            public WeaponConfig wSoldier, wEnemy, wTurret, wTank, wCannon, wBunker, wSniper;
            public StructureConfig turret, tank, cannon, bunker, mine, tesla, sniper, geyser;
            public DefenseBattleConfig defense;
            public EnemySquadConfig enemySquad;
            public BattleConfig battle;
            public CombatConfig combat;
            public CameraConfig camera;
            public BattlePrefabs prefabs;
            public SoundLibrary sounds;
        }

        /// <summary>Loads an asset or creates it with builder defaults. With reset, values are rewritten.</summary>
        static T Asset<T>(string name, bool reset, System.Action<T> defaults) where T : ScriptableObject
        {
            string path = DataDir + "/" + name + ".asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            bool fresh = a == null;
            if (fresh)
            {
                a = ScriptableObject.CreateInstance<T>();
                defaults?.Invoke(a);
                AssetDatabase.CreateAsset(a, path);
            }
            else if (reset)
            {
                var tmp = ScriptableObject.CreateInstance<T>();
                defaults?.Invoke(tmp);
                EditorUtility.CopySerialized(tmp, a);
                Object.DestroyImmediate(tmp);
            }
            EditorUtility.SetDirty(a);
            return a;
        }

        static Data BuildData(bool reset, Mats mats)
        {
            var d = new Data();
            d.palette = Asset<TeamPalette>("TeamPalette", true, p =>
            {
                p.teams = TeamHex.Select(t => new TeamColors { id = t.id, primary = Hex(t.primary), dark = Hex(t.dark), paint = Hex(t.paint) }).ToArray();
            });
            d.visuals = Asset<TeamVisuals>("TeamVisuals", true, null);
            d.formation = Asset<FormationConfig>("FormationConfig", reset, null);
            d.squad = Asset<SquadConfig>("SquadConfig", reset, null);

            d.wSoldier = Asset<WeaponConfig>("Weapon_PlayerSoldier", reset, w =>
            {
                w.range = 6.5f; w.fireInterval = 1.15f; w.intervalJitter = 0.4f; w.damage = 3f; w.projectileSpeed = 11f;
                w.spreadDegrees = 4f; w.projectileScale = 0.3f; w.burstCount = 3; w.burstGap = 0.075f; w.fireSound = SoundId.Shoot;
            });
            d.wEnemy = Asset<WeaponConfig>("Weapon_EnemySoldier", reset, w =>
            {
                w.range = 6f; w.fireInterval = 1.3f; w.intervalJitter = 0.5f; w.damage = 1.7f; w.projectileSpeed = 10f;
                w.spreadDegrees = 7f; w.projectileScale = 0.3f; w.burstCount = 3; w.burstGap = 0.085f; w.fireSound = SoundId.Shoot;
            });
            d.wTurret = Asset<WeaponConfig>("Weapon_Turret", reset, w =>
            {
                w.range = 9f; w.fireInterval = 1.4f; w.intervalJitter = 0.2f; w.damage = 6f; w.projectileSpeed = 14f; w.spreadDegrees = 3f;
                w.projectileScale = 0.36f; w.burstCount = 3; w.burstGap = 0.12f; w.splashRadius = 0.9f; w.splatSize = 0.9f; w.heavy = true;
                w.fireSound = SoundId.TurretShot; w.impactShake = 0f;
            });
            d.wTank = Asset<WeaponConfig>("Weapon_Tank", reset, w =>
            {
                w.range = 10f; w.fireInterval = 2.4f; w.intervalJitter = 0.4f; w.damage = 12f; w.projectileSpeed = 11f; w.spreadDegrees = 4f;
                w.projectileScale = 0.5f; w.arcHeight = 1.6f; w.splashRadius = 1.8f; w.splatSize = 1.2f; w.heavy = true; w.fireSound = SoundId.TankShot; w.impactShake = 0.18f;
            });
            d.wCannon = Asset<WeaponConfig>("Weapon_Cannon", reset, w =>
            {
                w.range = 12.5f; w.fireInterval = 3.2f; w.intervalJitter = 0.5f; w.damage = 14f; w.projectileSpeed = 9f; w.spreadDegrees = 5f;
                w.projectileScale = 0.55f; w.arcHeight = 4f; w.splashRadius = 2.0f; w.splatSize = 1.3f; w.heavy = true; w.fireSound = SoundId.CannonShot; w.impactShake = 0.25f;
            });
            d.wBunker = Asset<WeaponConfig>("Weapon_Bunker", reset, w =>
            {
                w.range = 8f; w.fireInterval = 0.32f; w.intervalJitter = 0.12f; w.damage = 2f; w.projectileSpeed = 15f; w.spreadDegrees = 6f;
                w.projectileScale = 0.22f; w.fireSound = SoundId.Shoot;
            });

            d.wSniper = Asset<WeaponConfig>("Weapon_Sniper", reset, w =>
            {
                w.range = 16f; w.fireInterval = 2.4f; w.intervalJitter = 0f; w.damage = 50f; w.projectileSpeed = 45f; w.spreadDegrees = 0.5f;
                w.projectileScale = 0.42f; w.hitRadius = 0.35f; w.hitSplatChance = 1f; w.missSplatChance = 1f; w.splatSize = 0.9f; w.heavy = true;
                w.fireSound = SoundId.SniperShot; w.impactShake = 0.06f;
            });

            d.playerSoldier = Asset<UnitConfig>("Unit_PlayerSoldier", reset, u => { u.maxHealth = 45f; u.maxSpeed = 4.6f; });
            d.enemySoldier = Asset<UnitConfig>("Unit_EnemySoldier", reset, u => { u.maxHealth = 30f; u.maxSpeed = 3.2f; });
            d.flagBearer = Asset<UnitConfig>("Unit_FlagBearer", reset, u => { u.maxHealth = 70f; u.maxSpeed = 2.2f; u.targetRadius = 0.45f; });
            d.playerSoldier.weapon = d.wSoldier;
            d.enemySoldier.weapon = d.wEnemy;
            d.flagBearer.weapon = d.wEnemy;

            d.turret = Asset<StructureConfig>("Structure_Turret", reset, s => { s.maxHealth = 200f; s.targetRadius = 0.8f; s.aimHeight = 0.9f; s.aimTurnSpeed = 150f; });
            d.tank = Asset<StructureConfig>("Structure_Tank", reset, s => { s.maxHealth = 300f; s.targetRadius = 1.1f; s.aimHeight = 0.8f; s.aimTurnSpeed = 90f; s.patrolSpeed = 1.1f; });
            d.cannon = Asset<StructureConfig>("Structure_Cannon", reset, s => { s.maxHealth = 170f; s.targetRadius = 0.8f; s.aimHeight = 0.8f; s.aimTurnSpeed = 70f; s.fireCone = 10f; });
            d.bunker = Asset<StructureConfig>("Structure_Bunker", reset, s => { s.maxHealth = 380f; s.targetRadius = 1.5f; s.aimHeight = 0.9f; });
            d.mine = Asset<StructureConfig>("Structure_Mine", reset, s => { s.maxHealth = 1f; s.targetRadius = 0.35f; s.triggerRadius = 1.3f; s.armDelay = 0.45f; s.explosionRadius = 2.6f; s.explosionDamage = 46f; });
            d.tesla = Asset<StructureConfig>("Structure_Tesla", reset, s => { s.maxHealth = 160f; s.targetRadius = 0.7f; s.aimHeight = 1.2f; s.displayRange = 5.5f; });
            d.sniper = Asset<StructureConfig>("Structure_Sniper", reset, s => { s.maxHealth = 140f; s.targetRadius = 0.9f; s.aimHeight = 1.6f; s.aimTurnSpeed = 110f; s.displayRange = 16f; });
            d.geyser = Asset<StructureConfig>("Structure_Geyser", reset, s => { s.maxHealth = 1f; s.targetRadius = 0.5f; s.displayRange = 2.8f; });
            d.sniper.weapon = d.wSniper;
            d.defense = Asset<DefenseBattleConfig>("DefenseBattleConfig", reset, null);
            d.turret.weapon = d.wTurret; d.tank.weapon = d.wTank; d.cannon.weapon = d.wCannon; d.bunker.weapon = d.wBunker;

            d.enemySquad = Asset<EnemySquadConfig>("EnemySquadConfig", reset, null);
            d.enemySquad.soldier = d.enemySoldier;
            d.enemySquad.flagBearer = d.flagBearer;
            d.battle = Asset<BattleConfig>("BattleConfig", reset, null);
            d.combat = Asset<CombatConfig>("CombatConfig", reset, null);
            d.camera = Asset<CameraConfig>("CameraConfig", reset, null);
            d.prefabs = Asset<BattlePrefabs>("BattlePrefabs", true, null);
            d.sounds = Asset<SoundLibrary>("SoundLibrary", reset, null);
            d.game = Asset<GameConfig>("GameConfig", reset, g => { g.playerTeam = TeamColorId.Red; g.enemyTeam = TeamColorId.Teal; });
            d.game.palette = d.palette;
            d.game.visuals = d.visuals;
            return d;
        }

        static void WireData(Data d, Prefabs p, Mats mats, Meshes meshes)
        {
            d.visuals.teams = mats.teams.Values.ToArray();
            d.visuals.flash = mats.flash;
            d.visuals.blobShadow = mats.blobShadow;
            d.visuals.neutralArc = mats.neutralArc;
            d.prefabs.soldier = p.soldier;
            d.prefabs.hero = p.hero;
            d.prefabs.flagBearer = p.flagBearer;
            d.prefabs.bubble = p.bubble;
            foreach (var o in new Object[] { d.visuals, d.prefabs, d.game }) EditorUtility.SetDirty(o);
            LastPrefabs = p;
            LastData = d;
            LastMeshes = meshes;
            LastMats = mats;
        }

        // Shared with SceneBuilder in the same domain.
        public static Prefabs LastPrefabs;
        public static Data LastData;
        public static Meshes LastMeshes;
        public static Mats LastMats;

        static void BuildSoundLibrary(Data d)
        {
            var lib = d.sounds;
            var entries = new List<SoundEntry>();
            // Phase 8 mix (ESTIMATE, nobody could listen): every SFX file is peak normalized, so per sound volumes are
            // relative within a category and SoundLibrary category multipliers set the overall balance. Shots are quiet
            // with tight voice limits (40 guns), UI moderate, explosions and jingles loud, music under the effects.
            void Add(SoundId id, string pattern, SoundCategory cat, float vol, int voices, float interval, float p0 = 0.92f, float p1 = 1.08f)
            {
                var clips = new List<AudioClip>();
                foreach (var dir in new[] { "Assets/Audio/SFX", "Assets/Audio/Music" })
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir).Where(f => f.EndsWith(".wav") || f.EndsWith(".ogg")).OrderBy(f => f))
                    {
                        string n = Path.GetFileNameWithoutExtension(f);
                        if (n == pattern || (n.StartsWith(pattern + "_") && char.IsDigit(n[n.Length - 1])))
                        {
                            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(f.Replace('\\', '/'));
                            if (c != null) clips.Add(c);
                        }
                    }
                }
                if (clips.Count == 0) Debug.LogWarning("[AssetBuilder] No audio for " + id + " (" + pattern + "), it will be silent");
                entries.Add(new SoundEntry { id = id, category = cat, clips = clips.ToArray(), volume = vol, maxVoices = voices, minInterval = interval, pitch = new Vector2(p0, p1) });
            }
            const SoundCategory Sh = SoundCategory.Shots, Im = SoundCategory.Impacts, Ex = SoundCategory.Explosions,
                Ji = SoundCategory.Jingles, Ui = SoundCategory.UI, Fb = SoundCategory.Feedback, Am = SoundCategory.Ambience;
            Add(SoundId.Shoot, "paint_shot", Sh, 0.2f, 3, 0.05f, 0.9f, 1.12f);
            Add(SoundId.ShootHeavy, "turret_shot", Sh, 0.55f, 2, 0.1f);
            Add(SoundId.TurretShot, "turret_shot", Sh, 0.55f, 2, 0.1f);
            Add(SoundId.CannonShot, "cannon_shot", Sh, 0.75f, 2, 0.2f);
            Add(SoundId.TankShot, "tank_shot", Sh, 0.7f, 2, 0.2f);
            Add(SoundId.SniperShot, "sniper_shot", Sh, 0.75f, 2, 0.15f);
            Add(SoundId.Hit, "paint_splat", Im, 0.18f, 3, 0.06f);
            Add(SoundId.Splat, "paint_splat", Im, 0.25f, 3, 0.06f);
            Add(SoundId.UnitPop, "unit_pop", Im, 0.45f, 3, 0.05f);
            Add(SoundId.BuildingHit, "building_hit", Im, 0.35f, 2, 0.12f);
            Add(SoundId.Explosion, "mine_explosion", Ex, 0.7f, 2, 0.12f);
            Add(SoundId.MineExplosion, "mine_explosion", Ex, 0.8f, 2, 0.1f);
            Add(SoundId.BuildingDestroyed, "building_destroyed", Ex, 0.85f, 2, 0.2f, 1f, 1f);
            Add(SoundId.GeyserErupt, "geyser_erupt", Ex, 0.75f, 2, 0.15f);
            Add(SoundId.TeslaZap, "tesla_zap", Ex, 0.55f, 2, 0.1f);
            Add(SoundId.Victory, "victory", Ji, 0.9f, 1, 1f, 1f, 1f);
            Add(SoundId.Defeat, "defeat", Ji, 0.9f, 1, 1f, 1f, 1f);
            Add(SoundId.LevelUp, "level_up", Ji, 0.85f, 1, 0.5f, 1f, 1f);
            Add(SoundId.CaptureComplete, "capture_complete", Ji, 0.8f, 1, 0.5f, 1f, 1f);
            Add(SoundId.ReinforcementCall, "reinforcement_call", Ji, 0.75f, 1, 0.5f, 1f, 1f);
            Add(SoundId.ReinforcementReady, "reinforcement_ready", Fb, 0.6f, 1, 0.5f, 1f, 1f);
            Add(SoundId.FormationSet, "formation_set", Fb, 0.45f, 1, 0.08f, 0.98f, 1.02f);
            Add(SoundId.CaptureTick, "capture_tick", Fb, 0.35f, 2, 0.1f, 1f, 1f);
            Add(SoundId.BubblePop, "bubble_pop", Fb, 0.6f, 2, 0.1f);
            Add(SoundId.TeslaCharge, "tesla_charge", Fb, 0.35f, 2, 0.3f);
            Add(SoundId.SniperAim, "sniper_aim", Fb, 0.3f, 1, 0.3f, 1f, 1f);
            Add(SoundId.GeyserRumble, "geyser_rumble", Fb, 0.45f, 2, 0.2f);
            Add(SoundId.StrokeDraw, "stroke_draw_loop", Am, 0.3f, 1, 0f, 1f, 1f);
            Add(SoundId.Footsteps, "footsteps_loop", Am, 0.25f, 1, 0f, 1f, 1f);
            Add(SoundId.UiClick, "ui_click", Ui, 0.55f, 2, 0.05f, 1f, 1f);
            Add(SoundId.UiBack, "ui_back", Ui, 0.55f, 2, 0.05f, 1f, 1f);
            Add(SoundId.UiOpen, "ui_open", Ui, 0.5f, 2, 0.05f, 1f, 1f);
            Add(SoundId.UiPurchase, "ui_purchase", Ui, 0.65f, 2, 0.05f, 1f, 1f);
            Add(SoundId.UiError, "ui_error", Ui, 0.55f, 1, 0.1f, 1f, 1f);
            Add(SoundId.CoinPickup, "coin_pickup", Ui, 0.6f, 3, 0.05f, 0.95f, 1.1f);
            lib.entries = entries.ToArray();
            lib.menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/menu_theme.ogg");
            lib.battleMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/battle_theme.ogg");
            if (lib.battleMusic == null) Debug.LogWarning("[AssetBuilder] battle_theme.ogg missing, no music");
            EditorUtility.SetDirty(lib);
        }
    }
}
