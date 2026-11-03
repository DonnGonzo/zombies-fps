using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        static readonly Color WardGreen = new Color(0.28f, 0.4f, 0.34f);
        void BuildPlagueWard()
        {
            // Beds have substantial headboards: cluster them in bays rather than choking the exit aisle.
            foreach (float x in new[] { -6f, 2f, 10f })
            {
                PlacePackItem(SickBed, L(PlagueWard, x, 0f, 5f), Quaternion.Euler(0f, 180f, 0f), true);
                PlacePackItem(Nightstand, L(PlagueWard, x + 2f, 0f, 5.5f), Quaternion.identity, true);
            }
            foreach (float x in new[] { -6f, 1f })
                PlacePackItem(SickBed, L(PlagueWard, x, 0f, -5.5f), Quaternion.identity, true);
            // Rear dispensary has a wide entry at x=-13 and a loop around the work island.
            PlacePackItem(LongTable, L(PlagueWard, -7f, 0f, -18f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(ApothecaryCabinet, L(PlagueWard, -18.8f, 0f, -16f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(ApothecaryCabinet, L(PlagueWard, -18.8f, 0f, -20f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(Cauldron, L(PlagueWard, -4f, 0f, -21f), Quaternion.identity, true);
            PlacePackItem(Workbench, L(PlagueWard, -16f, 0f, 5f), Quaternion.identity, true);
            PlacePackItem(Bench, L(PlagueWard, 16f, 0f, 5.5f), Quaternion.Euler(0f, 180f, 0f), true);
        }

        void PlagueWardMakeover()
        {
            RemodelCeiling(PlagueWard, new Color(0.47f, 0.45f, 0.36f), Surface.Plaster);
            RemodelRunner(PlagueWard, -13f, -8f, 3f, 30f, WardGreen);
            Deco(L(PlagueWard, 0f, 0.012f, 0f), new Vector3(38f, 0.024f, 3.6f), WardGreen, Surface.Tile);
            foreach (float x in new[] { -9.5f, -2.5f, 5.5f, 13.5f })
            {
                Rod(L(PlagueWard, x, 3.5f, 3f), L(PlagueWard, x, 3.5f, 7.5f), 0.06f, Soot, Surface.Metal);
                // Gathered curtains leave the aisle and foot of each bed visible.
                Deco(L(PlagueWard, x, 2f, 6.5f), new Vector3(0.06f, 2.8f, 1.5f), new Color(0.63f, 0.61f, 0.48f), Surface.Fabric);
                Rod(L(PlagueWard, x, 3.5f, 7.5f), L(PlagueWard, x, 6.8f, 7.5f), 0.025f, Soot, Surface.Metal);
            }
            PlacePackItem(Jars, L(PlagueWard, -7f, 0.81f, -18f), Quaternion.Euler(0f, 90f, 0f), false);
            PlacePackItem(StewPot, L(PlagueWard, -16f, 0.89f, 5f), Quaternion.identity, false);
            foreach (float x in new[] { -6f, 2f, 10f })
                RemodelSign(PlagueWard, "ISOLATION", x, 3.4f, 7.7f, 180f, 3f);
            RemodelSign(PlagueWard, "DISPENSARY", -13f, 4.7f, -10f, 0f, 5f);
            RemodelSign(PlagueWard, "DEPOT", -14f, 4.6f, 8.5f, 180f, 4f);
            RemodelSign(PlagueWard, "LOADING DOCK", 19.5f, 4.6f, 4f, 270f, 5f);
            foreach (float x in new[] { -14f, 0f, 14f }) RemodelLight(PlagueWard, x, 6.6f, 0f, new Color(0.7f, 0.86f, 0.76f));
            RemodelLight(PlagueWard, -12f, 6.6f, -19f, new Color(1f, 0.77f, 0.45f));
        }
    }
}
