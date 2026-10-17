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
        public void Doors_ConnectEveryZone_WithAWayRound()
        {
            // Every zone has at least two doors (so there's always a way round), and every zone can be reached.
            var degree = new int[ZoneMap.ZoneCount];
            foreach (var link in ZoneMap.DoorLinks)
            {
                degree[link.x]++;
                degree[link.y]++;
            }
            for (int z = 0; z < ZoneMap.ZoneCount; z++) Assert.GreaterOrEqual(degree[z], 2, ZoneMap.ZoneNames[z]);

            var visited = new HashSet<int> { ZoneMap.StartZone };
            var queue = new Queue<int>();
            queue.Enqueue(ZoneMap.StartZone);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var link in ZoneMap.DoorLinks)
                {
                    int other = link.x == current ? link.y : link.y == current ? link.x : -1;
                    if (other >= 0 && visited.Add(other)) queue.Enqueue(other);
                }
            }
            Assert.AreEqual(ZoneMap.ZoneCount, visited.Count);
        }

        [Test]
        public void Doors_SitOnTheWallBetweenNeighbours()
        {
            for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
            {
                var a = ZoneMap.ZoneCenter(ZoneMap.DoorLinks[d].x);
                var b = ZoneMap.ZoneCenter(ZoneMap.DoorLinks[d].y);
                Assert.AreEqual(ZoneMap.ZoneHalfSize * 2f, Vector3.Distance(a, b), 0.01f, "door " + d + " joins zones that aren't neighbours");
            }
            Assert.Less(ZoneMap.DoorLinks.Length, 31, "open doors are a bitmask");
        }

        [Test]
        public void HighSpots_PartsAndBenches_AreOnTheMap()
        {
            for (int i = 0; i < ZoneMap.HighSpotCount; i++)
                Assert.AreEqual(ZoneMap.HighSpotZones[i], ZoneMap.ZoneOfPoint(ZoneMap.HighSpot(i)), ZoneMap.HighSpotNames[i]);
            Assert.AreEqual(ZoneMap.PartCount * ZoneMap.SpotsPerPart, ZoneMap.PartSpotCount);
            for (int part = 0; part < ZoneMap.PartCount; part++)
            {
                // A part's candidate spots are in different zones, so where it turns up really changes.
                var zones = new HashSet<int>();
                for (int k = 0; k < ZoneMap.SpotsPerPart; k++)
                {
                    int zone = ZoneMap.ZoneOfPoint(ZoneMap.PartSpot(part * ZoneMap.SpotsPerPart + k));
                    Assert.GreaterOrEqual(zone, 0);
                    zones.Add(zone);
                }
                Assert.AreEqual(ZoneMap.SpotsPerPart, zones.Count, "part " + part);
            }
            for (int b = 0; b < ZoneMap.BenchCount; b++) Assert.GreaterOrEqual(ZoneMap.ZoneOfPoint(ZoneMap.BenchPosition(b)), 0);
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
