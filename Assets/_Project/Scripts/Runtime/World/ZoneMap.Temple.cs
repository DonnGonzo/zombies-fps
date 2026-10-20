using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Sunken Temple: round sandstone columns on moulded plinths with teal-and-gold capitals, carrying carved
    /// beams the length of the hall; a stone-kerbed reflecting pool with a fountain at its head; torches and
    /// banners on the columns; a starred frieze round the walls; a stone altar of offerings in the sanctuary between
    /// two fire braziers; and the temple's treasure (chests, coin, urns) heaped in the prayer hall's east wing.
    /// </summary>
    public partial class ZoneMap
    {
        static readonly Color Sandstone = new Color(0.6f, 0.53f, 0.42f);
        static readonly Color TempleTeal = new Color(0.14f, 0.36f, 0.38f);

        static readonly float[] TempleColumnZ = { -13.5f, -7.5f, -1.5f, 4.5f, 10.5f, 16.5f };

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildTemple()
        {
            // Two rows of columns down the hall: a box each to stand behind, drawn round.
            foreach (float x in new[] { -6f, 6f })
                foreach (float z in TempleColumnZ)
                {
                    var box = Greybox.Box("Column", Root, L(Temple, x, WallHeight / 2f, z), new Vector3(1f, WallHeight, 1f), Sandstone, surface: Surface.Plaster);
                    box.GetComponent<Renderer>().enabled = false;
                    var foot = L(Temple, x, 0f, z);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (WallHeight / 2f), new Vector3(0.9f, WallHeight / 2f, 0.9f), Quaternion.identity, Sandstone, Surface.Plaster);
                    Deco(foot + Vector3.up * 0.3f, new Vector3(1.25f, 0.6f, 1.25f), Shade(Sandstone, 0.85f), Surface.Tile);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 0.66f, new Vector3(1.06f, 0.06f, 1.06f), Quaternion.identity, Shade(Sandstone, 0.92f), Surface.Plaster);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 5.75f, new Vector3(0.98f, 0.12f, 0.98f), Quaternion.identity, TempleTeal, Surface.Tile);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 5.6f, new Vector3(0.96f, 0.03f, 0.96f), Quaternion.identity, OldGold, Surface.Metal);
                    Deco(foot + Vector3.up * 6.1f, new Vector3(1.35f, 0.3f, 1.35f), Shade(Sandstone, 0.9f), Surface.Tile);
                }

            // The sanctuary, two steps up at the north end, with its altar against the niche.
            Block("Sanctuary", Temple, -11.7f, 26f, -2f, 33.7f, 0.5f, new Color(0.5f, 0.45f, 0.36f), Surface.Tile);
            Stairs(Temple, -6.85f, 26f, Vector3.back, 0.5f, 4f);
            Greybox.Box("Temple Altar", Root, L(Temple, -6.85f, 1f, 32.9f), new Vector3(1.8f, 1f, 0.8f), Shade(Sandstone, 0.8f), surface: Surface.Tile);
            // The fountain at the head of the pool.
            PlacePackItem(Fountain, L(Temple, 0f, 0f, 17.4f), Quaternion.identity, true);
        }

        // ---------------------------------------------------------------- dressing

        void TempleMakeover()
        {
            var rng = new System.Random(1772);

            // Carved beams along each row of columns, with a gold line and a run of gold stars.
            float zc = (TempleColumnZ[0] + TempleColumnZ[TempleColumnZ.Length - 1]) / 2f;
            float len = TempleColumnZ[TempleColumnZ.Length - 1] - TempleColumnZ[0] + 1.35f;
            foreach (float x in new[] { -6f, 6f })
            {
                Deco(L(Temple, x, 6.62f, zc), new Vector3(1.1f, 0.75f, len), Shade(Sandstone, 0.95f), Surface.Tile);
                foreach (float s in new[] { -1f, 1f })
                {
                    Deco(L(Temple, x + s * 0.56f, 6.33f, zc), new Vector3(0.02f, 0.05f, len), OldGold, Surface.Metal);
                    for (float z = TempleColumnZ[0] + 0.3f; z < TempleColumnZ[TempleColumnZ.Length - 1]; z += 0.75f)
                        Deco(L(Temple, x + s * 0.56f, 6.65f, z), new Vector3(0.02f, 0.16f, 0.16f), Quaternion.Euler(45f, 0f, 0f), Shade(OldGold, 0.9f), Surface.Metal);
                }
            }

            // Torches on the columns facing the pool, and banners off them into the side aisles.
            for (int i = 0; i < TempleColumnZ.Length; i++)
                foreach (float x in new[] { -6f, 6f })
                {
                    var inward = x < 0f ? Vector3.right : Vector3.left;
                    var foot = L(Temple, x, 0f, TempleColumnZ[i]);
                    if (i % 2 == 1 && HangItem(WallTorch, foot + inward * 0.45f, Quaternion.LookRotation(inward), 2.4f))
                        CandleLight(foot + inward * 0.77f + Vector3.up * 2.75f);
                    if (i % 2 == 0) HangItem(x < 0f ? HangingBanner1 : HangingBanner2, foot - inward * 0.45f, Quaternion.LookRotation(-inward), 5.2f);
                }

            // A stone kerb round the reflecting pool.
            var pool = L(Temple, 0f, 0f, 1.5f);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(pool + new Vector3(s * 2.45f, 0.07f, 0f), new Vector3(0.35f, 0.14f, 26.7f), Shade(Sandstone, 0.85f), Surface.Tile);
                Deco(pool + new Vector3(0f, 0.07f, s * 13.2f), new Vector3(5.25f, 0.14f, 0.35f), Shade(Sandstone, 0.85f), Surface.Tile);
            }

            // A frieze round the walls: a teal band between gold lines, stars along it, and a cornice above.
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Zone != Temple) continue;
                ClearStretches(f, 5.9f, 0.3f, 0.04f, (a, b) =>
                {
                    Deco(f.At((a + b) / 2f, 5.9f, 0.02f), new Vector3(b - a, 0.5f, 0.02f), f.Facing, TempleTeal, Surface.Tile);
                    foreach (float y in new[] { 5.63f, 6.17f })
                        Deco(f.At((a + b) / 2f, y, 0.03f), new Vector3(b - a, 0.04f, 0.02f), f.Facing, OldGold, Surface.Metal);
                    for (float t = a + 0.5f; t < b - 0.3f; t += 1f)
                        Deco(f.At(t, 5.9f, 0.035f), new Vector3(0.22f, 0.22f, 0.01f), f.Facing * Quaternion.Euler(0f, 0f, 45f), Shade(OldGold, 0.9f), Surface.Metal);
                });
                ClearStretches(f, 6.82f, 0.1f, 0.24f, (a, b) =>
                    Deco(f.At((a + b) / 2f, 6.82f, 0.12f), new Vector3(b - a, 0.2f, 0.22f), f.Facing, Shade(Sandstone, 0.9f), Surface.Tile));
            }

            // The altar's top: a cloth, coin, a chalice, candles; fire braziers either side of it.
            var altar = L(Temple, -6.85f, 1.5f, 32.9f);
            Deco(altar + Vector3.up * 0.005f, new Vector3(1.9f, 0.01f, 0.5f), new Color(0.32f, 0.08f, 0.1f), Surface.Fabric);
            foreach (var (item, x) in new[] { ("CandleStick_Triple", -0.7f), ("Coin_Pile_2", -0.25f), ("Chalice", 0.2f), ("Coin_Pile", 0.45f), ("CandleStick_Triple", 0.75f) })
            {
                var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                if (model != null) AddModel(model, altar + new Vector3(x, 0.01f, 0f), Quaternion.Euler(0f, 180f, 0f), false);
            }
            foreach (var p in new[] { new Vector2(-3.4f, 30.6f), new Vector2(-10.6f, 28.2f) })
                Brazier(L(Temple, p.x, 0.5f, p.y));
            foreach (float x in new[] { -9.6f, -4.1f })
                if (PlaceFree(CandleStand, L(Temple, x, 0f, 24.6f), Quaternion.identity, true)) CandleLight(L(Temple, x, 1.45f, 24.6f));

            // The temple's treasure in the east wing, and urns in the corners.
            foreach (var (item, x, z, yaw) in new[]
            {
                (WoodChest, 18.6f, 32.6f, 180f), (WoodChest, 13.6f, 32.6f, 170f), (IronCrate, 19.1f, 26.5f, 15f),
                (BigVase, 16.2f, 33f, 0f), (SmallVase, 19.1f, 30.4f, 0f), (BigVase, -11f, -17f, 0f), (SmallVase, -10.2f, -17.1f, 0f),
                (BigVase, 11f, -17f, 0f), (BigVase, 11.1f, 19f, 0f), (SmallVase, -11.1f, 19f, 0f), (WeaponRack, 13.2f, 20.9f, 0f),
            })
                PlaceFree(item, L(Temple, x, 0f, z), Quaternion.Euler(0f, yaw, 0f), true);
            foreach (var (item, x, z) in new[] { ("Coin_Pile", 17.2f, 32.2f), ("Coin_Pile_2", 15.3f, 31.6f), ("Chalice", 14.6f, 32.9f), ("Coin_Pile", 19.2f, 23f), ("Vase_Rubble_Medium", 12.8f, 21.2f) })
            {
                var at = L(Temple, x, 0f, z);
                var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                if (model != null && KeptClear(at, 0.3f)) AddModel(model, at, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), false);
            }

            // Mosaic stars in the floor of the prayer hall.
            foreach (var p in new[] { new Vector2(4f, 27f), new Vector2(-6.85f, 22.2f) })
            {
                var at = L(Temple, p.x, 0.006f, p.y);
                Deco(at, new Vector3(2.6f, 0.008f, 2.6f), Quaternion.Euler(0f, 45f, 0f), Shade(OldGold, 0.7f), Surface.Tile);
                Deco(at + Vector3.up * 0.003f, new Vector3(2.6f, 0.008f, 2.6f), TempleTeal, Surface.Tile);
                Deco(at + Vector3.up * 0.006f, new Vector3(1.2f, 0.008f, 1.2f), Quaternion.Euler(0f, 45f, 0f), Shade(OldGold, 0.9f), Surface.Tile);
            }
        }

        /// <summary>A fire bowl on a stand, with its light.</summary>
        void Brazier(Vector3 at)
        {
            var bowl = PropModels.GetPack(Cauldron.Path, PackMaterial);
            if (bowl != null) AddModel(bowl, at, Quaternion.identity, true);
            else DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * 0.4f, new Vector3(0.9f, 0.4f, 0.9f), Quaternion.identity, Soot, Surface.Metal);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f * Mathf.Deg2Rad;
                Glow(PrimitiveType.Sphere, at + new Vector3(Mathf.Cos(a) * 0.15f, 0.85f + (k % 2) * 0.08f, Mathf.Sin(a) * 0.15f), new Vector3(0.26f, 0.42f, 0.26f), Quaternion.identity, new Color(1f, 0.5f, 0.15f));
            }
            Glow(PrimitiveType.Sphere, at + Vector3.up * 1.05f, new Vector3(0.2f, 0.45f, 0.2f), Quaternion.identity, new Color(1f, 0.78f, 0.35f));
            var light = new GameObject("Brazier Light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at + Vector3.up * 1.3f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = 8f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<Flicker>().Seed = Mathf.RoundToInt(at.x * 5f + at.z * 3f);
        }
    }
}
