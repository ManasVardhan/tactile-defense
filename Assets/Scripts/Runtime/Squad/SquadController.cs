using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Squad
{
    /// <summary>
    /// The player squad. Listens to StrokeRecorder: while dragging it re-solves the formation on the partial
    /// stroke so the chain chases the finger; on release it smooths, recognizes, solves and assigns. A tap
    /// moves the whole formation (shape preserved) so its centroid lands on the tapped point; a tap on a
    /// bubble pops it instead.
    /// </summary>
    public class SquadController : MonoBehaviour
    {
        public static SquadController Instance { get; private set; }

        public SquadConfig config;
        public FormationConfig formation;
        public UnitConfig soldierConfig;
        public StrokeRecorder recorder;
        public StrokeRenderer strokeRenderer;
        public SquadRing ring;
        public Transform spawnPoint;

        [Header("Loadout (set by BattleSetup before Start; null keeps the defaults)")]
        [Tooltip("Stats for the squad leader (Hero model). Null uses soldierConfig.")] public UnitConfig leaderConfig;
        [Tooltip("Hired mercenary stats. Null means no mercenary joins the battle.")] public UnitConfig mercenaryConfig;
        public float mercenaryScale = 1.2f;
        public string mercenaryName = "Mercenary";

        /// <summary>The hired mercenary (Hero model variant), or null.</summary>
        public Soldier Mercenary { get; private set; }

        readonly List<Soldier> soldiers = new List<Soldier>(96);
        readonly FormationSolver solver = new FormationSolver();
        readonly List<Vector3> work = new List<Vector3>(1024);
        readonly List<Vector3> scratch = new List<Vector3>(2048);
        readonly List<Vector3> pathPts = new List<Vector3>(1024);   // current formation path (smoothed)
        bool pathClosed;
        FormationSlot[] slots = new FormationSlot[128];
        int[] assignment = new int[128];
        Vector3[] positions = new Vector3[128];
        int slotCount;
        Vector3 anchor;
        float liveTimer;
        float respaceTimer = -1f;
        bool ringSnap = true;
        int lastReportedCount = -1;
        int startCount;

        public IReadOnlyList<Soldier> Soldiers => soldiers;
        public int AliveCount { get; private set; }
        public Vector3 Anchor => anchor;
        public Vector3 Centroid { get; private set; }
        public ShapeType LastShape { get; private set; }
        public bool IsDrawing => recorder != null && recorder.IsDrawing;
        public int StartCount => startCount;
        public int SlotCount => slotCount;
        public FormationSlot GetSlotFor(int soldierIndex) => slots[assignment[soldierIndex]];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            if (recorder == null) return;
            recorder.StrokeBegan += OnStrokeLive;
            recorder.StrokeChanged += OnStrokeLive;
            recorder.StrokeEnded += OnStrokeEnded;
            recorder.StrokeCancelled += OnStrokeCancelled;
            recorder.Tapped += OnTapped;
        }

        void OnDisable()
        {
            if (recorder == null) return;
            recorder.StrokeBegan -= OnStrokeLive;
            recorder.StrokeChanged -= OnStrokeLive;
            recorder.StrokeEnded -= OnStrokeEnded;
            recorder.StrokeCancelled -= OnStrokeCancelled;
            recorder.Tapped -= OnTapped;
        }

        void Start()
        {
            var vis = BattleContext.Visuals;
            var gc = BattleContext.Config;
            if (strokeRenderer != null) strokeRenderer.Init(vis != null && gc != null ? vis.Get(gc.playerTeam) : null);
            if (ring != null && config != null) ring.SetRadius(config.ringRadius);
            SpawnInitial(config != null ? config.soldierCount : 40);
        }

        void SpawnInitial(int n)
        {
            Vector3 c = spawnPoint != null ? spawnPoint.position : transform.position;
            c.y = 0f;
            // Start formation: a U shaped single file chain opening toward the player, like ps_12.
            work.Clear();
            float hw = (config != null ? config.startWidth : 15f) * 0.5f;
            float d = config != null ? config.startDepth : 6.5f;
            float top = c.z + d * 0.45f, bottom = top - d;
            work.Add(new Vector3(c.x - hw, 0f, bottom));
            work.Add(new Vector3(c.x - hw, 0f, top - 0.8f));
            work.Add(new Vector3(c.x - hw + 0.8f, 0f, top));
            work.Add(new Vector3(c.x + hw - 0.8f, 0f, top));
            work.Add(new Vector3(c.x + hw, 0f, top - 0.8f));
            work.Add(new Vector3(c.x + hw, 0f, bottom));
            PathResampler.ResampleBySpacing(work, false, 0.25f, scratch);
            work.Clear();
            for (int i = 0; i < scratch.Count; i++) work.Add(scratch[i]);
            for (int i = 0; i < n; i++) AddSoldier(c + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), i == n / 2);
            if (mercenaryConfig != null)
            {
                Mercenary = AddSoldier(c + new Vector3(0f, 0f, 1f), true, mercenaryConfig);
                if (Mercenary != null)
                {
                    Mercenary.name = "Mercenary_" + mercenaryName;
                    Mercenary.transform.localScale *= mercenaryScale;
                }
            }
            SetPath(work, false);
            SolveAndAssign(c + Vector3.back * 3f);
            for (int i = 0; i < soldiers.Count; i++) soldiers[i].Motor.Teleport(slots[assignment[i]].position);
            startCount = soldiers.Count;
            ringSnap = true;
        }

        Soldier AddSoldier(Vector3 pos, bool leader = false, UnitConfig overrideConfig = null)
        {
            var prefabs = BattleContext.Prefabs;
            if (prefabs == null || prefabs.soldier == null) return null;
            var gc = BattleContext.Config;
            var prefab = leader && prefabs.hero != null ? prefabs.hero : prefabs.soldier;
            var s = Instantiate(prefab, pos, Quaternion.identity, transform);
            s.name = (leader && prefabs.hero != null ? "Leader_" : "Player_") + soldiers.Count;
            var cfg = overrideConfig != null ? overrideConfig : (leader && leaderConfig != null ? leaderConfig : soldierConfig);
            s.Setup(Team.Player, gc != null ? gc.playerTeam : TeamColorId.Red, cfg);
            s.Motor.Teleport(pos);
            soldiers.Add(s);
            EnsureCapacity(soldiers.Count);
            return s;
        }

        void EnsureCapacity(int n)
        {
            if (slots.Length >= n) return;
            int c = Mathf.NextPowerOfTwo(n);
            System.Array.Resize(ref slots, c);
            System.Array.Resize(ref assignment, c);
            System.Array.Resize(ref positions, c);
        }

        void SetPath(List<Vector3> pts, bool closed)
        {
            pathPts.Clear();
            for (int i = 0; i < pts.Count; i++) pathPts.Add(pts[i]);
            pathClosed = closed;
        }

        void SolveAndAssign(Vector3 behindHint)
        {
            int n = soldiers.Count;
            if (n == 0) return;
            EnsureCapacity(n);
            var p = formation != null ? formation.ToParams() : FormationParams.Default;
            slotCount = solver.Solve(pathPts, pathClosed, n, p, behindHint, slots);
            for (int i = 0; i < n; i++) positions[i] = soldiers[i].transform.position;
            solver.Assign(positions, n, slots, assignment);
            ApplyTargets();
        }

        void ApplyTargets()
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < soldiers.Count; i++)
            {
                var slot = slots[assignment[i]];
                soldiers[i].Motor.Target = slot.position;
                soldiers[i].Motor.RestFacing = slot.facing;
                sum += slot.position;
            }
            if (soldiers.Count > 0) anchor = sum / soldiers.Count;
        }

        // ---------------- input handlers ----------------

        void OnStrokeLive(List<Vector3> raw)
        {
            AudioManager.SetLoop(SoundId.StrokeDraw, true);
            if (strokeRenderer != null) strokeRenderer.ShowLive(raw);
            liveTimer -= Time.deltaTime;
            if (liveTimer > 0f) return;
            liveTimer = formation != null ? formation.liveSolveInterval : 0.08f;
            work.Clear();
            for (int i = 0; i < raw.Count; i++) work.Add(raw[i]);
            PathResampler.Chaikin(work, false, 1, scratch);
            PathResampler.ResampleBySpacing(work, false, formation != null ? formation.resampleStep : 0.25f, scratch);
            SetPath(scratch, false);
            SolveAndAssign(Centroid);
            respaceTimer = -1f;
        }

        void OnStrokeCancelled()
        {
            AudioManager.SetLoop(SoundId.StrokeDraw, false);
            if (strokeRenderer != null) strokeRenderer.Hide();
        }

        void OnStrokeEnded(List<Vector3> raw)
        {
            AudioManager.SetLoop(SoundId.StrokeDraw, false);
            var f = formation;
            var settings = f != null
                ? new ShapeRecognizer.Settings { closedThreshold = f.closedThreshold, circleTolerance = f.circleTolerance, cornerAngle = f.cornerAngle, lineStraightness = f.lineStraightness }
                : ShapeRecognizer.Settings.Default;
            work.Clear();
            for (int i = 0; i < raw.Count; i++) work.Add(raw[i]);
            var shape = ShapeRecognizer.Recognize(work, settings);
            LastShape = shape.type;
            bool closed = shape.closed;
            if (closed && work.Count > 2) work.RemoveAt(work.Count - 1); // drop the overlap at the seam

            bool snap = f == null || f.snapRecognizedShapes;
            if (snap && shape.type == ShapeType.Circle)
            {
                work.Clear();
                int k = 64;
                for (int i = 0; i < k; i++)
                {
                    float a = i / (float)k * Mathf.PI * 2f;
                    work.Add(shape.center + new Vector3(Mathf.Cos(a) * shape.radius, 0f, Mathf.Sin(a) * shape.radius));
                }
            }
            else if (snap && shape.type == ShapeType.Line)
            {
                Vector3 a = raw[0], b = raw[raw.Count - 1];
                work.Clear();
                work.Add(a); work.Add(b);
            }
            else
            {
                PathResampler.Chaikin(work, closed, f != null ? f.smoothingIterations : 3, scratch);
            }
            PathResampler.ResampleBySpacing(work, closed, f != null ? f.resampleStep : 0.25f, scratch);
            SetPath(scratch, closed);
            SolveAndAssign(Centroid);
            if (strokeRenderer != null) strokeRenderer.ShowFinal(pathPts, closed);
            if (ring != null) ring.Pulse();
            AudioManager.Play(SoundId.FormationSet, anchor);
            BattleEvents.RaiseShapeRecognized(shape.type);
        }

        void OnTapped(Vector3 world, Vector2 screen)
        {
            var cam = recorder != null ? recorder.cam : Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(screen);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, Layers.PickupMask, QueryTriggerInteraction.Collide))
                {
                    var tappable = hit.collider.GetComponentInParent<ITappable>();
                    if (tappable != null && tappable.CanTap) { tappable.OnTapped(); return; }
                }
            }
            MoveFormationTo(world);
        }

        /// <summary>Translates the current formation so the slot centroid lands on 'world'.</summary>
        public void MoveFormationTo(Vector3 world)
        {
            world.y = 0f;
            Vector3 delta = world - anchor;
            var gc = BattleContext.Config;
            if (gc != null)
            {
                // Keep every slot inside the map.
                for (int i = 0; i < slotCount; i++)
                {
                    Vector3 p = slots[i].position + delta;
                    if (p.x < gc.mapMin.x) delta.x += gc.mapMin.x - p.x;
                    if (p.x > gc.mapMax.x) delta.x -= p.x - gc.mapMax.x;
                    if (p.z < gc.mapMin.y) delta.z += gc.mapMin.y - p.z;
                    if (p.z > gc.mapMax.y) delta.z -= p.z - gc.mapMax.y;
                }
            }
            for (int i = 0; i < pathPts.Count; i++) pathPts[i] += delta;
            for (int i = 0; i < slotCount; i++) slots[i].position += delta;
            solver.Path.Translate(delta);
            ApplyTargets();
            if (ring != null) ring.Pulse();
            AudioManager.Play(SoundId.FormationSet, world);
        }

        /// <summary>Spawns backup soldiers behind the squad and re-solves the formation.</summary>
        public int Reinforce(int count)
        {
            Vector3 c = Centroid;
            Vector3 back = c + Vector3.back * 3.5f;
            for (int i = 0; i < count; i++)
            {
                var s = AddSoldier(back + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-1.5f, 1f)));
                if (s != null && VfxManager.Instance != null)
                    VfxManager.Instance.Play(VfxType.Reinforce, s.transform.position, Color.white, 1f);
            }
            SolveAndAssign(back);
            return count;
        }

        // ---------------- per frame ----------------

        void Update()
        {
            float dt = Time.deltaTime;
            var gc = BattleContext.Config;
            Vector2 bmin = gc != null ? gc.mapMin : new Vector2(-100, -100);
            Vector2 bmax = gc != null ? gc.mapMax : new Vector2(100, 100);

            // Remove soldiers that finished dying; keep assignment aligned.
            bool removed = false;
            for (int i = soldiers.Count - 1; i >= 0; i--)
            {
                if (soldiers[i] != null && !soldiers[i].IsGone) continue;
                int last = soldiers.Count - 1;
                soldiers[i] = soldiers[last];
                assignment[i] = assignment[last];
                soldiers.RemoveAt(last);
                removed = true;
            }
            if (removed && config != null) respaceTimer = config.respaceDelay;
            if (respaceTimer > 0f)
            {
                respaceTimer -= dt;
                if (respaceTimer <= 0f && !IsDrawing && soldiers.Count > 0) SolveAndAssign(Centroid + (Centroid - anchor));
            }

            int n = soldiers.Count;
            Vector3 sum = Vector3.zero;
            int alive = 0;
            for (int i = 0; i < n; i++)
            {
                positions[i] = soldiers[i].transform.position;
                if (soldiers[i].IsAlive) { sum += positions[i]; alive++; }
            }
            AliveCount = alive;
            if (alive > 0) Centroid = sum / alive;

            if (soldierConfig != null)
            {
                // Everybody marches at the squad's speed so the formation stays together (leader and mercenary
                // stat differences are health, damage and fire rate).
                var ms = soldierConfig.ToMotor(bmin, bmax);
                for (int i = 0; i < n; i++) soldiers[i].Tick(dt, ms, positions, n, i);
            }

            if (ring != null && config != null)
            {
                ring.gameObject.SetActive(alive > 0);
                ring.Follow(anchor, config.ringFollowSharpness, dt, ringSnap);
                ringSnap = false;
            }

            if (alive != lastReportedCount)
            {
                lastReportedCount = alive;
                BattleEvents.RaiseSoldierCountChanged(alive, Mathf.Max(startCount, alive));
            }
        }

        /// <summary>Debug and test hook: snap every soldier onto its slot.</summary>
        public void TeleportToSlots()
        {
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].Motor.Teleport(slots[assignment[i]].position);
            ringSnap = true;
        }

        public void SetCheer(bool on)
        {
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].View.Cheer(on);
        }
    }
}
