using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Tests
{
    public class RemainingRoomLayoutTests
    {
        [TestCase(8, "BuildCraneYard", "CraneYardMakeover")]
        [TestCase(13, "BuildPlagueWard", "PlagueWardMakeover")]
        [TestCase(12, "BuildGaol", "GaolMakeover")]
        [TestCase(4, "BuildBarracks", "BarracksMakeover")]
        [TestCase(14, "BuildScriptorium", "ScriptoriumMakeover")]
        [TestCase(15, "BuildFurnace", "FurnaceMakeover")]
        public void AuthoredRoom_AllEntrancesObjectivesAndInteractionsAreReachable(int zone, string build, string dress)
        {
            var root = new GameObject("Room navigation test");
            bool photos = SurfaceTextures.PhotosEnabled;
            NavMeshData data = null;
            NavMeshDataInstance instance = default;
            try
            {
                SurfaceTextures.PhotosEnabled = false;
                var map = new ZoneMap();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ZoneMap).GetProperty("Root").SetValue(map, root.transform);
                var decor = typeof(ZoneMap).GetField("_decor", flags);
                decor.SetValue(map, Activator.CreateInstance(decor.FieldType, true));
                typeof(ZoneMap).GetMethod(build, flags).Invoke(map, null);
                typeof(ZoneMap).GetMethod(dress, flags).Invoke(map, null);
                var targets = new List<(string name, Vector3 point)>();
                for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
                {
                    var link = ZoneMap.DoorLinks[d];
                    if (link.x != zone && link.y != zone) continue;
                    var center = ZoneMap.DoorCenter(d);
                    var direction = ZoneMap.DoorInWallAlongZ(d) ? Vector3.right : Vector3.forward;
                    if (ZoneMap.ZoneOfPoint(center + direction) != zone) direction = -direction;
                    targets.Add(("door " + d, center + direction * 1.3f));
                }
                var spots = new List<ZoneMap.WallSpot>();
                spots.AddRange(ZoneMap.WindowSpots);
                spots.AddRange(ZoneMap.WallBuySpots);
                spots.AddRange(ZoneMap.PerkSpots);
                spots.AddRange(ZoneMap.GeneratorSpots);
                spots.Add(ZoneMap.ArmorSpot);
                spots.Add(ZoneMap.RerollSpot);
                foreach (var spot in spots)
                {
                    if (spot.Zone != zone) continue;
                    var (face, inward) = spot.Resolve();
                    targets.Add(("wall interaction " + face, face + inward * 1.7f));
                }
                for (int p = 0; p < ZoneMap.PartSpotCount; p++)
                    if (ZoneMap.ZoneOfPoint(ZoneMap.PartSpot(p)) == zone) targets.Add(("part " + p, ZoneMap.PartSpot(p)));
                for (int h = 0; h < ZoneMap.HighSpotCount; h++)
                    if (ZoneMap.HighSpotZones[h] == zone) targets.Add(("mission " + h, ZoneMap.HighSpot(h)));
                if (zone == 14) targets.Add(("reader's dais", ZoneMap.ZoneCenter(zone) + new Vector3(0f, 0.4f, 14.5f)));
                if (zone == 8)
                {
                    targets.Add(("west freight top", ZoneMap.ZoneCenter(zone) + new Vector3(-14f, 1.1f, -9f)));
                    targets.Add(("east freight top", ZoneMap.ZoneCenter(zone) + new Vector3(18f, 1.1f, 7.5f)));
                }

                var bounds = new Bounds(ZoneMap.ZoneCenter(zone), Vector3.zero);
                foreach (var r in ZoneMap.ZoneRects(zone))
                {
                    foreach (var otherZone in AllOtherRects(zone)) Assert.IsFalse(r.Overlaps(otherZone), "Overlapping rooms");
                    Box(root, new Vector3(r.center.x, -0.2f, r.center.y), new Vector3(r.width, 0.4f, r.height));
                    bounds.Encapsulate(new Vector3(r.xMin, -1f, r.yMin));
                    bounds.Encapsulate(new Vector3(r.xMax, 12f, r.yMax));
                }
                foreach (var wall in ZoneMap.Walls)
                {
                    var mid = (wall.A + wall.B) / 2f;
                    float h = wall.Top - wall.Bottom;
                    Box(root, new Vector3(mid.x, wall.Bottom + h / 2f, mid.y), wall.AlongX
                        ? new Vector3(wall.Length, h, ZoneMap.WallThickness) : new Vector3(ZoneMap.WallThickness, h, wall.Length));
                }
                Physics.SyncTransforms();
                var sources = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                var settings = NavMesh.GetSettingsByIndex(0);
                settings.minRegionArea = 0.5f;
                bounds.Expand(2f);
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                Assert.IsNotNull(data);
                instance = NavMesh.AddNavMeshData(data);
                typeof(ZoneMap).GetMethod("AddNavLinks", flags).Invoke(map, null);
                Assert.IsTrue(NavMesh.SamplePosition(targets[0].point, out var start, 0.65f, NavMesh.AllAreas));
                foreach (var target in targets)
                {
                    Assert.IsTrue(NavMesh.SamplePosition(target.point, out var end, 0.65f, NavMesh.AllAreas),
                        ZoneMap.ZoneNames[zone] + ": no walkable floor at " + target.name);
                    var path = new NavMeshPath();
                    Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, ZoneMap.ZoneNames[zone] + ": unreachable " + target.name);
                }
            }
            finally
            {
                if (instance.valid) instance.Remove();
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                SurfaceTextures.PhotosEnabled = photos;
                UnityEngine.Object.DestroyImmediate(root);
                Ladder.ClearAll();
            }
        }

        static IEnumerable<RectInt> AllOtherRects(int zone)
        {
            for (int other = 0; other < ZoneMap.ZoneCount; other++)
                if (other != zone)
                    foreach (var rect in ZoneMap.ZoneRects(other)) yield return rect;
        }

        static void Box(GameObject root, Vector3 at, Vector3 size)
        {
            var go = new GameObject("Room boundary");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = at;
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
