using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Tests
{
    public class ZoneMapTests
    {
        [Test]
        public void Doors_LinkEveryZoneIntoOneLoop()
        {
            // Every zone has exactly two doors, and walking door to door visits all six before coming back.
            var degree = new int[ZoneMap.ZoneCount];
            foreach (var link in ZoneMap.DoorLinks)
            {
                degree[link.x]++;
                degree[link.y]++;
            }
            for (int z = 0; z < ZoneMap.ZoneCount; z++) Assert.AreEqual(2, degree[z], ZoneMap.ZoneNames[z]);

            var visited = new HashSet<int> { ZoneMap.StartZone };
            int current = ZoneMap.StartZone, cameFrom = -1;
            for (int step = 0; step < ZoneMap.ZoneCount; step++)
            {
                int next = -1;
                foreach (var link in ZoneMap.DoorLinks)
                {
                    int other = link.x == current ? link.y : link.y == current ? link.x : -1;
                    if (other >= 0 && other != cameFrom) { next = other; break; }
                }
                Assert.GreaterOrEqual(next, 0);
                cameFrom = current;
                current = next;
                visited.Add(current);
            }
            Assert.AreEqual(ZoneMap.StartZone, current, "the loop should come back to the start");
            Assert.AreEqual(ZoneMap.ZoneCount, visited.Count);
        }

        [Test]
        public void OnlyTheStartZone_IsOpenWithNoDoorsBought()
        {
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
                Assert.AreEqual(z == ZoneMap.StartZone, ZoneMap.IsZoneOpen(z, 0), ZoneMap.ZoneNames[z]);
        }

        [Test]
        public void BuyingADoor_OpensBothSides()
        {
            for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
            {
                int mask = 1 << d;
                Assert.IsTrue(ZoneMap.IsZoneOpen(ZoneMap.DoorLinks[d].x, mask));
                Assert.IsTrue(ZoneMap.IsZoneOpen(ZoneMap.DoorLinks[d].y, mask));
            }
        }

        [Test]
        public void DefaultPrices_CoverEveryDoorAndWallGun()
        {
            var starter = ScriptableObject.CreateInstance<GunData>();
            var prices = PriceTable.CreateDefault(starter);
            Assert.AreEqual(ZoneMap.DoorLinks.Length, prices.doorCosts.Length);
            Assert.AreEqual(ZoneMap.WallBuyZones.Length, prices.wallGuns.Count);
            foreach (var entry in prices.wallGuns)
            {
                Assert.IsNotNull(entry.gun);
                Assert.Less(entry.ammoPrice, entry.price, entry.gun.displayName);
            }

            var catalog = prices.BuildCatalog(starter);
            Assert.AreSame(starter, catalog[0], "the starter gun is always id 0");
            Assert.AreEqual(catalog.Length, new HashSet<GunData>(catalog).Count, "no gun listed twice");
        }
    }
}
