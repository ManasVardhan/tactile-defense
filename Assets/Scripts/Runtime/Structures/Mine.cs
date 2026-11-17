using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Dark disc trap: arms when a player unit comes close, blinks, then bursts in a paint explosion with area damage.</summary>
    public class Mine : StructureBase
    {
        public Transform cap;
        float scan;
        float armTimer = -1f;
        Vector3 capBase;

        protected override bool Targetable => false;
        protected override TargetKind Kind => TargetKind.Mine;

        protected override void Start()
        {
            base.Start();
            if (cap != null) capBase = cap.localScale;
        }

        protected override void TickBehaviour(float dt)
        {
            if (armTimer >= 0f)
            {
                armTimer -= dt;
                float k = 1f + Mathf.Abs(Mathf.Sin(armTimer * 40f)) * 0.5f;
                if (cap != null) cap.localScale = capBase * k;
                else if (modelRoot != null) modelRoot.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(armTimer * 40f)) * 0.2f);
                if (armTimer < 0f) Explode();
                return;
            }
            scan -= dt;
            if (scan > 0f) return;
            scan = 0.1f;
            float r = config != null ? config.triggerRadius : 1.4f;
            if (UnitRegistry.CountWithin(Foe, transform.position, r) > 0)
            {
                armTimer = config != null ? config.armDelay : 0.4f;
                AudioManager.Play(SoundId.CaptureTick, transform.position);
            }
        }

        void Explode()
        {
            destroyed = true;
            Vector3 p = transform.position;
            float radius = config != null ? config.explosionRadius : 2.5f;
            UnitRegistry.DamageArea(Foe, p, radius, (config != null ? config.explosionDamage : 60f) * levelMultiplier, color);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.white;
            if (VfxManager.Instance != null)
            {
                VfxManager.Instance.Play(VfxType.Explosion, p + Vector3.up * 0.2f, c, 1.6f);
                VfxManager.Instance.Play(VfxType.Puff, p + Vector3.up * 0.5f, Color.white, 2.4f);
            }
            if (PaintSplatManager.Instance != null) PaintSplatManager.Instance.Spawn(p, color, radius * 1.3f, true);
            BattleCamera.Shake(0.6f);
            AudioManager.Play(SoundId.MineExplosion, p);
            gameObject.SetActive(false);
        }
    }
}
