using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>The objective types missions are built from. Designers combine them in MissionData.</summary>
    public enum MissionObjective
    {
        /// <summary>The squad kills a number of zombies.</summary>
        Kills,
        /// <summary>The squad gets a number of headshot kills.</summary>
        HeadshotKills,
        /// <summary>The squad kills a number of one zombie type.</summary>
        KillType,
        /// <summary>Someone stands at a generator for a number of seconds while the horde keeps coming.</summary>
        HoldGenerator,
        /// <summary>The squad clears a number of rounds in a row without anyone going down.</summary>
        NoDowns,
        /// <summary>A survivor holes up in an open zone: keep the zombies off them for a number of seconds.</summary>
        ProtectSurvivor,
        /// <summary>Someone climbs to one of the map's high spots and holds its signal beacon for a number of seconds.</summary>
        HoldHighGround,
    }

    /// <summary>What a finished mission gives every player, on top of its points.</summary>
    public enum MissionBonus
    {
        None,
        /// <summary>Every gun refilled.</summary>
        MaxAmmo,
        /// <summary>A free redraw of a power-up offer's cards.</summary>
        PowerUpReroll,
        /// <summary>The rare Ion Lance, put in every player's hands.</summary>
        RareWeapon,
    }

    [Serializable]
    public class MissionDef
    {
        public string title;
        [Tooltip("{0} is replaced with the target.")]
        public string description;
        public MissionObjective objective;
        [Tooltip("KillType only.")]
        public ZombieKind zombieKind;
        [Tooltip("Not offered before this round. KillType missions also wait for their zombie type.")]
        public int minRound = 1;
        public float targetBase = 10f;
        public float targetPerRound;
        public int targetMax = 9999;
        public long pointsBase = 500;
        public long pointsPerRound = 40;
        public MissionBonus bonus;

        public MissionDef() { }

        public MissionDef(string title, string description, MissionObjective objective, float targetBase, float targetPerRound, int targetMax,
            long pointsBase, long pointsPerRound, MissionBonus bonus, int minRound = 1, ZombieKind kind = ZombieKind.Walker)
        {
            this.title = title;
            this.description = description;
            this.objective = objective;
            this.targetBase = targetBase;
            this.targetPerRound = targetPerRound;
            this.targetMax = targetMax;
            this.pointsBase = pointsBase;
            this.pointsPerRound = pointsPerRound;
            this.bonus = bonus;
            this.minRound = minRound;
            zombieKind = kind;
        }

        /// <summary>The target for a mission set handed out on a round. Targets grow with the round so late missions still take effort.</summary>
        public int TargetFor(int round) => Mathf.Clamp(Mathf.RoundToInt(targetBase + targetPerRound * Mathf.Max(0, round - 1)), 1, Mathf.Max(1, targetMax));

        public long PointsFor(int round) => pointsBase + pointsPerRound * Math.Max(0, round - 1);

        public string Describe(int target) => string.Format(description, target);
    }

    /// <summary>
    /// Optional missions: three are handed out at round 1, then a fresh set every 10 rounds. An unfinished
    /// set simply goes away when it refreshes. Progress is shared by the squad and rewards go to everyone.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Mission Data", fileName = "MissionData")]
    public class MissionData : ScriptableObject
    {
        /// <summary>Mission ids are packed into 8 bits on the network.</summary>
        public const int MaxMissions = 255;
        public const int SlotCount = 3;

        public List<MissionDef> missions = new List<MissionDef>
        {
            new MissionDef("Cull the Horde", "Kill {0} zombies", MissionObjective.Kills, 40, 3, 600, 500, 40, MissionBonus.None),
            new MissionDef("Sharpshooter", "Get {0} headshot kills", MissionObjective.HeadshotKills, 15, 1.2f, 250, 400, 30, MissionBonus.PowerUpReroll),
            new MissionDef("Keep the Lights On", "Hold the generator for {0} seconds", MissionObjective.HoldGenerator, 40, 0.5f, 90, 600, 40, MissionBonus.MaxAmmo),
            new MissionDef("Nobody Falls", "Clear {0} rounds in a row with nobody going down", MissionObjective.NoDowns, 2, 0, 2, 750, 50, MissionBonus.PowerUpReroll),
            new MissionDef("Run Them Down", "Kill {0} runners", MissionObjective.KillType, 15, 0.3f, 120, 500, 40, MissionBonus.None, 8, ZombieKind.Runner),
            new MissionDef("Low Blow", "Kill {0} crawlers", MissionObjective.KillType, 12, 0.2f, 80, 500, 40, MissionBonus.MaxAmmo, 12, ZombieKind.Crawler),
            new MissionDef("Bring Down the Brutes", "Kill {0} brutes", MissionObjective.KillType, 6, 0.1f, 40, 700, 50, MissionBonus.MaxAmmo, 20, ZombieKind.Brute),
            new MissionDef("Acid Test", "Kill {0} spitters", MissionObjective.KillType, 5, 0.08f, 30, 700, 50, MissionBonus.PowerUpReroll, 30, ZombieKind.Spitter),
            new MissionDef("Safe Passage", "Keep the survivor alive for {0} seconds", MissionObjective.ProtectSurvivor, 40, 0.3f, 90, 700, 50, MissionBonus.MaxAmmo, 5),
            new MissionDef("The Prototype", "Protect the engineer for {0} seconds. She's carrying a prototype weapon", MissionObjective.ProtectSurvivor, 60, 0.3f, 120, 1000, 60, MissionBonus.RareWeapon, 25),
            new MissionDef("Cut the Feed", "Overload the Lantern relay: hold it for {0} seconds", MissionObjective.HoldGenerator, 60, 0.3f, 120, 1200, 50, MissionBonus.PowerUpReroll, 101),
            new MissionDef("King of the Hill", "Climb up and hold the signal beacon for {0} seconds", MissionObjective.HoldHighGround, 30, 0.3f, 75, 900, 50, MissionBonus.MaxAmmo, 6),
            new MissionDef("Signal Boost", "Boost the Dispatcher's signal: hold the high-ground beacon for {0} seconds", MissionObjective.HoldHighGround, 45, 0.3f, 100, 1200, 60, MissionBonus.PowerUpReroll, 15),
            new MissionDef("The Defector", "Keep the defecting scientist alive for {0} seconds", MissionObjective.ProtectSurvivor, 70, 0.2f, 120, 1400, 60, MissionBonus.MaxAmmo, 110),
        };

        [Tooltip("A fresh set of missions every Nth round (and on round 1).")]
        public int refreshEveryRounds = 10;
        [Tooltip("How close to a generator counts as holding it.")]
        public float generatorRadius = 3f;
        [Header("Protect the survivor")]
        public float survivorHealth = 400f;
        [Tooltip("Zombies this close to the survivor hurt them.")]
        public float survivorReach = 1.8f;
        [Tooltip("Damage each zombie in reach does per second, as a share of its normal hit.")]
        public float survivorDamageShare = 0.5f;

        public int Count => Mathf.Min(MaxMissions, missions != null ? missions.Count : 0);
        public MissionDef At(int id) => id >= 0 && id < Count ? missions[id] : null;

        public bool IsRefreshRound(int round) => round == 1 || (refreshEveryRounds > 0 && round > 0 && round % refreshEveryRounds == 0);

        /// <summary>The round the current set was handed out on, for a given round.</summary>
        public int SetStartRound(int round)
        {
            if (refreshEveryRounds <= 0 || round < refreshEveryRounds) return 1;
            return round - round % refreshEveryRounds;
        }

        public int NextRefreshRound(int round) => refreshEveryRounds <= 0 ? int.MaxValue : (Mathf.Max(0, round) / refreshEveryRounds + 1) * refreshEveryRounds;

        /// <summary>Whether a mission can be handed out on a round. KillType missions wait until their zombie type is around.</summary>
        public bool IsEligible(int id, int round, Func<ZombieKind, int> firstRoundOf)
        {
            var def = At(id);
            if (def == null || round < def.minRound) return false;
            if (def.objective == MissionObjective.KillType && firstRoundOf != null && round < firstRoundOf(def.zombieKind)) return false;
            return true;
        }

        /// <summary>Missions that happen at a place on the map: the zone (or, for high ground, the high spot) is in the slot.</summary>
        public static bool IsPlaced(MissionObjective o) =>
            o == MissionObjective.HoldGenerator || o == MissionObjective.ProtectSurvivor || o == MissionObjective.HoldHighGround;

        // A slot packs into one ulong: mission id (8 bits), done flag (1), zone or high spot (6), failed flag (1), progress (24), target (24).
        public const int NoMission = 0xFF;

        public static ulong Pack(int id, bool done, int zone, int progress, int target, bool failed = false) =>
            // Through uint first: int -> ulong would sign-extend (CS0675).
            (ulong)(uint)(id & 0xFF)
            | ((done ? 1UL : 0UL) << 8)
            | ((ulong)(uint)(zone & 0x3F) << 9)
            | ((failed ? 1UL : 0UL) << 15)
            | ((ulong)(uint)Mathf.Clamp(progress, 0, 0xFFFFFF) << 16)
            | ((ulong)(uint)Mathf.Clamp(target, 0, 0xFFFFFF) << 40);

        public static int IdOf(ulong slot) => (int)(slot & 0xFF);
        public static bool DoneOf(ulong slot) => ((slot >> 8) & 1) != 0;
        public static int ZoneOf(ulong slot) => (int)((slot >> 9) & 0x3F);
        /// <summary>A mission that can be lost (the survivor died) and was.</summary>
        public static bool FailedOf(ulong slot) => ((slot >> 15) & 1) != 0;
        public static int ProgressOf(ulong slot) => (int)((slot >> 16) & 0xFFFFFF);
        public static int TargetOf(ulong slot) => (int)((slot >> 40) & 0xFFFFFF);
        public static ulong Empty => Pack(NoMission, false, 0, 0, 0);
    }
}
