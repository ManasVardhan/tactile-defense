using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>The defender's optional support ability in defense battles (our design, ESTIMATE): a few paint
    /// strikes. Arm it (HUD button), then tap the ground; after a short fall the strike splashes attackers in an
    /// area. Gameplay only: the HUD polls Charges and Armed and listens to BattleEvents.StrikeLanded.</summary>
    public class DefenderStrike : MonoBehaviour
    {
        public DefenseBattleConfig config;
        public Camera cam;
        public int charges = 3;

        public bool Armed { get; private set; }
        public int Charges => charges;
        public float CooldownLeft { get; private set; }
        public bool Ready => charges > 0 && CooldownLeft <= 0f && !(BattleManager.Instance != null && BattleManager.Instance.Over);
        public int Landed { get; private set; }

        float fall = -1f;
        Vector3 target;
        bool pressed;

        public bool Arm()
        {
            if (!Ready) return false;
            Armed = true;
            return true;
        }

        public void Disarm() { Armed = false; pressed = false; }

        /// <summary>Drops a strike at 'world' (used by the tap handler and tests).</summary>
        public bool Drop(Vector3 world)
        {
            if (!Ready || fall >= 0f) return false;
            charges--;
            Armed = false;
            target = world; target.y = 0f;
            fall = config != null ? config.strikeDelay : 0.7f;
            CooldownLeft = config != null ? config.strikeCooldown : 4f;
            var gc = BattleContext.Config;
            Color c = BattleColors.Palette != null && gc != null ? BattleColors.Palette.Get(gc.enemyTeam).paint : Color.white;
            if (VfxManager.Instance != null) VfxManager.Instance.Play(VfxType.Reinforce, target + Vector3.up * 0.2f, c, 2f);
            AudioManager.Play(SoundId.CannonShot, target);
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (CooldownLeft > 0f) CooldownLeft -= dt;
            if (fall >= 0f)
            {
                fall -= dt;
                if (fall < 0f) Land();
            }
            if (!Armed) return;
            if (cam == null) cam = Camera.main;
            if (PointerInput.PressedThisFrame) pressed = !PointerInput.IsOverUI(PointerInput.Position);
            if (pressed && PointerInput.ReleasedThisFrame)
            {
                pressed = false;
                if (PointerInput.ScreenToGround(cam, PointerInput.Position, out Vector3 w)) Drop(w);
            }
        }

        void Land()
        {
            var gc = BattleContext.Config;
            var color = gc != null ? gc.enemyTeam : TeamColorId.Teal;
            float r = config != null ? config.strikeRadius : 3f;
            UnitRegistry.DamageArea(Team.Player, target, r, config != null ? config.strikeDamage : 45f, color);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.white;
            if (VfxManager.Instance != null)
            {
                VfxManager.Instance.Play(VfxType.Explosion, target + Vector3.up * 0.2f, c, 2f);
                VfxManager.Instance.Play(VfxType.Puff, target + Vector3.up * 0.6f, Color.white, 2.6f);
            }
            if (PaintSplatManager.Instance != null) PaintSplatManager.Instance.Spawn(target, color, r * 1.3f, true);
            BattleCamera.Shake(0.5f);
            AudioManager.Play(SoundId.MineExplosion, target);
            Landed++;
            BattleEvents.RaiseStrikeLanded(target);
        }
    }
}
