using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>UnitRegistry.FindNearest with preferDrawsFire: a draws-fire target in range outranks nearer ones.</summary>
    public class DrawsFireTests
    {
        readonly List<GameObject> made = new List<GameObject>();

        [SetUp]
        public void SetUp() { UnitRegistry.Clear(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
            made.Clear();
            UnitRegistry.Clear();
        }

        Health Target(float x, float z, bool drawsFire)
        {
            var g = new GameObject("Target");
            made.Add(g);
            g.transform.position = new Vector3(x, 0f, z);
            var h = g.AddComponent<Health>();
            h.targetRadius = 0.3f;
            h.drawsFire = drawsFire;
            h.Init(Team.Player, TeamColorId.Red, 30f);
            UnitRegistry.Register(h); // edit mode does not run OnEnable; Register ignores duplicates
            return h;
        }

        [Test]
        public void NearestWinsByDefault()
        {
            var near = Target(2f, 0f, false);
            Target(4f, 0f, true);
            Assert.AreSame(near, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f));
            Assert.AreSame(near, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f, false));
        }

        [Test]
        public void DrawsFireTargetInRangeOutranksNearer()
        {
            Target(2f, 0f, false);
            var tank = Target(5f, 0f, true);
            Target(5.5f, 0f, true);
            Assert.AreSame(tank, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f, true), "nearest of the draws-fire targets");
        }

        [Test]
        public void DrawsFireTargetOutOfRangeIsIgnored()
        {
            var near = Target(2f, 0f, false);
            Target(9f, 0f, true);
            Assert.AreSame(near, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f, true));
        }

        [Test]
        public void DeadDrawsFireTargetIsIgnored()
        {
            var near = Target(2f, 0f, false);
            var tank = Target(4f, 0f, true);
            tank.Kill(TeamColorId.Teal);
            Assert.AreSame(near, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f, true));
            // Even a dead target left in the registry (a stale entry) must not draw fire.
            UnitRegistry.Register(tank);
            Assert.AreSame(near, UnitRegistry.FindNearest(Team.Player, Vector3.zero, 6f, true), "stale dead entry ignored");
        }
    }
}
