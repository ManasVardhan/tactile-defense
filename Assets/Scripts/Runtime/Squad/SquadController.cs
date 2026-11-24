using System.Collections.Generic;
using TactileWars.Army;
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
    public class SquadController : MonoBehaviour, IPlayerForce
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

        // Routing (anchor follows grid waypoints in a compact blob, then re-forms at the destination).
        NavGrid nav;
        bool navBuilt;
        readonly List<Vector3> route = new List<Vector3>(64);
        int[] soldierWp = new int[128];   // per soldier waypoint index; route.Count - 1 means "go to your slot"
        int routeTick;
        bool routing;
        static readonly Vector3[] blobOffsets = BuildBlob(160);

        public IReadOnlyList<Soldier> Soldiers => soldiers;
        public int AliveCount { get; private set; }
        public Vector3 Anchor => anchor;
        public Vector3 Centroid { get; private set; }
        public ShapeType LastShape { get; private set; }
        public bool IsDrawing => recorder != null && recorder.IsDrawing;
        public int StartCount => startCount;
        /// <summary>The squad fights while anyone is alive (no reserves to deploy).</summary>
        public bool CanStillFight => AliveCount > 0;
        public int SlotCount => slotCount;
        public FormationSlot GetSlotFor(int soldierIndex) => slots[assignment[soldierIndex]];
        /// <summary>True while the squad follows grid waypoints around an obstacle.</summary>
        public bool IsRouting => routing;
        public IReadOnlyList<Vector3> Route => route;
        /// <summary>Coarse obstacle grid (built on first use from the Obstacle layer).</summary>
        public NavGrid Nav { get { EnsureNav(); return nav; } }

        static Vector3[] BuildBlob(int n)
        {
            var a = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Sqrt(i + 0.5f);
                float ang = i * 2.39996f;
                a[i] = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            }
            return a;
        }

        void EnsureNav()
        {
            if (navBuilt) return;
            navBuilt = true;
            var gc = BattleContext.Config;
            Vector2 min = gc != null ? gc.mapMin : new Vector2(-20f, 0f);
            Vector2 max = gc != null ? gc.mapMax : new Vector2(20f, 110f);
            nav = NavGrid.Build(min - Vector2.one, max + Vector2.one, config != null ? config.navCell : 1f, config != null ? config.navClearance : 0.3f, 1 << Layers.Obstacle);
        }

        /// <summary>Drops the cached grid (call after the map's props change).</summary>
        public void InvalidateNav() { navBuilt = false; nav = null; }

        float cheerTimer;
        bool cheerHeld;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; BattleEvents.PointCaptured -= OnPointCaptured; }

        void OnPointCaptured(int index) { CheerFor(1.6f); }

        /// <summary>Short cheer (capture), unless a held cheer (victory) is already on.</summary>
        public void CheerFor(float seconds)
        {
            if (cheerHeld) return;
            cheerTimer = seconds;
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].View.Cheer(true);
        }

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
            BattleEvents.PointCaptured -= OnPointCaptured;
            BattleEvents.PointCaptured += OnPointCaptured;
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
            soldierWp[soldiers.Count - 1] = int.MaxValue / 2; // newcomers are not routing
            return s;
        }

        void EnsureCapacity(int n)
        {
            if (slots.Length >= n) return;
            int c = Mathf.NextPowerOfTwo(n);
            System.Array.Resize(ref slots, c);
            System.Array.Resize(ref assignment, c);
            System.Array.Resize(ref positions, c);
            System.Array.Resize(ref soldierWp, c);
        }

        void SetPath(List<Vector3> pts, bool closed)
        {
            pathPts.Clear();
            for (int i = 0; i < pts.Count; i++) pathPts.Add(pts[i]);
            pathClosed = closed;
        }

        void SolveAndAssign(Vector3 behindHint, bool keepRoute = false)
        {
            int n = soldiers.Count;
            if (n == 0) return;
            EnsureCapacity(n);
            var p = formation != null ? formation.ToParams() : FormationParams.Default;
            slotCount = solver.Solve(pathPts, pathClosed, n, p, behindHint, slots);
            for (int i = 0; i < n; i++) positions[i] = soldiers[i].transform.position;
            solver.Assign(positions, n, slots, assignment);
            ApplyTargets(!keepRoute);
            if (keepRoute && routing) ApplyRouteTargets();
        }

        void ApplyTargets(bool cancelRoute = true)
        {
            if (cancelRoute) routing = false;
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
            ExtendLiveTail(scratch);
            SetPath(scratch, false);
            SolveAndAssign(Centroid);
            respaceTimer = -1f;
        }

        /// <summary>While drawing, a short stroke cannot hold the whole squad single file, and the solver would pack the
        /// overflow into extra rows (a clump). Instead the chain streams: the stroke is filled at min spacing from its
        /// leading end, and the rest of the squad queues in a column that runs from the stroke start back toward the
        /// squad, so the soldiers flow onto the line as it grows (like the column in f_0056).</summary>
        void ExtendLiveTail(List<Vector3> pts)
        {
            int n = soldiers.Count;
            if (n < 2 || pts.Count < 2) return;
            float minSp = formation != null ? formation.minSpacing : 0.7f;
            float need = (n - 1) * minSp;
            float L = 0f;
            for (int i = 1; i < pts.Count; i++) L += Vector3.Distance(pts[i - 1], pts[i]);
            float extra = need - L;
            if (extra <= 0.01f) return;
            Vector3 p0 = pts[0];
            Vector3 dir = Centroid - p0; dir.y = 0f;
            Vector3 strokeDir = pts[Mathf.Min(pts.Count - 1, 3)] - p0; strokeDir.y = 0f;
            if (dir.magnitude < 2f || (strokeDir.sqrMagnitude > 1e-4f && Vector3.Dot(dir.normalized, strokeDir.normalized) > 0.7f))
                dir = strokeDir.sqrMagnitude > 1e-4f ? -strokeDir : Vector3.back;
            dir.Normalize();
            float step = formation != null ? formation.resampleStep : 0.25f;
            int k = Mathf.CeilToInt(extra / step);
            var gc = BattleContext.Config;
            // Prepend tail points (farthest first) so the path still runs tail -> stroke start -> leading end.
            work.Clear();
            for (int i = k; i >= 1; i--)
            {
                Vector3 q = p0 + dir * Mathf.Min(extra, i * step);
                if (gc != null)
                {
                    q.x = Mathf.Clamp(q.x, gc.mapMin.x, gc.mapMax.x);
                    q.z = Mathf.Clamp(q.z, gc.mapMin.y, gc.mapMax.y);
                }
                work.Add(q);
            }
            for (int i = 0; i < pts.Count; i++) work.Add(pts[i]);
            pts.Clear();
            for (int i = 0; i < work.Count; i++) pts.Add(work[i]);
        }

        /// <summary>Presentation: a rippling hop when a formation is set.</summary>
        void HopAll()
        {
            var cc = BattleContext.Combat;
            float stagger = cc != null ? cc.hopStagger : 0.18f;
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i] != null) soldiers[i].Hop(Random.value * stagger);
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
                ? new ShapeRecognizer.Settings { closedThreshold = f.closedThreshold, circleTolerance = f.circleTolerance, cornerAngle = f.cornerAngle, lineStraightness = f.lineStraightness, lineMaxDeviation = f.lineMaxDeviation }
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
            HopAll();
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

        /// <summary>Sets a formation from a world path (the AI attacker's "drawn" shape): same solver as a stroke,
        /// shows the painted stroke and raises ShapeRecognized. Closed paths form rings.</summary>
        public void SetFormation(List<Vector3> pts, bool closed, ShapeType shape)
        {
            if (pts == null || pts.Count < 2) return;
            work.Clear();
            for (int i = 0; i < pts.Count; i++) { var p = pts[i]; p.y = 0f; work.Add(p); }
            PathResampler.ResampleBySpacing(work, closed, formation != null ? formation.resampleStep : 0.25f, scratch);
            SetPath(scratch, closed);
            SolveAndAssign(Centroid);
            LastShape = shape;
            if (strokeRenderer != null) strokeRenderer.ShowFinal(pathPts, closed);
            if (ring != null) ring.Pulse();
            HopAll();
            BattleEvents.RaiseShapeRecognized(shape);
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
            TryRoute(anchor);
            if (ring != null) ring.Pulse();
            AudioManager.Play(SoundId.FormationSet, world);
        }

        /// <summary>When walls block the straight way to 'dest', follow grid waypoints as a compact blob first.</summary>
        void TryRoute(Vector3 dest)
        {
            if (config != null && !config.useRouting) return;
            if (AliveCount == 0 && soldiers.Count == 0) return;
            Vector3 from = AliveCount > 0 ? Centroid : anchor;
            var g = Nav;
            if (g == null || g.LineClear(from, dest)) return;
            if (!g.FindPath(from, dest, route) || route.Count < 2) return;
            routing = true;
            for (int i = 0; i < soldiers.Count; i++) soldierWp[i] = 0;
            ApplyRouteTargets();
        }

        /// <summary>Every soldier follows the shared waypoints on its own (advancing when it reaches its waypoint
        /// or can see the next one), then heads for its formation slot. Stragglers never get sent straight at a wall.</summary>
        void ApplyRouteTargets()
        {
            for (int i = 0; i < soldiers.Count; i++) SetRouteTarget(i);
        }

        void SetRouteTarget(int i)
        {
            int last = route.Count - 1;
            int k = soldierWp[i];
            var m = soldiers[i].Motor;
            if (k >= last)
            {
                var slot = slots[assignment[i]];
                m.Target = slot.position;
                m.RestFacing = slot.facing;
                return;
            }
            float sp = (config != null ? config.routeBlobSpacing : 0.5f) * 0.45f;
            Vector3 wp = route[k];
            Vector3 dir = route[k + 1] - wp; dir.y = 0f;
            m.Target = wp + blobOffsets[i % blobOffsets.Length] * sp;
            m.RestFacing = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
        }

        void TickRoute()
        {
            if (!routing) return;
            int last = route.Count - 1;
            float adv = config != null ? config.routeAdvanceDistance : 2.2f;
            int done = 0;
            routeTick++;
            for (int i = 0; i < soldiers.Count; i++)
            {
                int k = soldierWp[i];
                if (k >= last) { done++; continue; }
                if (((i + routeTick) & 1) != 0) continue;   // half the soldiers per frame
                Vector3 pos = soldiers[i].transform.position;
                Vector3 d = pos - route[k]; d.y = 0f;
                Vector3 next = k + 1 >= last ? slots[assignment[i]].position : route[k + 1];
                if (d.magnitude < adv * 0.6f || (nav != null && nav.LineClear(pos, next, 0.7f)))
                {
                    soldierWp[i] = k + 1;
                    SetRouteTarget(i);
                }
            }
            if (done >= soldiers.Count) routing = false;
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
                soldierWp[i] = soldierWp[last];
                soldiers.RemoveAt(last);
                removed = true;
            }
            if (removed && config != null) respaceTimer = config.respaceDelay;
            if (respaceTimer > 0f)
            {
                respaceTimer -= dt;
                if (respaceTimer <= 0f && !IsDrawing && soldiers.Count > 0) SolveAndAssign(Centroid + (Centroid - anchor), routing);
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

            if (routing && alive > 0) TickRoute();
            if (cheerTimer > 0f)
            {
                cheerTimer -= dt;
                if (cheerTimer <= 0f && !cheerHeld)
                    for (int i = 0; i < n; i++) if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].View.Cheer(false);
            }

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
            routing = false;
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].Motor.Teleport(slots[assignment[i]].position);
            ringSnap = true;
        }

        public void SetCheer(bool on)
        {
            cheerHeld = on;
            cheerTimer = 0f;
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].View.Cheer(on);
        }
    }
}
