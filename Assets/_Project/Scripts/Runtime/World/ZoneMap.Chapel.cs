using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Chapel as a gothic church: stone piers down both nave walls carrying ribs across the vault, banners and
    /// torches on the piers, four rows of pews either side of the aisle, the Ten Worlds altar on its dais at the
    /// head of the nave with candle stands either side, the pipe organ on the choir loft, a rose inlaid in the
    /// crossing floor, and a carved tomb with its effigy in each transept arm.
    /// </summary>
    public partial class ZoneMap
    {
        const int ChapelZone = 3;

        static readonly Color PierStone = new Color(0.46f, 0.44f, 0.4f);

        /// <summary>The nave's piers (x, z, zone-local): the west wall's, then the east wall's, where nothing else needs the wall.</summary>
        static readonly Vector2[] ChapelPiers =
        {
            new Vector2(-7.4f, -17.75f), new Vector2(-7.4f, -12.25f), new Vector2(-7.4f, -7.45f), new Vector2(-7.4f, 7.45f),
            new Vector2(-7.4f, 11.25f), new Vector2(-7.4f, 16.75f),
            new Vector2(7.4f, -17.75f), new Vector2(7.4f, -12.25f), new Vector2(7.4f, -7.45f), new Vector2(7.4f, 7.45f),
        };

        static readonly Vector3 ChapelAltar = new Vector3(-3f, 0f, 12f);

        /// <summary>The tombs in the transept arms (x, z, zone-local), long east-west.</summary>
        static readonly Vector2[] ChapelTombs = { new Vector2(-11.5f, 3.4f), new Vector2(-11.5f, -3.4f), new Vector2(11.5f, 3.4f), new Vector2(11.5f, -3.4f) };

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildChapel()
        {
            Block("Choir Loft", ChapelZone, 2f, 13f, 7.7f, 20.7f, 2.6f, DarkWood, Surface.Wood);
            Stairs(ChapelZone, 2f, 16f, Vector3.left, 2.6f, 2.4f);

            var pew = new Color(0.3f, 0.19f, 0.11f);
            foreach (float z in new[] { -19f, -16.5f, -14f, -11.5f })
                foreach (float x in new[] { -4.2f, 4.2f })
                    Reskin(Greybox.Box("Pew", Root, L(ChapelZone, x, 0.45f, z), new Vector3(4.4f, 0.9f, 0.6f), pew, surface: Surface.Wood), Pew, false, Pew.Size.y);

            foreach (var p in ChapelPiers)
                Greybox.Box("Pier", Root, L(ChapelZone, p.x, WallHeight / 2f, p.y), new Vector3(0.6f, WallHeight, 0.9f), PierStone, surface: Surface.Concrete);

            // The altar on its dais at the head of the nave, facing the pews, and the organ against the loft's back wall.
            PlacePackItem(AltarDais, L(ChapelZone, ChapelAltar.x, 0f, ChapelAltar.z), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(Organ, L(ChapelZone, 5.2f, 2.6f, 20.7f - Organ.Size.z / 2f - 0.02f), Quaternion.Euler(0f, 180f, 0f), false);

            foreach (var t in ChapelTombs)
                Greybox.Box("Tomb", Root, L(ChapelZone, t.x, 0.4f, t.y), new Vector3(2.2f, 0.8f, 1f), Shade(PierStone, 0.9f), surface: Surface.Concrete);
        }

        // ---------------------------------------------------------------- dressing

        void ChapelMakeover()
        {
            var rng = new System.Random(3303);

            // The piers: a moulded base, a capital, a rib from each across the vault, and on each a torch or a banner.
            for (int i = 0; i < ChapelPiers.Length; i++)
            {
                var p = ChapelPiers[i];
                var foot = L(ChapelZone, p.x, 0f, p.y);
                Deco(foot + Vector3.up * 0.25f, new Vector3(0.8f, 0.5f, 1.15f), Shade(PierStone, 0.85f), Surface.Concrete);
                Deco(foot + Vector3.up * 5.75f, new Vector3(0.8f, 0.35f, 1.15f), Shade(PierStone, 0.85f), Surface.Concrete);
                Deco(foot + Vector3.up * 5.5f, new Vector3(0.68f, 0.08f, 1f), Shade(OldGold, 0.8f), Surface.Metal);
                var inward = p.x < 0f ? Vector3.right : Vector3.left;
                var face = foot + inward * 0.3f;
                var facing = Quaternion.LookRotation(inward);
                if (i % 2 == 0)
                {
                    if (HangItem(WallTorch, face, facing, 2.3f)) CandleLight(face + Vector3.up * 2.65f + inward * 0.32f);
                }
                else HangItem(i % 4 == 1 ? HangingBanner1 : HangingBanner2, face, facing, 5.2f);
            }
            foreach (float z in new[] { -17.75f, -12.25f, -7.45f, 7.45f })
            {
                Deco(L(ChapelZone, 0f, 6.78f, z), new Vector3(15.4f, 0.4f, 0.5f), Shade(PierStone, 1.05f), Surface.Concrete);
                Deco(L(ChapelZone, 0f, 6.56f, z), new Vector3(15.4f, 0.05f, 0.3f), Shade(OldGold, 0.7f), Surface.Metal);
            }

            // Candle stands either side of the altar, a lectern facing the pews, and things on the altar.
            foreach (float x in new[] { -6.6f, 0.6f })
                if (PlaceFree(CandleStand, L(ChapelZone, x, 0f, ChapelAltar.z), Quaternion.identity, true)) CandleLight(L(ChapelZone, x, 1.45f, ChapelAltar.z));
            PlaceFree(BookStand, L(ChapelZone, 2.6f, 0f, 9.2f), Quaternion.Euler(0f, 200f, 0f), true);
            if (PropModels.GetPack(AltarDais.Path, PackMaterial) != null)
                foreach (var (item, x) in new[] { ("CandleStick_Triple", -1.6f), ("Chalice", -0.3f), ("Book_7", 0.5f), ("CandleStick_Triple", 1.6f) })
                {
                    var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                    if (model != null) AddModel(model, L(ChapelZone, ChapelAltar.x + x, 1.75f, ChapelAltar.z + 0.15f), Quaternion.Euler(0f, 180f, 0f), false);
                }

            // A rose inlaid in the crossing floor: a dark stone disc, a gold ring and eight gold petals.
            var rose = L(ChapelZone, 0f, 0f, 0f);
            DecoPrim(PrimitiveType.Cylinder, rose + Vector3.up * 0.005f, new Vector3(6.4f, 0.005f, 6.4f), Quaternion.identity, Shade(OldGold, 0.6f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, rose + Vector3.up * 0.008f, new Vector3(6f, 0.005f, 6f), Quaternion.identity, new Color(0.17f, 0.16f, 0.16f), Surface.Tile);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(0f, k * 45f, 0f);
                Deco(rose + Vector3.up * 0.012f + r * new Vector3(0f, 0f, 1.6f), new Vector3(0.5f, 0.006f, 2.2f), r, Shade(OldGold, 0.75f), Surface.Metal);
            }
            DecoPrim(PrimitiveType.Cylinder, rose + Vector3.up * 0.014f, new Vector3(1f, 0.005f, 1f), Quaternion.identity, Shade(OldGold, 0.9f), Surface.Metal);

            // The tombs: a lid, the knight carved lying on it, his sword, and candles at his feet.
            foreach (var t in ChapelTombs)
            {
                var top = L(ChapelZone, t.x, 0.8f, t.y);
                float head = t.x < 0f ? 1f : -1f;   // heads to the crossing
                Deco(top + Vector3.up * 0.06f, new Vector3(2.35f, 0.12f, 1.12f), Shade(PierStone, 1.05f), Surface.Concrete);
                Deco(top + new Vector3(-head * 0.1f, 0.24f, 0f), new Vector3(1.5f, 0.24f, 0.5f), Shade(PierStone, 1.15f), Surface.Concrete);
                DecoPrim(PrimitiveType.Sphere, top + new Vector3(head * 0.78f, 0.28f, 0f), new Vector3(0.26f, 0.24f, 0.28f), Quaternion.identity, Shade(PierStone, 1.15f), Surface.Concrete);
                Deco(top + new Vector3(-head * 0.75f, 0.3f, 0f), new Vector3(0.18f, 0.3f, 0.36f), Shade(PierStone, 1.1f), Surface.Concrete);
                var sword = PropModels.GetPack(Fantasy + "Sword_Bronze", PackMaterial);
                if (sword != null) AddModel(sword, top + new Vector3(0f, 0.37f, 0f), Quaternion.Euler(0f, 0f, head * 90f) * Quaternion.Euler(0f, 90f, 0f), false);
                var candle = PropModels.GetPack(Fantasy + (rng.NextDouble() < 0.5 ? "Candle_1" : "Candle_2"), PackMaterial);
                if (candle != null)
                    foreach (float z in new[] { -0.4f, 0.4f })
                        AddModel(candle, top + new Vector3(-head * 1.05f, 0.12f, z), Quaternion.identity, false);
            }

            // Chests of old vestments at the ends of the transept arms.
            foreach (var p in new[] { new Vector2(-18.4f, 4.6f), new Vector2(-18.4f, -4.6f), new Vector2(18.4f, 4.6f), new Vector2(18.4f, -4.6f) })
                PlaceFree(WoodChest, L(ChapelZone, p.x, 0f, p.y), Quaternion.Euler(0f, p.x < 0f ? 90f : -90f, 0f), true);
        }
    }
}
