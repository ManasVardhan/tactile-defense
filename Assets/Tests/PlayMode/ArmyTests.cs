using System.Collections;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>The unit army API: deploying units of soldiers onto hexes, moving them with hex reservations,
    /// rejecting bad orders, destruction and restore, and the Tank type stats.</summary>
    public class ArmyTests
    {
        PlayerArmy army;
        Vector3 spawn;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            army = null;
            BattleTestUtil.Cleanup();
            yield return null;
        }

        /// <summary>Loads the battle (an attack battle, so the scene's PlayerArmy is live) and takes its army.</summary>
        IEnumerator Setup()
        {
            yield return BattleTestUtil.LoadBattle();
            army = PlayerArmy.Instance;
            Assert.IsNotNull(army, "the battle scene has a PlayerArmy");
            Assert.IsNotNull(army.roster, "unit roster asset");
            spawn = army.spawnPoint;
            spawn.y = 0f;
            yield return null;
        }

        bool Usable(HexCoord c) => army.Grid.IsFree(c) && UnitRegistry.CountWithin(Team.Enemy, army.Grid.Center(c), army.Grid.OuterRadius * 3f) == 0;

        /// <summary>Usable hex nearest 'p', at least two hexes from every hex in 'avoid'.</summary>
        HexCoord FreeHexNear(Vector3 p, params HexCoord[] avoid)
        {
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in army.Grid.Cells)
            {
                if (!Usable(c) || TooClose(c, avoid, 2)) continue;
                float d = (army.Grid.Center(c) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "a free hex exists");
            return best;
        }

        /// <summary>Usable hex exactly 'steps' hexes from 'from'; reachable on the nav grid when asked.</summary>
        HexCoord HexAtDistance(HexCoord from, int steps, bool reachable, params HexCoord[] avoid)
        {
            var path = new System.Collections.Generic.List<Vector3>();
            foreach (var c in army.Grid.Cells)
            {
                if (HexGrid.Distance(from, c) != steps || !Usable(c) || TooClose(c, avoid, 2)) continue;
                if (reachable && !army.Nav.FindPath(army.Grid.Center(from), army.Grid.Center(c), path)) continue;
                return c;
            }
            Assert.Fail($"no usable hex {steps} steps from {from}");
            return default;
        }

        static bool TooClose(HexCoord c, HexCoord[] avoid, int min)
        {
            foreach (var a in avoid) if (HexGrid.Distance(a, c) < min) return true;
            return false;
        }

        bool AllWithin(PlayerUnit unit, HexCoord hex)
        {
            Vector3 c = army.Grid.Center(hex);
            foreach (var s in unit.Soldiers)
            {
                if (!s.IsAlive) continue;
                Vector3 d = s.transform.position - c; d.y = 0f;
                if (d.magnitude > army.Grid.OuterRadius) return false;
            }
            return true;
        }

        static void KillAll(PlayerUnit unit)
        {
            foreach (var s in unit.Soldiers) if (s.IsAlive) s.Health.Kill(TeamColorId.Teal);
        }

        [UnityTest]
        public IEnumerator DeploySpawnsThreeSoldiersOnTheHex()
        {
            yield return Setup();
            var hex = FreeHexNear(spawn);
            Assert.IsTrue(army.CanDeploy(0, hex));
            var unit = army.Deploy(0, hex);
            Assert.IsNotNull(unit, "deploy returns the unit");
            Assert.AreEqual(3, unit.AliveCount);
            Assert.AreEqual(3, unit.Soldiers.Count);
            Assert.GreaterOrEqual(unit.Id, 1);
            Assert.AreEqual(hex, unit.Hex);
            Assert.AreEqual(EntryState.Deployed, army.Squad[0].state);
            Assert.AreSame(unit, army.Squad[0].unit);
            Assert.AreEqual(unit.Id, army.Grid.OwnerOf(hex));
            Assert.AreSame(unit, army.UnitAt(hex));
            Assert.AreEqual(2, army.ReadyCount);
            Assert.AreEqual(3, army.StartCount);

            yield return BattleTestUtil.Wait(1f);
            Assert.AreEqual(3, army.AliveCount);
            Assert.IsTrue(AllWithin(unit, hex), "soldiers stand on their hex");
            Assert.AreEqual(1f, unit.HealthFraction, 1e-3f);
        }

        [UnityTest]
        public IEnumerator CannotDeployTwiceOrOnOccupiedOrBlocked()
        {
            yield return Setup();
            var hex = FreeHexNear(spawn);
            var unit = army.Deploy(0, hex);
            Assert.IsNotNull(unit);
            var other = FreeHexNear(spawn, hex);
            Assert.IsNull(army.Deploy(0, other), "an entry deploys only once");
            Assert.IsFalse(army.CanDeploy(1, hex));
            Assert.IsNull(army.Deploy(1, hex), "occupied hex refused");

            army.Grid.SetBlocked(other, true);
            Assert.IsFalse(army.CanDeploy(1, other));
            Assert.IsNull(army.Deploy(1, other), "blocked hex refused");
            Assert.AreEqual(EntryState.Ready, army.Squad[1].state, "refusals leave the entry ready");
            Assert.AreEqual(1, army.Units.Count);

            var third = FreeHexNear(spawn, hex, other);
            Assert.IsNotNull(army.Deploy(1, third), "a free hex still works");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MoveReservesDestinationAndFreesOrigin()
        {
            yield return Setup();
            var origin = FreeHexNear(spawn);
            var unit = army.Deploy(0, origin);
            yield return BattleTestUtil.Wait(0.3f);
            var dest = HexAtDistance(origin, 4, true);

            Assert.IsTrue(army.OrderMove(unit, dest), "move accepted");
            Assert.AreEqual(unit.Id, army.Grid.OwnerOf(dest), "destination reserved");
            Assert.IsTrue(army.Grid.IsFree(origin), "origin released");
            Assert.AreEqual(dest, unit.Hex);
            Assert.AreEqual(dest, unit.Destination);
            Assert.IsTrue(unit.IsMoving);

            float deadline = Time.time + 8f;
            while (Time.time < deadline && (unit.IsMoving || !AllWithin(unit, dest))) yield return null;
            Assert.IsFalse(unit.IsMoving, "arrived");
            Assert.IsTrue(AllWithin(unit, dest), "soldiers stand on the destination hex");
            Assert.AreEqual(3, unit.AliveCount);
        }

        [UnityTest]
        public IEnumerator MoveToOccupiedHexIsRejected()
        {
            yield return Setup();
            var a = FreeHexNear(spawn);
            var b = FreeHexNear(spawn, a);
            var u0 = army.Deploy(0, a);
            var u1 = army.Deploy(1, b);
            Assert.IsNotNull(u0); Assert.IsNotNull(u1);
            int rejected = 0;
            HexCoord rejectedAt = default;
            army.OrderRejected += h => { rejected++; rejectedAt = h; };

            Assert.IsFalse(army.OrderMove(u0, b), "occupied destination refused");
            Assert.AreEqual(1, rejected, "OrderRejected fired once");
            Assert.AreEqual(b, rejectedAt);
            Assert.AreEqual(a, u0.Hex, "unit unmoved");
            Assert.IsFalse(u0.IsMoving);
            Assert.AreEqual(u0.Id, army.Grid.OwnerOf(a));
            Assert.AreEqual(u1.Id, army.Grid.OwnerOf(b));
            yield return BattleTestUtil.Wait(0.5f);
            Assert.IsTrue(AllWithin(u0, a), "soldiers stay home");
        }

        [UnityTest]
        public IEnumerator UnreachableDestinationIsRejectedAndNotReserved()
        {
            yield return Setup();
            var origin = FreeHexNear(spawn);
            var unit = army.Deploy(0, origin);
            var dest = HexAtDistance(origin, 4, false);

            // Wall the destination in on the nav grid only: the hex itself stays free.
            var nav = army.Nav;
            Vector3 c = army.Grid.Center(dest);
            for (int y = 0; y < nav.H; y++)
                for (int x = 0; x < nav.W; x++)
                {
                    Vector3 d = nav.CellCenter(x, y) - c; d.y = 0f;
                    float m = d.magnitude;
                    if (m >= 2.2f && m <= 3.6f) nav.SetBlocked(x, y, true);
                }
            Assert.IsTrue(army.Grid.IsFree(dest), "hex not blocked by the nav ring");
            int rejected = 0;
            army.OrderRejected += h => rejected++;

            Assert.IsFalse(army.OrderMove(unit, dest), "unreachable destination refused");
            Assert.AreEqual(1, rejected);
            Assert.IsTrue(army.Grid.IsFree(dest), "destination not reserved");
            Assert.AreEqual(unit.Id, army.Grid.OwnerOf(origin), "origin still owned");
            Assert.AreEqual(origin, unit.Hex);
            Assert.IsFalse(unit.IsMoving);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedWhileMovingFreesDestination()
        {
            yield return Setup();
            var origin = FreeHexNear(spawn);
            var unit = army.Deploy(0, origin);
            var dest = HexAtDistance(origin, 8, true);
            int destroyedEvents = 0;
            unit.Destroyed += u => destroyedEvents++;
            army.Select(unit);
            Assert.IsTrue(army.OrderMove(unit, dest));
            yield return null;
            KillAll(unit);
            yield return null;

            Assert.IsTrue(unit.IsDestroyed);
            Assert.AreEqual(1, destroyedEvents);
            Assert.IsTrue(army.Grid.IsFree(dest), "destination released");
            Assert.IsTrue(army.Grid.IsFree(origin), "origin stays free");
            Assert.AreEqual(EntryState.Destroyed, army.Squad[0].state);
            Assert.IsNull(army.Selected, "destroyed unit deselected");
            Assert.AreEqual(0, army.Units.Count);
            Assert.AreEqual(0, army.AliveCount);
            Assert.AreEqual(2, army.ReadyCount);
            Assert.IsTrue(army.CanStillFight, "two entries are still ready");

            // Lose the other two entries too: nothing left to fight with.
            for (int i = 1; i < 3; i++)
            {
                var u = army.Deploy(i, FreeHexNear(spawn));
                Assert.IsNotNull(u);
                Assert.IsTrue(army.CanStillFight);
                KillAll(u);
                yield return null;
            }
            Assert.AreEqual(0, army.ReadyCount);
            Assert.IsFalse(army.CanStillFight, "every entry destroyed");
        }

        [UnityTest]
        public IEnumerator TankHasTripleHealthAndDrawsFire()
        {
            yield return Setup();
            Assert.AreEqual(UnitTypeId.Tank, army.Squad[2].type, "default squad ends with a Tank");
            var a = FreeHexNear(spawn);
            var attacker = army.Deploy(0, a);
            var tank = army.Deploy(2, FreeHexNear(spawn, a));
            Assert.IsNotNull(attacker); Assert.IsNotNull(tank);
            Assert.AreEqual(UnitTypeId.Tank, tank.Type.id);
            Assert.AreEqual(3, tank.AliveCount);
            float am = attacker.Soldiers[0].Health.max, tm = tank.Soldiers[0].Health.max;
            Assert.AreEqual(am * 3f, tm, 1e-3f, "tank soldiers have triple health");
            foreach (var s in tank.Soldiers) Assert.IsTrue(s.Health.drawsFire, "tank soldiers draw fire");
            foreach (var s in attacker.Soldiers) Assert.IsFalse(s.Health.drawsFire);
            Assert.AreEqual(army.soldierConfig.maxHealth, am, 1e-3f, "assets are not modified");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestoreLastDestroyedMakesEntryReadyAgain()
        {
            yield return Setup();
            Assert.IsFalse(army.RestoreLastDestroyed(), "nothing destroyed yet");
            var a = FreeHexNear(spawn);
            var u0 = army.Deploy(0, a);
            var u1 = army.Deploy(1, FreeHexNear(spawn, a));
            int id1 = u1.Id;
            KillAll(u0);
            yield return null;
            KillAll(u1);
            yield return null;
            Assert.AreEqual(1, army.ReadyCount);

            Assert.IsTrue(army.RestoreLastDestroyed());
            Assert.AreEqual(EntryState.Ready, army.Squad[1].state, "most recently destroyed comes back first");
            Assert.IsNull(army.Squad[1].unit);
            Assert.AreEqual(EntryState.Destroyed, army.Squad[0].state);
            Assert.AreEqual(2, army.ReadyCount);

            Assert.IsTrue(army.RestoreLastDestroyed());
            Assert.AreEqual(EntryState.Ready, army.Squad[0].state);
            Assert.IsFalse(army.RestoreLastDestroyed(), "none left to restore");

            var again = army.Deploy(1, FreeHexNear(spawn));
            Assert.IsNotNull(again, "a restored entry deploys again");
            Assert.AreNotEqual(id1, again.Id, "fresh unit id");
            yield return null;
        }
    }
}
