using System.Collections;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>
    /// Balance runs: ArmyBot plays Level 1 (b01, no research, the default squad Attacker x2 plus Tank x1) start to
    /// finish, three times in one test so the editor starts once. Target: victory every run in under 150 s of game
    /// time, with 4 to 8 of the 9 soldiers alive on average. Each run is appended to Logs/bot_playthrough.txt.
    /// Later levels add their own cases here (see ArmyBot for the policy).
    /// </summary>
    [Category("Bot")]
    public class BotPlaythroughTests
    {
        public const int Runs = 3;
        public const float TimeLimit = 150f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSpeed.Reset();
            BattleLaunch.Clear();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        /// <summary>Loads a base through the launch path with a fresh, unresearched level 1 profile.</summary>
        static IEnumerator Load(int index)
        {
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var p = MetaGame.Profile;
            p.teamChosen = true;
            BattleTestUtil.MarkTutorialSeen(p);
            var b = MetaGame.Db.roster.bases[index];
            // Every run starts from the same ladder state: the save is shared across runs and a win clears the level
            // (which would add a squad slot). Levels before 'index' count as cleared so the level is open.
            p.defeatedBases.Clear();
            for (int i = 0; i < index; i++) p.defeatedBases.Add(MetaGame.Db.roster.bases[i].id);
            p.squad.Clear(); // the starter squad, padded with Attackers to the level's slots
            p.level = Mathf.Max(1, b.requiredLevel);
            BattleLaunch.Prepare(b);
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.5f);
        }

        static void Log(string line)
        {
            Debug.Log("[Bot] " + line);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/bot_playthrough.txt"));
            File.AppendAllText(path, System.DateTime.Now.ToString("HH:mm") + " " + line + "\n");
        }

        [UnityTest]
        public IEnumerator BotWinsLevelOne()
        {
            float survivors = 0f;
            var lines = new string[Runs];
            var bots = new ArmyBot[Runs];
            for (int run = 0; run < Runs; run++)
            {
                yield return Load(0);
                Assert.AreEqual("b01", BattleSetup.Instance.Applied.baseId);
                var bot = new ArmyBot();
                yield return bot.Play(TimeLimit + 30f);
                bots[run] = bot;
                lines[run] = bot.Line($"b01 run {run + 1}/{Runs}");
                Log(lines[run]);
                survivors += bot.Survivors;
            }
            Log($"b01 summary: average survivors {survivors / Runs:0.0} of 9");
            for (int run = 0; run < Runs; run++)
            {
                Assert.AreEqual(0, bots[run].StuckEvents, "no unit stuck on Level 1's track: " + lines[run]);
                Assert.IsTrue(bots[run].Victory, "Level 1 must be won: " + lines[run]);
                Assert.GreaterOrEqual(bots[run].Survivors, 4, "at least 4 of 9 soldiers survive: " + lines[run]);
                Assert.Less(bots[run].GameTime, TimeLimit, "won in under 150 s of game time: " + lines[run]);
            }
        }

        /// <summary>Levels 2 to 5, one run each, with a full squad of all six types (seven units) whatever the
        /// level unlocks: proves each level can be started, pathed and finished on its own map. Win or loss is
        /// recorded, not asserted (balance is the next task's); the run must end (or reach the time limit having
        /// taken a circle) with no unit stuck.</summary>
        [UnityTest]
        public IEnumerator BotPlaysLevelsTwoToFive()
        {
            var full = new List<UnitTypeId> { UnitTypeId.Tank, UnitTypeId.Attacker, UnitTypeId.Healer, UnitTypeId.Sniper, UnitTypeId.Vanguard, UnitTypeId.Demolisher, UnitTypeId.Tank };
            var results = new List<(ArmyBot bot, string line)>();
            for (int index = 1; index < Progression.LevelCount; index++)
            {
                yield return Load(index);
                var b = MetaGame.Db.roster.bases[index];
                Assert.AreEqual(b.id, BattleSetup.Instance.Applied.baseId);
                PlayerArmy.Instance.SetSquad(full);
                var bot = new ArmyBot();
                yield return bot.Play(LevelLimit);
                string line = bot.Line($"{b.id} {b.displayName} (7 units, all six types)");
                Log(line);
                results.Add((bot, line));
                BattleTestUtil.Cleanup();
            }
            foreach (var (bot, line) in results)
            {
                Assert.IsTrue(bot.Finished || bot.PointsCaptured > 0, "the run ended or took a circle: " + line);
                Assert.AreEqual(0, bot.StuckEvents, "no unit stuck: " + line);
            }
        }

        public const float LevelLimit = 240f;
    }
}
