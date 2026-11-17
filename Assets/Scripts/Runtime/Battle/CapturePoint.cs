using TactileWars.Audio;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Circular capture pad. Player soldiers standing on it fill the ring over a few seconds; enemies on
    /// the pad contest it (no progress). Locked until the previous point is captured.</summary>
    public class CapturePoint : MonoBehaviour
    {
        public int index;
        public Renderer progressRing;
        public TeamTint flagTint;
        public Transform flag;

        public bool Active { get; private set; }
        public bool Captured { get; private set; }
        public float Progress { get; private set; }
        public bool Contested { get; private set; }
        public int PlayersInside { get; private set; }

        BattleConfig cfg;
        float scan;
        float tickSound;
        MaterialPropertyBlock mpb;
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        float flagRaise;

        public void Init(BattleConfig c, int i)
        {
            cfg = c; index = i;
            mpb = new MaterialPropertyBlock();
            var vis = BattleContext.Visuals;
            var gc = BattleContext.Config;
            if (progressRing != null && vis != null && gc != null)
            {
                var m = vis.Get(gc.playerTeam);
                if (m != null && m.arc != null) progressRing.sharedMaterial = m.arc;
            }
            float r = c != null ? c.captureRadius : 2.9f;
            if (progressRing != null) progressRing.transform.localScale = new Vector3(r * 2.2f, r * 2.2f, 1f);
            UpdateVisual(0f);
        }

        public void SetActive(bool a) { Active = a; }

        void Update()
        {
            if (cfg == null) return;
            float dt = Time.deltaTime;
            if (Captured)
            {
                flagRaise = Mathf.MoveTowards(flagRaise, 1f, dt * 2f);
                if (flag != null) flag.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, Mathf.Sin(flagRaise * Mathf.PI));
                UpdateVisual(dt);
                return;
            }
            scan -= dt;
            if (scan <= 0f)
            {
                scan = 0.1f;
                PlayersInside = UnitRegistry.CountWithin(Team.Player, transform.position, cfg.captureRadius);
                int enemies = UnitRegistry.CountWithin(Team.Enemy, transform.position, cfg.captureRadius);
                Contested = PlayersInside > 0 && enemies > 0;
            }
            if (Active)
            {
                float before = Progress;
                if (PlayersInside > 0 && !Contested)
                {
                    float bonus = 1f + Mathf.Min(PlayersInside, cfg.maxSoldierBonus) * cfg.perSoldierBonus;
                    Progress = Mathf.Min(1f, Progress + dt / Mathf.Max(0.1f, cfg.captureTime) * bonus);
                    tickSound -= dt;
                    if (tickSound <= 0f) { tickSound = 0.5f; AudioManager.Play(SoundId.CaptureTick, transform.position); }
                }
                else if (PlayersInside == 0)
                {
                    Progress = Mathf.Max(0f, Progress - cfg.captureDecay * dt);
                }
                if (!Mathf.Approximately(before, Progress) || Contested)
                    BattleEvents.RaiseCaptureProgress(index, Progress, Contested);
                if (Progress >= 1f) Capture();
            }
            UpdateVisual(dt);
        }

        void Capture()
        {
            Captured = true;
            Progress = 1f;
            var vis = BattleContext.Visuals;
            var gc = BattleContext.Config;
            if (flagTint != null && vis != null && gc != null) flagTint.Apply(vis.Get(gc.playerTeam));
            Color c = BattleColors.Palette != null && gc != null ? BattleColors.Palette.Get(gc.playerTeam).paint : Color.white;
            if (VfxManager.Instance != null) VfxManager.Instance.Play(VfxType.Capture, transform.position + Vector3.up * 0.2f, c, 1.5f);
            AudioManager.Play(SoundId.CaptureComplete, transform.position);
            BattleEvents.RaisePointCaptured(index);
        }

        void UpdateVisual(float dt)
        {
            if (progressRing == null) return;
            progressRing.GetPropertyBlock(mpb);
            mpb.SetFloat(FillId, Progress);
            mpb.SetFloat(AlphaId, Active || Captured ? 1f : 0.45f);
            mpb.SetFloat(PulseId, Contested ? 1f : 0f);
            progressRing.SetPropertyBlock(mpb);
        }

        /// <summary>Test and debug hook.</summary>
        public void ForceCapture() { if (!Captured) { Progress = 1f; Capture(); } }
    }
}
