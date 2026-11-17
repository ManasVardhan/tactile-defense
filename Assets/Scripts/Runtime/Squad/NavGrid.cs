using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Squad
{
    /// <summary>
    /// Coarse obstacle grid (1 unit cells by default) over the map with an allocation free A* (8 neighbours, no
    /// corner cutting) and line of sight string pulling. Used to route the squad anchor around long walls through
    /// gates; per soldier steering still pushes and slides locally. Built once from the Obstacle layer (props,
    /// walls, fences; destructible defenses are not baked in). All arrays are preallocated, so searches do not
    /// allocate.
    /// </summary>
    public class NavGrid
    {
        public readonly int W, H;
        public readonly float Cell;
        public readonly Vector2 Origin;
        readonly bool[] blocked;

        // A* scratch (preallocated, reused).
        readonly float[] g;
        readonly int[] parent;
        readonly int[] stamp;
        readonly bool[] closed;
        readonly int[] heap;
        readonly int[] heapPos;
        readonly float[] f;
        int heapCount;
        int search;
        readonly List<int> cellPath;

        public int BlockedCount { get; private set; }

        public NavGrid(Vector2 min, Vector2 max, float cell)
        {
            Cell = Mathf.Max(0.25f, cell);
            Origin = min;
            W = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / Cell));
            H = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / Cell));
            int n = W * H;
            blocked = new bool[n];
            g = new float[n];
            f = new float[n];
            parent = new int[n];
            stamp = new int[n];
            closed = new bool[n];
            heap = new int[n];
            heapPos = new int[n];
            cellPath = new List<int>(512);
        }

        /// <summary>Marks cells whose box (grown by 'clearance') overlaps colliders on 'mask'.</summary>
        public static NavGrid Build(Vector2 min, Vector2 max, float cell, float clearance, int mask)
        {
            var grid = new NavGrid(min, max, cell);
            Physics.SyncTransforms();
            var half = new Vector3(grid.Cell * 0.5f + clearance, 0.45f, grid.Cell * 0.5f + clearance);
            int c = 0;
            for (int y = 0; y < grid.H; y++)
                for (int x = 0; x < grid.W; x++)
                {
                    Vector3 p = grid.CellCenter(x, y); p.y = 0.5f;
                    bool b = Physics.CheckBox(p, half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                    grid.blocked[y * grid.W + x] = b;
                    if (b) c++;
                }
            grid.BlockedCount = c;
            return grid;
        }

        public void SetBlocked(int x, int y, bool b)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            if (blocked[i] != b) BlockedCount += b ? 1 : -1;
            blocked[i] = b;
        }

        /// <summary>Blocks every cell whose center lies inside the world rectangle (tests and hand made grids).</summary>
        public void BlockRect(Vector2 a, Vector2 b)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector3 c = CellCenter(x, y);
                    if (c.x >= Mathf.Min(a.x, b.x) && c.x <= Mathf.Max(a.x, b.x) && c.z >= Mathf.Min(a.y, b.y) && c.z <= Mathf.Max(a.y, b.y))
                        SetBlocked(x, y, true);
                }
        }

        public Vector3 CellCenter(int x, int y) => new Vector3(Origin.x + (x + 0.5f) * Cell, 0f, Origin.y + (y + 0.5f) * Cell);

        public bool ToCell(Vector3 p, out int x, out int y)
        {
            x = Mathf.FloorToInt((p.x - Origin.x) / Cell);
            y = Mathf.FloorToInt((p.z - Origin.y) / Cell);
            bool inside = x >= 0 && y >= 0 && x < W && y < H;
            x = Mathf.Clamp(x, 0, W - 1);
            y = Mathf.Clamp(y, 0, H - 1);
            return inside;
        }

        public bool IsBlocked(int x, int y) => x < 0 || y < 0 || x >= W || y >= H || blocked[y * W + x];

        public bool IsBlocked(Vector3 p)
        {
            ToCell(p, out int x, out int y);
            return blocked[y * W + x];
        }

        /// <summary>True when the straight segment crosses no blocked cell (sampled every 0.4 cell). Samples closer
        /// than 'skipStart' to 'a' are ignored (a unit pressed against a wall stands in a dilated cell).</summary>
        public bool LineClear(Vector3 a, Vector3 b, float skipStart = 0f)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.4f)));
            for (int i = 0; i <= steps; i++)
            {
                if (i * len / steps < skipStart) continue;
                Vector3 p = a + d * (i / (float)steps);
                ToCell(p, out int x, out int y);
                if (blocked[y * W + x]) return false;
            }
            return true;
        }

        /// <summary>Nearest free cell to p (spiral search), so starts or goals inside props still route.</summary>
        bool NearestFree(int x, int y, out int fx, out int fy)
        {
            fx = x; fy = y;
            if (!IsBlocked(x, y)) return true;
            for (int r = 1; r < 8; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        if (!IsBlocked(x + dx, y + dy)) { fx = x + dx; fy = y + dy; return true; }
                    }
            return false;
        }

        /// <summary>A* from 'from' to 'to'. Fills 'path' with smoothed world waypoints (start excluded, goal
        /// included). Returns false when no route exists. Does not allocate.</summary>
        public bool FindPath(Vector3 from, Vector3 to, List<Vector3> path, int maxExpansions = 20000)
        {
            path.Clear();
            ToCell(from, out int sx, out int sy);
            ToCell(to, out int tx, out int ty);
            if (!NearestFree(sx, sy, out sx, out sy) || !NearestFree(tx, ty, out tx, out ty)) return false;
            int start = sy * W + sx, goal = ty * W + tx;
            search++;
            if (search == int.MaxValue) { search = 1; System.Array.Clear(stamp, 0, stamp.Length); }
            heapCount = 0;
            Touch(start);
            g[start] = 0f;
            parent[start] = -1;
            Push(start, Heuristic(sx, sy, tx, ty));
            int expansions = 0;
            bool found = false;
            while (heapCount > 0 && expansions < maxExpansions)
            {
                int cur = Pop();
                if (cur == goal) { found = true; break; }
                closed[cur] = true;
                expansions++;
                int cx = cur % W, cy = cur / W;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (IsBlocked(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (IsBlocked(cx + dx, cy) || IsBlocked(cx, cy + dy))) continue; // no corner cutting
                        int ni = ny * W + nx;
                        Touch(ni);
                        if (closed[ni]) continue;
                        float ng = g[cur] + (dx != 0 && dy != 0 ? 1.41421f : 1f);
                        if (ng >= g[ni]) continue;
                        g[ni] = ng;
                        parent[ni] = cur;
                        float nf = ng + Heuristic(nx, ny, tx, ty);
                        if (heapPos[ni] >= 0) { f[ni] = nf; SiftUp(heapPos[ni]); }
                        else Push(ni, nf);
                    }
            }
            if (!found) return false;

            // Walk back to the start, then string pull with line of sight.
            cellPath.Clear();
            for (int c = goal; c >= 0; c = parent[c]) cellPath.Add(c);
            Vector3 anchor = from;
            int idx = cellPath.Count - 1;
            while (idx > 0)
            {
                // Walk forward while the next cell stays visible from the anchor (linear number of checks).
                int far = idx - 1;
                for (int k = idx - 1; k >= 0; k--)
                {
                    Vector3 cand = k == 0 ? to : CellCenter(cellPath[k] % W, cellPath[k] / W);
                    if (LineClear(anchor, cand)) far = k;
                    else break;
                }
                Vector3 wp = far == 0 ? to : CellCenter(cellPath[far] % W, cellPath[far] / W);
                path.Add(wp);
                anchor = wp;
                idx = far;
            }
            if (path.Count == 0 || (path[path.Count - 1] - to).sqrMagnitude > 1e-4f) path.Add(to);
            return true;
        }

        float Heuristic(int x, int y, int tx, int ty)
        {
            int dx = Mathf.Abs(x - tx), dy = Mathf.Abs(y - ty);
            return (dx + dy) + (1.41421f - 2f) * Mathf.Min(dx, dy);
        }

        void Touch(int i)
        {
            if (stamp[i] == search) return;
            stamp[i] = search;
            g[i] = float.MaxValue;
            closed[i] = false;
            heapPos[i] = -1;
        }

        // Binary min heap keyed by f.
        void Push(int i, float key)
        {
            f[i] = key;
            heap[heapCount] = i;
            heapPos[i] = heapCount;
            heapCount++;
            SiftUp(heapCount - 1);
        }

        int Pop()
        {
            int top = heap[0];
            heapPos[top] = -1;
            heapCount--;
            if (heapCount > 0)
            {
                heap[0] = heap[heapCount];
                heapPos[heap[0]] = 0;
                SiftDown(0);
            }
            return top;
        }

        void SiftUp(int k)
        {
            while (k > 0)
            {
                int p = (k - 1) >> 1;
                if (f[heap[p]] <= f[heap[k]]) break;
                Swap(k, p);
                k = p;
            }
        }

        void SiftDown(int k)
        {
            while (true)
            {
                int l = k * 2 + 1, r = l + 1, m = k;
                if (l < heapCount && f[heap[l]] < f[heap[m]]) m = l;
                if (r < heapCount && f[heap[r]] < f[heap[m]]) m = r;
                if (m == k) break;
                Swap(k, m);
                k = m;
            }
        }

        void Swap(int a, int b)
        {
            int t = heap[a]; heap[a] = heap[b]; heap[b] = t;
            heapPos[heap[a]] = a;
            heapPos[heap[b]] = b;
        }
    }
}
