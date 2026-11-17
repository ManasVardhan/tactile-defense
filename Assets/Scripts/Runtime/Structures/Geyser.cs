using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Combat;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Geyser trap (name verified, "geysers explode": RESEARCH_MECHANICS section 4; everything else is an
    /// ESTIMATE). Hidden in idle: only a cracked rock ring shows. An attacker in range wakes it: the paint vent
    /// appears and bulges (rumble warning), then it erupts paint in an area. It has a few charges with a cooldown,
    /// optionally a timer, and is spent (consumed) after the last eruption. Not targetable, like mines.</summary>
    public class Geyser : StructureBase
    {
        public Transform vent;

        float warn = -1f;
        float cooldown;
        float timer;
        float scan;
        int chargesLeft;
        Vector3 ventScale = Vector3.one;

        public int Eruptions { get; private set; }
        public bool Spent => destroyed;
        public bool Warning => warn >= 0f;

        protected override bool Targetable => false;
        protected override TactileWars.Combat.TargetKind Kind => TargetKind.Mine;

        protected override void Start()
        {
            base.Start();
            chargesLeft = config != null ? Mathf.Max(1, config.geyserCharges) : 2;
            if (vent != null)
            {
                ventScale = vent.localScale;
                vent.gameObject.SetActive(false);
            }
        }

        protected override void TickBehaviour(float dt)
        {
            var cfg = config;
            if (warn >= 0f)
            {
                warn -= dt;
                if (vent != null)
                {
                    float k = 1f + Mathf.Abs(Mathf.Sin(warn * 22f)) * 0.35f;
                    vent.localScale = new Vector3(ventScale.x * k, ventScale.y * (1f + (k - 1f) * 3f), ventScale.z * k);
                }
                if (warn < 0f) Erupt();
                return;
            }
            if (cooldown > 0f) { cooldown -= dt; return; }

            float interval = cfg != null ? cfg.geyserTimerInterval : 0f;
            if (interval > 0f)
            {
                timer += dt;
                if (timer >= interval && UnitRegistry.CountWithin(Foe, transform.position, 6f) > 0) { timer = 0f; Arm(); return; }
            }
            scan -= dt;
            if (scan > 0f) return;
            scan = 0.1f;
            float r = cfg != null ? cfg.geyserTriggerRadius : 1.8f;
            if (UnitRegistry.CountWithin(Foe, transform.position, r) > 0) Arm();
        }

        /// <summary>Starts the rumble warning (also a test hook).</summary>
        public void Arm()
        {
            if (destroyed || warn >= 0f) return;
            warn = config != null ? config.geyserWarnTime : 0.9f;
            if (vent != null) vent.gameObject.SetActive(true);
            AudioManager.Play(SoundId.GeyserRumble, transform.position);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.white;
            if (VfxManager.Instance != null) VfxManager.Instance.Play(VfxType.Hit, transform.position + Vector3.up * 0.1f, c, 1.6f);
        }

        void Erupt()
        {
            var cfg = config;
            Vector3 p = transform.position;
            float radius = cfg != null ? cfg.geyserRadius : 2.8f;
            UnitRegistry.DamageArea(Foe, p, radius, (cfg != null ? cfg.geyserDamage : 34f) * levelMultiplier, color);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.white;
            var vfx = VfxManager.Instance;
            if (vfx != null)
            {
                vfx.Play(VfxType.Geyser, p + Vector3.up * 0.1f, c, 1f);
                vfx.Play(VfxType.Puff, p + Vector3.up * 0.6f, Color.white, 2.0f);
            }
            if (PaintSplatManager.Instance != null) PaintSplatManager.Instance.Spawn(p, color, radius * 1.2f, true);
            BattleCamera.Shake(0.4f);
            AudioManager.Play(SoundId.GeyserErupt, p);
            Eruptions++;
            chargesLeft--;
            if (vent != null) vent.localScale = ventScale;
            if (chargesLeft <= 0)
            {
                destroyed = true;           // spent: consumed like a mine
                if (vent != null) vent.gameObject.SetActive(false);
                if (health != null && health.IsAlive) health.enabled = false;
            }
            else cooldown = cfg != null ? cfg.geyserCooldown : 4.5f;
        }
    }
}
