using System;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>One pooled effect made of particle systems. Systems flagged as tintable take the play color.</summary>
    public class PooledVfx : MonoBehaviour
    {
        public VfxType type;
        public float lifetime = 1f;
        public ParticleSystem[] systems = Array.Empty<ParticleSystem>();
        public bool[] tintable = Array.Empty<bool>();
        public bool billboardToCamera;

        public void Play(Color color, float scale)
        {
            transform.localScale = new Vector3(scale, scale, scale);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                if (i < tintable.Length && tintable[i])
                {
                    var main = ps.main;
                    main.startColor = color;
                }
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }
}
