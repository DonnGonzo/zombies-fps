using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Archives as a great library. The monks' altar stands on a stepped stone dais in the middle of the reading
    /// room, steps on all four sides, four columns at its corners carrying a beam frame up under a painted ceiling
    /// rose. Rows of tall stacks fill the west bay and the north wing, the east bay has two long reading tables, and
    /// every free stretch of wall is lined with bookcases nearly to the ceiling under a carved cornice. Every
    /// bookcase model gets its shelves filled with books. Decor goes only where it fits: clear of the doors,
    /// windows, the perk, the generator, the build parts and the altar's ring of pedestals.
    /// </summary>
    public partial class ZoneMap
    {
        const int ArchivesZone = 10;

        /// <summary>The altar dais: centre (zone-local), how high its top is and how far its top reaches each way.</summary>
        public const float DaisX = 0f, DaisZ = 2f, DaisHeight = 1.5f;
        const float DaisHalf = 4.2f;
        /// <summary>Where the dais's columns stand, in from its top's corners.</summary>
        const float ColumnInset = 0.45f;

        /// <summary>The stacks, each along z: x, then from z to z (zone-local).</summary>
        static readonly Vector3[] ArchiveStacks =
        {
            new Vector3(-8.3f, 4f, 8.6f), new Vector3(-10.9f, 4f, 8.6f),
            new Vector3(-8.3f, -6.3f, -1.8f), new Vector3(-10.9f, -6.3f, -1.8f),
            new Vector3(5.5f, 11.2f, 15.6f), new Vector3(12.5f, 11.2f, 15.6f),
        };

        static readonly Color DaisStone = new Color(0.4f, 0.38f, 0.35f);
        static readonly Color Carpet = new Color(0.3f, 0.07f, 0.08f);

        static Vector3 DaisPoint(float x, float y, float z) => L(ArchivesZone, DaisX + x, y, DaisZ + z);

        static Vector3 Flat(Vector3 d) => new Vector3(Mathf.Abs(d.x), 0f, Mathf.Abs(d.z));

        static int DaisSteps => Mathf.CeilToInt(DaisHeight / StepRise - 0.01f);

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildArchives()
        {
            Block("Records Loft", ArchivesZone, 1.3f, 17f, 16.7f, 22.7f, 4f, DarkWood, Surface.Wood);
            AddLadder(ArchivesZone, 9f, 17f, Vector3.back, 4f);

            // The dais: nested blocks, each a step lower and a tread wider, so it can be climbed from any side.
            int n = DaisSteps;
            float rise = DaisHeight / n;
            for (int i = 0; i < n; i++)
            {
                float top = DaisHeight - i * rise, half = DaisHalf + i * StepRun;
                Greybox.Box(i == 0 ? "Altar Dais" : "Dais Step", Root, DaisPoint(0f, top / 2f, 0f), new Vector3(half * 2f, top, half * 2f),
                    i == 0 ? DaisStone : Shade(DaisStone, 0.92f), surface: Surface.Tile);
            }

            // Columns at the top's corners, each on a plinth.
            float c = DaisHalf - ColumnInset;
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                float h = WallHeight - DaisHeight;
                Greybox.Box("Dais Column", Root, DaisPoint(sx * c, DaisHeight + h / 2f, sz * c), new Vector3(0.55f, h, 0.55f), Shade(DaisStone, 1.08f), surface: Surface.Concrete);
                Greybox.Box("Column Plinth", Root, DaisPoint(sx * c, DaisHeight + 0.2f, sz * c), new Vector3(0.8f, 0.4f, 0.8f), Shade(DaisStone, 0.85f), surface: Surface.Concrete);
            }

            var bookcase = new Color(0.33f, 0.22f, 0.13f);
            bool model = PropModels.GetPack(Bookcase.Path, PackMaterial) != null;
            foreach (var s in ArchiveStacks)
            {
                var box = Greybox.Box("Bookshelf", Root, L(ArchivesZone, s.x, StackHeight / 2f, (s.y + s.z) / 2f), new Vector3(0.8f, StackHeight, s.z - s.y), bookcase, surface: Surface.Wood);
                Reskin(box, Bookcase, true);
                // The model's back is open: a board down the middle, so the two sides don't show through each other.
                if (model) Deco(L(ArchivesZone, s.x, StackHeight / 2f, (s.y + s.z) / 2f), new Vector3(0.04f, StackHeight - 0.1f, s.z - s.y - 0.1f), Shade(DarkWood, 0.6f), Surface.Wood);
            }
        }

        // ---------------------------------------------------------------- dressing

        /// <summary>Before the room's random furniture, so the bookcases get the walls.</summary>
        void ArchivesMakeover()
        {
            LineArchiveWalls();
            Cornice();
            DressDais();

            // Reading tables in the east bay, either side of the way in from the east door, each on its own rug.
            foreach (float z in new[] { -0.5f, 6.5f })
            {
                Deco(L(ArchivesZone, 11.5f, 0.005f, z), new Vector3(4.6f, 0.008f, 3.3f), new Color(0.12f, 0.08f, 0.16f), Surface.Fabric);
                Deco(L(ArchivesZone, 11.5f, 0.008f, z), new Vector3(4.3f, 0.008f, 3f), new Color(0.24f, 0.13f, 0.3f), Surface.Fabric);
                ReadingTableSet(L(ArchivesZone, 11.5f, 0f, z), Quaternion.identity, z < 0f ? 101 : 102);
                if (PlaceFree(CandleStand, L(ArchivesZone, 14.1f, 0f, z), Quaternion.identity, true)) CandleLight(L(ArchivesZone, 14.1f, 1.45f, z));
            }

            // Lecterns with open books, turned to face the dais.
            foreach (var p in new[] { new Vector2(-15.6f, 6.5f), new Vector2(15.4f, 9.6f), new Vector2(7.2f, -5.8f), new Vector2(-13.8f, -3.6f) })
            {
                var at = L(ArchivesZone, p.x, 0f, p.y);
                var look = DaisPoint(0f, 0f, 0f) - at;
                PlaceFree(BookStand, at, Quaternion.LookRotation(new Vector3(look.x, 0f, look.z), Vector3.up), true);
            }
        }

        /// <summary>Carpet round the dais and up its steps, the inlay on its top, candles, and the beam frame and ceiling rose.</summary>
        void DressDais()
        {
            int n = DaisSteps;
            float rise = DaisHeight / n, foot = DaisHalf + (n - 1) * StepRun;

            // A carpet on the floor round the dais's foot, gold-bordered.
            Deco(DaisPoint(0f, 0.004f, 0f), new Vector3((foot + 0.8f) * 2f, 0.008f, (foot + 0.8f) * 2f), Shade(OldGold, 0.55f), Surface.Fabric);
            Deco(DaisPoint(0f, 0.007f, 0f), new Vector3((foot + 0.68f) * 2f, 0.008f, (foot + 0.68f) * 2f), Carpet, Surface.Fabric);

            // A runner up the middle of each side's steps and in to the ring of pedestals.
            const float runner = 1.8f;
            var sides = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (var d in sides)
            {
                var across = new Vector3(Mathf.Abs(d.z), 0f, Mathf.Abs(d.x));
                for (int i = 1; i < n; i++)
                {
                    float top = DaisHeight - i * rise, mid = DaisHalf + (i - 0.5f) * StepRun;
                    Deco(DaisPoint(0f, top + 0.005f, 0f) + d * mid, across * runner + Vector3.up * 0.01f + Flat(d) * StepRun, Carpet, Surface.Fabric);
                    // A brass rod at the back of each tread.
                    DecoPrim(PrimitiveType.Cylinder, DaisPoint(0f, top + 0.02f, 0f) + d * (mid - StepRun / 2f + 0.03f), new Vector3(0.025f, runner / 2f + 0.05f, 0.025f),
                        Quaternion.FromToRotation(Vector3.up, across), OldGold, Surface.Metal);
                }
                float inner = PedestalRadius + 0.5f;
                Deco(DaisPoint(0f, DaisHeight + 0.005f, 0f) + d * ((inner + DaisHalf) / 2f), across * runner + Vector3.up * 0.01f + Flat(d) * (DaisHalf - inner), Carpet, Surface.Fabric);
            }

            // A pale stone border round the top, and a darker edge on every step.
            foreach (var d in sides)
            {
                var along = new Vector3(Mathf.Abs(d.z), 0f, Mathf.Abs(d.x));
                Deco(DaisPoint(0f, DaisHeight + 0.003f, 0f) + d * (DaisHalf - 0.2f), along * (DaisHalf * 2f) + Vector3.up * 0.006f + Flat(d) * 0.4f, Shade(DaisStone, 1.25f), Surface.Tile);
                for (int i = 1; i < n; i++)
                {
                    float top = DaisHeight - i * rise, edge = DaisHalf + i * StepRun;
                    Deco(DaisPoint(0f, top - 0.035f, 0f) + d * (edge - 0.02f), along * (edge * 2f) + Vector3.up * 0.06f + Flat(d) * 0.05f, Shade(DaisStone, 0.7f), Surface.Tile);
                }
            }

            // Candle stands on the top, either side of the north and south runners.
            foreach (var (x, z) in new[] { (-2.6f, -3.7f), (2.6f, -3.7f), (-2.6f, 3.7f), (2.6f, 3.7f) })
                if (PlaceFree(CandleStand, DaisPoint(x, DaisHeight, z), Quaternion.identity, true)) CandleLight(DaisPoint(x, DaisHeight + 1.45f, z));

            // Capitals on the columns, and a beam frame between their tops.
            float c = DaisHalf - ColumnInset;
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                Deco(DaisPoint(sx * c, 5.95f, sz * c), new Vector3(0.75f, 0.3f, 0.75f), Shade(DaisStone, 0.9f), Surface.Concrete);
                Deco(DaisPoint(sx * c, 2.6f, sz * c), new Vector3(0.6f, 0.08f, 0.6f), OldGold, Surface.Metal);
            }
            foreach (var d in sides)
            {
                var along = new Vector3(Mathf.Abs(d.z), 0f, Mathf.Abs(d.x));
                Deco(DaisPoint(0f, 6.45f, 0f) + d * c, along * (c * 2f + 0.75f) + Vector3.up * 0.7f + Flat(d) * 0.5f, Shade(DarkWood, 0.9f), Surface.Wood);
                Deco(DaisPoint(0f, 6.2f, 0f) + d * (c + 0.26f), along * (c * 2f + 0.8f) + Vector3.up * 0.06f + Flat(d) * 0.02f, OldGold, Surface.Metal);
            }

            // A painted rose on the ceiling over the altar: a night-blue disc ringed in gold.
            DecoPrim(PrimitiveType.Cylinder, DaisPoint(0f, WallHeight - 0.3f, 0f), new Vector3(c * 2f - 0.8f, 0.01f, c * 2f - 0.8f), Quaternion.identity, Shade(OldGold, 0.8f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, DaisPoint(0f, WallHeight - 0.31f, 0f), new Vector3(c * 2f - 1.1f, 0.01f, c * 2f - 1.1f), Quaternion.identity, new Color(0.07f, 0.09f, 0.2f), Surface.Plaster);
            var rng = new System.Random(4141);
            for (int k = 0; k < 40; k++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f), r = Mathf.Sqrt((float)rng.NextDouble()) * (c - 0.8f);
                Glow(PrimitiveType.Sphere, DaisPoint(Mathf.Cos(a) * r, WallHeight - 0.325f, Mathf.Sin(a) * r), Vector3.one * Range(rng, 0.04f, 0.09f), Quaternion.identity, new Color(1f, 0.86f, 0.55f));
            }
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

        /// <summary>A carved cornice along the top of the bookcases, a dark green frieze above it and a crown moulding under the ceiling.</summary>
        void Cornice()
        {
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Zone != ArchivesZone) continue;
                ClearStretches(f, 6.05f, 0.12f, 0.36f, (a, b) =>
                {
                    Deco(f.At((a + b) / 2f, 6.05f, 0.16f), new Vector3(b - a, 0.22f, 0.32f), f.Facing, Shade(DarkWood, 0.85f), Surface.Wood);
                    Deco(f.At((a + b) / 2f, 5.92f, 0.33f), new Vector3(b - a, 0.04f, 0.03f), f.Facing, OldGold, Surface.Metal);
                });
                ClearStretches(f, 6.5f, 0.25f, 0.04f, (a, b) =>
                    Deco(f.At((a + b) / 2f, 6.5f, 0.02f), new Vector3(b - a, 0.5f, 0.02f), f.Facing, new Color(0.11f, 0.2f, 0.15f), Surface.Fabric));
                ClearStretches(f, 6.88f, 0.06f, 0.2f, (a, b) =>
                    Deco(f.At((a + b) / 2f, 6.88f, 0.1f), new Vector3(b - a, 0.14f, 0.18f), f.Facing, Shade(DarkWood, 0.85f), Surface.Wood));
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

        // ---------------------------------------------------------------- books on the shelves

        /// <summary>
        /// Where a bookcase model's shelves are, in its own space: the span inside its sides, its back and front, and
        /// each shelf's top with the clear height over it.
        /// </summary>
        readonly struct Shelving
        {
            public readonly float X0, X1, Back, Front;
            public readonly float[] Shelves, Clear;
            public Shelving(float x0, float x1, float back, float front, float[] shelves, float[] clear)
            { X0 = x0; X1 = x1; Back = back; Front = front; Shelves = shelves; Clear = clear; }
        }

        static readonly Dictionary<string, Shelving> Shelvings = new Dictionary<string, Shelving>
        {
            // The Ten Worlds bookcase: six open shelves under its crown, 0.76 m apart.
            [Worlds + "Bookcase"] = new Shelving(-0.74f, 0.64f, -0.25f, 0.25f,
                new[] { 0.36f, 1.24f, 2.13f, 3.01f, 3.89f, 4.78f }, new[] { 0.74f, 0.75f, 0.74f, 0.74f, 0.75f, 0.74f }),
            // Quaternius' Bookcase_2: a tall bottom shelf, three low ones and one under the top.
            [Fantasy + "Bookcase_2"] = new Shelving(-0.57f, 0.57f, -0.15f, 0.18f,
                new[] { 0.14f, 0.77f, 1.15f, 1.53f, 1.92f }, new[] { 0.53f, 0.29f, 0.29f, 0.29f, 0.42f }),
        };

        static readonly Color[] BookCovers =
        {
            new Color(0.35f, 0.08f, 0.06f), new Color(0.12f, 0.2f, 0.12f), new Color(0.3f, 0.22f, 0.12f), new Color(0.1f, 0.12f, 0.25f),
            new Color(0.42f, 0.32f, 0.12f), new Color(0.2f, 0.16f, 0.12f), new Color(0.45f, 0.38f, 0.26f), new Color(0.22f, 0.07f, 0.12f),
            new Color(0.08f, 0.08f, 0.08f), new Color(0.15f, 0.25f, 0.27f),
        };

        /// <summary>
        /// Fills a bookcase model's shelves with books: mostly upright runs of mixed height and thickness, with the
        /// odd one leaning, a few lying in piles, and a gap here and there. The model sits at (at, rot, scale) with
        /// its footprint's centre on the floor at at, as AddModel and Reskin place it.
        /// </summary>
        void ShelveBooks(string path, PropModels.Model model, Vector3 at, Quaternion rot, Vector3 scale)
        {
            if (!Shelvings.TryGetValue(path, out var s)) return;
            var b = model.Bounds;
            var offset = new Vector3(b.center.x, b.min.y, b.center.z);
            var rng = new System.Random(Mathf.RoundToInt(at.x * 131f + at.z * 71f + at.y * 13f) ^ path.Length);
            float depth = s.Front - s.Back;

            void Book(Vector3 c, Vector3 size, float tilt, Color color)
            {
                var r = rot * Quaternion.Euler(0f, 0f, tilt);
                Deco(at + rot * Vector3.Scale(c - offset, scale), Vector3.Scale(size, scale), r, color, Surface.Fabric);
            }

            for (int k = 0; k < s.Shelves.Length; k++)
            {
                float y0 = s.Shelves[k], clear = s.Clear[k];
                float x = s.X0 + Range(rng, 0f, 0.03f);
                while (x < s.X1 - 0.03f)
                {
                    double roll = rng.NextDouble();
                    var cover = BookCovers[rng.Next(BookCovers.Length)];
                    if (roll < 0.05)
                    {
                        x += Range(rng, 0.05f, 0.16f);
                        continue;
                    }
                    if (roll < 0.13)
                    {
                        // A pile lying flat.
                        float w = Range(rng, 0.18f, 0.27f);
                        if (x + w > s.X1) break;
                        float y = y0;
                        for (int i = rng.Next(2, 6); i > 0 && y < y0 + clear - 0.1f; i--)
                        {
                            float t = Range(rng, 0.035f, 0.065f), bw = w - Range(rng, 0f, 0.05f), bd = depth * Range(rng, 0.6f, 0.85f);
                            Book(new Vector3(x + w / 2f + Range(rng, -0.015f, 0.015f), y + t / 2f, s.Front - bd / 2f - 0.02f), new Vector3(bw, t, bd), 0f, BookCovers[rng.Next(BookCovers.Length)]);
                            y += t;
                        }
                        x += w + 0.01f;
                        continue;
                    }
                    float th = Range(rng, 0.03f, 0.075f), h = clear * Range(rng, 0.6f, 0.9f), d = depth * Range(rng, 0.62f, 0.9f);
                    float z = s.Front - d / 2f - Range(rng, 0.01f, 0.04f);
                    if (roll > 0.96 && x + h * 0.4f + th < s.X1)
                    {
                        // One leaning over to the right, against nothing in particular.
                        float a = Range(rng, 14f, 24f), sin = Mathf.Sin(a * Mathf.Deg2Rad), cos = Mathf.Cos(a * Mathf.Deg2Rad);
                        float wide = h * sin + th * cos;
                        Book(new Vector3(x + wide / 2f, y0 + (h * cos + th * sin) / 2f, z), new Vector3(th, h, d), -a, cover);
                        x += wide + 0.01f;
                        continue;
                    }
                    if (x + th > s.X1) break;
                    Book(new Vector3(x + th / 2f, y0 + h / 2f, z), new Vector3(th, h, d), 0f, cover);
                    // A gilt band across some spines.
                    if (rng.NextDouble() < 0.3)
                        Book(new Vector3(x + th / 2f, y0 + h * Range(rng, 0.7f, 0.85f), z + d / 2f + 0.002f), new Vector3(th + 0.002f, 0.018f, 0.004f), 0f, OldGold);
                    x += th + Range(rng, 0f, 0.008f);
                }
            }
        }
    }
}
