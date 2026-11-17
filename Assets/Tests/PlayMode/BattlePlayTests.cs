using System.Collections;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    public class BattlePlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleTestUtil.Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragStroke_SoldiersReachTheirSlots()
        {
            yield return BattleTestUtil.LoadBattle();
            var squad = BattleTestUtil.Squad;
            Assert.IsNotNull(squad, "SquadController in scene");
            yield return BattleTestUtil.Wait(0.5f);
            int n = squad.Soldiers.Count;
            Assert.AreEqual(40, n, "default squad size");

            Vector3 c = squad.Centroid;
            var stroke = BattleTestUtil.LinePoints(c + new Vector3(-15f, 0, 3.5f), c + new Vector3(15f, 0, 3.5f), 40);
            yield return BattleTestUtil.Drag(stroke, 0.8f);
            Assert.AreEqual(ShapeType.Line, squad.LastShape, "a straight drag is recognized as a line");
            Assert.AreEqual(n, squad.SlotCount, "one slot per soldier");

            float deadline = Time.time + 6f;
            int near = 0;
            while (Time.time < deadline)
            {
                near = 0;
                for (int i = 0; i < squad.Soldiers.Count; i++)
                {
                    var s = squad.Soldiers[i];
                    Vector3 d = s.transform.position - s.Motor.Target; d.y = 0;
                    if (d.magnitude < 0.35f) near++;
                }
                if (near >= n) break;
                yield return null;
            }
            Debug.Log($"[Test] soldiers at slot: {near}/{n} after {6f - (deadline - Time.time):0.00}s");
            Assert.GreaterOrEqual(near, Mathf.CeilToInt(n * 0.95f), "soldiers end up near their slots");

            // Slots lie on the drawn line (z constant) and are spread along it.
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in squad.Soldiers)
            {
                Assert.AreEqual(c.z + 3.5f, s.Motor.Target.z, 0.05f, "slot on the stroke");
                minX = Mathf.Min(minX, s.Motor.Target.x); maxX = Mathf.Max(maxX, s.Motor.Target.x);
            }
            Assert.Greater(maxX - minX, 25f, "spread single file along the stroke");

            // A short stroke cannot hold everyone single file: extra rows form behind it.
            c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-4f, 0, 3f), c + new Vector3(4f, 0, 3f)), 0.5f);
            int behind = 0;
            foreach (var s in squad.Soldiers) if (s.Motor.Target.z < c.z + 3f - 0.3f) behind++;
            Assert.Greater(behind, 0, "rows behind a short stroke");
            Assert.AreEqual(n, squad.SlotCount);
        }

        [UnityTest]
        public IEnumerator AdvanceIntoEnemySquad_FiresAndDamagesEnemies()
        {
            yield return BattleTestUtil.LoadBattle();
            var squad = BattleTestUtil.Squad;
            yield return BattleTestUtil.Wait(0.3f);
            int firedBefore = ProjectileManager.Instance.TotalFired;
            float enemyHpBefore = TotalEnemyHp(out int enemiesBefore);
            Assert.Greater(enemiesBefore, 0);

            // Tap ahead toward the first enemy squad (left of the path near z 21).
            yield return BattleTestUtil.Tap(new Vector3(-6.5f, 0, 14.5f));
            float deadline = Time.time + 12f;
            while (Time.time < deadline)
            {
                if (BattleManager.Instance.Kills >= 2) break;
                yield return null;
            }
            int fired = ProjectileManager.Instance.TotalFired - firedBefore;
            float hpAfter = TotalEnemyHp(out int enemiesAfter);
            int kills = BattleManager.Instance.Kills;
            Debug.Log($"[Test] projectiles fired {fired}, hits {ProjectileManager.Instance.TotalHits}, enemy hp {enemyHpBefore:0} -> {hpAfter:0}, kills {kills}, splats {PaintSplatManager.Instance.Count}");
            Assert.Greater(fired, 10, "projectiles are fired");
            Assert.IsTrue(hpAfter < enemyHpBefore || kills > 0, "enemies take damage");
            Assert.Greater(PaintSplatManager.Instance.Count, 0, "paint splats on the ground");
        }

        [UnityTest]
        public IEnumerator SquadOnCapturePoint_ProgressIncreases()
        {
            yield return BattleTestUtil.LoadBattle();
            var squad = BattleTestUtil.Squad;
            var bm = BattleManager.Instance;
            yield return BattleTestUtil.Wait(0.2f);
            var cp = bm.capturePoints[0];
            Assert.IsTrue(cp.Active, "first point active");
            Assert.IsFalse(bm.capturePoints[1].Active, "second point locked");
            Assert.AreEqual(0f, cp.Progress);

            // Tight circle formation right on the pad.
            squad.MoveFormationTo(cp.transform.position);
            squad.TeleportToSlots();
            float start = Time.time;
            yield return BattleTestUtil.Wait(1.5f);
            Debug.Log($"[Test] capture progress after {Time.time - start:0.00}s: {cp.Progress:0.00} inside={cp.PlayersInside} contested={cp.Contested}");
            Assert.Greater(cp.PlayersInside, 0, "soldiers inside the pad");
            Assert.Greater(cp.Progress, 0.15f, "capture progress increases");
        }

        [UnityTest]
        public IEnumerator FlagBearerBubble_TapWipesSquad()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.3f);
            var squads = Object.FindObjectsByType<TactileWars.Units.EnemySquad>(FindObjectsSortMode.None);
            Assert.Greater(squads.Length, 0);
            var sq = squads[0];
            int aliveBefore = sq.AliveCount;
            Assert.Greater(aliveBefore, 0);
            sq.Bearer.Health.Kill(TeamColorId.Red);
            yield return BattleTestUtil.Wait(0.2f);
            Assert.IsTrue(sq.Bubble.CanTap, "bubble left behind and tappable");
            yield return BattleTestUtil.Tap(sq.Bubble.transform.position + Vector3.up * 0.8f);
            yield return BattleTestUtil.Wait(0.6f);
            Assert.AreEqual(0, sq.AliveCount, "tapping the bubble wipes the squad");
            Assert.IsTrue(sq.Wiped);
        }

        [UnityTest]
        public IEnumerator Reinforcements_AddSoldiersWhenMeterFull()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.2f);
            var bm = BattleManager.Instance;
            int before = BattleTestUtil.Squad.Soldiers.Count;
            Assert.IsFalse(bm.CallReinforcements(), "meter starts empty");
            bm.Meter.Add(1000f);
            BattleTestUtil.Pointer.Reinforce = true; // keyboard R path through the pointer source
            yield return null;
            yield return null;
            Assert.AreEqual(before + bm.battleConfig.reinforcementCount, BattleTestUtil.Squad.Soldiers.Count);
        }

        static float TotalEnemyHp(out int count)
        {
            float hp = 0f;
            var l = UnitRegistry.Of(Team.Enemy);
            count = 0;
            for (int i = 0; i < l.Count; i++)
            {
                if (l[i].kind == TargetKind.Structure) continue;
                hp += l[i].Current; count++;
            }
            return hp;
        }
    }
}
