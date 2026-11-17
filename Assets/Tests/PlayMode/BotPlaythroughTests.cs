using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>
    /// Phase 8 balance bot: plays an AI base start to finish through the real input path at accelerated time.
    /// Policy (a reasonable, not optimal, player): every half second it calls reinforcements when the meter is full,
    /// taps poppable flag bubbles near the squad, and if an enemy unit or structure is within reach it draws a slightly
    /// curved line facing it at about 85 percent of weapon range; otherwise it draws a ring on the active capture pad.
    /// Lines are redrawn when the target moves or every few seconds. Mines are hidden traps and are not avoided.
    /// Results go to Logs/bot_playthrough.txt.
    /// </summary>
    [Category("Bot")]
    public class BotPlaythroughTests
    {
        const float TimeScale = 3f;
        const float BattleLimit = 420f;   // game seconds

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            BattleLaunch.Clear();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        public struct Outcome
        {
            public string baseId, name;
            public bool victory, upgraded;
            public float time;
            public int start, reinforcements, lost, kills, structures, taps, redraws, fallbacks, captured, minesStart, minesTriggered, mineDeaths, otherDeaths;
            public override string ToString() =>
                $"{baseId} {name} ({(upgraded ? "researched loadout" : "no research")}): {(victory ? "VICTORY" : "DEFEAT")} in {time:0}s game time, " +
                $"start squad {start}, reinforcements {reinforcements}, soldiers lost {lost} ({(start > 0 ? 100f * lost / start : 0f):0}% of the starting squad, {(start + reinforcements > 0 ? 100f * lost / (start + reinforcements) : 0f):0}% of all deployed), " +
                $"points captured {captured}, kills {kills}, structures {structures}, mines triggered {minesTriggered}/{minesStart} (player deaths near a mine burst {mineDeaths}, other deaths {otherDeaths}), bubble taps {taps}, formations drawn {redraws} (fallbacks {fallbacks})";
        }

        static readonly List<string> log = new List<string>();

        [UnityTest] public IEnumerator Base01_NoResearch() { yield return Run(0, false); }
        [UnityTest] public IEnumerator Base05_Researched() { yield return Run(4, true); }
        [UnityTest] public IEnumerator Base10_Researched() { yield return Run(9, true); }
        [UnityTest] public IEnumerator Base10_NoResearch() { yield return Run(9, false); }

        static void Prepare(BaseDefinition b, bool upgrade)
        {
            var p = MetaGame.Profile;
            p.teamChosen = true;
            p.tutorial.drawStroke = true;
            p.tutorial.tapToMove = true;
            p.level = Mathf.Max(1, b.requiredLevel);
            if (upgrade)
            {
                // A player at this base's required level who spent on squad research (troops and offense branches).
                p.gold = 1000000;
                var tree = MetaGame.Db.research;
                bool bought = true;
                while (bought)
                {
                    bought = false;
                    foreach (var n in tree.nodes)
                        if (n.branch != ResearchBranch.Defense && MetaGame.BuyResearch(n) == PurchaseResult.Ok) bought = true;
                }
            }
            p.gold = 0;
            BattleLaunch.Prepare(b);
        }

        IEnumerator Run(int index, bool upgrade)
        {
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var b = MetaGame.Db.roster.bases[index];
            Prepare(b, upgrade);
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.5f);
            var o = new Outcome { baseId = b.id, name = b.displayName, upgraded = upgrade };
            yield return Play(o, r => o = r);
            string line = o.ToString();
            log.Add(line);
            Debug.Log("[Bot] " + line);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/bot_playthrough.txt"));
            File.AppendAllText(path, System.DateTime.Now.ToString("HH:mm") + " " + line + "\n");
            if (index == 0)
            {
                Assert.IsTrue(o.victory, "base 1 must be winnable by the bot: " + line);
                Assert.Less(o.lost, o.start / 2, "base 1: lose well under half the starting squad: " + line);
            }
        }

        static IEnumerator Play(Outcome o, System.Action<Outcome> done)
        {
            var bm = BattleManager.Instance;
            var squad = BattleTestUtil.Squad;
            var pointer = BattleTestUtil.Pointer;
            o.start = squad.StartCount;
            var mines = new List<Structures.Mine>(Object.FindObjectsByType<Structures.Mine>(FindObjectsSortMode.None));
            o.minesStart = mines.Count;
            var mineAlive = new bool[mines.Count];
            for (int i = 0; i < mines.Count; i++) mineAlive[i] = true;
            var bursts = new List<Vector4>(); // xyz position, w game time
            int mineDeaths = 0, otherDeaths = 0;
            System.Action<Team, TargetKind, Vector3> onKilled = (team, kind, pos) =>
            {
                if (team != Team.Player) return;
                bool nearBurst = false;
                foreach (var b in bursts) if (Time.time - b.w < 0.9f && Flat(pos - (Vector3)b).magnitude < 3.2f) { nearBurst = true; break; }
                if (nearBurst) mineDeaths++; else otherDeaths++;
            };
            void PollMines()
            {
                for (int i = 0; i < mines.Count; i++)
                {
                    if (!mineAlive[i]) continue;
                    if (mines[i] == null || !mines[i].gameObject.activeInHierarchy)
                    {
                        mineAlive[i] = false;
                        Vector3 mp = mines[i] != null ? mines[i].transform.position : Vector3.zero;
                        bursts.Add(new Vector4(mp.x, mp.y, mp.z, Time.time));
                    }
                }
            }
            int reinforcedSoldiers = 0;
            System.Action<int> onReinforced = n => reinforcedSoldiers += n;
            BattleEvents.Reinforced += onReinforced;
            BattleEvents.UnitKilled += onKilled;
            var ignored = new HashSet<Health>();
            Health engaged = null;
            float engagedSince = 0f;
            Time.timeScale = TimeScale;
            float weaponRange = squad.soldierConfig != null && squad.soldierConfig.weapon != null ? squad.soldierConfig.weapon.range : 6.5f;
            Vector3 lastCenter = new Vector3(9999f, 0f, 0f);
            float lastDraw = -99f;
            int reinforcementsCalled = 0;
            var gc = BattleContext.Config;
            while (!bm.Over && bm.Elapsed < BattleLimit)
            {
                // 1. Reinforcements when the meter is full.
                if (bm.Meter.Ready)
                {
                    pointer.Reinforce = true;
                    yield return null;
                    yield return null;
                    reinforcementsCalled++;
                }

                Vector3 c = squad.Centroid;
                // 2. Pop flag bubbles near the squad.
                foreach (var es in Object.FindObjectsByType<EnemySquad>(FindObjectsSortMode.None))
                {
                    var bub = es.Bubble;
                    if (bub == null || !bub.gameObject.activeInHierarchy || !bub.Poppable) continue;
                    Vector3 bp = bub.transform.position;
                    if (Flat(bp - c).magnitude > 20f) continue;
                    yield return BattleTestUtil.Tap(bp + Vector3.up * 0.2f);
                    if (bub.gameObject.activeInHierarchy && bub.Poppable) { bub.OnTapped(); o.fallbacks++; }
                    o.taps++;
                }

                // 3. Threat or objective.
                Health threat = null;
                float best = 16f;
                foreach (var h in UnitRegistry.Of(Team.Enemy))
                {
                    if (h == null || !h.IsAlive || h.kind == TargetKind.Mine || ignored.Contains(h)) continue;
                    float d = Flat(h.transform.position - c).magnitude;
                    if (d < best) { best = d; threat = h; }
                }
                // A target the squad cannot finish in 25 s (behind a wall, out of reach) is skipped, like a player would.
                if (threat != engaged) { engaged = threat; engagedSince = bm.Elapsed; }
                else if (threat != null && bm.Elapsed - engagedSince > 25f) { ignored.Add(threat); threat = null; engaged = null; }
                var cp = bm.CurrentPoint;
                List<Vector3> pts = null;
                bool closed = false;
                Vector3 center;
                if (threat != null)
                {
                    Vector3 tp = threat.transform.position; tp.y = 0f;
                    Vector3 dir = Flat(tp - c);
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    dir.Normalize();
                    float standoff = Mathf.Min(weaponRange * 0.85f, Mathf.Max(2f, best - 0.5f));
                    center = tp - dir * standoff;
                    Vector3 perp = new Vector3(dir.z, 0f, -dir.x);
                    float half = Mathf.Clamp(squad.AliveCount * 0.75f * 0.5f, 3f, 8f);
                    // The mine discs are visible on the ground: like a player, keep the line off them (try small shifts).
                    float bestPenalty = float.MaxValue;
                    Vector3 bestCenter = center;
                    foreach (var off in new[] { Vector2.zero, new Vector2(0f, -1.5f), new Vector2(2.5f, 0f), new Vector2(-2.5f, 0f), new Vector2(0f, -3f), new Vector2(2.5f, -1.5f), new Vector2(-2.5f, -1.5f) })
                    {
                        Vector3 cc = center + perp * off.x + dir * off.y;
                        int pen = 0;
                        for (int i = 0; i <= 16; i++)
                        {
                            float u = i / 16f * 2f - 1f;
                            Vector3 q = cc + perp * (u * half) + dir * ((1f - u * u) * 1.2f);
                            for (int m = 0; m < mines.Count; m++)
                                if (mineAlive[m] && mines[m] != null && Flat(mines[m].transform.position - q).magnitude < 3.0f) pen++;
                        }
                        float score = pen * 10f + off.magnitude;
                        if (score < bestPenalty) { bestPenalty = score; bestCenter = cc; }
                    }
                    center = bestCenter;
                    pts = new List<Vector3>();
                    for (int i = 0; i <= 16; i++)
                    {
                        float u = i / 16f * 2f - 1f;
                        // Slight crescent wrapping toward the target so more soldiers are in range.
                        Vector3 q = center + perp * (u * half) + dir * ((1f - u * u) * 1.2f);
                        pts.Add(ClampToMap(q, gc));
                    }
                }
                else if (cp != null)
                {
                    center = cp.transform.position; center.y = 0f;
                    pts = new List<Vector3>(BattleTestUtil.CirclePoints(center + new Vector3(0f, 0f, -0.3f), 2.2f, 40));
                    closed = true;
                }
                else center = c;

                bool moved = Flat(center - lastCenter).magnitude > 2.5f;
                bool stale = bm.Elapsed - lastDraw > (closed ? 8f : 4f);
                if (pts != null && (moved || stale))
                {
                    lastCenter = center;
                    lastDraw = bm.Elapsed;
                    Vector3 before = squad.Anchor;
                    yield return BattleTestUtil.Drag(pts.ToArray(), 0.5f);
                    o.redraws++;
                    // If the stroke started on a HUD element it is ignored by design; set the same formation directly.
                    if (Flat(squad.Anchor - before).magnitude < 0.3f && Flat(squad.Anchor - center).magnitude > 3f)
                    {
                        squad.SetFormation(pts, closed, closed ? Formation.ShapeType.Circle : Formation.ShapeType.Line);
                        o.fallbacks++;
                    }
                }
                float end = Time.time + 0.5f;
                while (Time.time < end && !bm.Over) { PollMines(); yield return null; }
            }
            PollMines();
            BattleEvents.UnitKilled -= onKilled;
            BattleEvents.Reinforced -= onReinforced;
            o.mineDeaths = mineDeaths;
            o.otherDeaths = otherDeaths;
            Time.timeScale = 1f;
            var r = bm.Result;
            o.victory = bm.Over && r.victory;
            o.time = bm.Elapsed;
            o.reinforcements = reinforcedSoldiers;
            o.lost = mineDeaths + otherDeaths;
            o.kills = bm.Kills;
            o.structures = bm.StructuresDestroyed;
            o.minesTriggered = o.minesStart - Object.FindObjectsByType<Structures.Mine>(FindObjectsSortMode.None).Length;
            o.captured = 0;
            foreach (var p in bm.capturePoints) if (p.Captured) o.captured++;
            done(o);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 ClampToMap(Vector3 q, GameConfig gc)
        {
            if (gc == null) return q;
            q.x = Mathf.Clamp(q.x, gc.mapMin.x + 0.5f, gc.mapMax.x - 0.5f);
            q.z = Mathf.Clamp(q.z, gc.mapMin.y + 0.5f, gc.mapMax.y - 0.5f);
            q.y = 0f;
            return q;
        }
    }
}
