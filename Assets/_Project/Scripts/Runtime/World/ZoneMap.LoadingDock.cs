using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        const int LoadingDockZone = 6;
        const float LoadingApron = 1.4f, DispatchDeck = 3.8f;
        static readonly Color DockSteel = new Color(0.17f, 0.25f, 0.26f);
        static readonly Color DockRust = new Color(0.48f, 0.25f, 0.12f);
        static readonly Color DockStone = new Color(0.4f, 0.42f, 0.41f);
        static Vector3 LD(float x, float y, float z) => L(LoadingDockZone, x, y, z);

        void BuildLoadingDock()
        {
            // Split loading aprons create a ground-level crossover through the long eastern arm.
            Block("South receiving apron", LoadingDockZone, 9f, -8f, 14.7f, 7f, LoadingApron, DockStone);
            Block("North dispatch apron", LoadingDockZone, 9f, 12f, 14.7f, 26f, LoadingApron, DockStone);
            Stairs(LoadingDockZone, 9f, -4f, Vector3.left, LoadingApron, 2.6f);
            Stairs(LoadingDockZone, 11.8f, 7f, Vector3.forward, LoadingApron, 2.6f);
            Stairs(LoadingDockZone, 11.8f, 12f, Vector3.back, LoadingApron, 2.6f);
            Stairs(LoadingDockZone, 11.8f, 26f, Vector3.forward, LoadingApron, 2.6f);

            // The existing truck-roof part stays at (-6.5,3,-3). Its roof and ladder remain real geometry.
            ImportedFreightTruck();
            AddLadder(LoadingDockZone, -6.5f, -6f, Vector3.back, 3f);
            Jump(LoadingDockZone, -6.5f, 3f, 0.3f, -6.5f, 2.4f, 2.2f);

            // Containers leave the western exits, north-west mission generator and southern perk clear.
            Block("Sealed freight container", LoadingDockZone, -8f, 6f, -2f, 8.6f, 2.6f, DockSteel, Surface.Metal);
            Block("East freight container", LoadingDockZone, 1.6f, -6f, 6.4f, -3.4f, 2.6f, DockRust, Surface.Rust);
            AddLadder(LoadingDockZone, 4f, -3.4f, Vector3.forward, 2.6f);
            // Discrete model colliders, rather than invisible solid cargo aisles.
            PlacePackItem(SfTarp, LD(3f, 0f, 5.5f), Quaternion.identity, true);
            PlacePackItem(SfCrate, LD(5.7f, 0f, 5.8f), Quaternion.Euler(0f, 12f, 0f), true);
            PlacePackItem(SfBarrel, LD(13.4f, LoadingApron, 1f), Quaternion.identity, true);
            PlacePackItem(SfChest, LD(13.4f, LoadingApron, 3.5f), Quaternion.identity, true);
            PlacePackItem(SfCrateLong, LD(12f, LoadingApron, 14f), Quaternion.identity, true);
            // Keep the part at (12,1.4,-5), and the ground part at (-13,0,-8), free of cargo.

            // Dispatch lookout has >2m clearance above the apron and a full stair route for zombies.
            Deck("Dispatch lookout", LoadingDockZone, 9f, 17f, 14.6f, 23f, DispatchDeck, DockSteel);
            Stairs(LoadingDockZone, 9f, 20f, Vector3.left, DispatchDeck, 2.8f);
            LoadingRail(9.1f, 17.1f, 14.4f, 17.1f);
            LoadingRail(9.1f, 22.9f, 14.4f, 22.9f);
            LoadingRail(9.1f, 17.1f, 9.1f, 18.5f);
            LoadingRail(9.1f, 21.5f, 9.1f, 22.9f);
            PlacePackItem(SfDeskSmall, LD(13.6f, DispatchDeck, 20f), Quaternion.Euler(0f, 270f, 0f), true);
            // Canopy posts sit at the back of the apron, outside the collectible and walking lanes.
            foreach (float z in new[] { -7f, 5.5f, 13f, 24f })
                Greybox.Box("Dock canopy column", Root, LD(14.1f, 3.3f, z), new Vector3(0.24f, 6.6f, 0.24f), DockSteel, surface: Surface.Metal);
        }

        void LoadingDockMakeover()
        {
            if (!_importedFreightTruck) TruckDetails();
            ContainerDetails(LoadingDockZone, -8f, 6f, -2f, 8.6f, 2.6f, DockSteel);
            ContainerDetails(LoadingDockZone, 1.6f, -6f, 6.4f, -3.4f, 2.6f, DockRust);
            LoadingCanopies();
            LoadingLane();
            // Bays share a consistent visual language: shutter ribs, bumpers, numbering and edge markings.
            foreach (var bay in new[] { (z: 0f, name: "01 / RECEIVING"), (z: 15f, name: "02 / DISPATCH") })
            {
                Deco(LD(14.61f, 3.1f, bay.z), new Vector3(0.1f, 3.3f, 4.4f), DockSteel, Surface.Metal);
                for (int i = 0; i < 13; i++)
                    Deco(LD(14.52f, 1.6f + i * 0.24f, bay.z), new Vector3(0.06f, 0.06f, 4.4f), DockRust, Surface.Rust);
                LoadingSign(bay.name, LD(14.45f, 5.1f, bay.z), 270f, 4.5f, 0.65f);
            }
            LoadingSign("LOADING DOCK", LD(-14.55f, 4.5f, 0f), 90f, 6.4f, 0.85f);
            LoadingSign("PUMP STATION / NORTH", LD(7f, 4.8f, 31.5f), 180f, 6f, 0.55f);
            LoadingSign("DISPATCH CONTROL", LD(14.45f, 5.8f, 20f), 270f, 4.8f, 0.5f);
            foreach (var span in new[] { (start: -7.8f, end: 6.8f), (start: 12.2f, end: 25.8f) })
                for (float z = span.start; z < span.end; z += 0.6f)
                {
                    if (Mathf.Abs(z + 4f) < 1.6f || Mathf.Abs(z - 20f) < 1.7f) continue;
                    Deco(LD(9.18f, LoadingApron + 0.006f, z), new Vector3(0.3f, 0.012f, 0.52f),
                        Mathf.RoundToInt(z / 0.6f) % 2 == 0 ? HazardYellow : Soot, Surface.Concrete);
                }
            foreach (float z in new[] { -7f, 0f, 4f, 14f, 24f })
                Deco(LD(8.94f, 0.8f, z), new Vector3(0.12f, 0.55f, 0.6f), Soot, Surface.Rubber);
            // Scanned timber pallets, torn packing material and salvaged machinery from the installed kits.
            PlacePackItem(SfAmmo, LD(13.6f, DispatchDeck + 0.94f, 20f), Quaternion.identity, false);
            PlacePackItem(Crate, LD(3f, 0f, 8f), Quaternion.Euler(0f, 20f, 0f), true);
            for (int i = 0; i < 7; i++)
                Deco(LD(2.4f + i * 0.21f, 0.03f, 7.1f + (i % 2) * 0.25f), new Vector3(0.1f, 0.05f, 0.6f),
                    Quaternion.Euler(0f, i * 33f, 0f), PropWood, Surface.Wood);
            LoadingFloodlight(LD(-11f, 5.8f, -1f), new Color(0.52f, 0.7f, 0.85f), 17f, true);
            LoadingFloodlight(LD(5f, 5.8f, 3f), new Color(0.65f, 0.78f, 0.88f), 16f, true);
            LoadingFloodlight(LD(5f, 5.8f, 26f), new Color(0.52f, 0.7f, 0.85f), 14f, true);
            foreach (float z in new[] { -3f, 9f, 21f })
                LoadingFloodlight(LD(11.5f, 6f, z), new Color(1f, 0.68f, 0.33f), 12f);
        }

        void LoadingCanopies()
        {
            // The yard is open to the sky. Only the east bays have a physical metal awning.
            for (float z = -6f; z <= 24f; z += 6f)
            {
                Greybox.Box("Dock canopy roof", Root, LD(11.15f, 6.7f, z), new Vector3(6.7f, 0.18f, 6f), DockSteel, surface: Surface.Metal);
                for (int rib = 0; rib < 12; rib++)
                    Deco(LD(11.15f, 6.59f, z - 2.75f + rib * 0.5f), new Vector3(6.7f, 0.08f, 0.06f), DockRust, Surface.Rust);
                Bar(LD(14.1f, 5.2f, z), LD(8f, 6.55f, z), 0.16f, 0.16f, Vector3.up, DockSteel, Surface.Metal);
            }
            Deco(LD(7.85f, 6.55f, 9f), new Vector3(0.18f, 0.35f, 36f), DockRust, Surface.Rust);
            // Downpipes drain to the loading platform, away from the ladder and the dock part.
            foreach (float z in new[] { -7f, 24f })
                Rod(LD(14.4f, LoadingApron, z), LD(14.4f, 6.65f, z), 0.12f, DockRust, Surface.Rust);
        }

        void LoadingLane()
        {
            foreach (float x in new[] { -9.5f, -3.5f })
                Paint(LoadingDockZone, x, -8f, x, 4.5f, 0.1f, FadedWhite);
            // Dashed transit lane bends around the truck into the northbound arm.
            for (float z = -1f; z < 30f; z += 3f)
                Paint(LoadingDockZone, 0.5f, z, 0.5f, z + 1.2f, 0.12f, HazardYellow);
            // Drain grate stays flush; narrow ribs and dark puddles catch the bay lights.
            Deco(LD(7.9f, 0.006f, 10f), new Vector3(0.38f, 0.012f, 34f), Soot, Surface.Metal);
            for (float z = -6.5f; z < 27f; z += 0.42f)
                Deco(LD(7.9f, 0.017f, z), new Vector3(0.4f, 0.018f, 0.055f), DockSteel, Surface.Metal);
            foreach (var p in new[] { new Vector2(-11f, 1f), new Vector2(-1f, -6.5f), new Vector2(4.5f, 11f), new Vector2(4.5f, 26.5f) })
                Splat(LD(p.x, 0f, p.y), 2.4f, 0.9f, p.x * 7f, 0.008f, new Color(0.09f, 0.13f, 0.15f), Surface.Metal);
        }

        void LoadingRail(float x0, float z0, float x1, float z1)
        {
            var a = LD(x0, DispatchDeck, z0);
            var b = LD(x1, DispatchDeck, z1);
            var direction = b - a;
            foreach (float y in new[] { 0.45f, 1f })
            {
                var go = Greybox.Box("Dock lookout rail", Root, (a + b) / 2f + Vector3.up * y,
                    new Vector3(0.08f, 0.08f, direction.magnitude), DockSteel, surface: Surface.Metal);
                go.transform.localRotation = Quaternion.LookRotation(direction);
            }
            int count = Mathf.CeilToInt(direction.magnitude / 1.2f);
            for (int i = 0; i <= count; i++)
                Greybox.Box("Dock rail post", Root, Vector3.Lerp(a, b, (float)i / count) + Vector3.up * 0.5f,
                    new Vector3(0.09f, 1f, 0.09f), DockSteel, surface: Surface.Metal);
        }

        void LoadingFloodlight(Vector3 at, Color tint, float range, bool mast = false)
        {
            if (mast)
                Greybox.Box("Dock lighting mast", Root, new Vector3(at.x + 0.6f, at.y / 2f, at.z),
                    new Vector3(0.16f, at.y, 0.16f), DockSteel, surface: Surface.Metal);
            if (mast) Rod(at + Vector3.right * 0.6f, at, 0.08f, DockSteel, Surface.Metal);
            Deco(at, new Vector3(0.85f, 0.2f, 0.6f), DockSteel, Surface.Metal);
            Glow(at - Vector3.up * 0.11f, new Vector3(0.7f, 0.035f, 0.45f), Quaternion.identity, tint);
            Rod(at + Vector3.up * 0.1f, at + Vector3.up * 0.55f, 0.05f, DockSteel, Surface.Metal);
            var lamp = new GameObject("Dock floodlight").AddComponent<Light>();
            lamp.transform.SetParent(Root, false);
            lamp.transform.localPosition = at - Vector3.up * 0.2f;
            lamp.type = LightType.Point;
            lamp.range = range;
            lamp.intensity = 1.6f;
            lamp.color = tint;
            lamp.shadows = LightShadows.None;
        }

        void LoadingSign(string text, Vector3 at, float yaw, float width, float height)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, height, 0.06f), rot, DockSteel, Surface.Metal);
            var holder = Greybox.Empty("Dock sign", Root, at + rot * Vector3.forward * 0.04f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.88f, 0.78f, 0.51f), height * 0.43f);
        }
    }
}
