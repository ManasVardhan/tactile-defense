using System;
using UnityEngine;

namespace TactileWars.Meta
{
    /// <summary>XP curve, level rewards, leagues, starting resources and the battle reward formula.
    /// ALL VALUES ARE ESTIMATES: the sources only verify that XP drives levels, gold is earned in attacks, Prisms are
    /// the premium currency and there are four leagues (Docs/References/web/RESEARCH_MECHANICS.md).</summary>
    [CreateAssetMenu(menuName = "TactileWars/Meta/Progression Config")]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("Start (estimates)")]
        public int startGold = 400;
        public int startPrisms = 10;

        [Header("Levels (estimates)")]
        [Tooltip("Total XP needed to REACH level i+1 (index 0 = level 1 = 0 XP).")]
        public int[] levelXp = { 0, 150, 360, 650, 1030, 1510, 2100, 2810, 3650, 4630, 5760, 7050, 8510, 10150, 11980, 14010, 16250, 18710, 21400, 24330 };
        [Tooltip("Reward granted on reaching a level (index = level - 1).")]
        public LevelReward[] levelRewards = Array.Empty<LevelReward>();

        [Header("Leagues (four leagues verified, names and thresholds are estimates)")]
        public League[] leagues =
        {
            new League { name = "Bronze League", minPoints = 0, color = new Color(0.80f, 0.52f, 0.30f) },
            new League { name = "Silver League", minPoints = 250, color = new Color(0.72f, 0.76f, 0.82f) },
            new League { name = "Gold League", minPoints = 700, color = new Color(0.96f, 0.72f, 0.18f) },
            new League { name = "Platinum League", minPoints = 1500, color = new Color(0.55f, 0.85f, 0.88f) },
        };
        public int leaguePointsPerWin = 18;
        public int leaguePointsPerDifficulty = 4;
        public int leaguePointsLostOnDefeat = 8;

        [Header("Battle rewards (estimates)")]
        [Tooltip("Battle par time in seconds used by the grade.")] public float parTimeSeconds = 150f;
        [Tooltip("Weight of the survivor ratio in the grade score (the rest is speed).")] [Range(0, 1)] public float survivorWeight = 0.7f;
        [Tooltip("Score thresholds for S, A, B, C (below C is D).")] public float[] gradeThresholds = { 0.85f, 0.7f, 0.5f, 0.3f };
        [Tooltip("Gold and XP multiplier per grade S, A, B, C, D.")] public float[] gradeMultipliers = { 1.3f, 1.15f, 1f, 0.9f, 0.8f };
        [Tooltip("Prisms for the first victory against a base.")] public int firstWinPrisms = 3;

        [Header("Exchange (estimate; replaces real money purchases)")]
        public int exchangePrisms = 5;
        public int exchangeGold = 250;

        public int MaxLevel => levelXp.Length;

        public int LevelForXp(int totalXp)
        {
            int lvl = 1;
            for (int i = 1; i < levelXp.Length; i++) if (totalXp >= levelXp[i]) lvl = i + 1; else break;
            return lvl;
        }

        public int XpForLevel(int level) => levelXp[Mathf.Clamp(level - 1, 0, levelXp.Length - 1)];

        /// <summary>Progress 0..1 from the current level threshold to the next.</summary>
        public float LevelProgress(int totalXp, out int into, out int span)
        {
            int lvl = LevelForXp(totalXp);
            int a = XpForLevel(lvl);
            if (lvl >= MaxLevel) { into = totalXp - a; span = 0; return 1f; }
            int b = XpForLevel(lvl + 1);
            into = totalXp - a;
            span = b - a;
            return span > 0 ? Mathf.Clamp01(into / (float)span) : 1f;
        }

        public LevelReward RewardFor(int level)
        {
            for (int i = 0; i < levelRewards.Length; i++) if (levelRewards[i].level == level) return levelRewards[i];
            return new LevelReward { level = level, gold = 50 * level, prisms = level % 5 == 0 ? 5 : 1 };
        }

        public int LeagueIndex(int points)
        {
            int idx = 0;
            for (int i = 0; i < leagues.Length; i++) if (points >= leagues[i].minPoints) idx = i;
            return idx;
        }

        public string Grade(float score)
        {
            if (score >= gradeThresholds[0]) return "S";
            if (score >= gradeThresholds[1]) return "A";
            if (score >= gradeThresholds[2]) return "B";
            if (score >= gradeThresholds[3]) return "C";
            return "D";
        }

        public static int GradeIndex(string g) => g switch { "S" => 0, "A" => 1, "B" => 2, "C" => 3, _ => 4 };
    }

    [Serializable]
    public struct LevelReward
    {
        public int level;
        public int gold;
        public int prisms;
    }

    [Serializable]
    public struct League
    {
        public string name;
        public int minPoints;
        public Color color;
    }
}
