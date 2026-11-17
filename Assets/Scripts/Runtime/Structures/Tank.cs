using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Slow patrol between two points; turret aims independently and fires cannon shots.</summary>
    public class Tank : StructureBase
    {
        public Transform hull;
        public Transform turret;
        public Vector3 patrolA;
        public Vector3 patrolB;
        bool toB = true;
        float pause;

        protected override void TickBehaviour(float dt)
        {
            // Patrol.
            Vector3 goal = toB ? patrolB : patrolA;
            Vector3 pos = transform.position;
            Vector3 to = goal - pos; to.y = 0f;
            if (pause > 0f) pause -= dt;
            else if (to.magnitude < 0.2f) { toB = !toB; pause = config != null ? config.patrolPause : 1f; }
            else
            {
                Vector3 dir = to.normalized;
                Quaternion want = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, (config != null ? config.hullTurnSpeed : 60f) * dt);
                float align = Vector3.Dot(transform.forward, dir);
                if (align > 0.8f) transform.position = pos + transform.forward * (config != null ? config.patrolSpeed : 1f) * dt * align;
            }

            var w = config != null ? config.weapon : null;
            if (w == null) return;
            var target = targeting.Tick(team, transform.position, w.range);
            float remaining = 999f;
            if (target != null) remaining = AimYaw(turret, target.transform.position, config.aimTurnSpeed, dt);
            weapon.Tick(dt, target != null && remaining <= config.fireCone ? target : null, MuzzlePos, team, color);
        }
    }
}
