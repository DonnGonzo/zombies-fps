using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The six rooms made from the rest of Ivan's Ten Worlds pack, restyled for the map:
    ///   Gaol (from the quarantine block): a vaulted cell block under the old gate, cells along the walls.
    ///   Plague Ward (from the hospital ward): sickbeds in curtained bays down a timber-posted hall.
    ///   Scriptorium (from the lecture hall): rows of writing desks facing a lectern stage, bookcases round the walls.
    ///   Alchemist's Furnace (from the reactor control room): a great copper furnace with its flue to the roof.
    ///   Rampart Ruins (from the parkour gauntlet): a climb over fallen masonry up onto a broken battlement.
    ///   Sunken Temple (from the prayer hall): a colonnaded hall with a reflecting pool, carpets and a raised sanctuary.
    ///
    /// Everything solid here is placed the same with or without the art; the models only change the look.
    /// </summary>
    public partial class ZoneMap
    {
        const int Gaol = 12, PlagueWard = 13, Scriptorium = 14, Furnace = 15, Rampart = 16, Temple = 17;

        static readonly Color Stone = new Color(0.4f, 0.39f, 0.36f);
        static readonly Color Timber = new Color(0.3f, 0.21f, 0.13f);
        static readonly Color FurnaceGlow = new Color(1f, 0.45f, 0.15f);

        void BuildNewRooms()
        {
            // Gaol: two squat pillars hold up the vault; the guard hall has a rack and a gibbet cage.
            foreach (float x in new[] { -6f, 6f })
                Greybox.Box("Pillar", Root, L(Gaol, x, WallHeight / 2f, -0.5f), new Vector3(1.3f, WallHeight, 1.3f), Stone, surface: Surface.Concrete);
            Greybox.Box("Rack", Root, L(Gaol, 6.5f, 0.45f, 9.5f), new Vector3(2.2f, 0.9f, 1f), Timber, surface: Surface.Wood);

            // Plague Ward: a row of timber posts down the hall.
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
                Greybox.Box("Post", Root, L(PlagueWard, x, WallHeight / 2f, 0f), new Vector3(0.45f, WallHeight, 0.45f), Timber, surface: Surface.Wood);

            // Scriptorium: rows of writing desks, each with its stool, facing the lectern stage on the north wall.
            var desk = PropModels.GetPack(WritingDesk.Path, PackMaterial);
            var stool = PropModels.GetPack(Stool.Path, PackMaterial);
            foreach (float x in new[] { -5f, -2.4f, 2.4f, 5f })
                foreach (float z in new[] { -5f, -1.5f, 2f })
                {
                    var at = L(Scriptorium, x, 0f, z);
                    var face = Quaternion.Euler(0f, 180f, 0f);
                    // One collider over desk and stool together.
                    var box = Greybox.Box("Writing Desk", Root, at + new Vector3(0f, 0.4f, -0.3f), new Vector3(1.3f, 0.8f, 1.15f), Timber, surface: Surface.Wood);
                    if (desk == null) continue;
                    box.GetComponent<Renderer>().enabled = false;
                    AddModel(desk, at, face, true);
                    if (stool != null) AddModel(stool, at + new Vector3(0f, 0f, -0.62f), face, true);
                }

            // Alchemist's Furnace: the furnace in the middle (its drum is the collider either way), glowing from inside.
            var core = Greybox.Prim(PrimitiveType.Cylinder, "Furnace", Root, L(Furnace, 0f, 2.6f, 0f), new Vector3(4.6f, 2.6f, 4.6f), new Color(0.3f, 0.2f, 0.14f), surface: Surface.Rust);
            var furnace = PropModels.GetPack(FurnaceCore.Path, PackMaterial);
            if (furnace != null)
            {
                core.GetComponent<Renderer>().enabled = false;
                AddModel(furnace, L(Furnace, 0f, 0f, 0f), Quaternion.identity, true, 0.95f);
            }
            var glow = new GameObject("Furnace Light").AddComponent<Light>();
            glow.transform.SetParent(Root, false);
            glow.transform.localPosition = L(Furnace, 0f, 2.4f, -3.2f);
            glow.type = LightType.Point;
            glow.color = FurnaceGlow;
            glow.range = 14f;
            glow.intensity = 2.4f;
            glow.shadows = LightShadows.None;
            glow.gameObject.AddComponent<Flicker>().Seed = 77;
            // Coal bunkers either side.
            foreach (float x in new[] { -16.5f, 16.5f })
                Greybox.Box("Coal Bunker", Root, L(Furnace, x, 0.5f, -6.5f), new Vector3(3f, 1f, 2f), new Color(0.2f, 0.17f, 0.15f), surface: Surface.Concrete);

            // Rampart Ruins: a broken battlement along the east wall, climbed by a run of fallen blocks, and masonry
            // lying about the north end for cover.
            Block("Broken Battlement", Rampart, 4.5f, -10f, 10.7f, 26f, 3.2f, Stone);
            Hop(Rampart, -5.7f, -6f, 0.8f);
            Hop(Rampart, -1.8f, -6f, 1.6f);
            Hop(Rampart, 2.1f, -6f, 2.4f);
            Jump(Rampart, -5.7f, 0f, -8.4f, -5.7f, 0.8f, -6.8f);
            Jump(Rampart, -4.9f, 0.8f, -6f, -2.6f, 1.6f, -6f);
            Jump(Rampart, -1f, 1.6f, -6f, 1.3f, 2.4f, -6f);
            Jump(Rampart, 2.9f, 2.4f, -6f, 5.2f, 3.2f, -6f);
            Stairs(Rampart, 7.6f, 26f, Vector3.forward, 3.2f, 2.4f);
            foreach (var (x, z, h) in new[] { (-5f, 24f, 1.2f), (4f, 34f, 1f), (-6f, 40f, 1.4f), (1f, 12f, 1.1f) })
                Greybox.Box("Fallen Masonry", Root, L(Rampart, x, h / 2f, z), new Vector3(2f, h, 1.3f), Shade(Stone, 0.9f), surface: Surface.Concrete);

            // Sunken Temple: two rows of columns down the hall, and the sanctuary raised two steps at the north end.
            foreach (float x in new[] { -6f, 6f })
                foreach (float z in new[] { -13.5f, -7.5f, -1.5f, 4.5f, 10.5f, 16.5f })
                    Greybox.Box("Column", Root, L(Temple, x, WallHeight / 2f, z), new Vector3(1f, WallHeight, 1f), new Color(0.58f, 0.52f, 0.42f), surface: Surface.Plaster);
            Block("Sanctuary", Temple, -11.7f, 26f, -2f, 33.7f, 0.5f, new Color(0.5f, 0.45f, 0.36f), Surface.Tile);
            Stairs(Temple, -6.85f, 26f, Vector3.back, 0.5f, 4f);
        }

        /// <summary>The new rooms' colliderless dressing.</summary>
        void DressNewRooms()
        {
            var rng = new System.Random(1717);

            // Gaol: straw on the floor, a drain down the middle, and a hanging gibbet cage in the guard hall.
            Deco(L(Gaol, 1f, 0.005f, -0.5f), new Vector3(30f, 0.01f, 0.5f), new Color(0.12f, 0.12f, 0.12f), Surface.Metal);
            for (int i = 0; i < 18; i++)
                Deco(L(Gaol, Range(rng, -15f, 17f), 0.008f, Range(rng, -5.5f, -3f)), new Vector3(Range(rng, 0.4f, 1.2f), 0.012f, Range(rng, 0.3f, 0.8f)),
                    Quaternion.Euler(0f, Range(rng, 0f, 180f), 0f), new Color(0.5f, 0.42f, 0.22f), Surface.Fabric);
            var cage = L(Gaol, -4f, 0f, 9f);
            Rod(cage + Vector3.up * WallHeight, cage + Vector3.up * 4.4f, 0.04f, new Color(0.1f, 0.1f, 0.1f), Surface.Metal);
            for (int k = 0; k < 8; k++)
            {
                var d = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 0.55f;
                Rod(cage + d + Vector3.up * 4.4f, cage + d + Vector3.up * 2.6f, 0.035f, new Color(0.12f, 0.11f, 0.1f), Surface.Rust);
            }
            DecoPrim(PrimitiveType.Cylinder, cage + Vector3.up * 2.6f, new Vector3(1.2f, 0.03f, 1.2f), Quaternion.identity, new Color(0.12f, 0.11f, 0.1f), Surface.Rust);
            DecoPrim(PrimitiveType.Cylinder, cage + Vector3.up * 4.4f, new Vector3(1.2f, 0.03f, 1.2f), Quaternion.identity, new Color(0.12f, 0.11f, 0.1f), Surface.Rust);

            // Plague Ward: linen curtains hanging between the bays along the north wall, and bundles of herbs drying
            // on a line across the hall.
            foreach (float x in new[] { -16f, -8f, 0f, 8f, 16f })
                Deco(L(PlagueWard, x, 1.6f, 7.4f), new Vector3(0.04f, 2.6f, 3.1f), new Color(0.62f, 0.58f, 0.48f), Surface.Fabric);
            Rod(L(PlagueWard, -19.7f, 4.6f, -3f), L(PlagueWard, 19.7f, 4.6f, -3f), 0.02f, new Color(0.3f, 0.25f, 0.18f), Surface.Fabric);
            for (float x = -18f; x < 18f; x += Range(rng, 0.8f, 1.6f))
                DecoPrim(PrimitiveType.Capsule, L(PlagueWard, x, 4.3f, -3f), new Vector3(0.18f, 0.25f, 0.18f), Quaternion.identity,
                    rng.NextDouble() < 0.5 ? new Color(0.25f, 0.3f, 0.14f) : new Color(0.38f, 0.32f, 0.16f), Surface.Grass);

            // Scriptorium: a long runner up the middle aisle.
            Deco(L(Scriptorium, 0f, 0.006f, 1f), new Vector3(2.4f, 0.01f, 15f), new Color(0.32f, 0.1f, 0.08f), Surface.Fabric);
            Deco(L(Scriptorium, 0f, 0.009f, 1f), new Vector3(2f, 0.01f, 14.6f), new Color(0.45f, 0.3f, 0.12f), Surface.Fabric);

            // Alchemist's Furnace: coal heaped in the bunkers and spilled round them, the flue up to the roof and a
            // glowing mouth on the furnace.
            foreach (float x in new[] { -16.5f, 16.5f })
                for (int i = 0; i < 9; i++)
                    DecoPrim(PrimitiveType.Sphere, L(Furnace, x + Range(rng, -1.3f, 1.3f), 1f + Range(rng, 0f, 0.15f), -6.5f + Range(rng, -0.8f, 0.8f)),
                        new Vector3(Range(rng, 0.4f, 0.8f), 0.3f, Range(rng, 0.4f, 0.8f)), Quaternion.identity, new Color(0.06f, 0.06f, 0.06f), Surface.Concrete);
            Rod(L(Furnace, 0f, 5.6f, 0f), L(Furnace, 0f, WallHeight + 0.3f, 0f), 1.1f, new Color(0.2f, 0.14f, 0.1f), Surface.Rust);
            Glow(L(Furnace, 0f, 1.1f, -2.32f), new Vector3(1.4f, 0.8f, 0.05f), Quaternion.identity, FurnaceGlow);

            // Rampart Ruins: rubble strewn below the battlement, and banners hanging from its parapet.
            for (int i = 0; i < 14; i++)
                DecoPrim(PrimitiveType.Cube, L(Rampart, Range(rng, -2f, 4f), 0.12f, Range(rng, -2f, 26f)),
                    new Vector3(Range(rng, 0.3f, 0.7f), Range(rng, 0.15f, 0.3f), Range(rng, 0.3f, 0.7f)), Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f), Shade(Stone, Range(rng, 0.8f, 1.05f)), Surface.Concrete);
            foreach (float z in new[] { 0f, 10f, 20f })
            {
                Deco(L(Rampart, 4.46f, 2.2f, z), new Vector3(0.04f, 2f, 1.2f), new Color(0.42f, 0.1f, 0.08f), Surface.Fabric);
                Deco(L(Rampart, 4.44f, 2.4f, z), new Vector3(0.03f, 0.5f, 0.5f), new Color(0.62f, 0.5f, 0.2f), Surface.Fabric);
            }

            // Sunken Temple: a long reflecting pool down the nave, carpets in the aisles, and a tiled niche in the
            // sanctuary's north wall.
            Deco(L(Temple, 0f, 0.012f, 1.5f), new Vector3(4.6f, 0.02f, 26f), new Color(0.45f, 0.4f, 0.32f), Surface.Tile);
            Deco(L(Temple, 0f, 0.025f, 1.5f), new Vector3(4f, 0.02f, 25.4f), new Color(0.03f, 0.06f, 0.07f), Surface.Metal);
            foreach (float x in new[] { -9f, 9f })
                for (float z = -15f; z < 18f; z += 4.5f)
                {
                    var c = rng.NextDouble() < 0.5 ? new Color(0.4f, 0.1f, 0.08f) : new Color(0.12f, 0.2f, 0.32f);
                    Deco(L(Temple, x, 0.006f, z), new Vector3(2.4f, 0.01f, 3.6f), c, Surface.Fabric);
                    Deco(L(Temple, x, 0.009f, z), new Vector3(2f, 0.01f, 3.2f), Shade(c, 1.5f), Surface.Fabric);
                }
            var niche = L(Temple, -6.85f, 0.5f, 33.68f);
            Deco(niche + new Vector3(0f, 2.2f, 0f), new Vector3(2.6f, 3.6f, 0.04f), new Color(0.16f, 0.34f, 0.36f), Surface.Tile);
            Deco(niche + new Vector3(0f, 2f, -0.01f), new Vector3(1.8f, 3f, 0.04f), new Color(0.08f, 0.1f, 0.12f), Surface.Plain);
            DecoPrim(PrimitiveType.Cylinder, niche + new Vector3(0f, 3.5f, -0.02f), new Vector3(1.8f, 0.02f, 1.8f), Quaternion.Euler(90f, 0f, 0f), new Color(0.62f, 0.5f, 0.2f), Surface.Metal);
        }
    }
}
