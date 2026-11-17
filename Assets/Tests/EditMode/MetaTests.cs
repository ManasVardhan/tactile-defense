using System.IO;
using NUnit.Framework;
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
            p.tutorial.drawStroke = true;
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
            Assert.IsTrue(r.tutorial.drawStroke);
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
    }
}
