using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
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
        public IEnumerator AdvanceIntoEnemySquad_FiresAndDamagesEnemies()
        {
            yield return BattleTestUtil.LoadBattle();
            // Level 1 keeps its enemies up the lane; stand a squad where base one's first squad was.
            BattleTestUtil.SpawnEnemySquad(new Vector3(-8.5f, 0f, 21.5f), 10, true, false);
            yield return BattleTestUtil.Wait(0.3f);
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            int firedBefore = ProjectileManager.Instance.TotalFired;
            float enemyHpBefore = TotalEnemyHp(out int enemiesBefore);
            Assert.Greater(enemiesBefore, 0);

            // Order every unit ahead toward the first enemy squad (left of the path near z 21).
            var taken = new List<HexCoord>();
            foreach (var u in units)
            {
                var dest = BattleTestUtil.FreeHexNear(new Vector3(-6.5f, 0, 14.5f), taken.ToArray());
                Assert.IsTrue(army.OrderMove(u, dest), "move toward the enemy accepted");
                taken.Add(dest);
            }
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
        public IEnumerator UnitOnCapturePoint_ProgressIncreases()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            var bm = BattleManager.Instance;
            var army = BattleTestUtil.Army;
            yield return BattleTestUtil.Wait(0.2f);
            var cp = bm.capturePoints[0];
            Assert.IsTrue(cp.Active, "first point active");
            Assert.IsFalse(bm.capturePoints[1].Active, "second point locked");
            Assert.AreEqual(0f, cp.Progress);

            // Deploy short of the pad and walk onto it.
            var pad = BattleTestUtil.PadHex(cp);
            var unit = army.Deploy(0, BattleTestUtil.FreeHexNear(cp.transform.position + new Vector3(0f, 0f, -9f), pad));
            Assert.IsNotNull(unit);
            Assert.AreEqual(0f, cp.Progress, "not on the pad yet");
            Assert.IsTrue(army.OrderMove(unit, pad));
            yield return BattleTestUtil.WaitArrive(unit, 10f);
            Assert.IsFalse(unit.IsMoving, "arrived on the pad");
            float start = Time.time;
            yield return BattleTestUtil.Wait(1.5f);
            Debug.Log($"[Test] capture progress after {Time.time - start:0.00}s on the pad: {cp.Progress:0.00} inside={cp.PlayersInside} contested={cp.Contested}");
            Assert.Greater(cp.PlayersInside, 0, "soldiers inside the pad");
            Assert.Greater(cp.Progress, 0.15f, "capture progress increases");
        }

        [UnityTest]
        public IEnumerator FlagBearerBubble_TapWipesSquad()
        {
            yield return BattleTestUtil.LoadBattle();
            // Level 1's only flag bearer squad is far up the lane; bring one into view (where base one's first squad stood).
            var sq = BattleTestUtil.SpawnEnemySquad(new Vector3(-8.5f, 0f, 21.5f), 10, true, false);
            yield return BattleTestUtil.Wait(0.3f);
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
        public IEnumerator Reinforcements_RestoreALostUnitWhenMeterFull()
        {
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.2f);
            var bm = BattleManager.Instance;
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            BattleTestUtil.KillAll(units[1]);
            yield return null;
            Assert.AreEqual(EntryState.Destroyed, army.Squad[1].state);
            Assert.AreEqual(0, army.ReadyCount);
            Assert.IsFalse(bm.CallReinforcements(), "meter starts empty");
            bm.Meter.Add(1000f);
            BattleTestUtil.Pointer.Reinforce = true; // keyboard R path through the pointer source
            yield return null;
            yield return null;
            Assert.AreEqual(EntryState.Ready, army.Squad[1].state, "the lost unit is back in the squad list");
            Assert.AreEqual(1, army.ReadyCount);
            Assert.IsFalse(bm.Meter.Ready, "the charge is used");
            Assert.IsNotNull(army.Deploy(1, BattleTestUtil.FreeHexNear(army.spawnPoint)), "and deploys again");
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
