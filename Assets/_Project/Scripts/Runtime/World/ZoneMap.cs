using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.World
{
    public enum BuyableKind
    {
        Door,
        WallGun,
        UpgradeMachine,
        Shop,
        /// <summary>A perk tonic machine; Index is the PerkKind.</summary>
        Perk,
        /// <summary>The armour plate station.</summary>
        Armor,
        /// <summary>The shrine that sells power-up rerolls.</summary>
        Reroll,
    }

    /// <summary>Something a player can stand at and press E to buy. Index is the door or wall-buy number.</summary>
    public class Buyable
    {
        public BuyableKind Kind;
        public int Index;
        /// <summary>Where the player stands to use it (floor level + 1 m).</summary>
        public Vector3 Position;
        public float Range;
    }

    public class Zone
    {
        public int Id;
        public string Name;
        public Vector3 Center;
        public readonly List<Transform> SpawnPoints = new List<Transform>();

        public bool Contains(Vector3 p) =>
            Mathf.Abs(p.x - Center.x) <= ZoneMap.ZoneHalfSize && Mathf.Abs(p.z - Center.z) <= ZoneMap.ZoneHalfSize;
    }

    /// <summary>
    /// The Phase 3 greybox map: six 24 m zones in a 3 x 2 grid, linked by six buyable doors into one loop,
    /// so there is always a way round to retreat and kite. Players start in the Gate Yard; each door opens
    /// a new zone and its zombie windows. Built in code on every machine, and the NavMesh is baked at
    /// runtime with the doors left out so they can carve it while closed.
    ///
    ///   Barracks (4) | Chapel (3)    | Garage (2)
    ///   -------------+---------------+-----------
    ///   Courtyard (5)| Gate Yard (0) | Depot (1)
    /// </summary>
    public class ZoneMap
    {
        public const int ZoneCount = 6;
        public const int StartZone = 0;
        public const float ZoneHalfSize = 12f;
        const float ZoneSize = ZoneHalfSize * 2f;
        const float WallHeight = 5f;
        const float InnerWall = 0.6f;
        const float GapWidth = 3.2f;
        const float GapHeight = 3.2f;
        const float DoorRange = 2.6f;
        const float WallBuyRange = 2f;
        const float MachineRange = 2.2f;
        /// <summary>The upgrade machine stands at the far end of the Chapel, the zone furthest from the start.</summary>
        static readonly Vector3 MachinePosition = new Vector3(0f, 0f, 33.4f);
        /// <summary>The spawn shop's counter in the Gate Yard, served from the east side.</summary>
        static readonly Vector3 ShopPosition = new Vector3(8.6f, 0f, 3f);
        const float ShopRange = 2.2f;

        public static readonly string[] ZoneNames = { "Gate Yard", "Depot", "Garage", "Chapel", "Barracks", "Courtyard" };

        static readonly Vector3[] ZoneCenters =
        {
            new Vector3(0f, 0f, 0f), new Vector3(24f, 0f, 0f), new Vector3(24f, 0f, 24f),
            new Vector3(0f, 0f, 24f), new Vector3(-24f, 0f, 24f), new Vector3(-24f, 0f, 0f),
        };

        /// <summary>The zones each door joins, in door order. PriceTable.doorCosts follows this order.</summary>
        public static readonly Vector2Int[] DoorLinks =
        {
            new Vector2Int(0, 1), new Vector2Int(0, 5), new Vector2Int(1, 2),
            new Vector2Int(5, 4), new Vector2Int(2, 3), new Vector2Int(4, 3),
        };

        static readonly Vector3[] DoorCenters =
        {
            new Vector3(12f, 0f, 0f), new Vector3(-12f, 0f, 0f), new Vector3(24f, 0f, 12f),
            new Vector3(-24f, 0f, 12f), new Vector3(12f, 0f, 24f), new Vector3(-12f, 0f, 24f),
        };

        /// <summary>Which zone each wall gun is in, in wall-buy order. PriceTable.wallGuns follows this order.</summary>
        public static readonly int[] WallBuyZones = { 0, 1, 5, 2, 4, 3, 1, 4, 2 };

        /// <summary>Perk machines in PerkKind order: a wall face and the direction into the room.</summary>
        static readonly (Vector3 face, Vector3 inward)[] PerkSpots =
        {
            (new Vector3(-11.7f, 0f, 31f), Vector3.right),     // Ironhide Tonic, Chapel
            (new Vector3(16f, 0f, -12f), Vector3.forward),     // Slick Fingers, Depot
            (new Vector3(-11.7f, 0f, -7f), Vector3.right),     // Second Wind, Gate Yard
            (new Vector3(36f, 0f, 31f), Vector3.left),         // Hair Trigger, Garage
            (new Vector3(-31f, 0f, 12.3f), Vector3.forward),   // Fleet Foot, Barracks
            (new Vector3(11.7f, 0f, 17f), Vector3.left),       // Steady Hand, Chapel
            (new Vector3(-31f, 0f, 11.7f), Vector3.back),      // Hazard Ward, Courtyard
            (new Vector3(-12.3f, 0f, 8f), Vector3.left),       // Bloodline, Courtyard
        };
        /// <summary>Armour plates in the Gate Yard; power-up rerolls in the Chapel.</summary>
        static readonly (Vector3 face, Vector3 inward) ArmorSpot = (new Vector3(11.7f, 0f, -8f), Vector3.left);
        static readonly (Vector3 face, Vector3 inward) RerollSpot = (new Vector3(-11.7f, 0f, 19f), Vector3.right);
        const float StationRange = 2f;

        /// <summary>One mission generator per zone (zone order), against a wall and clear of windows and doors.</summary>
        static readonly (Vector3 position, float yaw)[] GeneratorSpots =
        {
            (new Vector3(0f, 0f, -10.6f), 180f),
            (new Vector3(32f, 0f, -10.6f), 180f),
            (new Vector3(34.6f, 0f, 14f), 90f),
            (new Vector3(9f, 0f, 34.6f), 0f),
            (new Vector3(-31f, 0f, 34.6f), 0f),
            (new Vector3(-32f, 0f, -10.6f), 180f),
        };

        public static Vector3 ZoneCenter(int zone) => ZoneCenters[Mathf.Clamp(zone, 0, ZoneCount - 1)];

        /// <summary>Where a protect mission's survivor crouches: a few metres out from the zone's generator, towards the middle.</summary>
        public static Vector3 SurvivorSpot(int zone)
        {
            var gen = GeneratorSpots[Mathf.Clamp(zone, 0, ZoneCount - 1)].position;
            var toCenter = ZoneCenter(zone) - gen;
            toCenter.y = 0f;
            return gen + toCenter.normalized * 3.2f;
        }

        static readonly Color[] FloorTints =
        {
            new Color(0.33f, 0.34f, 0.33f), new Color(0.36f, 0.34f, 0.3f), new Color(0.3f, 0.31f, 0.33f),
            new Color(0.34f, 0.3f, 0.3f), new Color(0.31f, 0.33f, 0.3f), new Color(0.36f, 0.36f, 0.33f),
        };
        static readonly Color Wall = new Color(0.46f, 0.45f, 0.42f);
        static readonly Color Cover = new Color(0.4f, 0.36f, 0.3f);
        static readonly Color Concrete = new Color(0.55f, 0.55f, 0.53f);
        static readonly Color Board = new Color(0.3f, 0.2f, 0.12f);
        static readonly Color DoorWood = new Color(0.34f, 0.23f, 0.13f);
        static readonly Color Chalk = new Color(0.82f, 0.8f, 0.72f);
        static readonly Color Lamp = new Color(1f, 0.85f, 0.6f);

        public Transform Root { get; private set; }
        public Zone[] Zones { get; private set; }
        public Door[] Doors { get; private set; }
        public Buyable[] Buyables { get; private set; }
        /// <summary>Mission generators, indexed by zone.</summary>
        public Generator[] Generators { get; private set; }
        public Vector3 PlayerStart { get; private set; }

        // ---------------------------------------------------------------- queries

        /// <summary>The zone a position is in, or -1 outside the map.</summary>
        public int ZoneAt(Vector3 position)
        {
            for (int i = 0; i < Zones.Length; i++)
                if (Zones[i].Contains(position)) return i;
            return -1;
        }

        /// <summary>A zone is open once any door into it is bought. The start zone is always open.</summary>
        public static bool IsZoneOpen(int zone, int openDoorMask)
        {
            if (zone == StartZone) return true;
            for (int i = 0; i < DoorLinks.Length; i++)
                if ((openDoorMask & (1 << i)) != 0 && (DoorLinks[i].x == zone || DoorLinks[i].y == zone)) return true;
            return false;
        }

        public void CollectSpawnPoints(int openDoorMask, List<Transform> into)
        {
            into.Clear();
            foreach (var zone in Zones)
                if (IsZoneOpen(zone.Id, openDoorMask)) into.AddRange(zone.SpawnPoints);
        }

        /// <summary>The zone on the other side of a door from a position.</summary>
        public int OtherSide(int door, Vector3 from)
        {
            var link = DoorLinks[door];
            int here = ZoneAt(from);
            return here == link.x ? link.y : here == link.y ? link.x : link.y;
        }

        // ---------------------------------------------------------------- building

        public static ZoneMap Build(PriceTable prices)
        {
            var map = new ZoneMap();
            map.BuildInternal(prices);
            return map;
        }

        void BuildInternal(PriceTable prices)
        {
            Root = new GameObject("Map").transform;
            Zones = new Zone[ZoneCount];
            for (int i = 0; i < ZoneCount; i++) Zones[i] = new Zone { Id = i, Name = ZoneNames[i], Center = ZoneCenters[i] };

            // Floors, one per zone so each reads slightly differently.
            for (int i = 0; i < ZoneCount; i++)
                Greybox.Box("Floor " + ZoneNames[i], Root, ZoneCenters[i] + new Vector3(0f, -0.5f, 0f), new Vector3(ZoneSize, 1f, ZoneSize), FloorTints[i]);

            BuildWalls();
            BuildWindows();
            BuildDressing();
            BuildLamps();
            BuildGenerators();
            BuildShop();
            BuildStations();

            var surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // Doors and wall-buy panels go in after the bake: closed doors carve the NavMesh instead.
            var buyables = new List<Buyable>();
            BuildDoors(buyables);
            BuildWallBuys(prices, buyables);
            BuildUpgradeMachine(buyables);
            buyables.Add(new Buyable { Kind = BuyableKind.Shop, Index = 0, Position = ShopPosition + new Vector3(-1.5f, 1f, 0f), Range = ShopRange });
            for (int i = 0; i < PerkSpots.Length; i++)
                buyables.Add(new Buyable { Kind = BuyableKind.Perk, Index = i, Position = StandPoint(PerkSpots[i]), Range = StationRange });
            buyables.Add(new Buyable { Kind = BuyableKind.Armor, Index = 0, Position = StandPoint(ArmorSpot), Range = StationRange });
            buyables.Add(new Buyable { Kind = BuyableKind.Reroll, Index = 0, Position = StandPoint(RerollSpot), Range = StationRange });
            Buyables = buyables.ToArray();

            PlayerStart = new Vector3(0f, 0.05f, -4f);
        }

        void BuildWalls()
        {
            float xMin = -36f, xMax = 36f, zMin = -12f, zMax = 36f;
            float midX = (xMin + xMax) / 2f, midZ = (zMin + zMax) / 2f;
            float spanX = xMax - xMin, spanZ = zMax - zMin;

            // Perimeter: inner faces sit exactly on the map edge.
            Greybox.Box("Wall S", Root, new Vector3(midX, WallHeight / 2f, zMin - 0.5f), new Vector3(spanX + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall N", Root, new Vector3(midX, WallHeight / 2f, zMax + 0.5f), new Vector3(spanX + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall W", Root, new Vector3(xMin - 0.5f, WallHeight / 2f, midZ), new Vector3(1f, WallHeight, spanZ), Wall);
            Greybox.Box("Wall E", Root, new Vector3(xMax + 0.5f, WallHeight / 2f, midZ), new Vector3(1f, WallHeight, spanZ), Wall);

            // Interior walls with door gaps.
            WallAlongZ(12f, zMin, zMax, 0f, 24f);
            WallAlongZ(-12f, zMin, zMax, 0f, 24f);
            WallAlongX(12f, xMin, xMax, -24f, 24f);
        }

        /// <summary>A wall at a fixed x running along z, with door gaps centred on the given z values.</summary>
        void WallAlongZ(float x, float from, float to, params float[] gaps)
        {
            float start = from;
            foreach (float g in gaps)
            {
                float end = g - GapWidth / 2f;
                Greybox.Box("Wall", Root, new Vector3(x, WallHeight / 2f, (start + end) / 2f), new Vector3(InnerWall, WallHeight, end - start), Wall);
                Greybox.Box("Lintel", Root, new Vector3(x, (GapHeight + WallHeight) / 2f, g), new Vector3(InnerWall, WallHeight - GapHeight, GapWidth), Wall);
                start = g + GapWidth / 2f;
            }
            Greybox.Box("Wall", Root, new Vector3(x, WallHeight / 2f, (start + to) / 2f), new Vector3(InnerWall, WallHeight, to - start), Wall);
        }

        /// <summary>A wall at a fixed z running along x, with door gaps centred on the given x values.</summary>
        void WallAlongX(float z, float from, float to, params float[] gaps)
        {
            float start = from;
            foreach (float g in gaps)
            {
                float end = g - GapWidth / 2f;
                Greybox.Box("Wall", Root, new Vector3((start + end) / 2f, WallHeight / 2f, z), new Vector3(end - start, WallHeight, InnerWall), Wall);
                Greybox.Box("Lintel", Root, new Vector3(g, (GapHeight + WallHeight) / 2f, z), new Vector3(GapWidth, WallHeight - GapHeight, InnerWall), Wall);
                start = g + GapWidth / 2f;
            }
            Greybox.Box("Wall", Root, new Vector3((start + to) / 2f, WallHeight / 2f, z), new Vector3(to - start, WallHeight, InnerWall), Wall);
        }

        void BuildWindows()
        {
            float north = 12f + InnerWall / 2f, south = 12f - InnerWall / 2f;
            // Zone, a point on the wall face, and the normal pointing into the zone.
            AddWindow(0, new Vector3(-7f, 0f, -12f), Vector3.forward);
            AddWindow(0, new Vector3(7f, 0f, -12f), Vector3.forward);
            AddWindow(0, new Vector3(6f, 0f, south), Vector3.back);
            AddWindow(1, new Vector3(24f, 0f, -12f), Vector3.forward);
            AddWindow(1, new Vector3(36f, 0f, 6f), Vector3.left);
            AddWindow(2, new Vector3(16f, 0f, 36f), Vector3.back);
            AddWindow(2, new Vector3(36f, 0f, 18f), Vector3.left);
            AddWindow(3, new Vector3(-7f, 0f, 36f), Vector3.back);
            AddWindow(3, new Vector3(-4f, 0f, north), Vector3.forward);
            AddWindow(4, new Vector3(-24f, 0f, 36f), Vector3.back);
            AddWindow(4, new Vector3(-36f, 0f, 18f), Vector3.right);
            AddWindow(5, new Vector3(-24f, 0f, -12f), Vector3.forward);
            AddWindow(5, new Vector3(-36f, 0f, 6f), Vector3.right);
        }

        void AddWindow(int zone, Vector3 wallFace, Vector3 inward)
        {
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            Vector3 boards = wallFace + inward * 0.06f;
            for (int i = 0; i < 3; i++)
            {
                var board = Greybox.Box("Board", Root, boards + Vector3.up * (1.1f + i * 0.45f), new Vector3(1.8f, 0.22f, 0.08f), Board, collider: false);
                board.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (i - 1) * 9f);
            }
            var point = Greybox.Empty("ZombieSpawn " + ZoneNames[zone], Root, wallFace + inward * 1.2f);
            point.localRotation = rot;
            Zones[zone].SpawnPoints.Add(point);
        }

        void BuildDressing()
        {
            // Gate Yard: a low wall and crates to fight around.
            Greybox.Box("Low wall", Root, new Vector3(0f, 0.55f, 4f), new Vector3(6f, 1.1f, 0.5f), Concrete);
            Crates(new Vector3(-7f, 0f, 5f));
            Crates(new Vector3(7.5f, 0f, -6f));

            // Depot: two long shelving runs with aisles round them.
            Greybox.Box("Shelf", Root, new Vector3(24f, 1.2f, -5f), new Vector3(8f, 2.4f, 1f), Cover);
            Greybox.Box("Shelf", Root, new Vector3(24f, 1.2f, 5f), new Vector3(8f, 2.4f, 1f), Cover);
            Crates(new Vector3(31f, 0f, 0f));

            // Garage: a raised platform with a ramp, and a car-sized block.
            Greybox.Box("Platform", Root, new Vector3(27f, 1f, 30f), new Vector3(8f, 2f, 8f), Concrete);
            var ramp = Greybox.Box("Ramp", Root, new Vector3(27f, 0.95f, 22.44f), new Vector3(3f, 0.3f, 7.4f), Cover);
            ramp.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f); // High end meets the platform's south edge.
            Greybox.Box("Wreck", Root, new Vector3(18f, 0.75f, 20f), new Vector3(2f, 1.5f, 4.2f), new Color(0.3f, 0.32f, 0.36f));

            // Chapel: pews either side of an aisle, and an altar.
            for (int row = 0; row < 4; row++)
            {
                float z = 18f + row * 3f;
                Greybox.Box("Pew", Root, new Vector3(-4f, 0.45f, z), new Vector3(6f, 0.9f, 0.6f), Cover);
                Greybox.Box("Pew", Root, new Vector3(4f, 0.45f, z), new Vector3(6f, 0.9f, 0.6f), Cover);
            }

            // Barracks: bunks down both sides.
            for (int i = 0; i < 4; i++)
            {
                float z = 16f + i * 4f;
                Greybox.Box("Bunk", Root, new Vector3(-30f, 0.8f, z), new Vector3(2f, 1.6f, 1f), Cover);
                Greybox.Box("Bunk", Root, new Vector3(-18f, 0.8f, z), new Vector3(2f, 1.6f, 1f), Cover);
            }

            // Courtyard: a fountain and four pillars.
            var fountain = Greybox.Prim(PrimitiveType.Cylinder, "Fountain", Root, new Vector3(-24f, 0.4f, 0f), new Vector3(5f, 0.4f, 5f), Concrete);
            Object.DestroyImmediate(fountain.GetComponent<Collider>());
            var fountainCollider = fountain.AddComponent<MeshCollider>();
            fountainCollider.convex = true;
            foreach (var p in new[] { new Vector3(-30f, 0f, -6f), new Vector3(-18f, 0f, -6f), new Vector3(-30f, 0f, 6f), new Vector3(-18f, 0f, 6f) })
                Greybox.Box("Pillar", Root, p + Vector3.up * (WallHeight / 2f), new Vector3(1.4f, WallHeight, 1.4f), Concrete);
        }

        void Crates(Vector3 at)
        {
            Greybox.Box("Crate", Root, at + new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", Root, at + new Vector3(1.3f, 0.6f, 0.4f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", Root, at + new Vector3(0.6f, 1.8f, 0.2f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
        }

        void BuildLamps()
        {
            for (int i = 0; i < ZoneCount; i++) AddLamp(ZoneCenters[i] + new Vector3(0f, 4.4f, 0f));
            AddLamp(new Vector3(0f, 4.4f, -9f));
        }

        void AddLamp(Vector3 position)
        {
            Greybox.Prim(PrimitiveType.Cube, "Lamp", Root, position, new Vector3(0.5f, 0.1f, 0.5f), Lamp, collider: false, unlit: true);
            var go = new GameObject("Lamp Light");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = position + Vector3.down * 0.3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 17f;
            light.intensity = 1.6f;
            light.color = Lamp;
            light.shadows = LightShadows.None;
        }

        void BuildDoors(List<Buyable> buyables)
        {
            Doors = new Door[DoorLinks.Length];
            for (int i = 0; i < DoorLinks.Length; i++)
            {
                Vector3 c = DoorCenters[i];
                // Doors on the x = +/-12 walls face along x; doors on the z = 12 wall face along z.
                bool facesX = Mathf.Abs(Mathf.Abs(c.x) - 12f) < 0.01f;
                var rot = facesX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                var go = Greybox.Box("Door " + ZoneNames[DoorLinks[i].x] + " - " + ZoneNames[DoorLinks[i].y], Root,
                    c + Vector3.up * (GapHeight / 2f), new Vector3(GapWidth, GapHeight, 0.35f), DoorWood);
                go.transform.localRotation = rot;
                var door = go.AddComponent<Door>();
                door.Init(i);
                Doors[i] = door;
                buyables.Add(new Buyable { Kind = BuyableKind.Door, Index = i, Position = c + Vector3.up, Range = DoorRange });
            }
        }

        void BuildWallBuys(PriceTable prices, List<Buyable> buyables)
        {
            float south = 12f - InnerWall / 2f;
            // Wall face and inward normal for each wall gun, in WallBuyZones order.
            var spots = new[]
            {
                (new Vector3(-6f, 0f, south), Vector3.back),
                (new Vector3(36f, 0f, -4f), Vector3.left),
                (new Vector3(-36f, 0f, -4f), Vector3.right),
                (new Vector3(36f, 0f, 24f), Vector3.left),
                (new Vector3(-36f, 0f, 28f), Vector3.right),
                (new Vector3(6f, 0f, 36f), Vector3.back),
                (new Vector3(30f, 0f, south), Vector3.back),
                (new Vector3(-12.3f, 0f, 31f), Vector3.left),
                (new Vector3(12.3f, 0f, 17f), Vector3.right),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var (face, inward) = spots[i];
                var entry = prices.WallGunAt(i);
                BuildWallBuyPanel(face, inward, entry != null ? entry.gun : null);
                buyables.Add(new Buyable { Kind = BuyableKind.WallGun, Index = i, Position = face + inward * 1f + Vector3.up, Range = WallBuyRange });
            }
        }

        void BuildGenerators()
        {
            Generators = new Generator[ZoneCount];
            for (int i = 0; i < ZoneCount; i++)
            {
                var (position, yaw) = GeneratorSpots[i];
                var go = Greybox.Empty("Generator " + ZoneNames[i], Root, position).gameObject;
                // The gauge side faces into the zone.
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Generators[i] = go.AddComponent<Generator>();
                Generators[i].Init();
            }
        }

        /// <summary>Where a player stands to use a station against a wall.</summary>
        static Vector3 StandPoint((Vector3 face, Vector3 inward) spot) => spot.face + spot.inward * 1.4f + Vector3.up;

        /// <summary>Perk machines, the armour station and the reroll shrine. Built before the bake so zombies walk round them.</summary>
        void BuildStations()
        {
            var perks = GameConfig.Perks;
            for (int i = 0; i < PerkSpots.Length; i++)
            {
                var def = perks != null ? perks.At(i) : null;
                BuildVendingMachine(PerkSpots[i], def != null ? def.name : "Perk", def != null ? def.color : Color.white,
                    def != null ? def.price.ToString("N0") : "");
            }
            BuildVendingMachine(ArmorSpot, "Armour Plates", new Color(0.6f, 0.65f, 0.72f), "");
            BuildShrine(RerollSpot);
        }

        void BuildVendingMachine((Vector3 face, Vector3 inward) spot, string title, Color color, string price)
        {
            var root = Greybox.Empty("Machine " + title, Root, spot.face + spot.inward * 0.45f);
            root.localRotation = Quaternion.LookRotation(spot.inward, Vector3.up);
            Greybox.Box("Body", root, new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 2.1f, 0.8f), new Color(0.12f, 0.12f, 0.14f));
            Greybox.Box("Top", root, new Vector3(0f, 2.16f, 0f), new Vector3(1.2f, 0.12f, 0.9f), color * 0.6f, collider: false);
            // Front: a glowing panel in the perk's colour, with bottles behind glass.
            Greybox.Prim(PrimitiveType.Cube, "Panel", root, new Vector3(0f, 1.45f, 0.41f), new Vector3(0.9f, 0.9f, 0.02f), color * 0.85f, collider: false, unlit: true);
            for (int b = 0; b < 3; b++)
                Greybox.Prim(PrimitiveType.Cylinder, "Bottle", root, new Vector3(-0.25f + b * 0.25f, 0.62f, 0.32f), new Vector3(0.1f, 0.14f, 0.1f), color, collider: false, unlit: true);
            Greybox.Box("Slot", root, new Vector3(0f, 0.35f, 0.41f), new Vector3(0.6f, 0.12f, 0.02f), new Color(0.05f, 0.05f, 0.05f), collider: false);
            var labelColor = Color.Lerp(color, Color.white, 0.6f);
            Greybox.Label("Name", root, new Vector3(0f, 2.45f, 0.2f), title.ToUpperInvariant(), labelColor, 0.2f);
            if (!string.IsNullOrEmpty(price)) Greybox.Label("Price", root, new Vector3(0f, 1.45f, 0.43f), price, new Color(0.05f, 0.05f, 0.05f), 0.16f);
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 1.5f, 1.1f);
            light.type = LightType.Point;
            light.color = color;
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        void BuildShrine((Vector3 face, Vector3 inward) spot)
        {
            var glow = new Color(1f, 0.75f, 0.3f);
            var root = Greybox.Empty("Reroll Shrine", Root, spot.face + spot.inward * 0.45f);
            root.localRotation = Quaternion.LookRotation(spot.inward, Vector3.up);
            Greybox.Box("Plinth", root, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.8f), new Color(0.35f, 0.33f, 0.3f));
            var card = Greybox.Prim(PrimitiveType.Cube, "Cards", root, new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.06f, 0.35f), glow, collider: false, unlit: true);
            card.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Greybox.Label("Name", root, new Vector3(0f, 1.8f, 0f), "FORTUNE SHRINE", Color.Lerp(glow, Color.white, 0.5f), 0.18f);
            Greybox.Label("What", root, new Vector3(0f, 1.6f, 0f), "power-up rerolls", new Color(1f, 1f, 1f, 0.7f), 0.11f);
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 1.4f, 0.8f);
            light.type = LightType.Point;
            light.color = glow;
            light.range = 4f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
        }

        /// <summary>A market stall: counter, posts and an awning. Built before the bake so zombies walk round it.</summary>
        void BuildShop()
        {
            var shop = Greybox.Empty("Spawn Shop", Root, ShopPosition);
            var awning = new Color(0.2f, 0.45f, 0.55f);
            Greybox.Box("Counter", shop, new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 3f), new Color(0.38f, 0.28f, 0.18f));
            Greybox.Box("CounterTop", shop, new Vector3(-0.05f, 1.13f, 0f), new Vector3(1.2f, 0.06f, 3.2f), new Color(0.5f, 0.4f, 0.28f), collider: false);
            foreach (float z in new[] { -1.5f, 1.5f })
                Greybox.Box("Post", shop, new Vector3(0.4f, 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f), new Color(0.3f, 0.22f, 0.15f));
            var roof = Greybox.Box("Awning", shop, new Vector3(-0.2f, 2.85f, 0f), new Vector3(1.8f, 0.08f, 3.4f), awning, collider: false);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            Greybox.Prim(PrimitiveType.Cube, "Sign", shop, new Vector3(-0.56f, 2.35f, 0f), new Vector3(0.02f, 0.35f, 1.6f), new Color(0.95f, 0.8f, 0.35f), collider: false, unlit: true);
            // A few greybox "attachments" on the counter.
            Greybox.Box("Scope", shop, new Vector3(-0.1f, 1.22f, -0.8f), new Vector3(0.12f, 0.12f, 0.4f), new Color(0.15f, 0.15f, 0.15f), collider: false);
            Greybox.Box("Mag", shop, new Vector3(-0.1f, 1.25f, 0f), new Vector3(0.1f, 0.2f, 0.12f), new Color(0.2f, 0.2f, 0.22f), collider: false);
            Greybox.Box("Grip", shop, new Vector3(-0.1f, 1.22f, 0.7f), new Vector3(0.1f, 0.14f, 0.1f), new Color(0.25f, 0.2f, 0.15f), collider: false);

            // The Trader: a hooded scavenger behind the counter, with a lantern.
            var trader = Greybox.Empty("Trader", shop, new Vector3(0.95f, 0f, 0.4f));
            trader.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var coat = new Color(0.28f, 0.24f, 0.2f);
            Greybox.Prim(PrimitiveType.Capsule, "Body", trader, new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 0.9f, 0.45f), coat, collider: false);
            Greybox.Prim(PrimitiveType.Sphere, "Head", trader, new Vector3(0f, 1.72f, 0.02f), Vector3.one * 0.3f, new Color(0.7f, 0.56f, 0.45f), collider: false);
            Greybox.Prim(PrimitiveType.Sphere, "Hood", trader, new Vector3(0f, 1.78f, -0.04f), new Vector3(0.38f, 0.36f, 0.4f), new Color(0.2f, 0.17f, 0.14f), collider: false);
            Greybox.Prim(PrimitiveType.Cylinder, "Lantern", trader, new Vector3(0.32f, 1.25f, 0.1f), new Vector3(0.1f, 0.1f, 0.1f), new Color(1f, 0.75f, 0.35f), collider: false, unlit: true);
            var lamp = new GameObject("Trader Lamp").AddComponent<Light>();
            lamp.transform.SetParent(trader, false);
            lamp.transform.localPosition = new Vector3(0.32f, 1.4f, 0.1f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.7f, 0.35f);
            lamp.range = 4.5f;
            lamp.intensity = 1.3f;
            lamp.shadows = LightShadows.None;
            // Read from the customer side of the counter (-x).
            Greybox.Label("Trader Sign", shop, new Vector3(-0.58f, 2.35f, 0f), "TRADER", new Color(0.15f, 0.1f, 0.05f), 0.3f).transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        void BuildUpgradeMachine(List<Buyable> buyables)
        {
            var glow = new Color(0.65f, 0.35f, 1f);
            var machine = Greybox.Empty("Upgrade Machine", Root, MachinePosition);
            var body = Greybox.Box("Body", machine, new Vector3(0f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 1.4f), new Color(0.14f, 0.13f, 0.17f));
            // Built after the bake like the doors, so it carves its own hole in the NavMesh.
            var obstacle = body.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            Greybox.Box("Top", machine, new Vector3(0f, 2.35f, 0f), new Vector3(2.6f, 0.3f, 1.6f), new Color(0.22f, 0.2f, 0.26f));
            // Glowing slot and strips facing the room.
            Greybox.Prim(PrimitiveType.Cube, "Slot", machine, new Vector3(0f, 1.25f, -0.71f), new Vector3(1.2f, 0.25f, 0.02f), glow, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Cube, "StripL", machine, new Vector3(-1.05f, 1.1f, -0.71f), new Vector3(0.08f, 1.8f, 0.02f), glow, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Cube, "StripR", machine, new Vector3(1.05f, 1.1f, -0.71f), new Vector3(0.08f, 1.8f, 0.02f), glow, collider: false, unlit: true);
            var lightGo = new GameObject("Machine Light");
            lightGo.transform.SetParent(machine, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, -1.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = glow;
            light.range = 7f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            buyables.Add(new Buyable { Kind = BuyableKind.UpgradeMachine, Index = 0, Position = MachinePosition + new Vector3(0f, 1f, -1.9f), Range = MachineRange });
        }

        void BuildWallBuyPanel(Vector3 face, Vector3 inward, GunData gun)
        {
            var panel = Greybox.Empty("WallBuy " + (gun != null ? gun.displayName : "?"), Root, face + inward * 0.03f + Vector3.up * 1.5f);
            panel.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
            // A chalk outline with the gun's silhouette inside, like an old wall chalk drawing.
            Greybox.Box("Chalk T", panel, new Vector3(0f, 0.42f, 0f), new Vector3(1.6f, 0.05f, 0.02f), Chalk, collider: false);
            Greybox.Box("Chalk B", panel, new Vector3(0f, -0.42f, 0f), new Vector3(1.6f, 0.05f, 0.02f), Chalk, collider: false);
            Greybox.Box("Chalk L", panel, new Vector3(-0.78f, 0f, 0f), new Vector3(0.05f, 0.88f, 0.02f), Chalk, collider: false);
            Greybox.Box("Chalk R", panel, new Vector3(0.78f, 0f, 0f), new Vector3(0.05f, 0.88f, 0.02f), Chalk, collider: false);
            float length = gun != null ? Mathf.Clamp(gun.modelLength, 0.4f, 1.5f) : 1f;
            var accent = gun != null ? gun.accent : Cover;
            Greybox.Box("Gun", panel, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f * length, 0.12f, 0.04f), new Color(0.16f, 0.17f, 0.18f), collider: false);
            Greybox.Box("Grip", panel, new Vector3(-0.15f * length, -0.1f, 0f), new Vector3(0.08f, 0.2f, 0.04f), accent, collider: false);
            Greybox.Box("Stock", panel, new Vector3(-0.5f * length, 0.02f, 0f), new Vector3(0.25f * length, 0.14f, 0.04f), accent, collider: false);
        }
    }
}
