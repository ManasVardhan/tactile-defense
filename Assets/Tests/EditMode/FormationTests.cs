using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Formation;
using UnityEngine;

namespace TactileWars.Tests
{
    public class PathResamplerTests
    {
        static List<Vector3> Line(float len, int n)
        {
            var l = new List<Vector3>();
            for (int i = 0; i < n; i++) l.Add(new Vector3(i * len / (n - 1), 0, 0));
            return l;
        }

        static List<Vector3> Circle(float r, int n, Vector3 c = default)
        {
            var l = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                l.Add(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }
            return l;
        }

        [Test]
        public void Length_OpenAndClosed()
        {
            Assert.AreEqual(10f, PathResampler.Length(Line(10, 5), false), 1e-4f);
            var sq = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) };
            Assert.AreEqual(3f, PathResampler.Length(sq, false), 1e-4f);
            Assert.AreEqual(4f, PathResampler.Length(sq, true), 1e-4f);
        }

        [Test]
        public void ResampleCount_EvenArcLengthSpacing()
        {
            var src = new List<Vector3> { Vector3.zero, new Vector3(2, 0, 0), new Vector3(2, 0, 8) }; // uneven segments
            var dst = new List<Vector3>();
            PathResampler.ResampleCount(src, false, 11, dst);
            Assert.AreEqual(11, dst.Count);
            for (int i = 1; i < dst.Count; i++)
                Assert.AreEqual(1f, Vector3.Distance(dst[i - 1], dst[i]), 0.02f, "segment " + i);
            Assert.AreEqual(src[0], dst[0]);
            Assert.AreEqual(src[2], dst[10]);
        }

        [Test]
        public void ResampleBySpacing_ClosedLoop()
        {
            var dst = new List<Vector3>();
            PathResampler.ResampleBySpacing(Circle(3f, 200), true, 0.5f, dst);
            float circ = 2f * Mathf.PI * 3f;
            Assert.AreEqual(Mathf.RoundToInt(circ / 0.5f), dst.Count, 1);
        }

        [Test]
        public void Chaikin_KeepsEndpointsAndSmooths()
        {
            var pts = new List<Vector3> { Vector3.zero, new Vector3(1, 0, 0), new Vector3(1, 0, 1) };
            var scratch = new List<Vector3>();
            PathResampler.Chaikin(pts, false, 3, scratch);
            Assert.Greater(pts.Count, 6);
            Assert.AreEqual(Vector3.zero, pts[0]);
            Assert.AreEqual(new Vector3(1, 0, 1), pts[pts.Count - 1]);
            // The sharp corner was cut: no point lies exactly at (1,0,0).
            foreach (var p in pts) Assert.Greater(Vector3.Distance(p, new Vector3(1, 0, 0)), 0.05f);
        }

        [Test]
        public void PolylinePath_SampleAndClosestParam()
        {
            var path = new PolylinePath();
            path.Set(Line(10, 3), false);
            path.Sample(2.5f, out var pos, out var tan);
            Assert.AreEqual(2.5f, pos.x, 1e-4f);
            Assert.AreEqual(1f, tan.x, 1e-4f);
            Assert.AreEqual(7f, path.ClosestParam(new Vector3(7, 0, 3)), 1e-4f);
        }
    }

    public class ShapeRecognizerTests
    {
        static List<Vector3> Wobbly(List<Vector3> pts, float amp, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < pts.Count; i++) pts[i] += new Vector3(((float)rng.NextDouble() - 0.5f) * amp, 0, ((float)rng.NextDouble() - 0.5f) * amp);
            return pts;
        }

        [Test]
        public void StraightStroke_IsLine()
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 40; i++) pts.Add(new Vector3(i * 0.25f, 0, 0));
            var r = ShapeRecognizer.Recognize(Wobbly(pts, 0.15f, 1), ShapeRecognizer.Settings.Default);
            Assert.AreEqual(ShapeType.Line, r.type);
            Assert.IsFalse(r.closed);
        }

        [Test]
        public void ClosedRound_IsCircle()
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 60; i++) { float a = i / 60f * Mathf.PI * 2f; pts.Add(new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f)); }
            var r = ShapeRecognizer.Recognize(Wobbly(pts, 0.2f, 2), ShapeRecognizer.Settings.Default);
            Assert.IsTrue(r.closed, "closed");
            Assert.AreEqual(ShapeType.Circle, r.type);
            Assert.AreEqual(4f, r.radius, 0.3f);
        }

        [Test]
        public void ClosedBox_IsSquare()
        {
            var pts = new List<Vector3>();
            Vector3[] c = { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(5, 0, 5), new Vector3(0, 0, 5), new Vector3(0, 0, 0) };
            for (int s = 0; s < 4; s++)
                for (int i = 0; i < 20; i++) pts.Add(Vector3.Lerp(c[s], c[s + 1], i / 20f));
            pts.Add(new Vector3(0.1f, 0, 0.05f));
            var r = ShapeRecognizer.Recognize(Wobbly(pts, 0.1f, 3), ShapeRecognizer.Settings.Default);
            Assert.IsTrue(r.closed);
            Assert.AreEqual(ShapeType.Square, r.type, "corners=" + r.corners);
        }

        [Test]
        public void OpenCurve_IsFree()
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 40; i++) { float a = i / 40f * Mathf.PI; pts.Add(new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f)); }
            var r = ShapeRecognizer.Recognize(pts, ShapeRecognizer.Settings.Default);
            Assert.IsFalse(r.closed);
            Assert.AreEqual(ShapeType.Free, r.type);
        }

        [Test]
        public void ClosedDetection()
        {
            var open = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0), new Vector3(5, 0, 5), new Vector3(2, 0, 5) };
            Assert.IsFalse(ShapeRecognizer.IsClosed(open, 0.2f));
            var closed = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0), new Vector3(5, 0, 5), new Vector3(0, 0, 5), new Vector3(0.3f, 0, 0.4f) };
            Assert.IsTrue(ShapeRecognizer.IsClosed(closed, 0.2f));
        }
    }

    public class FormationSolverTests
    {
        static readonly FormationParams P = FormationParams.Default;

        static float MinPairDistance(FormationSlot[] s, int n)
        {
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    best = Mathf.Min(best, Vector3.Distance(s[i].position, s[j].position));
            return best;
        }

        static List<Vector3> Line(float len)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= 40; i++) l.Add(new Vector3(-len * 0.5f + len * i / 40f, 0, 10));
            return l;
        }

        [TestCase(40, 30f)]
        [TestCase(40, 12f)]
        [TestCase(40, 3f)]
        [TestCase(1, 8f)]
        [TestCase(55, 20f)]
        public void LineStroke_SlotCountAndMinSpacing(int n, float len)
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[128];
            int c = solver.Solve(Line(len), false, n, P, new Vector3(0, 0, 0), slots);
            Assert.AreEqual(n, c, "slot count equals soldier count");
            if (n > 1) Assert.GreaterOrEqual(MinPairDistance(slots, c), P.minSpacing * 0.97f, "min spacing");
        }

        [Test]
        public void LongLine_SpacingCappedAtMax()
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int c = solver.Solve(Line(80f), false, 20, P, Vector3.zero, slots);
            for (int i = 1; i < c; i++)
                Assert.LessOrEqual(Vector3.Distance(slots[i - 1].position, slots[i].position), P.maxSpacing + 1e-3f);
        }

        [Test]
        public void ShortLine_AddsRowsBehind()
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int c = solver.Solve(Line(3f), false, 40, P, new Vector3(0, 0, 0), slots); // squad behind (smaller z)
            int maxRow = 0;
            for (int i = 0; i < c; i++)
            {
                maxRow = Mathf.Max(maxRow, slots[i].row);
                if (slots[i].row > 0) Assert.Less(slots[i].position.z, 10f, "extra rows go behind the stroke");
                Assert.Greater(slots[i].facing.z, 0.9f, "front faces away from the squad");
            }
            Assert.Greater(maxRow, 0);
        }

        [Test]
        public void CircleStroke_ClosedRingWithMinSpacing()
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < 64; i++) { float a = i / 64f * Mathf.PI * 2f; pts.Add(new Vector3(Mathf.Cos(a) * 5f, 0, Mathf.Sin(a) * 5f)); }
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int c = solver.Solve(pts, true, 40, P, Vector3.zero, slots);
            Assert.AreEqual(40, c);
            Assert.IsTrue(solver.Path.Closed);
            Assert.GreaterOrEqual(MinPairDistance(slots, c), P.minSpacing * 0.97f);
            for (int i = 0; i < c; i++)
            {
                Assert.AreEqual(5f, new Vector2(slots[i].position.x, slots[i].position.z).magnitude, 0.15f, "on the ring");
                Vector3 outward = slots[i].position.normalized;
                Assert.Greater(Vector3.Dot(outward, slots[i].facing), 0.9f, "faces outward");
            }
        }

        [Test]
        public void SmallCircle_FillsInnerRings()
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < 48; i++) { float a = i / 48f * Mathf.PI * 2f; pts.Add(new Vector3(Mathf.Cos(a) * 2f, 0, Mathf.Sin(a) * 2f)); }
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int c = solver.Solve(pts, true, 40, P, Vector3.zero, slots);
            Assert.AreEqual(40, c);
            Assert.GreaterOrEqual(MinPairDistance(slots, c), P.minSpacing * 0.9f);
        }

        [Test]
        public void Cluster_WhenStrokeDegenerate()
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int c = solver.Solve(new List<Vector3> { new Vector3(3, 0, 3) }, false, 30, P, Vector3.zero, slots);
            Assert.AreEqual(30, c);
            Assert.GreaterOrEqual(MinPairDistance(slots, c), P.minSpacing * 0.95f);
        }

        [Test]
        public void Assignment_IsPermutationAndOrdered()
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int n = 30;
            int c = solver.Solve(Line(25f), false, n, P, Vector3.zero, slots);
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++) pos[i] = new Vector3(-10f + i * 0.7f, 0, 0); // soldiers in a row below the stroke
            var assign = new int[n];
            solver.Assign(pos, n, slots, assign);
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++) Assert.IsTrue(seen.Add(assign[i]), "unique slot");
            // Left to right order is preserved (no crossing).
            for (int i = 1; i < n; i++) Assert.Less(slots[assign[i - 1]].position.x, slots[assign[i]].position.x);
        }

        [Test]
        public void Assignment_NoCrossingImprovesOnReversedStart()
        {
            var solver = new FormationSolver();
            var slots = new FormationSlot[64];
            int n = 20;
            solver.Solve(Line(15f), false, n, P, Vector3.zero, slots);
            var rng = new System.Random(5);
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++) pos[i] = new Vector3((float)rng.NextDouble() * 16f - 8f, 0, (float)rng.NextDouble() * 4f);
            var assign = new int[n];
            solver.Assign(pos, n, slots, assign);
            // No pair can be swapped to reduce total distance (pairwise optimal implies no crossing paths).
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                {
                    float cur = Vector3.Distance(pos[a], slots[assign[a]].position) + Vector3.Distance(pos[b], slots[assign[b]].position);
                    float swp = Vector3.Distance(pos[a], slots[assign[b]].position) + Vector3.Distance(pos[b], slots[assign[a]].position);
                    Assert.LessOrEqual(cur, swp + 1e-3f);
                }
        }
    }
}
