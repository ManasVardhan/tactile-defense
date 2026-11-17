using System;
using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Meta
{
    public enum PlacementError
    {
        None, Unknown, Locked, OutsideZone, AttackerZone, CapturePad, OverlapsProp, OverlapsDefense,
        BudgetCap, SpaceCap, TypeLimit, NotEnoughGold
    }

    /// <summary>
    /// Working copy of the player's base while the editor is open. Nothing touches the profile until Commit.
    /// Rules (concepts verified, numbers ESTIMATES): inside the placeable zone, outside the attacker start zone,
    /// clear of capture pads, props and other defenses; total budget points within the level cap; total space
    /// within the space cap; per type count limits; research unlock.
    /// Economy (our documented rule): placing uses a stocked copy first, otherwise buys one for its gold cost;
    /// removing returns the defense to stock (no gold refund, it can be placed again for free); consumed defenses
    /// stay as wrecks that can be rebought for their gold cost at the same spot. Gold is paid on Save.
    /// </summary>
    public class BaseEditSession
    {
        public readonly List<DefensePlacement> Placements = new List<DefensePlacement>();
        public readonly List<DefensePlacement> Destroyed = new List<DefensePlacement>();
        readonly Dictionary<string, int> stock = new Dictionary<string, int>();
        readonly DefenseCatalog catalog;
        readonly MapLayoutData map;
        readonly Func<string, int> researchLevel;
        readonly int startGold;

        public int BudgetCap { get; private set; }
        public int SpaceCap { get; private set; }
        public float GridCell { get; private set; } = 1f;
        public int PendingGold { get; private set; }
        public int GoldAvailable => startGold - PendingGold;
        public bool Dirty { get; private set; }
        public DefenseCatalog Catalog => catalog;
        public MapLayoutData Map => map;

        public BaseEditSession(BaseLayoutSave save, int gold, DefenseCatalog catalog, MapLayoutData map, int budgetCap, int spaceCap,
            Func<string, int> researchLevel, float gridCell = 1f)
        {
            this.catalog = catalog;
            this.map = map;
            this.researchLevel = researchLevel;
            startGold = gold;
            BudgetCap = budgetCap;
            SpaceCap = spaceCap;
            GridCell = gridCell;
            if (save != null)
            {
                if (save.placements != null) Placements.AddRange(save.placements);
                if (save.destroyed != null) Destroyed.AddRange(save.destroyed);
                if (save.stock != null) foreach (var s in save.stock) if (s != null && s.count > 0) stock[s.id] = s.count;
            }
        }

        // ------------------------------------------------------------ queries

        public bool TryEntry(string id, out DefenseCatalog.Entry e)
        {
            e = default;
            return catalog != null && catalog.TryGet(id, out e);
        }

        public int CountOf(string id)
        {
            int n = 0;
            for (int i = 0; i < Placements.Count; i++) if (Placements[i].defenseId == id) n++;
            return n;
        }

        public int StockOf(string id) => stock.TryGetValue(id, out int n) ? n : 0;

        public int BudgetUsed
        {
            get
            {
                int b = 0;
                for (int i = 0; i < Placements.Count; i++) if (TryEntry(Placements[i].defenseId, out var e)) b += e.budget;
                return b;
            }
        }

        public int SpaceUsed
        {
            get
            {
                int b = 0;
                for (int i = 0; i < Placements.Count; i++) if (TryEntry(Placements[i].defenseId, out var e)) b += e.space;
                return b;
            }
        }

        public bool IsUnlocked(string id)
        {
            if (!TryEntry(id, out var e)) return false;
            return string.IsNullOrEmpty(e.researchId) || (researchLevel != null && researchLevel(e.researchId) > 0);
        }

        /// <summary>Gold this placement would cost now (0 when a stocked copy is available).</summary>
        public int CostToPlace(string id) => StockOf(id) > 0 ? 0 : (TryEntry(id, out var e) ? e.goldCost : 0);

        public Vector3 Snap(Vector3 p)
        {
            float c = Mathf.Max(0.1f, GridCell);
            p.x = (Mathf.Floor(p.x / c) + 0.5f) * c;
            p.z = (Mathf.Floor(p.z / c) + 0.5f) * c;
            p.y = 0f;
            return p;
        }

        /// <summary>Index of the placement whose footprint contains 'p', or -1.</summary>
        public int PickAt(Vector3 p, float extra = 0.3f)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Placements.Count; i++)
            {
                var d = Placements[i];
                float r = (TryEntry(d.defenseId, out var e) ? e.footprint : 1f) + extra;
                float dx = d.x - p.x, dz = d.z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 <= r * r && d2 < bestD) { bestD = d2; best = i; }
            }
            return best;
        }

        public int PickWreckAt(Vector3 p, float extra = 0.3f)
        {
            for (int i = 0; i < Destroyed.Count; i++)
            {
                var d = Destroyed[i];
                float r = (TryEntry(d.defenseId, out var e) ? e.footprint : 1f) + extra;
                float dx = d.x - p.x, dz = d.z - p.z;
                if (dx * dx + dz * dz <= r * r) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------ rules

        /// <summary>Validates placing 'id' at 'pos'. 'ignore' is a placement index to skip (moving it).
        /// 'adding' false checks position only (moves do not change budget, space or counts).</summary>
        public PlacementError Check(string id, Vector3 pos, int ignore = -1, bool adding = true, bool payGold = true)
        {
            if (!TryEntry(id, out var e)) return PlacementError.Unknown;
            if (adding && !IsUnlocked(id)) return PlacementError.Locked;
            var pe = CheckPosition(e, pos, ignore);
            if (pe != PlacementError.None) return pe;
            if (adding)
            {
                if (e.maxCount > 0 && CountOf(id) >= e.maxCount) return PlacementError.TypeLimit;
                if (BudgetUsed + e.budget > BudgetCap) return PlacementError.BudgetCap;
                if (SpaceUsed + e.space > SpaceCap) return PlacementError.SpaceCap;
                if (payGold && CostToPlace(id) > GoldAvailable) return PlacementError.NotEnoughGold;
            }
            return PlacementError.None;
        }

        PlacementError CheckPosition(DefenseCatalog.Entry e, Vector3 pos, int ignore)
        {
            float r = e.footprint;
            if (map != null)
            {
                var z = map.placeable;
                if (pos.x - r < z.x || pos.x + r > z.z || pos.z + r > z.w) return PlacementError.OutsideZone;
                if (pos.z - r < map.attackerZoneMaxZ) return PlacementError.AttackerZone;
                for (int i = 0; i < map.capturePoints.Length; i++)
                {
                    Vector3 c = map.capturePoints[i];
                    float dx = c.x - pos.x, dz = c.z - pos.z;
                    float rr = map.padClearance + r;
                    if (dx * dx + dz * dz < rr * rr) return PlacementError.CapturePad;
                }
                for (int i = 0; i < map.props.Length; i++)
                {
                    var p = map.props[i];
                    if (p.radius <= 0f) continue;
                    float dx = p.position.x - pos.x, dz = p.position.z - pos.z;
                    float rr = p.radius + r;
                    if (dx * dx + dz * dz < rr * rr) return PlacementError.OverlapsProp;
                }
            }
            for (int i = 0; i < Placements.Count; i++)
            {
                if (i == ignore) continue;
                var d = Placements[i];
                float or = TryEntry(d.defenseId, out var oe) ? oe.footprint : 1f;
                float dx = d.x - pos.x, dz = d.z - pos.z;
                float rr = or + r;
                if (dx * dx + dz * dz < rr * rr) return PlacementError.OverlapsDefense;
            }
            return PlacementError.None;
        }

        // ------------------------------------------------------------ edits

        public PlacementError Place(string id, Vector3 pos, float yaw)
        {
            pos = Snap(pos);
            var err = Check(id, pos);
            if (err != PlacementError.None) return err;
            if (StockOf(id) > 0) stock[id] = StockOf(id) - 1;
            else PendingGold += TryEntry(id, out var e) ? e.goldCost : 0;
            Placements.Add(new DefensePlacement { defenseId = id, x = pos.x, z = pos.z, rotation = yaw });
            Dirty = true;
            return PlacementError.None;
        }

        public PlacementError Move(int index, Vector3 pos)
        {
            if (index < 0 || index >= Placements.Count) return PlacementError.Unknown;
            pos = Snap(pos);
            var d = Placements[index];
            var err = Check(d.defenseId, pos, index, false);
            if (err != PlacementError.None) return err;
            d.x = pos.x; d.z = pos.z;
            Placements[index] = d;
            Dirty = true;
            return PlacementError.None;
        }

        public void Rotate(int index, float step)
        {
            if (index < 0 || index >= Placements.Count) return;
            var d = Placements[index];
            d.rotation = Mathf.Repeat(d.rotation + step, 360f);
            Placements[index] = d;
            Dirty = true;
        }

        /// <summary>Removes a placed defense back into stock (no gold refund; re-placing it is free).</summary>
        public void Remove(int index)
        {
            if (index < 0 || index >= Placements.Count) return;
            string id = Placements[index].defenseId;
            Placements.RemoveAt(index);
            stock[id] = StockOf(id) + 1;
            Dirty = true;
        }

        public int RebuyCost(int wreck) => wreck >= 0 && wreck < Destroyed.Count && TryEntry(Destroyed[wreck].defenseId, out var e) ? e.goldCost : 0;

        public int RebuyAllCost()
        {
            int c = 0;
            for (int i = 0; i < Destroyed.Count; i++) c += RebuyCost(i);
            return c;
        }

        /// <summary>Rebuys a consumed defense at its old spot for its gold cost (stock is used first).</summary>
        public PlacementError Rebuy(int wreck)
        {
            if (wreck < 0 || wreck >= Destroyed.Count) return PlacementError.Unknown;
            var d = Destroyed[wreck];
            var pos = new Vector3(d.x, 0f, d.z);
            var err = Check(d.defenseId, pos);
            if (err != PlacementError.None) return err;
            if (StockOf(d.defenseId) > 0) stock[d.defenseId] = StockOf(d.defenseId) - 1;
            else PendingGold += RebuyCost(wreck);
            Destroyed.RemoveAt(wreck);
            Placements.Add(d);
            Dirty = true;
            return PlacementError.None;
        }

        /// <summary>Rebuys every wreck that still fits. Returns how many were rebought.</summary>
        public int RebuyAll(out PlacementError firstError)
        {
            firstError = PlacementError.None;
            int n = 0;
            for (int i = Destroyed.Count - 1; i >= 0; i--)
            {
                var err = Rebuy(i);
                if (err == PlacementError.None) n++;
                else if (firstError == PlacementError.None) firstError = err;
            }
            return n;
        }

        public void DismissWreck(int wreck)
        {
            if (wreck < 0 || wreck >= Destroyed.Count) return;
            Destroyed.RemoveAt(wreck);
            Dirty = true;
        }

        /// <summary>Writes the working copy into a save object (gold is handled by the caller).</summary>
        public void WriteTo(BaseLayoutSave save)
        {
            save.placements ??= new List<DefensePlacement>();
            save.destroyed ??= new List<DefensePlacement>();
            save.placements.Clear();
            save.placements.AddRange(Placements);
            save.destroyed.Clear();
            save.destroyed.AddRange(Destroyed);
            save.stock ??= new List<StockEntry>();
            save.stock.Clear();
            foreach (var kv in stock) if (kv.Value > 0) save.stock.Add(new StockEntry { id = kv.Key, count = kv.Value });
            save.stock.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        }

        public void MarkSaved() { Dirty = false; }

        public static string Describe(PlacementError e) => e switch
        {
            PlacementError.Locked => "Locked: research it in the Defense branch",
            PlacementError.OutsideZone => "Outside your base",
            PlacementError.AttackerZone => "Attacker start zone: keep it clear",
            PlacementError.CapturePad => "Too close to a capture point",
            PlacementError.OverlapsProp => "Blocked by scenery",
            PlacementError.OverlapsDefense => "Overlaps another defense",
            PlacementError.BudgetCap => "Too heavy: budget cap reached",
            PlacementError.SpaceCap => "No space left: space cap reached",
            PlacementError.TypeLimit => "Limit reached for this defense",
            PlacementError.NotEnoughGold => "Not enough gold",
            PlacementError.Unknown => "Unknown defense",
            _ => "",
        };
    }
}
