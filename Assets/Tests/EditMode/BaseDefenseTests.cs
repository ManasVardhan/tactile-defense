using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Battle;
using TactileWars.Formation;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>Phase 5 rules: base placement validation, purchase and consume accounting, layout persistence,
    /// simulated raids, the routing grid and the line recognizer.</summary>
    public class BaseDefenseTests
    {
        DefenseCatalog cat;
        MapLayoutData map;
        string dir;

        [SetUp]
        public void SetUp()
        {
            cat = ScriptableObject.CreateInstance<DefenseCatalog>();
            cat.entries = new[]
            {
                new DefenseCatalog.Entry { id = "mine", displayName = "Mine", goldCost = 15, budget = 4, space = 1, maxCount = 3, footprint = 0.6f, researchId = "def_mine", trap = true },
                new DefenseCatalog.Entry { id = "turret", displayName = "Turret", goldCost = 60, budget = 10, space = 1, maxCount = 4, footprint = 1.1f, researchId = "def_turret" },
                new DefenseCatalog.Entry { id = "bunker", displayName = "Bunker", goldCost = 110, budget = 18, space = 3, maxCount = 2, footprint = 1.7f, researchId = "def_bunker" },
                new DefenseCatalog.Entry { id = "squad", displayName = "Squad", goldCost = 40, budget = 8, space = 2, maxCount = 3, footprint = 2.4f, researchId = "" },
            };
            map = ScriptableObject.CreateInstance<MapLayoutData>();
            map.capturePoints = new[] { new Vector3(0, 0, 46f), new Vector3(0, 0, 90f) };
            map.placeable = new Vector4(-15.5f, 18f, 15.5f, 101f);
            map.attackerZoneMaxZ = 18f;
            map.padClearance = 3.6f;
            map.props = new[] { new PropPlacement { position = new Vector3(10f, 0, 30f), radius = 1.5f, scale = 1f } };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cat);
            Object.DestroyImmediate(map);
            if (dir != null)
            {
                SaveSystem.DirectoryOverride = null;
                try { Directory.Delete(dir, true); } catch (System.Exception) { }
                dir = null;
            }
        }

        BaseEditSession Session(int gold = 1000, int budget = 60, int space = 8, int researchLevel = 1)
            => new BaseEditSession(new BaseLayoutSave(), gold, cat, map, budget, space, id => researchLevel, 1f);

        // ------------------------------------------------------------ placement rules

        [Test]
        public void Placement_ValidSpot_IsAccepted_AndSnapsToGrid()
        {
            var s = Session();
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(3.2f, 0, 30.7f), 180f));
            Assert.AreEqual(1, s.Placements.Count);
            Assert.AreEqual(3.5f, s.Placements[0].x, 1e-4f, "snapped to the 1 unit cell centre");
            Assert.AreEqual(30.5f, s.Placements[0].z, 1e-4f);
            Assert.AreEqual(10, s.BudgetUsed);
            Assert.AreEqual(1, s.SpaceUsed);
        }

        [Test]
        public void Placement_Zones_AreEnforced()
        {
            var s = Session();
            Assert.AreEqual(PlacementError.AttackerZone, s.Check("turret", new Vector3(0, 0, 12f)));
            Assert.AreEqual(PlacementError.OutsideZone, s.Check("turret", new Vector3(16f, 0, 40f)));
            Assert.AreEqual(PlacementError.OutsideZone, s.Check("turret", new Vector3(0, 0, 102f)));
            Assert.AreEqual(PlacementError.CapturePad, s.Check("turret", new Vector3(2f, 0, 47f)));
            Assert.AreEqual(PlacementError.OverlapsProp, s.Check("turret", new Vector3(10.5f, 0, 31f)));
            Assert.AreEqual(PlacementError.None, s.Check("turret", new Vector3(-8f, 0, 30f)));
        }

        [Test]
        public void Placement_OverlapWithOtherDefense_IsRejected_ButMoveIgnoresItself()
        {
            var s = Session();
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(-6f, 0, 30f), 180f));
            Assert.AreEqual(PlacementError.OverlapsDefense, s.Check("turret", new Vector3(-5f, 0, 30.5f)));
            Assert.AreEqual(PlacementError.None, s.Check("turret", new Vector3(-3f, 0, 30f)), "footprints 1.1 + 1.1 fit 3 units apart");
            Assert.AreEqual(PlacementError.None, s.Move(0, new Vector3(-5.5f, 0, 30f)), "moving onto its own old spot is fine");
        }

        [Test]
        public void Placement_BudgetCap_IsEnforced()
        {
            var s = Session(budget: 25);
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(-8f, 0, 30f), 180f));
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(-4f, 0, 30f), 180f));
            Assert.AreEqual(20, s.BudgetUsed);
            Assert.AreEqual(PlacementError.BudgetCap, s.Place("turret", new Vector3(0f, 0, 30f), 180f), "30 > 25");
            Assert.AreEqual(PlacementError.None, s.Place("mine", new Vector3(0f, 0, 30f), 0f), "24 <= 25");
            Assert.AreEqual(2 + 1, s.Placements.Count);
        }

        [Test]
        public void Placement_SpaceCap_And_TypeLimit_AreEnforced()
        {
            var s = Session(space: 4);
            Assert.AreEqual(PlacementError.None, s.Place("bunker", new Vector3(-8f, 0, 30f), 180f));
            Assert.AreEqual(PlacementError.SpaceCap, s.Place("bunker", new Vector3(-2f, 0, 30f), 180f), "3 + 3 > 4");
            var t = Session(space: 20);
            for (int i = 0; i < 3; i++) Assert.AreEqual(PlacementError.None, t.Place("mine", new Vector3(-10f + i * 3f, 0, 25f), 0f));
            Assert.AreEqual(PlacementError.TypeLimit, t.Place("mine", new Vector3(5f, 0, 25f), 0f), "max 3 mines");
        }

        [Test]
        public void Placement_LockedResearch_IsRejected_SquadNeedsNoResearch()
        {
            var s = Session(researchLevel: 0);
            Assert.AreEqual(PlacementError.Locked, s.Check("turret", new Vector3(-8f, 0, 30f)));
            Assert.AreEqual(PlacementError.None, s.Check("squad", new Vector3(-8f, 0, 30f)), "defender squads have no research gate");
        }

        // ------------------------------------------------------------ economy

        [Test]
        public void Purchase_PendingGold_StockFirst_AndRebuyWrecks()
        {
            var s = Session(gold: 100);
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(-8f, 0, 30f), 180f));
            Assert.AreEqual(60, s.PendingGold);
            Assert.AreEqual(40, s.GoldAvailable);
            Assert.AreEqual(PlacementError.NotEnoughGold, s.Place("turret", new Vector3(-3f, 0, 30f), 180f), "60 > 40 left");
            s.Remove(0);
            Assert.AreEqual(1, s.StockOf("turret"), "removed defenses go back to stock");
            Assert.AreEqual(60, s.PendingGold, "no gold refund");
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(-3f, 0, 30f), 180f), "re-placing a stocked copy is free");
            Assert.AreEqual(60, s.PendingGold);
            Assert.AreEqual(0, s.StockOf("turret"));

            var save = new BaseLayoutSave();
            save.destroyed.Add(new DefensePlacement { defenseId = "mine", x = 5.5f, z = 25.5f, rotation = 0f });
            save.destroyed.Add(new DefensePlacement { defenseId = "turret", x = -8.5f, z = 40.5f, rotation = 180f });
            var w = new BaseEditSession(save, 1000, cat, map, 60, 8, id => 1);
            Assert.AreEqual(75, w.RebuyAllCost());
            Assert.AreEqual(2, w.RebuyAll(out var err));
            Assert.AreEqual(PlacementError.None, err);
            Assert.AreEqual(75, w.PendingGold);
            Assert.AreEqual(0, w.Destroyed.Count);
            Assert.AreEqual(2, w.Placements.Count);
        }

        void UseTempSave()
        {
            dir = Path.Combine(Path.GetTempPath(), "tw_base_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            MetaGame.Db = MetaDatabase.Load();
        }

        [Test]
        public void CommitBaseEdit_DeductsGold_AndLayoutSurvivesRestart()
        {
            UseTempSave();
            Assert.IsNotNull(MetaGame.Db, "MetaDatabase in Resources");
            Assert.IsNotNull(MetaGame.Db.homeMap, "home map built");
            var p = MetaGame.Profile;
            p.gold = 500;
            p.SetResearch("def_turret", 2);
            var s = MetaGame.BeginBaseEdit();
            Assert.AreEqual(PlacementError.None, s.Place("mine", new Vector3(-4f, 0, 28f), 33f));
            Assert.AreEqual(PlacementError.None, s.Place("turret", new Vector3(6f, 0, 36f), 225f));
            int pending = s.PendingGold;
            Assert.AreEqual(MetaGame.Db.defenses.entries[MetaGame.Db.defenses.IndexOf("mine")].goldCost + MetaGame.Db.defenses.entries[MetaGame.Db.defenses.IndexOf("turret")].goldCost, pending);
            Assert.IsTrue(MetaGame.CommitBaseEdit(s));
            Assert.AreEqual(500 - pending, MetaGame.Profile.gold);

            // "Restart": drop the cached profile and read the file again.
            SaveSystem.DirectoryOverride = dir;
            var disk = SaveSystem.Profile;
            Assert.AreEqual(500 - pending, disk.gold);
            Assert.AreEqual(2, disk.BaseLayout.placements.Count);
            var t = disk.BaseLayout.placements[1];
            Assert.AreEqual("turret", t.defenseId);
            Assert.AreEqual(6.5f, t.x, 1e-4f);
            Assert.AreEqual(36.5f, t.z, 1e-4f);
            Assert.AreEqual(225f, t.rotation, 1e-4f);
        }

        [Test]
        public void DefenseResult_ConsumesPlacements_RewardsOnlyWhenHeld_TestsChangeNothing()
        {
            UseTempSave();
            var p = MetaGame.Profile;
            p.gold = 100;
            p.BaseLayout.placements.Add(new DefensePlacement { defenseId = "mine", x = 0.5f, z = 25.5f });
            p.BaseLayout.placements.Add(new DefensePlacement { defenseId = "turret", x = 5.5f, z = 35.5f, rotation = 180f });
            p.BaseLayout.placements.Add(new DefensePlacement { defenseId = "mine", x = -3.5f, z = 60.5f });

            var held = new BattleResult { victory = false, soldiersLost = 30, soldiersSurvived = 0, timeSeconds = 100f };
            var test = MetaGame.ApplyDefenseResult(held, new List<int> { 0, 2 }, true, "Tester", Core.TeamColorId.Blue, 0, false);
            Assert.IsTrue(test.held);
            Assert.AreEqual(3, p.BaseLayout.placements.Count, "test battles consume nothing");
            Assert.AreEqual(100, p.gold, "and pay nothing");

            var s = MetaGame.ApplyDefenseResult(held, new List<int> { 0, 2 }, false, "Raider", Core.TeamColorId.Blue, 0, false);
            Assert.AreEqual(1, p.BaseLayout.placements.Count);
            Assert.AreEqual("turret", p.BaseLayout.placements[0].defenseId);
            Assert.AreEqual(2, p.BaseLayout.destroyed.Count, "consumed defenses wait for rebuy");
            CollectionAssert.AreEquivalent(new[] { "mine", "mine" }, s.consumed);
            Assert.Greater(s.gold, 0, "a held defense pays gold");
            Assert.AreEqual(100 + s.gold, p.gold);
            Assert.AreEqual(1, p.defenseStats.battlesHeld);

            int goldBefore = p.gold;
            var fell = MetaGame.ApplyDefenseResult(new BattleResult { victory = true, soldiersLost = 3, soldiersSurvived = 30 }, new List<int> { 0 }, false, "Raider", Core.TeamColorId.Blue, 2, false);
            Assert.IsFalse(fell.held);
            Assert.AreEqual(0, fell.gold);
            Assert.AreEqual(goldBefore, p.gold);
            Assert.AreEqual(0, p.BaseLayout.placements.Count);
            Assert.AreEqual(3, p.BaseLayout.destroyed.Count);
            // Persisted.
            SaveSystem.DirectoryOverride = dir;
            Assert.AreEqual(3, SaveSystem.Profile.BaseLayout.destroyed.Count);
        }

        [Test]
        public void SimulatedRaid_IsDeterministic_Labeled_AndApplied()
        {
            UseTempSave();
            var p = MetaGame.Profile;
            p.gold = 1000;
            p.level = 5;
            for (int i = 0; i < 6; i++) p.BaseLayout.placements.Add(new DefensePlacement { defenseId = i % 2 == 0 ? "mine" : "turret", x = -10f + i * 3f, z = 30f });
            p.SetResearch("def_turret", 1);
            var a = new List<int>();
            var b = new List<int>();
            var r1 = RaidSimulator.Simulate(p, MetaGame.Defenses, MetaGame.BaseConfig, MetaGame.ResearchLevel, 4242, a);
            var r2 = RaidSimulator.Simulate(p, MetaGame.Defenses, MetaGame.BaseConfig, MetaGame.ResearchLevel, 4242, b);
            Assert.IsTrue(r1.isSimulated, "raids are labeled simulated");
            Assert.AreEqual(r1.held, r2.held);
            Assert.AreEqual(r1.goldDelta, r2.goldDelta);
            CollectionAssert.AreEqual(a, b, "same seed, same consumption");
            Assert.AreNotEqual(p.team, r1.attackerTeam, "attacker uses another color");
            int before = p.BaseLayout.placements.Count;
            MetaGame.ApplyRaid(r1, a);
            Assert.AreEqual(before - a.Count, p.BaseLayout.placements.Count);
            Assert.AreEqual(a.Count, p.BaseLayout.destroyed.Count);
            Assert.AreEqual(Mathf.Max(0, 1000 + r1.goldDelta), p.gold);
            Assert.AreEqual(1, p.raidLog.Count);

            // Over many seeds a stronger base holds more often (model sanity).
            int weakHolds = 0, strongHolds = 0;
            var weak = new PlayerProfile { level = 5 };
            weak.BaseLayout.placements.Add(new DefensePlacement { defenseId = "mine" });
            var strong = new PlayerProfile { level = 5 };
            for (int i = 0; i < 10; i++) strong.BaseLayout.placements.Add(new DefensePlacement { defenseId = "turret" });
            for (int sd = 0; sd < 200; sd++)
            {
                if (RaidSimulator.Simulate(weak, MetaGame.Defenses, MetaGame.BaseConfig, id => 1, sd, a).held) weakHolds++;
                if (RaidSimulator.Simulate(strong, MetaGame.Defenses, MetaGame.BaseConfig, id => 1, sd, a).held) strongHolds++;
            }
            Debug.Log($"[Test] raid holds out of 200: weak {weakHolds}, strong {strongHolds}");
            Assert.Greater(strongHolds, weakHolds + 40);
        }

        // ------------------------------------------------------------ routing grid

        [Test]
        public void NavGrid_RoutesThroughGate_WithoutAllocating()
        {
            var g = new NavGrid(new Vector2(-20f, 0f), new Vector2(20f, 40f), 1f);
            // A wall across z = 20 with a gate between x = 4 and x = 8.
            g.BlockRect(new Vector2(-20f, 19.5f), new Vector2(3.5f, 21f));
            g.BlockRect(new Vector2(8.5f, 19.5f), new Vector2(20f, 21f));
            var path = new List<Vector3>(64);
            Vector3 from = new Vector3(-10f, 0, 10f), to = new Vector3(-10f, 0, 30f);
            Assert.IsFalse(g.LineClear(from, to), "the straight line hits the wall");
            Assert.IsTrue(g.FindPath(from, to, path));
            bool throughGate = false;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 a = i == 0 ? from : path[i - 1], b = path[i];
                Assert.IsTrue(g.LineClear(a, b), "every leg is clear");
                if ((a.z - 20f) * (b.z - 20f) <= 0f)
                {
                    float t = Mathf.Abs(b.z - a.z) > 1e-4f ? (20f - a.z) / (b.z - a.z) : 0f;
                    float x = Mathf.Lerp(a.x, b.x, t);
                    if (x > 3.5f && x < 8.5f) throughGate = true;
                }
            }
            Assert.IsTrue(throughGate, "the route crosses z = 20 inside the gate");
            Assert.AreEqual(to, path[path.Count - 1]);

            g.FindPath(from, to, path); // warm up
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 10; k++) g.FindPath(from, to, path);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "path searches do not allocate");
        }

        // ------------------------------------------------------------ recognizer

        static List<Vector3> Arc(Vector3 c, float rx, float rz, float a0, float a1, int n = 24)
        {
            var p = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                p.Add(c + new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz));
            }
            return p;
        }

        [Test]
        public void Recognizer_LineThenShallowArcThenCircle()
        {
            var set = ShapeRecognizer.Settings.Default;
            var line = new List<Vector3>();
            for (int i = 0; i <= 30; i++) line.Add(new Vector3(-8f + i * 16f / 30f, 0, 3f + i * 2f / 30f));
            Assert.AreEqual(ShapeType.Line, ShapeRecognizer.Recognize(line, set).type);
            // The capture script's crescent: chord / length is about 0.93, right on the old line threshold.
            var arc = Arc(new Vector3(0, 0, 15.5f), 6.5f, 3f, 160f, 20f);
            var r = ShapeRecognizer.Recognize(arc, set);
            Debug.Log($"[Test] arc straightness {r.straightness:0.000}, deviation ratio {ShapeRecognizer.MaxDeviation(arc) / PathResampler.Length(arc, false):0.000}");
            Assert.AreEqual(ShapeType.Free, r.type, "a crescent is not snapped to a straight line");
            // An even flatter arc that passes the straightness ratio is still rejected by the deviation rule.
            var flat = Arc(new Vector3(0, 0, 0), 8f, 2.4f, 150f, 30f);
            set.lineMaxDeviation = 0f;
            bool oldRuleLine = ShapeRecognizer.Recognize(flat, set).type == ShapeType.Line;
            set.lineMaxDeviation = 0.08f;
            Assert.AreEqual(ShapeType.Free, ShapeRecognizer.Recognize(flat, set).type);
            Debug.Log($"[Test] flat arc was a line under the old rule: {oldRuleLine}");
            var circle = Arc(new Vector3(0, 0, 0), 4.2f, 4.2f, -90f, 270f, 48);
            Assert.AreEqual(ShapeType.Circle, ShapeRecognizer.Recognize(circle, set).type);
        }
    }
}
