using System.Collections.Generic;
using UnityEngine;

namespace TactileWars.Formation
{
    public struct FormationSlot
    {
        public Vector3 position;
        public Vector3 facing;  // outward / front normal on XZ
        public float param;     // arc length on the base path, used for ordered assignment
        public int row;
    }

    /// <summary>
    /// Distributes N soldiers single file along a path with spacing in [minSpacing, maxSpacing].
    /// Too short: extra parallel rows behind (open) or inside (closed). Degenerate: compact cluster.
    /// Assignment minimises total travel (ordered matching plus pairwise improvement, which removes
    /// crossings because the Euclidean optimum never crosses). All buffers are reused: no per call garbage
    /// after warm up.
    /// </summary>
    public class FormationSolver
    {
        readonly PolylinePath path = new PolylinePath(512);
        readonly List<Vector3> scratch = new List<Vector3>(512);
        int[] order = new int[64];
        float[] keys = new float[64];
        int[] slotOrder = new int[64];
        float[] slotKeys = new float[64];
        int[] tmpAssign = new int[64];

        public PolylinePath Path => path;

        void Ensure(int n)
        {
            if (order.Length >= n) return;
            int c = Mathf.NextPowerOfTwo(n);
            order = new int[c]; keys = new float[c]; slotOrder = new int[c]; slotKeys = new float[c]; tmpAssign = new int[c];
        }

        /// <summary>Computes slots. 'behindHint' is a world point on the side where extra rows go
        /// (usually the squad centroid, so the front faces away from where the squad comes from).</summary>
        public int Solve(List<Vector3> pts, bool closed, int count, FormationParams p, Vector3 behindHint, FormationSlot[] outSlots)
        {
            if (count <= 0) return 0;
            path.Set(pts, closed);
            float L = path.Length;
            if (pts.Count < 2 || L < p.minSpacing * 1.5f)
            {
                Vector3 c = pts.Count > 0 ? PathResampler.Centroid(pts) : behindHint;
                return Cluster(c, count, p, behindHint, outSlots);
            }

            bool isClosed = path.Closed;
            // Side sign: +1 means behind / inner side is the left normal (cross(up, tangent)).
            float side;
            if (isClosed)
            {
                side = PathResampler.SignedArea(path.Points) > 0f ? 1f : -1f; // CCW: left normal points inward
            }
            else
            {
                Vector3 mid; Vector3 tan;
                path.Sample(L * 0.5f, out mid, out tan);
                Vector3 left = new Vector3(-tan.z, 0f, tan.x);
                Vector3 toHint = behindHint - PathResampler.Centroid(path.Points); toHint.y = 0f;
                if (toHint.sqrMagnitude < 1e-4f) toHint = Vector3.back;
                side = Vector3.Dot(left, toHint) >= 0f ? 1f : -1f;
            }

            int written = 0;
            int row = 0;
            int remaining = count;
            float rowLen = L;
            float meanR = 1f;
            if (isClosed)
            {
                Vector3 c = PathResampler.Centroid(path.Points);
                meanR = 0f;
                for (int i = 0; i < path.Points.Count; i++) { Vector3 d = path.Points[i] - c; d.y = 0; meanR += d.magnitude; }
                meanR = Mathf.Max(1e-3f, meanR / Mathf.Max(1, path.Points.Count));
            }
            int inward = 0, outward = 0;
            bool inwardDone = false;
            while (remaining > 0)
            {
                float offset = row * p.rowSpacing;
                if (isClosed && row > 0)
                {
                    // Fill rings inside the loop while they fit, then continue with rings outside it.
                    if (!inwardDone)
                    {
                        float r = meanR - (inward + 1) * p.rowSpacing;
                        if (r >= p.minSpacing) { inward++; offset = inward * p.rowSpacing; rowLen = L * r / meanR; }
                        else inwardDone = true;
                    }
                    if (inwardDone)
                    {
                        outward++;
                        offset = -outward * p.rowSpacing;
                        rowLen = L * (meanR + outward * p.rowSpacing) / meanR;
                    }
                }
                int cap = isClosed ? Mathf.Max(1, Mathf.FloorToInt(rowLen / p.minSpacing)) : Mathf.FloorToInt(rowLen / p.minSpacing) + 1;
                int inRow = Mathf.Min(cap, remaining);
                float spacing;
                float start;
                if (isClosed)
                {
                    spacing = L / Mathf.Max(1, inRow); // evenly around the whole loop (base path param)
                    start = (row % 2) * spacing * 0.5f;
                }
                else
                {
                    spacing = inRow > 1 ? Mathf.Min(L / (inRow - 1), p.maxSpacing) : 0f;
                    float used = spacing * (inRow - 1);
                    float slack = (L - used) * 0.5f;
                    start = slack;
                    // Stagger odd rows by up to half a spacing (brick pattern) without leaving the stroke.
                    if (row % 2 == 1) start += Mathf.Min(spacing * 0.5f, slack);
                }
                for (int i = 0; i < inRow; i++)
                {
                    float s = start + i * spacing;
                    path.Sample(s, out Vector3 pos, out Vector3 tan);
                    Vector3 left = new Vector3(-tan.z, 0f, tan.x);
                    Vector3 behind = left * side;
                    Vector3 slotPos = pos + behind * offset;
                    slotPos.y = 0f;
                    outSlots[written++] = new FormationSlot { position = slotPos, facing = -behind, param = s, row = row };
                }
                remaining -= inRow;
                row++;
                if (row > 64) break;
            }
            return written;
        }

