using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Structures;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>The four Plan 2 unit types and their always-on abilities: the Healer heals and revives neighbors and
    /// never fires, the Sniper outranges, the Vanguard captures faster, the Demolisher lobs shells that hurt
    /// structures far more than soldiers and goes for structures first. Squads are set directly with SetSquad.</summary>
    public class UnitAbilityTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleTestUtil.Cleanup();
            yield return null;
        }

        static PlayerArmy Army => BattleTestUtil.Army;

        /// <summary>Loads the battle with no enemies and the given squad.</summary>
        static IEnumerator Setup(params UnitTypeId[] squad)
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            Army.SetSquad(squad);
            yield return BattleTestUtil.Wait(0.3f);
        }

        static PlayerUnit Deploy(UnitTypeId type, HexCoord hex)
        {
            for (int i = 0; i < Army.Squad.Count; i++)
                if (Army.Squad[i].type == type && Army.Squad[i].state == EntryState.Ready)
                {
                    var u = Army.Deploy(i, hex);
                    Assert.IsNotNull(u, "deploy " + type + " on " + hex);
                    return u;
                }
            Assert.Fail("no ready " + type);
            return null;
        }

        /// <summary>An open stretch of the lane, clear of the pads.</summary>
        static readonly Vector3 Open = new Vector3(0f, 0f, 24f);

        /// <summary>A free neighbor of 'hex' (nearest 'toward' when given).</summary>
        static HexCoord FreeNeighbor(HexCoord hex, Vector3? toward = null)
        {
            var g = Army.Grid;
            var near = new List<HexCoord>(6);
            g.Neighbors(hex, near);
            HexCoord best = default;
            float bd = float.MaxValue;
            foreach (var n in near)
            {
                if (!g.IsFree(n)) continue;
                float d = toward.HasValue ? (g.Center(n) - toward.Value).sqrMagnitude : 0f;
                if (d < bd) { bd = d; best = n; }
            }
            Assert.Less(bd, float.MaxValue, "a free neighbor of " + hex);
            return best;
        }

        /// <summary>A free hex exactly 'dist' hex steps from 'from'.</summary>
        static HexCoord FreeAtDistance(HexCoord from, int dist, params HexCoord[] notNear)
        {
            var g = Army.Grid;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c) || HexGrid.Distance(c, from) != dist) continue;
                bool bad = false;
                foreach (var a in notNear) if (HexGrid.Distance(c, a) <= 1) bad = true;
                if (!bad) return c;
            }
            Assert.Fail("no free hex " + dist + " from " + from);
            return default;
        }

        static Soldier FirstMember(EnemySquad squad) => squad.GetComponentInChildren<Soldier>();

        // ------------------------------------------------------------------ Healer

        [UnityTest]
        public IEnumerator HealerRestoresInjuredNeighborAndStopsAtMax()
        {
            yield return Setup(UnitTypeId.Healer, UnitTypeId.Attacker, UnitTypeId.Attacker);
            var hh = BattleTestUtil.FreeHexNear(Open);
            var healer = Deploy(UnitTypeId.Healer, hh);
            var near = Deploy(UnitTypeId.Attacker, FreeNeighbor(hh));
            var far = Deploy(UnitTypeId.Attacker, FreeAtDistance(hh, 2, near.Hex));
            Assert.AreEqual(1, HexGrid.Distance(hh, near.Hex));
            Assert.AreEqual(2, HexGrid.Distance(hh, far.Hex));
            yield return null;
            var hurt = near.Soldiers[0].Health;
            var other = far.Soldiers[0].Health;
            hurt.TakeDamage(hurt.max * 0.7f, hurt.Position + Vector3.forward, TeamColorId.Teal);
            other.TakeDamage(other.max * 0.7f, other.Position + Vector3.forward, TeamColorId.Teal);
            Assert.AreEqual(0.3f, hurt.Fraction, 1e-3f);
            float start = hurt.Current, last = start;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return null;
                Assert.GreaterOrEqual(hurt.Current, last - 1e-4f, "healing never goes down");
                Assert.LessOrEqual(hurt.Current, hurt.max, "never above max");
                last = hurt.Current;
            }
            float expected = healer.Type.healPerSecond * 3f;
            Debug.Log($"[Test] healer: neighbor {start:0.0} -> {hurt.Current:0.0} in 3 s (expected about +{expected:0.0}), two hexes away {other.Current:0.0}");
            Assert.Greater(hurt.Current, start + expected * 0.8f, "the neighbor heals at about healPerSecond");
            Assert.AreEqual(other.max * 0.3f, other.Current, 1e-3f, "a soldier two hexes away is not healed");

            // It tops out at max and stays there.
            end = Time.time + 8f;
            while (Time.time < end && hurt.Current < hurt.max) { yield return null; Assert.LessOrEqual(hurt.Current, hurt.max); }
            Assert.AreEqual(hurt.max, hurt.Current, 1e-4f, "healed to full");
            yield return BattleTestUtil.Wait(1f);
            Assert.AreEqual(hurt.max, hurt.Current, 1e-4f, "and not past it");
            Assert.AreEqual(other.max * 0.3f, other.Current, 1e-3f);
        }

        [UnityTest]
        public IEnumerator HealerRevivesOneSoldierInDamagedNeighbor()
        {
            yield return Setup(UnitTypeId.Healer, UnitTypeId.Attacker);
            var hh = BattleTestUtil.FreeHexNear(Open);
            var healer = Deploy(UnitTypeId.Healer, hh);
            var unit = Deploy(UnitTypeId.Attacker, FreeNeighbor(hh));
            yield return BattleTestUtil.Wait(0.3f);
            var original = new List<Soldier>(unit.Soldiers);
            original[0].Health.Kill(TeamColorId.Teal);
            yield return null;
            Assert.AreEqual(2, unit.AliveCount);
            float killed = Time.time;
            float interval = healer.Type.reviveInterval;
            Soldier revived = null;
            float revivedFraction = 0f;
            while (Time.time < killed + interval + 2f && revived == null)
            {
                yield return null;
                foreach (var s in unit.Soldiers)
                    if (s != null && s.IsAlive && !original.Contains(s)) { revived = s; revivedFraction = s.Health.Fraction; }
            }
            Assert.IsNotNull(revived, "a soldier came back within reviveInterval + 2 s");
            float took = Time.time - killed;
            Debug.Log($"[Test] revive after {took:0.00} s at {revivedFraction:0.000} health");
            Assert.GreaterOrEqual(took, interval - 0.1f, "not before reviveInterval");
            Assert.AreEqual(0.5f, revivedFraction, 0.05f, "back at about half health");
            Assert.AreEqual(3, unit.AliveCount, "the unit has 3 living soldiers again");
            Assert.AreEqual(1, healer.Healer.Revives);
            Assert.IsTrue(UnitRegistry.Of(Team.Player).Contains(revived.Health), "registered as a target");
            Assert.AreEqual(revived.transform.parent, unit.transform, "it belongs to the unit");

            // Ticked: it settles on a slot of the unit's hex. Shown by the pips.
            yield return BattleTestUtil.Wait(1f);
            Assert.IsTrue(BattleTestUtil.AllWithin(unit, unit.Hex), "the revived soldier stands on the unit's hex");
            var hud = TactileWars.UI.BattleHUD.Instance;
            if (hud != null && hud.Badges != null) Assert.AreEqual(3, hud.Badges.For(unit).FilledPips, "three pips again");
            // Counted on a pad and cleaned up like the others: move to the pad, count 3, then the unit dies whole.
            var cp = BattleManager.Instance.capturePoints[0];
            Assert.IsTrue(Army.OrderMove(unit, BattleTestUtil.PadHex(cp)));
            yield return BattleTestUtil.WaitArrive(unit, 12f);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual(3, cp.PlayersInside, "the revived soldier counts on the pad");
            BattleTestUtil.KillAll(unit);
            yield return null;
            Assert.IsTrue(unit.IsDestroyed);
            yield return BattleTestUtil.Wait(1.5f);
            Assert.IsTrue(unit == null, "the destroyed unit, revived soldier included, is cleaned up");
        }

        [UnityTest]
        public IEnumerator HealerDoesNotReviveDestroyedUnit()
        {
            yield return Setup(UnitTypeId.Healer, UnitTypeId.Attacker);
            var hh = BattleTestUtil.FreeHexNear(Open);
            var healer = Deploy(UnitTypeId.Healer, hh);
            var unit = Deploy(UnitTypeId.Attacker, FreeNeighbor(hh));
            yield return BattleTestUtil.Wait(0.3f);
            BattleTestUtil.KillAll(unit);
            yield return null;
            Assert.IsTrue(unit.IsDestroyed);
            yield return BattleTestUtil.Wait(healer.Type.reviveInterval + 2f);
            Assert.AreEqual(0, healer.Healer.Revives, "a destroyed unit stays destroyed");
            CollectionAssert.DoesNotContain((System.Collections.ICollection)new List<PlayerUnit>(Army.Units), unit);
            Assert.AreEqual(EntryState.Destroyed, Army.Squad[1].state);
            Assert.AreEqual(3, Army.AliveCount, "only the Healer's soldiers live");
        }

        [UnityTest]
        public IEnumerator HealerAloneDoesNothingAndLogsNoErrors()
        {
            yield return Setup(UnitTypeId.Healer);
            var healer = Deploy(UnitTypeId.Healer, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(2f);
            Assert.AreEqual(0f, healer.Healer.TotalHealed, "nobody to heal");
            Assert.AreEqual(3, healer.AliveCount);
            foreach (var s in healer.Soldiers) Assert.AreEqual(s.Health.max, s.Health.Current);

            // It does not bring back its own dead.
            healer.Soldiers[0].Health.Kill(TeamColorId.Teal);
            yield return BattleTestUtil.Wait(healer.Type.reviveInterval + 2f);
            Assert.AreEqual(2, healer.AliveCount, "a Healer does not revive its own soldiers");
            Assert.AreEqual(0, healer.Healer.Revives);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HealerNeverFires()
        {
            yield return Setup(UnitTypeId.Healer);
            var healer = Deploy(UnitTypeId.Healer, BattleTestUtil.FreeHexNear(Open));
            yield return null;
            var squad = BattleTestUtil.SpawnEnemySquad(healer.Centroid + new Vector3(0f, 0f, 4f), 1, false, true);
            yield return null;
            yield return null;
            var enemy = FirstMember(squad);
            Assert.IsNotNull(enemy);
            float range = healer.Soldiers[0].Config.weapon != null ? healer.Soldiers[0].Config.weapon.range : 0f;
            Assert.Less((enemy.transform.position - healer.Centroid).magnitude, range, "the enemy is within weapon range");
            float hp = enemy.Health.Current;
            yield return BattleTestUtil.Wait(3f);
            Assert.AreEqual(hp, enemy.Health.Current, 1e-4f, "the Healer never damages the enemy");
            foreach (var s in healer.Soldiers) Assert.IsTrue(s.HoldFire, "Healer soldiers hold fire");
        }

        // ------------------------------------------------------------------ Sniper

        [UnityTest]
        public IEnumerator SniperOutrangesAttacker()
        {
            yield return Setup(UnitTypeId.Attacker, UnitTypeId.Sniper);
            var g = Army.Grid;
            var squad = BattleTestUtil.SpawnEnemySquad(Open + new Vector3(0f, 0f, 8f), 1, false, true);
            yield return null;
            yield return null;
            var enemy = FirstMember(squad);
            Vector3 e = enemy.transform.position;
            float attackerRange = Army.soldierConfig.weapon.range;
            float want = attackerRange * 1.7f;
            HexCoord hex = default;
            float best = float.MaxValue;
            foreach (var c in g.Cells)
            {
                Vector3 d = g.Center(c) - e; d.y = 0f;
                if (!g.IsFree(c) || d.z > -3f) continue; // south of the enemy, down the lane
                float err = Mathf.Abs(d.magnitude - want);
                if (err < best) { best = err; hex = c; }
            }
            Assert.Less(best, 0.6f, "a hex at 1.7 times the Attacker range");

            var attacker = Deploy(UnitTypeId.Attacker, hex);
            float hp = enemy.Health.Current;
            yield return BattleTestUtil.Wait(5f);
            Debug.Log($"[Test] enemy {Vector3.Distance(attacker.Centroid, e):0.00} from the Attacker unit (range {attackerRange}): health {hp} -> {enemy.Health.Current}");
            Assert.AreEqual(hp, enemy.Health.Current, 1e-4f, "out of the Attacker's range");
            BattleTestUtil.KillAll(attacker);
            yield return BattleTestUtil.Wait(0.6f);

            var sniper = Deploy(UnitTypeId.Sniper, hex);
            float sniperRange = sniper.Soldiers[0].Config.weapon.range;
            Assert.AreEqual(attackerRange * 2f, sniperRange, 1e-3f, "twice the Attacker's range");
            yield return BattleTestUtil.Wait(5f);
            Debug.Log($"[Test] Sniper at {Vector3.Distance(sniper.Centroid, e):0.00}: enemy health {hp} -> {(enemy != null ? enemy.Health.Current : 0f)}");
            Assert.IsTrue(enemy == null || enemy.Health.Current < hp, "the Sniper's shots reach and damage it");
        }

        // ------------------------------------------------------------------ Vanguard

        static IEnumerator CaptureOverTwoSeconds(UnitTypeId type, CapturePoint cp, float[] result)
        {
            var u = Deploy(type, BattleTestUtil.PadHex(cp));
            yield return BattleTestUtil.Wait(0.25f);
            // Two halves of a second each, so neither unit completes the capture mid measure.
            float made = 0f;
            for (int half = 0; half < 2; half++)
            {
                cp.SetProgress(0f);
                yield return BattleTestUtil.Wait(1f);
                Assert.Less(cp.Progress, 1f);
                made += cp.Progress;
            }
            result[0] = made;
            result[1] = cp.PlayersInside;
            result[2] = cp.CaptureWeightInside;
            BattleTestUtil.KillAll(u);
            yield return BattleTestUtil.Wait(0.6f);
            cp.SetProgress(0f);
        }

        [UnityTest]
        public IEnumerator VanguardCapturesFasterThanAttacker()
        {
            yield return Setup(UnitTypeId.Attacker, UnitTypeId.Vanguard);
            var cp = BattleManager.Instance.capturePoints[0];
            var a = new float[3];
            var v = new float[3];
            yield return CaptureOverTwoSeconds(UnitTypeId.Attacker, cp, a);
            yield return CaptureOverTwoSeconds(UnitTypeId.Vanguard, cp, v);
            Debug.Log($"[Test] capture progress in 2 s: Attacker {a[0]:0.000} (inside {a[1]}, weight {a[2]}), Vanguard {v[0]:0.000} (inside {v[1]}, weight {v[2]})");
            Assert.AreEqual(3, (int)a[1]);
            Assert.AreEqual(3, (int)v[1], "three Vanguard soldiers inside");
            Assert.AreEqual(6, (int)v[2], "each counts twice toward the bonus");
            Assert.Greater(v[0], a[0] * 1.2f, "the Vanguard captures clearly faster");
        }

        // ------------------------------------------------------------------ Demolisher

        static GameObject SpawnTurret(Vector3 at)
        {
            var prefab = MetaDatabase.Load().defenses.Get("turret");
            Assert.IsNotNull(prefab, "catalog turret");
            return Object.Instantiate(prefab, at, Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>Waits for the first damage 'h' takes and returns its size in result[0] (0 when none in time).</summary>
        static IEnumerator FirstHit(Health h, float seconds, float[] result)
        {
            float got = 0f;
            System.Action<Health, float, Vector3, TeamColorId> on = (x, dmg, from, by) => { if (got == 0f) got = dmg; };
            h.Damaged += on;
            float end = Time.time + seconds;
            while (Time.time < end && got == 0f) yield return null;
            h.Damaged -= on;
            result[0] = got;
        }

        [UnityTest]
        public IEnumerator DemolisherHurtsStructuresMoreThanSoldiers()
        {
            yield return Setup(UnitTypeId.Demolisher);
            var demo = Deploy(UnitTypeId.Demolisher, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(0.5f);
            float range = demo.Soldiers[0].Config.weapon.range;
            Assert.AreEqual(Army.soldierConfig.weapon.range * 1.3f, range, 1e-3f);
            Assert.Greater(demo.Soldiers[0].Config.weapon.arcHeight, 0f, "lobbed shells");

            var turret = SpawnTurret(demo.Centroid + new Vector3(0f, 0f, range - 1.5f));
            var th = turret.GetComponent<StructureBase>().Health;
            var r = new float[1];
            yield return FirstHit(th, 8f, r);
            float onStructure = r[0];
            Object.Destroy(turret);
            yield return BattleTestUtil.Wait(0.5f);

            var squad = BattleTestUtil.SpawnEnemySquad(demo.Centroid + new Vector3(0f, 0f, range - 1.5f), 1, false, true);
            yield return null;
            yield return null;
            var grunt = FirstMember(squad);
            grunt.Health.Init(Team.Enemy, grunt.Health.color, th.max); // equal max health
            yield return FirstHit(grunt.Health, 8f, r);
            float onSoldier = r[0];
            Debug.Log($"[Test] one Demolisher shell: turret -{onStructure:0.0} of {th.max}, soldier -{onSoldier:0.0} of {grunt.Health.max}");
            Assert.Greater(onStructure, 0f, "a shell hit the turret");
            Assert.Greater(onSoldier, 0f, "a shell hit the soldier");
            Assert.GreaterOrEqual(onStructure, onSoldier * 4f, "a shell hurts a structure at least 4 times as much");
        }

        [UnityTest]
        public IEnumerator DemolisherPrefersStructure()
        {
            yield return Setup(UnitTypeId.Demolisher);
            var demo = Deploy(UnitTypeId.Demolisher, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(0.5f);
            float range = demo.Soldiers[0].Config.weapon.range;
            // A soldier near, the turret farther off to the side, both in range and well apart (beyond the splash).
            var squad = BattleTestUtil.SpawnEnemySquad(demo.Centroid + new Vector3(-2f, 0f, 3.2f), 1, false, true);
            var turret = SpawnTurret(demo.Centroid + new Vector3(3.5f, 0f, range - 1.6f));
            yield return null;
            yield return null;
            var grunt = FirstMember(squad);
            var th = turret.GetComponent<StructureBase>().Health;
            float dS = Vector3.Distance(grunt.transform.position, demo.Centroid), dT = Vector3.Distance(turret.transform.position, demo.Centroid);
            Assert.Less(dS, dT, "the soldier is nearer");
            Assert.Greater(Vector3.Distance(grunt.transform.position, turret.transform.position), 1.8f + 1.2f, "beyond one splash");
            string first = null;
            System.Action<Health, float, Vector3, TeamColorId> onT = (x, d, f, b) => { if (first == null) first = "turret"; };
            System.Action<Health, float, Vector3, TeamColorId> onS = (x, d, f, b) => { if (first == null) first = "soldier"; };
            th.Damaged += onT;
            grunt.Health.Damaged += onS;
            float end = Time.time + 8f;
            while (Time.time < end && first == null) yield return null;
            th.Damaged -= onT;
            grunt.Health.Damaged -= onS;
            Debug.Log($"[Test] soldier at {dS:0.0}, turret at {dT:0.0}: first shell on the {first}");
            Assert.AreEqual("turret", first, "the first shell lands on the turret");
            foreach (var s in demo.Soldiers) if (s.IsAlive && s.Targeting.Current != null) Assert.AreSame(th, s.Targeting.Current, "every Demolisher soldier aims at the turret");
            Object.Destroy(turret);
        }

        /// <summary>Gives every soldier of the unit a huge health pool, so a measurement never loses one.</summary>
        static void Invulnerable(PlayerUnit u)
        {
            foreach (var s in u.Soldiers) if (s != null && s.IsAlive) s.Health.Init(Team.Player, s.Health.color, 1e6f);
        }

        /// <summary>Seconds one unit of 'type' (alone on the lane) needs to destroy a silenced turret 5 units in front
        /// of it; result[0] is -1 when the turret still stands after 'limit' seconds.</summary>
        static IEnumerator TimeToKillTurret(UnitTypeId type, float limit, float[] result)
        {
            yield return Setup(type);
            var u = Deploy(type, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(0.5f);
            var turret = SpawnTurret(u.Centroid + new Vector3(0f, 0f, 5f));
            var sb = turret.GetComponent<StructureBase>();
            yield return null; // let it Start (health and registration) before silencing it
            sb.enabled = false; // it never shoots back, so both units keep all their soldiers
            float t0 = Time.time;
            while (Time.time < t0 + limit && sb.Health.IsAlive) yield return null;
            result[0] = sb.Health.IsAlive ? -1f : Time.time - t0;
        }

        /// <summary>Seconds one unit of 'type' (made invulnerable) needs to kill a held squad of four enemy soldiers 5
        /// units in front of it; result[0] is -1 when one still lives after 'limit' seconds.</summary>
        static IEnumerator TimeToKillSquad(UnitTypeId type, float limit, float[] result)
        {
            yield return Setup(type);
            var u = Deploy(type, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(0.5f);
            var squad = BattleTestUtil.SpawnEnemySquad(u.Centroid + new Vector3(0f, 0f, 5f), 4, false, true);
            yield return null;
            yield return null;
            var grunts = new List<Soldier>(squad.GetComponentsInChildren<Soldier>());
            Assert.AreEqual(4, grunts.Count, "four enemy soldiers");
            Invulnerable(u); // the squad shoots back: keep every soldier of the measured unit alive
            float t0 = Time.time;
            bool AnyAlive() { foreach (var g in grunts) if (g != null && g.IsAlive) return true; return false; }
            while (Time.time < t0 + limit && AnyAlive()) yield return null;
            result[0] = AnyAlive() ? -1f : Time.time - t0;
        }

        [UnityTest]
        public IEnumerator DemolisherDestroysTurretMuchFasterThanAttacker()
        {
            var a = new float[1];
            var d = new float[1];
            yield return TimeToKillTurret(UnitTypeId.Attacker, 90f, a);
            yield return TimeToKillTurret(UnitTypeId.Demolisher, 90f, d);
            Debug.Log($"[Test] turret destroyed by one Attacker unit in {a[0]:0.00}s, by one Demolisher unit in {d[0]:0.00}s (ratio {d[0] / a[0]:0.00})");
            Assert.Greater(a[0], 0f, "the Attacker unit destroys the turret");
            Assert.Greater(d[0], 0f, "the Demolisher unit destroys the turret");
            Assert.LessOrEqual(d[0], a[0] * 0.4f, "a Demolisher unit needs at most 40 percent of an Attacker unit's time");
        }

        [UnityTest]
        public IEnumerator DemolisherIsSlowAgainstSoldiers()
        {
            var a = new float[1];
            var d = new float[1];
            yield return TimeToKillSquad(UnitTypeId.Attacker, 90f, a);
            yield return TimeToKillSquad(UnitTypeId.Demolisher, 90f, d);
            Debug.Log($"[Test] four soldiers killed by one Attacker unit in {a[0]:0.00}s, by one Demolisher unit in {d[0]:0.00}s (ratio {d[0] / a[0]:0.00})");
            Assert.Greater(a[0], 0f, "the Attacker unit kills the squad");
            Assert.Greater(d[0], 0f, "the Demolisher unit still kills the squad, slowly");
            Assert.GreaterOrEqual(d[0], a[0] * 1.5f, "a Demolisher unit needs at least 1.5 times an Attacker unit's time against soldiers");
        }

        [UnityTest]
        public IEnumerator DemolisherShellsNeverHurtPlayerUnits()
        {
            yield return Setup(UnitTypeId.Demolisher, UnitTypeId.Attacker);
            var demo = Deploy(UnitTypeId.Demolisher, BattleTestUtil.FreeHexNear(Open));
            yield return BattleTestUtil.Wait(0.5f);
            float range = demo.Soldiers[0].Config.weapon.range;
            Vector3 at = demo.Centroid + new Vector3(0f, 0f, range - 1.5f);
            // A friendly unit right next to the target, inside the splash.
            var friend = Deploy(UnitTypeId.Attacker, BattleTestUtil.FreeHexNear(at + new Vector3(0f, 0f, -1.4f), demo.Hex));
            foreach (var s in friend.Soldiers) s.HoldFire = true;
            yield return null;
            // The target stands 1.2 north of the friendly soldier nearest the Demolisher's target area.
            Soldier close = null;
            foreach (var s in friend.Soldiers) if (close == null || s.transform.position.z > close.transform.position.z) close = s;
            at = close.transform.position + new Vector3(0f, 0f, 1.2f);
            at.y = 0f;
            Assert.Less(Vector3.Distance(at, demo.Centroid), range, "the target is in the Demolisher's range");
            var turret = SpawnTurret(at);
            var sb = turret.GetComponent<StructureBase>();
            yield return null; // let it Start (health and registration) before silencing it
            sb.enabled = false; // a silent target: any friendly damage could only come from the shells
            float nearest = float.MaxValue;
            foreach (var s in friend.Soldiers) nearest = Mathf.Min(nearest, Vector3.Distance(s.transform.position, at));
            Assert.Less(nearest, 1.5f, "a friendly soldier stands well inside the splash");
            float friendly = 0f;
            System.Action<Health, float, Vector3, TeamColorId> on = (x, d, f, b) => friendly += d;
            foreach (var s in friend.Soldiers) s.Health.Damaged += on;
            foreach (var s in demo.Soldiers) s.Health.Damaged += on;
            var r = new float[1];
            yield return FirstHit(sb.Health, 8f, r);
            yield return BattleTestUtil.Wait(3f);
            Debug.Log($"[Test] friendly soldier {nearest:0.0} from the target: friendly damage {friendly}, first shell on the turret {r[0]:0.0}");
            Assert.Greater(r[0], 0f, "shells land on the target");
            Assert.AreEqual(0f, friendly, "a player shell never damages player units");
            Object.Destroy(turret);
        }
    }
}
