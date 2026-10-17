using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
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
        /// <summary>A buildable part lying somewhere on the map; Index is the part spot. Free to pick up.</summary>
        Part,
        /// <summary>A place at a build station; Index is bench * BuildableCount + buildable.</summary>
        Workbench,
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
    /// The greybox map: twelve 34 m zones in a 4 x 3 grid, every neighbouring pair joined by a buyable door,
    /// so there are many ways round to retreat and kite. Players start in the Gate Yard; each door opens a new
    /// zone and its zombie windows. Zones have stairs up to platforms, parkour runs and ladders, and six high
    /// spots that can only be reached by climbing; those hold build parts, the build stations and the
    /// high-ground missions. Built in code on every machine; the NavMesh is baked at runtime with the doors
    /// left out so they can carve it while closed, and zombies get NavMesh links up every ladder and jump.
    ///
    ///   Greenhouse (11) | Archives (10)  | Lantern Core (9) | Crane Yard (8)
    ///   ----------------+----------------+------------------+----------------
    ///   Barracks (4)    | Chapel (3)     | Garage (2)       | Pump Station (7)
    ///   ----------------+----------------+------------------+----------------
    ///   Courtyard (5)   | Gate Yard (0)  | Depot (1)        | Loading Dock (6)
    /// </summary>
    public class ZoneMap
    {
        public const int ZoneCount = 12;
        public const int StartZone = 0;
        public const float ZoneHalfSize = 17f;
        const float ZoneSize = ZoneHalfSize * 2f;
        const float WallHeight = 7f;
        const float InnerWall = 0.6f;
        const float GapWidth = 3.2f;
        const float GapHeight = 3.2f;
        const float DoorRange = 2.6f;
        const float WallBuyRange = 2f;
        const float MachineRange = 2.2f;
        const float ShopRange = 2.2f;
        const float StationRange = 2f;
        const float PartRange = 1.6f;
        const float BenchRange = 1.1f;
        const float StepRise = 0.25f;
        const float StepRun = 0.35f;
        const float HopSize = 2.4f;

        // Grid position of each zone (zone order). Column 1, row 0 is the Gate Yard at the origin.
        static readonly int[] GridCol = { 1, 2, 2, 1, 0, 0, 3, 3, 3, 2, 1, 0 };
        static readonly int[] GridRow = { 0, 0, 1, 1, 1, 0, 0, 1, 2, 2, 2, 2 };
        const int GridCols = 4, GridRows = 3;

        /// <summary>The map's outer edge, for the map screen.</summary>
        public static readonly Rect Bounds = new Rect(-ZoneSize * 1.5f, -ZoneHalfSize, ZoneSize * GridCols, ZoneSize * GridRows);

        public static readonly string[] ZoneNames =
        {
            "Gate Yard", "Depot", "Garage", "Chapel", "Barracks", "Courtyard",
            "Loading Dock", "Pump Station", "Crane Yard", "Lantern Core", "Archives", "Greenhouse",
        };

        static Vector3 GridCenter(int zone) => new Vector3((GridCol[zone] - 1) * ZoneSize, 0f, GridRow[zone] * ZoneSize);

        static readonly Vector3[] ZoneCenters = BuildCenters();

        static Vector3[] BuildCenters()
        {
            var c = new Vector3[ZoneCount];
            for (int i = 0; i < ZoneCount; i++) c[i] = GridCenter(i);
            return c;
        }

        /// <summary>A point in a zone, from the zone's centre.</summary>
        static Vector3 L(int zone, float x, float y, float z) => GridCenter(zone) + new Vector3(x, y, z);

        /// <summary>The zones each door joins, in door order (cheapest near the start). PriceTable.doorCosts follows this order.</summary>
        public static readonly Vector2Int[] DoorLinks =
        {
            new Vector2Int(0, 1), new Vector2Int(0, 5), new Vector2Int(0, 3), new Vector2Int(1, 2),
            new Vector2Int(5, 4), new Vector2Int(1, 6), new Vector2Int(3, 2), new Vector2Int(4, 3),
            new Vector2Int(6, 7), new Vector2Int(2, 7), new Vector2Int(4, 11), new Vector2Int(3, 10),
            new Vector2Int(11, 10), new Vector2Int(7, 8), new Vector2Int(10, 9), new Vector2Int(9, 8),
            new Vector2Int(2, 9),
        };

        /// <summary>Doors sit in the middle of the wall two zones share.</summary>
        public static Vector3 DoorCenter(int door) => (GridCenter(DoorLinks[door].x) + GridCenter(DoorLinks[door].y)) * 0.5f;

        enum Side { North, South, East, West }

        /// <summary>A point on a zone's wall face and the direction into the zone. Along runs west to east or south to north.</summary>
        static (Vector3 face, Vector3 inward) Spot(int zone, Side side, float along)
        {
            bool perimeter = side == Side.North ? GridRow[zone] == GridRows - 1
                : side == Side.South ? GridRow[zone] == 0
                : side == Side.East ? GridCol[zone] == GridCols - 1
                : GridCol[zone] == 0;
            float d = perimeter ? ZoneHalfSize : ZoneHalfSize - InnerWall / 2f;
            switch (side)
            {
                case Side.North: return (L(zone, along, 0f, d), Vector3.back);
                case Side.South: return (L(zone, along, 0f, -d), Vector3.forward);
                case Side.East: return (L(zone, d, 0f, along), Vector3.left);
                default: return (L(zone, -d, 0f, along), Vector3.right);
            }
        }

        static float YawFor(Vector3 inward) => Quaternion.LookRotation(-inward, Vector3.up).eulerAngles.y;

        /// <summary>Which zone each wall gun is in, in wall-buy order. PriceTable.wallGuns follows this order.</summary>
        public static readonly int[] WallBuyZones = { 0, 1, 5, 2, 4, 9, 3, 7, 8 };
        static readonly (int zone, Side side, float along)[] WallBuySpots =
        {
            (0, Side.South, -5f), (1, Side.South, -8f), (5, Side.West, 8f), (2, Side.West, -10f), (4, Side.West, -12f),
            (9, Side.East, 10f), (3, Side.East, -10f), (7, Side.East, 3f), (8, Side.North, -8f),
        };

        /// <summary>Perk machines in PerkKind order.</summary>
        static readonly (int zone, Side side, float along)[] PerkSpots =
        {
            (3, Side.East, 10f),    // Ironhide Tonic, Chapel
            (1, Side.South, 8f),    // Slick Fingers, Depot
            (0, Side.West, -10f),   // Second Wind, Gate Yard
            (6, Side.South, -4f),   // Hair Trigger, Loading Dock
            (5, Side.North, -10f),  // Fleet Foot, Courtyard
            (10, Side.West, -10f),  // Steady Hand, Archives
            (11, Side.North, 8f),   // Hazard Ward, Greenhouse
            (9, Side.South, -9f),   // Bloodline, Lantern Core
        };
        /// <summary>Armour plates in the Barracks; power-up rerolls in the Pump Station.</summary>
        static readonly (int zone, Side side, float along) ArmorSpot = (4, Side.North, -9f);
        static readonly (int zone, Side side, float along) RerollSpot = (7, Side.East, -10f);

        static readonly (int zone, Side side, float along)[] WindowSpots =
        {
            (0, Side.South, -12f), (0, Side.South, 12f), (0, Side.North, 11f),
            (1, Side.South, 0f), (1, Side.South, 14f), (1, Side.East, -10f),
            (2, Side.West, 10f), (2, Side.South, -11f), (2, Side.East, -10f),
            (3, Side.West, -10f), (3, Side.South, 11f), (3, Side.North, 10f),
            (4, Side.West, 12f), (4, Side.North, 10f), (4, Side.South, 11f),
            (5, Side.West, -6f), (5, Side.South, 8f), (5, Side.North, 10f),
            (6, Side.South, 6f), (6, Side.East, 14f), (6, Side.North, -10f),
            (7, Side.South, 10f), (7, Side.West, -10f), (7, Side.East, 15f),
            (8, Side.North, 14f), (8, Side.East, -6f), (8, Side.East, 13f),
            (9, Side.South, 10f), (9, Side.West, -10f), (9, Side.East, -10f),
            (10, Side.North, -10f), (10, Side.West, 10f), (10, Side.South, 10f),
            (11, Side.West, -8f), (11, Side.North, -12f), (11, Side.East, 10f),
        };

        /// <summary>One mission generator per zone (zone order), against a wall and clear of windows and doors.</summary>
        static readonly (Side side, float along)[] GeneratorWalls =
        {
            (Side.South, 4f), (Side.East, 9f), (Side.South, 10f), (Side.South, -10f), (Side.East, 10f), (Side.East, -10f),
            (Side.West, -10f), (Side.North, 10f), (Side.South, 10f), (Side.West, 6f), (Side.East, 10f), (Side.South, 10f),
        };

        static Vector3 GeneratorPosition(int zone)
        {
            var (face, inward) = Spot(zone, GeneratorWalls[zone].side, GeneratorWalls[zone].along);
            return face + inward * 1.4f;
        }

        // ---------------------------------------------------------------- high spots, parts and build stations

        public const int HighSpotCount = 6;
        public static readonly string[] HighSpotNames = { "Depot Loft", "Garage Roof", "Water Tower", "Container Stack", "Records Loft", "Greenhouse Roof" };
        /// <summary>The zone each high spot is in.</summary>
        public static readonly int[] HighSpotZones = { 1, 2, 7, 8, 10, 11 };
        /// <summary>Where each high spot's signal beacon stands, on top.</summary>
        static readonly Vector3[] HighSpotPoints =
        {
            L(1, 9.5f, 4f, 14.2f), L(2, 15f, 4.5f, 11f), L(7, 11.5f, 6f, 10.2f),
            L(8, 11.2f, 3.6f, 7.5f), L(10, 10f, 4f, 15f), L(11, -9.5f, 4.5f, 6f),
        };

        public static Vector3 HighSpot(int spot) => HighSpotPoints[Mathf.Clamp(spot, 0, HighSpotCount - 1)];

        public const int BuildableCount = 3;
        public const int PartsPerBuildable = 3;
        public const int PartCount = BuildableCount * PartsPerBuildable;
        public const int SpotsPerPart = 3;
        public const int BenchCount = 2;

        /// <summary>
        /// Where each part can turn up: three candidate spots per part (part order), each in a different zone,
        /// many of them up on the high spots. The host picks one per part for every game.
        /// </summary>
        static readonly Vector3[] PartSpots =
        {
            // Scrap Cannon: barrel, coil, grip.
            L(1, 6f, 4f, 14f), L(5, -14.5f, 3.2f, -14.5f), L(10, -14f, 0f, 13f),
            L(2, -13f, 0f, -13f), L(8, 7f, 3.6f, 7.5f), L(4, -14.5f, 2.6f, -3f),
            L(3, -13.5f, 2.6f, 14f), L(6, 14f, 1.4f, -12f), L(11, 12f, 0f, 13f),
            // Scrap Shield: frame, plate, handle.
            L(7, 10f, 6f, 11.5f), L(0, -11f, 2.2f, 12f), L(9, 12f, 0f, -12f),
            L(9, -8f, 3f, 15.2f), L(4, 13f, 0f, -13f), L(6, 3f, 3f, -9f),
            L(10, 5f, 4f, 15f), L(1, -13f, 0f, -13f), L(7, -11f, 2.4f, 10f),
            // Buddy Bot: chassis, rotor, brain.
            L(8, -13f, 0f, 13f), L(3, 13f, 0f, 13f), L(5, -13f, 0f, 13f),
            L(11, -6.5f, 4.5f, 5f), L(2, -13f, 0f, 13f), L(0, -13f, 0f, -5f),
            L(6, -14f, 0f, -14f), L(9, 13f, 0f, 13f), L(10, -14f, 0f, -14f),
        };

        public static int PartSpotCount => PartSpots.Length;
        public static Vector3 PartSpot(int spot) => PartSpots[Mathf.Clamp(spot, 0, PartSpots.Length - 1)];
        public static int ZoneOfPoint(Vector3 p)
        {
            for (int i = 0; i < ZoneCount; i++)
                if (Mathf.Abs(p.x - ZoneCenters[i].x) <= ZoneHalfSize && Mathf.Abs(p.z - ZoneCenters[i].z) <= ZoneHalfSize) return i;
            return -1;
        }

        /// <summary>The two build stations: on the Garage Roof and the Greenhouse Roof. Each is a long bench, facing -z, with a place per buildable.</summary>
        static readonly Vector3[] BenchPositions = { L(2, 13f, 4.5f, 14.8f), L(11, -10f, 4.5f, 10.6f) };
        public static readonly string[] BenchNames = { "Garage Roof", "Greenhouse Roof" };
        public static Vector3 BenchPosition(int bench) => BenchPositions[Mathf.Clamp(bench, 0, BenchCount - 1)];
        /// <summary>Where a player stands to build one buildable at a bench.</summary>
        public static Vector3 BenchPlace(int bench, int buildable) => BenchPosition(bench) + new Vector3((buildable - 1) * 1.8f, 1f, -1.2f);

        /// <summary>The upgrade machine stands against the north wall of the Lantern Core, the zone at the heart of the far side.</summary>
        static readonly Vector3 MachinePosition = L(9, 8f, 0f, 15.4f);
        /// <summary>The spawn shop's counter in the Gate Yard, served from the west side.</summary>
        static readonly Vector3 ShopPosition = L(0, 11f, 0f, -6f);

        public static Vector3 MachineWorldPosition => MachinePosition;
        public static Vector3 ShopWorldPosition => ShopPosition;

        public static Vector3 ZoneCenter(int zone) => ZoneCenters[Mathf.Clamp(zone, 0, ZoneCount - 1)];

        /// <summary>Where a protect mission's survivor crouches: a few metres out from the zone's generator, towards the middle.</summary>
        public static Vector3 SurvivorSpot(int zone)
        {
            zone = Mathf.Clamp(zone, 0, ZoneCount - 1);
            var gen = GeneratorPosition(zone);
            var toCenter = ZoneCenter(zone) - gen;
            toCenter.y = 0f;
            return gen + toCenter.normalized * 3.2f;
        }

        static readonly Color[] FloorTints =
        {
            new Color(0.33f, 0.34f, 0.33f), new Color(0.36f, 0.34f, 0.3f), new Color(0.3f, 0.31f, 0.33f),
            new Color(0.34f, 0.3f, 0.3f), new Color(0.31f, 0.33f, 0.3f), new Color(0.36f, 0.36f, 0.33f),
            new Color(0.32f, 0.33f, 0.36f), new Color(0.29f, 0.33f, 0.34f), new Color(0.35f, 0.33f, 0.28f),
            new Color(0.3f, 0.28f, 0.34f), new Color(0.34f, 0.32f, 0.29f), new Color(0.28f, 0.34f, 0.28f),
        };
        static readonly Color Wall = new Color(0.46f, 0.45f, 0.42f);
        static readonly Color Cover = new Color(0.4f, 0.36f, 0.3f);
        static readonly Color Concrete = new Color(0.55f, 0.55f, 0.53f);
        static readonly Color Metal = new Color(0.36f, 0.38f, 0.4f);
        static readonly Color Rust = new Color(0.45f, 0.28f, 0.18f);
        static readonly Color Board = new Color(0.3f, 0.2f, 0.12f);
        static readonly Color DoorWood = new Color(0.34f, 0.23f, 0.13f);
        static readonly Color Chalk = new Color(0.82f, 0.8f, 0.72f);
        static readonly Color Lamp = new Color(1f, 0.85f, 0.6f);
        static readonly Color LadderColor = new Color(0.62f, 0.5f, 0.2f);
        static readonly Color HopColor = new Color(0.5f, 0.42f, 0.34f);

        public Transform Root { get; private set; }
        public Zone[] Zones { get; private set; }
        public Door[] Doors { get; private set; }
        public Buyable[] Buyables { get; private set; }
        /// <summary>Mission generators, indexed by zone.</summary>
        public Generator[] Generators { get; private set; }
        /// <summary>Signal beacons for high-ground missions, indexed by high spot.</summary>
        public Generator[] Beacons { get; private set; }
        public Vector3 PlayerStart { get; private set; }

        readonly List<(Vector3 from, Vector3 to)> _links = new List<(Vector3, Vector3)>();

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
            Ladder.ClearAll();
            Zones = new Zone[ZoneCount];
            for (int i = 0; i < ZoneCount; i++) Zones[i] = new Zone { Id = i, Name = ZoneNames[i], Center = ZoneCenters[i] };

            // Floors, one per zone so each reads slightly differently.
            for (int i = 0; i < ZoneCount; i++)
                Greybox.Box("Floor " + ZoneNames[i], Root, ZoneCenters[i] + new Vector3(0f, -0.5f, 0f), new Vector3(ZoneSize, 1f, ZoneSize), FloorTints[i]);

            BuildWalls();
            foreach (var w in WindowSpots) AddWindow(w.zone, Spot(w.zone, w.side, w.along));
            BuildZones();
            BuildLamps();
            BuildGenerators();
            BuildBeacons();
            BuildShop();
            BuildStations();
            BuildBenches();

            var surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AddNavLinks();

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
            for (int i = 0; i < PartSpots.Length; i++)
                buyables.Add(new Buyable { Kind = BuyableKind.Part, Index = i, Position = PartSpots[i] + Vector3.up, Range = PartRange });
            for (int bench = 0; bench < BenchCount; bench++)
                for (int item = 0; item < BuildableCount; item++)
                    buyables.Add(new Buyable { Kind = BuyableKind.Workbench, Index = bench * BuildableCount + item, Position = BenchPlace(bench, item), Range = BenchRange });
            Buyables = buyables.ToArray();

            PlayerStart = L(StartZone, 0f, 0.05f, -6f);
        }

        void BuildWalls()
        {
            float xMin = Bounds.xMin, xMax = Bounds.xMax, zMin = Bounds.yMin, zMax = Bounds.yMax;
            float midX = (xMin + xMax) / 2f, midZ = (zMin + zMax) / 2f;
            float spanX = xMax - xMin, spanZ = zMax - zMin;

            // Perimeter: inner faces sit exactly on the map edge.
            Greybox.Box("Wall S", Root, new Vector3(midX, WallHeight / 2f, zMin - 0.5f), new Vector3(spanX + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall N", Root, new Vector3(midX, WallHeight / 2f, zMax + 0.5f), new Vector3(spanX + 2f, WallHeight, 1f), Wall);
            Greybox.Box("Wall W", Root, new Vector3(xMin - 0.5f, WallHeight / 2f, midZ), new Vector3(1f, WallHeight, spanZ), Wall);
            Greybox.Box("Wall E", Root, new Vector3(xMax + 0.5f, WallHeight / 2f, midZ), new Vector3(1f, WallHeight, spanZ), Wall);

            // Interior walls, with a door gap wherever two zones meet.
            var rowGaps = new float[GridRows];
            for (int r = 0; r < GridRows; r++) rowGaps[r] = r * ZoneSize;
            var colGaps = new float[GridCols];
            for (int c = 0; c < GridCols; c++) colGaps[c] = (c - 1) * ZoneSize;
            for (int c = 1; c < GridCols; c++) WallAlongZ(xMin + c * ZoneSize, zMin, zMax, rowGaps);
            for (int r = 1; r < GridRows; r++) WallAlongX(zMin + r * ZoneSize, xMin, xMax, colGaps);
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

        void AddWindow(int zone, (Vector3 face, Vector3 inward) spot)
        {
            var (wallFace, inward) = spot;
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

        // ---------------------------------------------------------------- level pieces (zone-local coordinates)

        /// <summary>A solid block from the floor up to a height, given by its corners.</summary>
        Vector3 Block(string name, int zone, float x0, float z0, float x1, float z1, float height, Color color)
        {
            var center = L(zone, (x0 + x1) / 2f, height / 2f, (z0 + z1) / 2f);
            Greybox.Box(name, Root, center, new Vector3(x1 - x0, height, z1 - z0), color);
            return center + Vector3.up * (height / 2f);
        }

        /// <summary>A deck on posts you can walk under (top at least 2.6 m up).</summary>
        void Deck(string name, int zone, float x0, float z0, float x1, float z1, float top, Color color)
        {
            const float thick = 0.35f;
            Greybox.Box(name, Root, L(zone, (x0 + x1) / 2f, top - thick / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, thick, z1 - z0), color);
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 5f));
            int nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / 5f));
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                {
                    if (i != 0 && i != nx && j != 0 && j != nz) continue;
                    float px = Mathf.Lerp(x0 + 0.25f, x1 - 0.25f, (float)i / nx);
                    float pz = Mathf.Lerp(z0 + 0.25f, z1 - 0.25f, (float)j / nz);
                    Greybox.Box("Post", Root, L(zone, px, (top - thick) / 2f, pz), new Vector3(0.3f, top - thick, 0.3f), Metal);
                }
        }

        /// <summary>Stairs down from a ledge: edge is the middle of the ledge's side, down points away from it.</summary>
        void Stairs(int zone, float x, float z, Vector3 down, float height, float width)
        {
            int n = Mathf.CeilToInt(height / StepRise - 0.01f);
            float rise = height / n;
            Vector3 edge = L(zone, x, 0f, z);
            bool alongX = Mathf.Abs(down.x) > 0.5f;
            for (int i = 0; i < n - 1; i++)
            {
                float top = height - (i + 1) * rise;
                var size = alongX ? new Vector3(StepRun, top, width) : new Vector3(width, top, StepRun);
                Greybox.Box("Step", Root, edge + down * (StepRun * (i + 0.5f)) + Vector3.up * (top / 2f), size, Concrete);
            }
        }

        /// <summary>
        /// A ladder up a face to a ledge: x, z is the foot on the face, outward points to where climbers stand.
        /// Registers it for players and lays a NavMesh link up it for zombies.
        /// </summary>
        void AddLadder(int zone, float x, float z, Vector3 outward, float height)
        {
            Vector3 foot = L(zone, x, 0f, z);
            var root = Greybox.Empty("Ladder", Root, foot);
            root.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            // A backing strip so the ladder always has something solid behind it.
            Greybox.Box("Backing", root, new Vector3(0f, height / 2f, -0.05f), new Vector3(1f, height, 0.1f), Cover);
            foreach (float side in new[] { -0.32f, 0.32f })
                Greybox.Box("Rail", root, new Vector3(side, height / 2f + 0.3f, 0.09f), new Vector3(0.07f, height + 0.6f, 0.07f), LadderColor, collider: false);
            for (float y = 0.3f; y < height; y += 0.35f)
                Greybox.Box("Rung", root, new Vector3(0f, y, 0.09f), new Vector3(0.64f, 0.05f, 0.05f), LadderColor, collider: false);
            new Ladder(foot, outward, height);
            _links.Add((foot + outward * 0.7f, foot - outward * 0.9f + Vector3.up * height));
        }

        /// <summary>A parkour block, 2.4 m square, centred on x, z.</summary>
        void Hop(int zone, float x, float z, float height) =>
            Block("Hop", zone, x - HopSize / 2f, z - HopSize / 2f, x + HopSize / 2f, z + HopSize / 2f, height, HopColor);

        /// <summary>A jump zombies can take (and players can make), between two zone-local points.</summary>
        void Jump(int zone, float ax, float ay, float az, float bx, float by, float bz) =>
            _links.Add((L(zone, ax, ay, az), L(zone, bx, by, bz)));

        void Crates(Vector3 at)
        {
            Greybox.Box("Crate", Root, at + new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
            Greybox.Box("Crate", Root, at + new Vector3(1.3f, 0.6f, 0.4f), new Vector3(1.2f, 1.2f, 1.2f), Cover);
        }

        void Sign(int zone, Vector3 local, Vector3 facing, string text, Color color, float height = 0.45f)
        {
            var t = Greybox.Empty("Sign", Root, L(zone, local.x, local.y, local.z));
            t.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            Greybox.Label("Text", t, Vector3.zero, text, color, height);
        }

        /// <summary>
        /// Every zone's layout. Coordinates are from the zone's centre, x east and z north, with the walls at
        /// +/-17. Doors sit in the middle of the walls, so the middle 3 m of each wall with a door stays clear.
        /// </summary>
        void BuildZones()
        {
            var sign = new Color(1f, 0.9f, 0.6f);

            // Gate Yard: a watch platform up a flight of stairs, a low wall and crates.
            Block("Watch Platform", 0, -14f, 8.5f, -8f, 13.5f, 2.2f, Concrete);
            Stairs(0, -11f, 8.5f, Vector3.back, 2.2f, 2.4f);
            Greybox.Box("Sandbags", Root, L(0, -11f, 2.45f, 13.3f), new Vector3(6f, 0.5f, 0.4f), Cover);
            Greybox.Box("Low wall", Root, L(0, 0f, 0.55f, 4f), new Vector3(6f, 1.1f, 0.5f), Concrete);
            Crates(L(0, -6f, 0f, -10f));
            Crates(L(0, 6f, 0f, 9f));

            // Depot: shelving runs and a storage loft up a ladder.
            Block("Storage Loft", 1, 5f, 10.7f, 12f, 16.7f, 4f, Cover);
            AddLadder(1, 8.5f, 10.7f, Vector3.back, 4f);
            Greybox.Box("Shelf", Root, L(1, -7f, 1.2f, -5f), new Vector3(10f, 2.4f, 1f), Cover);
            Greybox.Box("Shelf", Root, L(1, -7f, 1.2f, 5f), new Vector3(10f, 2.4f, 1f), Cover);
            Greybox.Box("Shelf", Root, L(1, 8f, 1.2f, -6f), new Vector3(8f, 2.4f, 1f), Cover);
            Crates(L(1, -13f, 0f, 11f));

            // Garage: a roof reached by stairs to a landing and a parkour run; the first build station is up top.
            Block("Garage Roof", 2, 9f, 9f, 16.7f, 16.7f, 4.5f, Concrete);
            Block("Landing", 2, 3f, 5.3f, 6f, 8.3f, 2f, Concrete);
            Stairs(2, 4.5f, 5.3f, Vector3.back, 2f, 2.4f);
            Hop(2, 8.2f, 6.8f, 2.8f);
            Hop(2, 11.6f, 6.8f, 3.6f);
            Jump(2, 5.4f, 2f, 6.8f, 7.6f, 2.8f, 6.8f);
            Jump(2, 8.8f, 2.8f, 6.8f, 11f, 3.6f, 6.8f);
            Jump(2, 11.6f, 3.6f, 7.4f, 11.6f, 4.5f, 9.9f);
            Greybox.Box("Wreck", Root, L(2, -8f, 0.75f, -7f), new Vector3(2f, 1.5f, 4.2f), new Color(0.3f, 0.32f, 0.36f));
            Greybox.Box("Wreck", Root, L(2, -9f, 0.75f, 8f), new Vector3(4.2f, 1.5f, 2f), new Color(0.34f, 0.3f, 0.3f));

            // Chapel: pews either side of the aisle, and a choir loft up stairs in the corner.
            Block("Choir Loft", 3, -16.7f, 10f, -10f, 16.7f, 2.6f, Cover);
            Stairs(3, -10f, 12.5f, Vector3.right, 2.6f, 2.4f);
            for (int row = 0; row < 5; row++)
            {
                float z = -9f + row * 3.5f;
                Greybox.Box("Pew", Root, L(3, -5f, 0.45f, z), new Vector3(6f, 0.9f, 0.6f), Cover);
                Greybox.Box("Pew", Root, L(3, 5f, 0.45f, z), new Vector3(6f, 0.9f, 0.6f), Cover);
            }
            Greybox.Box("Lectern", Root, L(3, 7f, 0.6f, 10f), new Vector3(1f, 1.2f, 0.8f), Cover);

            // Barracks: bunks, and a mezzanine along the west wall with stairs and a ladder.
            Block("Mezzanine", 4, -17f, -6f, -12f, 6f, 2.6f, Cover);
            Stairs(4, -14.5f, 6f, Vector3.forward, 2.6f, 2.4f);
            AddLadder(4, -12f, -2f, Vector3.right, 2.6f);
            foreach (float z in new[] { -12f, -8f, -4f, 4f, 8f, 12f })
            {
                Greybox.Box("Bunk", Root, L(4, -6f, 0.8f, z), new Vector3(2f, 1.6f, 1f), Cover);
                Greybox.Box("Bunk", Root, L(4, 6f, 0.8f, z), new Vector3(2f, 1.6f, 1f), Cover);
            }

            // Courtyard: a fountain, four pillars, and a parkour run up to a ruined corner ledge.
            var fountain = Greybox.Prim(PrimitiveType.Cylinder, "Fountain", Root, L(5, 0f, 0.4f, 0f), new Vector3(5f, 0.4f, 5f), Concrete);
            Object.DestroyImmediate(fountain.GetComponent<Collider>());
            var fountainCollider = fountain.AddComponent<MeshCollider>();
            fountainCollider.convex = true;
            foreach (var p in new[] { new Vector2(-7f, -7f), new Vector2(7f, -7f), new Vector2(-7f, 7f), new Vector2(7f, 7f) })
                Greybox.Box("Pillar", Root, L(5, p.x, WallHeight / 2f, p.y), new Vector3(1.4f, WallHeight, 1.4f), Concrete);
            Block("Ruined Ledge", 5, -17f, -17f, -12f, -12f, 3.2f, Concrete);
            Hop(5, -3f, -14.5f, 0.8f);
            Hop(5, -6.4f, -14.5f, 1.6f);
            Hop(5, -9.8f, -14.5f, 2.4f);
            Jump(5, -3f, 0f, -12.2f, -3f, 0.8f, -14.5f);
            Jump(5, -3.6f, 0.8f, -14.5f, -5.8f, 1.6f, -14.5f);
            Jump(5, -7f, 1.6f, -14.5f, -9.2f, 2.4f, -14.5f);
            Jump(5, -10.4f, 2.4f, -14.5f, -12.9f, 3.2f, -14.5f);

            // Loading Dock: a raised dock with stairs, a truck with a ladder up the back, and containers.
            Block("Dock", 6, 11f, -14f, 17f, 10f, 1.4f, Concrete);
            Stairs(6, 11f, -10f, Vector3.left, 1.4f, 2.4f);
            Stairs(6, 11f, 6f, Vector3.left, 1.4f, 2.4f);
            Block("Truck", 6, 1.5f, -11.5f, 4.5f, -4.5f, 3f, new Color(0.3f, 0.34f, 0.4f));
            Greybox.Box("Cab", Root, L(6, 3f, 1.2f, -3.3f), new Vector3(2.8f, 2.4f, 2.2f), new Color(0.5f, 0.2f, 0.18f));
            AddLadder(6, 3f, -11.5f, Vector3.back, 3f);
            Jump(6, 3f, 3f, -5.2f, 3f, 2.4f, -3.3f);
            Block("Container", 6, -12f, 7.7f, -6f, 10.3f, 2.6f, Rust);
            Block("Container", 6, -11.3f, -11f, -8.7f, -5f, 2.6f, new Color(0.25f, 0.38f, 0.45f));

            // Pump Station: a water tower up a tall ladder, and a pump house up stairs.
            Deck("Water Tower", 7, 9f, 7f, 14f, 12f, 6f, Metal);
            var tank = Greybox.Prim(PrimitiveType.Cylinder, "Tank", Root, L(7, 11.5f, 4.1f, 9.5f), new Vector3(3.6f, 1.2f, 3.6f), Rust, collider: false);
            tank.name = "Tank";
            AddLadder(7, 11.5f, 7f, Vector3.back, 6f);
            Block("Pump House", 7, -13f, 7f, -6f, 12f, 2.4f, Concrete);
            Stairs(7, -9.5f, 7f, Vector3.back, 2.4f, 2.4f);
            Greybox.Box("Pump", Root, L(7, -8f, 1f, -9f), new Vector3(4f, 2f, 3f), Metal);
            Greybox.Box("Pipe", Root, L(7, 0f, 4.2f, -9f), new Vector3(12f, 0.5f, 0.5f), Rust, collider: false);

            // Crane Yard: a parkour run up to the top of a container stack, and the crane.
            Block("Container Stack", 8, 4f, 6f, 12f, 9f, 3.6f, Rust);
            Hop(8, -5f, 7.5f, 0.9f);
            Hop(8, -1.6f, 7.5f, 1.8f);
            Hop(8, 1.8f, 7.5f, 2.7f);
            Jump(8, -7f, 0f, 7.5f, -5f, 0.9f, 7.5f);
            Jump(8, -4.4f, 0.9f, 7.5f, -2.2f, 1.8f, 7.5f);
            Jump(8, -1f, 1.8f, 7.5f, 1.2f, 2.7f, 7.5f);
            Jump(8, 2.4f, 2.7f, 7.5f, 4.9f, 3.6f, 7.5f);
            Greybox.Box("Crane", Root, L(8, 12f, 4.5f, -10f), new Vector3(1.5f, 9f, 1.5f), new Color(0.75f, 0.6f, 0.15f));
            Greybox.Box("Boom", Root, L(8, 7f, 8.8f, -10f), new Vector3(11f, 0.6f, 0.6f), new Color(0.75f, 0.6f, 0.15f), collider: false);
            Greybox.Box("Cable", Root, L(8, 2.5f, 6.5f, -10f), new Vector3(0.05f, 4f, 0.05f), new Color(0.15f, 0.15f, 0.15f), collider: false);
            Block("Container", 8, -11f, -9.3f, -5f, -6.7f, 2.6f, new Color(0.3f, 0.45f, 0.3f));
            Block("Container", 8, 4.7f, -9f, 7.3f, -3f, 2.6f, Rust);

            // Lantern Core: the glowing core, a catwalk along the north wall with stairs and a ladder.
            var core = Greybox.Prim(PrimitiveType.Cylinder, "Core", Root, L(9, 0f, 3f, 0f), new Vector3(3f, 3f, 3f), new Color(0.2f, 0.22f, 0.3f));
            core.name = "Lantern Core";
            Greybox.Prim(PrimitiveType.Cylinder, "Core Glow", Root, L(9, 0f, 3.5f, 0f), new Vector3(3.1f, 0.15f, 3.1f), new Color(0.5f, 0.85f, 1f), collider: false, unlit: true);
            var coreLight = new GameObject("Core Light").AddComponent<Light>();
            coreLight.transform.SetParent(Root, false);
            coreLight.transform.localPosition = L(9, 0f, 4f, 0f);
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.5f, 0.85f, 1f);
            coreLight.range = 12f;
            coreLight.intensity = 1.8f;
            coreLight.shadows = LightShadows.None;
            Deck("Catwalk", 9, -16.7f, 13.3f, 2f, 17f, 3f, Metal);
            Stairs(9, -14f, 13.3f, Vector3.back, 3f, 2f);
            AddLadder(9, -2f, 13.3f, Vector3.back, 3f);

            // Archives: rows of shelves and a records loft up a ladder.
            Block("Records Loft", 10, 3f, 11f, 13f, 17f, 4f, Cover);
            AddLadder(10, 8f, 11f, Vector3.back, 4f);
            foreach (float x in new[] { -12f, -8f, -4f, 4f, 8f, 12f })
            {
                Greybox.Box("Bookshelf", Root, L(10, x, 1.1f, 5.75f), new Vector3(0.8f, 2.2f, 4.5f), Cover);
                Greybox.Box("Bookshelf", Root, L(10, x, 1.1f, -8f), new Vector3(0.8f, 2.2f, 8f), Cover);
            }

            // Greenhouse: planters, glass frames, and a potting shed whose roof (up a ladder) holds the second build station.
            Block("Potting Shed", 11, -14f, 4f, -5f, 12f, 4.5f, new Color(0.38f, 0.32f, 0.26f));
            AddLadder(11, -5f, 8f, Vector3.right, 4.5f);
            foreach (float z in new[] { -12f, -8f, -4.5f })
            {
                Greybox.Box("Planter", Root, L(11, -8f, 0.4f, z), new Vector3(6f, 0.8f, 1.2f), new Color(0.3f, 0.22f, 0.15f));
                Greybox.Box("Planter", Root, L(11, 8f, 0.4f, z), new Vector3(6f, 0.8f, 1.2f), new Color(0.3f, 0.22f, 0.15f));
            }
            var glass = new Color(0.55f, 0.75f, 0.7f);
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
                Greybox.Box("Frame", Root, L(11, x, 3f, -8f), new Vector3(0.15f, 6f, 0.15f), glass, collider: false);
            Greybox.Box("Frame", Root, L(11, 0f, 6f, -8f), new Vector3(24f, 0.15f, 0.15f), glass, collider: false);

            // Names over the high spots, so players know where to look.
            for (int i = 0; i < HighSpotCount; i++)
            {
                var label = Greybox.Label("High Spot", Root, HighSpotPoints[i] + Vector3.up * 2.6f, HighSpotNames[i].ToUpperInvariant(), sign, 0.5f);
                label.gameObject.AddComponent<Billboard>();
            }
        }

        void AddNavLinks()
        {
            var holder = Root.gameObject.AddComponent<NavLinkHolder>();
            foreach (var (from, to) in _links)
            {
                var data = new NavMeshLinkData
                {
                    startPosition = from,
                    endPosition = to,
                    width = 0.6f,
                    costModifier = -1f,
                    bidirectional = true,
                    area = 2, // Built-in "Jump" area: a little dearer than walking, so zombies only climb when it's the way.
                    agentTypeID = 0,
                };
                holder.Add(NavMesh.AddLink(data));
            }
        }

        void BuildLamps()
        {
            for (int i = 0; i < ZoneCount; i++)
                foreach (var c in new[] { new Vector2(-8.5f, -8.5f), new Vector2(8.5f, -8.5f), new Vector2(-8.5f, 8.5f), new Vector2(8.5f, 8.5f) })
                    AddLamp(ZoneCenters[i] + new Vector3(c.x, WallHeight - 0.4f, c.y));
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
            light.intensity = 1.5f;
            light.color = Lamp;
            light.shadows = LightShadows.None;
        }

        void BuildDoors(List<Buyable> buyables)
        {
            Doors = new Door[DoorLinks.Length];
            for (int i = 0; i < DoorLinks.Length; i++)
            {
                Vector3 c = DoorCenter(i);
                // Zones side by side east-west share a wall running along z, so their door faces along x.
                bool facesX = Mathf.Abs(GridCenter(DoorLinks[i].x).x - GridCenter(DoorLinks[i].y).x) > 1f;
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
            for (int i = 0; i < WallBuySpots.Length; i++)
            {
                var (face, inward) = Spot(WallBuySpots[i].zone, WallBuySpots[i].side, WallBuySpots[i].along);
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
                var (_, inward) = Spot(i, GeneratorWalls[i].side, GeneratorWalls[i].along);
                var go = Greybox.Empty("Generator " + ZoneNames[i], Root, GeneratorPosition(i)).gameObject;
                // The gauge side faces into the zone.
                go.transform.localRotation = Quaternion.Euler(0f, YawFor(inward), 0f);
                Generators[i] = go.AddComponent<Generator>();
                Generators[i].Init();
            }
        }

        /// <summary>A signal beacon on every high spot, for the high-ground missions.</summary>
        void BuildBeacons()
        {
            Beacons = new Generator[HighSpotCount];
            for (int i = 0; i < HighSpotCount; i++)
            {
                var go = Greybox.Empty("Beacon " + HighSpotNames[i], Root, HighSpotPoints[i]).gameObject;
                Beacons[i] = go.AddComponent<Generator>();
                Beacons[i].Init();
                Greybox.Box("Mast", go.transform, new Vector3(-0.5f, 2f, 0.2f), new Vector3(0.1f, 1.6f, 0.1f), Metal, collider: false);
                Greybox.Prim(PrimitiveType.Sphere, "Dish", go.transform, new Vector3(-0.5f, 2.8f, 0.2f), new Vector3(0.5f, 0.5f, 0.15f), Metal, collider: false);
            }
        }

        /// <summary>The two build stations: a long workbench with a place for each buildable.</summary>
        void BuildBenches()
        {
            var names = new[] { "SCRAP CANNON", "SCRAP SHIELD", "BUDDY BOT" };
            for (int b = 0; b < BenchCount; b++)
            {
                var root = Greybox.Empty("Build Station " + BenchNames[b], Root, BenchPositions[b]);
                Greybox.Box("Bench", root, new Vector3(0f, 0.47f, 0f), new Vector3(5.4f, 0.94f, 0.8f), new Color(0.42f, 0.32f, 0.2f));
                Greybox.Box("Vice", root, new Vector3(-2.2f, 1.05f, 0.1f), new Vector3(0.3f, 0.2f, 0.3f), Metal, collider: false);
                Greybox.Box("Pegboard", root, new Vector3(0f, 1.8f, 0.38f), new Vector3(5.4f, 1.4f, 0.05f), new Color(0.55f, 0.45f, 0.3f), collider: false);
                for (int i = 0; i < BuildableCount; i++)
                    Greybox.Label("Place", root, new Vector3((i - 1) * 1.8f, 1.9f, 0.34f), names[i], new Color(0.1f, 0.08f, 0.05f), 0.16f).transform.localRotation = Quaternion.identity;
                Greybox.Label("Title", root, new Vector3(0f, 2.75f, 0.34f), "BUILD STATION", new Color(1f, 0.85f, 0.4f), 0.26f).transform.localRotation = Quaternion.identity;
                var lightGo = new GameObject("Bench Light");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.4f, -1.2f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.8f, 0.45f);
                light.range = 6f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Where a player stands to use a station against a wall.</summary>
        static Vector3 StandPoint((int zone, Side side, float along) at)
        {
            var (face, inward) = Spot(at.zone, at.side, at.along);
            return face + inward * 1.4f + Vector3.up;
        }

        /// <summary>Perk machines, the armour station and the reroll shrine. Built before the bake so zombies walk round them.</summary>
        void BuildStations()
        {
            var perks = GameConfig.Perks;
            for (int i = 0; i < PerkSpots.Length; i++)
            {
                var def = perks != null ? perks.At(i) : null;
                BuildVendingMachine(Spot(PerkSpots[i].zone, PerkSpots[i].side, PerkSpots[i].along), def != null ? def.name : "Perk",
                    def != null ? def.color : Color.white, def != null ? def.price.ToString("N0") : "");
            }
            BuildVendingMachine(Spot(ArmorSpot.zone, ArmorSpot.side, ArmorSpot.along), "Armour Plates", new Color(0.6f, 0.65f, 0.72f), "");
            BuildShrine(Spot(RerollSpot.zone, RerollSpot.side, RerollSpot.along));
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
            var obstacle = body.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
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

    /// <summary>Keeps world text turned towards the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }

    /// <summary>Owns the map's NavMesh links (ladders and jumps) and removes them with the map.</summary>
    public class NavLinkHolder : MonoBehaviour
    {
        readonly List<NavMeshLinkInstance> _links = new List<NavMeshLinkInstance>();

        public void Add(NavMeshLinkInstance link) => _links.Add(link);

        void OnDestroy()
        {
            foreach (var l in _links) NavMesh.RemoveLink(l);
            _links.Clear();
        }
    }
}
