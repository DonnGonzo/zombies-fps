using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        const int CourtyardZone = 5;
        static readonly Color CourtStone = new Color(0.51f, 0.48f, 0.4f);
        static readonly Color CourtIron = new Color(0.16f, 0.22f, 0.2f);
        static readonly Color CourtMoss = new Color(0.22f, 0.29f, 0.13f);
        static Vector3 CY(float x, float y, float z) => L(CourtyardZone, x, y, z);

        void BuildCourtyard()
        {
            // The original southwest collectible remains on this terrace at (-16.2,3.2,-12.2).
            Block("Cloister terrace", CourtyardZone, -18.7f, -14.7f, -13.7f, -9.7f, 3.2f, CourtStone, Surface.Masonry);
            Stairs(CourtyardZone, -13.7f, -12.2f, Vector3.right, 3.2f, 2.6f);
            AddLadder(CourtyardZone, -16f, -9.7f, Vector3.forward, 3.2f);
            // Shallow walkable fountain: separate rim colliders and a solid modeled centerpiece.
            var bottom = Greybox.Prim(PrimitiveType.Cylinder, "Courtyard fountain floor", Root, CY(4f, 0.06f, -2f),
                new Vector3(6.6f, 0.06f, 6.6f), CourtStone, surface: Surface.Masonry);
            Object.DestroyImmediate(bottom.GetComponent<Collider>());
            bottom.AddComponent<MeshCollider>().convex = true;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var rim = Greybox.Box("Fountain coping", Root, CY(4f, 0.26f, -2f) + outward * 3.1f,
                    new Vector3(1.28f, 0.52f, 0.42f), CourtStone, surface: Surface.Ashlar);
                rim.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            PlacePackItem(Fountain, CY(4f, 0.12f, -2f), Quaternion.identity, true);
            CourtyardBed(-10f, -2f, 4f, 3.5f);
            CourtyardBed(12f, 10f, 4.5f, 3f);
            PlacePackItem(GardenBench, CY(-6f, 0f, 7.8f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(LongBench, CY(13f, 0f, -5.7f), Quaternion.Euler(0f, 270f, 0f), true);
            // A low arcade on the south side, behind the main combat loop. No pillars at door/window mouths.
            foreach (float x in new[] { -6f, 0f, 6f, 12f })
            {
                Greybox.Box("Cloister column", Root, CY(x, 1.75f, -9f), new Vector3(0.7f, 3.5f, 0.7f), CourtStone, surface: Surface.Fluted);
                Greybox.Box("Column base", Root, CY(x, 0.16f, -9f), new Vector3(1f, 0.32f, 1f), CourtStone, surface: Surface.Ashlar);
            }
            // Partial canopy has real collision; the rest of the yard stays genuinely open to the sky.
            Greybox.Box("Cloister roof west", Root, CY(-3f, 5.2f, -11.7f), new Vector3(6.9f, 0.3f, 5.5f), CourtStone, surface: Surface.Masonry);
            Greybox.Box("Cloister roof east", Root, CY(10.8f, 5.2f, -11.7f), new Vector3(3.2f, 0.3f, 5.5f), CourtStone, surface: Surface.Masonry);
            PlacePackItem(Bench, CY(2f, 0f, -13f), Quaternion.identity, true);
            PlacePackItem(BigVase, CY(14.5f, 0f, -12f), Quaternion.identity, true);
            // The ground pickup (-16,4), north-west perk and east-wall generator are kept clear.
        }

        void CourtyardBed(float x, float z, float width, float depth)
        {
            Block("Cloister garden bed", CourtyardZone, x - width / 2f, z - depth / 2f,
                x + width / 2f, z + depth / 2f, 0.55f, CourtStone, Surface.Masonry);
            Deco(CY(x, 0.56f, z), new Vector3(width - 0.25f, 0.02f, depth - 0.25f), new Color(0.18f, 0.16f, 0.1f), Surface.Dirt);
            foreach (float dx in new[] { -width * 0.28f, width * 0.28f })
                if (!KenneyModel("tree-shrub", CY(x + dx, 0.57f, z), dx * 20f, 1.8f))
                    DecoPrim(PrimitiveType.Sphere, CY(x + dx, 0.9f, z), new Vector3(1.1f, 0.8f, 1.1f), Quaternion.identity, CourtMoss, Surface.Grass);
        }

        void CourtyardMakeover()
        {
            CourtyardFountain();
            CourtyardArcade();
            CourtyardTerrace();
            CourtyardLights();
            // A pale processional walk connects Temple and Gate Yard north of the fountain.
            for (int i = 0; i < 12; i++)
                Deco(CY(-17.4f + i * 3.1f, 0.008f, 3.7f), new Vector3(3.05f, 0.016f, 2.3f), CourtStone, Surface.MosaicGrey);
            // Northbound path to Barracks, through the wider upper arm of the court.
            for (float z = 6f; z < 15f; z += 2f)
                Deco(CY(1f, 0.009f, z), new Vector3(2.7f, 0.018f, 1.95f), CourtStone, Surface.MosaicGrey);
            // Moss stays close to the walls and beds rather than obscuring the combat lanes.
            foreach (var p in new[] { new Vector2(-11f, -4f), new Vector2(13f, 11.7f), new Vector2(-5f, -10f), new Vector2(15f, -12.5f) })
                Splat(CY(p.x, 0f, p.y), 2.4f, 0.8f, p.x * 12f, 0.005f, CourtMoss, Surface.Grass);
            // Fallen masonry is low enough to step over and has matching collision.
            foreach (var p in new[] { new Vector2(3.3f, -10.9f), new Vector2(4.4f, -12.5f), new Vector2(5.2f, -10f) })
                Greybox.Box("Fallen cloister stone", Root, CY(p.x, 0.09f, p.y), new Vector3(0.7f, 0.18f, 0.5f), CourtStone, surface: Surface.Masonry);
            PlacePackItem(Candles, CY(-4.4f, 0f, 7.8f), Quaternion.identity, false);
            CourtyardSign("THE COURTYARD", CY(18.5f, 4.7f, 3f), 270f, 5.8f, 0.7f);
            CourtyardSign("BARRACKS", CY(1f, 4.7f, 14.5f), 180f, 4f, 0.6f);
            // Selected ivy strands on the high walls; all are batched decorative geometry.
            foreach (float x in new[] { -5f, 5f, 15f })
                for (int i = 0; i < 7; i++)
                    DecoPrim(PrimitiveType.Sphere, CY(x + Mathf.Sin(i * 1.4f) * 0.2f, 5.8f - i * 0.35f, 14.55f),
                        new Vector3(0.55f, 0.45f, 0.16f), Quaternion.identity, CourtMoss, Surface.Grass);
        }

        void CourtyardFountain()
        {
            // Water rests just above the pool floor, below the rim; the basin remains shallow and traversable.
            DecoPrim(PrimitiveType.Cylinder, CY(4f, 0.18f, -2f), new Vector3(5.75f, 0.005f, 5.75f), Quaternion.identity,
                new Color(0.1f, 0.24f, 0.25f), Surface.Metal);
            // Mosaic compass around the basin, with a broken outer ring instead of extra solid obstacles.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f;
                var rotation = Quaternion.Euler(0f, angle, 0f);
                Deco(CY(4f, 0.009f, -2f) + rotation * Vector3.forward * 3.85f, new Vector3(0.95f, 0.018f, 0.45f),
                    rotation, i % 3 == 0 ? CourtIron : CourtStone, Surface.Mosaic);
            }
            foreach (float angle in new[] { 0f, 90f, 180f, 270f })
            {
                var rotation = Quaternion.Euler(0f, angle, 0f);
                // Four subtle spill streams falling into the pool, below sightline height.
                var p = CY(4f, 0.9f, -2f) + rotation * Vector3.forward * 0.55f;
                Rod(p, p + rotation * Vector3.forward * 0.35f + Vector3.down * 0.7f, 0.045f,
                    new Color(0.27f, 0.46f, 0.46f), Surface.Metal);
            }
        }

        void CourtyardArcade()
        {
            foreach (float x in new[] { -6f, 0f, 6f, 12f })
                Deco(CY(x, 3.5f, -9f), new Vector3(1f, 0.25f, 1f), CourtStone, Surface.Ashlar);
            foreach (float center in new[] { -3f, 3f, 9f })
                for (int i = 0; i < 12; i++)
                {
                    // The middle arch has fallen through; its two surviving stubs frame the night sky.
                    if (center == 3f && i >= 4 && i <= 8) continue;
                    float a = i * Mathf.PI / 12f;
                    float b = (i + 1) * Mathf.PI / 12f;
                    Bar(CY(center + Mathf.Cos(a) * 3f, 3.5f + Mathf.Sin(a) * 1.5f, -9f),
                        CY(center + Mathf.Cos(b) * 3f, 3.5f + Mathf.Sin(b) * 1.5f, -9f),
                        0.42f, 0.4f, Vector3.forward, CourtStone, Surface.Ashlar);
                }
            // The intact bay has timber rafters; the ruined bay has only its projecting beam ends.
            for (float x = -5.5f; x < 0f; x += 1.1f)
                Deco(CY(x, 4.95f, -11.7f), new Vector3(0.15f, 0.18f, 5.3f), PropWood, Surface.Wood);
            foreach (float x in new[] { 1f, 5.5f, 7f })
                Deco(CY(x, 4.95f, -14f), new Vector3(0.18f, 0.2f, 1.5f), Quaternion.Euler(0f, x * 3f, 0f), PropWood, Surface.Wood);
        }

        void CourtyardTerrace()
        {
            // Coping and an old shrine frame the collectible without enclosing it.
            // Only the south and west edges have parapets; both access landings stay open.
            Greybox.Box("Terrace parapet", Root, CY(-16.2f, 3.55f, -14.45f), new Vector3(4.6f, 0.7f, 0.28f), CourtStone, surface: Surface.Ashlar);
            Greybox.Box("Terrace west parapet", Root, CY(-18.45f, 3.55f, -12.2f), new Vector3(0.28f, 0.7f, 4.6f), CourtStone, surface: Surface.Ashlar);
            PlacePackItem(CandleStand, CY(-17.7f, 3.2f, -13.4f), Quaternion.identity, true);
            LanternOn(CY(-18.4f, 3.9f, -10.4f), Quaternion.identity, true);
            Deco(CY(-16.2f, 3.205f, -12.2f), new Vector3(2.6f, 0.01f, 2.6f), CourtIron, Surface.MosaicGrey);
        }

        void CourtyardLights()
        {
            foreach (var p in new[] { new Vector2(-11f, 3.7f), new Vector2(11f, 3.7f), new Vector2(3f, 11f) })
            {
                // Lamps sit outside the pale walking strip, leaving its full width clear.
                var foot = CY(p.x, 0f, p.y + 1.7f);
                Greybox.Box("Courtyard lantern post", Root, foot + Vector3.up * 1.8f, new Vector3(0.14f, 3.6f, 0.14f), CourtIron, surface: Surface.Metal);
                LanternOn(foot + Vector3.up * 3.6f, Quaternion.identity, true, 1.3f);
                CourtLight(foot + Vector3.up * 3.65f, 10f, 1.3f, new Color(1f, 0.73f, 0.4f));
            }
            foreach (float x in new[] { -3f, 10.5f })
            {
                Rod(CY(x, 5f, -11.7f), CY(x, 4.1f, -11.7f), 0.025f, CourtIron, Surface.Metal);
                LanternOn(CY(x, 3.5f, -11.7f), Quaternion.identity, true);
                CourtLight(CY(x, 3.6f, -11.7f), 8f, 1.1f, new Color(1f, 0.68f, 0.33f));
            }
            CourtLight(CY(4f, 3.8f, -2f), 17f, 0.65f, new Color(0.4f, 0.63f, 0.85f));
            CourtLight(CY(-16f, 5f, -12f), 7f, 0.8f, new Color(1f, 0.65f, 0.3f));
        }

        void CourtLight(Vector3 at, float range, float intensity, Color tint)
        {
            var light = new GameObject("Courtyard light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = tint;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        void CourtyardSign(string text, Vector3 at, float yaw, float width, float height)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, height, 0.07f), rot, CourtIron, Surface.Metal);
            var holder = Greybox.Empty("Courtyard sign", Root, at + rot * Vector3.forward * 0.045f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.83f, 0.77f, 0.58f), height * 0.43f);
        }
    }
}
