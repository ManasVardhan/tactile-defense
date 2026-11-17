using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Shared defense behaviour: health, team tint, targeting, weapon, hit feedback and destruction.</summary>
    [RequireComponent(typeof(Health))]
    public abstract class StructureBase : MonoBehaviour
    {
        public StructureConfig config;
        public Team team = Team.Enemy;
        public Transform modelRoot;
        public Transform muzzle;
        public TeamTint tint;
        public HealthArc healthArc;
        public Renderer[] bodyRenderers;

        protected Health health;
        protected Targeting targeting;
        protected Weapon weapon;
        protected TeamColorId color;
        DamageFeedback feedback;
        protected bool destroyed;
        public bool Destroyed => destroyed;
        public Health Health => health;

        protected virtual bool Targetable => true;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
        }

        protected virtual void Start()
        {
            var gc = BattleContext.Config;
            color = gc != null ? (team == Team.Player ? gc.playerTeam : gc.enemyTeam) : TeamColorId.Teal;
            health.targetable = Targetable;
            health.kind = this is Mine ? TargetKind.Mine : TargetKind.Structure;
            if (config != null)
            {
                health.targetRadius = config.targetRadius;
                health.aimHeight = config.aimHeight;
            }
            health.Init(team, color, config != null ? config.maxHealth : 100f);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            targeting = new Targeting(0.2f);
            weapon = new Weapon(config != null ? config.weapon : null);
            var vis = BattleContext.Visuals;
            if (tint != null && vis != null) tint.Apply(vis.Get(color));
            if (healthArc != null && vis != null) { var m = vis.Get(color); if (m != null) healthArc.SetMaterial(m.arc); healthArc.SetFill(1f); }
            if (bodyRenderers == null || bodyRenderers.Length == 0) bodyRenderers = GetComponentsInChildren<MeshRenderer>();
            feedback = new DamageFeedback(bodyRenderers, vis != null ? vis.flash : null, modelRoot);
        }

        protected virtual void Update()
        {
            if (destroyed) return;
            float dt = Time.deltaTime;
            feedback?.Tick(dt, 12f);
            TickBehaviour(dt);
        }

        protected abstract void TickBehaviour(float dt);

        protected Vector3 MuzzlePos => muzzle != null ? muzzle.position : transform.position + Vector3.up * (config != null ? config.aimHeight : 0.8f);

        /// <summary>Rotates 'part' around world Y toward the target; returns the remaining angle in degrees.</summary>
        protected float AimYaw(Transform part, Vector3 target, float speed, float dt)
        {
            if (part == null) return 0f;
            Vector3 to = target - part.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return 0f;
            // Use the real barrel direction (pivot to muzzle) so aiming works whatever the authored axis.
            Vector3 fwd = muzzle != null && (muzzle.position - part.position).sqrMagnitude > 1e-4f ? muzzle.position - part.position : part.forward;
            fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            float step = Mathf.Clamp(angle, -speed * dt, speed * dt);
            part.Rotate(Vector3.up, step, Space.World);
            return Mathf.Abs(angle - step);
        }

        void OnDamaged(Health h, float dmg, Vector3 from, TeamColorId by)
        {
            feedback?.Trigger(0.06f, 0.06f);
            if (healthArc != null) healthArc.SetFill(h.Fraction);
            AudioManager.Play(SoundId.BuildingHit, transform.position);
        }

        protected virtual void OnDied(Health h, TeamColorId by)
        {
            destroyed = true;
            Vector3 p = transform.position;
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(by).paint : Color.white;
            if (VfxManager.Instance != null)
            {
                VfxManager.Instance.Play(VfxType.Explosion, p + Vector3.up * 0.3f, c, 1.4f);
                VfxManager.Instance.Play(VfxType.Puff, p + Vector3.up * 0.8f, Color.white, 2.2f);
            }
            if (PaintSplatManager.Instance != null)
            {
                var combat = BattleContext.Combat;
                PaintSplatManager.Instance.Spawn(p, by, combat != null ? combat.explosionSplatSize : 3f, true);
            }
            BattleCamera.Shake(0.45f);
            AudioManager.Play(SoundId.BuildingDestroyed, p);
            BattleEvents.RaiseUnitKilled(team, health.kind, p);
            if (healthArc != null) healthArc.gameObject.SetActive(false);
            // Collapse: squash the model flat and leave the rubble as a low silhouette.
            if (modelRoot != null) StartCoroutine(Collapse());
        }

        System.Collections.IEnumerator Collapse()
        {
            float t = 0f;
            Vector3 s0 = modelRoot.localScale;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = t / 0.35f;
                modelRoot.localScale = new Vector3(s0.x * (1f + 0.15f * k), s0.y * Mathf.Lerp(1f, 0.18f, k * k), s0.z * (1f + 0.15f * k));
                yield return null;
            }
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        }
    }
}
