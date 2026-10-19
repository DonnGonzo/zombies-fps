using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// A roof over the whole map at the top of the walls, so nobody can look (or throw, or jump) over a wall into
    /// another room. Each room's ceiling matches it: timber boards on rafters in the halls and yards, stone vaults
    /// with ribs in the chapel, courtyard, gaol and temple, gilt coffers over the Archives, steel deck on girders round the Lantern Core and the
    /// furnace, and glass panes on rafters over the Greenhouse.
    ///
    /// The ceiling is drawn with the rest of the dressing, in tiles so each is lit by the lamps under it, and casts
    /// no shadow (the moon still reads as light through it). Its colliders go in after the NavMesh is baked, so the
    /// roof's top never becomes somewhere zombies think they can walk.
    /// </summary>
    public partial class ZoneMap
    {
        enum RoofStyle { Timber, Vault, Steel, Glass, Coffered }

        const float RoofThickness = 0.4f;

        static (RoofStyle style, Surface surface, Color color) RoofLook(int zone) => zone switch
        {
            3 or 5 or 12 or 17 => (RoofStyle.Vault, Surface.Concrete, new Color(0.36f, 0.35f, 0.33f)),
            9 or 15 => (RoofStyle.Steel, Surface.Metal, new Color(0.2f, 0.21f, 0.23f)),
            11 => (RoofStyle.Glass, Surface.Wood, new Color(0.24f, 0.17f, 0.11f)),
            10 => (RoofStyle.Coffered, Surface.Wood, new Color(0.3f, 0.2f, 0.12f)),
            _ => (RoofStyle.Timber, Surface.Wood, new Color(0.26f, 0.18f, 0.11f)),
        };

        void BuildRoof()
        {
            for (int zone = 0; zone < ZoneCount; zone++)
            {
                var (style, surface, color) = RoofLook(zone);
                foreach (var r in ZoneRects(zone))
                {
                    // The ceiling itself, in tiles.
                    int nx = Mathf.CeilToInt(r.width / FloorTile), nz = Mathf.CeilToInt(r.height / FloorTile);
                    for (int a = 0; a < nx; a++)
                        for (int b = 0; b < nz; b++)
                        {
                            var c = new Vector3(r.xMin + (a + 0.5f) * r.width / nx, WallHeight + RoofThickness / 2f, r.yMin + (b + 0.5f) * r.height / nz);
                            var size = new Vector3(r.width / nx, RoofThickness, r.height / nz);
                            if (style == RoofStyle.Glass) Glow(c, size, Quaternion.identity, new Color(0.32f, 0.45f, 0.45f, 0.35f));
                            else Deco(c, size, Quaternion.identity, Shade(color, 0.85f), surface);
                        }

                    // What holds it up, running across the rectangle's short way.
                    bool acrossX = r.width <= r.height;
                    float span = acrossX ? r.width : r.height, run = acrossX ? r.height : r.width;
                    float spacing = style switch { RoofStyle.Vault => 4f, RoofStyle.Coffered => 3f, RoofStyle.Steel => 4f, RoofStyle.Glass => 2f, _ => 2.5f };
                    var (depth, width) = style switch
                    {
                        RoofStyle.Vault => (0.55f, 0.45f),
                        RoofStyle.Steel => (0.45f, 0.22f),
                        RoofStyle.Glass => (0.18f, 0.12f),
                        RoofStyle.Coffered => (0.34f, 0.3f),
                        _ => (0.26f, 0.2f),
                    };
                    var memberColor = style == RoofStyle.Timber || style == RoofStyle.Coffered ? Shade(color, 0.7f) : color;
                    int count = Mathf.Max(1, Mathf.RoundToInt(run / spacing));
                    for (int k = 1; k < count; k++)
                    {
                        float t = (acrossX ? r.yMin : r.xMin) + k * run / count;
                        var at = acrossX ? new Vector3(r.center.x, WallHeight - depth / 2f, t) : new Vector3(t, WallHeight - depth / 2f, r.center.y);
                        var size = acrossX ? new Vector3(span, depth, width) : new Vector3(width, depth, span);
                        Deco(at, size, memberColor, surface);
                    }
                    if (style == RoofStyle.Vault)
                    {
                        // Ribs the long way too, so the stone reads as a run of vaulted bays, with a boss where they cross.
                        int across = Mathf.Max(1, Mathf.RoundToInt(span / spacing));
                        for (int k = 1; k < across; k++)
                        {
                            float t = (acrossX ? r.xMin : r.yMin) + k * span / across;
                            var at = acrossX ? new Vector3(t, WallHeight - depth / 2f, r.center.y) : new Vector3(r.center.x, WallHeight - depth / 2f, t);
                            Deco(at, acrossX ? new Vector3(width, depth, run) : new Vector3(run, depth, width), Shade(color, 1.1f), surface);
                            for (int j = 1; j < count; j++)
                            {
                                float u = (acrossX ? r.yMin : r.xMin) + j * run / count;
                                var boss = acrossX ? new Vector3(t, WallHeight - depth - 0.1f, u) : new Vector3(u, WallHeight - depth - 0.1f, t);
                                DecoPrim(PrimitiveType.Sphere, boss, new Vector3(0.6f, 0.35f, 0.6f), Quaternion.identity, Shade(color, 1.2f), surface);
                            }
                        }
                    }
                    else if (style == RoofStyle.Coffered)
                    {
                        // Beams the long way too, and in each coffer a dark sunken panel framed in gilt with a rosette.
                        int across = Mathf.Max(1, Mathf.RoundToInt(span / spacing));
                        for (int k = 1; k < across; k++)
                        {
                            float t = (acrossX ? r.xMin : r.yMin) + k * span / across;
                            var at = acrossX ? new Vector3(t, WallHeight - depth / 2f, r.center.y) : new Vector3(r.center.x, WallHeight - depth / 2f, t);
                            Deco(at, acrossX ? new Vector3(width, depth, run) : new Vector3(run, depth, width), memberColor, surface);
                        }
                        float cx = (acrossX ? span / across : run / count), cz = (acrossX ? run / count : span / across);
                        for (float x = r.xMin + cx / 2f; x < r.xMax; x += cx)
                            for (float z = r.yMin + cz / 2f; z < r.yMax; z += cz)
                            {
                                var mid = new Vector3(x, WallHeight - 0.01f, z);
                                Deco(mid, new Vector3(cx - width - 0.1f, 0.012f, cz - width - 0.1f), OldGold, Surface.Metal);
                                Deco(mid - Vector3.up * 0.002f, new Vector3(cx - width - 0.24f, 0.012f, cz - width - 0.24f), Shade(color, 0.55f), surface);
                                DecoPrim(PrimitiveType.Sphere, mid - Vector3.up * 0.03f, new Vector3(0.22f, 0.08f, 0.22f), Quaternion.identity, OldGold, Surface.Metal);
                            }
                    }
                    else if (style == RoofStyle.Steel)
                    {
                        // A bottom flange on each girder, so they read as I-beams.
                        for (int k = 1; k < count; k++)
                        {
                            float t = (acrossX ? r.yMin : r.xMin) + k * run / count;
                            var at = acrossX ? new Vector3(r.center.x, WallHeight - depth, t) : new Vector3(t, WallHeight - depth, r.center.y);
                            Deco(at, acrossX ? new Vector3(span, 0.05f, 0.4f) : new Vector3(0.4f, 0.05f, span), Shade(color, 1.3f), Surface.Metal);
                        }
                    }
                }
            }
        }

        /// <summary>The roof's solid part, one slab per rectangle (after the NavMesh bake).</summary>
        void RoofColliders()
        {
            var holder = Greybox.Empty("Roof", Root, Vector3.zero);
            for (int zone = 0; zone < ZoneCount; zone++)
                foreach (var r in ZoneRects(zone))
                {
                    var go = new GameObject("Ceiling " + ZoneNames[zone]);
                    go.transform.SetParent(holder, false);
                    go.transform.localPosition = new Vector3(r.center.x, WallHeight + RoofThickness / 2f, r.center.y);
                    go.isStatic = true;
                    // A little past the rectangle's edges, so there's no crack where it meets the walls.
                    go.AddComponent<BoxCollider>().size = new Vector3(r.width + WallThickness, RoofThickness, r.height + WallThickness);
                }
        }
    }
}
