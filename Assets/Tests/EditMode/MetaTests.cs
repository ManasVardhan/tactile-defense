using System.IO;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>Save system (round trip, corruption fallback, migration), progression (XP to level, research
    /// purchase rules), loadout building and reward granting. Uses a temporary save folder.</summary>
    public class MetaTests
    {
        string dir;
        MetaDatabase db;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "tw_meta_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            db = MakeDb();
            MetaGame.Db = db;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DirectoryOverride = null;
            MetaGame.Db = null;
            BattleLoadout.Pending = null;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (db != null)
            {
                Object.DestroyImmediate(db.progression);
                Object.DestroyImmediate(db.research);
                foreach (var m in db.mercenaries) Object.DestroyImmediate(m);
                Object.DestroyImmediate(db);
            }
        }

        static MetaDatabase MakeDb()
        {
            var d = ScriptableObject.CreateInstance<MetaDatabase>();
            d.progression = ScriptableObject.CreateInstance<ProgressionConfig>();
            d.research = ScriptableObject.CreateInstance<ResearchTree>();
            d.research.nodes = new[]
            {
                new ResearchNode
                {
                    id = "squad_size", displayName = "Squad Size", branch = ResearchBranch.Troops, effect = ResearchEffect.SquadSize,
                    steps = new[] { new ResearchStep { requiredLevel = 1, goldCost = 100, value = 4f }, new ResearchStep { requiredLevel = 3, goldCost = 200, value = 8f } },
                },
                new ResearchNode
                {
                    id = "soldier_health", displayName = "Thick Helmets", branch = ResearchBranch.Troops, effect = ResearchEffect.SoldierHealth,
                    steps = new[] { new ResearchStep { requiredLevel = 1, goldCost = 50, value = 0.1f } },
                },
            };
            var merc = ScriptableObject.CreateInstance<MercenaryDef>();
            merc.id = "brick"; merc.displayName = "Brick"; merc.unlockLevel = 2; merc.unlockCost = 300; merc.hireCost = 50; merc.healthMult = 5f;
            d.mercenaries = new[] { merc };
            return d;
        }

        // ------------------------------------------------------------------ save

        [Test]
        public void Save_RoundTrip_PreservesProfile()
        {
            var p = SaveSystem.Profile;
            Assert.IsTrue(SaveSystem.LastLoadWasNew, "no file yet: new profile");
            p.playerName = "Tester";
            p.team = TeamColorId.Purple;
            p.teamChosen = true;
            p.gold = 1234; p.prisms = 7; p.xp = 420; p.level = 3; p.leaguePoints = 55;
            p.SetResearch("squad_size", 2);
            p.ownedMercenaries.Add("brick"); p.hiredMercenary = "brick";
            p.settings.musicVolume = 0.25f;
            p.tutorialSeen.Add("pick");
            p.tutorialSeen.Add("hint_pips");
            p.BaseLayout.placements.Add(new DefensePlacement { defenseId = "turret", x = 1.5f, z = 40f, rotation = 90f });
            Assert.IsTrue(SaveSystem.Save());
            Assert.IsTrue(File.Exists(SaveSystem.FilePath));
            Assert.IsFalse(File.Exists(SaveSystem.FilePath + ".tmp"), "temp file replaced atomically");

            var r = SaveSystem.Reload();
            Assert.IsFalse(SaveSystem.LastLoadWasNew);
            Assert.AreEqual("Tester", r.playerName);
            Assert.AreEqual(TeamColorId.Purple, r.team);
            Assert.AreEqual(1234, r.gold);
            Assert.AreEqual(7, r.prisms);
            Assert.AreEqual(420, r.xp);
            Assert.AreEqual(3, r.level);
            Assert.AreEqual(2, r.GetResearch("squad_size"));
            Assert.AreEqual("brick", r.hiredMercenary);
            Assert.AreEqual(0.25f, r.settings.musicVolume, 1e-4f);
            CollectionAssert.AreEqual(new[] { "pick", "hint_pips" }, r.tutorialSeen);
            Assert.AreEqual(1, r.BaseLayout.placements.Count);
            Assert.AreEqual("turret", r.BaseLayout.placements[0].defenseId);
            Assert.AreEqual(SaveSystem.CurrentVersion, r.version);
        }

        [Test]
        public void Save_CorruptFile_FallsBackToBackupThenNewProfile()
        {
            var p = SaveSystem.Profile;
            p.gold = 111;
            SaveSystem.Save();
            p.gold = 222;
            SaveSystem.Save(); // the previous file (111) becomes the backup
            File.WriteAllText(SaveSystem.FilePath, "{ this is not json ]]");
            var r = SaveSystem.Reload();
            Assert.IsTrue(SaveSystem.LastLoadRecovered, "recovery flagged");
            Assert.AreEqual(111, r.gold, "restored from the backup");
            Assert.IsTrue(File.Exists(Path.Combine(dir, "profile.corrupt.json")), "corrupt file kept for inspection");

            File.WriteAllText(SaveSystem.FilePath, "garbage");
            File.WriteAllText(SaveSystem.FilePath + ".bak", "");
            r = SaveSystem.Reload();
            Assert.IsTrue(SaveSystem.LastLoadRecovered);
            Assert.AreEqual(db.progression.startGold, r.gold, "fresh profile with starting gold");
            Assert.AreEqual(1, r.level);
        }

        [Test]
        public void Save_OldTutorialFlagsLoadWithEmptySeenList()
        {
            // A version 2 save from before the unit tutorial: the stroke coach's flags, no tutorialSeen list.
            File.WriteAllText(SaveSystem.FilePath, "{\"version\":2,\"playerName\":\"Old Hand\",\"gold\":77,\"tutorial\":{\"drawStroke\":true,\"tapToMove\":true,\"reinforcements\":true}}");
            var r = SaveSystem.Reload();
            Assert.IsFalse(SaveSystem.LastLoadRecovered, "read as is, no recovery");
            Assert.AreEqual("Old Hand", r.playerName);
            Assert.AreEqual(77, r.gold);
            Assert.IsTrue(r.tutorial.reinforcements, "the kept flag still loads");
            Assert.IsNotNull(r.tutorialSeen);
            Assert.AreEqual(0, r.tutorialSeen.Count, "the unit tutorial starts fresh");
        }

        [Test]
        public void Save_MigratesVersion1()
        {
            File.WriteAllText(SaveSystem.FilePath, "{\"version\":1,\"name\":\"Old Timer\",\"coins\":500,\"gems\":3,\"color\":4,\"level\":2,\"xp\":200,\"music\":30,\"sfx\":80}");
            var r = SaveSystem.Reload();
            Assert.AreEqual(SaveSystem.CurrentVersion, r.version);
            Assert.AreEqual("Old Timer", r.playerName);
            Assert.AreEqual(500, r.gold);
            Assert.AreEqual(3, r.prisms);
            Assert.AreEqual(TeamColorId.Pink, r.team);
            Assert.IsTrue(r.teamChosen);
            Assert.AreEqual(2, r.level);
            Assert.AreEqual(0.3f, r.settings.musicVolume, 1e-4f);
            Assert.AreEqual(0.8f, r.settings.sfxVolume, 1e-4f);
            Assert.IsNotNull(r.stats);
            Assert.IsNotNull(r.BaseLayout);
        }

        // ------------------------------------------------------------------ progression

        [Test]
        public void Progression_XpToLevel_AndLevelUpRewards()
        {
            var prog = db.progression;
            Assert.AreEqual(1, prog.LevelForXp(0));
            Assert.AreEqual(1, prog.LevelForXp(149));
            Assert.AreEqual(2, prog.LevelForXp(150));
            Assert.AreEqual(2, prog.LevelForXp(359));
            Assert.AreEqual(3, prog.LevelForXp(360));
            Assert.AreEqual(prog.MaxLevel, prog.LevelForXp(int.MaxValue / 2));

            var p = SaveSystem.Profile;
            int gold = p.gold, prisms = p.prisms;
            int gained = MetaGame.AddXp(400, out int rg, out int rp);
            Assert.AreEqual(2, gained, "150 and 360 thresholds crossed");
            Assert.AreEqual(3, p.level);
            Assert.Greater(rg, 0);
            Assert.AreEqual(gold + rg, p.gold, "level up gold granted");
            Assert.AreEqual(prisms + rp, p.prisms);
            Assert.AreEqual(3, SaveSystem.Reload().level, "saved");
        }

        [Test]
        public void Research_PurchaseDeductsGold_AndRespectsLevelLock()
        {
            var p = SaveSystem.Profile;
            var node = db.research.Find("squad_size");
            p.gold = 500; p.level = 1;
            Assert.AreEqual(PurchaseResult.Ok, MetaGame.BuyResearch(node));
            Assert.AreEqual(400, p.gold, "cost deducted");
            Assert.AreEqual(1, p.GetResearch("squad_size"));

            Assert.AreEqual(PurchaseResult.Locked, MetaGame.BuyResearch(node), "level 2 of the node needs player level 3");
            Assert.AreEqual(400, p.gold, "nothing charged when locked");

            p.level = 3; p.gold = 150;
            Assert.AreEqual(PurchaseResult.NotEnoughGold, MetaGame.BuyResearch(node));
            Assert.AreEqual(150, p.gold);

            p.gold = 1000;
            Assert.AreEqual(PurchaseResult.Ok, MetaGame.BuyResearch(node));
            Assert.AreEqual(800, p.gold);
            Assert.AreEqual(PurchaseResult.Maxed, MetaGame.BuyResearch(node));
            Assert.AreEqual(2, SaveSystem.Reload().GetResearch("squad_size"), "purchase saved");
        }

        [Test]
        public void Mercenary_UnlockAndHire()
        {
            var p = SaveSystem.Profile;
            var m = db.mercenaries[0];
            p.gold = 1000; p.level = 1;
            Assert.AreEqual(PurchaseResult.Locked, MetaGame.UnlockMercenary(m));
            p.level = 2;
            Assert.AreEqual(PurchaseResult.Ok, MetaGame.UnlockMercenary(m));
            Assert.AreEqual(700, p.gold);
            Assert.AreEqual("brick", p.hiredMercenary, "hired on unlock");
            Assert.IsTrue(MetaGame.SetHired(""));
            Assert.IsFalse(MetaGame.SetHired("unknown"), "cannot hire an unowned mercenary");
        }

        // ------------------------------------------------------------------ loadout and rewards

        [Test]
        public void Loadout_ReflectsResearchAndMercenary()
        {
            var p = SaveSystem.Profile;
            var baseLo = LoadoutBuilder.Build(p, db, null);
            Assert.AreEqual(LoadoutBuilder.DefaultSquad, baseLo.squadSize);
            Assert.AreEqual(1f, baseLo.soldierHealthMult, 1e-4f);
            Assert.IsNull(baseLo.mercenary);

            p.gold = 5000; p.level = 5;
            MetaGame.BuyResearch(db.research.Find("squad_size"));
            MetaGame.BuyResearch(db.research.Find("soldier_health"));
            MetaGame.UnlockMercenary(db.mercenaries[0]);
            var lo = LoadoutBuilder.Build(p, db, null);
            Assert.AreEqual(LoadoutBuilder.DefaultSquad + 4, lo.squadSize, "squad size research adds soldiers");
            Assert.AreEqual(1.1f, lo.soldierHealthMult, 1e-4f);
            Assert.IsNotNull(lo.mercenary);
            Assert.AreEqual(5f, lo.mercenary.healthMult, 1e-4f);
            Assert.AreEqual(p.team, lo.playerTeam);

            p.team = TeamColorId.Teal;
            var b = ScriptableObject.CreateInstance<BaseDefinition>();
            b.id = "x"; b.enemyTeam = TeamColorId.Teal; b.rewardMultiplier = 2f;
            var lo2 = LoadoutBuilder.Build(p, db, b);
            Assert.AreNotEqual(lo2.playerTeam, lo2.enemyTeam, "enemy never shares the player's color");
            Assert.AreEqual(2f, lo2.rewardMult, 1e-4f);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void BattleRewards_GrantedAndSaved()
        {
            var p = SaveSystem.Profile;
            int gold = p.gold;
            var b = ScriptableObject.CreateInstance<BaseDefinition>();
            b.id = "b01"; b.difficulty = 2;
            var r = new BattleResult { victory = true, kills = 30, soldiersSurvived = 36, soldiersLost = 4, timeSeconds = 90f, gold = 200, xp = 160 };
            var s = MetaGame.ApplyBattleResult(r, b);
            Assert.IsTrue(s.victory);
            Assert.AreEqual("S", s.grade, "few losses and fast: S grade");
            Assert.Greater(s.gold, 200, "grade bonus applied");
            Assert.IsTrue(s.firstWin);
            Assert.IsTrue(s.LeveledUp);
            var disk = SaveSystem.Reload();
            Assert.AreEqual(gold + s.gold + s.levelUpGold, disk.gold);
            Assert.AreEqual(1, disk.stats.victories);
            Assert.AreEqual(30, disk.stats.kills);
            Assert.Contains("b01", disk.defeatedBases);
            Assert.Greater(disk.leaguePoints, 0);
            Object.DestroyImmediate(b);
        }
            [Test]
        public void LevelOneGradeIgnoresTime()
        {
            // A slow but careful win: 8 of 9 survive after ten minutes (a first player reading every card).
            var r = new BattleResult { victory = true, kills = 10, soldiersSurvived = 8, soldiersLost = 1, timeSeconds = 600f, gold = 180, xp = 200 };
            var prog = db.progression;
            float timed = MetaGame.GradeScore(r, prog, out float surv, out float time);
            float untimed = MetaGame.GradeScore(r, prog, out _, out _, true);
            Assert.AreEqual(0f, time, 1e-4f, "ten minutes is far past par");
            Assert.AreEqual(surv, untimed, 1e-4f, "without time the score is the survivor ratio");
            Assert.Less(timed, untimed);

            var b01 = ScriptableObject.CreateInstance<BaseDefinition>();
            b01.id = "b01";
            var b02 = ScriptableObject.CreateInstance<BaseDefinition>();
            b02.id = "b02";
            try
            {
                Assert.IsTrue(MetaGame.GradeIgnoresTime(b01));
                Assert.IsFalse(MetaGame.GradeIgnoresTime(b02));
                var s1 = MetaGame.ApplyBattleResult(r, b01);
                var s2 = MetaGame.ApplyBattleResult(r, b02);
                Assert.AreEqual("S", s1.grade, "Level 1: 8 of 9 survivors is an S however long it took");
                Assert.AreEqual("-", s1.speedGrade, "Level 1 shows no speed grade");
                Assert.AreNotEqual("S", s2.grade, "other levels still grade speed");
                Assert.AreEqual("D", s2.speedGrade);
                Assert.Greater(s1.gold, s2.gold, "the better grade pays the bigger bonus");
            }
            finally
            {
                Object.DestroyImmediate(b01);
                Object.DestroyImmediate(b02);
            }
        }

        [Test]
        public void RewardEstimateTracksLayoutCounts()
        {
            var real = MetaDatabase.Load();
            Assert.IsNotNull(real, "the generated MetaDatabase");
            var fake = MetaGame.Db;
            MetaGame.Db = real;
            try
            {
                var bc = real.battleConfig;
                // Level 1 has no generated layout: 4 grunts on point 1, 5 grunts and a flag bearer on point 2, one turret.
                var b01 = real.roster.bases[0];
                Assert.AreEqual("b01", b01.id);
                CheckEstimate(b01, bc, 10, 1);
                // A generated base: every layout squad has its grunts plus a flag bearer.
                var b02 = real.roster.bases[1];
                Assert.IsNotNull(b02.layout);
                int soldiers = 0;
                foreach (var q in b02.layout.squads) soldiers += q.count + 1;
                CheckEstimate(b02, bc, soldiers, b02.layout.defenses.Length);
            }
            finally { MetaGame.Db = fake; }
        }

        static void CheckEstimate(BaseDefinition b, BattleConfig bc, int soldiers, int structures)
        {
            TactileWars.UI.AttackScreen.EstimateRewards(b, out int gold, out int xp);
            float wantGold = (bc.goldBase + bc.goldPerKill * soldiers + bc.goldPerStructure * structures + bc.goldVictoryBonus) * b.rewardMultiplier;
            float wantXp = (bc.xpBase + bc.xpPerKill * soldiers + bc.xpVictoryBonus) * b.rewardMultiplier;
            Debug.Log($"[Test] {b.id}: estimate gold {gold} xp {xp}, from counts gold {wantGold:0} xp {wantXp:0} ({soldiers} soldiers, {structures} structures)");
            Assert.That(gold, Is.InRange(wantGold * 0.75f, wantGold * 1.25f), b.id + " gold estimate within 25 percent");
            Assert.That(xp, Is.InRange(wantXp * 0.75f, wantXp * 1.25f), b.id + " xp estimate within 25 percent");
        }

        // ------------------------------------------------------------------ ladder, squad and save migration

        [Test]
        public void OldSaveMigratesWithSquadAndRefund()
        {
            // Review focus: a version 2 save with two levels of the retired Squad Size research, the retired stroke
            // tutorial flags and no squad.
            Assert.AreEqual(3, SaveSystem.CurrentVersion, "this change bumps the save to version 3");
            File.WriteAllText(SaveSystem.FilePath,
                "{\"version\":2,\"playerName\":\"Vet\",\"gold\":100,\"level\":5," +
                "\"research\":[{\"id\":\"squad_size\",\"level\":2},{\"id\":\"soldier_health\",\"level\":1}]," +
                "\"tutorial\":{\"drawStroke\":true,\"tapToMove\":true,\"reinforcements\":true}}");
            var r = SaveSystem.Reload();
            Assert.IsFalse(SaveSystem.LastLoadRecovered, "migrated, not recovered");
            Assert.AreEqual(SaveSystem.CurrentVersion, r.version);
            Assert.AreEqual("Vet", r.playerName);
            Assert.AreEqual(3, r.squad.Count, "a three slot squad");
            CollectionAssert.AreEqual(new[] { (int)UnitTypeId.Attacker, (int)UnitTypeId.Attacker, (int)UnitTypeId.Tank }, r.squad);
            Assert.AreEqual(150 + 320, SaveMigrations.RetiredResearchRefund("squad_size", 2), "the node's first two costs");
            Assert.AreEqual(100 + 150 + 320, r.gold, "both bought levels refunded");
            Assert.IsFalse(r.research.Exists(e => e.id == "squad_size"), "the retired entry is removed");
            Assert.AreEqual(1, r.GetResearch("soldier_health"), "other research kept");
            Assert.IsTrue(r.tutorial.reinforcements, "old tutorial flags still load");
            Assert.AreEqual(0, r.tutorialSeen.Count);

            // Saved again it reads back as is, with no second refund.
            SaveSystem.Save();
            var again = SaveSystem.Reload();
            Assert.AreEqual(570, again.gold);
            Assert.AreEqual(3, again.squad.Count);

            // Bigger Backup is refunded the same way.
            File.WriteAllText(SaveSystem.FilePath, "{\"version\":2,\"gold\":0,\"research\":[{\"id\":\"reinforcement_size\",\"level\":3}]}");
            Assert.AreEqual(220 + 450 + 800, SaveSystem.Reload().gold);
        }

        BaseDefinition[] MakeLadder(int n)
        {
            db.roster = ScriptableObject.CreateInstance<BaseRoster>();
            var bases = new BaseDefinition[n];
            for (int i = 0; i < n; i++)
            {
                bases[i] = ScriptableObject.CreateInstance<BaseDefinition>();
                bases[i].id = "b" + (i + 1).ToString("00");
                bases[i].displayName = "Base " + (i + 1);
            }
            db.roster.bases = bases;
            return bases;
        }

        void DestroyLadder()
        {
            if (db.roster == null) return;
            foreach (var b in db.roster.bases) Object.DestroyImmediate(b);
            Object.DestroyImmediate(db.roster);
            db.roster = null;
        }

        [Test]
        public void ReclearingALevelGivesNoUnlockText()
        {
            // Review focus: the unlock line appears on the first clear only.
            var bases = MakeLadder(3);
            try
            {
                Assert.IsTrue(MetaGame.IsBaseUnlocked(bases[0]), "Level 1 open");
                Assert.IsFalse(MetaGame.IsBaseUnlocked(bases[1]), "Level 2 locked on a fresh save");
                Assert.AreEqual(3, MetaGame.Squad.Count);

                var win = new BattleResult { victory = true, kills = 10, soldiersSurvived = 8, soldiersLost = 1, timeSeconds = 90f, gold = 150, xp = 150 };
                var lose = new BattleResult { victory = false, kills = 2, soldiersSurvived = 0, soldiersLost = 9, timeSeconds = 60f, gold = 20, xp = 20 };
                var s0 = MetaGame.ApplyBattleResult(lose, bases[0]);
                Assert.IsTrue(string.IsNullOrEmpty(s0.unlockText), "a defeat unlocks nothing");

                var s1 = MetaGame.ApplyBattleResult(win, bases[0]);
                Assert.AreEqual("Unlocked: Healer, +1 squad slot", s1.unlockText, "first clear");
                Assert.IsTrue(MetaGame.IsBaseUnlocked(bases[1]), "Level 2 open after clearing Level 1");
                Assert.IsFalse(MetaGame.IsBaseUnlocked(bases[2]));
                Assert.AreEqual(4, MetaGame.Squad.Count, "the new slot arrives");
                Assert.AreEqual(UnitTypeId.Attacker, MetaGame.Squad[3], "filled with an Attacker");
                Assert.AreEqual(4, SaveSystem.Reload().squad.Count, "saved");

                var s2 = MetaGame.ApplyBattleResult(win, bases[0]);
                Assert.IsTrue(string.IsNullOrEmpty(s2.unlockText), "re-clearing Level 1 shows no unlock line");
                Assert.AreEqual(4, MetaGame.Squad.Count, "no extra slot");
            }
            finally { DestroyLadder(); }
        }

        [Test]
        public void CannotClearLastSquadSlot()
        {
            var bases = MakeLadder(2);
            try
            {
                CollectionAssert.AreEqual(new UnitTypeId?[] { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank }, MetaGame.Squad);
                Assert.IsFalse(MetaGame.SetSquadSlot(0, UnitTypeId.Healer), "the Healer is locked before Level 1 is cleared");
                Assert.IsTrue(MetaGame.ClearSquadSlot(0));
                Assert.IsNull(MetaGame.Squad[0], "a cleared slot is empty");
                Assert.AreEqual(-1, SaveSystem.Reload().squad[0], "stored as -1");
                Assert.IsFalse(MetaGame.ClearSquadSlot(0), "already empty");
                Assert.IsTrue(MetaGame.ClearSquadSlot(1));
                Assert.IsFalse(MetaGame.ClearSquadSlot(2), "the last filled slot cannot be cleared");
                Assert.AreEqual(UnitTypeId.Tank, MetaGame.Squad[2], "the last unit stays");

                var lo = LoadoutBuilder.Build(MetaGame.Profile, db, bases[0]);
                CollectionAssert.AreEqual(new[] { UnitTypeId.Tank }, lo.squad, "the loadout skips cleared slots");

                Assert.IsTrue(MetaGame.SetSquadSlot(0, UnitTypeId.Tank));
                Assert.IsFalse(MetaGame.SetSquadSlot(3, UnitTypeId.Tank), "no fourth slot yet");
                lo = LoadoutBuilder.Build(MetaGame.Profile, db, bases[0]);
                CollectionAssert.AreEqual(new[] { UnitTypeId.Tank, UnitTypeId.Tank }, lo.squad);
            }
            finally { DestroyLadder(); }
        }
    }
}
