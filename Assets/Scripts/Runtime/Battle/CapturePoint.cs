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
        /// <summary>Capture weight of the soldiers inside (a Vanguard soldier counts twice); drives the speed bonus.</summary>
        public int CaptureWeightInside { get; private set; }
        /// <summary>Attack battles: progress is blocked because enemies stand inside the circle while player units
        /// are on it. The ring flashes in the enemy color while this holds.</summary>
        public bool ShowsContested => Contested && Active && !Captured;
        /// <summary>True on the frames the contested ring shows the enemy color (it alternates with the player's).</summary>
        public bool RingShowsEnemy { get; private set; }

        /// <summary>The drawn ring's radius over the capture radius (the ring quad is radius * 2 * this across).</summary>
        public const float RingRadiusFactor = 1.1f;
        /// <summary>The one capture radius: enemies within it contest, player soldiers within it count.</summary>
        public float Radius => cfg != null ? cfg.captureRadius : 2.9f;
        /// <summary>Radius of the ring as drawn on the ground. A player unit with a living soldier inside it counts.</summary>
        public float DrawnRadius => Radius * RingRadiusFactor;

        BattleConfig cfg;
        float scan;
        float tickSound;
        MaterialPropertyBlock mpb;
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        float flagRaise;
        Material playerArc, enemyArc;
        const float ContestFlashHz = 3f;

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
                playerArc = m != null ? m.arc : null;
                var e = vis.Get(gc.enemyTeam);
                enemyArc = e != null ? e.arc : null;
            }
            float r = c != null ? c.captureRadius : 2.9f;
            float d = r * 2f * RingRadiusFactor;
            if (progressRing != null) progressRing.transform.localScale = new Vector3(d, d, 1f);
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
                var army = Army.PlayerArmy.Instance;
                if (army != null)
                {
                    // Attack battles: a unit on a pad hex, or with a soldier inside the drawn ring, counts in full
                    // (PlayerArmy.UnitCountsOn, the same rule the overlay tints by).
                    PlayersInside = army.PadCount(this, out int weight);
                    CaptureWeightInside = weight;
                }
                else
                {
                    PlayersInside = UnitRegistry.CountWithin(Team.Player, transform.position, cfg.captureRadius);
                    CaptureWeightInside = PlayersInside;
                }
                int enemies = UnitRegistry.CountWithin(Team.Enemy, transform.position, cfg.captureRadius);
                Contested = PlayersInside > 0 && enemies > 0;
            }
            if (Active)
            {
                float before = Progress;
                if (PlayersInside > 0 && !Contested)
                {
                    bool armyRules = Army.PlayerArmy.Instance != null;
                    int cap = armyRules ? cfg.armyMaxSoldierBonus : cfg.maxSoldierBonus;
                    float bonus = 1f + Mathf.Min(CaptureWeightInside, cap) * (armyRules ? cfg.armyPerSoldierBonus : cfg.perSoldierBonus);
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
            // Attack battles: a blocked capture flashes the ring in the enemy color so the player sees why.
            bool enemyLook = Army.PlayerArmy.Instance != null && ShowsContested && enemyArc != null
                             && Mathf.Repeat(Time.time * ContestFlashHz, 1f) < 0.5f;
            if (enemyLook != RingShowsEnemy && playerArc != null)
                progressRing.sharedMaterial = enemyLook ? enemyArc : playerArc;
            RingShowsEnemy = enemyLook;
        }

        /// <summary>Test and debug hook.</summary>
        public void ForceCapture() { if (!Captured) { Progress = 1f; Capture(); } }

        /// <summary>Test and debug hook: sets the progress of an uncaptured point.</summary>
        public void SetProgress(float p) { if (!Captured) Progress = Mathf.Clamp01(p); }
    }
}
