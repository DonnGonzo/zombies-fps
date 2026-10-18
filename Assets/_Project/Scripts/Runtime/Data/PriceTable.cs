using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>
    /// Every price in the game in one place, so balancing is a spreadsheet job: doors, wall guns and their ammo.
    /// The map decides where each door and wall gun is; this table decides what they cost.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Price Table", fileName = "PriceTable")]
    public class PriceTable : ScriptableObject
    {
        [Serializable]
        public class WallGun
        {
            public GunData gun;
            [Tooltip("Points to buy the gun off the wall.")]
            public long price = 1000;
            [Tooltip("Points to refill it when you already own it.")]
            public long ammoPrice = 500;
        }

        [Tooltip("Cost of each door, in the map's door order (see ZoneMap.Doors).")]
        public long[] doorCosts = { 750, 750, 1000, 1000, 1000, 1250, 1250, 1250, 1500, 1500, 1500, 1750, 1750, 2000, 2000, 2000, 2250 };

        [Tooltip("Upgrade machine: the cost of each visit (Mk II, Mk III, Mk IV).")]
        public long[] upgradeCosts = { 5000, 10000, 15000 };

        [Tooltip("Guns on the walls, in the map's wall-buy order (see ZoneMap.WallBuys). A gun can appear twice.")]
        public List<WallGun> wallGuns = new List<WallGun>();

        [Tooltip("Guns that aren't on any wall: the Ion Lance (a mission reward), then the Scrap Cannon (built at a build station).")]
        public List<GunData> rareGuns = new List<GunData>();

        public GunData RareGun(int index) => rareGuns != null && index >= 0 && index < rareGuns.Count ? rareGuns[index] : null;

        public long DoorCost(int door)
        {
            if (doorCosts == null || doorCosts.Length == 0) return 1000;
            return doorCosts[Mathf.Clamp(door, 0, doorCosts.Length - 1)];
        }

        /// <summary>Cost to take a gun to the given machine tier (1 = first visit).</summary>
        public long UpgradeCost(int tier)
        {
            if (upgradeCosts == null || upgradeCosts.Length == 0) return 5000;
            return upgradeCosts[Mathf.Clamp(tier - 1, 0, upgradeCosts.Length - 1)];
        }

        public WallGun WallGunAt(int index)
        {
            if (wallGuns == null || index < 0 || index >= wallGuns.Count) return null;
            return wallGuns[index];
        }

        /// <summary>
        /// Every distinct gun in the game, starter first. A gun's index here is its network id, and every
        /// machine builds the same list from the same assets.
        /// </summary>
        public GunData[] BuildCatalog(GunData starter)
        {
            var list = new List<GunData> { starter };
            if (wallGuns != null)
                foreach (var entry in wallGuns)
                    if (entry != null && entry.gun != null && !list.Contains(entry.gun)) list.Add(entry.gun);
            if (rareGuns != null)
                foreach (var gun in rareGuns)
                    if (gun != null && !list.Contains(gun)) list.Add(gun);
            return list.ToArray();
        }

        /// <summary>Built-in prices and guns, used when the scene has no PriceTable asset.</summary>
        public static PriceTable CreateDefault(GunData starter)
        {
            var table = CreateInstance<PriceTable>();
            table.wallGuns = new List<WallGun>
            {
                new WallGun { gun = starter, price = 500, ammoPrice = 250 },
                new WallGun { gun = Piercing(MakeGun("Hatchet .44", 160, 3f, false, 220f, 6, 36, 48, 2.6f, 2.6f, 0.6f, 1.6f, 0.45f, 0.55f, new Color(0.35f, 0.25f, 0.18f)), 1, 0.7f), price = 750, ammoPrice = 375 },
                new WallGun { gun = MakeGun("Wren-7 SMG", 30, 2.2f, true, 900f, 32, 160, 224, 1.8f, 2.2f, 0.4f, 0.5f, 0.3f, 0.8f, new Color(0.25f, 0.3f, 0.35f)), price = 1000, ammoPrice = 500 },
                new WallGun { gun = Piercing(MakeGun("Brennan DMR", 120, 2.5f, false, 300f, 15, 75, 105, 2.4f, 3.0f, 0.06f, 1.2f, 0.5f, 1.25f, new Color(0.3f, 0.33f, 0.24f)), 2, 0.75f), price = 1500, ammoPrice = 750 },
                new WallGun { gun = starter, price = 500, ammoPrice = 250 },
                new WallGun { gun = Piercing(MakeGun("Talon LMG", 55, 2f, true, 600f, 100, 300, 400, 4.5f, 5.5f, 0.3f, 0.7f, 0.25f, 1.35f, new Color(0.2f, 0.2f, 0.2f)), 1, 0.65f), price = 2500, ammoPrice = 1250 },
                new WallGun { gun = Shotgun(MakeGun("Mauler 12 Shotgun", 55, 1.6f, false, 80f, 6, 36, 54, 3.0f, 3.4f, 0.8f, 3.4f, 0.9f, 1.05f, new Color(0.45f, 0.3f, 0.18f))), price = 1250, ammoPrice = 600 },
                new WallGun { gun = MakeGun("Kestrel AR", 70, 2.3f, true, 720f, 30, 210, 300, 2.1f, 2.6f, 0.1f, 0.75f, 0.3f, 1.1f, new Color(0.55f, 0.5f, 0.35f)), price = 2000, ammoPrice = 1000 },
                new WallGun { gun = Sniper(MakeGun("Vanta .50 Sniper", 650, 4f, false, 45f, 5, 30, 45, 3.2f, 3.8f, 0f, 4f, 0.6f, 1.5f, new Color(0.15f, 0.2f, 0.18f))), price = 2250, ammoPrice = 1100 },
            };
            var lance = MakeGun("Ion Lance", 260, 2f, true, 480f, 40, 200, 320, 2.4f, 2.8f, 0.15f, 0.5f, 0.2f, 1.25f, new Color(0.3f, 0.9f, 1f));
            lance.pierce = 3;
            lance.pierceDamageKept = 0.85f;
            lance.explosiveRounds = true;
            var cannon = MakeGun("Scrap Cannon", 900, 1.5f, false, 110f, 6, 36, 60, 2.8f, 3.2f, 0.3f, 3.2f, 0.8f, 1.4f, new Color(1f, 0.55f, 0.15f));
            cannon.pierce = 4;
            cannon.pierceDamageKept = 0.85f;
            cannon.explosiveRounds = true;
            table.rareGuns = new List<GunData> { lance, cannon };
            return table;
        }

        static GunData Piercing(GunData gun, int pierce, float kept)
        {
            gun.pierce = pierce;
            gun.pierceDamageKept = kept;
            return gun;
        }

        static GunData Shotgun(GunData gun)
        {
            gun.pellets = 8;
            gun.pelletSpread = 4.5f;
            gun.range = 40f;
            gun.hipSpread = 1.2f;
            gun.aimFov = 60f;
            return gun;
        }

        static GunData Sniper(GunData gun)
        {
            gun.pierce = 5;
            gun.pierceDamageKept = 0.9f;
            gun.range = 200f;
            gun.hipSpread = 4f;
            gun.movingSpread = 3f;
            gun.bloomPerShot = 2f;
            gun.aimFov = 22f;
            gun.aimTime = 0.3f;
            return gun;
        }

        static GunData MakeGun(string name, long damage, float headshot, bool automatic, float rpm, int magazine, int startingReserve,
            int maxReserve, float reload, float emptyReload, float aimSpread, float recoilPitch, float recoilYaw, float length, Color accent)
        {
            var gun = CreateInstance<GunData>();
            gun.name = name;
            gun.displayName = name;
            gun.damage = damage;
            gun.headshotMultiplier = headshot;
            gun.automatic = automatic;
            gun.roundsPerMinute = rpm;
            gun.magazineSize = magazine;
            gun.startingReserve = startingReserve;
            gun.maxReserve = maxReserve;
            gun.reloadTime = reload;
            gun.emptyReloadTime = emptyReload;
            gun.aimSpread = aimSpread;
            gun.recoilPitch = recoilPitch;
            gun.recoilYaw = recoilYaw;
            gun.modelLength = length;
            gun.accent = accent;
            return gun;
        }
    }
}
