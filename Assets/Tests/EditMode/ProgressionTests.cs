using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Meta;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>The level ladder rules (Progression): consecutive clears, which level is open, squad slots, the unit
    /// unlock schedule, the reward text and how a stored squad is repaired against what the player has unlocked.</summary>
    public class ProgressionTests
    {
        MetaDatabase db;

        [SetUp]
        public void SetUp()
        {
            db = ScriptableObject.CreateInstance<MetaDatabase>();
            db.roster = ScriptableObject.CreateInstance<BaseRoster>();
            var bases = new BaseDefinition[Progression.LevelCount];
            for (int i = 0; i < bases.Length; i++)
            {
                bases[i] = ScriptableObject.CreateInstance<BaseDefinition>();
                bases[i].id = "b" + (i + 1).ToString("00");
            }
            db.roster.bases = bases;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var b in db.roster.bases) Object.DestroyImmediate(b);
            Object.DestroyImmediate(db.roster);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void SlotsAre3Then4Through7()
        {
            Assert.AreEqual(5, Progression.LevelCount, "five levels on the ladder");
            Assert.AreEqual(7, Progression.MaxSlots, "the slot cap: no eighth slot is ever shown");
            var want = new[] { 3, 4, 5, 6, 7, 7, 7, 7 };
            for (int cleared = 0; cleared < want.Length; cleared++)
                Assert.AreEqual(want[cleared], Progression.SquadSlots(cleared), "slots after clearing " + cleared);
        }

        [Test]
        public void UnlockSchedule()
        {
            for (int c = 0; c <= Progression.LevelCount; c++)
            {
                Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Attacker, c), "Attacker always");
                Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Tank, c), "Tank always");
            }
            Assert.IsFalse(Progression.IsUnlocked(UnitTypeId.Healer, 0));
            Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Healer, 1));
            Assert.IsFalse(Progression.IsUnlocked(UnitTypeId.Sniper, 1));
            Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Sniper, 2));
            Assert.IsFalse(Progression.IsUnlocked(UnitTypeId.Vanguard, 2));
            Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Vanguard, 3));
            Assert.IsFalse(Progression.IsUnlocked(UnitTypeId.Demolisher, 3));
            Assert.IsTrue(Progression.IsUnlocked(UnitTypeId.Demolisher, 4));
            Assert.AreEqual(0, Progression.UnlockLevel(UnitTypeId.Attacker));
            Assert.AreEqual(0, Progression.UnlockLevel(UnitTypeId.Tank));
            Assert.AreEqual(1, Progression.UnlockLevel(UnitTypeId.Healer));
            Assert.AreEqual(2, Progression.UnlockLevel(UnitTypeId.Sniper));
            Assert.AreEqual(3, Progression.UnlockLevel(UnitTypeId.Vanguard));
            Assert.AreEqual(4, Progression.UnlockLevel(UnitTypeId.Demolisher));
            Assert.IsFalse(Progression.IsUnlocked((UnitTypeId)100, Progression.LevelCount), "a type outside the schedule is never fielded from the profile");
        }

        [Test]
        public void LevelsClearedCountsOnlyConsecutive()
        {
            var p = new PlayerProfile();
            Assert.AreEqual(0, Progression.LevelsCleared(p, db));
            p.defeatedBases.Add("b02");
            Assert.AreEqual(0, Progression.LevelsCleared(p, db), "Level 2 without Level 1 does not count");
            p.defeatedBases.Add("b01");
            p.defeatedBases.Add("b04");
            Assert.AreEqual(2, Progression.LevelsCleared(p, db), "b01, b02 and b04 defeated: two in a row");
            Assert.AreEqual(0, Progression.LevelsCleared(p, null), "no database: nothing cleared");
            Assert.AreEqual(1, Progression.LevelOf(db.roster.bases[0], db));
            Assert.AreEqual(5, Progression.LevelOf(db.roster.bases[4], db));
        }

        [Test]
        public void RetiredLevelIdsInAnOldSaveAreIgnored()
        {
            // Saves from the ten level ladder can list b06 to b10: they still load and count for nothing.
            var p = new PlayerProfile();
            foreach (var id in new[] { "b01", "b02", "b06", "b07", "b08", "b09", "b10" }) p.defeatedBases.Add(id);
            Assert.AreEqual(2, Progression.LevelsCleared(p, db), "only b01 and b02 count");
            Assert.AreEqual(5, Progression.SquadSlots(Progression.LevelsCleared(p, db)));
            p.defeatedBases.AddRange(new[] { "b03", "b04", "b05" });
            Assert.AreEqual(Progression.LevelCount, Progression.LevelsCleared(p, db), "never more than the ladder holds");
            var stored = Progression.RepairStored(new List<int> { 0, 1, 2, 3, 4, 5, 0, 1 }, Progression.LevelsCleared(p, db));
            Assert.AreEqual(7, stored.Count, "an eight unit squad from an old save is cut to seven slots");
        }

        [Test]
        public void LevelOpenRule()
        {
            Assert.IsTrue(Progression.IsLevelOpen(1, 0), "Level 1 is open on a fresh save");
            Assert.IsFalse(Progression.IsLevelOpen(2, 0));
            Assert.IsTrue(Progression.IsLevelOpen(2, 1));
            Assert.IsTrue(Progression.IsLevelOpen(1, 3), "cleared levels stay open (replay)");
            Assert.IsTrue(Progression.IsLevelOpen(4, 3));
            Assert.IsFalse(Progression.IsLevelOpen(5, 3));
            Assert.IsTrue(Progression.IsLevelOpen(5, 4), "the final level opens after Level 4");
        }

        [Test]
        public void RepairSquadDropsLockedTruncatesAndPads()
        {
            // Review focus: a 9 entry squad with a Demolisher at cleared 1 gives 4 entries with no Demolisher.
            var nine = new List<UnitTypeId>
            {
                UnitTypeId.Tank, UnitTypeId.Demolisher, UnitTypeId.Healer, UnitTypeId.Attacker, UnitTypeId.Sniper,
                UnitTypeId.Tank, UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Healer,
            };
            var r = Progression.RepairSquad(nine, 1);
            Assert.AreEqual(4, r.Count, "four slots after clearing Level 1");
            CollectionAssert.DoesNotContain(r, UnitTypeId.Demolisher);
            CollectionAssert.DoesNotContain(r, UnitTypeId.Sniper);
            CollectionAssert.AreEqual(new[] { UnitTypeId.Tank, UnitTypeId.Healer, UnitTypeId.Attacker, UnitTypeId.Tank }, r,
                "locked types dropped, order kept, then truncated");

            CollectionAssert.AreEqual(new[] { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank }, Progression.RepairSquad(null, 0));
            CollectionAssert.AreEqual(new[] { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank }, Progression.RepairSquad(new List<UnitTypeId>(), 0));
            CollectionAssert.AreEqual(new[] { UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Tank, UnitTypeId.Attacker, UnitTypeId.Attacker },
                Progression.RepairSquad(null, 2), "the starter squad padded with Attackers");
            CollectionAssert.AreEqual(new[] { UnitTypeId.Healer, UnitTypeId.Attacker, UnitTypeId.Attacker, UnitTypeId.Attacker },
                Progression.RepairSquad(new[] { UnitTypeId.Healer }, 1), "short squads are padded with Attackers");
            for (int c = 0; c <= Progression.LevelCount; c++)
                Assert.AreEqual(Progression.SquadSlots(c), Progression.RepairSquad(nine, c).Count, "length equals the slots at cleared " + c);
        }

        [Test]
        public void RewardTextForLevels()
        {
            Assert.AreEqual("Unlocks Healer, +1 squad slot", Progression.RewardText(1));
            Assert.AreEqual("Unlocks Sniper, +1 squad slot", Progression.RewardText(2));
            Assert.AreEqual("Unlocks Vanguard, +1 squad slot", Progression.RewardText(3));
            Assert.AreEqual("Unlocks Demolisher, +1 squad slot", Progression.RewardText(4));
            Assert.AreEqual("", Progression.RewardText(5), "the final level grants nothing new");
            Assert.AreEqual("Unlocked: Healer, +1 squad slot", Progression.UnlockText(0, 1), "the result screen line");
            Assert.AreEqual("Unlocked: Demolisher, +1 squad slot", Progression.UnlockText(3, 4));
            Assert.AreEqual("You cleared every level!", Progression.UnlockText(4, 5), "clearing the final level");
            Assert.AreEqual("", Progression.UnlockText(3, 3), "nothing new cleared: no line");
        }
    }
}
