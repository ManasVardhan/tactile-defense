using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Pooled paint ball (visual + flight state). Simulated in bulk by ProjectileManager.</summary>
    public class PaintProjectile : MonoBehaviour
    {
        public MeshRenderer meshRenderer;
        [System.NonSerialized] public Vector3 from, to;
        [System.NonSerialized] public float t, invDuration, scale, damage;
        [System.NonSerialized] public Health target;
        [System.NonSerialized] public WeaponConfig weapon;
        [System.NonSerialized] public Team team;
        [System.NonSerialized] public TeamColorId color;
    }
}