        /// <summary>Compact sunflower cluster with spacing at least minSpacing (golden angle spiral).</summary>
        public static int Cluster(Vector3 center, int count, FormationParams p, Vector3 facingFrom, FormationSlot[] outSlots)
        {
            const float golden = 2.39996323f;
            float k = p.minSpacing * 0.66f; // Vogel spiral: nearest neighbour is about 1.55 k, so about minSpacing
            Vector3 fwd = center - facingFrom; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            for (int i = 0; i < count; i++)
            {
                float r = k * Mathf.Sqrt(i + 0.5f);
                float a = i * golden;
                Vector3 pos = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 outward = pos - center; outward.y = 0f;
                Vector3 face = outward.sqrMagnitude > 1e-4f ? outward.normalized : fwd;
                outSlots[i] = new FormationSlot { position = pos, facing = face, param = i, row = 0 };
            }
            return count;
        }

        /// <summary>
        /// Assigns soldiers to slots. assignment[soldier] = slot index. Uses the solved path for ordering.
        /// </summary>
        public void Assign(Vector3[] soldierPos, int n, FormationSlot[] slots, int[] assignment)
        {
            Ensure(n);
            bool usePath = path.Points.Count >= 2 && path.Length > 1e-3f;
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
                keys[i] = usePath ? path.ClosestParam(soldierPos[i]) : Angle(soldierPos[i], slots, n);
                slotOrder[i] = i;
                slotKeys[i] = usePath ? slots[i].param : Angle(slots[i].position, slots, n);
            }
            SortBy(order, keys, n);
            SortBy(slotOrder, slotKeys, n);

            bool closed = usePath && path.Closed;
            float best = float.MaxValue;
            int bestShift = 0; bool bestRev = false;
            int shifts = closed ? n : 1;
            int step = closed ? Mathf.Max(1, n / 24) : 1;
            for (int rev = 0; rev < 2; rev++)
            {
                for (int sh = 0; sh < shifts; sh += step)
                {
                    float cost = 0f;
                    for (int k = 0; k < n; k++)
                    {
                        int si = SlotAt(k, sh, rev == 1, n);
                        cost += Dist(soldierPos[order[k]], slots[slotOrder[si]].position);
                        if (cost >= best) break;
                    }
                    if (cost < best) { best = cost; bestShift = sh; bestRev = rev == 1; }
                }
            }
            for (int k = 0; k < n; k++)
                assignment[order[k]] = slotOrder[SlotAt(k, bestShift, bestRev, n)];

            // Pairwise improvement (2-opt on the matching) until stable, capped at six passes.
            for (int pass = 0; pass < 6; pass++)
            {
                bool improved = false;
                for (int a = 0; a < n; a++)
                {
                    for (int b = a + 1; b < n; b++)
                    {
                        Vector3 pa = soldierPos[a], pb = soldierPos[b];
                        Vector3 sa = slots[assignment[a]].position, sb = slots[assignment[b]].position;
                        float cur = Dist(pa, sa) + Dist(pb, sb);
                        float swp = Dist(pa, sb) + Dist(pb, sa);
                        if (swp + 1e-4f < cur)
                        {
                            int t = assignment[a]; assignment[a] = assignment[b]; assignment[b] = t;
                            improved = true;
                        }
                    }
                }
                if (!improved) break;
            }
        }

        static int SlotAt(int k, int shift, bool reverse, int n)
        {
            int i = reverse ? (n - 1 - k) : k;
            i += shift;
            if (i >= n) i -= n;
            return i;
        }

        static float Angle(Vector3 p, FormationSlot[] slots, int n)
        {
            return Mathf.Atan2(p.z, p.x);
        }

        static float Dist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void SortBy(int[] idx, float[] key, int n)
        {
            // Insertion sort on parallel arrays: n is small (tens), no allocation.
            for (int i = 1; i < n; i++)
            {
                int vi = idx[i]; float vk = key[i];
                int j = i - 1;
                while (j >= 0 && key[j] > vk) { idx[j + 1] = idx[j]; key[j + 1] = key[j]; j--; }
                idx[j + 1] = vi; key[j + 1] = vk;
            }
        }
    }
}
