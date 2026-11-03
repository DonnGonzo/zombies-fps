using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        void BuildFurnace()
        {
            var core = Greybox.Prim(PrimitiveType.Cylinder, "Alchemical furnace", Root, L(Furnace, 0f, 2.95f, 0f),
                new Vector3(4.6f, 2.95f, 4.6f), Copper, surface: Surface.Rust);
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.AddComponent<MeshCollider>().convex = true;
            var model = PropModels.GetPack(FurnaceCore.Path, PackMaterial);
            if (model != null)
            {
                core.GetComponent<Renderer>().enabled = false;
                AddModel(model, L(Furnace, 0f, 0f, 0f), Quaternion.identity, true, 0.95f);
            }
            foreach (float x in new[] { -13f, 13f })
            {
                PlacePackItem(Workbench, L(Furnace, x, 0f, 5f), Quaternion.identity, true);
                PlacePackItem(Cauldron, L(Furnace, x + 3f, 0f, 5f), Quaternion.identity, true);
                PlacePackItem(ApothecaryCabinet, L(Furnace, x, 0f, 11f), Quaternion.Euler(0f, 180f, 0f), true);
            }
            foreach (float x in new[] { -17f, 17f })
                Block("Coal bunker", Furnace, x - 1.5f, -6.5f, x + 1.5f, -4f, 0.8f, Soot, Surface.Masonry);
            PlacePackItem(AnvilLog, L(Furnace, -6f, 0f, 9f), Quaternion.identity, true);
            PlacePackItem(Grindstone, L(Furnace, 6f, 0f, 9f), Quaternion.identity, true);
        }

        void FurnaceMakeover()
        {
            RemodelCeiling(Furnace, new Color(0.24f, 0.22f, 0.2f), Surface.Metal);
            for (int i = 0; i < 32; i++)
            {
                var rot = Quaternion.Euler(0f, i * 11.25f, 0f);
                Deco(L(Furnace, 0f, 0.016f, 0f) + rot * Vector3.forward * 5.5f,
                    new Vector3(1f, 0.032f, 0.25f), rot, Copper, Surface.Metal);
            }
            foreach (float x in new[] { -13f, 13f })
            {
                Rod(L(Furnace, 0f, 6.1f, 0f), L(Furnace, x, 6.1f, 0f), 0.3f, Copper, Surface.Rust);
                Rod(L(Furnace, x, 6.1f, 0f), L(Furnace, x, 6.1f, 5f), 0.3f, Copper, Surface.Rust);
                PlacePackItem(Jars, L(Furnace, x, 0.89f, 5f), Quaternion.identity, false);
                RemodelLight(Furnace, x, 6.5f, 5f, new Color(0.48f, 0.77f, 0.68f));
            }
            Rod(L(Furnace, 0f, 5.7f, 0f), L(Furnace, 0f, 7f, 0f), 1.1f, Copper, Surface.Rust);
            Glow(L(Furnace, 0f, 1.2f, -2.32f), new Vector3(1.2f, 0.7f, 0.04f), Quaternion.identity, FurnaceGlow);
            foreach (float x in new[] { -17f, 17f })
                for (int i = 0; i < 8; i++)
                    DecoPrim(PrimitiveType.Sphere, L(Furnace, x - 1f + (i % 4) * 0.6f, 0.85f, -6f + (i / 4) * 1.2f),
                        new Vector3(0.7f, 0.4f, 0.6f), Quaternion.identity, Soot, Surface.Concrete);
            RemodelSign(Furnace, "ALCHEMIST'S FURNACE", 0f, 4.8f, 14.5f, 180f, 9f);
            RemodelSign(Furnace, "LANTERN CORE", -12.1f, 4.6f, -8.5f, 0f, 5f);
            RemodelSign(Furnace, "CRANE YARD", 12f, 4.6f, -10.5f, 0f, 4f);
            RemodelLight(Furnace, 0f, 3f, -3f, FurnaceGlow);
            RemodelLight(Furnace, 0f, 6.5f, 11f, new Color(1f, 0.75f, 0.45f));
        }
    }
}
