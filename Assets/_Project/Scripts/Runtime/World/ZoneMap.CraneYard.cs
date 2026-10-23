using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        void BuildCraneYard()
        {
            // Mission beacon and part remain on the freight stack, now reachable by stairs as well as ladder.
            Block("Inspection freight stack", 8, 4f, 6f, 12f, 12f, 3.6f, ContainerBlue, Surface.Metal);
            Stairs(8, 8f, 6f, Vector3.back, 3.6f, 3f);
            AddLadder(8, 12f, 9f, Vector3.right, 3.6f);
            Block("West freight", 8, -17f, -11f, -11f, -7f, 1.1f, ContainerGreen, Surface.Metal);
            Block("East freight", 8, 16f, 6f, 20f, 9f, 1.1f, ContainerBlue, Surface.Metal);
            // These low stacks are jumpable by players but higher than the agent's step.
            // Give both sides a bidirectional climb so they cannot become safe camping spots.
            Jump(8, -14f, 0f, -5.8f, -14f, 1.1f, -8.2f);
            Jump(8, -9.8f, 0f, -9f, -12.2f, 1.1f, -9f);
            Jump(8, 18f, 0f, 4.8f, 18f, 1.1f, 7.2f);
            Jump(8, 14.8f, 0f, 7.5f, 17.2f, 1.1f, 7.5f);
            foreach (float x in new[] { -14f, 14f })
            {
                Greybox.Box("Gantry leg", Root, L(8, x, 4.5f, -6f), new Vector3(0.8f, 9f, 1.2f), CraneYellow, surface: Surface.Metal);
                Greybox.Box("Gantry foot", Root, L(8, x, 0.2f, -6f), new Vector3(2f, 0.4f, 3f), Soot, surface: Surface.Metal);
            }
            PlacePackItem(SfShelfWide, L(8, -19f, 0f, -11f), Quaternion.identity, true);
            PlacePackItem(SfCrate, L(8, -17f, 0f, 5f), Quaternion.identity, true);
            PlacePackItem(PipeRack, L(8, 17f, 0f, -10f), Quaternion.identity, true);
            PlacePackItem(SfBarrel, L(8, 19f, 0f, -9f), Quaternion.identity, true);
        }

        void CraneYardMakeover()
        {
            ContainerDetails(8, 4f, 6f, 12f, 12f, 3.6f, ContainerBlue);
            ContainerDetails(8, -17f, -11f, -11f, -7f, 1.1f, ContainerGreen);
            ContainerDetails(8, 16f, 6f, 20f, 9f, 1.1f, ContainerBlue);
            foreach (float z in new[] { -6.6f, -5.4f })
            {
                Deco(L(8, 0f, 8.7f, z), new Vector3(29f, 0.3f, 0.25f), CraneYellow, Surface.Metal);
                for (float x = -13f; x < 14f; x += 2f)
                    Bar(L(8, x, 8.7f, z), L(8, x + 2f, 7.8f, z), 0.12f, 0.12f, Vector3.forward, CraneYellow, Surface.Metal);
                Deco(L(8, 0f, 7.8f, z), new Vector3(29f, 0.25f, 0.25f), CraneYellow, Surface.Metal);
            }
            Deco(L(8, 3f, 8f, -6f), new Vector3(2.4f, 0.5f, 1.8f), Soot, Surface.Metal);
            Rod(L(8, 3f, 7.8f, -6f), L(8, 3f, 4.8f, -6f), 0.07f, Soot, Surface.Metal);
            Bar(L(8, 3f, 4.8f, -6f), L(8, 3.4f, 4.5f, -6f), 0.15f, 0.15f, Vector3.forward, CraneYellow, Surface.Metal);
            foreach (float z in new[] { -4.5f, -1.5f }) Paint(8, -22f, z, 22f, z, 0.1f, FadedYellow);
            foreach (float x in new[] { 0.5f, 3.5f }) Paint(8, x, -20f, x, -5f, 0.1f, FadedWhite);
            foreach (float x in new[] { -14f, 14f })
                Deco(L(8, x, 0.008f, 0f), new Vector3(0.12f, 0.016f, 27f), Soot, Surface.Metal);
            RemodelSign(8, "CRANE YARD / FREIGHT 08", 0f, 5.5f, 14.5f, 180f, 9f);
            RemodelSign(8, "PUMP STATION", 2f, 4.6f, -20.5f, 0f, 5f);
            RemodelSign(8, "RAMPART RUINS", 22.5f, 4.6f, 1f, 270f, 5f);
            foreach (float x in new[] { -14f, 14f }) RemodelLight(8, x, 6.9f, -6f, new Color(1f, 0.78f, 0.45f));
            Rod(L(8, 11.6f, 3.6f, 10f), L(8, 11.6f, 6.9f, 10f), 0.09f, Soot, Surface.Metal);
            Bar(L(8, 11.6f, 6.9f, 10f), L(8, 8f, 6.9f, 10f), 0.1f, 0.1f, Vector3.up, Soot, Surface.Metal);
            RemodelLight(8, 8f, 6.7f, 10f, new Color(0.55f, 0.75f, 1f));
        }
    }
}
