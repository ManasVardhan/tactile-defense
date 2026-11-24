using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>Phase 8: samples the soldier animators during a scripted fight and checks that Idle, Run, Shoot, Hit,
    /// Die and Cheer each actually play at the right moment (cheer is triggered by a capture).</summary>
    public class AnimationStateTests
    {
        static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
        static readonly int Die = Animator.StringToHash("Base Layer.Die");
        static readonly int Cheer = Animator.StringToHash("Base Layer.Cheer");
        static readonly int Shoot = Animator.StringToHash("Actions.Shoot");
        static readonly int Hit = Animator.StringToHash("Actions.Hit");
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [UnityTearDown]
        public IEnumerator TearDown() { BattleTestUtil.Cleanup(); yield return null; }

        readonly Dictionary<string, int> seen = new Dictionary<string, int>();

        void Count(string k) { seen.TryGetValue(k, out int v); seen[k] = v + 1; }

        void Sample(bool expectRest)
        {
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None))
            {
                var a = s.animator;
                if (a == null || !a.isActiveAndEnabled) continue;
                var b = a.GetCurrentAnimatorStateInfo(0);
                var act = a.GetCurrentAnimatorStateInfo(1);
                if (b.fullPathHash == Locomotion)
                {
                    float sp = a.GetFloat(SpeedId);
                    if (sp > 0.3f) Count("Run");
                    else if (sp < 0.05f && expectRest) Count("Idle");
                }
                if (b.fullPathHash == Die) Count("Die");
                if (b.fullPathHash == Cheer) Count("Cheer");
                if (act.fullPathHash == Shoot) Count("Shoot");
                if (act.fullPathHash == Hit) Count("Hit");
            }
        }

        [UnityTest]
        public IEnumerator AnimatorStatesPlayAtTheRightMoments()
        {
            yield return BattleTestUtil.LoadBattle();
            // Level 1 keeps its enemies up the lane; stand two squads where base one's first squads were.
            BattleTestUtil.SpawnEnemySquad(new Vector3(-8.5f, 0f, 21.5f), 10, true, false);
            BattleTestUtil.SpawnEnemySquad(new Vector3(8.5f, 0f, 28f), 10, true, false, true);
            yield return null;
            var army = BattleTestUtil.Army;
            var units = BattleTestUtil.DeployAll();
            yield return BattleTestUtil.Wait(1.0f);
            Sample(true);
            int idleAtStart = seen.TryGetValue("Idle", out int i0) ? i0 : 0;

            // Walk every unit a few hexes forward.
            var taken = new List<HexCoord>();
            foreach (var u in units)
            {
                var dest = BattleTestUtil.FreeHexNear(army.Grid.Center(u.Hex) + new Vector3(0f, 0f, 6f), taken.ToArray());
                Assert.IsTrue(army.OrderMove(u, dest));
                taken.Add(dest);
            }
            float end = Time.time + 1.2f;
            while (Time.time < end) { Sample(false); yield return null; }
            int runs = seen.TryGetValue("Run", out int r0) ? r0 : 0;

            // Fight: advance on the first enemy squads, sample for a while.
            taken.Clear();
            foreach (var u in units)
            {
                if (u == null || u.IsDestroyed) continue;
                var dest = BattleTestUtil.FreeHexNear(new Vector3(0f, 0, 15.5f), taken.ToArray());
                army.OrderMove(u, dest);
                taken.Add(dest);
            }
            // Sample at least 9 s, and on until a soldier has died (the wider unit spacing of the bigger soldiers
            // can push the first kill a little past 9 s), up to 25 s.
            float start = Time.time;
            while (Time.time < start + 9f || (!seen.ContainsKey("Die") && Time.time < start + 25f)) { Sample(false); yield return null; }

            // Capture makes the squad cheer.
            BattleManager.Instance.capturePoints[0].ForceCapture();
            end = Time.time + 0.8f;
            while (Time.time < end) { Sample(false); yield return null; }

            string report = "";
            foreach (var kv in seen) report += kv.Key + "=" + kv.Value + " ";
            Debug.Log("[AnimProbe] idle at start " + idleAtStart + ", run samples while moving " + runs + ", totals: " + report);
            Assert.Greater(idleAtStart, 10, "Idle should play while the units stand at the start");
            Assert.Greater(runs, 10, "Run should play while the units move");
            foreach (var k in new[] { "Shoot", "Hit", "Die", "Cheer" })
                Assert.IsTrue(seen.ContainsKey(k), k + " never played. Seen: " + report);
        }
    }
}
