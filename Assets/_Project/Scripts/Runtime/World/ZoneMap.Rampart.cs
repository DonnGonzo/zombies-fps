using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        static readonly Color RampStone = new Color(0.48f, 0.46f, 0.39f);
        static readonly Color RampMoss = new Color(0.23f, 0.29f, 0.16f);
        static readonly Color RampRed = new Color(0.38f, 0.07f, 0.06f);
        static Vector3 RR(float x, float y, float z) => L(Rampart, x, y, z);

        // Added after the navigation bake, like the outer wall seals. These narrow columns cover
        // the pier caps and extend well above jumping height without blocking shots or the arches.
        void SealRampartPillars()
        {
            const float bottom = 0.35f, height = 60f;
            foreach (float z in new[] { 3f, 21f })
                foreach (float x in new[] { -5.5f, 5.5f })
                {
                    var seal = new GameObject("Rampart pillar seal") { layer = BlockerLayer };
                    seal.transform.SetParent(Root, false);
                    seal.transform.localPosition = RR(x, bottom + height / 2f, z);
                    seal.AddComponent<BoxCollider>().size = new Vector3(1.85f, height, 2.05f);
                }
        }

        void BuildRampartRuins()
        {
            // A continuous eastern wall walk with two broad stairways and a mid-route escape ladder.
            // The western transit lane and both door approaches stay entirely at ground level.
            Block("Eastern curtain-wall walk", Rampart, 8f, -14f, 14f, 28f, 3.2f, RampStone, Surface.Ashlar);
            Stairs(Rampart, 11f, -14f, Vector3.back, 3.2f, 3.4f);
            Stairs(Rampart, 11f, 28f, Vector3.forward, 3.2f, 3.4f);
            AddLadder(Rampart, 8f, 9f, Vector3.left, 3.2f);
            // Solid parapets have deliberate firing gaps; nothing blocks either stair landing.
            foreach (float z in new[] { -11f, -5f, 1f, 7f, 13f, 19f, 25f })
                Greybox.Box("Battlement merlon", Root, RR(13.6f, 3.85f, z), new Vector3(0.8f, 1.3f, 2.6f), RampStone, surface: Surface.Ashlar);
            foreach (float z in new[] { -7f, 17f })
                Greybox.Box("Broken inner parapet", Root, RR(8.35f, 3.65f, z), new Vector3(0.7f, 0.9f, 3f), RampStone, surface: Surface.Masonry);
            // Remnants of two cross-vaults frame an unobstructed central avenue.
            foreach (float z in new[] { 3f, 21f })
                foreach (float x in new[] { -5.5f, 5.5f })
                {
                    Greybox.Box("Gatehouse pier", Root, RR(x, 1.8f, z), new Vector3(1.3f, 3.6f, 1.6f), RampStone, surface: Surface.Ashlar);
                    Greybox.Box("Pier footing", Root, RR(x, 0.2f, z), new Vector3(1.7f, 0.4f, 2f), RampStone, surface: Surface.Masonry);
                }
            // Broken defensive positions are islands, leaving room to circle around them.
            foreach (var p in new[] { new Vector2(-6f, -16f), new Vector2(-6f, 13f), new Vector2(4f, 39f) })
                Greybox.Box("Fallen curtain wall", Root, RR(p.x, 0.65f, p.y), new Vector3(3.5f, 1.3f, 1.5f), RampStone, surface: Surface.Masonry);
            PlacePackItem(Stall, RR(-6.5f, 0f, 41f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(LongTable, RR(-6.5f, 0f, 39.5f), Quaternion.identity, true);
            PlacePackItem(WeaponRack, RR(-8.5f, 0f, 37.5f), Quaternion.identity, true);
            PlacePackItem(WoodChest, RR(-4.3f, 0f, 42f), Quaternion.Euler(0f, -20f, 0f), true);
            PlacePackItem(KitBarrel, RR(-8.5f, 0f, 43f), Quaternion.identity, true);
            PlacePackItem(WoodCrate, RR(11.5f, 3.2f, 23f), Quaternion.identity, true);
            PlacePackItem(WeaponRack, RR(11.5f, 3.2f, -10f), Quaternion.identity, true);
        }

        void RampartMakeover()
        {
            // Weathered paving makes the combat loop legible without modern road markings.
            for (float z = -16f; z < 44f; z += 2f)
                Deco(RR(0f, 0.009f, z), new Vector3(3.8f, 0.018f, 1.92f), RampStone, Surface.MosaicGrey);
            foreach (var entry in new[] { new Vector2(-13f, -10f), new Vector2(-11f, 32f) })
                for (float x = entry.x + 1f; x < 0f; x += 2f)
                    Deco(RR(x, 0.012f, entry.y), new Vector3(1.92f, 0.02f, 3.8f), RampStone, Surface.MosaicGrey);
            for (float z = -13f; z < 28f; z += 2f)
                Deco(RR(11f, 3.208f, z), new Vector3(5.7f, 0.016f, 1.95f), RampStone, Surface.Masonry);
            foreach (float z in new[] { 3f, 21f })
            {
                foreach (float x in new[] { -5.5f, 5.5f })
                    Deco(RR(x, 3.65f, z), new Vector3(1.8f, 0.25f, 2f), RampStone, Surface.Ashlar);
                for (int i = 0; i < 16; i++)
                {
                    // The northern vault has a wide collapse, exposing the sky between its stubs.
                    if (z == 21f && i >= 5 && i <= 10) continue;
                    float a = i * Mathf.PI / 16f, b = (i + 1) * Mathf.PI / 16f;
                    Bar(RR(Mathf.Cos(a) * 5.5f, 3.7f + Mathf.Sin(a) * 2.7f, z),
                        RR(Mathf.Cos(b) * 5.5f, 3.7f + Mathf.Sin(b) * 2.7f, z),
                        0.55f, 0.8f, Vector3.forward, RampStone, Surface.Ashlar);
                }
            }
            // Buttresses and bands break up the long fortress face.
            foreach (float z in new[] { -12f, -2f, 12f, 26f })
            {
                Deco(RR(7.9f, 1.6f, z), new Vector3(0.18f, 3.2f, 1.1f), RampStone, Surface.Masonry);
                Deco(RR(7.78f, 2.1f, z), new Vector3(0.04f, 1.7f, 0.8f), RampRed, Surface.Fabric);
                Deco(RR(7.75f, 2.4f, z), new Vector3(0.03f, 0.12f, 0.65f), new Color(0.65f, 0.51f, 0.22f), Surface.Fabric);
            }
            var rng = new System.Random(16421);
            // Rubble is confined to the eastern verge beyond the wall walk, away from travel lanes.
            for (int i = 0; i < 36; i++)
                DecoPrim(PrimitiveType.Cube, RR(Range(rng, 14.8f, 16.3f), 0.12f, Range(rng, -17f, 43f)),
                    new Vector3(Range(rng, 0.3f, 0.7f), 0.24f, Range(rng, 0.3f, 0.8f)),
                    Quaternion.Euler(0f, Range(rng, 0f, 180f), 8f), RampStone, Surface.Masonry);
            foreach (var p in new[] { new Vector2(-7f, -16f), new Vector2(-7f, 15f), new Vector2(5f, 40f), new Vector2(15.5f, 8f) })
            {
                Splat(RR(p.x, 0f, p.y), 3f, 1.3f, p.y * 9f, 0.006f, RampMoss, Surface.Grass);
                if (!KenneyModel("tree-shrub", RR(p.x, 0f, p.y + 1.4f), p.y * 7f, 1.2f))
                    DecoPrim(PrimitiveType.Sphere, RR(p.x, 0.35f, p.y + 1.4f), new Vector3(1.2f, 0.7f, 1f), Quaternion.identity, RampMoss, Surface.Grass);
            }
            PlacePackItem(Candles, RR(-6.5f, 0.81f, 39.5f), Quaternion.identity, false);
            PlacePackItem(WallShield, RR(-5.5f, 2.2f, 2.15f), Quaternion.Euler(0f, 180f, 0f), false);
            foreach (var p in new[] { new Vector3(-5.5f, 2.4f, 4f), new Vector3(5.5f, 2.4f, 22f), new Vector3(-7.5f, 0.81f, 39.5f), new Vector3(12f, 3.2f, 1f) })
                LanternOn(RR(p.x, p.y, p.z), Quaternion.identity, true);
            RampartSign("PUMP STATION", RR(-12.5f, 4.6f, -10f), 90f);
            RampartSign("CRANE YARD", RR(-10.5f, 4.6f, 32f), 90f);
            RampartSign("THE EASTERN RAMPART", RR(0f, 4.8f, 45.5f), 180f);
        }

        void RampartSign(string text, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(5.5f, 0.65f, 0.08f), rot, Timber, Surface.Wood);
            var holder = Greybox.Empty("Rampart wayfinding", Root, at + rot * Vector3.forward * 0.05f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.8f, 0.73f, 0.52f), 0.25f);
        }
    }
}
