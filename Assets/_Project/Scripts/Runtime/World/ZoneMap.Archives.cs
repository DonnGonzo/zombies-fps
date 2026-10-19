using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Archives made over as a great library: every free stretch of wall lined with bookcases nearly to the
    /// ceiling, two long reading tables with chairs, books and candles in the east bay, candle stands flanking the
    /// monks' altar, lecterns, oak bookcases in front of the records loft instead of the old filing cabinets, and
    /// more books and scrolls dropped on the floor. Everything is placed only where it fits: clear of the doors,
    /// windows, the perk, the generator, the build parts and the altar's ring of pedestals.
    /// </summary>
    public partial class ZoneMap
    {
        const int ArchivesZone = 10;

        /// <summary>Before the room's random furniture, so the bookcases get the walls.</summary>
        void ArchivesMakeover()
        {
            LineArchiveWalls();

            // Reading tables in the east bay, each with three chairs a side.
            ReadingTableSet(L(ArchivesZone, 11f, 0f, 1f), Quaternion.identity, 101);
            ReadingTableSet(L(ArchivesZone, 9f, 0f, 6f), Quaternion.identity, 102);
            Deco(L(ArchivesZone, 10f, 0.005f, 3.5f), new Vector3(5.6f, 0.008f, 8.2f), new Color(0.12f, 0.08f, 0.16f), Surface.Fabric);
            Deco(L(ArchivesZone, 10f, 0.008f, 3.5f), new Vector3(5.2f, 0.008f, 7.8f), new Color(0.24f, 0.13f, 0.3f), Surface.Fabric);

            // Candle stands: by the tables, and either side of the altar (outside the pedestals' ring).
            foreach (var p in new[] { new Vector2(12.4f, 7.4f), new Vector2(6.2f, 8.2f), new Vector2(-9.9f, 7.5f), new Vector2(-1.1f, 7.5f) })
                if (PlaceFree(CandleStand, L(ArchivesZone, p.x, 0f, p.y), Quaternion.identity, true)) CandleLight(L(ArchivesZone, p.x, 1.45f, p.y));

            // Lecterns with open books in the corners.
            foreach (var p in new[] { new Vector3(14.5f, 0f, 9.5f), new Vector3(15.3f, 0f, -5.5f), new Vector3(-12.5f, 0f, 9.8f) })
                PlaceFree(BookStand, L(ArchivesZone, p.x, 0f, p.z), Quaternion.Euler(0f, p.x > 0f ? -90f : 90f, 0f), true);
        }

        /// <summary>Tall bookcases side by side along every stretch of wall where nothing else needs it.</summary>
        void LineArchiveWalls()
        {
            if (PropModels.GetPack(Bookcase.Path, PackMaterial) == null) return;
            var size = Bookcase.Size;
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Zone != ArchivesZone || f.Length < size.x + 0.6f) continue;
                for (float t = size.x / 2f + 0.35f; t < f.Length - size.x / 2f - 0.3f; t += size.x + 0.02f)
                {
                    if (!WallFree(f, t, size.y / 2f + 0.05f, size.x / 2f - 0.05f, size.y / 2f, size.z + 0.25f)) continue;
                    var mid = f.At(t, 0f, size.z / 2f + 0.04f);
                    PlacePackItem(Bookcase, mid, f.Facing, true);
                    _propSpots.Add(mid);
                    Physics.SyncTransforms();
                }
            }
        }

        void ReadingTableSet(Vector3 p, Quaternion rot, int seed)
        {
            if (!PlaceFree(LongTable, p, rot, true)) return;
            var rng = new System.Random(seed);
            TableTop(LongTable, p, rot, ArchivesZone, rng);
            foreach (float side in new[] { -1f, 1f })
                foreach (float x in new[] { -0.95f, 0f, 0.95f })
                {
                    if (rng.NextDouble() < 0.2) continue;   // a chair or two pushed away
                    var at = p + rot * new Vector3(x + Range(rng, -0.08f, 0.08f), 0f, side * 0.92f);
                    PlaceFree(KitChair, at, rot * Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f) * Quaternion.Euler(0f, Range(rng, -12f, 12f), 0f), true);
                }
        }

        /// <summary>The item on the floor at p if its footprint is clear of everything solid and every keep-out.</summary>
        bool PlaceFree(PackItem item, Vector3 p, Quaternion rot, bool solid)
        {
            var size = item.Size;
            if (!KeptClear(p, Mathf.Max(size.x, size.z) / 2f)) return false;
            if (Solid(p + Vector3.up * (size.y / 2f + 0.05f), new Vector3(size.x / 2f, size.y / 2f, size.z / 2f), rot)) return false;
            PlacePackItem(item, p, rot, solid);
            Physics.SyncTransforms();
            return true;
        }

        void CandleLight(Vector3 at)
        {
            var light = new GameObject("Candle Light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.38f);
            light.range = 5f;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<Flicker>().Seed = Mathf.RoundToInt(at.x * 7f + at.z * 13f);
        }
    }
}
