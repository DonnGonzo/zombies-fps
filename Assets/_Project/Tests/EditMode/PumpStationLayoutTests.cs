using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Tests
{
    public class PumpStationLayoutTests
    {
        [Test]
        public void ExpandedPumpStation_DoesNotOverlapNeighbours()
        {
            int area = 0;
            foreach (var r in ZoneMap.ZoneRects(7))
            {
                area += r.width * r.height;
                for (int zone = 0; zone < ZoneMap.ZoneCount; zone++)
                {
                    if (zone == 7) continue;
                    foreach (var other in ZoneMap.ZoneRects(zone))
                        Assert.IsFalse(r.Overlaps(other), "Pump Station overlaps " + ZoneMap.ZoneNames[zone]);
                }
            }
            Assert.Greater(area, 756, "The wider hall and inspection yard must retain their additional space.");
        }

        [Test]
        public void AuthoredPumpStructures_LeaveDoorApproachesClear_AndSupportMovedPart()
        {
            // Build only this room to test real colliders without networking or a full-map NavMesh bake.
            // The east corridor would intersect the original solid Pump House at local (13..18.7, 0..6.7).
            var root = new GameObject("Pump Station clearance test");
            bool photos = SurfaceTextures.PhotosEnabled;
            try
            {
                SurfaceTextures.PhotosEnabled = false;
                var map = new ZoneMap();
                typeof(ZoneMap).GetProperty("Root").SetValue(map, root.transform);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var decor = typeof(ZoneMap).GetField("_decor", flags);
                decor.SetValue(map, Activator.CreateInstance(decor.FieldType, true));
                typeof(ZoneMap).GetMethod("BuildPumpStation", flags).Invoke(map, null);
                typeof(ZoneMap).GetMethod("PumpStationMakeover", flags).Invoke(map, null);
                Physics.SyncTransforms();
                var origin = ZoneMap.ZoneCenter(7);
                var corridors = new[]
                {
                    new Bounds(origin + new Vector3(14.5f, 1.05f, 3f), new Vector3(9f, 1.9f, 6f)),
                    new Bounds(origin + new Vector3(-16f, 1.05f, 0f), new Vector3(6f, 1.9f, 4f)),
                    new Bounds(origin + new Vector3(11f, 1.05f, -3.5f), new Vector3(5f, 1.9f, 7f)),
                    new Bounds(origin + new Vector3(0f, 1.05f, 21.25f), new Vector3(4f, 1.9f, 3.5f)),
                };
                var names = new[] { "Rampart Ruins", "Garage", "Loading Dock", "Crane Yard" };
                var colliders = root.GetComponentsInChildren<Collider>();
                for (int i = 0; i < corridors.Length; i++)
                    foreach (var collider in colliders)
                        Assert.IsFalse(collider.bounds.Intersects(corridors[i]), names[i] + " approach blocked by " + collider.name);
                // The relocated shield part must stand on the new control-house roof, not float in the old exit.
                var ray = new Ray(origin + new Vector3(-7f, 2.6f, 15f), Vector3.down);
                bool supported = false;
                foreach (var collider in colliders)
                    if (collider.Raycast(ray, out _, 0.3f)) supported = true;
                Assert.IsTrue(supported, "The relocated part needs a supporting roof at height 2.4m.");
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
