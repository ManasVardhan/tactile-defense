using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Cooldown and burst logic. Spawns pooled paint projectiles through ProjectileManager.</summary>
    public class Weapon
    {
        public WeaponConfig config;
        public float damageMultiplier = 1f;
        float cooldown;
        int burstLeft;
        float burstTimer;
        Health burstTarget;

        public System.Action Fired;

        public Weapon(WeaponConfig cfg)
        {
            config = cfg;
            cooldown = cfg != null ? Random.Range(0f, cfg.fireInterval) : 1f;
        }

        public bool Ready => cooldown <= 0f;

        /// <summary>Returns true on the frame a shot is fired.</summary>
        public bool Tick(float dt, Health target, Vector3 muzzle, Team team, TeamColorId color)
        {
            if (config == null) return false;
            cooldown -= dt;
            bool fired = false;
            if (burstLeft > 0)
            {
                burstTimer -= dt;
                if (burstTimer <= 0f && burstTarget != null && burstTarget.IsAlive)
                {
                    Shoot(burstTarget, muzzle, team, color);
                    burstLeft--;
                    burstTimer = config.burstGap;
                    fired = true;
                }
                else if (burstTarget == null || !burstTarget.IsAlive) burstLeft = 0;
            }
            if (target == null || cooldown > 0f) return fired;
            cooldown = config.fireInterval + Random.Range(0f, config.intervalJitter);
            Shoot(target, muzzle, team, color);
            burstLeft = config.burstCount - 1;
            burstTimer = config.burstGap;
            burstTarget = target;
            return true;
        }

        void Shoot(Health target, Vector3 muzzle, Team team, TeamColorId color)
        {
            var pm = ProjectileManager.Instance;
            if (pm == null) return;
            pm.Fire(muzzle, target, config, team, color, damageMultiplier);
            Fired?.Invoke();
        }
    }
}
