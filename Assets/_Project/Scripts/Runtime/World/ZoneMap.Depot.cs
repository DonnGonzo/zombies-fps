using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        // An abandoned quartermaster's store. Keep the original rack footprints, loft, ladder,
        // mission beacon and part positions; all stock is contained inside those solid footprints.
        const int DepotZone = 1;
        static readonly Color DepotOak = new Color(0.34f, 0.23f, 0.13f);
        static readonly Color DepotIron = new Color(0.17f, 0.21f, 0.2f);
        static readonly Color DepotStone = new Color(0.46f, 0.43f, 0.35f);
        static Vector3 D(float x, float y, float z) => L(DepotZone, x, y, z);

        void BuildDepot()
        {
            Block("Depot bonded stores / loft", DepotZone, 10f, 6f, 20.7f, 11.7f, 4f, DepotStone, Surface.Masonry);
            AddLadder(DepotZone, 15f, 6f, Vector3.back, 4f);
            DepotRack(-10f, -5f, 14f, "01 / PROVISIONS");
            DepotRack(-10f, 4f, 14f, "02 / FIELD SUPPLIES");
            DepotRack(8f, -6f, 8f, "03 / SEALED CARGO");
            // Replaces the old crate pile with the same small corner devoted to packing supplies.
            PlacePackItem(Workbench, D(-18f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(Crate, D(-18f, 0f, 6f), Quaternion.Euler(0f, 12f, 0f), true);
            PlacePackItem(BarrelRack, D(-18f, 0f, 10f), Quaternion.identity, true);
        }

        void DepotRack(float x, float z, float width, string title)
        {
            // The solid back and closed lower cupboards make the continuous collision volume legible.
            var blocker = Greybox.Box("Depot stock rack", Root, D(x, 1.2f, z), new Vector3(width, 2.4f, 1f), DepotOak, surface: Surface.Wood);
            blocker.GetComponent<Renderer>().enabled = false;
            Deco(D(x, 0.43f, z), new Vector3(width, 0.86f, 1f), DepotOak, Surface.Wood);
            Deco(D(x, 1.6f, z), new Vector3(width, 1.6f, 0.12f), DepotOak, Surface.Wood);
            int bays = Mathf.RoundToInt(width / 2f);
            float bay = width / bays;
            for (int i = 0; i <= bays; i++)
                foreach (float side in new[] { -0.48f, 0.48f })
                    Deco(D(x - width / 2f + i * bay, 1.2f, z + side), new Vector3(0.1f, 2.4f, 0.1f), DepotIron, Surface.Rust);
            foreach (float y in new[] { 0.87f, 1.95f, 2.36f })
                Deco(D(x, y, z), new Vector3(width, 0.08f, 1f), DepotOak, Surface.Wood);
            for (int i = 0; i < bays; i++)
            {
                float bx = x - width / 2f + (i + 0.5f) * bay;
                // Full-depth supplies fill each bay; smaller packs sit on the top shelf.
                PlacePackItem(i % 3 == 0 ? IronCrate : WoodCrate, D(bx - 0.47f, 0.92f, z), Quaternion.identity, false);
                PlacePackItem(i % 2 == 0 ? Sack : KitBarrel, D(bx + 0.45f, 0.92f, z), Quaternion.identity, false);
                PlacePackItem(AppleCrate, D(bx, 1.99f, z), Quaternion.identity, false);
                foreach (float side in new[] { -0.51f, 0.51f })
                {
                    Deco(D(bx, 0.45f, z + side), new Vector3(bay - 0.15f, 0.63f, 0.03f), DepotOak, Surface.Wood);
                    Deco(D(bx, 0.6f, z + side * 1.02f), new Vector3(0.22f, 0.045f, 0.04f), DepotIron, Surface.Metal);
                }
            }
            DepotSign(title, D(x, 2.16f, z + 0.54f), 0f, Mathf.Min(width - 0.4f, 4.8f), 0.32f);
            DepotSign(title, D(x, 2.16f, z - 0.54f), 180f, Mathf.Min(width - 0.4f, 4.8f), 0.32f);
        }

        void DepotMakeover()
        {
            // The wide east-west aisle and north/south crossing stay clear for fighting and door access.
            foreach (float z in new[] { -2.3f, 1.6f })
                Paint(DepotZone, -19f, z, 18f, z, 0.08f, FadedWhite);
            for (float x = -18f; x <= 18f; x += 3f)
                Deco(D(x, 0.006f, -0.35f), new Vector3(0.7f, 0.01f, 0.09f), DepotStone, Surface.Masonry);

            // Heavy roof ties and diagonal braces give the low warehouse a recognizable silhouette.
            foreach (float x in new[] { -18f, -9f, 0f, 9f, 18f })
            {
                Deco(D(x, 5.95f, 0f), new Vector3(0.26f, 0.34f, 23.2f), DepotOak, Surface.Wood);
                foreach (float side in new[] { -1f, 1f })
                {
                    Bar(D(x, 5.95f, side * 7f), D(x, 6.75f, side * 11.4f), 0.18f, 0.18f, Vector3.up, DepotOak, Surface.Wood);
                    Deco(D(x, 5.95f, side * 7f), new Vector3(0.29f, 0.4f, 0.3f), DepotIron, Surface.Rust);
                }
            }
            // Three warm pendant pools across the main aisle; a cooler pool marks the loft ladder.
            foreach (float x in new[] { -13f, 0f, 13f })
            {
                Rod(D(x, 6.8f, -0.3f), D(x, 4.8f, -0.3f), 0.035f, DepotIron, Surface.Metal);
                LanternOn(D(x, 4.05f, -0.3f), Quaternion.identity, true, 1.4f);
                ChapelLight(D(x, 3.9f, -0.3f), 14f, 1.4f, new Color(1f, 0.73f, 0.42f));
            }
            ChapelLight(D(15f, 5.7f, 5f), 10f, 1.1f, new Color(0.56f, 0.73f, 0.88f));

            DepotLoft();
            // An interrupted packing job using Ten Worlds, Broken Furniture and Fantasy props.
            PlacePackItem(AppleCrate, D(-18f, 0.89f, 8f), Quaternion.Euler(0f, 90f, 0f), false);
            LanternOn(D(-18f, 0.89f, 8.7f), Quaternion.identity, true);
            PlacePackItem(Sack, D(-17.6f, 0f, 9.2f), Quaternion.Euler(0f, 20f, 0f), true);
            DepotSign("QUARTERMASTER / DISPATCH", D(-18f, 2.75f, 10.9f), 180f, 4.8f, 0.6f);
            // Scraps remain below step height and cannot obstruct the nearby collectible at (-18, -9).
            for (int i = 0; i < 7; i++)
                Deco(D(-18.7f + i * 0.25f, 0.04f, 5.4f + (i % 3) * 0.28f), new Vector3(0.13f, 0.06f, 0.7f), Quaternion.Euler(0f, i * 37f, 0f), DepotOak, Surface.Wood);
            Splat(D(-17.5f, 0f, 7f), 2f, 1.2f, 15f, 0.008f, new Color(0.12f, 0.09f, 0.06f), Surface.Dirt);
        }

        void DepotLoft()
        {
            // Dress the solid loft as locked bonded stores, with a timber deck above.
            Deco(D(15.35f, 4.025f, 8.85f), new Vector3(10.7f, 0.05f, 5.7f), DepotOak, Surface.Wood);
            foreach (float x in new[] { 11.8f, 18.5f })
            {
                Deco(D(x, 1.65f, 5.97f), new Vector3(2.6f, 3.3f, 0.05f), DepotIron, Surface.Metal);
                for (int i = 0; i < 10; i++)
                    Deco(D(x, 0.18f + i * 0.32f, 5.92f), new Vector3(2.6f, 0.055f, 0.05f), DepotOak, Surface.Wood);
                Deco(D(x, 1.4f, 5.87f), new Vector3(0.28f, 0.09f, 0.04f), DepotStone, Surface.Metal);
            }
            DepotSign("BONDED STORES", D(15.35f, 3.64f, 5.9f), 180f, 5.4f, 0.48f);
            // Beam ends and iron straps articulate the front without enclosing the ladder landing.
            foreach (float x in new[] { 10.2f, 13.2f, 17f, 20.5f })
            {
                Deco(D(x, 2f, 5.91f), new Vector3(0.18f, 4f, 0.16f), DepotOak, Surface.Wood);
                Deco(D(x, 3.8f, 5.8f), new Vector3(0.23f, 0.32f, 0.07f), DepotIron, Surface.Rust);
            }
            // Leave the beacon (16,10), part (13,9) and ladder top (15,6) unobstructed.
            PlacePackItem(ReadingTable, D(19f, 4.05f, 10.6f), Quaternion.identity, true);
            PlacePackItem(KitStool, D(19f, 4.05f, 9.4f), Quaternion.identity, true);
            PlacePackItem(WoodChest, D(11.2f, 4.05f, 10.8f), Quaternion.identity, true);
            LanternOn(D(19.7f, 4.87f, 10.6f), Quaternion.identity, true);
            PlacePackItem(Pail, D(18.5f, 4.87f, 10.6f), Quaternion.identity, false);
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
