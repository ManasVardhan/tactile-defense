using TactileWars.Audio;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Structures
{
    /// <summary>Tesla antenna (name verified, behaviour an ESTIMATE): charges, then fires a paint lightning arc that
    /// chains from the nearest soldier in range to up to N nearby soldiers, losing some damage per jump.
    /// Two LineRenderers (team paint glow and a white core) draw the jagged arc; no allocations per zap.</summary>
    public class TeslaTower : StructureBase
    {
        public Transform arcOrigin;
        public LineRenderer arcGlow;
        public LineRenderer arcCore;

        const int MaxChain = 12;
        const int Jags = 4;
        static readonly Health[] hitBuffer = new Health[MaxChain];
        readonly Vector3[] linePts = new Vector3[MaxChain * Jags + 2];
        int lineCount;
        float charge;
        float arcTimer;
        float jitterTimer;
        bool chargeSoundPlayed;
        Vector3 baseScale = Vector3.one;

        public int LastChainHits { get; private set; }
        public int TotalZaps { get; private set; }

        protected override void Start()
        {
            base.Start();
            if (modelRoot != null) baseScale = modelRoot.localScale;
            charge = Random.Range(0f, 0.5f);
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.cyan;
            if (arcGlow != null) { arcGlow.startColor = arcGlow.endColor = new Color(c.r, c.g, c.b, 0.9f); arcGlow.enabled = false; }
            if (arcCore != null) { arcCore.startColor = arcCore.endColor = Color.white; arcCore.enabled = false; }
        }

        Vector3 Origin => arcOrigin != null ? arcOrigin.position : transform.position + Vector3.up * 2.3f;

        protected override void TickBehaviour(float dt)
        {
            var cfg = config;
            float chargeTime = cfg != null ? cfg.teslaChargeTime : 2.2f;
            float range = cfg != null ? cfg.teslaRange : 5.5f;

            if (arcTimer > 0f)
            {
                arcTimer -= dt;
                jitterTimer -= dt;
                if (jitterTimer <= 0f) { jitterTimer = 0.05f; Rejag(); }
                if (arcTimer <= 0f) SetArcVisible(false);
            }

            if (charge < chargeTime) charge += dt;
            float k = Mathf.Clamp01(charge / chargeTime);
            if (modelRoot != null)
            {
                // Hum while charging, a small squash when full.
                float hum = k >= 1f ? 0.03f * Mathf.Sin(Time.time * 40f) : 0f;
                modelRoot.localScale = new Vector3(baseScale.x * (1f - hum), baseScale.y * (1f + hum), baseScale.z * (1f - hum));
            }
            if (k < 1f) return;
            var first = UnitRegistry.FindNearest(Foe, transform.position, range);
            if (first == null) return;
            if (!chargeSoundPlayed) { chargeSoundPlayed = true; AudioManager.Play(SoundId.TeslaCharge, transform.position); }
            Zap(first);
            charge = 0f;
            chargeSoundPlayed = false;
        }

        /// <summary>Fires one chained arc starting at 'first'. Returns the number of soldiers hit.</summary>
        public int Zap(Health first)
        {
            var cfg = config;
            int maxChain = Mathf.Clamp(cfg != null ? cfg.teslaChainCount : 4, 1, MaxChain);
            float chainRange = cfg != null ? cfg.teslaChainRange : 2.6f;
            float damage = (cfg != null ? cfg.teslaDamage : 26f) * levelMultiplier;
            float falloff = cfg != null ? cfg.teslaChainFalloff : 0.8f;

            int n = 0;
            hitBuffer[n++] = first;
            var foes = UnitRegistry.Of(Foe);
            while (n < maxChain)
            {
                Vector3 from = hitBuffer[n - 1].transform.position;
                Health best = null;
                float bestD = chainRange * chainRange;
                for (int i = 0; i < foes.Count; i++)
                {
                    var h = foes[i];
                    if (!h.IsAlive || h.kind == TargetKind.Mine) continue;
                    bool used = false;
                    for (int k = 0; k < n; k++) if (hitBuffer[k] == h) { used = true; break; }
                    if (used) continue;
                    Vector3 p = h.transform.position;
                    float dx = p.x - from.x, dz = p.z - from.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestD) { bestD = d2; best = h; }
                }
                if (best == null) break;
                hitBuffer[n++] = best;
            }

            // Record the arc path before damage (targets may die and deactivate).
            lineCount = 0;
            linePts[lineCount++] = Origin;
            for (int i = 0; i < n; i++) linePts[lineCount++] = hitBuffer[i].AimPoint;
            Color c = BattleColors.Palette != null ? BattleColors.Palette.Get(color).paint : Color.cyan;
            float dmg = damage;
            var vfx = VfxManager.Instance;
            for (int i = 0; i < n; i++)
            {
                var h = hitBuffer[i];
                Vector3 p = h.AimPoint;
                h.TakeDamage(dmg, transform.position, color);
                if (vfx != null) vfx.Play(VfxType.Zap, p, c, 1f);
                dmg *= falloff;
                hitBuffer[i] = null;
            }
            LastChainHits = n;
            TotalZaps++;
            arcTimer = cfg != null ? cfg.teslaArcTime : 0.28f;
            jitterTimer = 0f;
            Rejag();
            SetArcVisible(true);
            BattleCamera.Shake(0.12f);
            AudioManager.Play(SoundId.TeslaZap, transform.position);
            return n;
        }

        // Anchor points are kept in linePts[0..lineCount); the renderers get jagged points in between.
        void Rejag()
        {
            if (arcGlow == null && arcCore == null) return;
            int total = (lineCount - 1) * Jags + 1;
            if (arcGlow != null) arcGlow.positionCount = total;
            if (arcCore != null) arcCore.positionCount = total;
            int w = 0;
            for (int s = 0; s < lineCount - 1; s++)
            {
                Vector3 a = linePts[s], b = linePts[s + 1];
                Vector3 side = Vector3.Cross(b - a, Vector3.up);
                float len = side.magnitude;
                side = len > 1e-4f ? side / len : Vector3.right;
                for (int j = 0; j < Jags; j++)
                {
                    float t = j / (float)Jags;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    if (j > 0) p += side * Random.Range(-0.35f, 0.35f) + Vector3.up * Random.Range(-0.2f, 0.25f);
                    SetPoint(w++, p);
                }
            }
            SetPoint(w, linePts[lineCount - 1]);
        }

        void SetPoint(int i, Vector3 p)
        {
            if (arcGlow != null) arcGlow.SetPosition(i, p);
            if (arcCore != null) arcCore.SetPosition(i, p);
        }

        void SetArcVisible(bool on)
        {
            if (arcGlow != null) arcGlow.enabled = on;
            if (arcCore != null) arcCore.enabled = on;
        }

        protected override void OnDied(Health h, TeamColorId by)
        {
            SetArcVisible(false);
            base.OnDied(h, by);
        }

        /// <summary>Test hook: fill the charge so the next tick zaps.</summary>
        public void ForceCharged() { charge = config != null ? config.teslaChargeTime : 2.2f; }
    }
}
