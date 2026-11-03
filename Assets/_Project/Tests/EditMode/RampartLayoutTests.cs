using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Tests
{
    public class RampartLayoutTests
    {
        [Test]
        public void ExpandedRampart_DoesNotOverlapNeighbours()
        {
            int area = 0;
            foreach (var r in ZoneMap.ZoneRects(16))
            {
                area += r.width * r.height;
                for (int zone = 0; zone < ZoneMap.ZoneCount; zone++)
                {
                    if (zone == 16) continue;
                    foreach (var other in ZoneMap.ZoneRects(zone))
                        Assert.IsFalse(r.Overlaps(other), "Rampart Ruins overlaps " + ZoneMap.ZoneNames[zone]);
                }
            }
            Assert.Greater(area, 1524, "The expanded fortress yard must retain its additional space.");
        }

        [Test]
        public void AuthoredRampart_LeavesEntrancesAndCentralRouteClear()
        {
            // Check actual authored collision, independent of optional model rendering.
            var root = new GameObject("Rampart Ruins clearance test");
            bool photos = SurfaceTextures.PhotosEnabled;
            try
            {
                SurfaceTextures.PhotosEnabled = false;
                var map = new ZoneMap();
                typeof(ZoneMap).GetProperty("Root").SetValue(map, root.transform);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var decor = typeof(ZoneMap).GetField("_decor", flags);
                decor.SetValue(map, Activator.CreateInstance(decor.FieldType, true));
                typeof(ZoneMap).GetMethod("BuildRampartRuins", flags).Invoke(map, null);
                typeof(ZoneMap).GetMethod("RampartMakeover", flags).Invoke(map, null);
                typeof(ZoneMap).GetMethod("SealRampartPillars", flags).Invoke(map, null);
                Physics.SyncTransforms();
                var origin = ZoneMap.ZoneCenter(16);
                var corridors = new[]
                {
                    new Bounds(origin + new Vector3(-7f, 1.05f, -10f), new Vector3(12f, 1.9f, 5f)),
                    new Bounds(origin + new Vector3(-6f, 1.05f, 32f), new Vector3(10f, 1.9f, 5f)),
                    new Bounds(origin + new Vector3(0f, 1.05f, 12f), new Vector3(4f, 1.9f, 60f)),
                };
                var names = new[] { "Pump Station", "Crane Yard", "Central avenue" };
                var colliders = root.GetComponentsInChildren<Collider>();
                int seals = 0;
                foreach (var collider in colliders)
                {
                    if (collider.name != "Rampart pillar seal") continue;
                    seals++;
                    Assert.IsFalse(collider.isTrigger);
                    Assert.IsNull(collider.GetComponent<Renderer>(), "Pillar seals must be invisible");
                    Assert.AreEqual(0, ZoneMap.ShotMask & (1 << collider.gameObject.layer), "Shots must pass through seals");
                    Assert.Less(collider.bounds.min.y, 3.6f, "Seal must overlap the pillar below its cap");
                    Assert.Greater(collider.bounds.max.y, 50f, "Seal must not create another reachable perch");
                }
                Assert.AreEqual(4, seals, "Protect both pairs of gatehouse pillars");
                foreach (float z in new[] { 3f, 21f })
                    foreach (float x in new[] { -5.5f, 5.5f })
                    {
                        bool blocked = false;
                        // Sweep a player's body from the wall walk towards each pier cap.
                        var ray = new Ray(origin + new Vector3(x + 2f, 4.2f, z), Vector3.left);
                        foreach (var collider in colliders)
                            if (collider.name == "Rampart pillar seal" && collider.Raycast(ray, out _, 2f)) blocked = true;
                        Assert.IsTrue(blocked, "Jump onto pillar must be blocked at " + x + ", " + z);
                    }
                for (int i = 0; i < corridors.Length; i++)
                    foreach (var collider in colliders)
                        Assert.IsFalse(collider.bounds.Intersects(corridors[i]), names[i] + " approach blocked by " + collider.name);
                // Both ends and the centre of the elevated route have solid support.
                foreach (float z in new[] { -13f, 9f, 27f })
                {
                    var ray = new Ray(origin + new Vector3(11f, 3.4f, z), Vector3.down);
                    bool supported = false;
                    foreach (var collider in colliders)
                        if (collider.Raycast(ray, out _, 0.3f)) supported = true;
                    Assert.IsTrue(supported, "Battlement must remain continuous at " + z);
                }
            }
            finally
            {
                SurfaceTextures.PhotosEnabled = photos;
                UnityEngine.Object.DestroyImmediate(root);
                Ladder.ClearAll();
            }
        }
    }
}
