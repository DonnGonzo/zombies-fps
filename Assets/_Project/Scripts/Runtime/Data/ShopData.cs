using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>Where an attachment goes on a gun. A gun holds one attachment per slot; buying another replaces it.</summary>
    public enum AttachmentSlot
    {
        Optic,
        Magazine,
        Underbarrel,
        Muzzle,
        Barrel,
        Ammo,
        Trigger,
    }

    /// <summary>One attachment sold at the spawn shop. Every effect is a multiplier on the gun's own GunData.</summary>
    [Serializable]
    public class Attachment
    {
        public string name;
        public string description;
        public AttachmentSlot slot;
        [Tooltip("Player level that unlocks it in the shop.")]
        public int unlockLevel = 1;
        public long price = 1000;

        [Header("Effects (1 = no change)")]
        public float damage = 1f;
        public float headshot = 1f;
        public float fireRate = 1f;
        public float magazine = 1f;
        public float reloadTime = 1f;
        public float hipSpread = 1f;
        public float aimSpread = 1f;
        public float recoil = 1f;
        [Tooltip("Aim-down-sights field of view multiplier (under 1 zooms in more).")]
        public float aimFov = 1f;
        [Tooltip("Turns semi-automatic guns automatic.")]
        public bool makesAutomatic;

        public Attachment() { }

        public Attachment(string name, string description, AttachmentSlot slot, int unlockLevel, long price)
        {
            this.name = name;
            this.description = description;
            this.slot = slot;
            this.unlockLevel = unlockLevel;
            this.price = price;
        }
    }

    /// <summary>A gun finish unlocked by level and picked in the main menu before a match.</summary>
    [Serializable]
    public class Camo
    {
        public string name;
        public int unlockLevel = 1;
        [Tooltip("Ignored for the first camo, which keeps each gun's own colours.")]
        public Color color = Color.white;

        public Camo() { }
        public Camo(string name, int unlockLevel, Color color)
        {
            this.name = name;
            this.unlockLevel = unlockLevel;
            this.color = color;
        }
    }

    /// <summary>A prestige title shown next to the player's name, unlocked by level.</summary>
    [Serializable]
    public class Title
    {
        public string name;
        public int unlockLevel = 1;

        public Title() { }
        public Title(string name, int unlockLevel)
        {
            this.name = name;
            this.unlockLevel = unlockLevel;
        }
    }

    /// <summary>
    /// The spawn shop and player levels. Players earn XP every run (kills, rounds, missions), and their level
    /// unlocks attachments that the shop sells for in-run points, plus camos and prestige titles picked in the
    /// main menu. Attachments last for the run; levels are saved between runs (saveBetweenRuns).
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Shop Data", fileName = "ShopData")]
    public class ShopData : ScriptableObject
    {
        /// <summary>Owned attachments are a bitmask per gun slot.</summary>
        public const int MaxAttachments = 30;

        [Header("Levels")]
        [Tooltip("Keep XP and level between runs (saved on each player's PC). Off: everyone starts each run at level 1.")]
        public bool saveBetweenRuns = true;
        public int maxLevel = 50;
        [Tooltip("XP from level 1 to 2.")]
        public long xpFirstLevel = 300;
        [Tooltip("Extra XP each level after that.")]
        public long xpPerLevel = 120;

        [Header("XP")]
        public long xpPerKill = 2;
        public long xpPerHeadshotKill = 4;
        public long xpPerBossKill = 150;
        public long xpPerRoundBase = 20;
        public long xpPerRoundPerRound = 2;
        public long xpPerMission = 150;

        [Header("Attachments")]
        public List<Attachment> attachments = new List<Attachment>
        {
            new Attachment("Red-Dot Sight", "Tighter aim and a little less recoil", AttachmentSlot.Optic, 1, 750) { aimSpread = 0.5f, recoil = 0.9f },
            new Attachment("Extended Mag", "+50% magazine", AttachmentSlot.Magazine, 2, 1000) { magazine = 1.5f, reloadTime = 1.1f },
            new Attachment("Foregrip", "-35% recoil, tighter hip fire", AttachmentSlot.Underbarrel, 4, 750) { recoil = 0.65f, hipSpread = 0.8f },
            new Attachment("Suppressor", "-20% recoil and steadier hip fire", AttachmentSlot.Muzzle, 6, 1250) { recoil = 0.8f, hipSpread = 0.75f },
            new Attachment("Fast Mag", "35% faster reloads", AttachmentSlot.Magazine, 9, 1250) { reloadTime = 0.65f },
            new Attachment("Marksman Scope", "Zoomed aim, pinpoint accuracy, +20% headshot damage", AttachmentSlot.Optic, 12, 1500) { aimSpread = 0.2f, aimFov = 0.65f, headshot = 1.2f },
            new Attachment("Heavy Barrel", "+25% damage, a bit more recoil", AttachmentSlot.Barrel, 16, 2500) { damage = 1.25f, recoil = 1.1f },
            new Attachment("Hollow-Core Rounds", "+15% damage, +25% headshot damage", AttachmentSlot.Ammo, 20, 3000) { damage = 1.15f, headshot = 1.25f },
            new Attachment("Full-Auto Conversion", "Fires automatically, +15% fire rate", AttachmentSlot.Trigger, 25, 3500) { makesAutomatic = true, fireRate = 1.15f },
        };

        [Header("Cosmetics (picked in the main menu)")]
        public List<Camo> camos = new List<Camo>
        {
            new Camo("Factory", 1, Color.white),
            new Camo("Olive Drab", 3, new Color(0.33f, 0.36f, 0.22f)),
            new Camo("Desert", 5, new Color(0.66f, 0.56f, 0.38f)),
            new Camo("Arctic", 8, new Color(0.85f, 0.88f, 0.9f)),
            new Camo("Crimson", 12, new Color(0.55f, 0.08f, 0.08f)),
            new Camo("Cobalt", 16, new Color(0.15f, 0.3f, 0.7f)),
            new Camo("Carbon", 20, new Color(0.08f, 0.08f, 0.09f)),
            new Camo("Gold", 30, new Color(0.9f, 0.7f, 0.2f)),
            new Camo("Diamond", 40, new Color(0.7f, 0.95f, 1f)),
            new Camo("Dark Matter", 50, new Color(0.35f, 0.1f, 0.55f)),
        };
        public List<Title> titles = new List<Title>
        {
            new Title("Rookie", 1),
            new Title("Survivor", 5),
            new Title("Scavenger", 10),
            new Title("Veteran", 15),
            new Title("Exterminator", 20),
            new Title("Warden Slayer", 25),
            new Title("Night Stalker", 30),
            new Title("Legend", 40),
            new Title("Undying", 50),
        };

        /// <summary>Camo picks are packed 4 bits per gun id into one network variable.</summary>
        public const int MaxCamos = 16;
        public const int MaxCamoGuns = 16;

        public int CamoCount => Mathf.Min(MaxCamos, camos != null ? camos.Count : 0);
        public Camo CamoAt(int id) => id >= 0 && id < CamoCount ? camos[id] : null;
        public int TitleCount => titles != null ? titles.Count : 0;
        public Title TitleAt(int id) => id >= 0 && id < TitleCount ? titles[id] : null;

        public bool IsCamoUnlocked(int id, int level) => CamoAt(id) != null && level >= CamoAt(id).unlockLevel;
        public bool IsTitleUnlocked(int id, int level) => TitleAt(id) != null && level >= TitleAt(id).unlockLevel;

        /// <summary>The camo colour for a pick, or null for the gun's own colours (camo 0 or a bad id).</summary>
        public Color? CamoColor(int id) => id > 0 && id < CamoCount ? camos[id].color : (Color?)null;

        public static int CamoOf(ulong picks, int gunId) => gunId >= 0 && gunId < MaxCamoGuns ? (int)((picks >> (gunId * 4)) & 0xF) : 0;

        public static ulong WithCamo(ulong picks, int gunId, int camo)
        {
            if (gunId < 0 || gunId >= MaxCamoGuns) return picks;
            ulong mask = 0xFUL << (gunId * 4);
            return (picks & ~mask) | ((ulong)(camo & 0xF) << (gunId * 4));
        }

        public int Count => Mathf.Min(MaxAttachments, attachments != null ? attachments.Count : 0);
        public Attachment At(int id) => id >= 0 && id < Count ? attachments[id] : null;

        /// <summary>XP needed to go from a level to the next one.</summary>
        public long XpToNext(int level) => xpFirstLevel + xpPerLevel * Math.Max(0, level - 1);

        public int LevelFor(long totalXp)
        {
            int level = 1;
            long xp = Math.Max(0, totalXp);
            while (level < maxLevel && xp >= XpToNext(level))
            {
                xp -= XpToNext(level);
                level++;
            }
            return level;
        }

        /// <summary>XP already earned towards the next level.</summary>
        public long XpIntoLevel(long totalXp)
        {
            int level = 1;
            long xp = Math.Max(0, totalXp);
            while (level < maxLevel && xp >= XpToNext(level))
            {
                xp -= XpToNext(level);
                level++;
            }
            return xp;
        }

        public long XpForRound(int round) => xpPerRoundBase + xpPerRoundPerRound * Math.Max(0, round);

        /// <summary>Adds an attachment to a slot's mask, replacing any other attachment in the same gun slot.</summary>
        public int WithAttachment(int mask, int id)
        {
            var add = At(id);
            if (add == null) return mask;
            for (int i = 0; i < Count; i++)
                if (i != id && attachments[i].slot == add.slot) mask &= ~(1 << i);
            return mask | (1 << id);
        }

        public static bool Has(int mask, int id) => id >= 0 && (mask & (1 << id)) != 0;

        readonly Dictionary<(GunData, int), GunData> _cache = new Dictionary<(GunData, int), GunData>();

        /// <summary>A gun with attachments applied, as a cached runtime copy. Mask 0 is the gun itself.</summary>
        public GunData Apply(GunData gun, int mask)
        {
            if (gun == null || mask == 0) return gun;
            if (_cache.TryGetValue((gun, mask), out var cached) && cached != null) return cached;
            var g = Instantiate(gun);
            g.name = gun.name + " +" + mask;
            float recoil = 1f;
            for (int i = 0; i < Count; i++)
            {
                if (!Has(mask, i)) continue;
                var a = attachments[i];
                g.damage = Math.Max(1L, (long)Math.Round(g.damage * (double)a.damage));
                g.headshotMultiplier *= a.headshot;
                g.roundsPerMinute *= a.fireRate;
                g.magazineSize = Mathf.Max(1, Mathf.RoundToInt(g.magazineSize * a.magazine));
                g.reloadTime *= a.reloadTime;
                g.emptyReloadTime *= a.reloadTime;
                g.hipSpread *= a.hipSpread;
                g.aimSpread *= a.aimSpread;
                g.aimFov *= a.aimFov;
                recoil *= a.recoil;
                if (a.makesAutomatic) g.automatic = true;
            }
            g.recoilPitch *= recoil;
            g.recoilYaw *= recoil;
            _cache[(gun, mask)] = g;
            return g;
        }
    }
}
