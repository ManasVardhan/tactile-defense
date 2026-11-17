using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Owns every live paint ball: one Update for all, pooled instances, no allocations per shot.</summary>
    public class ProjectileManager : MonoBehaviour
    {
        public static ProjectileManager Instance { get; private set; }

        public PaintProjectile prefab;
        public TeamVisuals visuals;
        public int prewarm = 256;

        Pool<PaintProjectile> pool;
        readonly List<PaintProjectile> active = new List<PaintProjectile>(512);
        Transform camT;

        public int TotalFired { get; private set; }
        public int TotalHits { get; private set; }
        public int ActiveCount => active.Count;

        void Awake()
        {
            Instance = this;
            if (prefab != null) pool = new Pool<PaintProjectile>(prefab, transform, prewarm);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Fire(Vector3 muzzle, Health target, WeaponConfig w, Team team, TeamColorId color, float dmgMul)
        {
            if (pool == null || target == null) return;
            var p = pool.Get();
            Vector3 aim = target.AimPoint;
            if (w.arcHeight > 0f) aim.y = 0.05f;
            // Aim error: rotate around Y and vary range a little.
            Vector3 d = aim - muzzle;
            float spread = w.spreadDegrees;
            d = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * d;
            d *= 1f + Random.Range(-spread, spread) * 0.008f;
            Vector3 to = muzzle + d;
            float dist = Mathf.Max(0.1f, d.magnitude);
            p.from = muzzle;
            p.to = to;
            p.t = 0f;
            p.invDuration = w.projectileSpeed / dist;
            p.scale = w.projectileScale;
            p.damage = w.damage * dmgMul;
            p.target = target;
            p.weapon = w;
            p.team = team;
            p.color = color;
            var mats = visuals != null ? visuals.Get(color) : null;
            if (mats != null && p.meshRenderer.sharedMaterial != mats.projectile) p.meshRenderer.sharedMaterial = mats.projectile;
            p.transform.position = muzzle;
            p.transform.localScale = Vector3.one * (p.scale * 0.6f);
            active.Add(p);
            TotalFired++;
            AudioManager.Play(w.fireSound, muzzle);
        }

        void LateUpdate()
        {
            if (camT == null) { var c = Camera.main; if (c != null) camT = c.transform; }
            Quaternion bill = camT != null ? camT.rotation : Quaternion.identity;
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.t += dt * p.invDuration;
                if (p.t >= 1f)
                {
                    Impact(p);
                    int last = active.Count - 1;
                    active[i] = active[last];
                    active.RemoveAt(last);
                    pool.Release(p);
                    continue;
                }
                Vector3 pos = Vector3.LerpUnclamped(p.from, p.to, p.t);
                float arc = p.weapon.arcHeight;
                if (arc > 0f) pos.y += arc * 4f * p.t * (1f - p.t);
                p.transform.SetPositionAndRotation(pos, bill);
                float grow = Mathf.Min(1f, 0.6f + p.t * 3f);
                p.transform.localScale = new Vector3(p.scale * grow, p.scale * grow, p.scale * grow);
            }
        }

        void Impact(PaintProjectile p)
        {
            var w = p.weapon;
            Vector3 ground = p.to; ground.y = 0f;
            var splats = PaintSplatManager.Instance;
            var vfx = VfxManager.Instance;
            Color col = visuals != null && BattleColors.Palette != null ? BattleColors.Palette.Get(p.color).paint : Color.white;

            if (w.splashRadius > 0f)
            {
                int hits = UnitRegistry.DamageArea(UnitRegistry.Opponent(p.team), ground, w.splashRadius, p.damage, p.color);
                if (hits > 0) TotalHits += hits;
                if (splats != null) splats.Spawn(ground, p.color, w.splashRadius * 1.1f * w.splatSize, true);
                if (vfx != null) vfx.Play(VfxType.Explosion, ground, col, Mathf.Max(0.6f, w.splashRadius * 0.6f));
                if (w.impactShake > 0f) BattleCamera.Shake(w.impactShake);
                AudioManager.Play(SoundId.Explosion, ground);
                return;
            }

            var t = p.target;
            bool hit = false;
            if (t != null && t.IsAlive)
            {
                Vector3 tp = t.transform.position;
                float dx = tp.x - p.to.x, dz = tp.z - p.to.z;
                float r = t.targetRadius + w.hitRadius;
                if (dx * dx + dz * dz <= r * r)
                {
                    hit = true;
                    TotalHits++;
                    t.TakeDamage(p.damage, p.from, p.color);
                    if (vfx != null) vfx.Play(VfxType.Hit, p.to, col, 1f);
                    if (splats != null && Random.value < w.hitSplatChance)
                    {
                        Vector3 sp = tp + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                        sp.y = 0f;
                        splats.Spawn(sp, p.color, w.splatSize * Random.Range(0.8f, 1.1f), false);
                    }
                    AudioManager.Play(SoundId.Hit, p.to);
                }
            }
            if (!hit)
            {
                if (splats != null && Random.value < w.missSplatChance) splats.Spawn(ground, p.color, w.splatSize * Random.Range(0.55f, 0.9f), false);
                if (vfx != null) vfx.Play(VfxType.Hit, ground, col, 0.6f);
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < active.Count; i++) pool.Release(active[i]);
            active.Clear();
        }
    }

    /// <summary>Static access to the active palette for presentation code.</summary>
    public static class BattleColors
    {
        public static TeamPalette Palette;
        public static TeamVisuals Visuals;
    }
}
