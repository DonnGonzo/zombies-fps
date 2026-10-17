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
        public long[] doorCosts = { 750, 750, 1000, 1000, 1250, 1250 };

        [Tooltip("Guns on the walls, in the map's wall-buy order (see ZoneMap.WallBuys). A gun can appear twice.")]
        public List<WallGun> wallGuns = new List<WallGun>();

        public long DoorCost(int door)
        {
            if (doorCosts == null || doorCosts.Length == 0) return 1000;
            return doorCosts[Mathf.Clamp(door, 0, doorCosts.Length - 1)];
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
            return list.ToArray();
        }

        /// <summary>Built-in prices and guns, used when the scene has no PriceTable asset.</summary>
        public static PriceTable CreateDefault(GunData starter)
        {
            var table = CreateInstance<PriceTable>();
            table.wallGuns = new List<WallGun>
            {
                new WallGun { gun = starter, price = 500, ammoPrice = 250 },
                new WallGun { gun = MakeGun("Hatchet .44", 160, 3f, false, 220f, 6, 36, 48, 2.6f, 2.6f, 0.6f, 1.6f, 0.45f, 0.55f, new Color(0.35f, 0.25f, 0.18f)), price = 750, ammoPrice = 375 },
                new WallGun { gun = MakeGun("Wren-7 SMG", 30, 2.2f, true, 900f, 32, 160, 224, 1.8f, 2.2f, 0.4f, 0.5f, 0.3f, 0.8f, new Color(0.25f, 0.3f, 0.35f)), price = 1000, ammoPrice = 500 },
                new WallGun { gun = MakeGun("Brennan DMR", 120, 2.5f, false, 300f, 15, 75, 105, 2.4f, 3.0f, 0.06f, 1.2f, 0.5f, 1.25f, new Color(0.3f, 0.33f, 0.24f)), price = 1500, ammoPrice = 750 },
                new WallGun { gun = starter, price = 500, ammoPrice = 250 },
                new WallGun { gun = MakeGun("Talon LMG", 55, 2f, true, 600f, 100, 300, 400, 4.5f, 5.5f, 0.3f, 0.7f, 0.25f, 1.35f, new Color(0.2f, 0.2f, 0.2f)), price = 2500, ammoPrice = 1250 },
            };
            return table;
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
