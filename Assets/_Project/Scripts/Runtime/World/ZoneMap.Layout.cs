using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The map's floor plan, as plain data: the shape of every zone, where the doors are, the walls that come
    /// out of that, and where everything against a wall stands. Nothing here touches the scene, so the tests
    /// and the map screen can use it too.
    ///
    /// Zones are built from axis-aligned rectangles on a 1 m grid, so they come in different shapes and sizes:
    /// a cross-shaped chapel, a long barracks hall, an L-shaped loading dock, a T-shaped pump station, a
    /// stepped octagon round the Lantern Core. Walls go wherever two zones (or a zone and the outside) meet,
    /// with a gap for each door. Rough plan, north up:
    ///
    ///   Greenhouse   Archives    Lantern Core   Crane Yard
    ///   Barracks     Chapel      Garage         Pump Station
    ///   Courtyard    Gate Yard   Depot          Loading Dock
    /// </summary>
    public partial class ZoneMap
    {
        public const int ZoneCount = 12;
        public const int StartZone = 0;
        public const float WallHeight = 7f;
        /// <summary>Every wall is this thick, centred on the line between two zones.</summary>
        public const float WallThickness = 0.6f;
        const float WallHalf = WallThickness / 2f;
        public const float GapWidth = 3.2f;
        public const float GapHeight = 3.2f;

        public static readonly string[] ZoneNames =
        {
            "Gate Yard", "Depot", "Garage", "Chapel", "Barracks", "Courtyard",
            "Loading Dock", "Pump Station", "Crane Yard", "Lantern Core", "Archives", "Greenhouse",
        };

        /// <summary>
        /// Each zone's middle, which its own layout is measured from (see L). It's also where the zone's name
        /// goes on the map screen, and always open floor.
        /// </summary>
        static readonly Vector3[] ZoneAnchors =
        {
            new Vector3(0f, 0f, 0f), new Vector3(37f, 0f, 0f), new Vector3(33f, 0f, 27f), new Vector3(-4f, 0f, 37f),
            new Vector3(-35f, 0f, 35f), new Vector3(-35f, 0f, -3f), new Vector3(73f, 0f, -12f), new Vector3(69f, 0f, 27f),
            new Vector3(67f, 0f, 71f), new Vector3(28f, 0f, 68f), new Vector3(-5f, 0f, 65f), new Vector3(-39f, 0f, 69f),
        };

        /// <summary>The rectangles each zone is made of, in world metres: x0, z0, x1, z1. They never overlap.</summary>
        static readonly int[][] ZoneRectData =
        {
            // Gate Yard: the walled yard, with the old gate's passage out to the south.
            new[] { -16, -14, 16, 16, -5, -22, 5, -14 },
            // Depot: a long, low warehouse.
            new[] { 16, -12, 58, 12 },
            // Garage.
            new[] { 16, 12, 50, 42 },
            // Chapel: a cross, the nave running north with a transept arm either side.
            new[] { -12, 16, 4, 58, -24, 30, -12, 44, 4, 30, 16, 44 },
            // Barracks: one long hall.
            new[] { -46, 12, -24, 58 },
            // Courtyard: wide and open, with its north-west corner fallen in.
            new[] { -54, -18, -16, 4, -44, 4, -16, 12 },
            // Loading Dock: an L, the yard along the south and the dock running up the east side.
            new[] { 58, -22, 88, -2, 72, -2, 88, 20 },
            // Pump Station: a T, the pump hall with the water tower's yard off the north.
            new[] { 50, 20, 88, 34, 62, 34, 76, 50 },
            // Crane Yard: the biggest open space, with a loading bay off the south.
            new[] { 44, 56, 90, 86, 56, 50, 80, 56 },
            // Lantern Core: a stepped octagon round the core, entered from the Garage by a short passage.
            new[] { 12, 56, 44, 80, 16, 52, 40, 56, 20, 48, 36, 52, 16, 80, 40, 84, 20, 84, 36, 88, 22, 42, 34, 48 },
            // Archives: an L, the reading room with the stacks off the north-east.
            new[] { -22, 58, 12, 72, -4, 72, 12, 88 },
            // Greenhouse.
            new[] { -56, 58, -22, 80 },
        };

        static RectInt[][] s_Rects;

        /// <summary>The rectangles a zone is made of (x, y here are world x, z).</summary>
        public static RectInt[] ZoneRects(int zone)
        {
            if (s_Rects == null)
            {
                s_Rects = new RectInt[ZoneCount][];
                for (int z = 0; z < ZoneCount; z++)
                {
                    var d = ZoneRectData[z];
                    var rects = new RectInt[d.Length / 4];
                    for (int i = 0; i < rects.Length; i++)
                        rects[i] = new RectInt(d[i * 4], d[i * 4 + 1], d[i * 4 + 2] - d[i * 4], d[i * 4 + 3] - d[i * 4 + 1]);
                    s_Rects[z] = rects;
                }
            }
            return s_Rects[Mathf.Clamp(zone, 0, ZoneCount - 1)];
        }

        const int MinX = -56, MaxX = 90, MinZ = -22, MaxZ = 88;

        /// <summary>The map's outer edge, for the map screen (x, y here are world x, z).</summary>
        public static readonly Rect Bounds = new Rect(MinX, MinZ, MaxX - MinX, MaxZ - MinZ);

        /// <summary>A point in a zone, measured from the zone's anchor.</summary>
        static Vector3 L(int zone, float x, float y, float z) => ZoneAnchors[zone] + new Vector3(x, y, z);

        public static Vector3 ZoneCenter(int zone) => ZoneAnchors[Mathf.Clamp(zone, 0, ZoneCount - 1)];

        /// <summary>The zone a point is in (by its x and z), or -1 outside every zone.</summary>
        public static int ZoneOfPoint(Vector3 p)
        {
            for (int z = 0; z < ZoneCount; z++)
                foreach (var r in ZoneRects(z))
                    if (p.x >= r.xMin && p.x <= r.xMax && p.z >= r.yMin && p.z <= r.yMax) return z;
            return -1;
        }

        /// <summary>The zone of the 1 m cell whose south-west corner is x, z, or -1.</summary>
        static int CellZone(int x, int z) => ZoneOfPoint(new Vector3(x + 0.5f, 0f, z + 0.5f));

        // ---------------------------------------------------------------- doors

        /// <summary>The zones each door joins, in door order (cheapest near the start). PriceTable.doorCosts follows this order.</summary>
        public static readonly Vector2Int[] DoorLinks =
        {
            new Vector2Int(0, 1), new Vector2Int(0, 5), new Vector2Int(0, 3), new Vector2Int(1, 2),
            new Vector2Int(5, 4), new Vector2Int(1, 6), new Vector2Int(3, 2), new Vector2Int(4, 3),
            new Vector2Int(6, 7), new Vector2Int(2, 7), new Vector2Int(4, 11), new Vector2Int(3, 10),
            new Vector2Int(11, 10), new Vector2Int(7, 8), new Vector2Int(10, 9), new Vector2Int(9, 8),
            new Vector2Int(2, 9),
        };

        /// <summary>Where each door stands (x, z), on the wall its two zones share.</summary>
        static readonly Vector2[] DoorPoints =
        {
            new Vector2(16f, 0f), new Vector2(-16f, 0f), new Vector2(-4f, 16f), new Vector2(33f, 12f),
            new Vector2(-34f, 12f), new Vector2(58f, -7f), new Vector2(16f, 37f), new Vector2(-24f, 37f),
            new Vector2(80f, 20f), new Vector2(50f, 27f), new Vector2(-35f, 58f), new Vector2(-8f, 58f),
            new Vector2(-22f, 65f), new Vector2(69f, 50f), new Vector2(12f, 68f), new Vector2(44f, 68f),
            new Vector2(28f, 42f),
        };

        public static Vector3 DoorCenter(int door) => new Vector3(DoorPoints[door].x, 0f, DoorPoints[door].y);

        /// <summary>True when a door is in a wall that runs north-south (so you walk through it east-west).</summary>
        public static bool DoorInWallAlongZ(int door)
        {
            var c = DoorCenter(door);
            int west = ZoneOfPoint(c + new Vector3(-0.5f, 0f, 0f)), east = ZoneOfPoint(c + new Vector3(0.5f, 0f, 0f));
            return west != east && west >= 0 && east >= 0;
        }

        // ---------------------------------------------------------------- walls

        /// <summary>A straight piece of wall from A to B (x, z), from Bottom to Top. Lintels over doors start at GapHeight.</summary>
        public struct WallPiece
        {
            public Vector2 A, B;
            public float Bottom, Top;
            public bool AlongX => Mathf.Abs(B.x - A.x) > Mathf.Abs(B.y - A.y);
            public float Length => Vector2.Distance(A, B);
        }

        static List<WallPiece> s_Walls;

        /// <summary>
        /// Every wall on the map: wherever two different zones, or a zone and the outside, meet along a grid line,
        /// with a gap (and a lintel over it) for each door. Pieces run a little past their ends so corners close.
        /// </summary>
        public static List<WallPiece> Walls
        {
            get
            {
                if (s_Walls == null) s_Walls = BuildWallPieces();
                return s_Walls;
            }
        }

        static List<WallPiece> BuildWallPieces()
        {
            var pieces = new List<WallPiece>();
            // Lines of constant x (walls running north-south), then lines of constant z.
            for (int x = MinX; x <= MaxX; x++)
                AddRuns(pieces, x, true);
            for (int z = MinZ; z <= MaxZ; z++)
                AddRuns(pieces, z, false);
            return pieces;
        }

        static void AddRuns(List<WallPiece> pieces, int line, bool constantX)
        {
            int from = constantX ? MinZ : MinX, to = constantX ? MaxZ : MaxX;
            int start = int.MinValue;
            for (int t = from; t <= to; t++)
            {
                bool wall = false;
                if (t < to)
                {
                    int a = constantX ? CellZone(line - 1, t) : CellZone(t, line - 1);
                    int b = constantX ? CellZone(line, t) : CellZone(t, line);
                    wall = a != b;
                }
                if (wall && start == int.MinValue) start = t;
                else if (!wall && start != int.MinValue)
                {
                    AddRun(pieces, line, constantX, start, t);
                    start = int.MinValue;
                }
            }
        }

        /// <summary>One unbroken stretch of wall line, cut round any doors on it.</summary>
        static void AddRun(List<WallPiece> pieces, int line, bool constantX, float start, float end)
        {
            var gaps = new List<float>();
            for (int d = 0; d < DoorPoints.Length; d++)
            {
                var p = DoorPoints[d];
                float across = constantX ? p.x : p.y, along = constantX ? p.y : p.x;
                if (Mathf.Abs(across - line) < 0.01f && along > start && along < end) gaps.Add(along);
            }
            gaps.Sort();
            float s = start - WallHalf;
            foreach (float g in gaps)
            {
                float gapFrom = g - GapWidth / 2f, gapTo = g + GapWidth / 2f;
                if (gapFrom > s + 0.01f) pieces.Add(Piece(line, constantX, s, gapFrom, 0f, WallHeight));
                pieces.Add(Piece(line, constantX, gapFrom, gapTo, GapHeight, WallHeight));
                s = gapTo;
            }
            float e = end + WallHalf;
            if (e > s + 0.01f) pieces.Add(Piece(line, constantX, s, e, 0f, WallHeight));
        }

        static WallPiece Piece(int line, bool constantX, float from, float to, float bottom, float top) => new WallPiece
        {
            A = constantX ? new Vector2(line, from) : new Vector2(from, line),
            B = constantX ? new Vector2(line, to) : new Vector2(to, line),
            Bottom = bottom,
            Top = top,
        };

        // ---------------------------------------------------------------- things against walls

        public enum Side { North, South, East, West }

        /// <summary>
        /// A place against a zone's wall: which wall (the side it's on, and its line in the zone's own
        /// coordinates) and how far along it (x for north and south walls, z for east and west ones).
        /// </summary>
        public struct WallSpot
        {
            public int Zone;
            public Side Side;
            public float Line, Along;

            public WallSpot(int zone, Side side, float line, float along)
            {
                Zone = zone;
                Side = side;
                Line = line;
                Along = along;
            }

            /// <summary>The point on the wall's face and the direction into the zone.</summary>
            public (Vector3 face, Vector3 inward) Resolve()
            {
                switch (Side)
                {
                    case Side.North: return (L(Zone, Along, 0f, Line - WallHalf), Vector3.back);
                    case Side.South: return (L(Zone, Along, 0f, Line + WallHalf), Vector3.forward);
                    case Side.East: return (L(Zone, Line - WallHalf, 0f, Along), Vector3.left);
                    default: return (L(Zone, Line + WallHalf, 0f, Along), Vector3.right);
                }
            }
        }

        static WallSpot W(int zone, Side side, float line, float along) => new WallSpot(zone, side, line, along);

        /// <summary>Which zone each wall gun is in, in wall-buy order. PriceTable.wallGuns follows this order.</summary>
        public static readonly int[] WallBuyZones = { 0, 1, 5, 2, 4, 9, 3, 7, 8 };
        public static readonly WallSpot[] WallBuySpots =
        {
            W(0, Side.South, -14, -10), W(1, Side.South, -12, -8), W(5, Side.West, -19, 0), W(2, Side.West, -17, -6),
            W(4, Side.West, -11, 14), W(9, Side.East, 16, 8), W(3, Side.South, -7, -14), W(7, Side.West, -7, 16),
            W(8, Side.North, 15, -8),
        };

        /// <summary>Perk machines in PerkKind order.</summary>
        public static readonly WallSpot[] PerkSpots =
        {
            W(3, Side.North, 7, 14),     // Ironhide Tonic, Chapel (east arm)
            W(1, Side.South, -12, 8),    // Slick Fingers, Depot
            W(0, Side.West, -16, -8),    // Second Wind, Gate Yard
            W(6, Side.South, -10, -2),   // Hair Trigger, Loading Dock
            W(5, Side.North, 7, -14),    // Fleet Foot, Courtyard
            W(10, Side.South, -7, 10),   // Steady Hand, Archives
            W(11, Side.North, 11, 8),    // Hazard Ward, Greenhouse
            W(9, Side.South, -16, 10),   // Bloodline, Lantern Core
        };

        /// <summary>Armour plates in the Barracks; power-up rerolls in the Pump Station.</summary>
        public static readonly WallSpot ArmorSpot = W(4, Side.East, 11, 14);
        public static readonly WallSpot RerollSpot = W(7, Side.East, 19, -3);

        /// <summary>Boarded windows zombies climb in through, three per zone.</summary>
        public static readonly WallSpot[] WindowSpots =
        {
            W(0, Side.South, -22, 0), W(0, Side.South, -14, 12), W(0, Side.North, 16, 11),
            W(1, Side.South, -12, 0), W(1, Side.South, -12, 17), W(1, Side.East, 21, 1),
            W(2, Side.South, -15, -12), W(2, Side.East, 17, -11), W(2, Side.North, 15, -13),
            W(3, Side.North, 7, -14), W(3, Side.South, -7, 14), W(3, Side.West, -8, -9.5f),
            W(4, Side.West, -11, -16), W(4, Side.East, 11, -16), W(4, Side.North, 23, -8),
            W(5, Side.West, -19, -8), W(5, Side.South, -15, 8), W(5, Side.North, 15, 12),
            W(6, Side.South, -10, 5), W(6, Side.East, 15, 28), W(6, Side.West, -1, 20),
            W(7, Side.South, -7, -12), W(7, Side.North, 7, -16), W(7, Side.East, 7, 10),
            W(8, Side.North, 15, 14), W(8, Side.East, 23, -6), W(8, Side.West, -23, 10),
            W(9, Side.West, -16, -8), W(9, Side.South, -16, -10), W(9, Side.North, 16, -10),
            W(10, Side.North, 7, -14.5f), W(10, Side.West, 1, 12), W(10, Side.South, -7, -13),
            W(11, Side.West, -17, -6), W(11, Side.North, 11, 0), W(11, Side.East, 17, 6),
        };

        /// <summary>One mission generator per zone (zone order), against a wall and clear of windows and doors.</summary>
        public static readonly WallSpot[] GeneratorSpots =
        {
            W(0, Side.South, -14, 8), W(1, Side.North, 12, -14), W(2, Side.South, -15, 10), W(3, Side.East, 8, 10),
            W(4, Side.East, 11, 20), W(5, Side.East, 19, -10), W(6, Side.North, 10, -13), W(7, Side.North, 7, -10),
            W(8, Side.South, -15, 18), W(9, Side.West, -16, 6), W(10, Side.North, 7, -5.5f), W(11, Side.South, -11, 0),
        };

        static Vector3 GeneratorPosition(int zone)
        {
            var (face, inward) = GeneratorSpots[zone].Resolve();
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
            L(1, 16f, 4f, 10f), L(2, 15f, 4.5f, 9f), L(7, 4.2f, 6f, 17.2f),
            L(8, 11.2f, 3.6f, 7.5f), L(10, 12f, 4f, 20.5f), L(11, -9.5f, 4.5f, 3.5f),
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
            L(1, 13f, 4f, 9f), L(5, -16.2f, 3.2f, -12.2f), L(10, -15f, 0f, 3f),
            L(2, -13f, 0f, -11f), L(8, 7f, 3.6f, 7.5f), L(4, -8.2f, 2.6f, -5f),
            L(3, 5f, 2.6f, 18f), L(6, 12f, 1.4f, -5f), L(11, 13f, 0f, 8f),
            // Scrap Shield: frame, plate, handle.
            L(7, 2.7f, 6f, 18.5f), L(0, -12.7f, 2.2f, 13f), L(9, 12f, 0f, -10f),
            L(9, -2f, 3f, 18f), L(4, 8f, 0f, -20f), L(6, -6.5f, 3f, -3f),
            L(10, 4f, 4f, 20.5f), L(1, -18f, 0f, -9f), L(7, 15f, 2.4f, 4f),
            // Buddy Bot: chassis, rotor, brain.
            L(8, -18f, 0f, 12f), L(3, 17f, 0f, 4f), L(5, -16f, 0f, 4f),
            L(11, -6.5f, 4.5f, 3f), L(2, -13f, 0f, 11f), L(0, -13f, 0f, -11f),
            L(6, -13f, 0f, -8f), L(9, -12f, 0f, 10f), L(10, 14f, 0f, -4.5f),
        };

        public static int PartSpotCount => PartSpots.Length;
        public static Vector3 PartSpot(int spot) => PartSpots[Mathf.Clamp(spot, 0, PartSpots.Length - 1)];

        /// <summary>The two build stations: on the Garage Roof and the Greenhouse Roof. Each is a long bench, facing -z, with a place per buildable.</summary>
        static readonly Vector3[] BenchPositions = { L(2, 13f, 4.5f, 13.8f), L(11, -10f, 4.5f, 9.6f) };
        public static readonly string[] BenchNames = { "Garage Roof", "Greenhouse Roof" };
        public static Vector3 BenchPosition(int bench) => BenchPositions[Mathf.Clamp(bench, 0, BenchCount - 1)];
        /// <summary>Where a player stands to build one buildable at a bench.</summary>
        public static Vector3 BenchPlace(int bench, int buildable) => BenchPosition(bench) + new Vector3((buildable - 1) * 1.8f, 1f, -1.2f);

        /// <summary>The upgrade machine stands in a north alcove of the Lantern Core, the zone at the heart of the far side.</summary>
        static readonly Vector3 MachinePosition = L(9, 10f, 0f, 15f);
        /// <summary>The spawn shop's counter in the Gate Yard, served from the west side.</summary>
        static readonly Vector3 ShopPosition = L(0, 11f, 0f, -6f);

        public static Vector3 MachineWorldPosition => MachinePosition;
        public static Vector3 ShopWorldPosition => ShopPosition;
        public static Vector3 StartPosition => L(StartZone, 0f, 0.05f, -6f);

        /// <summary>Where a protect mission's survivor crouches: a few metres out from the zone's generator.</summary>
        public static Vector3 SurvivorSpot(int zone)
        {
            zone = Mathf.Clamp(zone, 0, ZoneCount - 1);
            var (_, inward) = GeneratorSpots[zone].Resolve();
            return GeneratorPosition(zone) + inward * 3.2f;
        }
    }
}
