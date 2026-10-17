using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>The eight perk tonics. Each machine sells one; the order here is the machine order on the map.</summary>
    public enum PerkKind : byte
    {
        Ironhide,
        SlickFingers,
        SecondWind,
        HairTrigger,
        FleetFoot,
        SteadyHand,
        HazardWard,
        Bloodline,
    }

    [Serializable]
    public class PerkDef
    {
        public string name;
        [Tooltip("Shown on the machine and in the HUD.")]
        public string description;
        public long price = 2000;
        public Color color = Color.white;

        public PerkDef() { }

        public PerkDef(string name, string description, long price, Color color)
        {
            this.name = name;
            this.description = description;
            this.price = price;
            this.color = color;
        }
    }

    /// <summary>
    /// Buyable perks: each player can own each one once, and loses all of them on going down (a revive or the
    /// end-of-round respawn doesn't bring them back). Separate from the 5-round power-ups, which survive downs.
    /// Also the other repeatable money sinks: armour plates and power-up rerolls.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Perk Data", fileName = "PerkData")]
    public class PerkData : ScriptableObject
    {
        public const int MaxPerks = 16;

        public List<PerkDef> perks = new List<PerkDef>
        {
            new PerkDef("Ironhide Tonic", "Double max health", 2500, new Color(0.85f, 0.2f, 0.15f)),
            new PerkDef("Slick Fingers", "Reload 50% faster", 3000, new Color(0.2f, 0.75f, 0.35f)),
            new PerkDef("Second Wind", "Revive teammates twice as fast. Solo: get yourself back up once", 1500, new Color(0.35f, 0.65f, 1f)),
            new PerkDef("Hair Trigger", "Fire 33% faster", 2000, new Color(1f, 0.75f, 0.2f)),
            new PerkDef("Fleet Foot", "Move 12% faster", 2000, new Color(0.95f, 0.95f, 0.95f)),
            new PerkDef("Steady Hand", "40% less spread, faster aiming", 2000, new Color(0.25f, 0.85f, 0.85f)),
            new PerkDef("Hazard Ward", "Immune to acid and crawler slows, take 15% less damage", 2500, new Color(0.55f, 0.95f, 0.2f)),
            new PerkDef("Bloodline", "Kills heal you, and health comes back sooner", 4000, new Color(0.6f, 0.05f, 0.25f)),
        };

        [Header("Effects")]
        public float ironhideHealth = 2f;
        public float slickReload = 1.5f;
        public float secondWindRevive = 2f;
        [Tooltip("Solo only: seconds down before getting back up.")]
        public float secondWindSelfRevive = 5f;
        public float hairTriggerFireRate = 1.33f;
        public float fleetFootSpeed = 1.12f;
        public float steadyHandSpread = 0.6f;
        public float hazardDamageTaken = 0.85f;
        public float bloodlineHealPerKill = 8f;
        public float bloodlineRegenDelay = 2f;

        [Header("Armour plates (repeatable money sink)")]
        public int maxPlates = 3;
        [Tooltip("Damage one plate soaks up before breaking.")]
        public float plateArmor = 50f;
        public long platePrice = 750;
        [Tooltip("Plates cost more as the rounds go on, so late money still matters.")]
        public long platePricePerRound = 25;
        public long platePriceMax = 5000;

        [Header("Power-up reroll (repeatable money sink)")]
        public long rerollPrice = 3000;
        public long rerollPricePerRound = 100;
        public long rerollPriceMax = 20000;

        public int Count => Mathf.Min(MaxPerks, perks != null ? perks.Count : 0);
        public PerkDef At(int id) => id >= 0 && id < Count ? perks[id] : null;

        public long PlatePrice(int round) => Math.Min(platePriceMax, platePrice + platePricePerRound * Math.Max(0, round - 1));
        public long RerollPrice(int round) => Math.Min(rerollPriceMax, rerollPrice + rerollPricePerRound * Math.Max(0, round - 1));

        public static bool Has(int mask, PerkKind perk) => (mask & (1 << (int)perk)) != 0;
        public static int With(int mask, PerkKind perk) => mask | (1 << (int)perk);
        public static int CountOwned(int mask)
        {
            int n = 0;
            for (int i = 0; i < MaxPerks; i++) if ((mask & (1 << i)) != 0) n++;
            return n;
        }
    }
}
