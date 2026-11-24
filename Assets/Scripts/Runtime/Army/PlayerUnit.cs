using System;
using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>
    /// One player unit: a few soldiers that hold one hex of the army's grid. Each soldier stands on its slot of the
    /// hex; a move follows nav grid waypoints (each soldier keeps its slot offset) to the destination hex, which is
    /// reserved for the unit as soon as the order is accepted. Ticked by its PlayerArmy.
    /// </summary>
    public class PlayerUnit : MonoBehaviour
    {
        const float ArriveDistance = 0.5f;     // every soldier this close to its slot ends a move
        const float WaypointAdvance = 1.2f;    // centroid this close to a waypoint moves on to the next one

        public UnitTypeConfig Type { get; private set; }
        /// <summary>The hex the unit holds: where it stands, or where it is heading while moving.</summary>
        public HexCoord Hex { get; private set; }
        public bool IsMoving { get; private set; }
        /// <summary>Target hex of the current or last move (equals Hex).</summary>
        public HexCoord Destination => Hex;
        /// <summary>Soldiers still in the scene, including ones playing their death.</summary>
        public IReadOnlyList<Soldier> Soldiers => soldiers;
        public int AliveCount { get; private set; }
        public bool IsDestroyed { get; private set; }
        /// <summary>Mean position of the living soldiers; the last value when none are alive.</summary>
        public Vector3 Centroid { get; private set; }
        /// <summary>Unique id, at least 1; the owner id of the unit's hex reservation.</summary>
        public int Id { get; private set; }
        /// <summary>Soldiers spawned by this unit.</summary>
        public int StartCount { get; private set; }
        public bool Selected { get; set; }
        /// <summary>The hired mercenary's one soldier unit (healed like any unit, never revived).</summary>
        public bool IsMercenary => Type != null && Type.id == PlayerArmy.MercenaryTypeId;

        /// <summary>The Healer ability (null for other types).</summary>
        public HealerAbility Healer => healer;

        /// <summary>Raised once, when the last soldier dies.</summary>
        public event Action<PlayerUnit> Destroyed;

        PlayerArmy army;
        UnitConfig config;
        MotorSettings motor;
        readonly List<Soldier> soldiers = new List<Soldier>(4);
        readonly List<int> soldierSlot = new List<int>(4);
        readonly List<Vector3> route = new List<Vector3>(32);
        int waypoint;
        Vector3[] positions = new Vector3[4];
        HealerAbility healer;
        int spawned;
        bool cheering;

        /// <summary>Sum of the living soldiers' health over the unit's full health.</summary>
        public float HealthFraction
        {
            get
            {
                float full = StartCount * (config != null ? config.maxHealth : 0f);
                if (full <= 0f) return 0f;
                float sum = 0f;
                for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) sum += soldiers[i].Health.Current;
                return Mathf.Clamp01(sum / full);
            }
        }

        /// <summary>Spawns the soldiers on 'hex' (already reserved for 'id' by the army) with the scaled config.
        /// False (nothing spawned) when the soldier prefab is missing.</summary>
        public bool Init(PlayerArmy army, UnitTypeConfig type, UnitConfig soldierConfig, HexCoord hex, int id)
        {
            this.army = army;
            Type = type;
            config = soldierConfig;
            Hex = hex;
            Id = id;
            var gc = BattleContext.Config;
            Vector2 bmin = gc != null ? gc.mapMin : new Vector2(-100, -100);
            Vector2 bmax = gc != null ? gc.mapMax : new Vector2(100, 100);
            motor = soldierConfig.ToMotor(bmin, bmax);

            var prefabs = BattleContext.Prefabs;
            if (prefabs == null || prefabs.soldier == null) return false;
            int n = Mathf.Max(1, type.soldierCount);
            if (positions.Length < n + 1) positions = new Vector3[n + 1];
            var grid = army.Grid;
            for (int i = 0; i < n; i++) SpawnSoldier(i % 3, grid.SlotPosition(hex, i % 3));
            StartCount = soldiers.Count;
            AliveCount = soldiers.Count;
            Centroid = grid.Center(hex);
            if (type.healPerSecond > 0f || type.reviveInterval > 0f) healer = new HealerAbility(this, army);
            return true;
        }

        /// <summary>Spawns one soldier of this unit's type into 'slot' at 'p'; ticked, counted and cleaned up like the rest.</summary>
        Soldier SpawnSoldier(int slot, Vector3 p)
        {
            var gc = BattleContext.Config;
            var s = Instantiate(BattleContext.Prefabs.soldier, p, Quaternion.identity, transform);
            s.name = "Unit" + Id + "_" + Type.id + "_" + spawned++;
            s.Setup(Team.Player, gc != null ? gc.playerTeam : TeamColorId.Red, config);
            s.Health.drawsFire = Type.drawsFire;
            s.Health.Died += OnSoldierDied;
            s.HoldFire = !Type.hasWeapon;
            s.Targeting.PreferStructures = Type.prefersStructures;
            s.transform.localScale *= Type.modelScale;
            // The type's accent on the helmet, so unit types read apart by color; the body keeps the team color.
            if (s.tint != null) s.SetAccent(army.AccentFor(Type, s.tint.baseAccent));
            s.Motor.Teleport(p);
            s.Motor.Target = p;
            soldiers.Add(s);
            soldierSlot.Add(slot);
            return s;
        }

        /// <summary>Brings one dead soldier back at 'healthFraction' of its health on a free slot (Healer revive).
        /// Only while the unit stands with at least one and fewer than its full count living. Returns the soldier.</summary>
        public Soldier ReviveOne(float healthFraction)
        {
            if (IsDestroyed || IsMercenary || army == null || AliveCount < 1 || AliveCount >= StartCount) return null;
            if (BattleContext.Prefabs == null || BattleContext.Prefabs.soldier == null) return null;
            int slot = -1;
            for (int k = 0; k < 3 && slot < 0; k++)
            {
                bool used = false;
                for (int i = 0; i < soldiers.Count; i++) if (soldierSlot[i] == k && soldiers[i] != null && soldiers[i].IsAlive) used = true;
                if (!used) slot = k;
            }
            if (slot < 0) return null;
            var grid = army.Grid;
            Vector3 p = Centroid + (grid.SlotPosition(Hex, slot) - grid.Center(Hex));
            if (!IsMoving) p = grid.SlotPosition(Hex, slot);
            p.y = 0f;
            var s = SpawnSoldier(slot, p);
            s.Health.SetFraction(healthFraction);
            if (cheering) s.View.Cheer(true);
            if (positions.Length < soldiers.Count) positions = new Vector3[soldiers.Count + 1];
            AliveCount++;
            return s;
        }

        /// <summary>Orders a move to 'dest'. Fails without any change when the unit is destroyed, 'dest' is held by
        /// someone else or blocked, or the nav grid has no route.</summary>
        public bool MoveTo(HexCoord dest)
        {
            if (IsDestroyed || army == null) return false;
            var grid = army.Grid;
            if (!grid.IsFree(dest) && grid.OwnerOf(dest) != Id) return false;
            var path = army.PathScratch;
            if (!army.Nav.FindPath(Centroid, grid.Center(dest), path)) return false;
            if (!grid.TryReserve(dest, Id)) return false;
            if (Hex != dest) grid.Release(Hex, Id);
            Hex = dest;
            route.Clear();
            for (int i = 0; i < path.Count; i++) route.Add(path[i]);
            if (route.Count == 0 || (route[route.Count - 1] - grid.Center(dest)).sqrMagnitude > 1e-4f) route.Add(grid.Center(dest));
            waypoint = 0;
            IsMoving = true;
            return true;
        }

        /// <summary>Called by the army once per frame.</summary>
        public void Tick(float dt)
        {
            // Drop soldiers that finished dying.
            for (int i = soldiers.Count - 1; i >= 0; i--)
            {
                if (soldiers[i] != null && !soldiers[i].IsGone) continue;
                int last = soldiers.Count - 1;
                soldiers[i] = soldiers[last];
                soldierSlot[i] = soldierSlot[last];
                soldiers.RemoveAt(last);
                soldierSlot.RemoveAt(last);
            }

            int n = soldiers.Count;
            if (positions.Length < n) positions = new Vector3[n + 1];
            Vector3 sum = Vector3.zero;
            int alive = 0;
            for (int i = 0; i < n; i++)
            {
                positions[i] = soldiers[i].transform.position;
                if (soldiers[i].IsAlive) { sum += positions[i]; alive++; }
            }
            AliveCount = alive;
            if (alive > 0) Centroid = sum / alive;

            if (!IsDestroyed && army != null) SetTargets();
            for (int i = 0; i < n; i++) soldiers[i].Tick(dt, motor, positions, n, i);
            if (!IsDestroyed && healer != null) healer.Tick(dt);
        }

        void SetTargets()
        {
            var grid = army.Grid;
            Vector3 center = grid.Center(Hex);
            int last = route.Count - 1;
            if (IsMoving && waypoint < last)
            {
                Vector3 d = route[waypoint] - Centroid; d.y = 0f;
                if (d.magnitude < WaypointAdvance || army.Nav.LineClear(Centroid, route[waypoint + 1])) waypoint++;
            }
            bool final = !IsMoving || waypoint >= last;
            Vector3 anchor = final ? center : route[waypoint];
            Vector3 facing = Vector3.forward;
            if (!final)
            {
                Vector3 dir = route[waypoint] - Centroid; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
            }
            bool arrived = true;
            for (int i = 0; i < soldiers.Count; i++)
            {
                var s = soldiers[i];
                if (!s.IsAlive) continue;
                Vector3 target = anchor + (grid.SlotPosition(Hex, soldierSlot[i]) - center);
                s.Motor.Target = target;
                s.Motor.RestFacing = facing;
                Vector3 off = s.transform.position - target; off.y = 0f;
                if (off.magnitude > ArriveDistance) arrived = false;
            }
            if (IsMoving && final && arrived) IsMoving = false;
        }

        void OnSoldierDied(Health h, TeamColorId by)
        {
            if (IsDestroyed) return;
            int alive = 0;
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) alive++;
            AliveCount = alive;
            if (alive > 0) return;
            if (army != null) army.Grid.Release(Hex, Id);
            IsDestroyed = true;
            IsMoving = false;
            Destroyed?.Invoke(this);
        }

        void OnDestroy()
        {
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null) soldiers[i].Health.Died -= OnSoldierDied;
        }

        // ---------------- army helpers ----------------

        internal void SetCheer(bool on)
        {
            cheering = on;
            for (int i = 0; i < soldiers.Count; i++) if (soldiers[i] != null && soldiers[i].IsAlive) soldiers[i].View.Cheer(on);
        }

        /// <summary>Adds the living soldiers' positions to 'sum'; returns how many were added.</summary>
        internal int AccumulateAlive(ref Vector3 sum)
        {
            int c = 0;
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i] != null && soldiers[i].IsAlive) { sum += soldiers[i].transform.position; c++; }
            return c;
        }

        /// <summary>True once the unit is destroyed and every soldier has vanished.</summary>
        internal bool IsFinished => IsDestroyed && soldiers.Count == 0;
    }
}
