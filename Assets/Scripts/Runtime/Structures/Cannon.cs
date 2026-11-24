using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Long range lobbed paint shells with area damage. The Barrel yaws toward the target.</summary>
    public class Cannon : StructureBase
    {
        public Transform barrel;
        float recoil;
        Vector3 barrelBase;

        /// <summary>Lobbed shells splash an area, so the cannon does not single out Tank units.</summary>
        protected override bool PreferDrawsFire => false;

        protected override void Start()
        {
            base.Start();
            if (barrel != null) barrelBase = barrel.localPosition;
        }

        protected override void TickBehaviour(float dt)
        {
            var w = config != null ? config.weapon : null;
            if (w == null) return;
            var target = targeting.Tick(team, transform.position, w.range);
            float remaining = 999f;
            Transform aimPart = barrel != null ? barrel : modelRoot;
            if (target != null) remaining = AimYaw(aimPart, target.transform.position, config.aimTurnSpeed, dt);
            if (weapon.Tick(dt, target != null && remaining <= config.fireCone ? target : null, MuzzlePos, team, color)) recoil = 1f;
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 4f);
            if (barrel != null) barrel.localPosition = barrelBase - barrel.localRotation * Vector3.forward * (recoil * 0.12f);
        }
    }
}
