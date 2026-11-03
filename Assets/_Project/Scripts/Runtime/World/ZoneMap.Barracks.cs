using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        void BuildBarracks()
        {
            // Retain the west gallery and its mission-part position, with a wide stair and ladder escape.
            Block("Quartermaster gallery", 4, -10.7f, -8f, -5.7f, 8f, 2.6f, Timber, Surface.Wood);
            Stairs(4, -8.2f, 8f, Vector3.forward, 2.6f, 2.8f);
            AddLadder(4, -5.7f, -3f, Vector3.right, 2.6f);
            foreach (float z in new[] { -10f, -5f, 6f, 11f, 18f })
                foreach (float x in new[] { 0f, 6f })
                {
                    PlacePackItem(Bunk1, L(4, x, 0f, z), Quaternion.identity, true);
                    PlacePackItem(WoodChest, L(4, x, 0f, z - 1.9f), Quaternion.identity, true);
                }
            PlacePackItem(LongTable, L(4, -8f, 2.6f, 4.5f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(WeaponRack, L(4, -8f, 2.6f, -0.5f), Quaternion.identity, true);
            PlacePackItem(Dummy, L(4, -7f, 0f, -19f), Quaternion.identity, true);
            PlacePackItem(WeaponRack, L(4, -4f, 0f, -20f), Quaternion.identity, true);
            PlacePackItem(LongBench, L(4, -7f, 0f, 19f), Quaternion.identity, true);
        }

        void BarracksMakeover()
        {
            RemodelCeiling(4, new Color(0.36f, 0.29f, 0.21f), Surface.Wood);
            RemodelRunner(4, 3f, -1f, 3f, 41f, new Color(0.27f, 0.32f, 0.24f));
            Deco(L(4, 0f, 0.012f, 2f), new Vector3(21f, 0.024f, 2.6f), new Color(0.4f, 0.36f, 0.28f), Surface.Wood);
            // The authored bunks already include mattresses and bedding.
            foreach (float z in new[] { -14f, -1f, 12f })
            {
                Deco(L(4, 0f, 6.45f, z), new Vector3(21.5f, 0.25f, 0.35f), Timber, Surface.Wood);
                Deco(L(4, 10.6f, 4.6f, z), new Vector3(0.04f, 2f, 1.1f), GateBlue, Surface.Fabric);
            }
            PlacePackItem(Candles, L(4, -8f, 3.41f, 4.5f), Quaternion.identity, false);
            RemodelSign(4, "QUARTERMASTER", -5.6f, 3.8f, 1f, 90f, 5f);
            RemodelSign(4, "COURTYARD", 1f, 4.6f, -22.5f, 0f, 5f);
            RemodelSign(4, "GREENHOUSE", 0f, 4.6f, 22.5f, 180f, 5f);
            RemodelSign(4, "CHAPEL", 10.5f, 4.6f, 2f, 270f, 4f);
            foreach (float z in new[] { -16f, -3f, 10f, 20f }) RemodelLight(4, 3f, 6.5f, z, new Color(1f, 0.78f, 0.49f));
            RemodelLight(4, -8f, 6.5f, 0f, new Color(0.7f, 0.8f, 0.95f));
        }
    }
}
