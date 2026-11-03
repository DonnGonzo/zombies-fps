using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        const int PumpZone = 7;
        static readonly Color PumpTeal = new Color(0.15f, 0.33f, 0.34f);
        static readonly Color PumpIron = new Color(0.2f, 0.24f, 0.25f);
        static readonly Color PumpCopper = new Color(0.53f, 0.29f, 0.15f);
        static Vector3 PS(float x, float y, float z) => L(PumpZone, x, y, z);

        void BuildPumpStation()
        {
            // All four door approaches stay on ground level. In particular the eastern exit at (19,3)
            // has no Pump House, posts, stairs or machinery in its approach rectangle (10..19, 0..6).
            // The control house moves into the new northwest annex; its collectible moves with it.
            Block("Pump control house", PumpZone, -9.5f, 12f, -4.5f, 18.5f, 2.4f, PumpIron, Surface.Masonry);
            Stairs(PumpZone, -4.5f, 15.25f, Vector3.right, 2.4f, 2.6f);
            AddLadder(PumpZone, -7f, 12f, Vector3.back, 2.4f);
            PlacePackItem(SfDeskSmall, PS(-7f, 2.4f, 17.8f), Quaternion.identity, true);
            PlacePackItem(SfLocker, PS(-8.4f, 0f, 10.5f), Quaternion.identity, true);
            // Two pump skids south of the east/west transit lane.
            PumpSkid(-10f, -3f);
            PumpSkid(2f, -3f);
            // Inspection tower keeps the mission beacon and shield-part anchors unchanged.
            Deck("Water tower inspection deck", PumpZone, 1.7f, 14f, 6.7f, 19f, TowerHeight, PumpIron);
            AddLadder(PumpZone, 4.2f, 14f, Vector3.back, TowerHeight);
            PumpTank("Tower pressure vessel", 4.2f, 0f, 16.5f, 3.5f, 3.7f);
            PumpTank("Settling vessel", 9f, 0f, 8.2f, 3.4f, 3.1f);
            // Back-up climb from the west also has a zombie navigation link.
            AddLadder(PumpZone, 1.7f, 16.5f, Vector3.left, TowerHeight);
            PlacePackItem(SfShelfLow, PS(-17.2f, 0f, 7.5f), Quaternion.Euler(0f, 90f, 0f), true);
            PlacePackItem(TechBarrel, PS(15.5f, 0f, 8.5f), Quaternion.identity, true);
            PlacePackItem(IronPail, PS(14f, 0f, 8.5f), Quaternion.identity, true);
        }

        void PumpSkid(float x, float z)
        {
            Greybox.Box("Pump skid", Root, PS(x, 0.15f, z), new Vector3(4.5f, 0.3f, 2.6f), PumpIron, surface: Surface.Metal);
            Greybox.Box("Pump motor", Root, PS(x - 1f, 0.9f, z), new Vector3(1.8f, 1.2f, 1.7f), PumpTeal, surface: Surface.Metal);
            var volute = Greybox.Prim(PrimitiveType.Cylinder, "Pump housing", Root, PS(x + 0.9f, 1.05f, z),
                new Vector3(1.65f, 0.55f, 1.65f), PumpTeal, surface: Surface.Metal);
            volute.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.DestroyImmediate(volute.GetComponent<Collider>());
            volute.AddComponent<MeshCollider>().convex = true;
        }

        void PumpTank(string name, float x, float bottom, float z, float diameter, float height)
        {
            var go = Greybox.Prim(PrimitiveType.Cylinder, name, Root, PS(x, bottom + height / 2f, z),
                new Vector3(diameter, height / 2f, diameter), PumpTeal, surface: Surface.Metal);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<MeshCollider>().convex = true;
            foreach (float y in new[] { bottom + 0.25f, bottom + height - 0.25f })
                DecoPrim(PrimitiveType.Cylinder, PS(x, y, z), new Vector3(diameter + 0.05f, 0.055f, diameter + 0.05f),
                    Quaternion.identity, PumpCopper, Surface.Rust);
        }

        void PumpStationMakeover()
        {
            PumpPipework();
            PumpRoof();
            // Wide, legible routes to Garage, Loading Dock, Rampart Ruins and Crane Yard.
            foreach (float z in new[] { 0.4f, 5.6f })
                Paint(PumpZone, -18f, z, 18f, z, 0.1f, FadedWhite);
            foreach (float x in new[] { -1f, 1f })
                Paint(PumpZone, x, 6f, x, 22f, 0.08f, FadedWhite);
            // The southern approach is offset to the Loading Dock door at (11,-7).
            foreach (float x in new[] { 9.5f, 12.5f })
                Paint(PumpZone, x, -6f, x, 0f, 0.09f, FadedWhite);
            PumpSign("RAMPART RUINS / KEEP CLEAR", PS(18.5f, 4.7f, 3f), 270f, 6.5f, 0.6f);
            PumpSign("PUMP STATION", PS(-18.5f, 4.7f, 0f), 90f, 5.5f, 0.75f);
            PumpSign("TOWER YARD / CRANE ACCESS", PS(0f, 4.7f, 22.5f), 180f, 7f, 0.6f);
            PumpSign("CONTROL HOUSE", PS(-7f, 2f, 11.85f), 180f, 4.2f, 0.45f);
            // Inspection floor and station instruments; part at (-7,2.4,15) stays unoccupied.
            Deco(PS(-7f, 2.405f, 15.25f), new Vector3(4.7f, 0.01f, 6.2f), PumpTeal, Surface.Tile);
            PlacePackItem(SfAmmo, PS(-7.5f, 3.34f, 17.8f), Quaternion.identity, false);
            LanternOn(PS(-6.2f, 3.34f, 17.8f), Quaternion.identity, true);
            // Narrow flush drains flank the machinery, not the door thresholds.
            foreach (float x in new[] { -13f, 5.2f })
            {
                Deco(PS(x, 0.005f, -3f), new Vector3(0.32f, 0.01f, 5f), Soot, Surface.Metal);
                for (int i = 0; i < 15; i++)
                    Deco(PS(x, 0.014f, -5.3f + i * 0.32f), new Vector3(0.34f, 0.018f, 0.05f), PumpIron, Surface.Metal);
            }
            foreach (var p in new[] { new Vector2(-9f, -4.5f), new Vector2(3f, -4.5f), new Vector2(9f, 10.3f) })
                Splat(PS(p.x, 0f, p.y), 2.2f, 0.8f, p.x * 8f, 0.008f, new Color(0.08f, 0.17f, 0.18f), Surface.Metal);
            PumpLight(PS(-10f, 5.2f, -3f), new Color(0.47f, 0.76f, 0.8f));
            PumpLight(PS(2f, 5.2f, -3f), new Color(0.47f, 0.76f, 0.8f));
            PumpLight(PS(12f, 5.8f, 3f), new Color(1f, 0.75f, 0.43f));
            PumpLight(PS(-7f, 5.8f, 16f), new Color(1f, 0.75f, 0.43f));
            PumpLight(PS(4.2f, 6.8f, 16.5f), new Color(0.7f, 0.82f, 0.9f));
            PumpLight(PS(0f, 5.8f, 7f), new Color(0.7f, 0.82f, 0.9f));
        }

        void PumpPipework()
        {
            foreach (float x in new[] { -10f, 2f })
            {
                // All risers stay on the pump skids; the horizontal main crosses above head height.
                Rod(PS(x + 0.9f, 1.4f, -3f), PS(x + 0.9f, 4.8f, -3f), 0.42f, PumpCopper, Surface.Rust);
                for (int fin = 0; fin < 9; fin++)
                    Deco(PS(x - 1.7f + fin * 0.18f, 1.1f, -2.12f), new Vector3(0.05f, 0.8f, 0.07f), PumpIron, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, PS(x + 0.9f, 1.5f, -2.4f), new Vector3(0.42f, 0.03f, 0.42f),
                    Quaternion.Euler(90f, 0f, 0f), new Color(0.72f, 0.7f, 0.59f), Surface.Plain);
                Bar(PS(x + 0.9f, 1.5f, -2.35f), PS(x + 1.02f, 1.63f, -2.35f), 0.025f, 0.025f, Vector3.forward, Soot, Surface.Metal);
            }
            Rod(PS(-9.1f, 4.8f, -3f), PS(9f, 4.8f, -3f), 0.5f, PumpCopper, Surface.Rust);
            Rod(PS(9f, 4.8f, -3f), PS(9f, 4.8f, 8.2f), 0.5f, PumpCopper, Surface.Rust);
            Rod(PS(9f, 4.8f, 8.2f), PS(9f, 3.1f, 8.2f), 0.5f, PumpCopper, Surface.Rust);
            for (float x = -8f; x <= 8f; x += 2f)
                DecoPrim(PrimitiveType.Cylinder, PS(x, 4.8f, -3f), new Vector3(0.7f, 0.055f, 0.7f),
                    Quaternion.Euler(0f, 0f, 90f), PumpIron, Surface.Metal);
            // The tower tank now has physical collision matching its shape, below its inspection deck.
            foreach (float z in new[] { 14.25f, 18.75f })
                Bar(PS(1.95f, 0.4f, z), PS(6.45f, 4f, z), 0.08f, 0.08f, Vector3.up, PumpCopper, Surface.Rust);
        }

        void PumpRoof()
        {
            // Tiled custom roof follows both expanded rectangles. No beams lower the tower's headroom.
            foreach (var r in ZoneRects(PumpZone))
            {
                int nx = Mathf.CeilToInt(r.width / 4f), nz = Mathf.CeilToInt(r.height / 4f);
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        var p = new Vector3(r.xMin + (x + 0.5f) * r.width / nx, 7.15f, r.yMin + (z + 0.5f) * r.height / nz);
                        if (x == nx / 2)
                            Glow(p, new Vector3(r.width / nx, 0.3f, r.height / nz), Quaternion.identity, new Color(0.12f, 0.22f, 0.25f));
                        else Deco(p, new Vector3(r.width / nx, 0.3f, r.height / nz), PumpIron, Surface.Metal);
                    }
                for (int z = 0; z < nz; z++)
                    Deco(new Vector3(r.center.x, 6.88f, r.yMin + z * r.height / nz), new Vector3(r.width, 0.16f, 0.15f), PumpCopper, Surface.Rust);
            }
        }

        void PumpLight(Vector3 at, Color tint)
        {
            Deco(at, new Vector3(1.2f, 0.15f, 0.35f), PumpIron, Surface.Metal);
            Glow(at - Vector3.up * 0.09f, new Vector3(1f, 0.03f, 0.22f), Quaternion.identity, tint);
            Rod(at, new Vector3(at.x, 7f, at.z), 0.03f, PumpIron, Surface.Metal);
            var light = new GameObject("Pump Station work light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at - Vector3.up * 0.2f;
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 1.5f;
            light.color = tint;
            light.shadows = LightShadows.None;
        }

        void PumpSign(string text, Vector3 at, float yaw, float width, float height)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, height, 0.06f), rot, PumpTeal, Surface.Metal);
            var holder = Greybox.Empty("Pump Station sign", Root, at + rot * Vector3.forward * 0.04f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.85f, 0.8f, 0.6f), height * 0.43f);
        }
    }
}
