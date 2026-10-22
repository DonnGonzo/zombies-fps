using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        void BuildGaol()
        {
            foreach (float x in new[] { -5f, 3f })
            {
                PlacePackItem(GaolCell, L(Gaol, x, 0f, -6f), Quaternion.identity, true);

            }
            foreach (float x in new[] { -6f, 6f })
                PlacePackItem(GaolCell, L(Gaol, x, 0f, 3.5f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(GaolCell, L(Gaol, 11f, 0f, 3.5f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(LongTable, L(Gaol, 5.5f, 0f, 9.5f), Quaternion.identity, true);
            PlacePackItem(KitChair, L(Gaol, 5.5f, 0f, 10.7f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(WeaponRack, L(Gaol, 8.5f, 0f, 8f), Quaternion.Euler(0f, 270f, 0f), true);
            PlacePackItem(WoodChest, L(Gaol, -6f, 0f, 9.5f), Quaternion.identity, true);
            PlacePackItem(KitBarrel, L(Gaol, 15.5f, 0f, -5.5f), Quaternion.identity, true);
            PlacePackItem(LongBench, L(Gaol, -12f, 0f, 1f), Quaternion.identity, true);
        }

        void GaolMakeover()
        {
            RemodelCeiling(Gaol, new Color(0.32f, 0.35f, 0.34f), Surface.Masonry);
            Deco(L(Gaol, 0f, 0.01f, -1f), new Vector3(32f, 0.02f, 3f), new Color(0.23f, 0.27f, 0.26f), Surface.Tile);
            RemodelRunner(Gaol, 0f, 7f, 3f, 10f, new Color(0.23f, 0.27f, 0.26f));
            // Vault ribs arch over the patrol aisle without adding a perch or obstruction.
            foreach (float x in new[] { -13f, -7f, 1f, 9f, 15f })
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 12f, b = (i + 1) * Mathf.PI / 12f;
                    Bar(L(Gaol, x, 4.4f + Mathf.Sin(a) * 2.2f, -2f + Mathf.Cos(a) * 7.5f),
                        L(Gaol, x, 4.4f + Mathf.Sin(b) * 2.2f, -2f + Mathf.Cos(b) * 7.5f),
                        0.24f, 0.35f, Vector3.right, Stone, Surface.Ashlar);
                }
            foreach (float x in new[] { -5f, 3f })
            {
                RemodelSign(Gaol, "CELL " + (x < 0 ? "01" : "02"), x, 4.4f, -6f, 0f, 2.4f);
                PlacePackItem(Bones, L(Gaol, x, 0f, -6f), Quaternion.identity, false);
            }
            PlacePackItem(Candles, L(Gaol, 6f, 0.81f, 9.5f), Quaternion.identity, false);
            PlacePackItem(Cage, L(Gaol, -6f, 0.69f, 9.5f), Quaternion.identity, false);
            RemodelSign(Gaol, "GATE YARD", 0f, 4.5f, 11.5f, 180f, 5f);
            RemodelSign(Gaol, "PLAGUE WARD", 17.5f, 4.5f, 0f, 270f, 5f);
            for (float x = -14f; x < 17f; x += 0.5f)
                Deco(L(Gaol, x, 0.025f, -2.5f), new Vector3(0.2f, 0.02f, 0.45f), Soot, Surface.Metal);
            foreach (float x in new[] { -12f, 0f, 12f }) RemodelLight(Gaol, x, 6.5f, -1f, new Color(0.55f, 0.69f, 0.75f));
            RemodelLight(Gaol, 4f, 6.5f, 9f, new Color(1f, 0.7f, 0.37f));
        }
    }
}
