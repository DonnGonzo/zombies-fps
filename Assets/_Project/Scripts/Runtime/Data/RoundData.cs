using System;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>
    /// Difficulty curves for rounds 1-500. Pure functions so they can be unit tested and simulated
    /// without running the game (see Tests/EditMode/RoundDataTests).
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Round Data", fileName = "RoundData")]
    public class RoundData : ScriptableObject
    {
        public const int MaxRound = 500;

        [Header("Zombie count: base + perRound * round^exponent")]
        public float countBase = 6f;
        public float countPerRound = 2.2f;
        public float countExponent = 0.85f;
        [Tooltip("Extra zombies per player beyond the first, as a fraction of the solo count.")]
        public float countPerExtraPlayer = 0.5f;
        [Tooltip("Never more than this many alive at once. Remaining zombies spawn as others die.")]
        public int liveCap = 24;
        [Tooltip("Live cap added for each player beyond the first.")]
        public int liveCapPerExtraPlayer = 4;
        public int liveCapMax = 40;

        [Header("Spawn pacing: start * decay^(round-1), never below min")]
        [Tooltip("Seconds between spawns on round 1 with one player.")]
        public float spawnIntervalStart = 2f;
        public float spawnIntervalDecay = 0.95f;
        public float spawnIntervalMin = 0.3f;
        [Tooltip("Spawns speed up by this fraction for each player beyond the first.")]
        public float spawnSpeedupPerExtraPlayer = 0.3f;

        [Header("Round flow (seconds)")]
        public float firstRoundDelay = 8f;
        public float intermissionTime = 10f;

        [Header("Economy (points)")]
        public long startingPoints = 500;
        [Tooltip("Paid to every player when a round is cleared: base + perRound * (round - 1).")]
        public long roundBonusBase = 100;
        public long roundBonusPerRound = 10;

        [Header("Boss rounds")]
        [Tooltip("A Warden boss joins every Nth round.")]
        public int bossEveryRounds = 10;
        [Tooltip("One more boss on a boss round every this many rounds (round 50 brings 2).")]
        public int extraBossEveryRounds = 50;
        public int maxBosses = 4;
        [Tooltip("Points every player gets when a boss goes down.")]
        public long bossBonus = 500;

        [Header("Health")]
        public long healthBase = 150;
        [Tooltip("Flat health added each round up to linearUntilRound.")]
        public long healthPerRoundEarly = 100;
        public int linearUntilRound = 9;
        [Tooltip("Steep multiplicative growth per round until softCapRound.")]
        public float steepGrowth = 1.05f;
        public int softCapRound = 30;
        [Tooltip("Linear growth (fraction of the round-30 value) per round until lateGameRound.")]
        public float softGrowthPerRound = 0.02f;
        public int lateGameRound = 100;
        [Tooltip("Late-game multiplicative growth per round after lateGameRound.")]
        public float lateGrowth = 1.01f;

        [Header("Speed tiers")]
        public int jogFromRound = 4;
        public int sprintFromRound = 10;

        public int ZombieCountForRound(int round, int players = 1)
        {
            round = Mathf.Clamp(round, 1, MaxRound);
            players = Mathf.Max(1, players);
            double solo = countBase + countPerRound * Math.Pow(round, countExponent);
            double scaled = solo * (1.0 + countPerExtraPlayer * (players - 1));
            return (int)Math.Round(scaled);
        }

        public int LiveCapFor(int players)
        {
            players = Mathf.Max(1, players);
            return Mathf.Min(liveCapMax, liveCap + liveCapPerExtraPlayer * (players - 1));
        }

        public float SpawnIntervalForRound(int round, int players = 1)
        {
            round = Mathf.Clamp(round, 1, MaxRound);
            players = Mathf.Max(1, players);
            double interval = spawnIntervalStart * Math.Pow(spawnIntervalDecay, round - 1);
            interval /= 1.0 + spawnSpeedupPerExtraPlayer * (players - 1);
            return Mathf.Max(spawnIntervalMin, (float)interval);
        }

        public long RoundBonus(int round)
        {
            round = Mathf.Clamp(round, 1, MaxRound);
            return roundBonusBase + roundBonusPerRound * (round - 1);
        }

        public bool IsBossRound(int round) => bossEveryRounds > 0 && round > 0 && round % bossEveryRounds == 0;

        public int BossCountForRound(int round)
        {
            if (!IsBossRound(round)) return 0;
            int extra = extraBossEveryRounds > 0 ? round / extraBossEveryRounds : 0;
            return Mathf.Clamp(1 + extra, 1, Mathf.Max(1, maxBosses));
        }

        public long HealthForRound(int round)
        {
            round = Mathf.Clamp(round, 1, MaxRound);
            double h = healthBase + healthPerRoundEarly * (Math.Min(round, linearUntilRound) - 1);
            if (round > linearUntilRound)
                h *= Math.Pow(steepGrowth, Math.Min(round, softCapRound) - linearUntilRound);
            if (round > softCapRound)
                h *= 1.0 + softGrowthPerRound * (Math.Min(round, lateGameRound) - softCapRound);
            if (round > lateGameRound)
                h *= Math.Pow(lateGrowth, round - lateGameRound);
            if (h >= long.MaxValue) return long.MaxValue;
            return Math.Max(1L, (long)Math.Round(h));
        }

        public ZombieSpeedTier SpeedTierForRound(int round)
        {
            if (round >= sprintFromRound) return ZombieSpeedTier.Sprint;
            if (round >= jogFromRound) return ZombieSpeedTier.Jog;
            return ZombieSpeedTier.Walk;
        }
    }
}
