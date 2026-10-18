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
                var link = ZoneMap.DoorLinks[d];
                var c = ZoneMap.DoorCenter(d);
                var across = ZoneMap.DoorInWallAlongZ(d) ? new Vector3(0.5f, 0f, 0f) : new Vector3(0f, 0f, 0.5f);
                var along = ZoneMap.DoorInWallAlongZ(d) ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);
                // Both ends of the gap, not just the middle, open from one zone into the other.
                foreach (float t in new[] { -ZoneMap.GapWidth / 2f, 0f, ZoneMap.GapWidth / 2f })
                {
                    var sides = new HashSet<int> { ZoneMap.ZoneOfPoint(c + along * t - across), ZoneMap.ZoneOfPoint(c + along * t + across) };
                    Assert.IsTrue(sides.SetEquals(new[] { link.x, link.y }), "door " + d + " isn't on the wall between " + ZoneMap.ZoneNames[link.x] + " and " + ZoneMap.ZoneNames[link.y]);
                }
            }
            Assert.Less(ZoneMap.DoorLinks.Length, 31, "open doors are a bitmask");
        }

        [Test]
        public void Zones_ComeInDifferentShapesAndSizes()
        {
            var areas = new HashSet<int>();
            int shaped = 0;
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
            {
                int area = 0;
                foreach (var r in ZoneMap.ZoneRects(z)) area += r.width * r.height;
                areas.Add(area);
                if (ZoneMap.ZoneRects(z).Length > 1) shaped++;
                // Every zone's anchor (where its name goes) is on its own floor.
                Assert.AreEqual(z, ZoneMap.ZoneOfPoint(ZoneMap.ZoneCenter(z)), ZoneMap.ZoneNames[z]);
            }
            Assert.Greater(areas.Count, 8, "zones should vary in size");
            Assert.GreaterOrEqual(shaped, 6, "at least half the zones should be more than a plain rectangle");
        }

        [Test]
        public void WallThings_StandAgainstARealWall_ClearOfDoors()
        {
            var spots = new List<ZoneMap.WallSpot>();
            spots.AddRange(ZoneMap.WallBuySpots);
            spots.AddRange(ZoneMap.PerkSpots);
            spots.Add(ZoneMap.ArmorSpot);
            spots.Add(ZoneMap.RerollSpot);
            spots.AddRange(ZoneMap.WindowSpots);
            spots.AddRange(ZoneMap.GeneratorSpots);
            foreach (var spot in spots)
            {
                var (face, inward) = spot.Resolve();
                string what = ZoneMap.ZoneNames[spot.Zone] + " " + spot.Side + " " + spot.Along;
                Assert.AreEqual(spot.Zone, ZoneMap.ZoneOfPoint(face + inward * 0.5f), what + " isn't inside its zone");
                Assert.AreNotEqual(spot.Zone, ZoneMap.ZoneOfPoint(face - inward * 0.8f), what + " has no wall behind it");
                for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
                    Assert.Greater(Vector3.Distance(face - inward * ZoneMap.WallThickness / 2f, ZoneMap.DoorCenter(d)), 3f, what + " is in door " + d);
            }
            Assert.AreEqual(ZoneMap.ZoneCount, ZoneMap.GeneratorSpots.Length);
            Assert.AreEqual(ZoneMap.WallBuyZones.Length, ZoneMap.WallBuySpots.Length);
            for (int i = 0; i < ZoneMap.WallBuySpots.Length; i++) Assert.AreEqual(ZoneMap.WallBuyZones[i], ZoneMap.WallBuySpots[i].Zone);
        }

        [Test]
        public void Walls_CloseOffTheMap()
        {
            Assert.Greater(ZoneMap.Walls.Count, 40);
            int lintels = 0;
            foreach (var piece in ZoneMap.Walls)
                if (piece.Bottom > 0f) lintels++;
            Assert.AreEqual(ZoneMap.DoorLinks.Length, lintels, "one lintel over every door");
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
            Assert.AreEqual(ZoneMap.StartZone, ZoneMap.ZoneOfPoint(ZoneMap.StartPosition));
            Assert.AreEqual(ZoneMap.StartZone, ZoneMap.ZoneOfPoint(ZoneMap.ShopWorldPosition));
            Assert.AreEqual(9, ZoneMap.ZoneOfPoint(ZoneMap.MachineWorldPosition), "the upgrade machine is in the Lantern Core");
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
