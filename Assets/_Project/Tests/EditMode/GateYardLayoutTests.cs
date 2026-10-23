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
    public class GateYardLayoutTests
    {
        [Test]
        public void GateYard_HasRecessedEntrances_WithoutOverlappingNeighbours()
        {
            foreach (var r in ZoneMap.ZoneRects(0))
                for (int zone = 1; zone < ZoneMap.ZoneCount; zone++)
                    foreach (var other in ZoneMap.ZoneRects(zone))
                        Assert.IsFalse(r.Overlaps(other), "Gate Yard overlaps " + ZoneMap.ZoneNames[zone]);
            Assert.AreEqual(0, ZoneMap.ZoneOfPoint(new Vector3(14f, 0f, 0f)), "Depot hall");
            Assert.AreEqual(-1, ZoneMap.ZoneOfPoint(new Vector3(14f, 0f, 7f)), "Keep the setback beside the hall");
            Assert.AreEqual(0, ZoneMap.ZoneOfPoint(new Vector3(-12f, 0f, -18f)), "Southwest market pocket");
            Assert.AreEqual(-1, ZoneMap.ZoneOfPoint(new Vector3(6f, 0f, 14f)), "Keep the northeast setback");
        }

        [Test]
        public void SpawnShopDoorsAndPickups_RemainClearAndNavigable()
        {
            var root = new GameObject("Gate Yard layout test");
            bool photos = SurfaceTextures.PhotosEnabled;
            Vector3 playerStart = GameConfig.PlayerStart;
            NavMeshData navData = null;
            NavMeshDataInstance navInstance = default;
            try
            {
                SurfaceTextures.PhotosEnabled = false;
                GameConfig.PlayerStart = ZoneMap.StartPosition;
                var map = new ZoneMap();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ZoneMap).GetProperty("Root").SetValue(map, root.transform);
                var decor = typeof(ZoneMap).GetField("_decor", flags);
                decor.SetValue(map, Activator.CreateInstance(decor.FieldType, true));
                foreach (string method in new[] { "BuildGateYard", "GateYardMakeover", "BuildShop" })
                    typeof(ZoneMap).GetMethod(method, flags).Invoke(map, null);
                Physics.SyncTransforms();
                var colliders = root.GetComponentsInChildren<Collider>();
                var clearances = new List<(string name, Bounds bounds)>
                {
                    ("East/west avenue", new Bounds(new Vector3(0f, 1.05f, 0f), new Vector3(32f, 1.9f, 4f))),
                    ("Chapel approach", new Bounds(new Vector3(-4f, 1.05f, 8f), new Vector3(4f, 1.9f, 16f))),
                    ("Gaol passage", new Bounds(new Vector3(0f, 1.05f, -15f), new Vector3(4f, 1.9f, 14f))),
                    ("Trader customer side", new Bounds(new Vector3(8.8f, 1.05f, -6f), new Vector3(2.2f, 1.9f, 4f))),
                    ("Ground collectible", new Bounds(new Vector3(-13f, 1.05f, -11f), new Vector3(1.8f, 1.9f, 1.8f))),
                };
                for (int slot = 0; slot < GameConfig.MaxPlayers; slot++)
                    clearances.Add(("Player spawn " + slot, new Bounds(GameConfig.PlayerSpawn(slot) + Vector3.up,
                        new Vector3(1.2f, 1.9f, 1.2f))));
                var targets = new List<Vector3>
                {
                    new Vector3(-15f, 0f, 0f), new Vector3(15f, 0f, 0f), new Vector3(-4f, 0f, 15f),
                    new Vector3(0f, 0f, -21f), new Vector3(9f, 0f, -6f), new Vector3(-13f, 0f, -11f),
                    new Vector3(-12.7f, 2.2f, 13f),
                };
                var wallSpots = new List<ZoneMap.WallSpot>();
                wallSpots.AddRange(ZoneMap.WindowSpots);
                wallSpots.AddRange(ZoneMap.WallBuySpots);
                wallSpots.AddRange(ZoneMap.PerkSpots);
                wallSpots.AddRange(ZoneMap.GeneratorSpots);
                foreach (var spot in wallSpots)
                {
                    if (spot.Zone != 0) continue;
                    var (face, inward) = spot.Resolve();
                    var approach = face + inward * 1.4f;
                    targets.Add(approach);
                    clearances.Add(("Wall interaction " + face, new Bounds(approach + Vector3.up * 1.05f, new Vector3(1.8f, 1.9f, 1.8f))));
                }
                foreach (var entry in clearances)
                    foreach (var collider in colliders)
                        Assert.IsFalse(collider.bounds.Intersects(entry.bounds), entry.name + " blocked by " + collider.name);
                bool supported = false;
                foreach (var collider in colliders)
                    if (collider.Raycast(new Ray(new Vector3(-12.7f, 2.4f, 13f), Vector3.down), out _, 0.3f)) supported = true;
                Assert.IsTrue(supported, "Lookout collectible needs a supporting platform");

                var powerBuyables = new List<Buyable>();
                typeof(ZoneMap).GetMethod("AddPowerBuyables", flags).Invoke(map, new object[] { powerBuyables });
                foreach (var buyable in powerBuyables)
                {
                    if (buyable.Kind != BuyableKind.Note) continue;
                    var note = ZoneMap.NotePosition(buyable.Index);
                    Assert.AreEqual(0, ZoneMap.ZoneOfPoint(note), "Note outside Gate Yard: " + buyable.Index);
                    Assert.Less(note.y, 2f, "Every note should be readable from the ground");
                    targets.Add(buyable.Position - Vector3.up);
                    var eye = buyable.Position + Vector3.up * 0.55f;
                    var direction = note - eye;
                    foreach (var collider in colliders)
                        Assert.IsFalse(collider.Raycast(new Ray(eye, direction.normalized), out _, direction.magnitude - 0.02f),
                            "Note " + buyable.Index + " hidden by " + collider.name);
                }

                // Bake the room's real footprint, authored colliders and boundary walls; test walking paths,
                // not just straight-line clearance. Closed buyable doors intentionally stay out of this bake.
                foreach (var r in ZoneMap.ZoneRects(0))
                    Box(root, new Vector3(r.center.x, -0.2f, r.center.y), new Vector3(r.width, 0.4f, r.height));
                foreach (var wall in ZoneMap.Walls)
                {
                    var mid = (wall.A + wall.B) / 2f;
                    float h = wall.Top - wall.Bottom;
                    Box(root, new Vector3(mid.x, wall.Bottom + h / 2f, mid.y),
                        wall.AlongX ? new Vector3(wall.Length, h, ZoneMap.WallThickness) : new Vector3(ZoneMap.WallThickness, h, wall.Length));
                }
                Physics.SyncTransforms();
                var sources = new List<NavMeshBuildSource>();
                var roomColliders = root.GetComponentsInChildren<Collider>();
                foreach (var buyable in powerBuyables)
                {
                    if (buyable.Kind != BuyableKind.Note) continue;
                    var note = ZoneMap.NotePosition(buyable.Index);
                    var eye = buyable.Position + Vector3.up * 0.55f;
                    var sight = note - eye;
                    var behind = note - buyable.Position;
                    behind.y = 0f;
                    bool wallBehind = false;
                    foreach (var collider in roomColliders)
                    {
                        Assert.IsFalse(collider.Raycast(new Ray(eye, sight.normalized), out _, sight.magnitude - 0.02f),
                            "Room boundary hides note " + buyable.Index);
                        if (collider.Raycast(new Ray(note, behind.normalized), out _, 0.2f)) wallBehind = true;
                    }
                    Assert.IsTrue(wallBehind, "Note " + buyable.Index + " must be pinned to a real wall");
                }
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                var settings = NavMesh.GetSettingsByIndex(0);
                settings.minRegionArea = 0.5f;
                navData = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(0f, 3f, -3f), new Vector3(40f, 14f, 44f)), Vector3.zero, Quaternion.identity);
                Assert.IsNotNull(navData);
                navInstance = NavMesh.AddNavMeshData(navData);
                Assert.IsTrue(NavMesh.SamplePosition(ZoneMap.StartPosition, out var start, 0.6f, NavMesh.AllAreas));
                foreach (var target in targets)
                {
                    Assert.IsTrue(NavMesh.SamplePosition(target, out var end, 0.6f, NavMesh.AllAreas), "No walkable floor at " + target);
                    var path = new NavMeshPath();
                    Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Unreachable from spawn: " + target);
                }
            }
            finally
            {
                if (navInstance.valid) navInstance.Remove();
                if (navData != null) UnityEngine.Object.DestroyImmediate(navData);
                SurfaceTextures.PhotosEnabled = photos;
                GameConfig.PlayerStart = playerStart;
                UnityEngine.Object.DestroyImmediate(root);
                Ladder.ClearAll();
            }
        }

        static void Box(GameObject parent, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Layout collision");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
