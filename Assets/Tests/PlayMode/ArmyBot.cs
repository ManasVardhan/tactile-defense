using System.Collections;
using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>
    /// The balance bot for attack battles: a reasonable, not optimal, player that drives the PlayerArmy through its
    /// public API only (Deploy, OrderMove, CallReinforcements), so it plays any unit type and any level.
    /// Policy:
    /// - "Forward" is the map's path (MapPath, from MapLayoutData.path), never an axis, so the bot plays any layout.
    /// - Deploy every Ready entry on free hexes 3 to 5 hexes short of the nearest enemy target on the way to the
    ///   current capture point (on the player's side of it). Draws-fire units (Tanks) take the row nearest the enemy.
    /// - Every 1.5 game seconds order each unit toward the current point: draws-fire units first, to the pad hexes
    ///   nearest its center; the others to free hexes one step behind them. A unit holds where it stands while an
    ///   enemy (soldier or structure; hidden traps are not seen) is within its weapon range.
    /// - Call reinforcements when the meter is ready and a unit was lost, and deploy what comes back.
    /// Flag bubbles are not popped. The battle runs at 3x game speed.
    /// </summary>
    public class ArmyBot
    {
        public const float Speed = 3f;
        public const float OrderInterval = 1.5f;

        public bool Victory { get; private set; }
        /// <summary>The battle ended (won or lost) inside the time limit.</summary>
        public bool Finished { get; private set; }
        public float GameTime { get; private set; }
        public int Survivors { get; private set; }
        public int Fielded { get; private set; }
        public int Reinforcements { get; private set; }
        public int Kills { get; private set; }
        public int Structures { get; private set; }
        public int PointsCaptured { get; private set; }
        public int Orders { get; private set; }
        /// <summary>Times a unit had a move order but stood still (under 0.5 units) for 8 game seconds.</summary>
        public int StuckEvents { get; private set; }
        readonly Dictionary<PlayerUnit, (Vector3 at, float since)> still = new Dictionary<PlayerUnit, (Vector3, float)>();

        readonly Dictionary<PlayerUnit, HexCoord> plan = new Dictionary<PlayerUnit, HexCoord>();
        int planFor = -1;

        public string Line(string level) =>
            $"{level}: {(Victory ? "VICTORY" : "DEFEAT")} in {GameTime:0}s game time, survivors {Survivors} of 9, soldiers fielded {Fielded}, " +
            $"reinforcement calls {Reinforcements}, points captured {PointsCaptured}, kills {Kills}, structures {Structures}, move orders {Orders}, stuck {StuckEvents}";

        public IEnumerator Play(float maxGameSeconds)
        {
            var bm = BattleManager.Instance;
            var army = PlayerArmy.Instance;
            GameSpeed.Set(Speed);
            float nextTick = 0f;
            while (!bm.Over && bm.Elapsed < maxGameSeconds)
            {
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + OrderInterval;
                    Tick(bm, army);
                }
                yield return null;
            }
            GameSpeed.Reset();
            Finished = bm.Over;
            Victory = bm.Over && bm.Result.victory;
            GameTime = bm.Elapsed;
            Survivors = army.AliveCount;
            Fielded = army.StartCount;
            Kills = bm.Kills;
            Structures = bm.StructuresDestroyed;
            PointsCaptured = 0;
            foreach (var p in bm.capturePoints) if (p.Captured) PointsCaptured++;
        }

        void Tick(BattleManager bm, PlayerArmy army)
        {
            var cp = bm.CurrentPoint;
            if (cp == null) return;
            bool lost = false;
            for (int i = 0; i < army.Squad.Count; i++) if (army.Squad[i].state == EntryState.Destroyed) lost = true;
            if (lost && bm.Meter.Ready && bm.CallReinforcements()) Reinforcements++;
            DeployReady(army, cp);
            OrderUnits(army, cp, bm.CurrentPointIndex);
            WatchStuck(army, bm.Elapsed);
        }

        void WatchStuck(PlayerArmy army, float now)
        {
            foreach (var u in army.Units)
            {
                if (u == null || u.IsDestroyed) continue;
                if (!u.IsMoving) { still.Remove(u); continue; }
                Vector3 c = Flat(u.Centroid);
                if (!still.TryGetValue(u, out var s) || Flat(c - s.at).magnitude > 0.5f) { still[u] = (c, now); continue; }
                if (now - s.since >= 8f) { StuckEvents++; still[u] = (c, now); }
            }
        }

        // ------------------------------------------------------------ deploy

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The nearest living enemy (soldier or structure, not a hidden trap) to 'from' on the way to the
        /// point: no further along the map's path (MapPath) than the point's far edge, and no further from the point
        /// than 'from' is plus a margin (so a branch the army is not taking is ignored). Null when the way is clear.</summary>
        static Health NearestOnTheWay(Vector3 from, Vector3 point)
        {
            Health best = null;
            float bestD = float.MaxValue;
            float limit = MapPath.Progress(point) + 8f;
            float reach = Flat(point - from).magnitude + 10f;
            foreach (var h in UnitRegistry.Of(Team.Enemy))
            {
                if (h == null || !h.IsAlive || h.kind == TargetKind.Mine) continue;
                Vector3 hp = h.transform.position;
                if (MapPath.Progress(hp) > limit || Flat(hp - point).magnitude > reach) continue;
                float d = Flat(h.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = h; }
            }
            return best;
        }

        void DeployReady(PlayerArmy army, CapturePoint cp)
        {
            var ready = new List<int>();
            for (int i = 0; i < army.Squad.Count; i++) if (army.Squad[i].state == EntryState.Ready) ready.Add(i);
            if (ready.Count == 0) return;
            // Draws-fire types first: they take the nearest row.
            ready.Sort((a, b) => DrawsFire(army, b).CompareTo(DrawsFire(army, a)));
            var g = army.Grid;
            Vector3 from = army.Units.Count > 0 ? army.Centroid : army.spawnPoint;
            Vector3 point = Flat(cp.transform.position);
            var target = NearestOnTheWay(from, point);
            Vector3 aim = target != null ? Flat(target.transform.position) : point;
            g.TryWorldToHex(aim, out HexCoord aimHex);
            var used = new List<HexCoord>();
            foreach (int entry in ready)
            {
                int ring = DrawsFire(army, entry) ? 3 : 4;
                HexCoord best = default;
                float bestScore = float.MaxValue;
                foreach (var c in g.Cells)
                {
                    int d = HexGrid.Distance(c, aimHex);
                    if (d < 3 || d > 5 || used.Contains(c) || !army.CanDeploy(entry, c)) continue;
                    Vector3 p = g.Center(c);
                    // The player's side of the target, toward where the army comes from.
                    if (Flat(p - from).sqrMagnitude > Flat(aim - from).sqrMagnitude) continue;
                    float score = Mathf.Abs(d - ring) * 100f + Flat(p - from).magnitude;
                    if (score < bestScore) { bestScore = score; best = c; }
                }
                if (bestScore == float.MaxValue) continue; // nowhere to stand yet; try again next tick
                if (army.Deploy(entry, best) != null) used.Add(best);
            }
        }

        static bool DrawsFire(PlayerArmy army, int entry)
        {
            var t = army.TypeConfig(army.Squad[entry].type);
            return t != null && t.drawsFire;
        }

        // ------------------------------------------------------------ orders

        static float RangeOf(PlayerUnit u)
        {
            foreach (var s in u.Soldiers)
                if (s != null && s.Config != null && s.Config.weapon != null) return s.Config.weapon.range;
            return 6f;
        }

        static bool EnemyWithin(Vector3 at, float range)
        {
            foreach (var h in UnitRegistry.Of(Team.Enemy))
            {
                if (h == null || !h.IsAlive || h.kind == TargetKind.Mine) continue;
                float r = range + h.targetRadius;
                if (Flat(h.transform.position - at).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        void OrderUnits(PlayerArmy army, CapturePoint cp, int pointIndex)
        {
            if (planFor != pointIndex) { plan.Clear(); planFor = pointIndex; }
            var units = new List<PlayerUnit>(army.Units);
            units.Sort((a, b) => b.Type.drawsFire.CompareTo(a.Type.drawsFire));
            var g = army.Grid;
            Vector3 point = Flat(cp.transform.position);
            float radius = BattleManager.Instance.battleConfig.captureRadius;
            var taken = new List<HexCoord>();
            var fronts = new List<HexCoord>();
            foreach (var u in units)
            {
                if (u == null || u.IsDestroyed) continue;
                bool front = u.Type.drawsFire || fronts.Count == 0;
                if (!plan.TryGetValue(u, out HexCoord dest) || taken.Contains(dest) || !Usable(g, dest, u))
                {
                    dest = front ? PadHex(army, point, radius, u, taken) : BehindHex(army, point, radius, u, fronts, taken);
                    plan[u] = dest;
                }
                taken.Add(dest);
                if (front) fronts.Add(dest);

                if (EnemyWithin(u.Centroid, RangeOf(u)))
                {
                    // Hold: stop where the unit stands.
                    if (u.IsMoving && g.TryWorldToHex(u.Centroid, out HexCoord here) && Usable(g, here, u) && army.OrderMove(u, here)) Orders++;
                    continue;
                }
                if (u.Hex != dest && army.OrderMove(u, dest)) Orders++;
            }
        }

        static bool Usable(HexGrid g, HexCoord c, PlayerUnit u) => g.InBounds(c) && (g.IsFree(c) || g.OwnerOf(c) == u.Id);

        /// <summary>The usable hex overlapping the pad nearest its center (ties: nearest the unit).</summary>
        static HexCoord PadHex(PlayerArmy army, Vector3 point, float radius, PlayerUnit u, List<HexCoord> taken)
        {
            var g = army.Grid;
            HexCoord best = u.Hex;
            float bestScore = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (taken.Contains(c) || !army.HexOverlapsPad(c, point, radius) || !Usable(g, c, u)) continue;
                Vector3 p = g.Center(c);
                float score = Flat(p - point).magnitude * 10f + Flat(p - u.Centroid).magnitude;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        /// <summary>A usable hex next to a front unit's hex, on the side the unit comes from (one hex behind).</summary>
        static HexCoord BehindHex(PlayerArmy army, Vector3 point, float radius, PlayerUnit u, List<HexCoord> fronts, List<HexCoord> taken)
        {
            var g = army.Grid;
            HexCoord best = u.Hex;
            float bestScore = float.MaxValue;
            var near = new List<HexCoord>(6);
            foreach (var f in fronts)
            {
                near.Clear();
                g.Neighbors(f, near);
                foreach (var c in near)
                {
                    if (taken.Contains(c) || !Usable(g, c, u)) continue;
                    Vector3 p = g.Center(c);
                    // Prefer hexes still on the pad, then the one nearest the unit.
                    float score = (army.HexOverlapsPad(c, point, radius) ? 0f : 50f) + Flat(p - u.Centroid).magnitude;
                    if (score < bestScore) { bestScore = score; best = c; }
                }
            }
            if (bestScore == float.MaxValue) return PadHex(army, point, radius, u, taken);
            return best;
        }
    }
}
