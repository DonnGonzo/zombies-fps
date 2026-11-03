using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        static bool RemodeledRoom(int zone) => zone == 4 || zone == 8 || zone == Gaol || zone == PlagueWard || zone == Scriptorium || zone == Furnace;

        void RemodelLight(int zone, float x, float y, float z, Color color)
        {
            var at = L(zone, x, y, z);
            Deco(at, new Vector3(0.7f, 0.15f, 0.7f), Soot, Surface.Metal);
            Glow(at - Vector3.up * 0.09f, new Vector3(0.5f, 0.03f, 0.5f), Quaternion.identity, color);
            var light = new GameObject("Authored room light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at - Vector3.up * 0.2f;
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 1.8f;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        void RemodelSign(int zone, string text, float x, float y, float z, float yaw, float width)
        {
            var at = L(zone, x, y, z);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Deco(at, new Vector3(width, 0.6f, 0.08f), rot, Soot, Surface.Wood);
            var holder = Greybox.Empty("Room wayfinding", Root, at + rot * Vector3.forward * 0.05f);
            holder.localRotation = rot;
            Greybox.Label("Lettering", holder, Vector3.zero, text, new Color(0.87f, 0.78f, 0.55f), 0.24f);
        }

        // Visual ceiling tiles follow every wing of the room; RoofColliders supplies the solid ceiling after baking.
        void RemodelCeiling(int zone, Color color, Surface surface)
        {
            foreach (var r in ZoneRects(zone))
            {
                int nx = Mathf.CeilToInt(r.width / 4f), nz = Mathf.CeilToInt(r.height / 4f);
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                        Deco(new Vector3(r.xMin + (x + 0.5f) * r.width / nx, 7.15f, r.yMin + (z + 0.5f) * r.height / nz),
                            new Vector3(r.width / nx, 0.3f, r.height / nz), color, surface);
                for (int z = 0; z < nz; z++)
                    Deco(new Vector3(r.center.x, 6.8f, r.yMin + (z + 0.5f) * r.height / nz),
                        new Vector3(r.width, 0.3f, 0.25f), Shade(color, 0.65f), surface);
            }
        }

        void RemodelRunner(int zone, float x, float z, float width, float depth, Color color)
        {
            Deco(L(zone, x, 0.012f, z), new Vector3(width, 0.024f, depth), color, Surface.Tile);
            Deco(L(zone, x - width / 2f + 0.1f, 0.028f, z), new Vector3(0.08f, 0.012f, depth), GateBrass, Surface.Metal);
            Deco(L(zone, x + width / 2f - 0.1f, 0.028f, z), new Vector3(0.08f, 0.012f, depth), GateBrass, Surface.Metal);
        }
    }
}
