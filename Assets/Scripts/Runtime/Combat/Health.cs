using System;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    public enum TargetKind { Soldier, FlagBearer, Structure, Mine }

    /// <summary>Hit points and team membership. Registers itself as a target while alive.</summary>
    public class Health : MonoBehaviour
    {
        public Team team;
        public TeamColorId color;
        public TargetKind kind;
        public float max = 30f;
        public float targetRadius = 0.35f;
        public float aimHeight = 0.45f;
        public bool targetable = true;
        [Tooltip("Shooters that prefer draws-fire targets pick this one over nearer targets while it is in range.")] public bool drawsFire;

        public float Current { get; private set; }
        public bool IsAlive { get; private set; }
        public float Fraction => max > 0f ? Current / max : 0f;
        internal int registryIndex = -1;

        public event Action<Health, float, Vector3, TeamColorId> Damaged;
        public event Action<Health, TeamColorId> Died;

        public Vector3 Position => transform.position;
        public Vector3 AimPoint
        {
            get { Vector3 p = transform.position; p.y += aimHeight; return p; }
        }

        public void Init(Team t, TeamColorId c, float maxHp)
        {
            team = t; color = c; max = maxHp; Current = maxHp; IsAlive = true;
            if (isActiveAndEnabled && targetable) UnitRegistry.Register(this);
        }

        void OnEnable()
        {
            if (IsAlive && targetable) UnitRegistry.Register(this);
        }

        void OnDisable()
        {
            UnitRegistry.Unregister(this);
        }

        public void TakeDamage(float dmg, Vector3 from, TeamColorId attacker)
        {
            if (!IsAlive || dmg <= 0f) return;
            Current -= dmg;
            Damaged?.Invoke(this, dmg, from, attacker);
            if (Current <= 0f) Kill(attacker);
        }

        public void Kill(TeamColorId by)
        {
            if (!IsAlive) return;
            Current = 0f;
            IsAlive = false;
            UnitRegistry.Unregister(this);
            Died?.Invoke(this, by);
        }

        /// <summary>Sets the health of a living target to a fraction of its max (a revived soldier starts at half).</summary>
        public void SetFraction(float f)
        {
            if (!IsAlive) return;
            Current = Mathf.Clamp(max * f, 0.01f, max);
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Current = Mathf.Min(max, Current + amount);
        }
    }
}
