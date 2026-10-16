using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Greybox arena for Phase 0: a walled yard with cover, a raised platform and boarded-up
    /// spawn windows. The NavMesh is baked at runtime from its colliders.
    /// </summary>
    public static class PrototypeArena
    {
        public const float HalfSize = 22f;
        const float WallHeight = 5f;

        static readonly Color Floor = new Color(0.33f, 0.34f, 0.33f);
        static readonly Color Wall = new Color(0.46f, 0.45f, 0.42f);
        static readonly Color Cover = new Color(0.4f, 0.36f, 0.3f);
        static readonly Color Concrete = new Color(0.55f, 0.55f, 0.53f);
        static readonly Color Board = new Color(0.3f, 0.2f, 0.12f);
        static readonly Color Lamp = new Color(1f, 0.85f, 0.6f);

        public struct Result
        {
            public Transform Root;
            public Transform[] SpawnPoints;
            public Vector3 PlayerStart;
        }

        public static Result Build()
        {
            var root = new GameObject("Arena").transform;
            float s = HalfSize;

            Greybox.Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(s * 2f, 1f, s * 2f), Floor);

            // Perimeter walls.
            Greybox.Box("Wall N", root, new Vector3(0f, WallHeight / 2f, s + 0.5f), new Vector3(s * 2f + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall S", root, new Vector3(0f, WallHeight / 2f, -s - 0.5f), new Vector3(s * 2f + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall E", root, new Vector3(s + 0.5f, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, s * 2f), Wall);
            Greybox.Box("Wall W", root, new Vector3(-s - 0.5f, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, s * 2f), Wall);

            // Pillars.
            foreach (var p in new[] { new Vector3(8f, 0f, 8f), new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, -8f), new Vector3(-8f, 0f, -8f) })
                Greybox.Box("Pillar", root, p + Vector3.up * (WallHeight / 2f), new Vector3(1.6f, WallHeight, 1.6f), Concrete);

            // Crates and low walls for cover and kiting.
            Greybox.Box("Crate", root, new Vector3(4f, 0.6f, 14f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", root, new Vector3(5.3f, 0.6f, 14.4f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", root, new Vector3(4.6f, 1.8f, 14.2f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", root, new Vector3(-14f, 0.6f, -3f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", root, new Vector3(-13f, 0.5f, -4.4f), new Vector3(1f, 1f, 1f), Cover);
            Greybox.Box("Low wall", root, new Vector3(0f, 0.55f, -4f), new Vector3(6f, 1.1f, 0.5f), Concrete);
            Greybox.Box("Low wall", root, new Vector3(14f, 0.55f, 2f), new Vector3(0.5f, 1.1f, 7f), Concrete);
            Greybox.Box("Low wall", root, new Vector3(-6f, 0.55f, 15f), new Vector3(5f, 1.1f, 0.5f), Concrete);

            // Raised platform with a ramp in the south-east corner.
            Greybox.Box("Platform", root, new Vector3(16f, 1f, -16f), new Vector3(8f, 2f, 8f), Concrete);
            var ramp = Greybox.Box("Ramp", root, new Vector3(16f, 0.95f, -8.6f), new Vector3(3f, 0.3f, 7.4f), Cover);
            ramp.transform.localRotation = Quaternion.Euler(16f, 0f, 0f); // High end meets the platform edge.

            // Boarded windows on the walls mark where zombies come from.
            var spawnPoints = new List<Transform>();
            AddWindow(root, spawnPoints, new Vector3(0f, 0f, s - 1.2f), 180f);
            AddWindow(root, spawnPoints, new Vector3(-12f, 0f, s - 1.2f), 180f);
            AddWindow(root, spawnPoints, new Vector3(12f, 0f, s - 1.2f), 180f);
            AddWindow(root, spawnPoints, new Vector3(-s + 1.2f, 0f, 6f), 90f);
            AddWindow(root, spawnPoints, new Vector3(-s + 1.2f, 0f, -12f), 90f);
            AddWindow(root, spawnPoints, new Vector3(s - 1.2f, 0f, 10f), -90f);
            AddWindow(root, spawnPoints, new Vector3(0f, 0f, -s + 1.2f), 0f);
            AddWindow(root, spawnPoints, new Vector3(-10f, 0f, -s + 1.2f), 0f);

            // Lamps over the yard.
            AddLamp(root, new Vector3(0f, 4.4f, 0f));
            AddLamp(root, new Vector3(-12f, 4.4f, 10f));
            AddLamp(root, new Vector3(12f, 4.4f, 8f));
            AddLamp(root, new Vector3(-10f, 4.4f, -12f));
            AddLamp(root, new Vector3(14f, 4.4f, -14f));

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            return new Result
            {
                Root = root,
                SpawnPoints = spawnPoints.ToArray(),
                PlayerStart = new Vector3(0f, 0.05f, -12f),
            };
        }

        static void AddWindow(Transform root, List<Transform> points, Vector3 position, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // Boards sit on the wall face behind the spawn point.
            Vector3 wallFace = position - rot * Vector3.forward * 0.65f;
            for (int i = 0; i < 3; i++)
            {
                var board = Greybox.Box("Board", root, wallFace + Vector3.up * (1.1f + i * 0.45f), new Vector3(1.8f, 0.22f, 0.08f), Board, collider: false);
                board.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (i - 1) * 9f);
            }
            var point = Greybox.Empty("ZombieSpawn", root, position);
            point.localRotation = rot;
            points.Add(point);
        }

        static void AddLamp(Transform root, Vector3 position)
        {
            Greybox.Prim(PrimitiveType.Cube, "Lamp", root, position, new Vector3(0.5f, 0.1f, 0.5f), Lamp, collider: false, unlit: true);
            var go = new GameObject("Lamp Light");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position + Vector3.down * 0.3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 1.6f;
            light.color = Lamp;
            light.shadows = LightShadows.None;
        }
    }
}
