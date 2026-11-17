using TactileWars.Audio;
using TactileWars.Combat;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Sniper tower (name verified, behaviour an ESTIMATE): long range single shots. The Head yaws toward
    /// the target and a thin aiming line in the team color grows for 'sniperAimTime' seconds, then one fast heavy
    /// paint shot fires (pooled through ProjectileManager). Prefers flag bearers when one is in range.</summary>
    public class Sniper : StructureBase
    {
        public Transform head;
        public LineRenderer aimLine;

        WeaponConfig shot;
        Health target;
        float aim;
        float cooldown;
        float rescan;
        bool aimSound;

        public int ShotsFired { get; private set; }
        public Health Target => target;
        public bool Aiming => target != null && aim > 0f;

        protected override void Start()
        {
            base.Start();
            // A runtime weapon so the shot uses this config's damage and speed (and the level multiplier).
            var src = config != null ? config.weapon : null;
            shot = src != null ? Instantiate(src) : ScriptableObject.CreateInstance<WeaponConfig>();
            shot.name = "SniperShot_Runtime";
            if (config != null)
            {
                shot.range = config.sniperRange;
                shot.damage = config.sniperDamage;
                shot.projectileSpeed = config.sniperShotSpeed;
            }
            shot.spreadDegrees = Mathf.Min(shot.spreadDegrees, 1f);
            shot.splashRadius = 0f;
            shot.burstCount = 1;
            cooldown = Random.Range(0.3f, 1f);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.red;
            if (aimLine != null)
            {
                aimLine.positionCount = 2;
                aimLine.startColor = new Color(c.r, c.g, c.b, 0.9f);
                aimLine.endColor = new Color(c.r, c.g, c.b, 0.35f);
                aimLine.enabled = false;
            }
        }

        void OnDestroy() { if (shot != null) Destroy(shot); }

        float Range => config != null ? config.sniperRange : 16f;

        protected override void TickBehaviour(float dt)
        {
            if (cooldown > 0f) cooldown -= dt;
            if (target != null && (!target.IsAlive || !InRange(target)))
            {
                target = null;
                aim = 0f;
            }
            rescan -= dt;
            if (target == null && cooldown <= 0f && rescan <= 0f)
            {
                rescan = 0.25f;
                target = PickTarget();
                aim = 0f;
                aimSound = false;
            }

            if (target == null)
            {
                if (aimLine != null && aimLine.enabled) aimLine.enabled = false;
                if (head != null) head.Rotate(Vector3.up, 8f * dt, Space.World);
                return;
            }

            float remaining = AimYaw(head != null ? head : modelRoot, target.transform.position, config != null ? config.aimTurnSpeed : 120f, dt);
            if (remaining < 25f) aim += dt;
            if (!aimSound && aim > 0f) { aimSound = true; AudioManager.Play(SoundId.SniperAim, transform.position); }
            float aimTime = config != null ? config.sniperAimTime : 1.3f;
            float k = Mathf.Clamp01(aim / aimTime);
            if (aimLine != null)
            {
                aimLine.enabled = aim > 0f;
                aimLine.SetPosition(0, MuzzlePos);
                aimLine.SetPosition(1, target.AimPoint);
                aimLine.widthMultiplier = Mathf.Lerp(0.03f, 0.1f, k);
            }
            if (k >= 1f) Fire();
        }

        Health PickTarget()
        {
            // Flag bearers first (a sniper picks off leaders), otherwise the nearest unit in range.
            var foes = UnitRegistry.Of(Foe);
            Health best = null;
            float bestD = float.MaxValue;
            float r = Range;
            Vector3 pos = transform.position;
            for (int i = 0; i < foes.Count; i++)
            {
                var h = foes[i];
                if (!h.IsAlive || h.kind == TargetKind.Mine) continue;
                Vector3 p = h.transform.position;
                float dx = p.x - pos.x, dz = p.z - pos.z;
                float d2 = dx * dx + dz * dz;
                float rr = r + h.targetRadius;
                if (d2 > rr * rr) continue;
                if (h.kind == TargetKind.FlagBearer) d2 *= 0.25f;
                if (d2 < bestD) { bestD = d2; best = h; }
            }
            return best;
        }

        bool InRange(Health h)
        {
            Vector3 p = h.transform.position, pos = transform.position;
            float dx = p.x - pos.x, dz = p.z - pos.z;
            float r = Range * 1.1f + h.targetRadius;
            return dx * dx + dz * dz <= r * r;
        }

        void Fire()
        {
            var pm = ProjectileManager.Instance;
            if (pm != null && target != null) pm.Fire(MuzzlePos, target, shot, team, color, levelMultiplier);
            ShotsFired++;
            AudioManager.Play(SoundId.SniperShot, transform.position);
            var vfx = VfxManager.Instance;
            if (vfx != null) vfx.Play(VfxType.Muzzle, MuzzlePos, Color.white, 2.2f);
            if (aimLine != null) aimLine.enabled = false;
            target = null;
            aim = 0f;
            cooldown = config != null ? config.sniperCooldown : 2.4f;
        }

        protected override void OnDied(Health h, Core.TeamColorId by)
        {
            if (aimLine != null) aimLine.enabled = false;
            base.OnDied(h, by);
        }
    }
}
