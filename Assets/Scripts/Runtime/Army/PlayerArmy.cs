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
    public enum EntryState { Ready, Deployed, Destroyed }

    /// <summary>One slot of the player's squad list: a unit type waiting to deploy, on the field, or lost.</summary>
    public class SquadEntry
    {
        public UnitTypeId type;
        public EntryState state;
        public PlayerUnit unit;
        /// <summary>The hired mercenary's entry (always after the squad's): never restored by backup.</summary>
        public bool IsMercenary => type == PlayerArmy.MercenaryTypeId;
    }

    /// <summary>
    /// The player's side in unit battles: a squad list of unit types, the hex grid they deploy onto, and the units on
    /// the field. Deploys an entry onto a free hex, routes move orders with hex reservations, tracks destruction
    /// (and restore), and ticks every unit. Input, HUD and camera are wired by other components through this API.
    /// </summary>
    public class PlayerArmy : MonoBehaviour, IPlayerForce
    {
        public static PlayerArmy Instance { get; private set; }

        public UnitRoster roster;
        [Tooltip("Base soldier stats; each unit type scales a runtime copy.")] public UnitConfig soldierConfig;
        [Tooltip("Nav grid cell size and clearance.")] public SquadConfig navConfig;
        [Tooltip("The player's edge of the map, where the camera starts.")] public Vector3 spawnPoint;

        static readonly UnitTypeId[] DefaultSquad = { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank };

        /// <summary>Type id of the hired mercenary's synthetic one soldier type (not a roster type, never saved).</summary>
        public const UnitTypeId MercenaryTypeId = (UnitTypeId)100;
        UnitTypeConfig mercenaryType;
        /// <summary>The hired mercenary's type in this battle, null when none joins.</summary>
        public UnitTypeConfig MercenaryType => mercenaryType;

        readonly List<SquadEntry> squad = new List<SquadEntry>(8);
        readonly List<PlayerUnit> units = new List<PlayerUnit>(8);     // deployed and alive
        readonly List<PlayerUnit> ticking = new List<PlayerUnit>(8);   // alive plus destroyed ones still playing deaths
        readonly List<int> destroyedOrder = new List<int>(8);          // entry indices, oldest first
        readonly Dictionary<UnitTypeId, UnitConfig> scaled = new Dictionary<UnitTypeId, UnitConfig>();
        readonly Dictionary<UnitTypeId, Material> accents = new Dictionary<UnitTypeId, Material>();
        readonly List<UnityEngine.Object> clones = new List<UnityEngine.Object>(8);
        internal readonly List<Vector3> PathScratch = new List<Vector3>(64);

        HexGrid grid;
        NavGrid nav;
        int nextId = 1;
        int startCount;
        int lastReportedAlive = -1;
        float cheerTimer;
        bool cheerHeld;
        Vector3 centroid;

        public IReadOnlyList<SquadEntry> Squad { get { EnsureSquad(); return squad; } }
        public IReadOnlyList<PlayerUnit> Units => units;
        public PlayerUnit Selected { get; private set; }
        public bool InputEnabled { get; set; } = true;

        public event Action SquadChanged;
        public event Action SelectionChanged;
        public event Action<HexCoord> OrderRejected;

        /// <summary>Deployment grid, built on first use from the map bounds and baked against obstacles and structures.</summary>
        public HexGrid Grid { get { EnsureGrid(); return grid; } }
        /// <summary>Coarse obstacle grid used for move routes (built on first use, like the legacy squad's).</summary>
        public NavGrid Nav { get { EnsureGrid(); return nav; } }

        public int StartCount => startCount;
        public int AliveCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < units.Count; i++) c += units[i].AliveCount;
                return c;
            }
        }
        public Vector3 Centroid
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int c = 0;
                for (int i = 0; i < ticking.Count; i++) c += ticking[i].AccumulateAlive(ref sum);
                if (c > 0) centroid = sum / c;
                return centroid;
            }
        }
        public int ReadyCount
        {
            get
            {
                EnsureSquad();
                int c = 0;
                for (int i = 0; i < squad.Count; i++) if (squad[i].state == EntryState.Ready) c++;
                return c;
            }
        }
        public bool CanStillFight => AliveCount > 0 || ReadyCount > 0;

        void Awake() { Instance = this; }

        void Start()
        {
            EnsureSquad();
            BattleEvents.PointCaptured -= OnPointCaptured;
            BattleEvents.PointCaptured += OnPointCaptured;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            BattleEvents.PointCaptured -= OnPointCaptured;
            for (int i = 0; i < clones.Count; i++) if (clones[i] != null) Destroy(clones[i]);
            clones.Clear();
        }

        void EnsureGrid()
        {
            if (nav != null) return;
            EnsureLayout();
            var gc = BattleContext.Config;
            Vector2 min = gc != null ? gc.mapMin : new Vector2(-20f, 0f);
            Vector2 max = gc != null ? gc.mapMax : new Vector2(20f, 110f);
            nav = NavGrid.Build(min - Vector2.one, max + Vector2.one, navConfig != null ? navConfig.navCell : 1f, navConfig != null ? navConfig.navClearance : 0.3f, 1 << Layers.Obstacle);
            grid.BakeBlocked(nav);
            grid.BakeStructures(1 << Layers.Structure);
            OpenCapturePads();
        }

        /// <summary>Creates the grid from the map bounds without baking (its cell layout is all the ground needs).</summary>
        void EnsureLayout()
        {
            if (grid != null) return;
            var gc = BattleContext.Config;
            Vector2 min = gc != null ? gc.mapMin : new Vector2(-20f, 0f);
            Vector2 max = gc != null ? gc.mapMax : new Vector2(20f, 110f);
            var bc = BattleContext.Battle;
            grid = new HexGrid(min, max, bc != null ? bc.hexOuterRadius : BattleConfig.DefaultHexOuterRadius);
        }

        /// <summary>Hexes overlapping a capture pad are never blocked (the thin flag pole would otherwise block the
        /// hexes at the pad center), so units can stand on the pad; soldiers steer around the pole.</summary>
        void OpenCapturePads()
        {
            var bm = BattleManager.Instance;
            if (bm == null || bm.capturePoints == null) return;
            foreach (var c in grid.Cells)
                for (int i = 0; i < bm.capturePoints.Length; i++)
                {
                    var cp = bm.capturePoints[i];
                    if (cp != null && HexOverlapsPad(c, cp.transform.position, cp.Radius)) { grid.SetBlocked(c, false); break; }
                }
        }

        /// <summary>True when the hex's center lies within the pad radius plus half the hex outer radius of the pad
        /// center: the hex overlaps the pad.</summary>
        public bool HexOverlapsPad(HexCoord hex, Vector3 padCenter, float padRadius)
        {
            EnsureLayout();
            Vector3 d = grid.Center(hex) - padCenter;
            float reach = padRadius + grid.OuterRadius * 0.5f;
            return d.x * d.x + d.z * d.z <= reach * reach;
        }

        /// <summary>Extra reach for IsPadHex's slot test (world units).</summary>
        public const float PadSlotSlack = 0.3f;

        /// <summary>THE pad hex rule, used for counting (UnitCountsOn), the overlay tint and the tutorial's capture
        /// suggestion: an open hex that overlaps the pad (center within the radius plus half a hex) or that has a
        /// soldier slot inside the ring as drawn (plus PadSlotSlack, so a soldier resting a little off its slot on an
        /// untinted hex never pokes into the ring). A unit resting on a pad hex counts in full.</summary>
        public bool IsPadHex(HexCoord hex, CapturePoint cp)
        {
            if (cp == null) return false;
            var g = Grid;
            if (!g.InBounds(hex) || g.IsBlocked(hex)) return false;
            Vector3 c = cp.transform.position;
            if (HexOverlapsPad(hex, c, cp.Radius)) return true;
            float r = cp.DrawnRadius + PadSlotSlack;
            float r2 = r * r;
            for (int slot = 0; slot < 3; slot++)
            {
                Vector3 d = g.SlotPosition(hex, slot) - c;
                if (d.x * d.x + d.z * d.z <= r2) return true;
            }
            return false;
        }

        /// <summary>True when every living soldier of the unit counts on the pad: its hex is a pad hex and it rests
        /// there (or walks onto it with a soldier within the radius plus one hex), or any of its living soldiers
        /// stands inside the drawn ring.</summary>
        public bool UnitCountsOn(PlayerUnit u, CapturePoint cp)
        {
            if (u == null || cp == null || u.AliveCount == 0) return false;
            Vector3 c = cp.transform.position;
            if (AnySoldierWithin(u, c, cp.DrawnRadius)) return true;
            if (!IsPadHex(u.Hex, cp)) return false;
            return !u.IsMoving || AnySoldierWithin(u, c, cp.Radius + HexOuterRadius);
        }

        /// <summary>Living soldiers of every unit that counts on the pad; 'weight' sums their capture weights (the
        /// unit type's captureWeight per soldier).</summary>
        public int PadCount(CapturePoint cp, out int weight)
        {
            int n = 0;
            weight = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!UnitCountsOn(u, cp)) continue;
                n += u.AliveCount;
                weight += u.AliveCount * Mathf.Max(1, u.Type != null ? u.Type.captureWeight : 1);
            }
            return n;
        }

        static bool AnySoldierWithin(PlayerUnit u, Vector3 p, float radius)
        {
            var list = u.Soldiers;
            for (int k = 0; k < list.Count; k++)
            {
                var s = list[k];
                if (s == null || !s.IsAlive) continue;
                Vector3 d = s.transform.position - p;
                if (d.x * d.x + d.z * d.z <= radius * radius) return true;
            }
            return false;
        }

        /// <summary>World center of hex (0, 0) and the hex outer radius, without baking the grid.</summary>
        public Vector3 GridOrigin { get { EnsureLayout(); return grid.Center(new HexCoord(0, 0)); } }
        public float HexOuterRadius { get { EnsureLayout(); return grid.OuterRadius; } }

        void EnsureSquad()
        {
            if (squad.Count == 0) Fill(DefaultSquad);
        }

        /// <summary>The squad entries for 'types', then the mercenary's entry when one is hired.</summary>
        void Fill(IList<UnitTypeId> types)
        {
            squad.Clear();
            for (int i = 0; i < types.Count; i++)
                if (types[i] != MercenaryTypeId) squad.Add(new SquadEntry { type = types[i], state = EntryState.Ready });
            if (mercenaryType != null) squad.Add(new SquadEntry { type = MercenaryTypeId, state = EntryState.Ready });
        }

        /// <summary>Sets the squad list (a hired mercenary stays after it). Meant for before or at Start; ignored once
        /// a unit has deployed.</summary>
        public void SetSquad(IList<UnitTypeId> types)
        {
            if (types == null || ticking.Count > 0) return;
            Fill(types);
            destroyedOrder.Clear();
            SquadChanged?.Invoke();
        }

        /// <summary>Adds the hired mercenary as one extra entry after the squad (null removes it). The type is a one
        /// soldier unit; it counts for CanStillFight and can be healed, but backup never restores it and a Healer
        /// never revives it. Ignored once a unit has deployed.</summary>
        public void SetMercenary(UnitTypeConfig type)
        {
            if (ticking.Count > 0) return;
            EnsureSquad();
            var types = new List<UnitTypeId>(squad.Count);
            for (int i = 0; i < squad.Count; i++) if (!squad[i].IsMercenary) types.Add(squad[i].type);
            mercenaryType = type;
            if (type != null) type.id = MercenaryTypeId;
            Fill(types);
            destroyedOrder.Clear();
            SquadChanged?.Invoke();
        }

        /// <summary>The type config of a squad entry's type: a roster type, or the hired mercenary's.</summary>
        public UnitTypeConfig TypeConfig(UnitTypeId id)
        {
            if (id == MercenaryTypeId) return mercenaryType;
            return roster != null ? roster.Get(id) : null;
        }

        /// <summary>True when the entry is ready, the hex is free and no enemy soldier stands on it.</summary>
        public bool CanDeploy(int entryIndex, HexCoord hex)
        {
            EnsureSquad();
            if (entryIndex < 0 || entryIndex >= squad.Count || squad[entryIndex].state != EntryState.Ready) return false;
            if (TypeConfig(squad[entryIndex].type) == null || soldierConfig == null) return false;
            var g = Grid;
            if (!g.IsFree(hex)) return false;
            return UnitRegistry.CountWithin(Team.Enemy, g.Center(hex), g.OuterRadius * 0.9f) == 0;
        }

        /// <summary>Spawns the entry's unit on the hex. Null when not allowed.</summary>
        public PlayerUnit Deploy(int entryIndex, HexCoord hex)
        {
            if (!CanDeploy(entryIndex, hex)) return null;
            var entry = squad[entryIndex];
            var type = TypeConfig(entry.type);
            int id = nextId;
            if (!grid.TryReserve(hex, id)) return null;
            nextId++;
            var go = new GameObject("Unit_" + id + "_" + type.id);
            go.transform.SetParent(transform, false);
            var unit = go.AddComponent<PlayerUnit>();
            if (!unit.Init(this, type, ConfigFor(type), hex, id))
            {
                // No soldiers could spawn: give the hex back and report the deploy as refused.
                grid.Release(hex, id);
                Destroy(go);
                return null;
            }
            unit.Destroyed += OnUnitDestroyed;
            startCount += unit.StartCount;
            units.Add(unit);
            ticking.Add(unit);
            entry.state = EntryState.Deployed;
            entry.unit = unit;
            if (cheerHeld) unit.SetCheer(true);
            SquadChanged?.Invoke();
            return unit;
        }

        /// <summary>Orders a unit to a hex. Raises OrderRejected and returns false when the move is refused.</summary>
        public bool OrderMove(PlayerUnit unit, HexCoord dest)
        {
            if (unit != null && units.Contains(unit) && unit.MoveTo(dest)) return true;
            OrderRejected?.Invoke(dest);
            return false;
        }

        /// <summary>Raises OrderRejected for a refused command that is not a move (for example a deploy on a taken hex).</summary>
        public void NotifyRejected(HexCoord hex) { OrderRejected?.Invoke(hex); }

        /// <summary>Selects a unit (null clears the selection).</summary>
        public void Select(PlayerUnit unit)
        {
            if (unit != null && !units.Contains(unit)) unit = null;
            if (unit == Selected) return;
            if (Selected != null) Selected.Selected = false;
            Selected = unit;
            if (unit != null) unit.Selected = true;
            SelectionChanged?.Invoke();
        }

        /// <summary>The unit holding the hex (standing on it or heading to it), else one whose soldiers stand on it.</summary>
        public PlayerUnit UnitAt(HexCoord hex)
        {
            int owner = Grid.OwnerOf(hex);
            for (int i = 0; i < units.Count; i++) if (owner != 0 && units[i].Id == owner) return units[i];
            for (int i = 0; i < units.Count; i++)
                if (units[i].AliveCount > 0 && grid.TryWorldToHex(units[i].Centroid, out HexCoord c) && c == hex) return units[i];
            return null;
        }

        /// <summary>Puts the most recently destroyed squad entry back to Ready (never the mercenary). False when none is
        /// destroyed.</summary>
        public bool RestoreLastDestroyed()
        {
            for (int k = destroyedOrder.Count - 1; k >= 0; k--)
            {
                int i = destroyedOrder[k];
                destroyedOrder.RemoveAt(k);
                if (i < 0 || i >= squad.Count || squad[i].state != EntryState.Destroyed) continue;
                squad[i].state = EntryState.Ready;
                squad[i].unit = null;
                SquadChanged?.Invoke();
                return true;
            }
            return false;
        }

        void OnUnitDestroyed(PlayerUnit unit)
        {
            unit.Destroyed -= OnUnitDestroyed;
            units.Remove(unit);
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i].unit != unit) continue;
                squad[i].state = EntryState.Destroyed;
                if (!squad[i].IsMercenary) destroyedOrder.Add(i); // backup never restores the mercenary
            }
            if (Selected == unit) Select(null);
            SquadChanged?.Invoke();
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const float LobArcHeight = 3f;

        /// <summary>Shared runtime copy of the soldiers' Accent material in the type's accent color (one per type, so
        /// the SRP batcher still batches); null when the soldier has no Accent slot.</summary>
        internal Material AccentFor(UnitTypeConfig type, Material baseAccent)
        {
            if (type == null || baseAccent == null) return null;
            if (accents.TryGetValue(type.id, out var m) && m != null) return m;
            m = new Material(baseAccent) { name = baseAccent.name + "_" + type.id };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, type.accent);
            clones.Add(m);
            accents[type.id] = m;
            return m;
        }

        /// <summary>Runtime copy of soldierConfig (and its weapon) scaled by the type; one per type, never an asset.</summary>
        UnitConfig ConfigFor(UnitTypeConfig type)
        {
            if (scaled.TryGetValue(type.id, out var u) && u != null) return u;
            var src = soldierConfig;
            u = Instantiate(src);
            u.name = src.name + "_" + type.id;
            u.maxHealth = src.maxHealth * type.healthMult;
            u.maxSpeed = src.maxSpeed * type.speedMult;
            clones.Add(u);
            if (src.weapon != null)
            {
                var w = Instantiate(src.weapon);
                w.name = src.weapon.name + "_" + type.id;
                w.range = src.weapon.range * type.rangeMult;
                w.damage = src.weapon.damage * type.damageMult;
                w.fireInterval = src.weapon.fireInterval * type.fireIntervalMult;
                // Longer reach keeps the same miss distance at the far end; bigger hits look bigger.
                if (type.rangeMult > 1f) w.spreadDegrees = src.weapon.spreadDegrees / type.rangeMult;
                w.projectileScale = src.weapon.projectileScale * Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, type.damageMult)), 0.8f, 1.6f);
                if (type.lobbed)
                {
                    // The cannon's lob (ProjectileManager arcs any weapon with arcHeight and splashes with
                    // splashRadius): one shell per volley carrying the volley's damage.
                    w.damage *= Mathf.Max(1, src.weapon.burstCount);
                    w.burstCount = 1;
                    w.arcHeight = LobArcHeight;
                    w.splashRadius = type.splashRadius;
                    w.structureDamageMult = type.structureDamageMult;
                    w.soldierDamageMult = type.soldierDamageMult;
                    w.projectileScale = src.weapon.projectileScale * 1.6f;
                    w.hitSplatChance = 0f;
                    w.missSplatChance = 0f;
                    w.splatSize = 0.9f;
                    w.impactShake = 0.08f;
                    w.heavy = true;
                    w.fireSound = TactileWars.Audio.SoundId.CannonShot;
                }
                u.weapon = w;
                clones.Add(w);
            }
            scaled[type.id] = u;
            return u;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = ticking.Count - 1; i >= 0; i--)
            {
                var u = ticking[i];
                if (u == null) { ticking.RemoveAt(i); continue; }
                u.Tick(dt);
                if (u.IsFinished)
                {
                    ticking.RemoveAt(i);
                    Destroy(u.gameObject);
                }
            }
            int alive = AliveCount;
            if (alive != lastReportedAlive)
            {
                lastReportedAlive = alive;
                BattleEvents.RaiseSoldierCountChanged(alive, Mathf.Max(startCount, alive));
            }
            if (cheerTimer > 0f)
            {
                cheerTimer -= dt;
                if (cheerTimer <= 0f && !cheerHeld) for (int i = 0; i < units.Count; i++) units[i].SetCheer(false);
            }
        }

        void OnPointCaptured(int index) { CheerFor(1.6f); }

        public void SetCheer(bool on)
        {
            cheerHeld = on;
            cheerTimer = 0f;
            for (int i = 0; i < units.Count; i++) units[i].SetCheer(on);
        }

        public void CheerFor(float seconds)
        {
            if (cheerHeld) return;
            cheerTimer = seconds;
            for (int i = 0; i < units.Count; i++) units[i].SetCheer(true);
        }
    }
}
