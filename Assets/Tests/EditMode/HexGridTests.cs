using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>Hex grid rules: world mapping, neighbors, distance, occupancy, nav baking and unit slots.</summary>
    public class HexGridTests
    {
        const float R = 1.4f;
        HexGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = new HexGrid(new Vector2(-15f, -15f), new Vector2(15f, 15f), R);
        }

        HexCoord At(float x, float z)
        {
            Assert.IsTrue(grid.TryWorldToHex(new Vector3(x, 0f, z), out HexCoord c), $"({x}, {z}) should be in bounds");
            return c;
        }

        HexCoord FirstCell()
        {
            foreach (var c in grid.Cells) return c;
            Assert.Fail("grid has no cells");
            return default;
        }

        [Test]
        public void CenterRoundTrips()
        {
            int n = 0;
            foreach (var c in grid.Cells)
            {
                Assert.IsTrue(grid.TryWorldToHex(grid.Center(c), out HexCoord back), c.ToString());
                Assert.AreEqual(c, back);
                Assert.AreEqual(0f, grid.Center(c).y);
                n++;
            }
            Assert.Greater(n, 20);
        }

        [Test]
        public void PointsNearCenterMapToThatHex()
        {
            foreach (var c in grid.Cells)
            {
                Vector3 p = grid.Center(c);
                Assert.IsTrue(grid.TryWorldToHex(p + new Vector3(0.6f * R, 0f, 0f), out HexCoord a));
                Assert.AreEqual(c, a);
                Assert.IsTrue(grid.TryWorldToHex(p + new Vector3(0f, 0f, 0.6f * R), out HexCoord b));
                Assert.AreEqual(c, b);
            }
        }

        [Test]
        public void OutsideBoundsIsRejected()
        {
            Assert.IsFalse(grid.TryWorldToHex(new Vector3(20f, 0f, 0f), out _));
            Assert.IsFalse(grid.TryWorldToHex(new Vector3(0f, 0f, 20f), out _));
            Assert.IsFalse(grid.TryWorldToHex(new Vector3(20f, 0f, 20f), out _));
        }

        [Test]
        public void InteriorCellHasSixNeighborsAtDistanceOne()
        {
            var c = At(0f, 0f);
            var list = new List<HexCoord> { c };
            grid.Neighbors(c, list);
            Assert.AreEqual(6, list.Count);
            var seen = new HashSet<HexCoord>();
            foreach (var n in list)
            {
                Assert.AreEqual(1, HexGrid.Distance(c, n));
                Assert.IsTrue(grid.InBounds(n));
                Assert.IsTrue(seen.Add(n), "duplicate neighbor");
                float d = (grid.Center(n) - grid.Center(c)).magnitude;
                Assert.AreEqual(Mathf.Sqrt(3f) * R, d, 1e-3f);
            }
        }

        [Test]
        public void CornerCellHasFewer()
        {
            var c = FirstCell();
            var list = new List<HexCoord>();
            grid.Neighbors(c, list);
            Assert.Greater(list.Count, 0);
            Assert.Less(list.Count, 6);
            foreach (var n in list)
            {
                Assert.AreEqual(1, HexGrid.Distance(c, n));
                Assert.IsTrue(grid.InBounds(n));
            }
        }

        [Test]
        public void DistanceIsSymmetricAndZeroOnSelf()
        {
            var a = At(-10f, -8f);
            var b = At(9f, 11f);
            Assert.AreEqual(0, HexGrid.Distance(a, a));
            Assert.AreEqual(HexGrid.Distance(a, b), HexGrid.Distance(b, a));
            Assert.Greater(HexGrid.Distance(a, b), 5);
            Assert.AreEqual(3, HexGrid.Distance(new HexCoord(0, 0), new HexCoord(3, -1)));
            Assert.AreEqual(3, HexGrid.Distance(new HexCoord(0, 0), new HexCoord(-1, -2)));
        }

        [Test]
        public void ReserveRules()
        {
            var c = At(0f, 0f);
            Assert.IsTrue(grid.IsFree(c));
            Assert.AreEqual(0, grid.OwnerOf(c));
            Assert.IsTrue(grid.TryReserve(c, 1));
            Assert.AreEqual(1, grid.OwnerOf(c));
            Assert.IsFalse(grid.IsFree(c));
            Assert.IsFalse(grid.TryReserve(c, 2));
            Assert.IsTrue(grid.TryReserve(c, 1));
            grid.Release(c, 2);
            Assert.AreEqual(1, grid.OwnerOf(c));
            grid.Release(c, 1);
            Assert.AreEqual(0, grid.OwnerOf(c));
            Assert.IsTrue(grid.IsFree(c));

            var b = At(5f, 5f);
            grid.SetBlocked(b, true);
            Assert.IsTrue(grid.IsBlocked(b));
            Assert.IsFalse(grid.IsFree(b));
            Assert.IsFalse(grid.TryReserve(b, 1));
            Assert.AreEqual(0, grid.OwnerOf(b));

            Assert.IsFalse(grid.TryReserve(new HexCoord(1000, 1000), 1));
            Assert.IsFalse(grid.IsFree(new HexCoord(1000, 1000)));
        }

        [Test]
        public void BakeBlockedFollowsNavGrid()
        {
            var nav = new NavGrid(new Vector2(-15f, -15f), new Vector2(15f, 15f), 1f);
            nav.BlockRect(new Vector2(-3f, -3f), new Vector2(3f, 3f));
            grid.BakeBlocked(nav);

            int inside = 0;
            foreach (var c in grid.Cells)
            {
                Vector3 p = grid.Center(c);
                if (Mathf.Abs(p.x) < 3f && Mathf.Abs(p.z) < 3f)
                {
                    Assert.IsTrue(grid.IsBlocked(c), $"{c} at {p} should be blocked");
                    inside++;
                }
            }
            Assert.Greater(inside, 0);

            var far = At(10f, 0f);
            Assert.IsFalse(grid.IsBlocked(far));
            Assert.IsTrue(grid.IsFree(far));
        }

        [Test]
        public void SlotsStayInsideHexAndApart()
        {
            var c = At(0f, 0f);
            Vector3 center = grid.Center(c);
            var slots = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                slots[i] = grid.SlotPosition(c, i);
                Assert.LessOrEqual((slots[i] - center).magnitude, 0.5f * R + 1e-4f);
            }
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    Assert.GreaterOrEqual((slots[i] - slots[j]).magnitude, 0.9f);
        }
    }
}
