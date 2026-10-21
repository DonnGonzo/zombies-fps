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
        /// <summary>The Lantern Core's reactor, where the easter egg's steps start. Free to touch.</summary>
        Reactor,
        /// <summary>One of the easter egg's later places (relics, altars, the Watchman, portals); Index is the EggSpot.</summary>
        Egg,
        /// <summary>The start-up generator's control panel, where the code is entered (opens a keypad, like the shop).</summary>
        PowerPanel,
        /// <summary>A note with one digit of the start-up code; Index is the note spot. Free to read.</summary>
        Note,
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

        public bool Contains(Vector3 p) => ZoneMap.ZoneOfPoint(p) == Id;
    }

    /// <summary>
    /// The greybox map, built in code on every machine from the floor plan in ZoneMap.Layout: twelve zones of
    /// different shapes and sizes joined by buyable doors, with many ways round to retreat and kite. Players
    /// start in the Gate Yard; each door opens a new zone and its zombie windows. Zones have stairs up to
    /// platforms, parkour runs and ladders, and six high spots that can only be reached by climbing; those hold
    /// build parts, the build stations and the high-ground missions. The NavMesh is baked at runtime with the
    /// doors left out so they can carve it while closed, and zombies get NavMesh links up every ladder and jump.
    /// </summary>
    public partial class ZoneMap
    {
        const float DoorRange = 2.6f;
        const float WallBuyRange = 2f;
        const float MachineRange = 2.2f;
        const float ShopRange = 2.2f;
        const float StationRange = 2f;
        const float PartRange = 1.6f;
        const float BenchRange = 1.1f;
        const float StepRise = 0.25f;
        const float StepRun = 0.35f;
        /// <summary>Parkour blocks are 3 m square: big enough that zombies keep a path map on top of them.</summary>
        const float HopSize = 3f;

        /// <summary>What a zone's floor and walls are made of. Walls get a lower band (wainscot, paint, tiles) when BandHeight > 0.</summary>
        readonly struct ZoneLook
        {
            public readonly Surface Floor, Wall, Band;
            public readonly Color FloorColor, WallColor, BandColor;
            public readonly float BandHeight;

            public ZoneLook(Surface floor, Color floorColor, Surface wall, Color wallColor, Surface band = Surface.Plain, Color bandColor = default, float bandHeight = 0f)
            {
                Floor = floor; FloorColor = floorColor; Wall = wall; WallColor = wallColor;
                Band = band; BandColor = bandColor; BandHeight = bandHeight;
            }
        }

        /// <summary>Per zone (zone order), so each part of the facility reads as its own kind of place.</summary>
        static readonly ZoneLook[] Looks =
        {
            // Gate Yard: an asphalt yard inside bare concrete walls, grimy at the foot.
            new ZoneLook(Surface.Asphalt, new Color(0.2f, 0.2f, 0.21f), Surface.Concrete, new Color(0.42f, 0.41f, 0.38f), Surface.Concrete, new Color(0.28f, 0.28f, 0.27f), 0.9f),
            // Depot: a flagstone floor and old brick walls on a stone plinth.
            new ZoneLook(Surface.Tile, new Color(0.4f, 0.39f, 0.36f), Surface.Brick, new Color(0.42f, 0.26f, 0.19f), Surface.Concrete, new Color(0.35f, 0.34f, 0.32f), 1f),
            // Garage: darker, sooty flagstones; walls limewashed green up to shoulder height.
            new ZoneLook(Surface.Tile, new Color(0.31f, 0.3f, 0.28f), Surface.Concrete, new Color(0.42f, 0.41f, 0.39f), Surface.Plaster, new Color(0.2f, 0.28f, 0.24f), 1.3f),
            // Chapel: a grey octagon-and-diamond tiled floor, dressed ashlar walls on a dark masonry plinth.
            new ZoneLook(Surface.MosaicGrey, new Color(0.42f, 0.41f, 0.39f), Surface.Ashlar, new Color(0.6f, 0.56f, 0.48f), Surface.Masonry, new Color(0.34f, 0.32f, 0.3f), 1.2f),
            // Barracks: worn floorboards, institutional pale green plaster, wood skirting.
            new ZoneLook(Surface.Wood, new Color(0.36f, 0.26f, 0.17f), Surface.Plaster, new Color(0.47f, 0.5f, 0.42f), Surface.Wood, new Color(0.3f, 0.22f, 0.14f), 1f),
            // Courtyard: stone paving and weathered brick.
            new ZoneLook(Surface.Tile, new Color(0.4f, 0.38f, 0.34f), Surface.Brick, new Color(0.44f, 0.29f, 0.21f)),
            // Loading Dock: asphalt, concrete walls with a faded yellow safety band.
            new ZoneLook(Surface.Asphalt, new Color(0.22f, 0.22f, 0.23f), Surface.Concrete, new Color(0.4f, 0.4f, 0.39f), Surface.Plaster, new Color(0.42f, 0.35f, 0.13f), 0.8f),
            // Pump Station: wet tiled floor, tiled lower walls, concrete above.
            new ZoneLook(Surface.Tile, new Color(0.36f, 0.42f, 0.41f), Surface.Concrete, new Color(0.38f, 0.4f, 0.39f), Surface.Tile, new Color(0.4f, 0.5f, 0.48f), 1.6f),
            // Crane Yard: packed dirt and gravel, raw concrete walls.
            new ZoneLook(Surface.Dirt, new Color(0.3f, 0.26f, 0.2f), Surface.Concrete, new Color(0.36f, 0.35f, 0.33f)),
            // Lantern Core: steel deck plate and steel wall panels.
            new ZoneLook(Surface.Metal, new Color(0.3f, 0.32f, 0.35f), Surface.Metal, new Color(0.27f, 0.29f, 0.33f), Surface.Metal, new Color(0.15f, 0.16f, 0.19f), 1f),
            // Archives: parquet, yellowed plaster, tall wood panelling.
            new ZoneLook(Surface.Wood, new Color(0.38f, 0.27f, 0.17f), Surface.Plaster, new Color(0.52f, 0.47f, 0.38f), Surface.Wood, new Color(0.29f, 0.19f, 0.11f), 1.2f),
            // Greenhouse: worn terracotta tiles, old brick walls on a low concrete kerb.
            new ZoneLook(Surface.Tile, new Color(0.42f, 0.3f, 0.22f), Surface.Brick, new Color(0.4f, 0.28f, 0.22f), Surface.Concrete, new Color(0.32f, 0.31f, 0.29f), 0.6f),
            // Gaol: wet, dark flagstones and bare castle stone, blackened at the foot.
            new ZoneLook(Surface.Tile, new Color(0.26f, 0.26f, 0.26f), Surface.Concrete, new Color(0.34f, 0.33f, 0.31f), Surface.Concrete, new Color(0.2f, 0.2f, 0.2f), 1.2f),
            // Plague Ward: scrubbed boards, limewashed walls over a tiled dado.
            new ZoneLook(Surface.Wood, new Color(0.42f, 0.33f, 0.23f), Surface.Plaster, new Color(0.62f, 0.6f, 0.53f), Surface.Tile, new Color(0.42f, 0.48f, 0.45f), 1.4f),
            // Scriptorium: warm boards, ochre plaster above tall dark panelling.
            new ZoneLook(Surface.Wood, new Color(0.4f, 0.28f, 0.18f), Surface.Plaster, new Color(0.6f, 0.52f, 0.38f), Surface.Wood, new Color(0.25f, 0.16f, 0.1f), 1.6f),
            // Alchemist's Furnace: sooty flags and scorched brick on an iron-banded plinth.
            new ZoneLook(Surface.Tile, new Color(0.24f, 0.22f, 0.2f), Surface.Brick, new Color(0.34f, 0.2f, 0.15f), Surface.Rust, new Color(0.3f, 0.17f, 0.1f), 1f),
            // Rampart Ruins: trampled earth inside old grey curtain walls.
            new ZoneLook(Surface.Dirt, new Color(0.3f, 0.27f, 0.2f), Surface.Concrete, new Color(0.46f, 0.44f, 0.4f)),
            // Sunken Temple: dark wet flags, green-grey masonry, stained darker to the old tide line.
            new ZoneLook(Surface.Ashlar, new Color(0.3f, 0.32f, 0.3f), Surface.Masonry, new Color(0.42f, 0.44f, 0.4f), Surface.Masonry, new Color(0.22f, 0.26f, 0.23f), 0.9f),
        };

        /// <summary>The wall core: shows only on wall ends, tops and the outside, since every zone face gets its own skin.</summary>
        static readonly Color Wall = new Color(0.36f, 0.35f, 0.33f);
        static readonly Color Cover = new Color(0.4f, 0.36f, 0.3f);
        static readonly Color Concrete = new Color(0.44f, 0.43f, 0.4f);
        static readonly Color Metal = new Color(0.3f, 0.32f, 0.34f);
        static readonly Color Rust = new Color(0.52f, 0.26f, 0.13f);
        static readonly Color Wood = new Color(0.4f, 0.29f, 0.17f);
        static readonly Color DarkWood = new Color(0.28f, 0.19f, 0.11f);
        static readonly Color Board = new Color(0.33f, 0.23f, 0.14f);
        static readonly Color DoorIron = new Color(0.17f, 0.17f, 0.18f);
        static readonly Color Chalk = new Color(0.82f, 0.8f, 0.72f);
        static readonly Color Lamp = new Color(1f, 0.85f, 0.6f);
        static readonly Color LadderColor = new Color(0.55f, 0.44f, 0.14f);
        static readonly Color ContainerBlue = new Color(0.14f, 0.3f, 0.48f);
        static readonly Color ContainerGreen = new Color(0.18f, 0.38f, 0.22f);
        static readonly Color CraneYellow = new Color(0.72f, 0.52f, 0.1f);
        static readonly Color HazardYellow = new Color(0.62f, 0.5f, 0.1f);
        static readonly Color Girder = new Color(0.22f, 0.2f, 0.19f);
        static readonly Color BunkGreen = new Color(0.24f, 0.28f, 0.22f);
        static readonly Color CoreGlow = new Color(0.5f, 0.85f, 1f);
        /// <summary>The Lantern Core's drum, sized to the reactor model that stands in its place.</summary>
        const float CoreDiameter = Reactor.Diameter + 0.04f;
        /// <summary>Height of the Archives' book stacks: well over head height, so the aisles read as a maze of shelves.</summary>
        const float StackHeight = 4.4f;
        /// <summary>
        /// Ceiling lamps: about this far apart, so nowhere is more than about 7 m from one, and how far and bright
        /// each shines. Floors are drawn in tiles about FloorTile across so each tile takes the lamps over it.
        /// </summary>
        const float LampSpacing = 10f, LampRange = 15f, LampIntensity = 1.8f, FloorTile = 8f;
        /// <summary>Whether the reactor model went in (the drum's own trim is left off then).</summary>
        bool _reactor;

        /// <summary>Parkour blocks per zone: stacked steel crates in the Garage, broken masonry in the Courtyard, rusty crates in the Crane Yard.</summary>
        static (Surface surface, Color color) HopLook(int zone) => zone switch
        {
            2 => (Surface.Metal, new Color(0.3f, 0.34f, 0.36f)),
            5 => (Surface.Concrete, new Color(0.45f, 0.43f, 0.39f)),
            8 => (Surface.Rust, new Color(0.45f, 0.26f, 0.15f)),
            16 => (Surface.Concrete, new Color(0.46f, 0.44f, 0.4f)),
            _ => (Surface.Wood, Wood),
        };

        /// <summary>Stairs match the room: wooden in the Chapel, Barracks and Greenhouse, steel in the Lantern Core, concrete elsewhere.</summary>
        static (Surface surface, Color color) StairLook(int zone) => zone switch
        {
            3 => (Surface.Wood, new Color(0.3f, 0.21f, 0.13f)),
            4 => (Surface.Wood, new Color(0.33f, 0.24f, 0.15f)),
            9 or 15 => (Surface.Metal, new Color(0.3f, 0.32f, 0.35f)),
            11 or 13 or 14 => (Surface.Wood, new Color(0.33f, 0.24f, 0.15f)),
            _ => (Surface.Concrete, Concrete),
        };

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
        /// <summary>Where climbers stand at each ladder's foot, kept clear of floor clutter.</summary>
        readonly List<Vector3> _ladderFeet = new List<Vector3>();

        // ---------------------------------------------------------------- queries

        /// <summary>The zone a position is in, or -1 outside the map.</summary>
        public int ZoneAt(Vector3 position) => ZoneOfPoint(position);

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
            // Scanned medieval textures and props, unless turned off in Settings. Only looks: colliders never change.
            SurfaceTextures.PhotosEnabled = GameSettings.MapArt;
            var map = new ZoneMap();
            map.BuildInternal(prices);
            return map;
        }

        void BuildInternal(PriceTable prices)
        {
            Root = new GameObject("Map").transform;
            Ladder.ClearAll();
            Zones = new Zone[ZoneCount];
            for (int i = 0; i < ZoneCount; i++) Zones[i] = new Zone { Id = i, Name = ZoneNames[i], Center = ZoneAnchors[i] };

            // Colliderless dressing collects here as it's made and is merged into a few meshes at the end.
            _decor = new DecorBatch();

            // Floors, one collider per rectangle, in each zone's own material. They're drawn as tiles through the
            // dressing, so each patch of floor is lit by the lamps over it: one huge floor only takes its few
            // nearest lights per pixel and goes dark between them.
            for (int i = 0; i < ZoneCount; i++)
                foreach (var r in ZoneRects(i))
                {
                    var floor = Greybox.Box("Floor " + ZoneNames[i], Root, new Vector3(r.center.x, -0.5f, r.center.y), new Vector3(r.width, 1f, r.height),
                        Looks[i].FloorColor, surface: Looks[i].Floor);
                    floor.GetComponent<Renderer>().enabled = false;
                    int nx = Mathf.CeilToInt(r.width / FloorTile), nz = Mathf.CeilToInt(r.height / FloorTile);
                    for (int a = 0; a < nx; a++)
                        for (int b = 0; b < nz; b++)
                        {
                            float x0 = r.xMin + a * r.width / nx, z0 = r.yMin + b * r.height / nz;
                            Deco(new Vector3(x0 + r.width / nx / 2f, -0.5f, z0 + r.height / nz / 2f), new Vector3(r.width / nx, 1f, r.height / nz),
                                Looks[i].FloorColor, Looks[i].Floor);
                        }
                }

            BuildWalls();
            foreach (var w in WindowSpots) AddWindow(w.Zone, w.Resolve());
            BuildZones();
            BuildLamps();
            BuildRoof();
            BuildGenerators();
            BuildPower();
            BuildBeacons();
            BuildShop();
            BuildStations();
            BuildBenches();
            // Everything solid is in place now, so the dressing can keep clear of it.
            DressZones();
            BuildEggRooms();
            BuildSkyline();
            _decor.Build(Root);
            _decor = null;

            var surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Keep small walkable tops (parkour blocks, the truck's cab) so zombies can path onto them.
            surface.minRegionArea = 0.5f;
            surface.BuildNavMesh();
            AddNavLinks();
            SealWallTops();
            RoofColliders();

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
            // Reached from any side: the range is measured from the reactor's middle.
            buyables.Add(new Buyable { Kind = BuyableKind.Reactor, Index = 0, Position = ReactorBase + Vector3.up, Range = CoreDiameter / 2f + 1.6f });
            AddEggBuyables(buyables);
            AddPowerBuyables(buyables);
            Buyables = buyables.ToArray();

            PlayerStart = StartPosition;
        }

        /// <summary>Every wall and door lintel from the floor plan.</summary>
        void BuildWalls()
        {
            foreach (var piece in Walls)
            {
                var mid = (piece.A + piece.B) / 2f;
                float height = piece.Top - piece.Bottom;
                var size = piece.AlongX ? new Vector3(piece.Length, height, WallThickness) : new Vector3(WallThickness, height, piece.Length);
                Greybox.Box(piece.Bottom > 0f ? "Lintel" : "Wall", Root, new Vector3(mid.x, piece.Bottom + height / 2f, mid.y), size, Wall, surface: Surface.Concrete);
                SkinWall(piece);
            }
        }

        /// <summary>Layer for invisible blockers: bullets, decals and sight lines pass through it, bodies don't.</summary>
        public const int BlockerLayer = 2; // Unity's built-in "Ignore Raycast" layer
        /// <summary>Raycast mask for shots and effects: everything except the invisible blockers.</summary>
        public const int ShotMask = ~(1 << BlockerLayer);

        /// <summary>
        /// Invisible walls from the top of every full-height wall up into the sky, so nobody can jump from a high
        /// spot over a wall, walk along the wall tops, or drop into the sealed-off gaps between zones and fall
        /// forever. Added after the NavMesh bake so they don't change any paths.
        /// </summary>
        void SealWallTops()
        {
            const float sealHeight = 60f;
            var seals = Greybox.Empty("Wall Seals", Root, Vector3.zero);
            foreach (var piece in Walls)
            {
                if (piece.Top < WallHeight - 0.01f) continue;
                var mid = (piece.A + piece.B) / 2f;
                var size = piece.AlongX ? new Vector3(piece.Length, sealHeight, WallThickness) : new Vector3(WallThickness, sealHeight, piece.Length);
                var go = new GameObject("Seal") { layer = BlockerLayer };
                go.transform.SetParent(seals, false);
                go.transform.localPosition = new Vector3(mid.x, piece.Top + sealHeight / 2f, mid.y);
                go.AddComponent<BoxCollider>().size = size;
            }
        }

        void AddWindow(int zone, (Vector3 face, Vector3 inward) spot)
        {
            var (wallFace, inward) = spot;
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            Vector3 boards = wallFace + inward * 0.06f;
            for (int i = 0; i < 3; i++)
            {
                var board = Greybox.Box("Board", Root, boards + Vector3.up * (1.1f + i * 0.45f), new Vector3(1.8f, 0.22f, 0.08f), Board, collider: false, surface: Surface.Wood);
                board.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (i - 1) * 9f);
            }
            WindowFrame(wallFace, inward);
            var point = Greybox.Empty("ZombieSpawn " + ZoneNames[zone], Root, wallFace + inward * 1.2f);
            point.localRotation = rot;
            Zones[zone].SpawnPoints.Add(point);
        }

        // ---------------------------------------------------------------- level pieces (zone-local coordinates)

        /// <summary>A solid block from the floor up to a height, given by its corners.</summary>
        Vector3 Block(string name, int zone, float x0, float z0, float x1, float z1, float height, Color color, Surface surface = Surface.Concrete)
        {
            var center = L(zone, (x0 + x1) / 2f, height / 2f, (z0 + z1) / 2f);
            Greybox.Box(name, Root, center, new Vector3(x1 - x0, height, z1 - z0), color, surface: surface);
            return center + Vector3.up * (height / 2f);
        }

        /// <summary>A deck on posts you can walk under (top at least 2.6 m up).</summary>
        void Deck(string name, int zone, float x0, float z0, float x1, float z1, float top, Color color)
        {
            const float thick = 0.35f;
            Greybox.Box(name, Root, L(zone, (x0 + x1) / 2f, top - thick / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, thick, z1 - z0), color, surface: Surface.Metal);
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 5f));
            int nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / 5f));
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                {
                    if (i != 0 && i != nx && j != 0 && j != nz) continue;
                    float px = Mathf.Lerp(x0 + 0.25f, x1 - 0.25f, (float)i / nx);
                    float pz = Mathf.Lerp(z0 + 0.25f, z1 - 0.25f, (float)j / nz);
                    Greybox.Box("Post", Root, L(zone, px, (top - thick) / 2f, pz), new Vector3(0.3f, top - thick, 0.3f), Metal, surface: Surface.Metal);
                }
            // A yellow band round the deck's edge so it reads from below.
            Deco(L(zone, (x0 + x1) / 2f, top - thick / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0 + 0.04f, 0.12f, z1 - z0 + 0.04f), LadderColor, Surface.Metal);
        }

        /// <summary>Stairs down from a ledge: edge is the middle of the ledge's side, down points away from it.</summary>
        void Stairs(int zone, float x, float z, Vector3 down, float height, float width)
        {
            int n = Mathf.CeilToInt(height / StepRise - 0.01f);
            float rise = height / n;
            Vector3 edge = L(zone, x, 0f, z);
            bool alongX = Mathf.Abs(down.x) > 0.5f;
            var (surface, color) = StairLook(zone);
            // Industrial stairs get a worn yellow nosing on each tread.
            bool nosing = surface != Surface.Wood;
            for (int i = 0; i < n - 1; i++)
            {
                float top = height - (i + 1) * rise;
                var size = alongX ? new Vector3(StepRun, top, width) : new Vector3(width, top, StepRun);
                var center = edge + down * (StepRun * (i + 0.5f));
                Greybox.Box("Step", Root, center + Vector3.up * (top / 2f), size, color, surface: surface);
                if (nosing)
                    Deco(center + down * (StepRun / 2f - 0.05f) + Vector3.up * (top + 0.004f),
                        alongX ? new Vector3(0.08f, 0.008f, width - 0.1f) : new Vector3(width - 0.1f, 0.008f, 0.08f), HazardYellow, Surface.Concrete);
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
            Greybox.Box("Backing", root, new Vector3(0f, height / 2f, -0.05f), new Vector3(1f, height, 0.1f), new Color(0.32f, 0.21f, 0.13f), surface: Surface.Rust);
            foreach (float side in new[] { -0.32f, 0.32f })
                Greybox.Box("Rail", root, new Vector3(side, height / 2f + 0.3f, 0.09f), new Vector3(0.07f, height + 0.6f, 0.07f), LadderColor, collider: false, surface: Surface.Metal);
            for (float y = 0.3f; y < height; y += 0.35f)
                Greybox.Box("Rung", root, new Vector3(0f, y, 0.09f), new Vector3(0.64f, 0.05f, 0.05f), LadderColor, collider: false, surface: Surface.Rust);
            new Ladder(foot, outward, height);
            _ladderFeet.Add(foot + outward * 0.6f);
            _links.Add((foot + outward * 0.7f, foot - outward * 0.9f + Vector3.up * height));
        }

        /// <summary>A parkour block, HopSize square, centred on x, z.</summary>
        void Hop(int zone, float x, float z, float height)
        {
            var (surface, color) = HopLook(zone);
            Block("Hop", zone, x - HopSize / 2f, z - HopSize / 2f, x + HopSize / 2f, z + HopSize / 2f, height, color, surface);
            // A painted edge on top, so the jump's landing reads in the dark.
            var top = L(zone, x, height + 0.004f, z);
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                bool alongX = Mathf.Abs(d.z) > 0.5f;
                Deco(top + d * (HopSize / 2f - 0.06f), alongX ? new Vector3(HopSize, 0.008f, 0.1f) : new Vector3(0.1f, 0.008f, HopSize), Shade(HazardYellow, 0.8f), Surface.Concrete);
            }
        }

        /// <summary>A jump zombies can take (and players can make), between two zone-local points.</summary>
        void Jump(int zone, float ax, float ay, float az, float bx, float by, float bz) =>
            _links.Add((L(zone, ax, ay, az), L(zone, bx, by, bz)));

        void Crates(Vector3 at)
        {
            Greybox.Box("Crate", Root, at + new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), Wood, surface: Surface.Wood);
            Greybox.Box("Crate", Root, at + new Vector3(1.3f, 0.6f, 0.4f), new Vector3(1.2f, 1.2f, 1.2f), Wood, surface: Surface.Wood);
            CrateBands(at + new Vector3(0f, 0.6f, 0f), 1.2f);
            CrateBands(at + new Vector3(1.3f, 0.6f, 0.4f), 1.2f);
        }

        /// <summary>
        /// Every zone's layout, in coordinates from the zone's anchor (x east, z north). The walls are 0.3 m
        /// inside each zone's edge, and the 3 m in front of every door stays clear.
        /// </summary>
        void BuildZones()
        {
            var sign = new Color(1f, 0.9f, 0.6f);

            // Gate Yard: a watch platform up a flight of stairs, a low wall, crates, and the fallen gate in the passage south.
            Block("Watch Platform", 0, -15.7f, 10f, -9.7f, 15.7f, 2.2f, Concrete);
            Stairs(0, -12.7f, 10f, Vector3.back, 2.2f, 2.4f);
            Greybox.Box("Sandbags", Root, L(0, -9.95f, 2.45f, 12.85f), new Vector3(0.4f, 0.5f, 5.7f), new Color(0.42f, 0.37f, 0.26f), surface: Surface.Fabric);
            Greybox.Box("Low wall", Root, L(0, 0f, 0.55f, 4f), new Vector3(6f, 1.1f, 0.5f), Concrete, surface: Surface.Concrete);
            Greybox.Box("Fallen Gate", Root, L(0, 0f, 0.12f, -18.5f), new Vector3(5f, 0.24f, 1.6f), new Color(0.42f, 0.25f, 0.15f), surface: Surface.Rust);
            Crates(L(0, -6f, 0f, -10f));
            Crates(L(0, 6f, 0f, 9f));

            BuildDepot();

            BuildGarage();

            // Chapel: piers down the nave, pews, the altar at its head and a choir loft up stairs at the north end.
            BuildChapel();

            // Barracks: two rows of bunks down the hall, and a mezzanine along the west wall with stairs and a ladder.
            Block("Mezzanine", 4, -10.7f, -8f, -5.7f, 8f, 2.6f, new Color(0.34f, 0.25f, 0.16f), Surface.Wood);
            Stairs(4, -8.2f, 8f, Vector3.forward, 2.6f, 2.4f);
            AddLadder(4, -5.7f, -3f, Vector3.right, 2.6f);
            foreach (float z in new[] { -17f, -13f, -9f, 9f, 13f, 17f })
            {
                Greybox.Box("Bunk", Root, L(4, 0f, 0.8f, z), new Vector3(2f, 1.6f, 1f), BunkGreen, surface: Surface.Metal);
                Greybox.Box("Bunk", Root, L(4, 5f, 0.8f, z), new Vector3(2f, 1.6f, 1f), BunkGreen, surface: Surface.Metal);
            }

            BuildCourtyard();

            BuildLoadingDock();

            BuildPumpStation();

            // Crane Yard: a parkour run up to the top of a container stack, the crane, and scattered containers.
            Block("Container Stack", 8, 4f, 6f, 12f, 9f, 3.6f, Rust, Surface.Rust);
            Hop(8, -6.2f, 7.5f, 0.9f);
            Hop(8, -2.3f, 7.5f, 1.8f);
            Hop(8, 1.6f, 7.5f, 2.7f);
            Jump(8, -8.7f, 0f, 7.5f, -6.9f, 0.9f, 7.5f);
            Jump(8, -5.4f, 0.9f, 7.5f, -3.1f, 1.8f, 7.5f);
            Jump(8, -1.5f, 1.8f, 7.5f, 0.8f, 2.7f, 7.5f);
            Jump(8, 2.4f, 2.7f, 7.5f, 4.7f, 3.6f, 7.5f);
            // Low enough to stand under the roof.
            Greybox.Box("Crane", Root, L(8, 12f, 3.1f, -10f), new Vector3(1.5f, 6.2f, 1.5f), CraneYellow, surface: Surface.Metal);
            Greybox.Box("Boom", Root, L(8, 7f, 5.9f, -10f), new Vector3(11f, 0.6f, 0.6f), CraneYellow, collider: false, surface: Surface.Metal);
            Greybox.Box("Cable", Root, L(8, 2.5f, 5.1f, -10f), new Vector3(0.05f, 1.6f, 0.05f), new Color(0.12f, 0.12f, 0.12f), collider: false, surface: Surface.Metal);
            Block("Container", 8, -11f, -9.3f, -5f, -6.7f, 2.6f, ContainerGreen, Surface.Metal);
            Block("Container", 8, 4.7f, -9f, 7.3f, -3f, 2.6f, Rust, Surface.Rust);
            Block("Container", 8, -20f, 2f, -14f, 4.6f, 2.6f, ContainerBlue, Surface.Metal);

            // Lantern Core: the reactor (or the old glowing drum without its model), consoles round it, and a catwalk
            // at the north end with stairs and a ladder. The drum's collider stands for the reactor either way.
            var core = Greybox.Prim(PrimitiveType.Cylinder, "Core", Root, L(9, 0f, 3f, 0f), new Vector3(CoreDiameter, 3f, CoreDiameter), new Color(0.18f, 0.2f, 0.27f), surface: Surface.Metal);
            core.name = "Lantern Core";
            var coreLight = new GameObject("Core Light").AddComponent<Light>();
            coreLight.transform.SetParent(Root, false);
            coreLight.transform.localPosition = L(9, 0f, 4f, 0f);
            coreLight.type = LightType.Point;
            coreLight.color = CoreGlow;
            coreLight.range = 12f;
            coreLight.intensity = 1.8f;
            coreLight.shadows = LightShadows.None;
            _reactor = Reactor.Build(Root, L(9, 0f, 0f, 0f), Quaternion.identity, coreLight) != null;
            if (_reactor)
            {
                core.GetComponent<Renderer>().enabled = false;
                coreLight.color = new Color(1f, 0.55f, 0.25f);
                coreLight.transform.localPosition = L(9, 0f, 3f, 0f);
                coreLight.intensity = 2.2f;
            }
            else
                Greybox.Prim(PrimitiveType.Cylinder, "Core Glow", Root, L(9, 0f, 3.5f, 0f), new Vector3(CoreDiameter + 0.1f, 0.15f, CoreDiameter + 0.1f), CoreGlow, collider: false, unlit: true);
            foreach (var p in new[] { new Vector2(-7f, -7f), new Vector2(7f, -7f), new Vector2(-7f, 7f), new Vector2(7f, 7f) })
                Greybox.Box("Console", Root, L(9, p.x, 0.5f, p.y), new Vector3(1.2f, 1f, 1.2f), new Color(0.2f, 0.22f, 0.25f), surface: Surface.Metal);
            Deck("Catwalk", 9, -7.7f, 16f, 7.7f, 19.7f, 3f, Metal);
            Stairs(9, -5.5f, 16f, Vector3.back, 3f, 2f);
            AddLadder(9, 5.5f, 16f, Vector3.back, 3f);

            // Archives: the monks' altar on a stepped dais in the middle of the reading room, rows of tall stacks
            // either side, and a records loft up a ladder at the far end of the north wing.
            BuildArchives();

            // Greenhouse: raised beds, the glass dome over the binding stones, the pond garden, and the gallery
            // along the north wall (up stairs or a ladder) with the second build station (ZoneMap.Greenhouse.cs).
            BuildGreenhouse();

            BuildNewRooms();

            // Names over the high spots, so players know where to look.
            for (int i = 0; i < HighSpotCount; i++)
            {
                var label = Greybox.Label("High Spot", Root, HighSpotPoints[i] + Vector3.up * 1.9f, HighSpotNames[i].ToUpperInvariant(), sign, 0.5f);
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

        /// <summary>
        /// Ceiling lamps over every rectangle of floor, about LampSpacing apart, each hanging from a
        /// steel beam spanning the rectangle's short way at the top of the walls.
        /// </summary>
        void BuildLamps()
        {
            Physics.SyncTransforms();
            for (int i = 0; i < ZoneCount; i++)
                foreach (var r in ZoneRects(i))
                {
                    // The Chapel, the Temple and the Greenhouse hang their own lights.
                    if (r.width < 4 || r.height < 4 || HandDressed(i)) continue;
                    int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / LampSpacing)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / LampSpacing));
                    bool beamsAlongX = r.width <= r.height;
                    for (int a = 0; a < nx; a++)
                        for (int b = 0; b < nz; b++)
                        {
                            var at = new Vector3(r.xMin + (a + 0.5f) * r.width / nx, WallHeight - 0.4f, r.yMin + (b + 0.5f) * r.height / nz);
                            AddLamp(at, i);
                            // One beam per row of lamps.
                            if (beamsAlongX && a == 0)
                                Deco(new Vector3(r.center.x, WallHeight - 0.14f, at.z), new Vector3(r.width, 0.24f, 0.18f), Girder, Surface.Rust);
                            else if (!beamsAlongX && b == 0)
                                Deco(new Vector3(at.x, WallHeight - 0.14f, r.center.y), new Vector3(0.18f, 0.24f, r.height), Girder, Surface.Rust);
                        }
                }
        }

        /// <summary>An enamelled shade on a short rod with a glowing bulb under it, and its light.</summary>
        void AddLamp(Vector3 position, int zone)
        {
            bool lantern = PropModels.Get(PropModels.Lantern) != null;
            bool chandelier = (ChandelierZones.TryGetValue(zone, out var hanging) || KitChandeliers.TryGetValue(zone, out hanging)) && HangChandelier(position, hanging);
            if (chandelier || CeilingStrip(position, zone)) lantern = true;
            else if (lantern) CeilingLantern(position);
            else
            {
                Deco(position + Vector3.up * 0.17f, new Vector3(0.05f, 0.18f, 0.05f), Girder, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, position + Vector3.up * 0.04f, new Vector3(0.75f, 0.05f, 0.75f), Quaternion.identity, new Color(0.17f, 0.22f, 0.19f), Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, position + Vector3.up * 0.1f, new Vector3(0.3f, 0.04f, 0.3f), Quaternion.identity, new Color(0.17f, 0.22f, 0.19f), Surface.Metal);
                Glow(PrimitiveType.Cylinder, position - Vector3.up * 0.02f, new Vector3(0.42f, 0.02f, 0.42f), Quaternion.identity, Lamp);
            }
            var go = new GameObject("Lamp Light");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = position + Vector3.down * (chandelier ? 1.9f : lantern ? 0.45f : 0.3f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = LampRange;
            light.intensity = LampIntensity;
            light.color = LampTint(zone);
            light.shadows = LightShadows.None;
        }

        void BuildDoors(List<Buyable> buyables)
        {
            Doors = new Door[DoorLinks.Length];
            for (int i = 0; i < DoorLinks.Length; i++)
            {
                Vector3 c = DoorCenter(i);
                // A door in a wall running north-south faces along x.
                var rot = DoorInWallAlongZ(i) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                var go = Greybox.Box("Door " + ZoneNames[DoorLinks[i].x] + " - " + ZoneNames[DoorLinks[i].y], Root,
                    c + Vector3.up * (GapHeight / 2f), new Vector3(GapWidth, GapHeight, 0.35f), DoorIron, surface: Surface.Metal, moving: true);
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
                var (face, inward) = WallBuySpots[i].Resolve();
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
                var (_, inward) = GeneratorSpots[i].Resolve();
                var go = Greybox.Empty("Generator " + ZoneNames[i], Root, GeneratorPosition(i)).gameObject;
                // The gauge side faces into the zone.
                go.transform.localRotation = Quaternion.Euler(0f, Quaternion.LookRotation(-inward, Vector3.up).eulerAngles.y, 0f);
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
                Greybox.Box("Mast", go.transform, new Vector3(-0.5f, 1.4f, 0.2f), new Vector3(0.1f, 1.6f, 0.1f), Metal, collider: false, surface: Surface.Metal);
                Greybox.Prim(PrimitiveType.Sphere, "Dish", go.transform, new Vector3(-0.5f, 2.15f, 0.2f), new Vector3(0.5f, 0.5f, 0.15f), Metal, collider: false, surface: Surface.Metal);
                // A red tip light, so the beacon reads from across the map.
                Greybox.Prim(PrimitiveType.Sphere, "Tip", go.transform, new Vector3(-0.5f, 2.2f, 0.2f), Vector3.one * 0.08f, new Color(1f, 0.15f, 0.08f), collider: false, unlit: true);
            }
        }

        /// <summary>The two build stations: a long workbench with a place for each buildable.</summary>
        void BuildBenches()
        {
            var names = new[] { "SCRAP CANNON", "SCRAP SHIELD", "BUDDY BOT" };
            for (int b = 0; b < BenchCount; b++)
            {
                var root = Greybox.Empty("Build Station " + BenchNames[b], Root, BenchPositions[b]);
                Greybox.Box("Bench", root, new Vector3(0f, 0.47f, 0f), new Vector3(5.4f, 0.94f, 0.8f), new Color(0.4f, 0.3f, 0.18f), surface: Surface.Wood);
                Greybox.Box("Vice", root, new Vector3(-2.2f, 1.05f, 0.1f), new Vector3(0.3f, 0.2f, 0.3f), Metal, collider: false, surface: Surface.Metal);
                Greybox.Box("Pegboard", root, new Vector3(0f, 1.8f, 0.38f), new Vector3(5.4f, 1.4f, 0.05f), new Color(0.5f, 0.4f, 0.27f), collider: false, surface: Surface.Wood);
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
        static Vector3 StandPoint(WallSpot at)
        {
            var (face, inward) = at.Resolve();
            return face + inward * 1.4f + Vector3.up;
        }

        /// <summary>Perk machines, the armour station and the reroll shrine. Built before the bake so zombies walk round them.</summary>
        void BuildStations()
        {
            var perks = GameConfig.Perks;
            for (int i = 0; i < PerkSpots.Length; i++)
            {
                var def = perks != null ? perks.At(i) : null;
                BuildVendingMachine(PerkSpots[i].Resolve(), def != null ? def.name : "Perk",
                    def != null ? def.color : Color.white, def != null ? def.price.ToString("N0") : "");
            }
            BuildVendingMachine(ArmorSpot.Resolve(), "Armour Plates", new Color(0.6f, 0.65f, 0.72f), "");
            BuildShrine(RerollSpot.Resolve());
        }

        void BuildVendingMachine((Vector3 face, Vector3 inward) spot, string title, Color color, string price)
        {
            var root = Greybox.Empty("Machine " + title, Root, spot.face + spot.inward * 0.45f);
            root.localRotation = Quaternion.LookRotation(spot.inward, Vector3.up);
            Greybox.Box("Body", root, new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 2.1f, 0.8f), new Color(0.12f, 0.12f, 0.14f), surface: Surface.Metal);
            Greybox.Box("Top", root, new Vector3(0f, 2.16f, 0f), new Vector3(1.2f, 0.12f, 0.9f), color * 0.6f, collider: false, surface: Surface.Metal);
            // Front: a glowing panel in the perk's colour, with bottles behind glass.
            Greybox.Prim(PrimitiveType.Cube, "Panel", root, new Vector3(0f, 1.45f, 0.41f), new Vector3(0.9f, 0.9f, 0.02f), color * 0.85f, collider: false, unlit: true);
            for (int b = 0; b < 3; b++)
                Greybox.Prim(PrimitiveType.Cylinder, "Bottle", root, new Vector3(-0.25f + b * 0.25f, 0.62f, 0.32f), new Vector3(0.1f, 0.14f, 0.1f), color, collider: false, unlit: true);
            Greybox.Box("Slot", root, new Vector3(0f, 0.35f, 0.41f), new Vector3(0.6f, 0.12f, 0.02f), new Color(0.05f, 0.05f, 0.05f), collider: false, surface: Surface.Metal);
            // A glowing marquee strip over the panel and a kick plate, like a real machine.
            Greybox.Prim(PrimitiveType.Cube, "Marquee", root, new Vector3(0f, 2.0f, 0.41f), new Vector3(0.9f, 0.1f, 0.02f), Color.Lerp(color, Color.white, 0.4f), collider: false, unlit: true);
            Greybox.Box("Kick", root, new Vector3(0f, 0.08f, 0.41f), new Vector3(1.1f, 0.16f, 0.02f), new Color(0.3f, 0.3f, 0.3f), collider: false, surface: Surface.Rust);
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
            Greybox.Box("Plinth", root, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.8f), new Color(0.4f, 0.38f, 0.34f), surface: Surface.Concrete);
            // Candles round the cards, an old roadside-shrine look.
            foreach (var c in new[] { new Vector3(-0.38f, 1.08f, -0.25f), new Vector3(0.38f, 1.08f, -0.2f), new Vector3(0.3f, 1.08f, 0.28f) })
            {
                Greybox.Prim(PrimitiveType.Cylinder, "Candle", root, c, new Vector3(0.06f, 0.08f, 0.06f), new Color(0.75f, 0.7f, 0.58f), collider: false, surface: Surface.Plaster);
                Greybox.Prim(PrimitiveType.Sphere, "Flame", root, c + Vector3.up * 0.11f, new Vector3(0.035f, 0.06f, 0.035f), new Color(1f, 0.6f, 0.2f), collider: false, unlit: true);
            }
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
            Greybox.Box("Counter", shop, new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 3f), new Color(0.36f, 0.26f, 0.16f), surface: Surface.Wood);
            Greybox.Box("CounterTop", shop, new Vector3(-0.05f, 1.13f, 0f), new Vector3(1.2f, 0.06f, 3.2f), new Color(0.46f, 0.36f, 0.24f), collider: false, surface: Surface.Wood);
            foreach (float z in new[] { -1.5f, 1.5f })
                Greybox.Box("Post", shop, new Vector3(0.4f, 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f), new Color(0.3f, 0.22f, 0.15f), surface: Surface.Wood);
            var roof = Greybox.Box("Awning", shop, new Vector3(-0.2f, 2.85f, 0f), new Vector3(1.8f, 0.08f, 3.4f), awning, collider: false, surface: Surface.Fabric);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            Greybox.Prim(PrimitiveType.Cube, "Sign", shop, new Vector3(-0.56f, 2.35f, 0f), new Vector3(0.02f, 0.35f, 1.6f), new Color(0.95f, 0.8f, 0.35f), collider: false, unlit: true);
            // A few greybox "attachments" on the counter.
            Greybox.Box("Scope", shop, new Vector3(-0.1f, 1.22f, -0.8f), new Vector3(0.12f, 0.12f, 0.4f), new Color(0.15f, 0.15f, 0.15f), collider: false, surface: Surface.Metal);
            Greybox.Box("Mag", shop, new Vector3(-0.1f, 1.25f, 0f), new Vector3(0.1f, 0.2f, 0.12f), new Color(0.2f, 0.2f, 0.22f), collider: false, surface: Surface.Metal);
            Greybox.Box("Grip", shop, new Vector3(-0.1f, 1.22f, 0.7f), new Vector3(0.1f, 0.14f, 0.1f), new Color(0.25f, 0.2f, 0.15f), collider: false, surface: Surface.Rubber);

            // The Trader: a hooded scavenger behind the counter, with a lantern.
            var trader = Greybox.Empty("Trader", shop, new Vector3(0.95f, 0f, 0.4f));
            trader.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var coat = new Color(0.28f, 0.24f, 0.2f);
            Greybox.Prim(PrimitiveType.Capsule, "Body", trader, new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 0.9f, 0.45f), coat, collider: false, surface: Surface.Fabric);
            Greybox.Prim(PrimitiveType.Sphere, "Head", trader, new Vector3(0f, 1.72f, 0.02f), Vector3.one * 0.3f, new Color(0.62f, 0.48f, 0.38f), collider: false, surface: Surface.Skin);
            Greybox.Prim(PrimitiveType.Sphere, "Hood", trader, new Vector3(0f, 1.78f, -0.04f), new Vector3(0.38f, 0.36f, 0.4f), new Color(0.2f, 0.17f, 0.14f), collider: false, surface: Surface.Fabric);
            Greybox.Prim(PrimitiveType.Cylinder, "Lantern", trader, new Vector3(0.32f, 1.25f, 0.1f), new Vector3(0.1f, 0.1f, 0.1f), new Color(1f, 0.75f, 0.35f), collider: false, unlit: true);
            // The lantern's cage and handle.
            Greybox.Prim(PrimitiveType.Cylinder, "Lantern Cap", trader, new Vector3(0.32f, 1.36f, 0.1f), new Vector3(0.13f, 0.015f, 0.13f), new Color(0.15f, 0.14f, 0.12f), collider: false, surface: Surface.Metal);
            Greybox.Prim(PrimitiveType.Cylinder, "Lantern Base", trader, new Vector3(0.32f, 1.14f, 0.1f), new Vector3(0.13f, 0.015f, 0.13f), new Color(0.15f, 0.14f, 0.12f), collider: false, surface: Surface.Metal);
            Greybox.Box("Lantern Handle", trader, new Vector3(0.32f, 1.42f, 0.1f), new Vector3(0.015f, 0.1f, 0.015f), new Color(0.15f, 0.14f, 0.12f), collider: false, surface: Surface.Metal);
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
            var body = Greybox.Box("Body", machine, new Vector3(0f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 1.4f), new Color(0.14f, 0.13f, 0.17f), surface: Surface.Metal);
            // Built after the bake like the doors, so it carves its own hole in the NavMesh.
            var obstacle = body.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            Greybox.Box("Top", machine, new Vector3(0f, 2.35f, 0f), new Vector3(2.6f, 0.3f, 1.6f), new Color(0.22f, 0.2f, 0.26f), surface: Surface.Metal);
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
            // The gun itself hangs in the outline, side on, sized to fill it.
            if (Weapons.GunArt.Extent(gun, out float gunLength, out var middle, out float halfWidth))
            {
                float scale = Mathf.Clamp(1.25f / gunLength, 0.9f, 1.6f);
                var hung = Weapons.GunArt.BuildProp(gun, panel).transform;
                hung.localScale = Vector3.one * scale;
                // Barrel along the panel's x, its side to the room, clear of the wall.
                hung.localRotation = Quaternion.Euler(0f, 90f, 0f);
                hung.localPosition = new Vector3(-middle.z * scale, -middle.y * scale + 0.02f, -(halfWidth + 0.01f) * scale);
                Greybox.DisableShadows(hung.gameObject);
                return;
            }
            float length = gun != null ? Mathf.Clamp(gun.modelLength, 0.4f, 1.5f) : 1f;
            var accent = gun != null ? gun.accent : Cover;
            Greybox.Box("Gun", panel, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f * length, 0.12f, 0.04f), new Color(0.16f, 0.17f, 0.18f), collider: false, surface: Surface.Metal);
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
