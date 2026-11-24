using System.Collections.Generic;
using TactileWars.Combat;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>
    /// The Healer's always-on support, ticked by its PlayerUnit. Each tick the unit shares healPerSecond * dt among
    /// the injured living friendly soldiers whose unit holds its own hex or one of the six around it, most injured
    /// first, never above max health. Every reviveInterval seconds that a unit on a neighboring hex (never the
    /// Healer's own) has between 1 and soldierCount - 1 living soldiers, one of its dead soldiers comes back at half
    /// health. A destroyed unit stays destroyed. A green effect marks healed and revived soldiers. No allocations per tick.
    /// </summary>
    public class HealerAbility
    {
        public const float ReviveHealth = 0.5f;
        const float EffectInterval = 0.45f;
        static readonly Color HealGreen = new Color(0.35f, 1f, 0.45f, 1f);

        readonly PlayerUnit unit;
        readonly PlayerArmy army;
        readonly List<Soldier> injured = new List<Soldier>(24);
        float reviveTimer;
        float effectTimer;
        Soldier lastHealed;

        /// <summary>Total health restored so far (tests).</summary>
        public float TotalHealed { get; private set; }
        public int Revives { get; private set; }
        /// <summary>Seconds a revive candidate has waited; the revive happens at reviveInterval.</summary>
        public float ReviveTimer => reviveTimer;

        public HealerAbility(PlayerUnit unit, PlayerArmy army)
        {
            this.unit = unit;
            this.army = army;
        }

        public void Tick(float dt)
        {
            if (unit.IsDestroyed || unit.AliveCount == 0 || army == null) return;
            var type = unit.Type;
            if (type.healPerSecond > 0f) Heal(type.healPerSecond * dt, dt);
            if (type.reviveInterval > 0f) TickRevive(type.reviveInterval, dt);
        }

        void Heal(float pool, float dt)
        {
            injured.Clear();
            var units = army.Units;
            for (int u = 0; u < units.Count; u++)
            {
                var other = units[u];
                if (other.AliveCount == 0 || HexGrid.Distance(other.Hex, unit.Hex) > 1) continue;
                var list = other.Soldiers;
                for (int k = 0; k < list.Count; k++)
                {
                    var s = list[k];
                    if (s == null || !s.IsAlive || s.Health.Current >= s.Health.max) continue;
                    // Insert sorted by health fraction, most injured first.
                    int at = injured.Count;
                    float f = s.Health.Fraction;
                    while (at > 0 && injured[at - 1].Health.Fraction > f) at--;
                    injured.Insert(at, s);
                }
            }
            for (int i = 0; i < injured.Count && pool > 1e-5f; i++)
            {
                var h = injured[i].Health;
                float before = h.Current;
                h.Heal(pool);
                float gave = h.Current - before;
                pool -= gave;
                TotalHealed += gave;
                if (gave > 0f) lastHealed = injured[i];
            }
            effectTimer -= dt;
            if (effectTimer <= 0f && lastHealed != null && lastHealed.IsAlive)
            {
                effectTimer = EffectInterval;
                Effect(lastHealed, 0.55f);
                lastHealed = null;
            }
        }

        void TickRevive(float interval, float dt)
        {
            PlayerUnit candidate = null;
            var units = army.Units;
            for (int u = 0; u < units.Count && candidate == null; u++)
            {
                var other = units[u];
                if (other == unit || other.IsDestroyed || other.IsMercenary) continue;
                if (other.AliveCount < 1 || other.AliveCount >= other.StartCount) continue;
                if (HexGrid.Distance(other.Hex, unit.Hex) != 1) continue;
                candidate = other;
            }
            if (candidate == null) { reviveTimer = 0f; return; }
            reviveTimer += dt;
            if (reviveTimer < interval) return;
            var s = candidate.ReviveOne(ReviveHealth);
            reviveTimer = 0f;
            if (s == null) return;
            Revives++;
            Effect(s, 1.1f);
        }

        static void Effect(Soldier s, float scale)
        {
            var vfx = VfxManager.Instance;
            if (vfx != null) vfx.Play(VfxType.Reinforce, s.transform.position + Vector3.up * 0.6f, HealGreen, scale);
        }
    }
}
