using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        const int GarageZone = 2;
        const float GarageDeck = 4.5f;
        static readonly Color GarageTeal = new Color(0.17f, 0.29f, 0.27f);
        static readonly Color GarageSteel = new Color(0.22f, 0.24f, 0.25f);
        static readonly Color GarageOrange = new Color(0.62f, 0.3f, 0.1f);
        static Vector3 GR(float x, float y, float z) => L(GarageZone, x, y, z);

        void BuildGarage()
        {
            // Original build-station and beacon anchors remain on the 4.5m deck. Below it is now a real workshop.
            Deck("Garage fabrication gallery", GarageZone, 9f, 7f, 16.7f, 14.7f, GarageDeck, GarageSteel);
            Stairs(GarageZone, 9f, 11f, Vector3.left, GarageDeck, 2.8f);
            AddLadder(GarageZone, 12f, 7f, Vector3.back, GarageDeck);
            GarageRail(9.1f, 7.1f, 11.1f, 7.1f);
            GarageRail(12.9f, 7.1f, 16.5f, 7.1f);
            GarageRail(9.1f, 7.1f, 9.1f, 9.5f);
            GarageRail(9.1f, 12.5f, 9.1f, 14.5f);

            // Two distinct service islands west of the central travel lane.
            GarageBox("Hydraulic lift bed", -8f, 0.55f, -6f, 2.7f, 0.2f, 5.2f, GarageSteel);
            GarageBox("Vehicle on lift", -8f, 1.4f, -6f, 2f, 1.5f, 4.2f, GarageTeal);
            foreach (float x in new[] { -10f, -6f })
            {
                GarageBox("Lift column", x, 1.7f, -6f, 0.38f, 3.4f, 0.48f, GarageOrange);
                GarageBox("Lift foot", x, 0.08f, -6f, 0.85f, 0.16f, 1f, GarageSteel);
                GarageBox("Lift arm", (x - 8f) / 2f, 0.4f, -6f, 2f, 0.2f, 0.25f, GarageSteel);
            }
            PlacePackItem(Workbench, GR(-9f, 0f, 5f), Quaternion.identity, true);
            PlacePackItem(AnvilLog, GR(-6.3f, 0f, 5f), Quaternion.Euler(0f, 20f, 0f), true);
            PlacePackItem(Grindstone, GR(-11.5f, 0f, 5f), Quaternion.identity, true);
            PlacePackItem(SfLocker, GR(-15.7f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(SfShelfWide, GR(-15.7f, 0f, 2.8f), Quaternion.Euler(0f, 90f, 0f), true);
            // The eastern ground-floor route connects Pump Station, generator and underside of the deck.
            PlacePackItem(SfDesk, GR(14.7f, 0f, 12f), Quaternion.Euler(0f, 270f, 0f), true);
            PlacePackItem(SfChest, GR(10.8f, 0f, 13.3f), Quaternion.identity, true);
            PlacePackItem(SfCrate, GR(7f, 0f, -5f), Quaternion.identity, true);
            PlacePackItem(SfBarrel, GR(10f, 0f, -5f), Quaternion.identity, true);
            // Both western pickups (-13,-11 / -13,11), wall gun (-17,-6), and south generator (10,-15) stay clear.
        }

        void GarageBox(string name, float x, float y, float z, float sx, float sy, float sz, Color color)
        {
            Greybox.Box("Garage " + name, Root, GR(x, y, z), new Vector3(sx, sy, sz), color, surface: Surface.Metal);
        }

        void GarageMakeover()
        {
            GarageRoof();
            GarageServiceBay();
            GarageFabrication();
            GarageLighting();
            // Broad marked crossing connects all four doors. Bay borders stop short of those routes.
            foreach (float x in new[] { -2.4f, 2.4f })
                Paint(GarageZone, x, -13f, x, 7f, 0.09f, FadedWhite);
            foreach (float z in new[] { -2f, 2f })
                Paint(GarageZone, -14f, z, 15f, z, 0.08f, FadedWhite);
            foreach (float x in new[] { -11.5f, -4.5f })
                Paint(GarageZone, x, -10f, x, -2.7f, 0.12f, HazardYellow);
            Paint(GarageZone, -11.5f, -10f, -4.5f, -10f, 0.12f, HazardYellow);
            foreach (float z in new[] { 3.5f, 7f })
                Paint(GarageZone, -12.5f, z, -4.5f, z, 0.1f, HazardYellow);
            // Drainage channel in the repair bay and oil dragged out from the lift.
            Deco(GR(-8f, 0.008f, -11f), new Vector3(5.5f, 0.016f, 0.35f), Soot, Surface.Metal);
            for (int i = 0; i < 18; i++)
                Deco(GR(-10.6f + i * 0.3f, 0.018f, -11f), new Vector3(0.055f, 0.02f, 0.38f), GarageSteel, Surface.Metal);
            Splat(GR(-8f, 0f, -9f), 2.3f, 1f, 15f, 0.009f, new Color(0.045f, 0.04f, 0.03f), Surface.Metal);
            Splat(GR(-6f, 0f, -10.4f), 1.4f, 0.45f, -30f, 0.009f, new Color(0.065f, 0.05f, 0.03f), Surface.Metal);
            GarageSign("GARAGE / REPAIR WORKS", GR(0f, 4.8f, -14.5f), 0f, 7.2f, 0.8f);
            GarageSign("01 / VEHICLE SERVICE", GR(-8f, 4.4f, -10.5f), 0f, 5.4f, 0.55f);
            foreach (float x in new[] { -10.3f, -5.7f })
                Rod(GR(x, 4.68f, -10.5f), GR(x, 6.5f, -10.5f), 0.025f, GarageSteel, Surface.Metal);
            GarageSign("02 / FABRICATION", GR(-9f, 3.2f, 7.4f), 180f, 4.8f, 0.6f);
            foreach (float x in new[] { -11f, -7f })
                Rod(GR(x, 3.5f, 7.4f), GR(x, 6.5f, 7.4f), 0.025f, GarageSteel, Surface.Metal);
            GarageSign("BUILD STATION", GR(13f, 4f, 6.85f), 180f, 5f, 0.55f);
        }

        void GarageServiceBay()
        {
            CarDetails(GR(-8f, 0.65f, -6f), new Vector3(2f, 1.5f, 4.2f), true, new System.Random(2702));
            foreach (float x in new[] { -10f, -6f })
            {
                Rod(GR(x, 0.2f, -5.72f), GR(x, 3.2f, -5.72f), 0.09f, new Color(0.55f, 0.58f, 0.58f), Surface.Metal);
                Deco(GR(x, 1.25f, -5.67f), new Vector3(0.24f, 0.38f, 0.09f), GarageTeal, Surface.Metal);
                Glow(GR(x, 1.3f, -5.61f), new Vector3(0.07f, 0.07f, 0.02f), Quaternion.identity, new Color(0.5f, 0.8f, 0.3f));
            }
            // Overhead service rail follows the repair bay; the hoist never intrudes below player head height.
            Deco(GR(-8f, 5.6f, -6f), new Vector3(0.24f, 0.3f, 11f), GarageOrange, Surface.Rust);
            foreach (float z in new[] { -11f, -1f })
                Rod(GR(-8f, 5.75f, z), GR(-8f, 6.8f, z), 0.08f, GarageSteel, Surface.Metal);
            Deco(GR(-8f, 5.3f, -5f), new Vector3(0.9f, 0.35f, 0.8f), GarageSteel, Surface.Metal);
            foreach (float x in new[] { -8.25f, -7.75f })
                Rod(GR(x, 5.1f, -5f), GR(x, 3.1f, -5f), 0.035f, GarageSteel, Surface.Metal);
            Bar(GR(-8.35f, 3.1f, -5f), GR(-7.65f, 3.1f, -5f), 0.1f, 0.1f, Vector3.up, GarageOrange, Surface.Metal);
        }

        void GarageFabrication()
        {
            // Supplied Fantasy, Sci-Fi, Ten Worlds and Broken Furniture props create a workshop in use.
            PlacePackItem(SfAmmo, GR(-9.5f, 0.89f, 5f), Quaternion.identity, false);
            PlacePackItem(IronPail, GR(-8.6f, 0.89f, 5f), Quaternion.identity, false);
            PlacePackItem(Crate, GR(-5.4f, 0f, 6.5f), Quaternion.Euler(0f, 15f, 0f), true);
            PlacePackItem(Worktable, GR(12f, 0f, 9.5f), Quaternion.identity, true);
            PlacePackItem(SfAmmo, GR(12f, 1.44f, 9.5f), Quaternion.identity, false);
            LanternOn(GR(11.1f, 1.44f, 9.5f), Quaternion.identity, true);
            // Tool board is mounted to the existing west wall, away from the wall-buy interaction.
            Deco(GR(-16.55f, 2.4f, 3f), new Vector3(0.12f, 1.7f, 4.4f), GarageTeal, Surface.Metal);
            for (int i = 0; i < 7; i++)
            {
                float z = 1.3f + i * 0.5f;
                Rod(GR(-16.43f, 1.85f, z), GR(-16.43f, 2.65f, z), 0.045f, GarageOrange, Surface.Metal);
                Deco(GR(-16.4f, 2.65f, z), new Vector3(0.08f, 0.12f, 0.22f), GarageSteel, Surface.Metal);
            }
            // Apron trim, steel knee braces and stair indication make the new open deck read from below.
            Deco(GR(12.85f, 4.1f, 7.08f), new Vector3(7.7f, 0.25f, 0.15f), GarageTeal, Surface.Metal);
            foreach (float x in new[] { 9.25f, 16.45f })
                Bar(GR(x, 3.1f, 7.25f), GR(x, 4.1f, 8.15f), 0.14f, 0.14f, Vector3.up, GarageSteel, Surface.Metal);
            GarageSign("ASSEMBLY / UPSTAIRS", GR(9.12f, 2.8f, 12f), 270f, 3.6f, 0.45f);
        }

        void GarageRoof()
        {
            // Grimy metal roof and long glazed strips, kept at the original collision plane.
            const float tileWidth = 34f / 9f;
            for (int tile = 0; tile < 9; tile++)
                foreach (float z in new[] { -9f, 0f, 9f })
                {
                    float x = -17f + (tile + 0.5f) * tileWidth;
                    Deco(GR(x, 7.16f, z), new Vector3(tileWidth, 0.32f, 6f), GarageTeal, Surface.Metal);
                }
            foreach (float z in new[] { -4.5f, 4.5f })
            {
                Glow(GR(0f, 7.16f, z), new Vector3(34f, 0.32f, 3f), Quaternion.identity, new Color(0.14f, 0.21f, 0.23f));
                for (float x = -16f; x <= 16f; x += 2f)
                    Deco(GR(x, 6.96f, z), new Vector3(0.08f, 0.12f, 3f), GarageSteel, Surface.Metal);
            }
            // End bays finish the roof exactly at z = +/-15; no spill into neighboring rooms.
            foreach (float z in new[] { -13.5f, 13.5f })
                Deco(GR(0f, 7.16f, z), new Vector3(34f, 0.32f, 3f), GarageTeal, Surface.Metal);
            foreach (float x in new[] { -12f, -4f, 4f })
            {
                Deco(GR(x, 6.45f, 0f), new Vector3(0.28f, 0.3f, 29.5f), GarageSteel, Surface.Metal);
                foreach (float z in new[] { -12f, 12f })
                    Bar(GR(x, 6.45f, z - 2f), GR(x, 6.94f, z + 2f), 0.15f, 0.15f, Vector3.up, GarageSteel, Surface.Metal);
            }
            // Over the 4.5m deck, beams sit higher to preserve player headroom.
            Deco(GR(12.8f, 6.9f, 10.8f), new Vector3(7.7f, 0.16f, 0.2f), GarageSteel, Surface.Metal);
        }

        void GarageLighting()
        {
            GarageStrip(GR(-8f, 4.7f, -8f), 4.2f, new Color(0.65f, 0.82f, 0.88f));
            GarageStrip(GR(-8f, 4.7f, 4f), 4.2f, new Color(1f, 0.68f, 0.35f));
            GarageStrip(GR(0f, 5.5f, -4f), 3f, new Color(0.64f, 0.78f, 0.85f));
            GarageStrip(GR(13f, 6.8f, 12f), 4f, new Color(1f, 0.78f, 0.5f));
            GarageStrip(GR(13f, 3.7f, 10f), 3f, new Color(0.42f, 0.72f, 0.7f), 4.15f);
        }

        void GarageStrip(Vector3 at, float width, Color tint, float ceiling = 7f)
        {
            Deco(at, new Vector3(width, 0.12f, 0.32f), GarageSteel, Surface.Metal);
            Glow(at - Vector3.up * 0.075f, new Vector3(width - 0.15f, 0.03f, 0.2f), Quaternion.identity, tint);
            foreach (float side in new[] { -1f, 1f })
            {
                var point = at + Vector3.right * (width * 0.4f * side);
                Rod(point, new Vector3(point.x, ceiling, point.z), 0.02f, GarageSteel, Surface.Metal);
            }
            var light = new GameObject("Garage work light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at - Vector3.up * 0.2f;
            light.type = LightType.Point;
            light.range = 11f;
            light.intensity = 1.5f;
            light.color = tint;
            light.shadows = LightShadows.None;
        }

        void GarageRail(float x0, float z0, float x1, float z1)
        {
            var a = GR(x0, GarageDeck, z0);
            var b = GR(x1, GarageDeck, z1);
            float length = Vector3.Distance(a, b);
            foreach (float y in new[] { 0.45f, 0.95f })
            {
                var bar = Greybox.Box("Garage gallery railing", Root, (a + b) / 2f + Vector3.up * y,
                    new Vector3(0.08f, 0.08f, length), GarageOrange, surface: Surface.Metal);
                bar.transform.localRotation = Quaternion.LookRotation(b - a);
            }
            int posts = Mathf.CeilToInt(length / 1.2f);
            for (int i = 0; i <= posts; i++)
                Greybox.Box("Garage railing post", Root, Vector3.Lerp(a, b, (float)i / posts) + Vector3.up * 0.5f,
                    new Vector3(0.08f, 1f, 0.08f), GarageSteel, surface: Surface.Metal);
        }

        void GarageSign(string text, Vector3 at, float yaw, float width, float height)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, height, 0.06f), rot, GarageTeal, Surface.Metal);
            var holder = Greybox.Empty("Garage sign", Root, at + rot * Vector3.forward * 0.04f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.88f, 0.78f, 0.55f), height * 0.43f);
        }
    }
}
