using System.Collections.Generic;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Army
{
    /// <summary>
    /// Pointy-top hex grid over a world rectangle (x, z) for unit deployment. Coordinates are axial (q, r); storage
    /// is flat arrays indexed by odd-r offset coordinates (col, row), row 0 along min.y and col 0 along min.x. Each
    /// hex tracks a blocked flag and an owner id (0 = none) so one unit holds one hex. Lookups do not allocate.
    /// </summary>
    public class HexGrid
    {
        static readonly float Sqrt3 = Mathf.Sqrt(3f);

        // Axial neighbor offsets.
        static readonly int[] DirQ = { 1, 1, 0, -1, -1, 0 };
        static readonly int[] DirR = { 0, -1, -1, 0, 1, 1 };

        public readonly int Cols, Rows;
        public float OuterRadius { get; }

        readonly Vector2 origin; // world (x, z) of the center of offset cell (0, 0)
        readonly bool[] blocked;
        readonly int[] owner;

        public HexGrid(Vector2 min, Vector2 max, float outerRadius)
        {
            OuterRadius = Mathf.Max(0.05f, outerRadius);
            float w = Sqrt3 * OuterRadius;
            origin = new Vector2(min.x + w * 0.5f, min.y + OuterRadius);
            // Columns: even rows fit fully; odd rows overhang max.x by half a hex at most.
            Cols = Mathf.Max(1, Mathf.FloorToInt((max.x - min.x) / w));
            Rows = Mathf.Max(1, Mathf.FloorToInt((max.y - min.y - 2f * OuterRadius) / (1.5f * OuterRadius)) + 1);
            blocked = new bool[Cols * Rows];
            owner = new int[Cols * Rows];
        }

        static int ToCol(HexCoord c) => c.q + ((c.r - (c.r & 1)) >> 1);
        static HexCoord FromOffset(int col, int row) => new HexCoord(col - ((row - (row & 1)) >> 1), row);

        bool Index(HexCoord c, out int i)
        {
            int col = ToCol(c), row = c.r;
            bool inside = col >= 0 && row >= 0 && col < Cols && row < Rows;
            i = inside ? row * Cols + col : -1;
            return inside;
        }

        public bool InBounds(HexCoord c) => Index(c, out _);

        /// <summary>World center of the hex at ground level (y = 0).</summary>
        public Vector3 Center(HexCoord c)
        {
            float x = origin.x + Sqrt3 * OuterRadius * (c.q + c.r * 0.5f);
            float z = origin.y + 1.5f * OuterRadius * c.r;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Hex containing the world point (cube rounding). False when that hex is outside the grid.</summary>
        public bool TryWorldToHex(Vector3 world, out HexCoord c)
        {
            float px = world.x - origin.x, pz = world.z - origin.y;
            float fq = (Sqrt3 / 3f * px - pz / 3f) / OuterRadius;
            float fr = (2f / 3f * pz) / OuterRadius;
            float fs = -fq - fr;
            int rq = Mathf.RoundToInt(fq), rr = Mathf.RoundToInt(fr), rs = Mathf.RoundToInt(fs);
            float dq = Mathf.Abs(rq - fq), dr = Mathf.Abs(rr - fr), ds = Mathf.Abs(rs - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            c = new HexCoord(rq, rr);
            return InBounds(c);
        }

        /// <summary>Clears 'into' and fills it with the in-bounds neighbors of c.</summary>
        public void Neighbors(HexCoord c, List<HexCoord> into)
        {
            into.Clear();
            for (int d = 0; d < 6; d++)
            {
                var n = new HexCoord(c.q + DirQ[d], c.r + DirR[d]);
                if (InBounds(n)) into.Add(n);
            }
        }

        /// <summary>Hex steps between a and b.</summary>
        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = a.q - b.q, dr = a.r - b.r;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
        }

        /// <summary>Every in-bounds hex, row by row from min.y, starting at the min corner.</summary>
        public IEnumerable<HexCoord> Cells
        {
            get
            {
                for (int row = 0; row < Rows; row++)
                    for (int col = 0; col < Cols; col++)
                        yield return FromOffset(col, row);
            }
        }

        /// <summary>True for blocked hexes; hexes outside the grid count as blocked.</summary>
        public bool IsBlocked(HexCoord c) => !Index(c, out int i) || blocked[i];

        public void SetBlocked(HexCoord c, bool value)
        {
            if (Index(c, out int i)) blocked[i] = value;
        }

        /// <summary>Blocks hexes whose center, or any of the 6 corners pulled 30 percent toward the center, lies on a
        /// blocked nav cell. Only adds blocks; never clears existing ones.</summary>
        public void BakeBlocked(NavGrid nav)
        {
            float d = OuterRadius * 0.7f;
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Cols; col++)
                {
                    int i = row * Cols + col;
                    if (blocked[i]) continue;
                    Vector3 p = Center(FromOffset(col, row));
                    bool b = nav.IsBlocked(p);
                    for (int k = 0; k < 6 && !b; k++)
                    {
                        float a = (60f * k - 30f) * Mathf.Deg2Rad; // pointy-top corners at -30, 30, 90, ...
                        b = nav.IsBlocked(p + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d));
                    }
                    blocked[i] = b;
                }
        }

        /// <summary>Also blocks hexes whose center is within OuterRadius * 0.6 of a non-trigger collider on
        /// 'layerMask'. The probe sits at y = 0.5 so colliders resting on the ground are found.</summary>
        public void BakeStructures(int layerMask)
        {
            Physics.SyncTransforms();
            float radius = OuterRadius * 0.6f;
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Cols; col++)
                {
                    int i = row * Cols + col;
                    if (blocked[i]) continue;
                    Vector3 p = Center(FromOffset(col, row)); p.y = 0.5f;
                    if (Physics.OverlapSphere(p, radius, layerMask, QueryTriggerInteraction.Ignore).Length > 0)
                        blocked[i] = true;
                }
        }

        /// <summary>Claims c for ownerId. Fails when c is out of bounds, blocked or held by another owner; succeeds
        /// when ownerId already holds it.</summary>
        public bool TryReserve(HexCoord c, int ownerId)
        {
            if (!Index(c, out int i) || blocked[i]) return false;
            if (owner[i] != 0 && owner[i] != ownerId) return false;
            owner[i] = ownerId;
            return true;
        }

        /// <summary>Frees c when ownerId holds it; no effect otherwise.</summary>
        public void Release(HexCoord c, int ownerId)
        {
            if (Index(c, out int i) && owner[i] == ownerId) owner[i] = 0;
        }

        /// <summary>Owner id holding c, 0 when none or out of bounds.</summary>
        public int OwnerOf(HexCoord c) => Index(c, out int i) ? owner[i] : 0;

        public bool IsFree(HexCoord c) => Index(c, out int i) && !blocked[i] && owner[i] == 0;

        /// <summary>Distance of the soldier slots from the hex center, over OuterRadius.</summary>
        public const float SlotFraction = 0.5f;

        /// <summary>Standing spot for soldier 'slot' (0 to 2): an upright triangle around the center, apex toward
        /// +z, at OuterRadius * SlotFraction.</summary>
        public Vector3 SlotPosition(HexCoord c, int slot)
        {
            float a = (90f + 120f * slot) * Mathf.Deg2Rad;
            float d = OuterRadius * SlotFraction;
            return Center(c) + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }
    }
}
