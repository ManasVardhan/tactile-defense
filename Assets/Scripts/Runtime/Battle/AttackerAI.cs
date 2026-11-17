using System.Collections.Generic;
using TactileWars.Combat;
using TactileWars.Formation;
using TactileWars.Squad;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>
    /// Plays the attacker in defense battles by driving the SquadController the way a player would (formations and
    /// taps; it never moves soldiers directly). Decisions every 'thinkInterval' seconds (our design, ESTIMATE):
    /// - Engage: a defender squad or structure within range: draw a LINE facing it at stand off distance, aimed at
    ///   the squad centre where the flag bearer stands; pop poppable bubbles after a reaction delay.
    /// - Spread: visible mines (or revealed geysers) near the next step: a wide RING to limit splash casualties.
    /// - Capture: near the active capture point: CLUSTER in a tight ring on the pad.
    /// - Advance: otherwise step along the grid route toward the active point (walls route through gates).
    /// Calls reinforcements when the meter is full.
    /// </summary>
    public class AttackerAI : MonoBehaviour
    {
        public enum State { Idle, Advance, Spread, Engage, Capture }

        public DefenseBattleConfig config;
        public SquadController squad;
        public BattleManager battle;

        public State Current { get; private set; }
        public int Orders { get; private set; }
        public int LinesFormed { get; private set; }
        public int RingsFormed { get; private set; }
        public int ClustersFormed { get; private set; }
        public int BubblesPopped { get; private set; }
        public int ReinforcementsCalled { get; private set; }
        public Vector3 LastTarget { get; private set; }

        readonly List<EnemySquad> squads = new List<EnemySquad>(32);
        readonly List<Mine> mines = new List<Mine>(32);
        readonly List<Geyser> geysers = new List<Geyser>(16);
        readonly Dictionary<EnemySquad, float> bubbleSeen = new Dictionary<EnemySquad, float>(16);
        readonly List<Vector3> pts = new List<Vector3>(64);
        readonly List<Vector3> route = new List<Vector3>(64);
        float think;
        float lastOrderTime = -99f;
        Vector3 lastEngagePoint;
        float engageSince;
        int killsAtEngage;
        float routeAge = 99f;
        int pointAtCluster = -1;

        void OnEnable() { BattleEvents.EnemySquadSpawned += OnSquadSpawned; }
        void OnDisable() { BattleEvents.EnemySquadSpawned -= OnSquadSpawned; }
        void OnSquadSpawned(EnemySquad s) { if (s != null && !squads.Contains(s)) squads.Add(s); }

        void Start()
        {
            if (battle == null) battle = BattleManager.Instance;
            if (squad == null) squad = SquadController.Instance;
            squads.AddRange(FindObjectsByType<EnemySquad>(FindObjectsSortMode.None));
            mines.AddRange(FindObjectsByType<Mine>(FindObjectsSortMode.None));
            geysers.AddRange(FindObjectsByType<Geyser>(FindObjectsSortMode.None));
            think = 1.0f; // let the start formation settle
        }

        float Cfg(float v, float def) => config != null ? v : def;

        void Update()
        {
            if (battle == null || squad == null || battle.Over) return;
            float dt = Time.deltaTime;
            routeAge += dt;
            think -= dt;
            if (think > 0f) return;
            think = Cfg(config != null ? config.thinkInterval : 0f, 0.4f);
            if (squad.AliveCount == 0) return;
            Decide();
        }

        void Decide()
        {
            Vector3 c = squad.Centroid;
            PopBubbles(c);
            if (battle.Meter != null && battle.Meter.Ready && battle.CallReinforcements()) ReinforcementsCalled++;

            var cp = battle.CurrentPoint;
            if (cp == null) return;
            Vector3 obj = cp.transform.position; obj.y = 0f;
            float capR = BattleContext.Battle != null ? BattleContext.Battle.captureRadius : 2.9f;

            // Threat: the nearest defender soldier squad or structure in range.
            float engage = config != null ? config.engageRange : 11f;
            Vector3 threatPos;
            bool threat = FindThreat(c, engage, out threatPos);

            float toObj = Flat(c - obj).magnitude;
            if (threat && !(toObj < capR + 1.5f && Flat(threatPos - obj).magnitude > capR + 6f) && Engage(c, threatPos)) return;
            if (toObj < capR + 4.5f)
            {
                Cluster(obj, battle.CurrentPointIndex);
                return;
            }
            Advance(c, obj);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        bool FindThreat(Vector3 c, float range, out Vector3 pos)
        {
            pos = default;
            // Defender squads first (target the flag bearer at the squad centre).
            float best = range * range;
            bool found = false;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                if (s == null || s.Wiped || s.AliveCount == 0) continue;
                float d = Flat(s.Center - c).sqrMagnitude;
                if (d < best) { best = d; pos = s.Center; found = true; }
            }
            if (found) return true;
            var h = UnitRegistry.FindNearest(Core.Team.Enemy, c, range);
            if (h != null && h.kind != TargetKind.Mine) { pos = h.transform.position; return true; }
            return false;
        }

        void PopBubbles(Vector3 c)
        {
            float now = Time.time;
            float reaction = config != null ? config.bubbleReaction : 0.8f;
            float range = config != null ? config.bubbleRange : 16f;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                if (s == null || s.Bubble == null || !s.Bubble.CanTap) continue;
                if (Flat(s.Center - c).magnitude > range) continue;
                if (!bubbleSeen.TryGetValue(s, out float t0)) { bubbleSeen[s] = now; continue; }
                if (now - t0 < reaction) continue;
                s.Bubble.OnTapped();
                BubblesPopped++;
            }
        }

        bool Recently(float seconds) => Time.time - lastOrderTime < seconds;

        /// <summary>False when a wall stands between the squad and its firing line (then it keeps advancing).</summary>
        bool Engage(Vector3 c, Vector3 target)
        {
            float reform = config != null ? config.reformInterval : 2.5f;
            bool newTarget = Current != State.Engage || Flat(target - lastEngagePoint).magnitude > 3f;
            if (newTarget) { engageSince = Time.time; killsAtEngage = battle.Kills; }
            // No progress for a while: push in closer.
            float stand = config != null ? config.standOff : 5.2f;
            float stall = config != null ? config.stallSeconds : 9f;
            if (Time.time - engageSince > stall && battle.Kills == killsAtEngage) stand *= 0.55f;
            else if (battle.Kills != killsAtEngage) { killsAtEngage = battle.Kills; engageSince = Time.time; }
            if (!newTarget && Recently(reform)) return true;

            Vector3 dir = Flat(target - c);
            if (dir.sqrMagnitude < 1e-3f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 center = target - dir * stand;
            var nav = squad.Nav;
            if (nav != null && !nav.LineClear(c, center)) return false;
            OrderLine(center, dir);
            lastEngagePoint = target;
            LastTarget = target;
            Current = State.Engage;
            return true;
        }

        void Cluster(Vector3 pad, int pointIndex)
        {
            if (Current == State.Capture && pointAtCluster == pointIndex && Recently(6f)) return;
            OrderRing(pad, config != null ? config.padClusterRadius : 1.6f);
            ClustersFormed++;
            pointAtCluster = pointIndex;
            LastTarget = pad;
            Current = State.Capture;
        }

        void Advance(Vector3 c, Vector3 obj)
        {
            // Route toward the objective on the coarse grid (refreshed every couple of seconds).
            var nav = squad.Nav;
            if (routeAge > 2f || route.Count == 0)
            {
                routeAge = 0f;
                if (nav == null || !nav.FindPath(c, obj, route)) { route.Clear(); route.Add(obj); }
            }
            float step = config != null ? config.stepDistance : 7f;
            Vector3 next = AlongRoute(c, step);

            bool danger = HazardNear(c, next);
            if (danger)
            {
                if (Current != State.Spread || !Recently(2f) || Flat(squad.Anchor - next).magnitude > 3f)
                {
                    OrderRing(next, config != null ? config.spreadRadius : 5.2f);
                    RingsFormed++;
                    Current = State.Spread;
                    LastTarget = next;
                }
                return;
            }

            bool arrived = Flat(squad.Anchor - c).magnitude < 3f;
            if (Current != State.Advance)
            {
                // Coming out of a fight or a ring: march in a compact line facing the objective.
                Vector3 dir = Flat(next - c);
                if (dir.sqrMagnitude < 1e-3f) dir = Vector3.forward;
                OrderLine(next, dir.normalized, 0.7f);
                Current = State.Advance;
                LastTarget = next;
                return;
            }
            if (arrived || !Recently(3f))
            {
                squad.MoveFormationTo(next);
                Orders++;
                lastOrderTime = Time.time;
                LastTarget = next;
            }
        }

        Vector3 AlongRoute(Vector3 c, float step)
        {
            Vector3 p = c;
            float left = step;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 d = Flat(route[i] - p);
                float m = d.magnitude;
                if (m >= left) return p + d / Mathf.Max(1e-4f, m) * left;
                left -= m;
                p = route[i];
            }
            return route.Count > 0 ? route[route.Count - 1] : p;
        }

        bool HazardNear(Vector3 a, Vector3 b)
        {
            float r = config != null ? config.mineAlertRange : 5f;
            for (int i = 0; i < mines.Count; i++)
            {
                var m = mines[i];
                if (m == null || m.Destroyed || !m.gameObject.activeInHierarchy) continue;
                if (SegmentDistance(m.transform.position, a, b) < r) return true;
            }
            for (int i = 0; i < geysers.Count; i++)
            {
                var g = geysers[i];
                if (g == null || g.Spent || g.Eruptions == 0) continue; // hidden until it has erupted once
                if (SegmentDistance(g.transform.position, a, b) < r) return true;
            }
            return false;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            return Flat(p - (a + ab * t)).magnitude;
        }

        void OrderLine(Vector3 center, Vector3 facing, float lengthScale = 1f)
        {
            int n = Mathf.Max(1, squad.AliveCount);
            float len = Mathf.Clamp(n * (config != null ? config.lineSpacing : 0.45f), config != null ? config.minLineLength : 6f, config != null ? config.maxLineLength : 15f) * lengthScale;
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
            pts.Clear();
            pts.Add(center - right * (len * 0.5f));
            pts.Add(center + right * (len * 0.5f));
            squad.SetFormation(pts, false, ShapeType.Line);
            LinesFormed++;
            Orders++;
            lastOrderTime = Time.time;
        }

        void OrderRing(Vector3 center, float radius)
        {
            pts.Clear();
            const int k = 40;
            for (int i = 0; i < k; i++)
            {
                float a = i / (float)k * Mathf.PI * 2f;
                pts.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            squad.SetFormation(pts, true, ShapeType.Circle);
            Orders++;
            lastOrderTime = Time.time;
        }
    }
}
