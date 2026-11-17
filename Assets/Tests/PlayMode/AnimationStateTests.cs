using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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
            yield return BattleTestUtil.Wait(1.0f);
            Sample(true);
            int idleAtStart = seen.TryGetValue("Idle", out int i0) ? i0 : 0;

            var squad = BattleTestUtil.Squad;
            Vector3 c = squad.Centroid;
            yield return BattleTestUtil.Drag(BattleTestUtil.LinePoints(c + new Vector3(-6f, 0, 9f), c + new Vector3(6f, 0, 10f)), 0.5f);
            float end = Time.time + 1.2f;
            while (Time.time < end) { Sample(false); yield return null; }
            int runs = seen.TryGetValue("Run", out int r0) ? r0 : 0;

            // Fight: arc toward the first enemy squads, sample for a while.
            var arc = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Lerp(160f, 20f, i / 24f) * Mathf.Deg2Rad;
                arc.Add(new Vector3(0f, 0, 15.5f) + new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 3.0f));
            }
            yield return BattleTestUtil.Drag(arc.ToArray(), 0.6f);
            end = Time.time + 9f;
            while (Time.time < end) { Sample(false); yield return null; }

            // Capture makes the squad cheer.
            BattleManager.Instance.capturePoints[0].ForceCapture();
            end = Time.time + 0.8f;
            while (Time.time < end) { Sample(false); yield return null; }

            string report = "";
            foreach (var kv in seen) report += kv.Key + "=" + kv.Value + " ";
            Debug.Log("[AnimProbe] idle at start " + idleAtStart + ", run samples while moving " + runs + ", totals: " + report);
            Assert.Greater(idleAtStart, 10, "Idle should play while the squad stands at the start");
            Assert.Greater(runs, 10, "Run should play while the squad moves to a new line");
            foreach (var k in new[] { "Shoot", "Hit", "Die", "Cheer" })
                Assert.IsTrue(seen.ContainsKey(k), k + " never played. Seen: " + report);
        }
    }
}
