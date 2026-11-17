using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Garrisoned stone bunker: tough, no turning parts, rapid paint fire through its slit in any direction.</summary>
    public class Bunker : StructureBase
    {
        protected override void TickBehaviour(float dt)
        {
            var w = config != null ? config.weapon : null;
            if (w == null) return;
            var target = targeting.Tick(team, transform.position, w.range);
            Vector3 m = transform.position + Vector3.up * (config.aimHeight + 0.2f);
            if (target != null)
            {
                Vector3 to = target.transform.position - transform.position; to.y = 0f;
                m += to.normalized * (config.targetRadius * 0.8f);
            }
            weapon.Tick(dt, target, muzzle != null ? muzzle.position : m, team, color);
        }
    }
}
