using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        // Freight hall: two stock islands west, crane in the central crossing, open loading
        // bay and dispatch gallery east. Door, window, mission and collectible anchors stay fixed.
        const int DepotZone = 1;
        const float DepotDeck = 4f;
        static readonly Color DepotOak = new Color(0.35f, 0.24f, 0.14f);
        static readonly Color DepotIron = new Color(0.14f, 0.22f, 0.21f);
        static readonly Color DepotStone = new Color(0.48f, 0.45f, 0.38f);
        static readonly Color DepotBrass = new Color(0.58f, 0.39f, 0.16f);
        static Vector3 D(float x, float y, float z) => L(DepotZone, x, y, z);

        void BuildDepot()
        {
            // A real deck instead of a filled block: players and zombies can cross beneath it.
            DepotSolid("Dispatch gallery deck", 13.3f, 3.82f, 8.85f, 14.6f, 0.36f, 5.7f, DepotOak, Surface.Wood);
            foreach (float x in new[] { 6.3f, 11f, 20.3f })
                foreach (float z in new[] { 6.3f, 11.3f })
                {
                    DepotSolid("Gallery iron column", x, 1.82f, z, 0.28f, 3.64f, 0.28f, DepotIron, Surface.Metal);
                    DepotSolid("Column plinth", x, 0.15f, z, 0.52f, 0.3f, 0.52f, DepotStone, Surface.Masonry);
                }
            Stairs(DepotZone, 7.8f, 6f, Vector3.back, DepotDeck, 2.8f);
            AddLadder(DepotZone, 15f, 6f, Vector3.back, DepotDeck);
            // Solid rail segments have matching visible geometry; gaps serve stairs and ladder.
            DepotRail(6.1f, 6.1f, 6.1f, 11.4f);
            DepotRail(9.3f, 6.1f, 14.1f, 6.1f);
            DepotRail(15.9f, 6.1f, 20.4f, 6.1f);
            DepotRail(20.4f, 6.1f, 20.4f, 11.4f);

            DepotCargoIsland(-11f, -5f, false);
            DepotCargoIsland(-11f, 5f, true);
            // Low packing table east of the stair; broad southern lane leads to Loading Dock.
            PlacePackItem(Worktable, D(13f, 0f, -3.5f), Quaternion.identity, true);
            PlacePackItem(Crate, D(15.6f, 0f, -3.4f), Quaternion.Euler(0f, 12f, 0f), true);
            // Loading bay beneath the deck: stock hugs the back wall, not the approach to the ladder.
            foreach (float x in new[] { 8f, 10f, 18f, 19.5f })
                PlacePackItem(WoodChest, D(x, 0f, 10.7f), Quaternion.identity, true);
            PlacePackItem(ReadingTable, D(18.7f, DepotDeck, 10.65f), Quaternion.identity, true);
            PlacePackItem(KitStool, D(18.7f, DepotDeck, 9.45f), Quaternion.identity, true);
            PlacePackItem(Sideboard, D(8f, DepotDeck, 10.9f), Quaternion.identity, true);
            // Clear floor remains around beacon (16,4,10), part (13,4,9), and part (-18,0,-9).
        }

        void DepotSolid(string name, float x, float y, float z, float sx, float sy, float sz, Color color, Surface surface)
        {
            Greybox.Box("Depot " + name, Root, D(x, y, z), new Vector3(sx, sy, sz), color, surface: surface);
        }

        void DepotRail(float x0, float z0, float x1, float z1)
        {
            var a = D(x0, DepotDeck, z0);
            var b = D(x1, DepotDeck, z1);
            float length = Vector3.Distance(a, b);
            var rotation = Quaternion.LookRotation(b - a);
            // Actual narrow bars instead of an invisible wall across open railing.
            foreach (float y in new[] { 0.45f, 0.95f })
            {
                var bar = Greybox.Box("Depot gallery rail", Root, (a + b) / 2f + Vector3.up * y,
                    new Vector3(0.09f, 0.09f, length), DepotIron, surface: Surface.Metal);
                bar.transform.localRotation = rotation;
            }
            int count = Mathf.CeilToInt(length / 1.2f);
            for (int i = 0; i <= count; i++)
                Greybox.Box("Depot baluster", Root, Vector3.Lerp(a, b, (float)i / count) + Vector3.up * 0.5f,
                    new Vector3(0.09f, 1f, 0.09f), DepotIron, surface: Surface.Metal);
        }

        void DepotCargoIsland(float x, float z, bool provisions)
        {
            // Walkable low pallet, individual solid stock and a gap through the middle of each island.
            DepotSolid("Cargo pallet", x, 0.08f, z, 5.2f, 0.16f, 2.8f, DepotOak, Surface.Wood);
            foreach (float dx in new[] { -1.8f, 1.8f })
                foreach (float dz in new[] { -0.7f, 0.7f })
                {
                    var item = provisions ? KitBarrel : WoodCrate;
                    PlacePackItem(item, D(x + dx, 0.16f, z + dz), Quaternion.identity, true);
                    if (!provisions)
                        PlacePackItem(IronCrate, D(x + dx, 0.16f + item.Size.y, z + dz), Quaternion.identity, true);
                }
            if (provisions)
            {
                PlacePackItem(BarrelRack, D(x, 0.16f, z + 0.8f), Quaternion.identity, true);
                PlacePackItem(Sack, D(x - 0.45f, 0.16f, z - 0.8f), Quaternion.identity, true);
            }
        }

        void DepotMakeover()
        {
            DepotArchitecture();
            DepotCrane();
            DepotGallery();
            DepotLighting();
            // Dark stone central crossing with brass edging: all four exits remain at ground level.
            for (int x = -21; x < 21; x += 3)
                Deco(D(x + 1.5f, 0.005f, 0f), new Vector3(2.98f, 0.01f, 4f), DepotIron, Surface.Masonry);
            foreach (float z in new[] { -2.1f, 2.1f })
                Paint(DepotZone, -20.5f, z, 20.5f, z, 0.06f, DepotBrass);
            foreach (float x in new[] { -3f, 3f })
                Paint(DepotZone, x, -10f, x, 10f, 0.06f, DepotBrass);
            // Floor lettering and cargo borders identify distinct areas without cluttering the sightlines.
            foreach (float z in new[] { -5f, 5f })
            {
                foreach (float dx in new[] { -3f, 3f })
                    Paint(DepotZone, -11f + dx, z - 1.8f, -11f + dx, z + 1.8f, 0.1f, FadedWhite);
                DepotSign(z < 0 ? "01 / ARMAMENTS" : "02 / RATIONS", D(-11f, 2.5f, z), z < 0 ? 0f : 180f, 4f, 0.55f);
                // Sign hangers terminate at the roof, well above movement and line-of-fire space.
                foreach (float dx in new[] { -1.7f, 1.7f })
                    Rod(D(-11f + dx, 2.8f, z), D(-11f + dx, 6.5f, z), 0.02f, DepotIron, Surface.Metal);
            }
            PlacePackItem(SfAmmo, D(12.5f, 1.44f, -3.5f), Quaternion.identity, false);
            PlacePackItem(AppleCrate, D(13.5f, 1.44f, -3.5f), Quaternion.identity, false);
            LanternOn(D(12f, 1.44f, -3.5f), Quaternion.identity, true);
            PlacePackItem(Pail, D(18f, 4.82f, 10.65f), Quaternion.identity, false);
            LanternOn(D(19.4f, 4.82f, 10.65f), Quaternion.identity, true);
            // Splintered packaging and a dragged stain tell the story of the abandoned cargo.
            for (int i = 0; i < 8; i++)
                Deco(D(15.2f + i * 0.17f, 0.035f, -4.6f + (i % 3) * 0.2f), new Vector3(0.12f, 0.06f, 0.6f),
                    Quaternion.Euler(0f, i * 31f, 0f), DepotOak, Surface.Wood);
            Splat(D(14.7f, 0f, -4.5f), 2.8f, 0.65f, -15f, 0.012f, new Color(0.15f, 0.07f, 0.045f), Surface.Dirt);
        }

        void DepotArchitecture()
        {
            // Purpose-built ceiling: dark timber bays around a long luminous clerestory.
            // Visual roof stays at the existing 7m collision plane, preserving neighboring rooms.
            for (float x = -19.5f; x < 21f; x += 3f)
            {
                foreach (float z in new[] { -7.25f, 7.25f })
                    Deco(D(x, 7.15f, z), new Vector3(3f, 0.3f, 9.5f), DepotOak, Surface.Wood);
                Glow(D(x, 7.15f, 0f), new Vector3(2.88f, 0.3f, 4.85f), Quaternion.identity, new Color(0.16f, 0.25f, 0.3f));
                Deco(D(x - 1.5f, 6.98f, 0f), new Vector3(0.12f, 0.16f, 5f), DepotIron, Surface.Metal);
            }
            foreach (float z in new[] { -2.5f, 2.5f })
                Deco(D(0f, 6.9f, z), new Vector3(42f, 0.22f, 0.18f), DepotIron, Surface.Metal);
            foreach (float x in new[] { -18f, -9f, 0f, 9f, 18f })
            {
                Deco(D(x, 6.25f, 0f), new Vector3(0.25f, 0.32f, 23.3f), DepotIron, Surface.Metal);
                foreach (float side in new[] { -1f, 1f })
                {
                    Bar(D(x, 6.25f, side * 4f), D(x, 6.9f, side * 11.5f), 0.2f, 0.2f, Vector3.up, DepotIron, Surface.Metal);
                    // High corbels avoid doors, windows and the wall-mounted purchases below.
                    Deco(D(x, 5.5f, side * 11.45f), new Vector3(0.7f, 2.4f, 0.45f), DepotStone, Surface.Ashlar);
                }
            }
            // Two long cornices visually unify the hall; worn brick and limestone remain below.
            foreach (float z in new[] { -11.65f, 11.65f })
                Deco(D(0f, 4.2f, z), new Vector3(41.2f, 0.18f, 0.12f), DepotStone, Surface.Ashlar);
            DepotSign("THE DEPOT", D(-20.6f, 4.7f, 0f), 90f, 6f, 0.9f);
            DepotSign("DISPATCH / EAST DOCK", D(20.6f, 4.7f, -6.8f), 270f, 5.5f, 0.65f);
        }

        void DepotCrane()
        {
            // A fixed gantry above the crossing. Nothing suspended below 4.5m can snag a player.
            foreach (float z in new[] { -3.5f, 3.5f })
            {
                Deco(D(-1f, 5.75f, z), new Vector3(34f, 0.3f, 0.25f), DepotIron, Surface.Metal);
                foreach (float x in new[] { -16f, -6f, 4f, 14f })
                    Rod(D(x, 5.9f, z), D(x, 6.9f, z), 0.08f, DepotIron, Surface.Metal);
            }
            Deco(D(-1f, 5.55f, 0f), new Vector3(1.4f, 0.38f, 7.4f), DepotBrass, Surface.Rust);
            foreach (float z in new[] { -3.5f, 3.5f })
                DecoPrim(PrimitiveType.Cylinder, D(-1f, 5.8f, z), new Vector3(0.5f, 0.17f, 0.5f), Quaternion.Euler(90f, 0f, 0f), DepotIron, Surface.Metal);
            Deco(D(-1f, 5.2f, 0f), new Vector3(1.1f, 0.4f, 0.9f), DepotIron, Surface.Metal);
            foreach (float x in new[] { -1.25f, -0.75f })
                Rod(D(x, 5.1f, 0f), D(x, 4.55f, 0f), 0.045f, DepotBrass, Surface.Metal);
            // Curved cargo hook, assembled in the existing batched decoration mesh.
            for (int i = 0; i < 9; i++)
            {
                float a = (i * 25f - 90f) * Mathf.Deg2Rad;
                float b = ((i + 1) * 25f - 90f) * Mathf.Deg2Rad;
                Rod(D(-1f + Mathf.Cos(a) * 0.22f, 4.7f + Mathf.Sin(a) * 0.22f, 0f),
                    D(-1f + Mathf.Cos(b) * 0.22f, 4.7f + Mathf.Sin(b) * 0.22f, 0f), 0.07f, DepotIron, Surface.Metal);
            }
        }

        void DepotGallery()
        {
            foreach (float z in new[] { 6.1f, 11.4f })
                Deco(D(13.3f, 3.52f, z), new Vector3(14.6f, 0.26f, 0.22f), DepotIron, Surface.Metal);
            foreach (float x in new[] { 6.3f, 11f, 20.3f })
                foreach (float z in new[] { 6.3f, 11.3f })
                    Bar(D(x, 2.75f, z), D(x + (x > 15f ? -0.8f : 0.8f), 3.6f, z), 0.12f, 0.12f, Vector3.up, DepotIron, Surface.Metal);
            DepotSign("03 / BONDED CARGO", D(17.8f, 3.35f, 5.86f), 180f, 4.5f, 0.5f);
            DepotSign("DISPATCH OFFICE", D(18.2f, 5.8f, 11.48f), 180f, 4.5f, 0.5f);
            // A large manifest board makes the upper room read as an office without covering the beacon.
            Deco(D(9f, 5.35f, 11.42f), new Vector3(4f, 1.5f, 0.12f), DepotOak, Surface.Wood);
            for (int i = 0; i < 6; i++)
                Deco(D(7.5f + i * 0.6f, 5.35f + (i % 2) * 0.16f, 11.34f), new Vector3(0.4f, 0.7f, 0.015f),
                    Quaternion.Euler(0f, 0f, i % 2 == 0 ? -4f : 6f), new Color(0.62f, 0.56f, 0.4f), Surface.Fabric);
            // Stair handrails stop at the gallery opening, leaving a 2.6m clear flight.
            foreach (float x in new[] { 6.45f, 9.15f })
                Bar(D(x, 0.95f, 0.75f), D(x, 4.85f, 6f), 0.08f, 0.08f, Vector3.up, DepotIron, Surface.Metal);
        }

        void DepotLighting()
        {
            foreach (float x in new[] { -16f, -7f, 3f, 16f })
            {
                var at = D(x, 5.5f, x == 16f ? -5f : 0f);
                Rod(at + Vector3.up, at, 0.025f, DepotIron, Surface.Metal);
                LanternOn(at - Vector3.up * 0.6f, Quaternion.identity, true, 1.2f);
                DepotLight(at - Vector3.up * 0.8f, 12f, 1.35f, new Color(1f, 0.7f, 0.4f));
            }
            DepotLight(D(-3f, 5.8f, 0f), 19f, 0.65f, new Color(0.43f, 0.67f, 0.85f));
            DepotLight(D(15f, 5.9f, 9f), 10f, 1.2f, new Color(0.6f, 0.76f, 0.87f));
            foreach (float x in new[] { 10f, 18f })
            {
                LanternOn(D(x, 2.7f, 9f), Quaternion.identity, true);
                Rod(D(x, 3.64f, 9f), D(x, 3.25f, 9f), 0.03f, DepotIron, Surface.Metal);
                DepotLight(D(x, 2.7f, 9f), 6f, 0.9f, new Color(1f, 0.64f, 0.3f));
            }
        }

        void DepotLight(Vector3 at, float range, float intensity, Color color)
        {
            var light = new GameObject("Depot light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        void DepotSign(string text, Vector3 at, float yaw, float width, float height)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, height, 0.06f), rotation, DepotIron, Surface.Metal);
            var holder = Greybox.Empty("Depot sign", Root, at + rotation * Vector3.forward * 0.04f);
            holder.localRotation = rotation;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.8f, 0.73f, 0.53f), height * 0.43f);
        }
    }
}
