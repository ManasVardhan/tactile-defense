using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>A paint gun soldier. Gameplay (health, motor, targeting, weapon) is ticked by its owning squad;
    /// presentation (animator, flash, squash, death effects) lives in SoldierView and DamageFeedback.</summary>
    [RequireComponent(typeof(Health))]
    public class Soldier : MonoBehaviour
    {
        public Transform modelRoot;
        public Animator animator;
        public Transform muzzle;
        public Renderer[] bodyRenderers;
        public TeamTint tint;
        public Transform blobShadow;

        public Health Health { get; private set; }
        public SoldierMotor Motor { get; private set; }
        public Targeting Targeting { get; private set; }
        public Weapon Weapon { get; private set; }
        public SoldierView View { get; private set; }
        public Team Team { get; private set; }
        public TeamColorId Color { get; private set; }
        public UnitConfig Config { get; private set; }
        public bool IsAlive => Health != null && Health.IsAlive;
        public bool IsGone { get; private set; }
        public bool HoldFire { get; set; }

        DamageFeedback feedback;
        float dieTimer = -1f;
        CombatConfig combat;

        public System.Action<Soldier> Removed;

        void Awake()
        {
            Health = GetComponent<Health>();
            Motor = new SoldierMotor(transform);
        }

        public void Setup(Team team, TeamColorId color, UnitConfig cfg, TargetKind kind = TargetKind.Soldier)
        {
            Team = team; Color = color; Config = cfg;
            combat = BattleContext.Combat;
            gameObject.layer = team == Team.Player ? Layers.PlayerUnit : Layers.EnemyUnit;
            Health.kind = kind;
            Health.targetRadius = cfg.targetRadius;
            Health.aimHeight = cfg.aimHeight;
            Health.Init(team, color, cfg.maxHealth);
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            Targeting = new Targeting(combat != null ? combat.targetScanInterval : 0.25f);
            Weapon = new Weapon(cfg.weapon);
            if (tint != null && BattleContext.Visuals != null) tint.Apply(BattleContext.Visuals.Get(color));
            var flashMat = BattleContext.Visuals != null ? BattleContext.Visuals.flash : null;
            feedback = new DamageFeedback(bodyRenderers, flashMat, modelRoot);
            View = new SoldierView(animator, modelRoot);
            var fb = feedback;
            View.Squash = a => fb.Squash(a);
            View.ApplyModel(0f);
        }

        bool movedSinceRest;

        /// <summary>Formation-set hop (presentation only).</summary>
        public void Hop(float delay) { if (!IsGone && IsAlive) View?.Hop(delay); }

        public Vector3 MuzzlePosition
        {
            get
            {
                if (muzzle != null) return muzzle.position;
                return transform.position + transform.forward * 0.4f + Vector3.up * 0.42f;
            }
        }

        /// <summary>Called by the owning squad once per frame.</summary>
        public void Tick(float dt, in MotorSettings ms, Vector3[] neighbours, int count, int self)
        {
            if (IsGone) return;
            if (dieTimer >= 0f)
            {
                dieTimer -= dt;
                feedback?.Tick(dt, combat != null ? combat.squashRecover : 10f);
                if (dieTimer < 0f) Vanish();
                return;
            }

            Vector3 pos = transform.position;
            float range = Config.weapon != null ? Config.weapon.range : 0f;
            var target = HoldFire ? null : Targeting.Tick(Team, pos, range);
            Motor.HasLookTarget = target != null;
            if (target != null) Motor.LookTarget = target.transform.position;
            Motor.Tick(dt, ms, neighbours, count, self);

            if (target != null)
            {
                // Only fire when roughly facing the target, so volleys read as aimed.
                Vector3 to = target.transform.position - transform.position; to.y = 0f;
                if (Vector3.Dot(transform.forward, to.normalized) > 0.6f || Motor.SpeedNormalized > 0.5f)
                {
                    if (Weapon.Tick(dt, target, MuzzlePosition, Team, Color)) OnShot();
                }
                else Weapon.Tick(dt, null, MuzzlePosition, Team, Color);
            }
            else Weapon.Tick(dt, null, MuzzlePosition, Team, Color);

            // Landing squash when the soldier settles on its slot after a run.
            if (Motor.SpeedNormalized > 0.5f) movedSinceRest = true;
            else if (Motor.AtRest && movedSinceRest)
            {
                movedSinceRest = false;
                feedback?.Squash(combat != null ? combat.landSquash * 0.7f : 0.15f);
            }

            View.Tick(dt, Motor.SpeedNormalized);
            feedback.Tick(dt, combat != null ? combat.squashRecover : 10f);
        }

        void OnShot()
        {
            View.Shoot();
            var vfx = VfxManager.Instance;
            if (vfx != null && combat != null && Random.value < combat.muzzleFlashChance)
                vfx.Play(VfxType.Muzzle, MuzzlePosition, UnityEngine.Color.white, combat.muzzleFlashScale);
        }

        void OnDamaged(Health h, float dmg, Vector3 from, TeamColorId by)
        {
            feedback?.Trigger(combat != null ? combat.hitFlashDuration : 0.08f, combat != null ? combat.squashAmount : 0.25f);
            View?.Hit();
        }

        void OnDied(Health h, TeamColorId by)
        {
            Motor.Enabled = false;
            View?.Die();
            dieTimer = combat != null ? combat.dieAnimTime : 0.5f;
            BattleEvents.RaiseUnitKilled(Team, Health.kind, transform.position);
        }

        /// <summary>Instant removal with a pop (bubble wipe, mine overkill).</summary>
        public void Pop()
        {
            if (IsGone) return;
            if (Health.IsAlive) Health.Kill(Color);
            dieTimer = 0.05f + Random.value * 0.25f;
        }

        void Vanish()
        {
            IsGone = true;
            Vector3 p = transform.position;
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(Color).paint : UnityEngine.Color.white;
            var vfx = VfxManager.Instance;
            if (vfx != null)
            {
                vfx.Play(VfxType.Puff, p + Vector3.up * 0.4f, UnityEngine.Color.white, 1f);
                vfx.Play(VfxType.Ghost, p + Vector3.up * 0.5f, c, 1f);
            }
            if (PaintSplatManager.Instance != null && combat != null)
                PaintSplatManager.Instance.Spawn(p, Color, combat.deathSplatSize * Random.Range(0.8f, 1.1f), false);
            AudioManager.Play(SoundId.UnitPop, p);
            Removed?.Invoke(this);
            gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }
    }
}
