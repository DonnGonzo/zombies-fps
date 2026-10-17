using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Data
{
    public enum PowerStat
    {
        Damage,
        ReloadSpeed,
        MaxHealth,
        MoveSpeed,
        HeadshotDamage,
        AmmoCapacity,
        ReviveSpeed,
        KillPoints,
    }

    /// <summary>One power-up. Its bonus grows with each level but with diminishing returns, so stacking never trivialises late rounds.</summary>
    [Serializable]
    public class PowerUp
    {
        public string name;
        [Tooltip("{0} is replaced with the bonus as a percentage.")]
        public string description;
        public PowerStat stat;
        [Tooltip("The bonus it approaches with endless levels (1 = +100%).")]
        public float maxBonus = 1f;
        [Tooltip("Level at which it gives half of maxBonus.")]
        public float halfLevel = 5f;

        public PowerUp() { }

        public PowerUp(string name, string description, PowerStat stat, float maxBonus, float halfLevel)
        {
            this.name = name;
            this.description = description;
            this.stat = stat;
            this.maxBonus = maxBonus;
            this.halfLevel = halfLevel;
        }

        /// <summary>maxBonus * (1 - 0.5^(level / halfLevel)): level 0 gives 0, halfLevel gives half.</summary>
        public float Bonus(int level)
        {
            if (level <= 0) return 0f;
            return maxBonus * (1f - Mathf.Pow(0.5f, level / Mathf.Max(0.01f, halfLevel)));
        }

        public string Describe(int level) => string.Format(description, Mathf.RoundToInt(Bonus(level) * 100f));
    }

    /// <summary>One visit to the upgrade machine. Tiers apply on top of any gun's own GunData.</summary>
    [Serializable]
    public class MachineTier
    {
        public string suffix;
        [Tooltip("Total damage multiplier at this tier (not stacked with earlier tiers).")]
        public float damageMultiplier = 2f;
        [Tooltip("Total magazine and reserve multiplier at this tier.")]
        public float capacityMultiplier = 1.5f;
        [Tooltip("Hits jump to one more zombie nearby.")]
        public bool chain;
        [Tooltip("Shots burst on impact and hurt zombies around it.")]
        public bool explosive;

        public MachineTier() { }

        public MachineTier(string suffix, float damage, float capacity, bool chain, bool explosive)
        {
            this.suffix = suffix;
            damageMultiplier = damage;
            capacityMultiplier = capacity;
            this.chain = chain;
            this.explosive = explosive;
        }
    }

    /// <summary>
    /// Run progression: the power-ups offered every few rounds (pick 1 of 3, they stack) and the upgrade
    /// machine's tiers. Prices for the machine live in the PriceTable with everything else.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Upgrade Data", fileName = "UpgradeData")]
    public class UpgradeData : ScriptableObject
    {
        /// <summary>Levels are packed 8 bits per power-up into one network variable.</summary>
        public const int MaxPowerUps = 8;

        [Header("Power-ups")]
        public List<PowerUp> powerUps = new List<PowerUp>
        {
            new PowerUp("Hollow Point", "+{0}% damage", PowerStat.Damage, 3f, 8f),
            new PowerUp("Quick Hands", "{0}% faster reloads", PowerStat.ReloadSpeed, 1f, 5f),
            new PowerUp("Thick Skin", "+{0}% max health", PowerStat.MaxHealth, 1.5f, 6f),
            new PowerUp("Light Feet", "+{0}% move speed", PowerStat.MoveSpeed, 0.3f, 5f),
            new PowerUp("Deadeye", "+{0}% headshot damage", PowerStat.HeadshotDamage, 1.5f, 6f),
            new PowerUp("Deep Pockets", "+{0}% magazine and reserve", PowerStat.AmmoCapacity, 1f, 5f),
            new PowerUp("Medic", "{0}% faster revives", PowerStat.ReviveSpeed, 2f, 4f),
            new PowerUp("Bounty", "+{0}% points per kill", PowerStat.KillPoints, 2f, 6f),
        };
        [Tooltip("Everyone picks a power-up after clearing every Nth round.")]
        public int powerUpEveryRounds = 5;
        [Tooltip("No more offers after this round (0 = never stop).")]
        public int lastPowerUpRound = 50;
        [Tooltip("On every Nth round the offer is drawn from power-ups you own, and picking one adds extra levels.")]
        public int empoweredEveryRounds = 25;
        public int empoweredLevels = 2;

        [Header("Upgrade machine")]
        public List<MachineTier> machineTiers = new List<MachineTier>
        {
            new MachineTier("Mk II", 2f, 1.5f, false, false),
            new MachineTier("Mk III", 2.6f, 1.75f, true, false),
            new MachineTier("Mk IV", 3.3f, 2f, true, true),
        };
        [Tooltip("Each player can use the machine once per power-up window (every powerUpEveryRounds rounds).")]
        public bool machineOncePerWindow = true;
        [Range(0f, 1f)] public float chainDamage = 0.6f;
        public float chainRange = 5f;
        [Range(0f, 1f)] public float explosiveDamage = 0.5f;
        public float explosiveRadius = 2.5f;

        public int PowerUpCount => Mathf.Min(MaxPowerUps, powerUps != null ? powerUps.Count : 0);
        public int MaxTier => machineTiers != null ? machineTiers.Count : 0;

        public PowerUp PowerUpAt(int index) => index >= 0 && index < PowerUpCount ? powerUps[index] : null;

        /// <summary>Tier 0 is the gun as it comes off the wall.</summary>
        public MachineTier Tier(int tier) => tier >= 1 && tier <= MaxTier ? machineTiers[tier - 1] : null;

        public float TierDamage(int tier) => Tier(tier)?.damageMultiplier ?? 1f;
        public float TierCapacity(int tier) => Tier(tier)?.capacityMultiplier ?? 1f;

        /// <summary>Total bonus for a stat given packed levels (several power-ups may share a stat).</summary>
        public float StatBonus(ulong packedLevels, PowerStat stat)
        {
            float total = 0f;
            for (int i = 0; i < PowerUpCount; i++)
                if (powerUps[i].stat == stat) total += powerUps[i].Bonus(LevelOf(packedLevels, i));
            return total;
        }

        public static int LevelOf(ulong packed, int index) => (int)((packed >> (index * 8)) & 0xFF);

        public static ulong WithLevel(ulong packed, int index, int level)
        {
            level = Mathf.Clamp(level, 0, 255);
            ulong mask = 0xFFUL << (index * 8);
            return (packed & ~mask) | ((ulong)(uint)level << (index * 8));
        }

        public bool IsPowerUpRound(int round) => powerUpEveryRounds > 0 && round > 0 && round % powerUpEveryRounds == 0
            && (lastPowerUpRound <= 0 || round <= lastPowerUpRound);
        public bool IsEmpoweredRound(int round) => IsPowerUpRound(round) && empoweredEveryRounds > 0 && round % empoweredEveryRounds == 0;

        /// <summary>Which machine window a round is in: the machine recharges every powerUpEveryRounds rounds (also after the last power-up).</summary>
        public int WindowOf(int round) => powerUpEveryRounds > 0 ? Mathf.Max(0, round - 1) / powerUpEveryRounds : 0;
    }
}
