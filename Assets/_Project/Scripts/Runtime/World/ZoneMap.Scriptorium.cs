using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        void BuildScriptorium()
        {
            foreach (float z in new[] { 0f, 6f, 11f })
                foreach (float x in new[] { -4f, 4f })
                {
                    PlacePackItem(WritingDesk, L(Scriptorium, x, 0f, z), Quaternion.identity, true);
                    PlacePackItem(Stool, L(Scriptorium, x, 0f, z - 0.9f), Quaternion.identity, true);
                }
            Block("Reader's dais", Scriptorium, -5f, 14f, 5f, 16.5f, 0.4f, Timber, Surface.Wood);
            Stairs(Scriptorium, 0f, 14f, Vector3.back, 0.4f, 3f);
            PlacePackItem(BookStand, L(Scriptorium, 0f, 0.4f, 15.5f), Quaternion.Euler(0f, 180f, 0f), true);
            foreach (float x in new[] { -7f, 7f, 20f })
                PlacePackItem(Bookcase, L(Scriptorium, x, 0f, 16.5f), Quaternion.Euler(0f, 180f, 0f), true);
            PlacePackItem(ReadingTable, L(Scriptorium, 14f, 0f, 8f), Quaternion.identity, true);
            PlacePackItem(LongTable, L(Scriptorium, 18f, 0f, 12f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(TallBookcase, L(Scriptorium, 21.5f, 0f, 5.5f), Quaternion.Euler(0f, 270f, 0f), true);
        }

        void ScriptoriumMakeover()
        {
            RemodelCeiling(Scriptorium, new Color(0.3f, 0.23f, 0.18f), Surface.Wood);
            RemodelRunner(Scriptorium, 0f, 3f, 3f, 21f, new Color(0.35f, 0.12f, 0.1f));
            RemodelRunner(Scriptorium, 11f, 10f, 2f, 11f, new Color(0.35f, 0.12f, 0.1f));
            var rng = new System.Random(14261);
            foreach (float z in new[] { 0f, 6f, 11f })
                foreach (float x in new[] { -4f, 4f })
                    TableTop(WritingDesk, L(Scriptorium, x, 0f, z), Quaternion.identity, Scriptorium, rng);
            TableTop(ReadingTable, L(Scriptorium, 14f, 0f, 8f), Quaternion.identity, Scriptorium, rng);
            foreach (float z in new[] { -4f, 4f, 12f })
            {
                // Clerestory light panels are flush with the high roof, with carved timber surrounds.
                Glow(L(Scriptorium, 0f, 6.97f, z), new Vector3(3f, 0.04f, 3f), Quaternion.identity, new Color(0.18f, 0.26f, 0.34f));
                foreach (float x in new[] { -2f, 2f })
                    Deco(L(Scriptorium, x, 6.6f, z), new Vector3(0.2f, 0.5f, 5f), Timber, Surface.Wood);
            }
            foreach (float x in new[] { -7f, 7f })
                Deco(L(Scriptorium, x, 3.9f, 17.6f), new Vector3(1f, 2.6f, 0.06f), new Color(0.32f, 0.1f, 0.1f), Surface.Fabric);
            RemodelSign(Scriptorium, "SCRIPTORIUM", 0f, 5.8f, 17.5f, 180f, 7f);
            RemodelSign(Scriptorium, "COPYING / BINDERY", 16f, 4f, 4.5f, 0f, 6f);
            RemodelSign(Scriptorium, "ARCHIVES", -1f, 4.5f, -7.5f, 0f, 4f);
            RemodelSign(Scriptorium, "GREENHOUSE", -8.5f, 4.5f, -6f, 90f, 4.5f);
            foreach (float z in new[] { -3f, 7f, 15f }) RemodelLight(Scriptorium, 0f, 6.5f, z, new Color(1f, 0.8f, 0.5f));
            RemodelLight(Scriptorium, 16f, 6.5f, 10f, new Color(1f, 0.8f, 0.5f));
        }
    }
}
