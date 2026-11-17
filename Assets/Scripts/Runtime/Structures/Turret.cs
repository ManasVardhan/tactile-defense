using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Yaws its Head toward the nearest player unit and fires heavier paint shots.</summary>
    public class Turret : StructureBase
    {
        public Transform head;

        protected override void TickBehaviour(float dt)
        {
            var w = config != null ? config.weapon : null;
            if (w == null) return;
            var target = targeting.Tick(team, transform.position, w.range);
            float remaining = 999f;
            if (target != null) remaining = AimYaw(head, target.transform.position, config.aimTurnSpeed, dt);
            else if (head != null) head.Rotate(Vector3.up, 12f * dt, Space.World); // idle scan
            weapon.Tick(dt, target != null && remaining <= config.fireCone ? target : null, MuzzlePos, team, color);
        }
    }
}
