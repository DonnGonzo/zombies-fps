using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.World
{
    /// <summary>Where each of the easter egg's things is (BuyableKind.Egg's Index).</summary>
    public enum EggSpot
    {
        Relic0, Relic1, Relic2, Relic3,
        /// <summary>The monks' altar in the Archives: relics go round it, the spark on it.</summary>
        Altar,
        /// <summary>Sergeant Brannock in the Crane Yard.</summary>
        Watchman,
        /// <summary>The binding stones in the Greenhouse: the spark goes in, later the way down opens.</summary>
        Stones,
        /// <summary>The blood altar in the Sanctum.</summary>
        BloodAltar,
        /// <summary>The two choices once Azhrakel has spoken.</summary>
        Fight, Serve,
        /// <summary>The ways back up from the Sanctum and from the Hollow Keep.</summary>
        SanctumExit, KeepExit,
    }

    /// <summary>
    /// The easter egg's places on the map, built with everything else (solid parts before the NavMesh bake): the
    /// monks' altar and its four pedestals in the Archives, the four relic spots in the Chapel and the Sunken Temple,
    /// Sergeant Brannock's post in the Crane Yard, the binding stones under the Greenhouse, and two rooms off the
    /// map: the Blood Sanctum, where Azhrakel's altar stands, and the Hollow Keep where Ashgrave hid. They are far
    /// out past the skyline, closed in, and only reached through the egg's portals. EasterEgg shows and hides the
    /// living parts (relics, the spark, the figures, the portals) on every machine.
    /// </summary>
    public partial class ZoneMap
    {
        static readonly Color AltarStone = new Color(0.42f, 0.4f, 0.37f);
        static readonly Color OldGold = new Color(0.62f, 0.47f, 0.18f);
        static readonly Color Blood = new Color(0.32f, 0.03f, 0.03f);
        static readonly Color Basalt = new Color(0.12f, 0.1f, 0.1f);
        public static readonly Color ManaColor = new Color(0.45f, 0.7f, 1f);
        public static readonly Color BloodGlow = new Color(1f, 0.18f, 0.08f);

        // ---------------------------------------------------------------- places

        /// <summary>The monks' altar, on top of the dais in the middle of the Archives' reading room.</summary>
        public static Vector3 ArchivesAltar => L(EasterEggData.ArchivesZone, DaisX, DaisHeight, DaisZ);
        public const float AltarHeight = 1.0f;
        const float PedestalRadius = 2.6f;
        public const float PedestalHeight = 1.05f;

        /// <summary>The four pedestals round the Archives altar, on its diagonals.</summary>
        public static Vector3 Pedestal(int i)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            return ArchivesAltar + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PedestalRadius;
        }

        // Built on first use, not as a static field: initialisers in different partial files run in an
        // order the compiler picks, and in player builds this one ran before ZoneAnchors was set.
        /// <summary>Where the relics lie: the Chapel's chancel and west aisle, the Temple's sanctuary and east wing.</summary>
        static Vector3[] s_RelicSpots;
        static Vector3[] RelicSpots => s_RelicSpots ??= new[]
        {
            L(EasterEggData.ChapelZone, 5.6f, 0.5f, 19.8f), L(EasterEggData.ChapelZone, -16f, 0f, 2f),
            L(EasterEggData.TempleZone, -9.5f, 0.5f, 31f), L(EasterEggData.TempleZone, 15f, 0f, 27f),
        };

        public static Vector3 RelicSpot(int relic) => RelicSpots[Mathf.Clamp(relic, 0, RelicSpots.Length - 1)];

        public static Vector3 WatchmanSpot => L(EasterEggData.CraneYardZone, -4f, 0f, -1f);
        public static Vector3 StonesCenter => L(EasterEggData.GreenhouseZone, 1.5f, 0f, 3.5f);
        const float StonesRadius = 3.4f;

        /// <summary>The Blood Sanctum: a 32 m square hall far to the south, past the skyline.</summary>
        public static readonly Vector3 SanctumCenter = new Vector3(0f, 0f, -300f);
        public const float SanctumHalf = 16f;
        public static Vector3 BloodAltar => SanctumCenter + new Vector3(0f, 0f, 4f);
        public static Vector3 GodSpot => SanctumCenter + new Vector3(0f, 0f, 11f);
        public static Vector3 FightSpot => SanctumCenter + new Vector3(-6.5f, 0f, 0f);
        public static Vector3 ServeSpot => SanctumCenter + new Vector3(6.5f, 0f, 0f);
        public static Vector3 SanctumArrival => SanctumCenter + new Vector3(0f, 0.05f, -10f);
        public static Vector3 SanctumExitSpot => SanctumCenter + new Vector3(0f, 0f, -14.6f);

        /// <summary>The Hollow Keep: Ashgrave's long throne hall, beside the Sanctum.</summary>
        public static readonly Vector3 KeepCenter = new Vector3(100f, 0f, -300f);
        public const float KeepHalfX = 12f, KeepHalfZ = 18f;
        public static Vector3 ThroneSpot => KeepCenter + new Vector3(0f, 0.6f, 14.5f);
        public static Vector3 KeepArrival => KeepCenter + new Vector3(0f, 0.05f, -13f);
        public static Vector3 KeepExitSpot => KeepCenter + new Vector3(0f, 0f, -16.6f);
        const float ArenaHeight = 10f;

        /// <summary>Anything south of this is in one of the egg's rooms off the map.</summary>
        public const float ArenaLine = -200f;
        public static bool InArena(Vector3 p) => p.z < ArenaLine;

        /// <summary>The floor of the Sanctum or the Keep (x, y here are world x, z), for spawning in.</summary>
        public static RectInt SanctumRect => new RectInt((int)(SanctumCenter.x - SanctumHalf), (int)(SanctumCenter.z - SanctumHalf), (int)(SanctumHalf * 2), (int)(SanctumHalf * 2));
        public static RectInt KeepRect => new RectInt((int)(KeepCenter.x - KeepHalfX), (int)(KeepCenter.z - KeepHalfZ), (int)(KeepHalfX * 2), (int)(KeepHalfZ * 2));

        /// <summary>Where a returning player comes back out: beside the binding stones, spread by player slot.</summary>
        public static Vector3 ReturnSpot(int slot) => StonesCenter + new Vector3(-5f + (slot % 4) * 1.4f, 0.05f, -5f);

        public static Vector3 EggSpotPosition(EggSpot s) => s switch
        {
            EggSpot.Relic0 or EggSpot.Relic1 or EggSpot.Relic2 or EggSpot.Relic3 => RelicSpot((int)s),
            EggSpot.Altar => ArchivesAltar,
            EggSpot.Watchman => WatchmanSpot,
            EggSpot.Stones => StonesCenter,
            EggSpot.BloodAltar => BloodAltar,
            EggSpot.Fight => FightSpot,
            EggSpot.Serve => ServeSpot,
            EggSpot.SanctumExit => SanctumExitSpot,
            _ => KeepExitSpot,
        };

        static float EggSpotRange(EggSpot s) => s switch
        {
            EggSpot.Altar => PedestalRadius + 1.2f,
            EggSpot.Stones => StonesRadius + 0.6f,
            EggSpot.BloodAltar => 3f,
            EggSpot.Watchman => 2.4f,
            _ => 1.8f,
        };

        /// <summary>Every egg spot as a buyable (after the bake; EasterEgg decides what each does right now).</summary>
        void AddEggBuyables(List<Buyable> buyables)
        {
            for (var s = EggSpot.Relic0; s <= EggSpot.KeepExit; s++)
                buyables.Add(new Buyable { Kind = BuyableKind.Egg, Index = (int)s, Position = EggSpotPosition(s) + Vector3.up, Range = EggSpotRange(s) });
        }

        void KeepEggClear()
        {
            _keepOut.Add((ArchivesAltar, PedestalRadius + 1.4f));
            _keepOut.Add((StonesCenter, StonesRadius + 1.4f));
            _keepOut.Add((WatchmanSpot, 3f));
            foreach (var p in RelicSpots) _keepOut.Add((p, 1.4f));
        }

        // ---------------------------------------------------------------- on the map

        /// <summary>The solid parts on the map, in before the room furniture so it keeps clear of them.</summary>
        void DressEgg()
        {
            KeepEggClear();
            MonksAltar();
            RelicPlinths();
            WatchPost();
            BindingStones();
            Physics.SyncTransforms();
        }

        void MonksAltar()
        {
            var a = ArchivesAltar;
            Greybox.Box("Monks' Altar", Root, a + Vector3.up * (AltarHeight / 2f), new Vector3(1.8f, AltarHeight, 1.1f), AltarStone, surface: Surface.Concrete);
            Deco(a + Vector3.up * (AltarHeight + 0.03f), new Vector3(1.95f, 0.06f, 1.25f), Shade(AltarStone, 1.15f), Surface.Concrete);
            Deco(a + Vector3.up * 0.08f, new Vector3(2.2f, 0.16f, 1.5f), Shade(AltarStone, 0.8f), Surface.Concrete);
            // A gold-trimmed cloth over it, and the spark's cradle in the middle.
            Deco(a + Vector3.up * (AltarHeight + 0.065f), new Vector3(0.7f, 0.01f, 1.27f), new Color(0.3f, 0.08f, 0.1f), Surface.Fabric);
            Deco(a + Vector3.up * (AltarHeight + 0.07f), new Vector3(0.74f, 0.012f, 0.05f), OldGold, Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, a + Vector3.up * (AltarHeight + 0.12f), new Vector3(0.36f, 0.05f, 0.36f), Quaternion.identity, OldGold, Surface.Metal);
            // The circle the pedestals stand on, worn into the floor.
            for (int k = 0; k < 24; k++)
            {
                float ang = k * 15f * Mathf.Deg2Rad;
                var p = a + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * PedestalRadius;
                Deco(new Vector3(p.x, a.y + 0.006f, p.z), new Vector3(0.12f, 0.01f, 0.72f), Quaternion.Euler(0f, -k * 15f, 0f), Shade(OldGold, 0.6f), Surface.Metal);
            }
            for (int i = 0; i < EasterEggData.RelicCount; i++)
            {
                var p = Pedestal(i);
                Greybox.Prim(PrimitiveType.Cylinder, "Relic Pedestal", Root, p + Vector3.up * (PedestalHeight / 2f), new Vector3(0.5f, PedestalHeight / 2f, 0.5f), AltarStone, surface: Surface.Concrete);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * PedestalHeight, new Vector3(0.66f, 0.04f, 0.66f), Quaternion.identity, Shade(AltarStone, 1.2f), Surface.Concrete);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.06f, new Vector3(0.7f, 0.06f, 0.7f), Quaternion.identity, Shade(AltarStone, 0.8f), Surface.Concrete);
                // Its sign, cut into the stone facing the altar.
                Glow(PrimitiveType.Cube, p + Vector3.up * 0.6f + (a - p).normalized * 0.255f, new Vector3(0.14f, 0.14f, 0.01f),
                    Quaternion.LookRotation((a - p).normalized) * Quaternion.Euler(0f, 0f, 45f), Shade(OldGold, 0.8f));
            }
            // Candles round the altar's foot.
            var rng = new System.Random(7300);
            for (int k = 0; k < 9; k++)
            {
                float ang = Range(rng, 0f, Mathf.PI * 2f), d = Range(rng, 1.25f, 1.7f);
                var p = a + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d * 0.8f);
                float h = Range(rng, 0.12f, 0.3f);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * (h / 2f), new Vector3(0.06f, h / 2f, 0.06f), Quaternion.identity, new Color(0.85f, 0.8f, 0.68f), Surface.Plaster);
                Glow(PrimitiveType.Sphere, p + Vector3.up * (h + 0.03f), new Vector3(0.03f, 0.05f, 0.03f), Quaternion.identity, new Color(1f, 0.75f, 0.35f));
            }
        }

        /// <summary>A little stone plinth for each relic to rest on.</summary>
        void RelicPlinths()
        {
            foreach (var p in RelicSpots)
            {
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.2f, new Vector3(0.45f, 0.2f, 0.45f), Quaternion.identity, AltarStone, Surface.Concrete);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.41f, new Vector3(0.55f, 0.02f, 0.55f), Quaternion.identity, Shade(OldGold, 0.7f), Surface.Metal);
            }
        }

        /// <summary>Brannock's post: sandbags, a crate for a seat, a lantern and an old Watch banner.</summary>
        void WatchPost()
        {
            var p = WatchmanSpot;
            var bags = new Color(0.42f, 0.37f, 0.26f);
            for (int k = 0; k < 5; k++)
            {
                float ang = (200f + k * 28f) * Mathf.Deg2Rad;
                var at = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.2f;
                Deco(at + Vector3.up * 0.25f, new Vector3(1.1f, 0.5f, 0.5f), Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f), bags, Surface.Fabric);
                Deco(at + Vector3.up * 0.7f, new Vector3(1f, 0.4f, 0.45f), Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 95f, 0f), Shade(bags, 0.9f), Surface.Fabric);
            }
            Deco(p + new Vector3(1.4f, 0.3f, 0.9f), new Vector3(0.6f, 0.6f, 0.6f), Wood, Surface.Wood);
            Glow(PrimitiveType.Sphere, p + new Vector3(1.4f, 0.75f, 0.9f), new Vector3(0.14f, 0.2f, 0.14f), Quaternion.identity, new Color(1f, 0.7f, 0.3f));
            var light = new GameObject("Watch Lantern").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = p + new Vector3(1.4f, 1.2f, 0.9f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.35f);
            light.range = 7f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            // The banner: the Watch's lantern sign on faded blue, on a pole stuck in the sandbags.
            var pole = p + new Vector3(-1.6f, 0f, -1.3f);
            Rod(pole, pole + Vector3.up * 3.2f, 0.06f, Girder, Surface.Metal);
            Deco(pole + new Vector3(0.45f, 2.6f, 0f), new Vector3(0.9f, 1.1f, 0.02f), new Color(0.12f, 0.16f, 0.3f), Surface.Fabric);
            Glow(PrimitiveType.Cube, pole + new Vector3(0.45f, 2.65f, -0.015f), new Vector3(0.22f, 0.32f, 0.01f), Quaternion.identity, Shade(OldGold, 0.9f));
        }

        /// <summary>The Watch's binding stones under the Greenhouse: six standing stones round a low centre stone.</summary>
        void BindingStones()
        {
            var c = StonesCenter;
            Greybox.Prim(PrimitiveType.Cylinder, "Binding Stone", Root, c + Vector3.up * 0.3f, new Vector3(1.3f, 0.3f, 1.3f), Shade(AltarStone, 0.8f), surface: Surface.Concrete);
            DecoPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.61f, new Vector3(0.5f, 0.02f, 0.5f), Quaternion.identity, Shade(OldGold, 0.6f), Surface.Metal);
            var rng = new System.Random(7400);
            for (int k = 0; k < 6; k++)
            {
                float ang = (k * 60f + 30f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * StonesRadius;
                float h = Range(rng, 1.5f, 2.1f);
                var rot = Quaternion.Euler(Range(rng, -4f, 4f), -ang * Mathf.Rad2Deg, Range(rng, -5f, 5f));
                var stone = Greybox.Box("Standing Stone", Root, p + Vector3.up * (h / 2f), new Vector3(0.6f, h, 0.9f), Shade(AltarStone, Range(rng, 0.75f, 0.95f)), surface: Surface.Concrete);
                stone.transform.localRotation = rot;
                // Moss up the sides and a carved rune facing in.
                Deco(p + Vector3.up * 0.25f, new Vector3(0.64f, 0.5f, 0.94f), rot, new Color(0.2f, 0.3f, 0.14f), Surface.Grass);
                Glow(PrimitiveType.Cube, p + Vector3.up * (h * 0.6f) + (c - p).normalized * 0.31f, new Vector3(0.18f, 0.3f, 0.01f),
                    Quaternion.LookRotation((c - p).normalized), Shade(OldGold, 0.7f));
            }
            // Roots and old blood under the soil.
            for (int k = 0; k < 7; k++)
            {
                float ang = Range(rng, 0f, Mathf.PI * 2f), d = Range(rng, 0.8f, 3f);
                Splat(c + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d), Range(rng, 0.6f, 1.4f), Range(rng, 0.4f, 0.9f), Range(rng, 0f, 180f), 0.005f,
                    k % 2 == 0 ? new Color(0.18f, 0.14f, 0.08f) : Shade(Blood, 0.7f), Surface.Dirt);
            }
        }

        // ---------------------------------------------------------------- off the map

        /// <summary>The Sanctum and the Keep, solid and lit, in before the NavMesh bake so zombies can walk them.</summary>
        void BuildEggRooms()
        {
            Sanctum();
            HollowKeep();
        }

        /// <summary>A closed hall: floor tiles, four walls and a ceiling, all solid.</summary>
        void Hall(string name, Vector3 center, float halfX, float halfZ, Color floor, Surface floorSurface, Color wall, Surface wallSurface)
        {
            var box = Greybox.Box(name + " Floor", Root, center + Vector3.down * 0.5f, new Vector3(halfX * 2f + 1.2f, 1f, halfZ * 2f + 1.2f), floor, surface: floorSurface);
            box.GetComponent<Renderer>().enabled = false;
            for (float x = -halfX; x < halfX - 0.01f; x += FloorTile)
                for (float z = -halfZ; z < halfZ - 0.01f; z += FloorTile)
                {
                    float w = Mathf.Min(FloorTile, halfX - x), d = Mathf.Min(FloorTile, halfZ - z);
                    Deco(center + new Vector3(x + w / 2f, -0.5f, z + d / 2f), new Vector3(w, 1f, d), floor, floorSurface);
                }
            float t = WallThickness;
            Greybox.Box(name + " Wall", Root, center + new Vector3(0f, ArenaHeight / 2f, halfZ + t / 2f), new Vector3(halfX * 2f + t * 2f, ArenaHeight, t), wall, surface: wallSurface);
            Greybox.Box(name + " Wall", Root, center + new Vector3(0f, ArenaHeight / 2f, -halfZ - t / 2f), new Vector3(halfX * 2f + t * 2f, ArenaHeight, t), wall, surface: wallSurface);
            Greybox.Box(name + " Wall", Root, center + new Vector3(halfX + t / 2f, ArenaHeight / 2f, 0f), new Vector3(t, ArenaHeight, halfZ * 2f), wall, surface: wallSurface);
            Greybox.Box(name + " Wall", Root, center + new Vector3(-halfX - t / 2f, ArenaHeight / 2f, 0f), new Vector3(t, ArenaHeight, halfZ * 2f), wall, surface: wallSurface);
            Greybox.Box(name + " Ceiling", Root, center + new Vector3(0f, ArenaHeight + t / 2f, 0f), new Vector3(halfX * 2f + t * 2f, t, halfZ * 2f + t * 2f), Shade(wall, 0.6f), surface: wallSurface);
            // Skirting and a cornice so the corners read.
            foreach (int s in new[] { -1, 1 })
            {
                Deco(center + new Vector3(0f, 0.2f, s * (halfZ - 0.06f)), new Vector3(halfX * 2f, 0.4f, 0.12f), Shade(wall, 0.6f), wallSurface);
                Deco(center + new Vector3(s * (halfX - 0.06f), 0.2f, 0f), new Vector3(0.12f, 0.4f, halfZ * 2f), Shade(wall, 0.6f), wallSurface);
                Deco(center + new Vector3(0f, ArenaHeight - 0.3f, s * (halfZ - 0.1f)), new Vector3(halfX * 2f, 0.6f, 0.2f), Shade(wall, 0.7f), wallSurface);
                Deco(center + new Vector3(s * (halfX - 0.1f), ArenaHeight - 0.3f, 0f), new Vector3(0.2f, 0.6f, halfZ * 2f), Shade(wall, 0.7f), wallSurface);
            }
        }

        Light HallLight(string name, Vector3 at, Color color, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(Root, false);
            l.transform.localPosition = at;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>A stone bowl of fire on a pillar.</summary>
        void Brazier(Vector3 p, Color fire)
        {
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.6f, new Vector3(0.35f, 0.6f, 0.35f), Quaternion.identity, Basalt, Surface.Concrete);
            DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 1.3f, new Vector3(0.9f, 0.35f, 0.9f), Quaternion.identity, Shade(Basalt, 1.4f), Surface.Metal);
            Glow(PrimitiveType.Sphere, p + Vector3.up * 1.5f, new Vector3(0.6f, 0.45f, 0.6f), Quaternion.identity, fire);
            Glow(PrimitiveType.Sphere, p + Vector3.up * 1.75f, new Vector3(0.3f, 0.4f, 0.3f), Quaternion.identity, Color.Lerp(fire, Color.white, 0.4f));
            HallLight("Brazier", p + Vector3.up * 2.2f, fire, 11f, 1.8f);
        }

        void Sanctum()
        {
            var c = SanctumCenter;
            Hall("Blood Sanctum", c, SanctumHalf, SanctumHalf, new Color(0.2f, 0.16f, 0.15f), Surface.Tile, new Color(0.24f, 0.16f, 0.14f), Surface.Brick);
            var rng = new System.Random(7500);
            // A ring of columns round the hall.
            for (int k = 0; k < 10; k++)
            {
                float ang = (k * 36f + 18f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 11.5f;
                if (p.z < c.z - 9f && Mathf.Abs(p.x - c.x) < 4f) continue;
                Greybox.Prim(PrimitiveType.Cylinder, "Sanctum Column", Root, p + Vector3.up * (ArenaHeight / 2f), new Vector3(1.3f, ArenaHeight / 2f, 1.3f), Shade(Basalt, 1.6f), surface: Surface.Concrete);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.25f, new Vector3(1.7f, 0.25f, 1.7f), Quaternion.identity, Basalt, Surface.Concrete);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * (ArenaHeight - 0.4f), new Vector3(1.7f, 0.3f, 1.7f), Quaternion.identity, Basalt, Surface.Concrete);
                // Chains hanging between them.
                Rod(p + Vector3.up * 7.5f, c + (p - c) * 0.6f + Vector3.up * 8.6f, 0.07f, new Color(0.2f, 0.18f, 0.17f), Surface.Metal);
            }
            // The blood altar, on a dais, with channels of blood running out from it.
            var a = BloodAltar;
            Greybox.Box("Blood Dais", Root, a + Vector3.up * 0.15f, new Vector3(6f, 0.3f, 5f), Basalt, surface: Surface.Concrete);
            Greybox.Box("Blood Altar", Root, a + Vector3.up * 0.8f, new Vector3(2.2f, 1f, 1.3f), Shade(Basalt, 1.3f), surface: Surface.Concrete);
            Deco(a + Vector3.up * 1.33f, new Vector3(2.4f, 0.06f, 1.5f), Shade(Basalt, 1.7f), Surface.Concrete);
            Glow(PrimitiveType.Cube, a + Vector3.up * 1.365f, new Vector3(1.9f, 0.01f, 1.0f), Quaternion.identity, Shade(BloodGlow, 0.5f));
            for (int k = 0; k < 8; k++)
            {
                float ang = k * 45f;
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                Glow(PrimitiveType.Cube, c + dir * 7f + Vector3.up * 0.006f + (a - c), new Vector3(0.18f, 0.01f, 8f), Quaternion.Euler(0f, ang, 0f), Shade(BloodGlow, 0.35f));
            }
            for (int k = 0; k < 14; k++)
            {
                float ang = Range(rng, 0f, Mathf.PI * 2f), d = Range(rng, 3f, 13f);
                Splat(c + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d), Range(rng, 0.8f, 2.2f), Range(rng, 0.6f, 1.6f), Range(rng, 0f, 180f), 0.004f, Blood, Surface.Metal);
            }
            // Where the god rises: a pit of embers behind the altar.
            var g = GodSpot;
            DecoPrim(PrimitiveType.Cylinder, g + Vector3.up * 0.02f, new Vector3(6f, 0.02f, 4f), Quaternion.identity, Basalt, Surface.Concrete);
            Glow(PrimitiveType.Cylinder, g + Vector3.up * 0.03f, new Vector3(5f, 0.01f, 3.2f), Quaternion.identity, Shade(BloodGlow, 0.7f));
            // The two choice stones, either side: a blade to fight, a bowed figure to serve.
            ChoiceStone(FightSpot, "FIGHT");
            ChoiceStone(ServeSpot, "SERVE");
            // The way back up: a doorway of light on the south wall.
            ExitArch(SanctumExitSpot, Vector3.forward);
            foreach (var x in new[] { -13f, 13f })
                foreach (var z in new[] { -13f, 13f })
                    Brazier(c + new Vector3(x, 0f, z), new Color(1f, 0.35f, 0.12f));
            HallLight("Sanctum Glow", c + new Vector3(0f, 7f, 3f), new Color(0.9f, 0.25f, 0.15f), 26f, 1.6f);
            HallLight("Sanctum Fill", c + new Vector3(0f, 6f, -8f), new Color(0.75f, 0.45f, 0.4f), 20f, 0.9f);
        }

        void ChoiceStone(Vector3 p, string word)
        {
            Greybox.Box("Choice Stone", Root, p + Vector3.up * 1.1f, new Vector3(0.9f, 2.2f, 0.9f), Shade(Basalt, 1.5f), surface: Surface.Concrete);
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.05f, new Vector3(1.6f, 0.05f, 1.6f), Quaternion.identity, Basalt, Surface.Concrete);
            var toCenter = (SanctumArrival - p);
            toCenter.y = 0f;
            var label = Greybox.Label("Choice", Root, p + Vector3.up * 2.6f, word, new Color(1f, 0.55f, 0.4f), 0.5f);
            label.transform.rotation = Quaternion.LookRotation(-toCenter.normalized);
        }

        void ExitArch(Vector3 p, Vector3 inward)
        {
            var side = Vector3.Cross(Vector3.up, inward);
            foreach (int s in new[] { -1, 1 })
                Deco(p + side * (s * 1.3f) + Vector3.up * 1.6f, new Vector3(0.4f, 3.2f, 0.4f), Quaternion.LookRotation(inward), Shade(Basalt, 1.4f), Surface.Concrete);
            Deco(p + Vector3.up * 3.35f, new Vector3(3f, 0.4f, 0.45f), Quaternion.LookRotation(inward), Shade(Basalt, 1.4f), Surface.Concrete);
            var label = Greybox.Label("Exit", Root, p + Vector3.up * 3.9f + inward * 0.3f, "BACK TO THE GREENHOUSE", new Color(0.8f, 0.9f, 1f), 0.3f);
            label.transform.rotation = Quaternion.LookRotation(-inward);
        }

        void HollowKeep()
        {
            var c = KeepCenter;
            Hall("Hollow Keep", c, KeepHalfX, KeepHalfZ, new Color(0.22f, 0.22f, 0.24f), Surface.Tile, new Color(0.26f, 0.26f, 0.29f), Surface.Brick);
            // Two rows of pillars, Watch banners hanging between them, torn and black.
            foreach (int s in new[] { -1, 1 })
                for (float z = -10f; z <= 10f; z += 5f)
                {
                    var p = c + new Vector3(s * 6.5f, 0f, z);
                    Greybox.Box("Keep Pillar", Root, p + Vector3.up * (ArenaHeight / 2f), new Vector3(1.1f, ArenaHeight, 1.1f), new Color(0.32f, 0.32f, 0.35f), surface: Surface.Concrete);
                    Deco(p + new Vector3(s * 2.2f, 6f, 0f), new Vector3(0.02f, 3.4f, 1.3f), new Color(0.07f, 0.08f, 0.14f), Surface.Fabric);
                    Glow(PrimitiveType.Cube, p + new Vector3(s * 2.18f, 6.6f, 0f), new Vector3(0.01f, 0.5f, 0.35f), Quaternion.identity, Shade(OldGold, 0.6f));
                }
            // The dais and the throne.
            var t = ThroneSpot;
            Greybox.Box("Throne Dais", Root, new Vector3(t.x, 0.3f, t.z), new Vector3(8f, 0.6f, 4.6f), new Color(0.2f, 0.2f, 0.22f), surface: Surface.Concrete);
            Greybox.Box("Throne", Root, t + new Vector3(0f, 0.55f, 0.6f), new Vector3(1.6f, 1.1f, 1.2f), new Color(0.16f, 0.15f, 0.17f), surface: Surface.Metal);
            Deco(t + new Vector3(0f, 1.9f, 1.15f), new Vector3(1.7f, 2.6f, 0.25f), new Color(0.16f, 0.15f, 0.17f), Surface.Metal);
            foreach (int s in new[] { -1, 1 })
            {
                Deco(t + new Vector3(s * 0.85f, 1.25f, 0.55f), new Vector3(0.18f, 0.3f, 1.1f), new Color(0.2f, 0.19f, 0.21f), Surface.Metal);
                Deco(t + new Vector3(s * 0.65f, 3.35f, 1.15f), new Vector3(0.12f, 0.6f, 0.12f), Shade(OldGold, 0.8f), Surface.Metal);
            }
            Deco(t + new Vector3(0f, 0.012f, -1.6f), new Vector3(2.4f, 0.01f, 9f), new Color(0.25f, 0.05f, 0.06f), Surface.Fabric);
            // A carpet up the hall, frozen torches, frost on the floor.
            Deco(c + new Vector3(0f, 0.008f, -2f), new Vector3(2.4f, 0.01f, 26f), new Color(0.25f, 0.05f, 0.06f), Surface.Fabric);
            var rng = new System.Random(7600);
            for (int k = 0; k < 10; k++)
                Splat(c + new Vector3(Range(rng, -10f, 10f), 0f, Range(rng, -15f, 15f)), Range(rng, 1f, 2.4f), Range(rng, 0.8f, 1.8f), Range(rng, 0f, 180f), 0.004f,
                    new Color(0.5f, 0.55f, 0.65f), Surface.Plaster);
            foreach (int s in new[] { -1, 1 })
                foreach (float z in new[] { -12f, 0f, 12f })
                    Brazier(c + new Vector3(s * (KeepHalfX - 1.2f), 0f, z), new Color(0.45f, 0.55f, 1f));
            ExitArch(KeepExitSpot, Vector3.forward);
            HallLight("Keep Glow", t + new Vector3(0f, 6f, -2f), new Color(0.6f, 0.65f, 1f), 20f, 1.4f);
            HallLight("Keep Fill", c + new Vector3(0f, 7f, -6f), new Color(0.55f, 0.55f, 0.65f), 24f, 0.9f);
        }
    }
}
