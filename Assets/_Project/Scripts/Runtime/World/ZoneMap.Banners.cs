using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Each room's own colours: heraldic banners high on its walls, and the tint of its lamps, so every part of the
    /// map reads as its own place at a glance.
    /// </summary>
    public partial class ZoneMap
    {
        /// <summary>Banner cloth and trim per zone (zone order).</summary>
        static readonly (Color cloth, Color trim)[] BannerColors =
        {
            (new Color(0.42f, 0.08f, 0.07f), new Color(0.68f, 0.52f, 0.18f)), // Gate Yard
            (new Color(0.16f, 0.26f, 0.12f), new Color(0.6f, 0.46f, 0.2f)),   // Depot
            (new Color(0.08f, 0.08f, 0.08f), new Color(0.75f, 0.36f, 0.1f)),  // Garage
            (new Color(0.25f, 0.1f, 0.3f), new Color(0.7f, 0.56f, 0.2f)),     // Chapel
            (new Color(0.45f, 0.08f, 0.08f), new Color(0.75f, 0.72f, 0.64f)), // Barracks
            (new Color(0.1f, 0.18f, 0.38f), new Color(0.75f, 0.72f, 0.64f)),  // Courtyard
            (new Color(0.12f, 0.28f, 0.18f), new Color(0.75f, 0.72f, 0.64f)), // Loading Dock
            (new Color(0.1f, 0.24f, 0.32f), new Color(0.3f, 0.55f, 0.5f)),    // Pump Station
            (new Color(0.3f, 0.2f, 0.1f), new Color(0.72f, 0.55f, 0.12f)),    // Crane Yard
            (new Color(0.2f, 0.08f, 0.28f), new Color(0.3f, 0.7f, 0.75f)),    // Lantern Core
            (new Color(0.08f, 0.2f, 0.12f), new Color(0.7f, 0.56f, 0.2f)),    // Archives
            (new Color(0.2f, 0.3f, 0.12f), new Color(0.75f, 0.7f, 0.55f)),    // Greenhouse
            (new Color(0.07f, 0.07f, 0.07f), new Color(0.5f, 0.08f, 0.06f)),  // Gaol
            (new Color(0.68f, 0.64f, 0.55f), new Color(0.5f, 0.08f, 0.06f)),  // Plague Ward
            (new Color(0.1f, 0.14f, 0.36f), new Color(0.72f, 0.58f, 0.2f)),   // Scriptorium
            (new Color(0.1f, 0.08f, 0.07f), new Color(0.8f, 0.38f, 0.1f)),    // Alchemist's Furnace
            (new Color(0.42f, 0.08f, 0.07f), new Color(0.68f, 0.52f, 0.18f)), // Rampart Ruins
            (new Color(0.1f, 0.3f, 0.32f), new Color(0.72f, 0.58f, 0.22f)),   // Sunken Temple
        };

        /// <summary>The colour of a room's lamps: warm everywhere, a little colder in the stone rooms, hotter by the furnace.</summary>
        static Color LampTint(int zone) => zone switch
        {
            12 => new Color(0.85f, 0.88f, 0.8f),
            3 or 17 => new Color(1f, 0.82f, 0.55f),
            9 or 15 => new Color(1f, 0.7f, 0.42f),
            5 or 16 => new Color(0.82f, 0.86f, 1f),
            _ => Lamp,
        };

        /// <summary>Banners hung from poles high on the walls, about every nine metres where the wall is clear.</summary>
        void HangBanners()
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Length < 6f || f.Zone < 0 || f.Zone >= BannerColors.Length || f.Zone == LabZone) continue;
                var (cloth, trim) = BannerColors[f.Zone];
                var rng = new System.Random(3300 + i * 97);
                for (float t = Range(rng, 2.5f, 4.5f); t < f.Length - 2.5f; t += Range(rng, 8f, 11f))
                {
                    counts.TryGetValue(f.Zone, out int n);
                    if (n >= 8) break;
                    // From 6.2 m down to 3.4 m, clear of lamps, windows and everything else on the wall.
                    if (!WallFree(f, t, 4.8f, 0.8f, 1.5f, 0.15f)) continue;
                    counts[f.Zone] = n + 1;
                    var rot = f.Facing;
                    Rod(f.At(t - 0.75f, 6.25f, 0.12f), f.At(t + 0.75f, 6.25f, 0.12f), 0.05f, new Color(0.14f, 0.1f, 0.07f), Surface.Wood);
                    Deco(f.At(t, 4.85f, 0.09f), new Vector3(1.3f, 2.7f, 0.03f), rot, cloth, Surface.Fabric);
                    Deco(f.At(t, 4.85f, 0.105f), new Vector3(0.12f, 2.7f, 0.01f), rot, trim, Surface.Fabric);
                    Deco(f.At(t, 5.7f, 0.105f), new Vector3(1.3f, 0.12f, 0.01f), rot, trim, Surface.Fabric);
                    // A swallowtail at the foot.
                    foreach (float s in new[] { -0.33f, 0.33f })
                        Deco(f.At(t + s, 3.38f, 0.09f), new Vector3(0.64f, 0.36f, 0.03f), rot * Quaternion.Euler(0f, 0f, s > 0f ? 12f : -12f), cloth, Surface.Fabric);
                }
            }
        }
    }
}
