using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        static readonly Color GateStone = new Color(0.57f, 0.51f, 0.4f);
        static readonly Color GateBlue = new Color(0.16f, 0.3f, 0.34f);
        static readonly Color GateBrass = new Color(0.63f, 0.47f, 0.23f);
        static Vector3 GY(float x, float y, float z) => L(StartZone, x, y, z);

        void BuildGateYard()
        {
            // Preserve the elevated shield-part anchor and provide two ways off the lookout.
            Block("Gatekeeper lookout", StartZone, -15.7f, 10f, -9.7f, 15.7f, 2.2f, GateStone, Surface.Ashlar);
            Stairs(StartZone, -12.7f, 10f, Vector3.back, 2.2f, 2.8f);
            AddLadder(StartZone, -9.7f, 13f, Vector3.right, 2.2f);
            foreach (float z in new[] { 11f, 15f })
                Greybox.Box("Lookout parapet", Root, GY(-15.2f, 2.65f, z), new Vector3(0.6f, 0.9f, 1.4f), GateStone, surface: Surface.Ashlar);
            Greybox.Box("Lookout shelter roof", Root, GY(-12.7f, 6.2f, 13f), new Vector3(5.8f, 0.18f, 5.5f), GateBlue, surface: Surface.Wood);
            // The old gate passage now reads as a gatehouse, with open arches into the market pocket.
            foreach (float z in new[] { -14.7f, -20.5f })
                foreach (float x in new[] { -3.9f, 3.9f })
                    Greybox.Box("Gatehouse pier", Root, GY(x, 2.5f, z), new Vector3(0.65f, 5f, 0.8f), GateStone, surface: Surface.Ashlar);
            Greybox.Box("Gatehouse canopy", Root, GY(0f, 5.35f, -18f), new Vector3(9.4f, 0.3f, 8f), GateStone, surface: Surface.Masonry);
            // Recessed Depot vestibule: no columns or extra door across its six-metre-wide floor.
            Greybox.Box("Depot passage roof", Root, GY(14f, 4.6f, 0f), new Vector3(4f, 0.3f, 6f), GateBlue, surface: Surface.Metal);
            // A separate timber shelter frames the existing functional trader counter.
            foreach (float z in new[] { -9f, -3.8f })
                Greybox.Box("Trader shelter post", Root, GY(14.7f, 1.9f, z), new Vector3(0.2f, 3.8f, 0.2f), Timber, surface: Surface.Wood);
            Greybox.Box("Trader shelter roof", Root, GY(12f, 3.9f, -6.4f), new Vector3(6.5f, 0.18f, 5.9f), Timber, surface: Surface.Wood);
            // Landmark is offset from the four-player spawn apron and all four exit paths.
            Block("Standard plinth", StartZone, 4.1f, 4.1f, 5.9f, 5.9f, 0.65f, GateStone, Surface.Ashlar);
            Greybox.Box("Gate standard mast", Root, GY(5f, 3.05f, 5f), new Vector3(0.14f, 4.8f, 0.14f), GateBrass, surface: Surface.Metal);
            // Authored market stalls replace random clutter. The southern wall gun and window stay accessible.
            PlacePackItem(Stall, GY(-13f, 0f, -16f), Quaternion.identity, true);
            PlacePackItem(LongTable, GY(-13f, 0f, -15.4f), Quaternion.identity, true);
            PlacePackItem(AppleCrate, GY(-13.4f, 0.81f, -15.4f), Quaternion.identity, false);
            PlacePackItem(KitBarrel, GY(-6.5f, 0f, -17f), Quaternion.identity, true);
            PlacePackItem(Sack, GY(-7.3f, 0f, -17.4f), Quaternion.identity, true);
            PlacePackItem(StallCart, GY(8.2f, 0f, 8.1f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(WoodChest, GY(13.7f, 0f, -8.6f), Quaternion.identity, true);
            PlacePackItem(WeaponRack, GY(14f, 0f, -4.4f), Quaternion.Euler(0f, 270f, 0f), true);
            PlacePackItem(LongBench, GY(-7f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f), true);
        }

        void GateYardMakeover()
        {
            // Flush compass apron: no raised centrepiece or collision at any player spawn slot.
            DecoPrim(PrimitiveType.Cylinder, GY(0f, 0.008f, -6f), new Vector3(8f, 0.008f, 8f), Quaternion.identity,
                GateStone, Surface.MosaicGrey);
            for (int i = 0; i < 24; i++)
            {
                var rot = Quaternion.Euler(0f, i * 15f, 0f);
                Deco(GY(0f, 0.02f, -6f) + rot * Vector3.forward * 3.65f, new Vector3(0.7f, 0.015f, 0.13f), rot, GateBrass, Surface.Metal);
            }
            // Broad crossing paths, with a deliberate offset to Chapel instead of a rigid grid.
            for (float x = -15f; x < 16f; x += 2f)
                Deco(GY(x, 0.009f, 0f), new Vector3(1.95f, 0.018f, 3.8f), GateStone, Surface.MosaicGrey);
            for (float z = 2f; z < 16f; z += 2f)
                Deco(GY(-4f, 0.009f, z), new Vector3(3.8f, 0.018f, 1.95f), GateStone, Surface.MosaicGrey);
            for (float z = -10f; z > -22f; z -= 2f)
                Deco(GY(0f, 0.009f, z), new Vector3(3.8f, 0.018f, 1.95f), GateStone, Surface.MosaicGrey);
            // Gatehouse ribs are high enough for unobstructed sight and movement below them.
            foreach (float z in new[] { -14.7f, -20.5f })
                GateArch(0f, z, 3.9f, 3.6f, 1.5f);
            GateArch(-4f, 15.5f, 3.3f, 3.5f, 1.7f);
            // Raised portcullis teeth communicate the old gate without an invisible or solid floor barrier.
            for (float x = -3f; x <= 3f; x += 0.6f)
                Deco(GY(x, 4.75f, -14.75f), new Vector3(0.07f, 0.7f, 0.08f), Soot, Surface.Metal);
            Deco(GY(0f, 4.65f, -14.75f), new Vector3(6.5f, 0.09f, 0.1f), Soot, Surface.Metal);
            // Gatekeeper lookout: crenels and a short shelter above the original collectible.
            foreach (float x in new[] { -15.3f, -10.1f })
                Deco(GY(x, 4.2f, 15.25f), new Vector3(0.22f, 3.9f, 0.22f), Timber, Surface.Wood);
            PlacePackItem(WallShield, GY(-12.7f, 1.5f, 9.92f), Quaternion.Euler(0f, 180f, 0f), false);
            PlacePackItem(Candles, GY(-14.5f, 2.2f, 14.7f), Quaternion.identity, false);
            // A torn blue standard gives the spawn square a landmark visible from each approach.
            Rod(GY(5f, 5.1f, 5f), GY(7.2f, 5.1f, 5f), 0.06f, GateBrass, Surface.Metal);
            for (int i = 0; i < 6; i++)
            {
                float h = i == 5 ? 1.2f : 1.8f;
                Deco(GY(5.2f + i * 0.32f, 5f - h / 2f, 5f + Mathf.Sin(i) * 0.08f),
                    new Vector3(0.34f, h, 0.04f), GateBlue, Surface.Fabric);
            }
            Deco(GY(6f, 4.25f, 4.87f), new Vector3(0.45f, 0.45f, 0.02f), Quaternion.Euler(0f, 0f, 45f), GateBrass, Surface.Metal);
            GateSign("GATE YARD", GY(-4f, 5.5f, 15.35f), 180f, 5.2f);
            GateSign("CHAPEL", GY(-4f, 3.95f, 15.3f), 180f, 3f);
            GateSign("DEPOT", GY(12.1f, 3.65f, 0f), 270f, 3.8f);
            GateSign("COURTYARD", GY(-15.5f, 4.3f, 0f), 90f, 4.7f);
            GateSign("GAOL", GY(0f, 4f, -14.25f), 0f, 3.2f);
            GateSign("TRADER / SUPPLIES", GY(8.8f, 3.3f, -6f), 270f, 4.6f);
            foreach (var p in new[] { new Vector3(-14.7f, 4.2f, 2f), new Vector3(-4f, 5.8f, 14.9f),
                new Vector3(14f, 4.2f, 0f), new Vector3(0f, 4.8f, -18f), new Vector3(-13f, 3.4f, -16f), new Vector3(10f, 3.4f, -6f) })
                GateLight(GY(p.x, p.y, p.z));
            Bar(GY(-15.7f, 4.2f, 2f), GY(-14.7f, 4.2f, 2f), 0.08f, 0.08f, Vector3.up, Timber, Surface.Wood);
            Bar(GY(-4f, 5.8f, 15.7f), GY(-4f, 5.8f, 14.9f), 0.08f, 0.08f, Vector3.up, Timber, Surface.Wood);
            foreach (var p in new[] { new Vector3(14f, 4.45f, 0f), new Vector3(0f, 5.2f, -18f), new Vector3(10f, 3.81f, -6f) })
                Rod(GY(p.x, p.y, p.z), GY(p.x, p.y - 0.4f, p.z), 0.035f, Soot, Surface.Metal);
            Deco(GY(-13f, 3f, -16f), new Vector3(0.08f, 0.8f, 0.08f), Timber, Surface.Wood);
            // Weathering stays at the edges, clear of the spawn apron and pickup at (-13,-11).
            foreach (var p in new[] { new Vector2(-14f, 4f), new Vector2(10.5f, 10f), new Vector2(-14f, -19f) })
                Splat(GY(p.x, 0f, p.y), 1.8f, 0.7f, p.x * 8f, 0.005f, new Color(0.23f, 0.27f, 0.15f), Surface.Grass);
        }

        void GateArch(float x, float z, float radius, float spring, float rise)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 16f, b = (i + 1) * Mathf.PI / 16f;
                Bar(GY(x + Mathf.Cos(a) * radius, spring + Mathf.Sin(a) * rise, z),
                    GY(x + Mathf.Cos(b) * radius, spring + Mathf.Sin(b) * rise, z),
                    0.34f, 0.45f, Vector3.forward, GateStone, Surface.Ashlar);
            }
        }

        void GateLight(Vector3 at)
        {
            LanternOn(at, Quaternion.identity, true);
            var light = new GameObject("Gate Yard lantern light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at + Vector3.up * 0.18f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.77f, 0.45f);
            light.intensity = 1.5f;
            light.range = 13f;
            light.shadows = LightShadows.None;
        }

        void GateSign(string text, Vector3 at, float yaw, float width)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, 0.6f, 0.08f), rot, GateBlue, Surface.Wood);
            var holder = Greybox.Empty("Gate Yard wayfinding", Root, at + rot * Vector3.forward * 0.05f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.9f, 0.8f, 0.55f), 0.25f);
        }
    }
}
