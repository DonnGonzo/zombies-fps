using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Greenhouse, an old iron-and-glass winter garden gone wild.
    ///
    /// The hall: a glass roof on arched cast-iron trusses, vines hanging from them, the night showing through the
    /// tall glazed panels high on the brick walls and through the panes that have fallen in. Raised brick beds fill
    /// the west half in two blocks with paths between: cabbages, staked beans, flowers, shrubs, a few fruit trees and
    /// one bed gone to weed. In the middle a glass dome on slender columns stands over the Watch's binding stones
    /// (ZoneMap.Egg.cs), a lantern chandelier hanging under its finial. Palms in big stone planters line the east
    /// walk between the doors, and a gallery runs the length of the north wall, up wooden stairs at its east end or
    /// a ladder: the second build station and the Greenhouse high spot are up there, potting benches under it.
    ///
    /// The pond garden (the south-west wing): a lawn round a lily pond with a fountain, reeds and willows.
    ///
    /// The whole room is dressed here (see <see cref="HandDressed"/>): it gets none of the generic room sets, wall
    /// clutter, wall lamps, ceiling lamps or roof, and uses Kenney's Retro Fantasy Kit (Resources/CC0/Kenney, made by
    /// tools/kenney/pack_kenney.py) for its trees, shrubs, iron railing, barrels, crates and pulleys.
    /// </summary>
    public partial class ZoneMap
    {
        const int GreenhouseZone = 11;
        const string KenneyKit = "Kenney/";

        /// <summary>The dome over the binding stones: its centre, the ring of columns, where the ribs spring and the finial.</summary>
        const float DomeX = 9f, DomeZ = -1f, DomeRadius = 6f, DomeSpring = 3.6f, DomeApex = 6.7f;
        const int DomeColumns = 8;
        /// <summary>The gallery along the north wall, GalleryTop up (ZoneMap.Layout.cs): stairs down east from its end, a ladder at the front.</summary>
        const float GalleryX0 = -22f, GalleryX1 = 0f, GalleryFront = 8.5f, GalleryBack = 14f, GalleryDeck = 0.3f, GalleryLadderX = -16.5f, GalleryStairZ = 12.45f;
        static readonly float[] GalleryPosts = { -20f, -13.5f, -7f, -0.4f };
        /// <summary>The raised beds: two blocks of four, BedTop high.</summary>
        const float BedTop = 0.6f, BedHalf = 1f;
        static readonly (float x0, float x1)[] BedColumns = { (-17f, -10.5f), (-7.5f, -1f) };
        static readonly float[] BedRows = { -10.5f, -5.5f, -0.5f, 4.5f };
        /// <summary>The palms' planters along the east walk.</summary>
        static readonly Vector2[] PalmSpots = { new Vector2(19.3f, -0.5f), new Vector2(19.3f, 11.3f), new Vector2(19.3f, -11.3f) };
        const float PalmPlanterTop = 0.7f, PalmPlanterRadius = 1.2f;
        /// <summary>The lily pond in the south-west wing, inside a low kerb you can step over.</summary>
        const float PondX0 = -17f, PondX1 = -7f, PondZ0 = -25f, PondZ1 = -18f, PondKerb = 0.3f, PondKerbTop = 0.2f, PondWater = 0.12f;
        static readonly Vector2[] PondTrees = { new Vector2(-19.8f, -15.8f), new Vector2(-19.8f, -26.2f), new Vector2(-4.3f, -20.5f) };
        /// <summary>The arched trusses: where they spring from the walls and how high their crowns reach.</summary>
        const float TrussSpring = 6f, TrussCrown = 6.9f;

        static readonly Color GreenhouseIron = new Color(0.15f, 0.2f, 0.18f);
        static readonly Color PaneGlass = new Color(0.55f, 0.78f, 0.74f, 0.16f);
        static readonly Color RoofGlass = new Color(0.32f, 0.45f, 0.45f, 0.3f);
        static readonly Color NightGlass = new Color(0.04f, 0.07f, 0.1f);
        static readonly Color BedBrick = new Color(0.45f, 0.27f, 0.19f);
        static readonly Color Soil = new Color(0.19f, 0.13f, 0.08f);
        static readonly Color Leaf = new Color(0.17f, 0.3f, 0.11f);
        static readonly Color LeafDark = new Color(0.1f, 0.19f, 0.08f);
        static readonly Color LeafPale = new Color(0.3f, 0.38f, 0.16f);
        static readonly Color Terracotta = new Color(0.6f, 0.32f, 0.19f);
        static readonly Color PlanterStone = new Color(0.46f, 0.44f, 0.4f);
        static readonly Color GalleryWood = new Color(0.33f, 0.23f, 0.14f);
        static readonly Color GreenhouseLampLight = new Color(1f, 0.76f, 0.48f);
        static readonly Color GreenhouseMoon = new Color(0.62f, 0.75f, 1f);

        /// <summary>A point in the Greenhouse, from its anchor.</summary>
        static Vector3 G(float x, float y, float z) => L(GreenhouseZone, x, y, z);

        static Vector3 DomeCenter => G(DomeX, 0f, DomeZ);

        static Vector3 DomeOut(int k) => Quaternion.Euler(0f, 22.5f + k * 360f / DomeColumns, 0f) * Vector3.forward;

        /// <summary>A point on a dome rib, t from where it springs (0) to the finial (1).</summary>
        static Vector3 DomeRib(Vector3 outward, float t)
        {
            float a = t * Mathf.PI / 2f;
            return DomeCenter + outward * (DomeRadius * Mathf.Cos(a)) + Vector3.up * (DomeSpring + (DomeApex - DomeSpring) * Mathf.Sin(a));
        }

        /// <summary>The span a truss at local x crosses: wall face to wall face, through the pond wing west of x = -2.</summary>
        static (float z0, float z1) TrussSpan(float x) => (x < -2f ? -27.7f : -13.7f, 13.7f);

        /// <summary>The height of a truss's arch at local z.</summary>
        static float TrussY(float z0, float z1, float z)
        {
            float u = Mathf.Clamp01((z - z0) / (z1 - z0));
            return TrussSpring + (TrussCrown - TrussSpring) * Mathf.Sin(u * Mathf.PI);
        }

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildGreenhouse()
        {
            const int z = GreenhouseZone;

            // The raised beds: low brick walls round the soil, cover from the floor and in the way of zombies.
            foreach (var (x0, x1) in BedColumns)
                foreach (float row in BedRows)
                    Block("Raised Bed", z, x0, row - BedHalf, x1, row + BedHalf, BedTop, BedBrick, Surface.Brick);

            // The gallery: a deck of boards on cast-iron posts, wooden stairs down its east end, a ladder at the front.
            Greybox.Box("Gallery", Root, G((GalleryX0 + GalleryX1) / 2f, GalleryTop - GalleryDeck / 2f, (GalleryFront + GalleryBack) / 2f),
                new Vector3(GalleryX1 - GalleryX0, GalleryDeck, GalleryBack - GalleryFront), GalleryWood, surface: Surface.Wood);
            foreach (float x in GalleryPosts)
                GreenhouseSolid("Gallery Post", new Vector3(x, (GalleryTop - GalleryDeck) / 2f, GalleryFront + 0.25f), new Vector3(0.3f, GalleryTop - GalleryDeck, 0.3f));
            Stairs(z, GalleryX1, GalleryStairZ, Vector3.right, GalleryTop, 2.4f);
            AddLadder(z, GalleryLadderX, GalleryFront, Vector3.back, GalleryTop);

            // The dome's columns.
            for (int k = 0; k < DomeColumns; k++)
            {
                var foot = DomeCenter + DomeOut(k) * DomeRadius;
                var box = Greybox.Box("Dome Column", Root, foot + Vector3.up * (DomeSpring / 2f), new Vector3(0.36f, DomeSpring, 0.36f), GreenhouseIron, surface: Surface.Metal);
                box.GetComponent<Renderer>().enabled = false;
            }

            // The palms' planters, square inside the round stone tubs drawn over them.
            foreach (var p in PalmSpots)
                GreenhouseSolid("Palm Planter", new Vector3(p.x, PalmPlanterTop / 2f, p.y), new Vector3(1.75f, PalmPlanterTop, 1.75f), 45f);

            // The pond's kerb, and the willows' trunks round it.
            Block("Pond Kerb", z, PondX0 - PondKerb, PondZ0 - PondKerb, PondX1 + PondKerb, PondZ0, PondKerbTop, PlanterStone, Surface.Ashlar);
            Block("Pond Kerb", z, PondX0 - PondKerb, PondZ1, PondX1 + PondKerb, PondZ1 + PondKerb, PondKerbTop, PlanterStone, Surface.Ashlar);
            Block("Pond Kerb", z, PondX0 - PondKerb, PondZ0, PondX0, PondZ1, PondKerbTop, PlanterStone, Surface.Ashlar);
            Block("Pond Kerb", z, PondX1, PondZ0, PondX1 + PondKerb, PondZ1, PondKerbTop, PlanterStone, Surface.Ashlar);
            foreach (var t in PondTrees)
                GreenhouseSolid("Tree Trunk", new Vector3(t.x, 1f, t.y), new Vector3(0.35f, 2f, 0.35f));
        }

        /// <summary>A collider with nothing drawn: what's seen there is drawn in the dressing.</summary>
        GameObject GreenhouseSolid(string name, Vector3 local, Vector3 size, float yaw = 0f)
        {
            var box = Greybox.Box(name, Root, G(local.x, local.y, local.z), size, GreenhouseIron, surface: Surface.Metal);
            box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.GetComponent<Renderer>().enabled = false;
            return box;
        }

        // ---------------------------------------------------------------- dressing

        void GreenhouseMakeover()
        {
            var rng = new System.Random(1811);
            GreenhouseFloor(rng);
            GreenhouseBeds(rng);
            GreenhouseDome(rng);
            GreenhousePond(rng);
            GreenhouseGallery(rng);
            GreenhousePalms(rng);
            GreenhouseFurniture(rng);
            GreenhouseWalls(rng);
            GreenhouseRoof(rng);
            GreenhouseLights();
        }

        /// <summary>A Kenney kit model (they're small, so scaled up) with its base centred on p; false if the art isn't there.</summary>
        bool KenneyModel(string name, Vector3 p, float yaw, float scale, bool shadows = true)
        {
            var model = PropModels.GetPack(KenneyKit + name, PackMaterial);
            if (name == "tree-large" && OakTree(p, yaw, model, scale)) return true;
            if (model == null) return false;
            AddModel(model, p, Quaternion.Euler(0f, yaw, 0f), shadows, scale);
            return true;
        }

        /// <summary>Oak spread against height (the supplied model is wider than it is tall).</summary>
        const float OakSpread = 1.31f;

        /// <summary>
        /// The supplied animated oak in place of Kenney's large tree (the shrubs stay hedges): as tall as the Kenney
        /// tree would have been, but no more than half as wide again, so it keeps inside its bed. False without the art.
        /// </summary>
        bool OakTree(Vector3 p, float yaw, PropModels.Model kenney, float scale)
        {
            float height = scale * 1.5f;
            if (kenney != null)
            {
                var size = kenney.Bounds.size * scale;
                height = Mathf.Min(size.y, Mathf.Max(size.x, size.z) * 1.5f / OakSpread);
            }
            return AuthoredVisual("animated-oak-trees", p, height, yaw) != null;
        }

        /// <summary>A solid Kenney prop on the floor at p, if there's room: the model, or a plain box without the art.</summary>
        bool KenneyProp(string name, Vector3 size, Vector3 p, float yaw, float scale)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            if (!KeptClear(p, Mathf.Max(size.x, size.z) / 2f)) return false;
            if (Solid(p + Vector3.up * (size.y / 2f + 0.05f), size / 2f, rot)) return false;
            var box = Greybox.Box("Prop " + name, Root, p + Vector3.up * (size.y / 2f), size, PropWood, surface: Surface.Wood);
            box.transform.localRotation = rot;
            box.GetComponent<Renderer>().enabled = false;
            if (!KenneyModel(name, p, yaw, scale)) Deco(p + Vector3.up * (size.y / 2f), size, rot, PropWood, Surface.Wood);
            Physics.SyncTransforms();
            return true;
        }

        // ---------------------------------------------------------------- floor

        void GreenhouseFloor(System.Random rng)
        {
            // A lawn round the pond, gone long at its edges.
            Deco(G(-12f, 0.004f, -20.85f), new Vector3(19.4f, 0.006f, 13.7f), new Color(0.16f, 0.22f, 0.09f), Surface.Grass);
            for (int i = 0; i < 26; i++)
            {
                float x = Range(rng, -21.2f, -2.8f), z = Range(rng, -27.2f, -14.4f);
                if (x > PondX0 - 0.6f && x < PondX1 + 0.6f && z > PondZ0 - 0.6f && z < PondZ1 + 0.6f) continue;
                Splat(G(x, 0f, z), Range(rng, 0.8f, 2.2f), Range(rng, 0.6f, 1.6f), Range(rng, 0f, 180f), 0.009f, rng.NextDouble() < 0.5 ? LeafDark : LeafPale, Surface.Grass);
            }

            // Mosaic paving under the dome, ringed in stone.
            var c = DomeCenter;
            DecoPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.005f, new Vector3(DomeRadius * 2f + 0.9f, 0.005f, DomeRadius * 2f + 0.9f), Quaternion.identity, new Color(0.5f, 0.45f, 0.38f), Surface.Mosaic);
            for (int k = 0; k < 32; k++)
            {
                var q = Quaternion.Euler(0f, (k + 0.5f) * 360f / 32f, 0f);
                Deco(c + q * Vector3.forward * (DomeRadius + 0.5f) + Vector3.up * 0.02f, new Vector3(2f * Mathf.PI * (DomeRadius + 0.5f) / 32f * 1.04f, 0.04f, 0.36f), q, PlanterStone, Surface.Ashlar);
            }

            // Soil trodden out of the beds onto the paths, and moss in the cracks.
            foreach (var (x0, x1) in BedColumns)
                foreach (float row in BedRows)
                    for (int k = 0; k < 2; k++)
                        if (rng.NextDouble() < 0.5)
                            Splat(G(Range(rng, x0, x1), 0f, row + (k == 0 ? -1f : 1f) * Range(rng, 1.1f, 1.4f)), Range(rng, 0.4f, 1f), Range(rng, 0.2f, 0.4f), Range(rng, -15f, 15f), 0.006f, Shade(Soil, 1.6f), Surface.Dirt);
            for (int i = 0; i < 18; i++)
                Splat(G(Range(rng, -21f, 21f), 0f, Range(rng, -13f, 7.5f)), Range(rng, 0.3f, 0.9f), Range(rng, 0.2f, 0.5f), Range(rng, 0f, 180f), 0.007f, LeafDark, Surface.Grass);
        }

        // ---------------------------------------------------------------- the beds

        void GreenhouseBeds(System.Random rng)
        {
            int n = 0;
            foreach (var (x0, x1) in BedColumns)
                foreach (float row in BedRows)
                {
                    var mid = G((x0 + x1) / 2f, 0f, row);
                    float w = x1 - x0;
                    // A stone coping round the brick, and the soil inside it.
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Deco(mid + new Vector3(0f, BedTop + 0.03f, s * (BedHalf - 0.08f)), new Vector3(w + 0.04f, 0.06f, 0.2f), PlanterStone, Surface.Ashlar);
                        Deco(mid + new Vector3(s * (w / 2f - 0.08f), BedTop + 0.03f, 0f), new Vector3(0.2f, 0.06f, BedHalf * 2f - 0.3f), PlanterStone, Surface.Ashlar);
                    }
                    Deco(mid + Vector3.up * (BedTop + 0.01f), new Vector3(w - 0.3f, 0.02f, BedHalf * 2f - 0.3f), Soil, Surface.Dirt);
                    var top = mid + Vector3.up * (BedTop + 0.02f);
                    switch (n++ % 6)
                    {
                        case 0: Cabbages(top, w, rng); break;
                        case 1: BeanCanes(top, w, rng); break;
                        case 2: Flowers(top, w, rng); break;
                        case 3: Shrubs(top, w, rng); break;
                        case 4: FruitTrees(top, w, rng); break;
                        default: Overgrown(top, w, rng); break;
                    }
                }
        }

        void Cabbages(Vector3 top, float w, System.Random rng)
        {
            foreach (float r in new[] { -0.45f, 0.45f })
                for (float x = -w / 2f + 0.5f; x < w / 2f - 0.3f; x += Range(rng, 0.5f, 0.7f))
                {
                    var p = top + new Vector3(x, 0f, r + Range(rng, -0.08f, 0.08f));
                    float s = Range(rng, 0.32f, 0.46f);
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 0.1f, new Vector3(s, s * 0.6f, s), Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f), rng.NextDouble() < 0.3 ? LeafPale : Leaf, Surface.Grass);
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 0.16f, new Vector3(s * 0.5f, s * 0.45f, s * 0.5f), Quaternion.identity, new Color(0.42f, 0.5f, 0.24f), Surface.Grass);
                }
        }

        void BeanCanes(Vector3 top, float w, System.Random rng)
        {
            // A row of cane wigwams, beans twined up them.
            for (float x = -w / 2f + 0.8f; x < w / 2f - 0.5f; x += 1.3f)
            {
                var apex = top + new Vector3(x, 1.7f, 0f);
                foreach (var (dx, dz) in new[] { (-0.35f, -0.45f), (0.35f, -0.45f), (-0.35f, 0.45f), (0.35f, 0.45f) })
                {
                    var foot = top + new Vector3(x + dx, 0f, dz);
                    Rod(foot, apex + new Vector3(dx * 0.1f, 0.15f, dz * 0.1f), 0.025f, new Color(0.55f, 0.47f, 0.3f), Surface.Wood);
                    for (float t = 0.12f; t < 0.95f; t += Range(rng, 0.1f, 0.18f))
                    {
                        var p = Vector3.Lerp(foot, apex, t) + new Vector3(Range(rng, -0.06f, 0.06f), 0f, Range(rng, -0.06f, 0.06f));
                        DecoPrim(PrimitiveType.Sphere, p, new Vector3(0.18f, 0.05f, 0.14f), Quaternion.Euler(Range(rng, -50f, 50f), Range(rng, 0f, 360f), 0f), Leaf, Surface.Grass);
                        if (rng.NextDouble() < 0.25)
                            Rod(p, p - Vector3.up * 0.16f, 0.018f, new Color(0.36f, 0.45f, 0.18f), Surface.Grass);
                    }
                }
            }
        }

        void Flowers(Vector3 top, float w, System.Random rng)
        {
            var colors = new[] { new Color(0.75f, 0.2f, 0.22f), new Color(0.85f, 0.7f, 0.25f), new Color(0.5f, 0.3f, 0.6f), new Color(0.85f, 0.82f, 0.75f) };
            for (int i = 0; i < Mathf.RoundToInt(w * 7f); i++)
            {
                var foot = top + new Vector3(Range(rng, -w / 2f + 0.3f, w / 2f - 0.3f), 0f, Range(rng, -0.7f, 0.7f));
                var head = foot + new Vector3(Range(rng, -0.05f, 0.05f), Range(rng, 0.3f, 0.65f), Range(rng, -0.05f, 0.05f));
                Rod(foot, head, 0.015f, Leaf, Surface.Grass);
                DecoPrim(PrimitiveType.Sphere, head, Vector3.one * Range(rng, 0.09f, 0.15f), Quaternion.identity, colors[(i / 5) % colors.Length], Surface.Plain);
                if (i % 3 == 0)
                    DecoPrim(PrimitiveType.Sphere, foot + Vector3.up * 0.08f, new Vector3(0.26f, 0.12f, 0.26f), Quaternion.identity, LeafDark, Surface.Grass);
            }
        }

        void Shrubs(Vector3 top, float w, System.Random rng)
        {
            for (float x = -w / 2f + 0.8f; x < w / 2f - 0.5f; x += Range(rng, 1.1f, 1.5f))
            {
                var p = top + new Vector3(x, 0f, Range(rng, -0.25f, 0.25f));
                if (!KenneyModel("tree-shrub", p, Range(rng, 0f, 90f), Range(rng, 1.2f, 1.6f)))
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.9f), Quaternion.identity, Leaf, Surface.Grass);
            }
            for (int i = 0; i < 10; i++)
                DecoPrim(PrimitiveType.Sphere, top + new Vector3(Range(rng, -w / 2f + 0.3f, w / 2f - 0.3f), 0.05f, Range(rng, -0.7f, 0.7f)), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity, LeafDark, Surface.Grass);
        }

        void FruitTrees(Vector3 top, float w, System.Random rng)
        {
            foreach (float x in new[] { -w / 4f, w / 4f })
            {
                var p = top + new Vector3(x, 0f, 0f);
                if (!KenneyModel("tree-large", p, Range(rng, 0f, 90f), 1.8f))
                {
                    Rod(p, p + Vector3.up * 1.6f, 0.12f, new Color(0.3f, 0.22f, 0.14f), Surface.Wood);
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 2.1f, new Vector3(1.4f, 1.2f, 1.4f), Quaternion.identity, Leaf, Surface.Grass);
                }
                // Fallen fruit under them.
                for (int k = 0; k < 5; k++)
                    DecoPrim(PrimitiveType.Sphere, p + new Vector3(Range(rng, -0.6f, 0.6f), 0.04f, Range(rng, -0.6f, 0.6f)), Vector3.one * 0.08f, Quaternion.identity, new Color(0.62f, 0.24f, 0.1f), Surface.Plain);
            }
            Cabbages(top, w * 0.4f, rng);
        }

        /// <summary>A bed nobody has tended since it all went wrong: weeds, a tangle of creeper and a dead trunk.</summary>
        void Overgrown(Vector3 top, float w, System.Random rng)
        {
            for (int i = 0; i < 28; i++)
            {
                var foot = top + new Vector3(Range(rng, -w / 2f + 0.2f, w / 2f - 0.2f), 0f, Range(rng, -0.75f, 0.75f));
                var tip = foot + new Vector3(Range(rng, -0.25f, 0.25f), Range(rng, 0.25f, 0.9f), Range(rng, -0.25f, 0.25f));
                Rod(foot, tip, 0.02f, rng.NextDouble() < 0.4 ? new Color(0.36f, 0.33f, 0.16f) : LeafDark, Surface.Grass);
            }
            for (int i = 0; i < 6; i++)
                Creeper(top + new Vector3(Range(rng, -w / 2f + 0.3f, w / 2f - 0.3f), 0.05f, Range(rng, -0.6f, 0.6f)), new Vector3(Range(rng, -1f, 1f), 0f, Range(rng, -1f, 1f)).normalized, rng);
            var trunk = top + new Vector3(w / 4f, 0f, 0.1f);
            Rod(trunk, trunk + new Vector3(0.2f, 1.5f, 0f), 0.14f, new Color(0.24f, 0.2f, 0.16f), Surface.Wood);
            Rod(trunk + new Vector3(0.12f, 0.9f, 0f), trunk + new Vector3(0.75f, 1.6f, 0.3f), 0.06f, new Color(0.24f, 0.2f, 0.16f), Surface.Wood);
            Rod(trunk + new Vector3(0.18f, 1.3f, 0f), trunk + new Vector3(-0.4f, 1.9f, -0.2f), 0.05f, new Color(0.24f, 0.2f, 0.16f), Surface.Wood);
        }

        /// <summary>A creeper sprawling over a surface: a wandering stem with leaves along it.</summary>
        void Creeper(Vector3 start, Vector3 dir, System.Random rng)
        {
            var p = start;
            int n = rng.Next(4, 8);
            for (int k = 0; k < n; k++)
            {
                dir = Quaternion.Euler(0f, Range(rng, -40f, 40f), 0f) * dir;
                var next = p + dir * Range(rng, 0.2f, 0.35f);
                Rod(p, next, 0.025f, new Color(0.2f, 0.26f, 0.1f), Surface.Grass);
                DecoPrim(PrimitiveType.Sphere, next + Vector3.up * 0.02f, new Vector3(0.16f, 0.04f, 0.12f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), rng.NextDouble() < 0.5 ? Leaf : LeafDark, Surface.Grass);
                p = next;
            }
        }

        // ---------------------------------------------------------------- the dome

        void GreenhouseDome(System.Random rng)
        {
            var c = DomeCenter;
            for (int k = 0; k < DomeColumns; k++)
            {
                var o = DomeOut(k);
                var foot = c + o * DomeRadius;
                // A slender cast-iron column on a stone base, a flared capital, scrolls bracing it to the ring.
                Deco(foot + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 0.6f), Quaternion.LookRotation(o), PlanterStone, Surface.Ashlar);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 0.48f, new Vector3(0.38f, 0.08f, 0.38f), Quaternion.identity, GreenhouseIron, Surface.Metal);
                Rod(foot + Vector3.up * 0.5f, foot + Vector3.up * (DomeSpring - 0.25f), 0.22f, GreenhouseIron, Surface.Metal);
                for (float y = 1.2f; y < DomeSpring - 0.5f; y += 0.9f)
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * y, new Vector3(0.27f, 0.03f, 0.27f), Quaternion.identity, Shade(GreenhouseIron, 1.4f), Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (DomeSpring - 0.18f), new Vector3(0.42f, 0.14f, 0.42f), Quaternion.identity, GreenhouseIron, Surface.Metal);
                var side = Vector3.Cross(Vector3.up, o);
                foreach (float s in new[] { -1f, 1f })
                {
                    var from = foot + Vector3.up * (DomeSpring - 1f);
                    var to = c + Quaternion.AngleAxis(s * 14f, Vector3.up) * o * DomeRadius + Vector3.up * DomeSpring;
                    Rod(from, Vector3.Lerp(from, to, 0.5f) + side * s * 0.1f + Vector3.up * 0.15f, 0.05f, GreenhouseIron, Surface.Metal);
                    Rod(Vector3.Lerp(from, to, 0.5f) + side * s * 0.1f + Vector3.up * 0.15f, to, 0.05f, GreenhouseIron, Surface.Metal);
                }
                // Ivy up every other column.
                if (k % 2 == 0)
                    for (float y = 0.5f; y < DomeSpring - 0.3f; y += 0.16f)
                    {
                        var round = Quaternion.Euler(0f, y * 160f + k * 40f, 0f) * Vector3.forward;
                        DecoPrim(PrimitiveType.Sphere, foot + Vector3.up * y + round * 0.14f, new Vector3(0.16f, 0.12f, 0.05f), Quaternion.LookRotation(round), rng.NextDouble() < 0.5 ? Leaf : LeafDark, Surface.Grass);
                    }
            }

            // The ring beam the ribs spring from.
            const int ringSegments = 32;
            for (int k = 0; k < ringSegments; k++)
            {
                var q = Quaternion.Euler(0f, (k + 0.5f) * 360f / ringSegments, 0f);
                var at = c + q * Vector3.forward * DomeRadius + Vector3.up * DomeSpring;
                Deco(at, new Vector3(2f * Mathf.PI * DomeRadius / ringSegments * 1.05f, 0.26f, 0.2f), q, GreenhouseIron, Surface.Metal);
                Deco(at - Vector3.up * 0.17f, new Vector3(2f * Mathf.PI * DomeRadius / ringSegments * 1.05f, 0.06f, 0.3f), q, Shade(GreenhouseIron, 1.4f), Surface.Metal);
            }

            // Ribs to the finial, with a light rib between each pair, and glazing rings round them.
            const int bands = 5, steps = 10;
            for (int k = 0; k < DomeColumns * 2; k++)
            {
                var o = Quaternion.Euler(0f, 22.5f + k * 180f / DomeColumns, 0f) * Vector3.forward;
                bool main = k % 2 == 0;
                for (int i = 0; i < steps; i++)
                    Rod(DomeRib(o, (float)i / steps), DomeRib(o, (float)(i + 1) / steps), main ? 0.12f : 0.05f, GreenhouseIron, Surface.Metal);
            }
            for (int b = 1; b < bands; b++)
            {
                float t = (float)b / bands;
                for (int k = 0; k < DomeColumns * 2; k++)
                {
                    var o0 = Quaternion.Euler(0f, 22.5f + k * 180f / DomeColumns, 0f) * Vector3.forward;
                    var o1 = Quaternion.Euler(0f, 22.5f + (k + 1) * 180f / DomeColumns, 0f) * Vector3.forward;
                    Rod(DomeRib(o0, t), DomeRib(o1, t), 0.04f, GreenhouseIron, Surface.Metal);
                }
            }

            // The panes, a few of them gone.
            for (int k = 0; k < DomeColumns * 2; k++)
            {
                var o0 = Quaternion.Euler(0f, 22.5f + k * 180f / DomeColumns, 0f) * Vector3.forward;
                var o1 = Quaternion.Euler(0f, 22.5f + (k + 1) * 180f / DomeColumns, 0f) * Vector3.forward;
                for (int b = 0; b < bands; b++)
                {
                    if (rng.NextDouble() < 0.12) continue;
                    float t0 = (float)b / bands, t1 = (float)(b + 1) / bands;
                    Vector3 a = DomeRib(o0, t0), bb = DomeRib(o1, t0), cc = DomeRib(o0, t1), d = DomeRib(o1, t1);
                    var across = (bb - a + d - cc) / 2f;
                    var up = (cc - a + d - bb) / 2f;
                    var normal = Vector3.Cross(up, across).normalized;
                    Glow((a + bb + cc + d) / 4f, new Vector3(across.magnitude, up.magnitude, 0.01f), Quaternion.LookRotation(normal, up), PaneGlass);
                }
            }

            // The finial: a little lantern of glass under an iron cap and a spike.
            var apex = c + Vector3.up * DomeApex;
            DecoPrim(PrimitiveType.Cylinder, apex + Vector3.up * 0.04f, new Vector3(0.7f, 0.05f, 0.7f), Quaternion.identity, GreenhouseIron, Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, apex + Vector3.up * 0.12f, new Vector3(0.34f, 0.2f, 0.34f), Quaternion.identity, Shade(OldGold, 0.8f), Surface.Metal);
            Rod(apex + Vector3.up * 0.2f, apex + Vector3.up * 0.28f, 0.03f, GreenhouseIron, Surface.Metal);
        }

        // ---------------------------------------------------------------- the pond garden

        void GreenhousePond(System.Random rng)
        {
            float cx = (PondX0 + PondX1) / 2f, cz = (PondZ0 + PondZ1) / 2f, w = PondX1 - PondX0, d = PondZ1 - PondZ0;
            var mid = G(cx, 0f, cz);

            // The fountain in the middle, the art if it's there, its jets and the ripples round it.
            PlaceFree(Fountain, mid, Quaternion.identity, true);
            var jet = new Color(0.7f, 0.85f, 0.9f, 0.35f);
            Glow(PrimitiveType.Cylinder, mid + Vector3.up * 1.5f, new Vector3(0.07f, 0.36f, 0.07f), Quaternion.identity, jet);
            for (int k = 0; k < 6; k++)
            {
                var o = Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward;
                var a = mid + Vector3.up * 1.15f + o * 0.15f;
                var b = mid + Vector3.up * (PondWater + 0.02f) + o * 1.2f;
                var arc = a + o * 0.55f + Vector3.up * 0.35f;
                foreach (var (p, q) in new[] { (a, arc), (arc, b) })
                {
                    var v = q - p;
                    Glow(PrimitiveType.Cylinder, (p + q) / 2f, new Vector3(0.035f, v.magnitude / 2f, 0.035f), Quaternion.FromToRotation(Vector3.up, v), jet);
                }
            }
            // Keep the litter and loose props out of the water.
            _keepOut.Add((G(-14.5f, 0f, -21.5f), 3.6f));
            _keepOut.Add((G(-9.5f, 0f, -21.5f), 3.6f));
            for (int r = 0; r < 3; r++)
                Ring(mid + Vector3.up * (PondWater + 0.006f), 1.15f + r * 0.45f, 0.03f, 24, new Color(0.45f, 0.65f, 0.65f, 0.4f));

            // A dark bottom with weed on it, and the water over it.
            Deco(mid + Vector3.up * 0.006f, new Vector3(w, 0.01f, d), new Color(0.03f, 0.06f, 0.05f), Surface.Metal);
            for (int i = 0; i < 10; i++)
                Splat(G(Range(rng, PondX0 + 0.5f, PondX1 - 0.5f), 0f, Range(rng, PondZ0 + 0.5f, PondZ1 - 0.5f)), Range(rng, 0.7f, 1.8f), Range(rng, 0.5f, 1.4f), Range(rng, 0f, 180f), 0.013f, new Color(0.08f, 0.15f, 0.06f), Surface.Grass);
            Glow(mid + Vector3.up * PondWater, new Vector3(w, 0.004f, d), Quaternion.identity, new Color(0.06f, 0.2f, 0.18f, 0.55f));
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(mid + new Vector3(0f, PondKerbTop + 0.015f, s * (d / 2f + PondKerb / 2f)), new Vector3(w + PondKerb * 2f + 0.06f, 0.03f, PondKerb + 0.06f), Shade(PlanterStone, 1.1f), Surface.Ashlar);
                Deco(mid + new Vector3(s * (w / 2f + PondKerb / 2f), PondKerbTop + 0.015f, 0f), new Vector3(PondKerb + 0.06f, 0.03f, d), Shade(PlanterStone, 1.1f), Surface.Ashlar);
            }

            // Lily pads and their flowers, and reeds along the edges.
            for (int i = 0; i < 34; i++)
            {
                var p = G(Range(rng, PondX0 + 0.3f, PondX1 - 0.3f), PondWater + 0.004f, Range(rng, PondZ0 + 0.3f, PondZ1 - 0.3f));
                if ((p - mid).sqrMagnitude < 1.6f * 1.6f) continue;
                float r = Range(rng, 0.3f, 0.6f);
                DecoPrim(PrimitiveType.Cylinder, p, new Vector3(r, 0.004f, r * 0.9f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Color(0.15f, 0.3f, 0.11f), Surface.Grass);
                if (rng.NextDouble() < 0.25)
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 0.05f, new Vector3(0.14f, 0.09f, 0.14f), Quaternion.identity, rng.NextDouble() < 0.5 ? new Color(0.85f, 0.65f, 0.72f) : new Color(0.9f, 0.88f, 0.8f), Surface.Plain);
            }
            for (int i = 0; i < 16; i++)
            {
                bool alongX = i % 2 == 0;
                float s = rng.NextDouble() < 0.5 ? -1f : 1f;
                var foot = alongX
                    ? G(Range(rng, PondX0 + 0.4f, PondX1 - 0.4f), 0f, s < 0f ? PondZ0 + Range(rng, 0.2f, 0.5f) : PondZ1 - Range(rng, 0.2f, 0.5f))
                    : G(s < 0f ? PondX0 + Range(rng, 0.2f, 0.5f) : PondX1 - Range(rng, 0.2f, 0.5f), 0f, Range(rng, PondZ0 + 0.4f, PondZ1 - 0.4f));
                int n = rng.Next(5, 10);
                for (int k = 0; k < n; k++)
                {
                    var b = foot + new Vector3(Range(rng, -0.25f, 0.25f), 0f, Range(rng, -0.25f, 0.25f));
                    var tip = b + new Vector3(Range(rng, -0.2f, 0.2f), Range(rng, 0.7f, 1.4f), Range(rng, -0.2f, 0.2f));
                    Rod(b, tip, Range(rng, 0.018f, 0.03f), rng.NextDouble() < 0.3 ? new Color(0.34f, 0.32f, 0.16f) : new Color(0.2f, 0.3f, 0.12f), Surface.Grass);
                    if (rng.NextDouble() < 0.15)
                        DecoPrim(PrimitiveType.Capsule, tip - Vector3.up * 0.1f, new Vector3(0.05f, 0.09f, 0.05f), Quaternion.identity, new Color(0.3f, 0.2f, 0.12f), Surface.Fabric);
                }
            }

            // Stepping stones across the west end, the willows, and a bench looking over it all.
            for (int k = 0; k < 6; k++)
            {
                var p = G(PondX0 + 1.4f + Mathf.Sin(k * 1.1f) * 0.4f, PondWater + 0.01f, PondZ0 + 0.5f + k * (d - 1f) / 5f);
                DecoPrim(PrimitiveType.Cylinder, p, new Vector3(Range(rng, 0.55f, 0.75f), 0.012f, Range(rng, 0.5f, 0.65f)), Quaternion.Euler(0f, Range(rng, 0f, 180f), 0f), PlanterStone, Surface.Ashlar);
            }
            foreach (var t in PondTrees)
            {
                var foot = G(t.x, 0f, t.y);
                if (!KenneyModel("tree-large", foot, Range(rng, 0f, 90f), 2.6f))
                {
                    Rod(foot, foot + Vector3.up * 2.4f, 0.3f, new Color(0.3f, 0.22f, 0.14f), Surface.Wood);
                    DecoPrim(PrimitiveType.Sphere, foot + Vector3.up * 3.1f, new Vector3(2.4f, 2f, 2.4f), Quaternion.identity, Leaf, Surface.Grass);
                }
                for (int k = 0; k < 8; k++)
                    DecoPrim(PrimitiveType.Sphere, foot + new Vector3(Range(rng, -1.1f, 1.1f), 0.01f, Range(rng, -1.1f, 1.1f)), new Vector3(0.14f, 0.01f, 0.09f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Color(0.42f, 0.36f, 0.14f), Surface.Grass);
            }
            PlaceFree(GardenBench, G(-12f, 0f, -15.6f), Quaternion.Euler(0f, 180f, 0f), true);
        }

        // ---------------------------------------------------------------- the gallery

        void GreenhouseGallery(System.Random rng)
        {
            float y = GalleryTop;
            float len = GalleryX1 - GalleryX0 - 0.3f, cx = (GalleryX0 + 0.3f + GalleryX1) / 2f;
            // The boards' joints, and a deep iron fascia along the front.
            for (float z = GalleryFront + 0.22f; z < GalleryBack - 0.35f; z += 0.22f)
                Deco(G(cx, y + 0.002f, z), new Vector3(len, 0.004f, 0.015f), Shade(GalleryWood, 0.6f), Surface.Wood);
            Deco(G((GalleryX0 + GalleryX1) / 2f, y - 0.22f, GalleryFront - 0.02f), new Vector3(GalleryX1 - GalleryX0, 0.44f, 0.08f), GreenhouseIron, Surface.Metal);
            Deco(G((GalleryX0 + GalleryX1) / 2f, y - 0.03f, GalleryFront - 0.06f), new Vector3(GalleryX1 - GalleryX0, 0.06f, 0.06f), Shade(GreenhouseIron, 1.5f), Surface.Metal);
            Deco(G(GalleryX1 + 0.02f, y - 0.22f, (GalleryFront + GalleryStairZ - 1.2f) / 2f), new Vector3(0.08f, 0.44f, GalleryStairZ - 1.2f - GalleryFront), GreenhouseIron, Surface.Metal);

            // The posts: cast iron with a capital and a curved bracket each way under the fascia.
            foreach (float x in GalleryPosts)
            {
                var foot = G(x, 0f, GalleryFront + 0.25f);
                Deco(foot + Vector3.up * 0.15f, new Vector3(0.42f, 0.3f, 0.42f), PlanterStone, Surface.Ashlar);
                Rod(foot + Vector3.up * 0.3f, foot + Vector3.up * (y - 0.6f), 0.2f, GreenhouseIron, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (y - 0.55f), new Vector3(0.34f, 0.1f, 0.34f), Quaternion.identity, GreenhouseIron, Surface.Metal);
                foreach (float s in new[] { -1f, 1f })
                {
                    var from = foot + Vector3.up * (y - 1.2f);
                    var bend = foot + new Vector3(s * 0.35f, y - 0.62f, 0f);
                    Rod(from, bend, 0.05f, GreenhouseIron, Surface.Metal);
                    Rod(bend, foot + new Vector3(s * 0.9f, y - 0.42f, -0.25f), 0.05f, GreenhouseIron, Surface.Metal);
                }
            }

            // The railing: iron balusters between a bottom rail and a wooden handrail, along the front (round the ladder) and the open end over the stairs.
            float ladderFrom = GalleryLadderX - 0.6f, ladderTo = GalleryLadderX + 0.6f;
            void Railing(Vector3 from, Vector3 to)
            {
                var v = to - from;
                float l = v.magnitude;
                var dir = v / l;
                int n = Mathf.Max(1, Mathf.RoundToInt(l / 0.16f));
                for (int k = 1; k < n; k++)
                {
                    var at = from + dir * (l * k / n);
                    Rod(at + Vector3.up * 0.08f, at + Vector3.up * 0.95f, 0.022f, GreenhouseIron, Surface.Metal);
                }
                Rod(from + Vector3.up * 0.08f, to + Vector3.up * 0.08f, 0.05f, GreenhouseIron, Surface.Metal);
                Rod(from + Vector3.up * 0.82f, to + Vector3.up * 0.82f, 0.035f, GreenhouseIron, Surface.Metal);
                Rod(from + Vector3.up * 0.98f, to + Vector3.up * 0.98f, 0.08f, Shade(GalleryWood, 0.8f), Surface.Wood);
            }
            Railing(G(GalleryX0 + 0.3f, y, GalleryFront + 0.05f), G(ladderFrom, y, GalleryFront + 0.05f));
            Railing(G(ladderTo, y, GalleryFront + 0.05f), G(GalleryX1 - 0.05f, y, GalleryFront + 0.05f));
            Railing(G(GalleryX1 - 0.05f, y, GalleryFront + 0.05f), G(GalleryX1 - 0.05f, y, GalleryStairZ - 1.25f));

            // The stairs' open side: newel posts and a handrail down the flight.
            float run = Mathf.CeilToInt(y / StepRise - 0.01f) * StepRun;
            var railTop = G(GalleryX1, y + 0.95f, GalleryStairZ - 1.17f);
            var railFoot = G(GalleryX1 + run - StepRun, 0.95f + StepRise, GalleryStairZ - 1.17f);
            Rod(railTop, railFoot, 0.06f, Shade(GalleryWood, 0.8f), Surface.Wood);
            for (float t = 0.08f; t < 1f; t += 0.08f)
            {
                var p = Vector3.Lerp(railTop, railFoot, t);
                Rod(p, p - Vector3.up * 0.95f, 0.025f, GreenhouseIron, Surface.Metal);
            }
            Rod(railFoot + Vector3.up * 0.1f, railFoot - Vector3.up * (railFoot.y - 0.25f), 0.1f, Shade(GalleryWood, 0.7f), Surface.Wood);

            // A hoist off the fascia with a crate hanging from it, and an empty one.
            KenneyModel("pulley-crate", G(-8.6f, y - 2.25f, GalleryFront - 0.73f), 0f, 2f, false);
            KenneyModel("pulley", G(-2.6f, y - 2.25f, GalleryFront - 0.6f), 0f, 2f, false);

            // Up top: seed trays and pots along the wall either side of the bench, crates and a coil of rope.
            for (float x = -21f; x < -0.8f; x += Range(rng, 0.4f, 0.7f))
            {
                if (x > -13.2f && x < -6.8f) continue;   // the build station
                if (x > -20.1f && x < -17.9f) continue;  // the beacon
                Pot(G(x, y, 13.4f + Range(rng, -0.1f, 0.1f)), Range(rng, 0.8f, 1.3f), rng);
            }
            KenneyProp("detail-crate-ropes", new Vector3(0.66f, 0.66f, 0.66f), G(-15.2f, y, 13.2f), 12f, 2.2f);
            KenneyProp("detail-crate", new Vector3(0.66f, 0.66f, 0.66f), G(-5.4f, y, 13.2f), -8f, 2.2f);
            KenneyModel("detail-crate-small", G(-5.4f, y + 0.66f, 13.2f), 30f, 2f);

            // Under it: bulbs on flex from the deck, and the potting benches.
            foreach (float x in new[] { -18.5f, -11f, -3.5f })
            {
                var at = G(x, y - GalleryDeck - 0.5f, 11.2f);
                Rod(at + Vector3.up * 0.5f, at, 0.012f, Soot, Surface.Rubber);
                DecoPrim(PrimitiveType.Cylinder, at - Vector3.up * 0.04f, new Vector3(0.34f, 0.04f, 0.34f), Quaternion.identity, new Color(0.2f, 0.26f, 0.22f), Surface.Metal);
                Glow(PrimitiveType.Sphere, at - Vector3.up * 0.1f, Vector3.one * 0.12f, Quaternion.identity, Lamp);
            }
        }

        /// <summary>A terracotta pot (size about 1) with something growing in it, standing at p.</summary>
        void Pot(Vector3 p, float size, System.Random rng)
        {
            float r = 0.2f * size, h = 0.2f * size;
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * h / 2f, new Vector3(r, h / 2f, r), Quaternion.identity, Shade(Terracotta, Range(rng, 0.85f, 1.1f)), Surface.Plain);
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * (h - 0.01f), new Vector3(r * 1.12f, 0.02f, r * 1.12f), Quaternion.identity, Terracotta, Surface.Plain);
            switch (rng.Next(4))
            {
                case 0:
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * (h + 0.06f), new Vector3(r * 1.3f, r, r * 1.3f), Quaternion.identity, Leaf, Surface.Grass);
                    break;
                case 1:
                    for (int k = 0; k < 4; k++)
                        Rod(p + Vector3.up * h, p + Vector3.up * h + new Vector3(Range(rng, -0.1f, 0.1f), Range(rng, 0.15f, 0.35f), Range(rng, -0.1f, 0.1f)) * size, 0.015f, LeafPale, Surface.Grass);
                    break;
                case 2:
                    // Dead and brown.
                    Rod(p + Vector3.up * h, p + Vector3.up * (h + 0.2f * size) + Vector3.right * 0.06f, 0.012f, new Color(0.32f, 0.25f, 0.15f), Surface.Wood);
                    break;
                default:
                    DecoPrim(PrimitiveType.Sphere, p + Vector3.up * (h + 0.03f), new Vector3(r * 1.6f, r * 0.5f, r * 1.6f), Quaternion.identity, LeafDark, Surface.Grass);
                    break;
            }
        }

        // ---------------------------------------------------------------- palms

        void GreenhousePalms(System.Random rng)
        {
            foreach (var spot in PalmSpots)
            {
                var foot = G(spot.x, 0f, spot.y);
                // A round stone tub with a moulded rim, and soil in it.
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (PalmPlanterTop / 2f), new Vector3(PalmPlanterRadius * 2f, PalmPlanterTop / 2f, PalmPlanterRadius * 2f), Quaternion.identity, PlanterStone, Surface.Ashlar);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (PalmPlanterTop - 0.04f), new Vector3(PalmPlanterRadius * 2f + 0.16f, 0.06f, PalmPlanterRadius * 2f + 0.16f), Quaternion.identity, Shade(PlanterStone, 1.1f), Surface.Ashlar);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * PalmPlanterTop, new Vector3(PalmPlanterRadius * 2f - 0.2f, 0.01f, PalmPlanterRadius * 2f - 0.2f), Quaternion.identity, Soil, Surface.Dirt);

                // The supplied oak where it's in the project; otherwise a palm built from parts.
                if (AuthoredVisual("animated-oak-trees", foot + Vector3.up * PalmPlanterTop, 4.4f, Range(rng, 0f, 360f)) != null) continue;

                // The trunk leaning a little, ringed where the old fronds fell.
                float height = Range(rng, 5f, 5.8f);
                var lean = new Vector3(Range(rng, -0.6f, -0.2f), 0f, Range(rng, -0.4f, 0.4f));
                var p = foot + Vector3.up * PalmPlanterTop;
                const int segments = 9;
                for (int k = 0; k < segments; k++)
                {
                    float t = (k + 1f) / segments;
                    var next = foot + Vector3.up * (PalmPlanterTop + (height - PalmPlanterTop) * t) + lean * (t * t);
                    float dia = Mathf.Lerp(0.34f, 0.22f, t);
                    Rod(p, next, dia, k % 2 == 0 ? new Color(0.36f, 0.29f, 0.2f) : new Color(0.3f, 0.24f, 0.17f), Surface.Wood);
                    DecoPrim(PrimitiveType.Cylinder, next, new Vector3(dia + 0.05f, 0.03f, dia + 0.05f), Quaternion.identity, new Color(0.26f, 0.2f, 0.14f), Surface.Wood);
                    p = next;
                }

                // The crown: long fronds arching out and down, leaflets along each, coconuts under them.
                int fronds = rng.Next(9, 12);
                for (int f = 0; f < fronds; f++)
                {
                    var o = Quaternion.Euler(0f, f * 360f / fronds + Range(rng, -10f, 10f), 0f) * Vector3.forward;
                    float l = Range(rng, 2.2f, 3f), rise = Range(rng, 0.3f, 0.7f);
                    var a = p;
                    for (int k = 0; k < 6; k++)
                    {
                        float t = (k + 1f) / 6f;
                        var b = p + o * (l * t) + Vector3.up * (rise * Mathf.Sin(t * Mathf.PI * 0.8f) - t * t * 1.4f);
                        var side = Vector3.Cross(Vector3.up, o);
                        float span = 0.55f * Mathf.Sin(t * Mathf.PI) + 0.1f;
                        Bar(a, b, span, 0.012f, Vector3.Cross(b - a, side).normalized, f % 3 == 0 ? LeafPale : Leaf, Surface.Grass);
                        Rod(a, b, 0.03f, new Color(0.3f, 0.36f, 0.15f), Surface.Grass);
                        a = b;
                    }
                }
                for (int k = 0; k < 3; k++)
                    DecoPrim(PrimitiveType.Sphere, p + new Vector3(Range(rng, -0.2f, 0.2f), -0.2f, Range(rng, -0.2f, 0.2f)), Vector3.one * 0.18f, Quaternion.identity, new Color(0.3f, 0.22f, 0.12f), Surface.Wood);
            }
        }

        // ---------------------------------------------------------------- furniture and clutter

        void GreenhouseFurniture(System.Random rng)
        {
            // Potting benches under the gallery and in the south-east corner, pots and tools on them.
            var north = Quaternion.Euler(0f, 180f, 0f);
            foreach (var (x, z, rot) in new[] { (-19.6f, 13.1f, north), (-11.5f, 13.1f, north), (-4.6f, 13.1f, north), (15.5f, -13.1f, Quaternion.identity) })
                if (PlaceFree(Workbench, G(x, 0f, z), rot, true))
                    for (float t = -0.8f; t < 0.85f; t += 0.32f)
                        if (rng.NextDouble() < 0.75) Pot(G(x + t, Workbench.Size.y, z + Range(rng, -0.2f, 0.2f)), Range(rng, 0.7f, 1.1f), rng);

            // Sacks, pails and barrels round them; Kenney's barrels and crates.
            PlaceFree(Sack, G(-17.9f, 0f, 13.3f), north * Quaternion.Euler(0f, 20f, 0f), true);
            PlaceFree(Pail, G(-21.1f, 0f, 11.9f), Quaternion.identity, true);
            PlaceFree(AppleBarrel, G(-2.9f, 0f, 13.2f), Quaternion.identity, true);
            KenneyProp("barrels", new Vector3(1.32f, 1.08f, 0.66f), G(-21.2f, 0f, 9.6f), 90f, 2.2f);
            KenneyProp("detail-barrel", new Vector3(0.53f, 0.66f, 0.53f), G(17.3f, 0f, -13.1f), 0f, 2.2f);
            KenneyProp("detail-crate", new Vector3(0.66f, 0.66f, 0.66f), G(13.5f, 0f, -13f), 15f, 2.2f);
            KenneyModel("detail-crate-small", G(13.5f, 0.66f, -13f), 40f, 2f);
            KenneyProp("detail-crate-ropes", new Vector3(0.66f, 0.66f, 0.66f), G(-2.9f, 0f, -26.9f), 5f, 2.2f);
            for (int i = 0; i < 6; i++)
            {
                var p = G(Range(rng, -20.5f, -1f), 0f, Range(rng, 12.6f, 13.3f));
                if (FloorFree(p, 0.25f)) Pot(p, Range(rng, 1.2f, 1.8f), rng);
            }

            // Tall planters either side of the walk in from the Barracks, benches round the dome.
            PlaceFree(Planter, G(3.4f, 0f, -10.2f), Quaternion.identity, true);
            PlaceFree(Planter, G(14.6f, 0f, -10.2f), Quaternion.identity, true);
            PlaceFree(GardenBench, G(DomeX, 0f, DomeZ + DomeRadius + 1.4f), Quaternion.Euler(0f, 180f, 0f), true);
            PlaceFree(GardenBench, G(DomeX + DomeRadius + 1.4f, 0f, DomeZ + 3f), Quaternion.Euler(0f, -90f, 0f), true);
            PlaceFree(BigVase, G(2.6f, 0f, 4.4f), Quaternion.identity, true);
            PlaceFree(BigVase, G(15.4f, 0f, 4.4f), Quaternion.identity, true);
        }

        // ---------------------------------------------------------------- walls

        void GreenhouseWalls(System.Random rng)
        {
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Zone != GreenhouseZone) continue;

                // Tall glazed panels high on the brick, the night through them, wherever the wall is clear: a full
                // panel, or the top half over the gallery.
                int panels = Mathf.FloorToInt((f.Length - 0.6f) / 3.1f);
                for (int k = 0; k < panels; k++)
                {
                    float t = (f.Length - panels * 3.1f) / 2f + (k + 0.5f) * 3.1f;
                    float bottom = WallFree(f, t, 4.7f, 1.35f, 1.95f, 0.12f) ? 2.8f : WallFree(f, t, 5.6f, 1.35f, 0.95f, 0.12f) ? 4.7f : -1f;
                    if (bottom < 0f) continue;
                    GlazedPanel(f, t, bottom, 6.6f);
                }

                // Ivy climbing from the foot of the wall.
                for (float t = Range(rng, 1f, 3f); t < f.Length - 1f; t += Range(rng, 2.5f, 6f))
                {
                    if (!WallFree(f, t, 1.2f, 0.4f, 1f, 0.15f)) continue;
                    WallIvy(f, t, Range(rng, 2f, 5.5f), rng);
                }

                // A lantern on a bracket either end of each long wall.
                if (f.Length > 10f)
                    foreach (float t in new[] { 2.2f, f.Length - 2.2f })
                        if (WallFree(f, t, 3.4f, 0.3f, 0.4f, 0.5f))
                        {
                            WallLantern(f.At(t, 3.4f), f.Inward, true);
                            CandleLight(f.At(t, 3.1f, 0.5f));
                        }
            }
        }

        /// <summary>An iron-framed glazed panel on a wall face, from bottom to top, with a round fanlight in its head.</summary>
        void GlazedPanel(WallFace f, float t, float bottom, float top)
        {
            const float w = 2.5f;
            float h = top - bottom, mid = (bottom + top) / 2f;
            Deco(f.At(t, mid, 0.01f), new Vector3(w, h, 0.02f), f.Facing, NightGlass, Surface.Metal);
            Glow(f.At(t, mid, 0.025f), new Vector3(w - 0.1f, h - 0.1f, 0.005f), f.Facing, new Color(0.25f, 0.35f, 0.45f, 0.12f));
            // The frame, a mullion and transoms.
            foreach (float s in new[] { -1f, 1f })
                Deco(f.At(t + s * (w / 2f - 0.04f), mid, 0.04f), new Vector3(0.08f, h, 0.06f), f.Facing, GreenhouseIron, Surface.Metal);
            Deco(f.At(t, top - 0.04f, 0.04f), new Vector3(w, 0.08f, 0.06f), f.Facing, GreenhouseIron, Surface.Metal);
            Deco(f.At(t, bottom + 0.04f, 0.05f), new Vector3(w + 0.2f, 0.08f, 0.1f), f.Facing, Shade(PlanterStone, 0.9f), Surface.Ashlar);
            Deco(f.At(t, mid, 0.035f), new Vector3(0.04f, h, 0.04f), f.Facing, GreenhouseIron, Surface.Metal);
            for (float y = bottom + 0.9f; y < top - 1.2f; y += 0.9f)
                Deco(f.At(t, y, 0.035f), new Vector3(w, 0.04f, 0.04f), f.Facing, GreenhouseIron, Surface.Metal);
            // The fanlight: radiating bars in the head, under an arch.
            var hub = f.At(t, top - 1.1f, 0.035f);
            for (int k = 0; k <= 6; k++)
            {
                float a = k * Mathf.PI / 6f;
                var tip = hub + (f.Along * Mathf.Cos(a) * (w / 2f - 0.06f)) + Vector3.up * (Mathf.Sin(a) * 1.02f);
                Rod(hub, tip, 0.03f, GreenhouseIron, Surface.Metal);
            }
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                Rod(hub + f.Along * Mathf.Cos(a0) * (w / 2f - 0.06f) + Vector3.up * (Mathf.Sin(a0) * 1.02f),
                    hub + f.Along * Mathf.Cos(a1) * (w / 2f - 0.06f) + Vector3.up * (Mathf.Sin(a1) * 1.02f), 0.05f, GreenhouseIron, Surface.Metal);
            }
        }

        /// <summary>Ivy up a wall face from its foot: a few stems spreading as they climb, thick with leaves.</summary>
        void WallIvy(WallFace f, float t, float height, System.Random rng)
        {
            int stems = rng.Next(2, 5);
            for (int s = 0; s < stems; s++)
            {
                var p = f.At(t + Range(rng, -0.3f, 0.3f), 0f, 0.03f);
                float lean = Range(rng, -0.35f, 0.35f);
                while ((p.y) < height)
                {
                    var next = p + Vector3.up * Range(rng, 0.25f, 0.45f) + f.Along * (lean + Range(rng, -0.15f, 0.15f));
                    Rod(p, next, 0.025f, new Color(0.24f, 0.2f, 0.12f), Surface.Wood);
                    for (int k = 0; k < 3; k++)
                    {
                        var leaf = Vector3.Lerp(p, next, Range(rng, 0f, 1f)) + f.Along * Range(rng, -0.18f, 0.18f) + f.Inward * 0.03f;
                        DecoPrim(PrimitiveType.Sphere, leaf, new Vector3(0.2f, 0.17f, 0.04f), f.Facing * Quaternion.Euler(0f, 0f, Range(rng, 0f, 180f)), rng.NextDouble() < 0.5 ? Leaf : LeafDark, Surface.Grass);
                    }
                    p = next;
                }
            }
        }

        // ---------------------------------------------------------------- the glass roof

        void GreenhouseRoof(System.Random rng)
        {
            // The panes in a 2 m grid on glazing bars, with some fallen in and open to the night.
            foreach (var r in ZoneRects(GreenhouseZone))
            {
                for (float x = r.xMin; x < r.xMax - 0.01f; x += 2f)
                    for (float z = r.yMin; z < r.yMax - 0.01f; z += 2f)
                    {
                        if (rng.NextDouble() < 0.08) continue;
                        Glow(new Vector3(x + 1f, WallHeight + RoofThickness / 2f, z + 1f), new Vector3(2f, RoofThickness, 2f), Quaternion.identity, RoofGlass);
                    }
                for (float x = r.xMin + 2f; x < r.xMax - 0.01f; x += 2f)
                    Deco(new Vector3(x, WallHeight - 0.03f, r.center.y), new Vector3(0.04f, 0.04f, r.height), GreenhouseIron, Surface.Metal);
                for (float z = r.yMin + 2f; z < r.yMax - 0.01f; z += 2f)
                    Deco(new Vector3(r.center.x, WallHeight - 0.05f, z), new Vector3(r.width, 0.05f, 0.05f), GreenhouseIron, Surface.Metal);
            }

            // Arched trusses wall to wall every 4 m, braced to the ridge plate, knees down the walls.
            for (float x = -20f; x <= 20.01f; x += 4f)
            {
                var (z0, z1) = TrussSpan(x);
                const int segments = 18;
                for (int k = 0; k < segments; k++)
                {
                    float a = Mathf.Lerp(z0, z1, (float)k / segments), b = Mathf.Lerp(z0, z1, (k + 1f) / segments);
                    Bar(G(x, TrussY(z0, z1, a), a), G(x, TrussY(z0, z1, b), b), 0.12f, 0.22f, Vector3.up, GreenhouseIron, Surface.Metal);
                }
                Deco(G(x, WallHeight - 0.08f, (z0 + z1) / 2f), new Vector3(0.12f, 0.12f, z1 - z0), GreenhouseIron, Surface.Metal);
                for (float z = z0 + 1.5f; z < z1 - 1f; z += 1.5f)
                {
                    float y = TrussY(z0, z1, z);
                    if (WallHeight - 0.14f - y > 0.12f) Rod(G(x, y, z), G(x, WallHeight - 0.14f, z + 0.75f), 0.04f, GreenhouseIron, Surface.Metal);
                }
                foreach (var (wall, inward) in new[] { (z0, 1f), (z1, -1f) })
                {
                    // No knee down the north wall where it would hang over the gallery.
                    if (inward < 0f && x < GalleryX1 + 0.5f) continue;
                    Rod(G(x, 4.9f, wall + inward * 0.05f), G(x, TrussSpring - 0.05f, wall + inward * 1.2f), 0.06f, GreenhouseIron, Surface.Metal);
                    Deco(G(x, 5.4f, wall + inward * 0.04f), new Vector3(0.16f, 1.1f, 0.06f), GreenhouseIron, Surface.Metal);
                }

                // Vines and baskets hanging off the arch.
                for (float z = z0 + Range(rng, 1f, 3f); z < z1 - 1f; z += Range(rng, 1.5f, 4f))
                {
                    var top = G(x, TrussY(z0, z1, z) - 0.1f, z);
                    if (Mathf.Abs(x - DomeX) < DomeRadius + 0.5f && Mathf.Abs(z - DomeZ) < DomeRadius + 0.5f) continue;
                    if (z > GalleryFront - 0.5f && x < GalleryX1 + 0.5f) continue;
                    if (rng.NextDouble() < 0.18) HangingBasket(top, rng);
                    else HangingVine(top, rng);
                }
            }
        }

        void HangingVine(Vector3 top, System.Random rng)
        {
            int n = rng.Next(2, 6);
            var p = top;
            for (int k = 0; k < n; k++)
            {
                var next = p + new Vector3(Range(rng, -0.15f, 0.15f), -Range(rng, 0.35f, 0.6f), Range(rng, -0.15f, 0.15f));
                Rod(p, next, 0.025f, new Color(0.18f, 0.24f, 0.1f), Surface.Grass);
                for (int j = 0; j < 2; j++)
                    DecoPrim(PrimitiveType.Sphere, Vector3.Lerp(p, next, Range(rng, 0.2f, 1f)) + new Vector3(Range(rng, -0.07f, 0.07f), 0f, Range(rng, -0.07f, 0.07f)), new Vector3(0.15f, 0.03f, 0.11f),
                        Quaternion.Euler(Range(rng, -50f, 50f), Range(rng, 0f, 360f), 0f), rng.NextDouble() < 0.5 ? Leaf : LeafDark, Surface.Grass);
                p = next;
            }
        }

        void HangingBasket(Vector3 top, System.Random rng)
        {
            var basket = top - Vector3.up * Range(rng, 1.1f, 1.6f);
            foreach (float a in new[] { 0f, 120f, 240f })
                Rod(top, basket + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.22f + Vector3.up * 0.1f, 0.012f, Soot, Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, basket, new Vector3(0.48f, 0.3f, 0.48f), Quaternion.identity, new Color(0.34f, 0.25f, 0.14f), Surface.Wood);
            DecoPrim(PrimitiveType.Sphere, basket + Vector3.up * 0.1f, new Vector3(0.56f, 0.26f, 0.56f), Quaternion.identity, Leaf, Surface.Grass);
            for (int k = 0; k < 4; k++)
                HangingVine(basket + Quaternion.Euler(0f, k * 90f + Range(rng, -20f, 20f), 0f) * Vector3.forward * 0.22f, rng);
        }

        // ---------------------------------------------------------------- lights

        void GreenhouseLights()
        {
            // The moon down through the glass onto the dome.
            var moon = new GameObject("Greenhouse Moonlight").AddComponent<Light>();
            moon.transform.SetParent(Root, false);
            moon.transform.localPosition = G(DomeX - 1f, 16f, DomeZ + 1f);
            moon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            moon.type = LightType.Spot;
            moon.spotAngle = 70f;
            moon.range = 24f;
            moon.intensity = 3.5f;
            moon.color = GreenhouseMoon;
            moon.shadows = LightShadows.None;

            // The chandelier under the dome's finial.
            var apex = G(DomeX, DomeApex - 0.05f, DomeZ);
            if (!HangChandelier(apex, Chandelier)) CeilingLantern(apex - Vector3.up * 0.3f);
            ChapelLight(apex - Vector3.up * 2.6f, 12f, 1.5f, GreenhouseLampLight);

            // Lanterns hung from the trusses over the beds, the east walk and the pond.
            foreach (var (x, z) in new[] { (-20f, -3f), (-8f, -8f), (-8f, 2.5f), (0f, -9f), (16f, -6f), (16f, 5.5f), (-12f, -21.5f) })
            {
                var (z0, z1) = TrussSpan(x);
                var at = G(x, TrussY(z0, z1, z) - 0.15f, z);
                CeilingLantern(at);
                ChapelLight(at - Vector3.up * 1.2f, 11f, 1.3f, GreenhouseLampLight);
            }

            // The bulbs under the gallery, and a cool green fill off all the leaves and glass.
            foreach (float x in new[] { -18.5f, -11f, -3.5f })
                ChapelLight(G(x, GalleryTop - GalleryDeck - 0.75f, 11.2f), 7f, 1f, Lamp);
            ChapelLight(G(-10f, 4.5f, -4f), 22f, 0.35f, new Color(0.55f, 0.8f, 0.6f));
            ChapelLight(G(12f, 4.5f, 2f), 22f, 0.3f, new Color(0.55f, 0.8f, 0.6f));
            ChapelLight(G(-12f, 2.5f, -21.5f), 9f, 0.6f, new Color(0.35f, 0.75f, 0.7f));
        }
    }
}
