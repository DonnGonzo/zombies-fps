using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The map's set dressing: a skin on every wall face in its zone's materials, lamp fixtures, pipes, signs,
    /// posters, floor clutter, zone props and the skyline beyond the outer walls.
    ///
    /// None of it has a collider, so none of it changes how anyone moves or what the NavMesh (baked from physics
    /// colliders only) looks like. Clutter on floors stays within 0.12 m of the floor; everything else is fixed
    /// flat to a wall or a solid block, and nothing goes in a doorway or in front of anything players use (see
    /// KeptClear). Placement is seeded, so every machine builds the same map. It's all merged into a few big
    /// meshes per material as it goes (see DecorBatch), so thousands of pieces cost only a couple of hundred
    /// draw calls.
    /// </summary>
    public partial class ZoneMap
    {
        /// <summary>A zone-facing side of a full-height wall, for hanging things on. A and B are at floor level on the skin's face.</summary>
        struct WallFace
        {
            public int Zone;
            public Vector3 A, B, Inward;
            public bool Outer;

            public float Length => Vector3.Distance(A, B);
            public Vector3 Along => (B - A).normalized;
            /// <summary>A point t metres along the face, y up, off out from it.</summary>
            public Vector3 At(float t, float y, float off = 0f) => A + Along * t + Vector3.up * y + Inward * off;
            public Quaternion Facing => Quaternion.LookRotation(Inward, Vector3.up);
        }

        const float SkinDepth = 0.01f;
        const float BandDepth = 0.025f;
        const int MaxGraffitiText = 10;

        static readonly Color FadedYellow = new Color(0.5f, 0.42f, 0.16f);
        static readonly Color FadedWhite = new Color(0.55f, 0.55f, 0.52f);
        static readonly Color Soot = new Color(0.07f, 0.07f, 0.075f);
        static readonly Color Glass = new Color(0.03f, 0.04f, 0.05f);
        static readonly Color WallLampGlow = new Color(1f, 0.78f, 0.5f);

        static readonly string[] GraffitiTexts =
        {
            "THEY HEAR YOU", "NO WAY OUT", "DON'T OPEN", "KEEP MOVING", "GOD HELP US", "AIM FOR THE HEAD",
            "WE WERE HERE", "LIGHTS OUT = RUN", "TURN BACK", "STAY QUIET",
        };

        DecorBatch _decor;
        readonly List<WallFace> _faces = new List<WallFace>();
        readonly List<(Vector3 at, float radius)> _keepOut = new List<(Vector3, float)>();
        int _graffitiText;

        // ---------------------------------------------------------------- decor primitives

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        void Deco(Vector3 pos, Vector3 size, Color color, Surface surface) =>
            _decor.Add(PrimitiveType.Cube, pos, Quaternion.identity, size, Greybox.Lit(color, surface));

        void Deco(Vector3 pos, Vector3 size, Quaternion rot, Color color, Surface surface) =>
            _decor.Add(PrimitiveType.Cube, pos, rot, size, Greybox.Lit(color, surface));

        void DecoPrim(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color color, Surface surface) =>
            _decor.Add(type, pos, rot, scale, Greybox.Lit(color, surface));

        void Glow(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color color) =>
            _decor.Add(type, pos, rot, scale, Greybox.Unlit(color));

        void Glow(Vector3 pos, Vector3 size, Quaternion rot, Color color) => Glow(PrimitiveType.Cube, pos, size, rot, color);

        /// <summary>A round bar (pipe, cable, leg) from a to b.</summary>
        void Rod(Vector3 a, Vector3 b, float diameter, Color color, Surface surface)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;
            _decor.Add(PrimitiveType.Cylinder, (a + b) / 2f, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(diameter, len / 2f, diameter), Greybox.Lit(color, surface));
        }

        /// <summary>A square bar from a to b, <paramref name="thick"/> along <paramref name="up"/> (which must not run along the bar).</summary>
        void Bar(Vector3 a, Vector3 b, float width, float thick, Vector3 up, Color color, Surface surface)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;
            _decor.Add(PrimitiveType.Cube, (a + b) / 2f, Quaternion.LookRotation(d / len, up), new Vector3(width, thick, len), Greybox.Lit(color, surface));
        }

        /// <summary>A flat oval on the floor (puddle, stain, splat), y above it.</summary>
        void Splat(Vector3 p, float sx, float sz, float yaw, float y, Color color, Surface surface) =>
            DecoPrim(PrimitiveType.Cylinder, new Vector3(p.x, y, p.z), new Vector3(sx, 0.0025f, sz), Quaternion.Euler(0f, yaw, 0f), color, surface);

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- keeping clear

        /// <summary>Doors, everything against a wall that players use, the machines, ladder feet and floor-level parts.</summary>
        void CollectKeepOuts()
        {
            _keepOut.Clear();
            for (int d = 0; d < DoorPoints.Length; d++) _keepOut.Add((DoorCenter(d), GapWidth / 2f + 2.2f));
            void Spot(WallSpot s, float r)
            {
                var (face, inward) = s.Resolve();
                _keepOut.Add((face + inward * 0.7f, r));
            }
            foreach (var s in WallBuySpots) Spot(s, 2.4f);
            foreach (var s in PerkSpots) Spot(s, 2.4f);
            foreach (var s in GeneratorSpots) Spot(s, 2.8f);
            foreach (var s in WindowSpots) Spot(s, 2f);
            Spot(ArmorSpot, 2.4f);
            Spot(RerollSpot, 2.4f);
            _keepOut.Add((MachinePosition, 3.5f));
            _keepOut.Add((ShopPosition, 3.5f));
            foreach (var f in _ladderFeet) _keepOut.Add((f, 1.4f));
            foreach (var p in PartSpots)
                if (p.y < 0.5f) _keepOut.Add((p, 1.2f));
        }

        /// <summary>True when a point (with this much reach round it) is clear of every keep-out.</summary>
        bool KeptClear(Vector3 p, float extra = 0f)
        {
            foreach (var (at, r) in _keepOut)
            {
                float dx = p.x - at.x, dz = p.z - at.z, rr = r + extra;
                if (dx * dx + dz * dz < rr * rr) return false;
            }
            return true;
        }

        static bool Solid(Vector3 center, Vector3 halfExtents, Quaternion rot) =>
            Physics.CheckBox(center, halfExtents, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        /// <summary>Open floor (nothing solid standing on it) that's clear of the keep-outs, for clutter this wide.</summary>
        bool FloorFree(Vector3 p, float radius) =>
            KeptClear(p, radius) && !Solid(new Vector3(p.x, 0.35f, p.z), new Vector3(radius, 0.28f, radius), Quaternion.identity);

        /// <summary>
        /// Room on a wall face for something this big: nothing solid against the wall there, and below door height,
        /// clear of the keep-outs too.
        /// </summary>
        bool WallFree(WallFace f, float t, float y, float halfWidth, float halfHeight, float depth)
        {
            var p = f.At(t, y);
            if (t - halfWidth < 0.3f || t + halfWidth > f.Length - 0.3f) return false;
            if (y - halfHeight < GapHeight + 0.2f && !KeptClear(p, halfWidth)) return false;
            return !Solid(p + f.Inward * (0.05f + depth / 2f), new Vector3(halfWidth, halfHeight, depth / 2f), f.Facing);
        }

        // ---------------------------------------------------------------- walls

        /// <summary>The zone on one side of a wall line at cell t along it (side +1 is north or east), or -1 outside.</summary>
        static int SideZone(int line, bool alongX, int side, int t) =>
            alongX ? CellZone(t, side > 0 ? line : line - 1) : CellZone(side > 0 ? line : line - 1, t);

        /// <summary>The zone a wall's skin faces at cell t, or -1 where there's no wall or nothing to face.</summary>
        static int SkinCell(int line, bool alongX, int side, int t, float from, float to)
        {
            if (Mathf.Min(t + 1, to) - Mathf.Max(t, from) < 0.05f) return -1;
            int zone = SideZone(line, alongX, side, t);
            return zone >= 0 && zone != SideZone(line, alongX, -side, t) ? zone : -1;
        }

        /// <summary>
        /// Skins each side of a wall piece in the material of the zone it faces, a stretch per zone, plus a coping
        /// along the top (and wire along the outer walls). The wall's own box keeps its collider; skins are just looks.
        /// </summary>
        void SkinWall(WallPiece piece)
        {
            bool alongX = piece.AlongX;
            float line = alongX ? piece.A.y : piece.A.x;
            int lineCell = Mathf.RoundToInt(line);
            float from = alongX ? Mathf.Min(piece.A.x, piece.B.x) : Mathf.Min(piece.A.y, piece.B.y);
            float to = alongX ? Mathf.Max(piece.A.x, piece.B.x) : Mathf.Max(piece.A.y, piece.B.y);
            int midCell = Mathf.FloorToInt((from + to) / 2f);
            bool outer = SideZone(lineCell, alongX, 1, midCell) < 0 || SideZone(lineCell, alongX, -1, midCell) < 0;

            for (int side = -1; side <= 1; side += 2)
            {
                int t = Mathf.FloorToInt(from);
                while (t < to)
                {
                    int zone = SkinCell(lineCell, alongX, side, t, from, to);
                    if (zone < 0) { t++; continue; }
                    int g0 = t;
                    while (t + 1 < to && SkinCell(lineCell, alongX, side, t + 1, from, to) == zone) t++;
                    int g1 = ++t;
                    float lo = Mathf.Max(from, g0), hi = Mathf.Min(to, g1);
                    // At an end, stop at the face of a wall across it (an inside corner), or wrap round this wall's end.
                    if (lo <= g0 + 0.001f) lo = SideZone(lineCell, alongX, side, g0 - 1) == zone ? from - BandDepth : g0 + WallHalf;
                    if (hi >= g1 - 0.001f) hi = SideZone(lineCell, alongX, side, g1) == zone ? to + BandDepth : g1 - WallHalf;
                    if (hi - lo > 0.05f) SkinFace(zone, piece, alongX, line, side, lo, hi, outer);
                }
            }

            Vector3 mid = alongX ? new Vector3((from + to) / 2f, 0f, line) : new Vector3(line, 0f, (from + to) / 2f);
            float len = to - from;
            Deco(mid + Vector3.up * (piece.Top + 0.06f), alongX ? new Vector3(len, 0.12f, WallThickness + 0.1f) : new Vector3(WallThickness + 0.1f, 0.12f, len),
                Shade(Wall, 0.75f), Surface.Concrete);
            if (outer) BarbedWire(mid, alongX, len, piece.Top + 0.12f);
        }

        void SkinFace(int zone, WallPiece piece, bool alongX, float line, int side, float lo, float hi, bool outer)
        {
            var look = Looks[zone];
            var inward = alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);
            float len = hi - lo, mid = (lo + hi) / 2f;
            Vector3 P(float along, float y, float off) => alongX ? new Vector3(along, y, line + side * off) : new Vector3(line + side * off, y, along);
            Vector3 S(float l, float h, float d) => alongX ? new Vector3(l, h, d) : new Vector3(d, h, l);

            float bottom = piece.Bottom, top = piece.Top;
            float bandTop = Mathf.Max(bottom, look.BandHeight);
            if (bandTop > bottom)
            {
                Deco(P(mid, (bottom + bandTop) / 2f, WallHalf + BandDepth / 2f + 0.001f), S(len, bandTop - bottom, BandDepth), look.BandColor, look.Band);
                Deco(P(mid, bandTop, WallHalf + 0.02f), S(len, 0.06f, 0.04f), Shade(look.BandColor, 0.7f), look.Band);
            }
            else if (bottom <= 0f)
            {
                Deco(P(mid, 0.07f, WallHalf + 0.015f), S(len, 0.14f, 0.03f), Shade(look.WallColor, 0.45f), Surface.Concrete);
            }
            Deco(P(mid, (bandTop + top) / 2f, WallHalf + SkinDepth / 2f + 0.001f), S(len, top - bandTop, SkinDepth), look.WallColor, look.Wall);

            if (bottom <= 0f)
            {
                float face = WallHalf + SkinDepth + 0.001f;
                _faces.Add(new WallFace { Zone = zone, A = P(lo, 0f, face), B = P(hi, 0f, face), Inward = inward, Outer = outer });
            }
        }

        /// <summary>Posts and three strands of wire along the top of an outer wall.</summary>
        void BarbedWire(Vector3 mid, bool alongX, float len, float y)
        {
            var dir = alongX ? Vector3.right : Vector3.forward;
            var wire = new Color(0.16f, 0.15f, 0.14f);
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
            for (int i = 0; i <= posts; i++)
                Deco(mid + dir * (-len / 2f + len * i / posts) + Vector3.up * (y + 0.35f), new Vector3(0.05f, 0.7f, 0.05f), wire, Surface.Rust);
            foreach (float h in new[] { 0.25f, 0.45f, 0.65f })
                Rod(mid - dir * (len / 2f) + Vector3.up * (y + h), mid + dir * (len / 2f) + Vector3.up * (y + h), 0.018f, wire, Surface.Metal);
        }

        /// <summary>A dark opening, sill, head and jambs round a boarded window.</summary>
        void WindowFrame(Vector3 face, Vector3 inward)
        {
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var right = rot * Vector3.right;
            Deco(face + inward * 0.04f + Vector3.up * 1.55f, new Vector3(1.9f, 1.45f, 0.01f), rot, new Color(0.02f, 0.02f, 0.025f), Surface.Plain);
            Deco(face + inward * 0.06f + Vector3.up * 0.8f, new Vector3(2.2f, 0.08f, 0.12f), rot, DarkWood, Surface.Wood);
            Deco(face + inward * 0.04f + Vector3.up * 2.33f, new Vector3(2.2f, 0.12f, 0.08f), rot, DarkWood, Surface.Wood);
            foreach (float s in new[] { -1f, 1f })
                Deco(face + inward * 0.04f + right * (1f * s) + Vector3.up * 1.55f, new Vector3(0.1f, 1.45f, 0.08f), rot, DarkWood, Surface.Wood);
        }

        /// <summary>A caged bulkhead lamp on a wall, glowing unless it's broken.</summary>
        void WallLamp(Vector3 at, Vector3 inward, bool lit)
        {
            if (PropModels.Get(PropModels.Lantern) != null)
            {
                WallLantern(at, inward, lit);
                return;
            }
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var housing = new Color(0.14f, 0.15f, 0.15f);
            Deco(at + inward * 0.015f, new Vector3(0.3f, 0.36f, 0.03f), rot, housing, Surface.Metal);
            Deco(at + inward * 0.1f + Vector3.up * 0.06f, new Vector3(0.28f, 0.08f, 0.17f), rot, housing, Surface.Metal);
            if (lit) Glow(at + inward * 0.1f - Vector3.up * 0.04f, new Vector3(0.22f, 0.12f, 0.14f), rot, WallLampGlow);
            else Deco(at + inward * 0.1f - Vector3.up * 0.04f, new Vector3(0.22f, 0.12f, 0.14f), rot, Glass, Surface.Metal);
            var right = rot * Vector3.right;
            foreach (float s in new[] { -0.07f, 0.07f })
                Deco(at + inward * 0.18f + right * s - Vector3.up * 0.04f, new Vector3(0.015f, 0.14f, 0.015f), rot, housing, Surface.Metal);
        }

        /// <summary>Every wall face: stains, lamps, the zone's pipe runs, and a few signs, posters and fittings.</summary>
        void DressWallFaces()
        {
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Length < 1.5f) continue;
                var rng = new System.Random(5100 + i * 7919);
                Stains(f, rng);
                WallLamps(f, rng);
                WallRuns(f);
                WallClutter(f, rng);
            }
        }

        /// <summary>Dark water streaks running down from the top of the wall.</summary>
        void Stains(WallFace f, System.Random rng)
        {
            var look = Looks[f.Zone];
            int n = rng.Next(0, Mathf.Max(1, Mathf.RoundToInt(f.Length / 4f)) + 1);
            for (int k = 0; k < n; k++)
            {
                float w = Range(rng, 0.25f, 1.1f), h = Range(rng, 1f, 3.2f), t = Range(rng, w, f.Length - w);
                if (t < w) continue;
                Deco(f.At(t, WallHeight - 0.1f - h / 2f, 0.003f), new Vector3(w, h, 0.004f), f.Facing, Shade(look.WallColor, 0.55f), look.Wall);
            }
        }

        void WallLamps(WallFace f, System.Random rng)
        {
            int n = Mathf.FloorToInt(f.Length / 12f);
            if (n == 0 && f.Length >= 7f) n = 1;
            for (int k = 0; k < n; k++)
            {
                float t = (k + 0.5f) * f.Length / n;
                bool lit = rng.NextDouble() > 0.2;
                if (WallFree(f, t, 4.2f, 0.2f, 0.2f, 0.25f)) WallLamp(f.At(t, 4.2f), f.Inward, lit);
            }
        }

        /// <summary>
        /// Calls emit(from, to) for each stretch of a face where a run at this height has room, so a pipe stops at
        /// anything built against the wall.
        /// </summary>
        void ClearStretches(WallFace f, float y, float halfHeight, float depth, System.Action<float, float> emit)
        {
            float len = f.Length;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.75f));
            float step = len / n, start = -1f;
            for (int i = 0; i <= n; i++)
            {
                bool clear = i < n && !Solid(f.At((i + 0.5f) * step, y, 0.05f + depth / 2f), new Vector3(step / 2f, halfHeight, depth / 2f), f.Facing);
                if (clear && start < 0f) start = i * step;
                else if (!clear && start >= 0f)
                {
                    if (i * step - start > 0.6f) emit(start, i * step);
                    start = -1f;
                }
            }
        }

        /// <summary>A pipe along a wall at height y, standing off it on brackets.</summary>
        void WallPipe(WallFace f, float y, float diameter, float off, Color color, Surface surface)
        {
            ClearStretches(f, y, diameter / 2f + 0.05f, off + diameter / 2f, (a, b) =>
            {
                Rod(f.At(a, y, off), f.At(b, y, off), diameter, color, surface);
                for (float t = a + 0.4f; t < b - 0.2f; t += 2.5f)
                    Deco(f.At(t, y, off / 2f), new Vector3(0.05f, diameter + 0.06f, off), f.Facing, Soot, Surface.Metal);
            });
        }

        /// <summary>Each zone's continuous runs along its walls: pipes, conduit, glow strips, rails.</summary>
        void WallRuns(WallFace f)
        {
            switch (f.Zone)
            {
                case 7: // Pump Station: two big pipe runs.
                    WallPipe(f, 5f, 0.34f, 0.28f, new Color(0.44f, 0.26f, 0.15f), Surface.Rust);
                    WallPipe(f, 5.7f, 0.18f, 0.2f, new Color(0.26f, 0.34f, 0.4f), Surface.Metal);
                    break;
                case 1: case 2: case 6: case 4: // Depot, Garage, Loading Dock, Barracks: electrical conduit.
                    WallPipe(f, 4.75f, 0.07f, 0.08f, new Color(0.32f, 0.33f, 0.33f), Surface.Metal);
                    break;
                case 9: // Lantern Core: a glow line round the room and a cable tray above it.
                    ClearStretches(f, 3.5f, 0.05f, 0.04f, (a, b) =>
                        Glow(f.At((a + b) / 2f, 3.5f, 0.015f), new Vector3(b - a, 0.05f, 0.02f), f.Facing, Shade(CoreGlow, 0.7f)));
                    ClearStretches(f, 5.4f, 0.06f, 0.3f, (a, b) =>
                        Deco(f.At((a + b) / 2f, 5.4f, 0.15f), new Vector3(b - a, 0.08f, 0.3f), f.Facing, new Color(0.16f, 0.17f, 0.19f), Surface.Metal));
                    break;
                case 3: // Chapel: a plaster cornice.
                    Deco(f.At(f.Length / 2f, 6.65f, 0.05f), new Vector3(f.Length, 0.18f, 0.1f), f.Facing, Shade(Looks[3].WallColor, 0.85f), Surface.Plaster);
                    break;
                case 10: // Archives: a picture rail.
                    Deco(f.At(f.Length / 2f, 2.6f, 0.015f), new Vector3(f.Length, 0.05f, 0.03f), f.Facing, DarkWood, Surface.Wood);
                    break;
            }
        }

        enum Fitting { Poster, HazardSign, Graffiti, Vent, FuseBox, NoticeBoard, Sconce, Picture, ValveWheel, Ivy, Trellis, Pegboard, Panel }

        /// <summary>What hangs on each zone's walls, in rough proportion.</summary>
        static Fitting[] FittingsOf(int zone) => zone switch
        {
            0 => new[] { Fitting.Graffiti, Fitting.Graffiti, Fitting.Poster, Fitting.HazardSign, Fitting.Vent, Fitting.FuseBox },
            1 => new[] { Fitting.HazardSign, Fitting.FuseBox, Fitting.Vent, Fitting.Poster, Fitting.Graffiti },
            2 => new[] { Fitting.Pegboard, Fitting.Pegboard, Fitting.FuseBox, Fitting.HazardSign, Fitting.Vent, Fitting.Poster },
            3 => new[] { Fitting.Sconce, Fitting.Sconce, Fitting.Picture, Fitting.NoticeBoard },
            4 => new[] { Fitting.NoticeBoard, Fitting.Poster, Fitting.Poster, Fitting.Sconce, Fitting.Graffiti, Fitting.Vent },
            5 => new[] { Fitting.Ivy, Fitting.Ivy, Fitting.Graffiti, Fitting.Graffiti, Fitting.Poster },
            6 => new[] { Fitting.HazardSign, Fitting.Graffiti, Fitting.FuseBox, Fitting.Vent, Fitting.Poster },
            7 => new[] { Fitting.ValveWheel, Fitting.ValveWheel, Fitting.FuseBox, Fitting.HazardSign, Fitting.Vent },
            8 => new[] { Fitting.Graffiti, Fitting.HazardSign, Fitting.Ivy, Fitting.Poster, Fitting.FuseBox },
            9 => new[] { Fitting.Panel, Fitting.Panel, Fitting.Vent, Fitting.HazardSign },
            10 => new[] { Fitting.Picture, Fitting.Picture, Fitting.Sconce, Fitting.NoticeBoard },
            _ => new[] { Fitting.Trellis, Fitting.Trellis, Fitting.Ivy, Fitting.Ivy, Fitting.Poster },
        };

        void WallClutter(WallFace f, System.Random rng)
        {
            var kinds = FittingsOf(f.Zone);
            var used = new List<Vector2>();
            int n = Mathf.RoundToInt(f.Length / 6f * Range(rng, 0.6f, 1.3f));
            for (int k = 0; k < n; k++)
            {
                var kind = kinds[rng.Next(kinds.Length)];
                var (w, h, y, depth) = FittingSize(kind, rng);
                float t = Range(rng, 0.5f + w / 2f, f.Length - 0.5f - w / 2f);
                bool overlaps = false;
                foreach (var u in used)
                    if (Mathf.Abs(u.x - t) < (u.y + w) / 2f + 0.4f) overlaps = true;
                if (overlaps || !WallFree(f, t, y, w / 2f, h / 2f, depth)) continue;
                used.Add(new Vector2(t, w));
                PlaceFitting(kind, f, t, y, w, h, rng);
            }
        }

        static (float w, float h, float y, float depth) FittingSize(Fitting kind, System.Random rng) => kind switch
        {
            Fitting.Poster => (Range(rng, 0.5f, 0.8f), Range(rng, 0.7f, 1f), Range(rng, 1.5f, 2f), 0.02f),
            Fitting.HazardSign => (0.6f, 0.45f, Range(rng, 1.7f, 2.3f), 0.03f),
            Fitting.Graffiti => (Range(rng, 1.5f, 3f), Range(rng, 0.6f, 1.2f), Range(rng, 1.2f, 2f), 0.02f),
            Fitting.Vent => (Range(rng, 0.8f, 1.4f), 0.6f, Range(rng, 5.4f, 6.2f), 0.06f),
            Fitting.FuseBox => (0.6f, 0.8f, 1.6f, 0.2f),
            Fitting.NoticeBoard => (Range(rng, 1.2f, 1.8f), 0.9f, 1.65f, 0.04f),
            Fitting.Sconce => (0.3f, 0.5f, 2.9f, 0.25f),
            Fitting.Picture => (Range(rng, 0.6f, 1.2f), Range(rng, 0.6f, 0.9f), Range(rng, 1.7f, 2f), 0.05f),
            Fitting.ValveWheel => (0.6f, 0.6f, Range(rng, 1.3f, 1.6f), 0.3f),
            Fitting.Ivy => (Range(rng, 1.2f, 2.6f), Range(rng, 2f, 4f), Range(rng, 3.5f, 4.8f), 0.04f),
            Fitting.Trellis => (2.4f, 3f, 4f, 0.08f),
            Fitting.Pegboard => (Range(rng, 1.4f, 2.2f), 0.9f, 1.75f, 0.12f),
            _ => (0.7f, 0.9f, 1.6f, 0.06f),
        };

        void PlaceFitting(Fitting kind, WallFace f, float t, float y, float w, float h, System.Random rng)
        {
            var rot = f.Facing;
            var right = f.Along;
            Vector3 At(float dx, float dy, float off) => f.At(t + dx, y + dy, off);
            switch (kind)
            {
                case Fitting.Poster:
                {
                    var paper = rng.NextDouble() < 0.5 ? new Color(0.55f, 0.52f, 0.44f) : new Color(0.5f, 0.2f, 0.15f);
                    var tilt = rot * Quaternion.Euler(0f, 0f, Range(rng, -6f, 6f));
                    Deco(At(0f, 0f, 0.004f), new Vector3(w, h, 0.004f), tilt, paper, Surface.Plaster);
                    Deco(At(0f, h * 0.25f, 0.007f), new Vector3(w * 0.8f, h * 0.18f, 0.003f), tilt, Shade(paper, 0.35f), Surface.Plaster);
                    Deco(At(0f, -h * 0.15f, 0.007f), new Vector3(w * 0.7f, h * 0.35f, 0.003f), tilt, Shade(paper, 0.6f), Surface.Plaster);
                    break;
                }
                case Fitting.HazardSign:
                    Deco(At(0f, 0f, 0.01f), new Vector3(w, h, 0.02f), rot, HazardYellow, Surface.Metal);
                    Deco(At(0f, 0f, 0.022f), new Vector3(w * 0.85f, h * 0.2f, 0.004f), rot, Soot, Surface.Plain);
                    Deco(At(0f, h * 0.32f, 0.022f), new Vector3(w * 0.9f, h * 0.12f, 0.004f), rot, new Color(0.45f, 0.08f, 0.05f), Surface.Plain);
                    break;
                case Fitting.Graffiti:
                    Graffiti(f, t, y, w, h, rng);
                    break;
                case Fitting.Vent:
                    Deco(At(0f, 0f, 0.02f), new Vector3(w, h, 0.04f), rot, new Color(0.22f, 0.23f, 0.23f), Surface.Metal);
                    for (float sy = -h / 2f + 0.1f; sy < h / 2f - 0.05f; sy += 0.1f)
                        Deco(At(0f, sy, 0.045f), new Vector3(w - 0.08f, 0.03f, 0.02f), rot, Soot, Surface.Metal);
                    break;
                case Fitting.FuseBox:
                    Deco(At(0f, 0f, 0.1f), new Vector3(w, h, 0.2f), rot, new Color(0.26f, 0.3f, 0.28f), Surface.Metal);
                    Deco(At(0.2f, 0.1f, 0.205f), new Vector3(0.05f, 0.12f, 0.02f), rot, Soot, Surface.Metal);
                    Deco(At(0f, h * 0.3f, 0.205f), new Vector3(0.24f, 0.18f, 0.005f), rot, HazardYellow, Surface.Plain);
                    // Conduit up from the box to the ceiling run.
                    Rod(At(0f, h / 2f, 0.06f), f.At(t, 4.75f, 0.06f), 0.05f, new Color(0.32f, 0.33f, 0.33f), Surface.Metal);
                    break;
                case Fitting.NoticeBoard:
                    Deco(At(0f, 0f, 0.015f), new Vector3(w, h, 0.03f), rot, new Color(0.4f, 0.3f, 0.18f), Surface.Fabric);
                    Deco(At(0f, 0f, 0.012f), new Vector3(w + 0.08f, h + 0.08f, 0.024f), rot, DarkWood, Surface.Wood);
                    for (int p = 0; p < 5; p++)
                        Deco(At(Range(rng, -w / 2f + 0.15f, w / 2f - 0.15f), Range(rng, -h / 2f + 0.15f, h / 2f - 0.15f), 0.032f), new Vector3(0.21f, 0.28f, 0.003f),
                            rot * Quaternion.Euler(0f, 0f, Range(rng, -10f, 10f)), new Color(0.6f, 0.58f, 0.5f), Surface.Plaster);
                    break;
                case Fitting.Sconce:
                    Deco(At(0f, -0.1f, 0.06f), new Vector3(0.08f, 0.25f, 0.12f), rot, new Color(0.25f, 0.2f, 0.12f), Surface.Metal);
                    Deco(At(0f, 0.04f, 0.15f), new Vector3(0.14f, 0.03f, 0.14f), rot, new Color(0.25f, 0.2f, 0.12f), Surface.Metal);
                    DecoPrim(PrimitiveType.Cylinder, At(0f, 0.12f, 0.15f), new Vector3(0.05f, 0.07f, 0.05f), Quaternion.identity, new Color(0.7f, 0.66f, 0.55f), Surface.Plaster);
                    if (rng.NextDouble() < 0.7) Glow(PrimitiveType.Sphere, At(0f, 0.22f, 0.15f), new Vector3(0.04f, 0.07f, 0.04f), Quaternion.identity, new Color(1f, 0.6f, 0.22f));
                    break;
                case Fitting.Picture:
                    Deco(At(0f, 0f, 0.025f), new Vector3(w + 0.1f, h + 0.1f, 0.05f), rot, new Color(0.32f, 0.24f, 0.1f), Surface.Wood);
                    Deco(At(0f, 0f, 0.051f), new Vector3(w, h, 0.004f), rot, rng.NextDouble() < 0.5 ? new Color(0.22f, 0.24f, 0.2f) : new Color(0.3f, 0.22f, 0.16f), Surface.Fabric);
                    break;
                case Fitting.ValveWheel:
                    Rod(At(0f, 0f, 0f), At(0f, 0f, 0.22f), 0.08f, new Color(0.3f, 0.3f, 0.3f), Surface.Metal);
                    DecoPrim(PrimitiveType.Cylinder, At(0f, 0f, 0.24f), new Vector3(0.5f, 0.02f, 0.5f), rot * Quaternion.Euler(90f, 0f, 0f), new Color(0.45f, 0.08f, 0.06f), Surface.Metal);
                    Bar(At(-0.22f, 0f, 0.26f), At(0.22f, 0f, 0.26f), 0.04f, 0.03f, f.Inward, new Color(0.45f, 0.08f, 0.06f), Surface.Metal);
                    Bar(At(0f, -0.22f, 0.26f), At(0f, 0.22f, 0.26f), 0.04f, 0.03f, f.Inward, new Color(0.45f, 0.08f, 0.06f), Surface.Metal);
                    // Its pipe drops from the run overhead.
                    Rod(At(0f, 0f, 0.12f), f.At(t, 5f, 0.12f), 0.12f, new Color(0.4f, 0.26f, 0.16f), Surface.Rust);
                    break;
                case Fitting.Ivy:
                    for (int p = 0; p < 7; p++)
                        Deco(At(Range(rng, -w / 2f, w / 2f), Range(rng, -h / 2f, h / 2f), 0.01f), new Vector3(Range(rng, 0.4f, 1.1f), Range(rng, 0.4f, 1.2f), 0.02f),
                            rot * Quaternion.Euler(0f, 0f, Range(rng, -30f, 30f)), rng.NextDouble() < 0.6 ? new Color(0.12f, 0.2f, 0.08f) : new Color(0.18f, 0.22f, 0.09f), Surface.Grass);
                    break;
                case Fitting.Trellis:
                    for (float dx = -w / 2f; dx <= w / 2f + 0.01f; dx += 0.6f)
                        Deco(At(dx, 0f, 0.04f), new Vector3(0.04f, h, 0.03f), rot, Wood, Surface.Wood);
                    for (float dy = -h / 2f; dy <= h / 2f + 0.01f; dy += 0.6f)
                        Deco(At(0f, dy, 0.06f), new Vector3(w, 0.04f, 0.03f), rot, Wood, Surface.Wood);
                    for (int p = 0; p < 6; p++)
                        Deco(At(Range(rng, -w / 2f, w / 2f), Range(rng, -h / 2f, h / 4f), 0.075f), new Vector3(Range(rng, 0.3f, 0.7f), Range(rng, 0.5f, 1.2f), 0.02f),
                            rot * Quaternion.Euler(0f, 0f, Range(rng, -20f, 20f)), new Color(0.13f, 0.21f, 0.08f), Surface.Grass);
                    break;
                case Fitting.Pegboard:
                    Deco(At(0f, 0f, 0.015f), new Vector3(w, h, 0.03f), rot, new Color(0.42f, 0.34f, 0.22f), Surface.Wood);
                    for (int p = 0; p < 6; p++)
                    {
                        float dx = Range(rng, -w / 2f + 0.15f, w / 2f - 0.15f), dy = Range(rng, -h / 2f + 0.2f, h / 2f - 0.15f);
                        Deco(At(dx, dy, 0.05f), new Vector3(Range(rng, 0.04f, 0.08f), Range(rng, 0.2f, 0.4f), 0.03f),
                            rot * Quaternion.Euler(0f, 0f, Range(rng, -40f, 40f)), rng.NextDouble() < 0.5 ? new Color(0.2f, 0.2f, 0.22f) : new Color(0.5f, 0.12f, 0.08f), Surface.Metal);
                    }
                    break;
                case Fitting.Panel:
                    Deco(At(0f, 0f, 0.03f), new Vector3(w, h, 0.06f), rot, new Color(0.14f, 0.15f, 0.18f), Surface.Metal);
                    Glow(At(0f, 0.15f, 0.062f), new Vector3(w * 0.7f, h * 0.3f, 0.005f), rot, new Color(0.12f, 0.4f, 0.36f));
                    for (int p = 0; p < 3; p++)
                        Glow(At(-w * 0.25f + p * w * 0.25f, -h * 0.25f, 0.062f), new Vector3(0.04f, 0.04f, 0.005f), rot,
                            p == 1 ? new Color(0.9f, 0.2f, 0.1f) : new Color(0.2f, 0.8f, 0.4f));
                    break;
            }
        }

        /// <summary>Spray-painted strokes, and now and then words, on a wall.</summary>
        void Graffiti(WallFace f, float t, float y, float w, float h, System.Random rng)
        {
            var paints = new[] { new Color(0.45f, 0.06f, 0.05f), new Color(0.6f, 0.6f, 0.56f), new Color(0.1f, 0.3f, 0.2f), new Color(0.12f, 0.15f, 0.35f) };
            var paint = paints[rng.Next(paints.Length)];
            if (_graffitiText < MaxGraffitiText && rng.NextDouble() < 0.35)
            {
                // Unlit text: kept dark so it reads as paint, not a sign.
                var holder = Greybox.Empty("Graffiti", Root, f.At(t, y, 0.01f));
                holder.localRotation = f.Facing * Quaternion.Euler(0f, 0f, Range(rng, -5f, 5f));
                Greybox.Label("Text", holder, Vector3.zero, GraffitiTexts[(_graffitiText * 7 + f.Zone) % GraffitiTexts.Length], new Color(0.34f, 0.05f, 0.04f), 0.42f);
                _graffitiText++;
                return;
            }
            int strokes = rng.Next(3, 7);
            for (int s = 0; s < strokes; s++)
            {
                var a = f.At(t + Range(rng, -w / 2f, w / 2f), y + Range(rng, -h / 2f, h / 2f), 0.004f);
                var b = a + f.Along * Range(rng, -0.8f, 0.8f) + Vector3.up * Range(rng, -0.5f, 0.5f);
                Bar(a, b, 0.006f, Range(rng, 0.05f, 0.12f), f.Inward, paint, Surface.Plain);
            }
        }

        /// <summary>A plate over each side of every door naming the zone through it, with a lamp above.</summary>
        void DoorSigns()
        {
            for (int d = 0; d < DoorLinks.Length; d++)
            {
                var c = DoorCenter(d);
                var normal = DoorInWallAlongZ(d) ? Vector3.right : Vector3.forward;
                for (int s = -1; s <= 1; s += 2)
                {
                    var inward = normal * s;
                    int here = ZoneOfPoint(c + inward);
                    int other = DoorLinks[d].x == here ? DoorLinks[d].y : DoorLinks[d].x;
                    var rot = Quaternion.LookRotation(inward, Vector3.up);
                    var face = c + inward * (WallHalf + SkinDepth);
                    Deco(face + inward * 0.02f + Vector3.up * 3.75f, new Vector3(2.2f, 0.46f, 0.04f), rot, new Color(0.12f, 0.13f, 0.13f), Surface.Metal);
                    Deco(face + inward * 0.015f + Vector3.up * 3.75f, new Vector3(2.3f, 0.54f, 0.03f), rot, HazardYellow, Surface.Metal);
                    var holder = Greybox.Empty("Door Sign", Root, face + inward * 0.045f + Vector3.up * 3.75f);
                    holder.localRotation = rot;
                    Greybox.Label("Name", holder, Vector3.zero, ZoneNames[other].ToUpperInvariant(), new Color(0.72f, 0.66f, 0.52f), 0.3f);
                    WallLamp(face + Vector3.up * 4.45f, inward, true);
                }
            }
        }

        // ---------------------------------------------------------------- floors

        enum Litter { Rubble, Paper, Puddle, Oil, Leaves, Crack, Blood, Weeds, Gravel, Drain, Pallet, Plank }

        /// <summary>How much of each kind of litter a zone gets, by weight, in Litter order.</summary>
        static readonly int[][] LitterMix =
        {
            new[] { 3, 2, 3, 1, 2, 3, 1, 1, 0, 1, 0, 0 }, // Gate Yard
            new[] { 2, 3, 1, 2, 0, 2, 1, 0, 0, 0, 3, 1 }, // Depot
            new[] { 2, 1, 1, 5, 0, 2, 1, 0, 0, 1, 0, 1 }, // Garage
            new[] { 3, 2, 0, 0, 2, 2, 2, 0, 0, 0, 0, 2 }, // Chapel
            new[] { 1, 3, 0, 0, 1, 1, 2, 0, 0, 0, 0, 2 }, // Barracks
            new[] { 3, 1, 2, 0, 4, 3, 1, 3, 0, 1, 0, 0 }, // Courtyard
            new[] { 2, 1, 3, 3, 1, 3, 1, 1, 0, 1, 2, 0 }, // Loading Dock
            new[] { 1, 1, 5, 1, 0, 1, 1, 0, 0, 3, 0, 0 }, // Pump Station
            new[] { 3, 0, 4, 1, 1, 0, 1, 2, 4, 0, 1, 0 }, // Crane Yard
            new[] { 1, 1, 1, 2, 0, 0, 1, 0, 0, 2, 0, 0 }, // Lantern Core
            new[] { 1, 6, 0, 0, 1, 0, 1, 0, 0, 0, 0, 2 }, // Archives
            new[] { 1, 0, 1, 0, 4, 0, 1, 5, 1, 0, 0, 1 }, // Greenhouse
            new[] { 3, 0, 4, 0, 0, 3, 2, 1, 1, 2, 0, 1 }, // Gaol
            new[] { 1, 3, 1, 0, 0, 1, 3, 0, 0, 0, 0, 1 }, // Plague Ward
            new[] { 1, 6, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, // Scriptorium
            new[] { 3, 0, 1, 3, 0, 2, 1, 0, 2, 1, 0, 1 }, // Alchemist's Furnace
            new[] { 5, 0, 1, 0, 3, 3, 1, 4, 3, 0, 0, 2 }, // Rampart Ruins
            new[] { 1, 1, 1, 0, 2, 1, 1, 0, 0, 0, 0, 0 }, // Sunken Temple
        };

        static float LitterRadius(Litter kind) => kind switch
        {
            Litter.Puddle => 1.2f,
            Litter.Oil => 0.8f,
            Litter.Crack => 1.2f,
            Litter.Gravel => 1.2f,
            Litter.Pallet => 0.8f,
            Litter.Plank => 0.7f,
            Litter.Weeds => 0.6f,
            _ => 0.5f,
        };

        /// <summary>Scatters a zone's litter over its open floor, about one heap per 12 square metres.</summary>
        void ScatterFloor(int zone)
        {
            var rng = new System.Random(7300 + zone * 104729);
            var rects = ZoneRects(zone);
            float area = 0f;
            foreach (var r in rects) area += r.width * r.height;
            int count = Mathf.Clamp(Mathf.RoundToInt(area / 12f), 12, 110);
            var mix = LitterMix[zone];
            int total = 0;
            foreach (int w in mix) total += w;
            for (int i = 0; i < count; i++)
            {
                float pick = (float)rng.NextDouble() * area;
                var rect = rects[rects.Length - 1];
                foreach (var r in rects)
                {
                    if (pick < r.width * r.height) { rect = r; break; }
                    pick -= r.width * r.height;
                }
                int roll = rng.Next(total), k = 0;
                while (roll >= mix[k]) roll -= mix[k++];
                var kind = (Litter)k;
                var p = new Vector3(Range(rng, rect.xMin + 0.8f, rect.xMax - 0.8f), 0f, Range(rng, rect.yMin + 0.8f, rect.yMax - 0.8f));
                if (rect.width < 2 || rect.height < 2 || !FloorFree(p, LitterRadius(kind))) continue;
                PlaceLitter(kind, p, zone, rng);
            }
        }

        void PlaceLitter(Litter kind, Vector3 p, int zone, System.Random rng)
        {
            var look = Looks[zone];
            float yaw = Range(rng, 0f, 360f);
            var q = Quaternion.Euler(0f, yaw, 0f);
            switch (kind)
            {
                case Litter.Rubble:
                {
                    bool brick = look.Wall == Surface.Brick;
                    int n = rng.Next(3, 8);
                    for (int i = 0; i < n; i++)
                    {
                        float s = Range(rng, 0.05f, 0.16f), hgt = Mathf.Min(0.1f, s * Range(rng, 0.5f, 0.9f));
                        var at = p + new Vector3(Range(rng, -0.5f, 0.5f), hgt / 2f, Range(rng, -0.5f, 0.5f));
                        var color = brick && rng.NextDouble() < 0.5 ? Looks[zone].WallColor : new Color(0.4f, 0.39f, 0.36f);
                        Deco(at, new Vector3(s, hgt, s * Range(rng, 0.6f, 1.2f)), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), color, brick ? Surface.Brick : Surface.Concrete);
                    }
                    break;
                }
                case Litter.Paper:
                {
                    int n = rng.Next(1, 5);
                    for (int i = 0; i < n; i++)
                        Deco(p + new Vector3(Range(rng, -0.4f, 0.4f), 0.004f + i * 0.002f, Range(rng, -0.4f, 0.4f)), new Vector3(0.21f, 0.002f, 0.297f),
                            Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), rng.NextDouble() < 0.6 ? new Color(0.6f, 0.58f, 0.5f) : new Color(0.48f, 0.44f, 0.36f), Surface.Plaster);
                    break;
                }
                case Litter.Puddle:
                {
                    int n = rng.Next(1, 4);
                    for (int i = 0; i < n; i++)
                        Splat(p + q * new Vector3(Range(rng, -0.4f, 0.4f), 0f, Range(rng, -0.3f, 0.3f)), Range(rng, 0.7f, 1.8f), Range(rng, 0.5f, 1.3f),
                            yaw + Range(rng, -40f, 40f), 0.004f, new Color(0.03f, 0.035f, 0.04f), Surface.Metal);
                    break;
                }
                case Litter.Oil:
                    Splat(p, Range(rng, 0.5f, 1.3f), Range(rng, 0.4f, 1f), yaw, 0.003f, new Color(0.04f, 0.035f, 0.03f), Surface.Metal);
                    Splat(p + q * new Vector3(0.4f, 0f, 0.2f), Range(rng, 0.2f, 0.5f), Range(rng, 0.2f, 0.4f), yaw, 0.0035f, new Color(0.04f, 0.035f, 0.03f), Surface.Metal);
                    break;
                case Litter.Leaves:
                {
                    int n = rng.Next(8, 18);
                    var browns = new[] { new Color(0.3f, 0.18f, 0.08f), new Color(0.38f, 0.26f, 0.1f), new Color(0.22f, 0.16f, 0.08f) };
                    for (int i = 0; i < n; i++)
                        Deco(p + new Vector3(Range(rng, -0.7f, 0.7f), 0.004f + (i % 3) * 0.0015f, Range(rng, -0.7f, 0.7f)), new Vector3(0.07f, 0.003f, 0.045f),
                            Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), browns[rng.Next(browns.Length)], Surface.Grass);
                    break;
                }
                case Litter.Crack:
                {
                    var at = p;
                    float heading = yaw;
                    int n = rng.Next(3, 6);
                    for (int i = 0; i < n; i++)
                    {
                        float len = Range(rng, 0.3f, 0.8f);
                        var dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                        var next = at + dir * len;
                        if ((next - p).sqrMagnitude > 1.1f * 1.1f) break;
                        Deco((at + next) / 2f + Vector3.up * 0.002f, new Vector3(Range(rng, 0.02f, 0.045f), 0.003f, len), Quaternion.Euler(0f, heading, 0f),
                            Shade(look.FloorColor, 0.3f), Surface.Plain);
                        at = next;
                        heading += Range(rng, -40f, 40f);
                    }
                    break;
                }
                case Litter.Blood:
                {
                    var blood = new Color(0.17f, 0.02f, 0.015f);
                    Splat(p, Range(rng, 0.4f, 0.9f), Range(rng, 0.3f, 0.7f), yaw, 0.005f, blood, Surface.Rubber);
                    int n = rng.Next(3, 7);
                    for (int i = 0; i < n; i++)
                    {
                        float s = Range(rng, 0.05f, 0.16f);
                        Splat(p + q * new Vector3(Range(rng, -0.2f, 0.2f), 0f, Range(rng, 0.3f, 0.9f)), s, s, 0f, 0.005f, blood, Surface.Rubber);
                    }
                    break;
                }
                case Litter.Weeds:
                {
                    int n = rng.Next(2, 6);
                    for (int i = 0; i < n; i++)
                    {
                        var at = p + new Vector3(Range(rng, -0.4f, 0.4f), 0f, Range(rng, -0.4f, 0.4f));
                        var green = rng.NextDouble() < 0.5 ? new Color(0.15f, 0.22f, 0.08f) : new Color(0.22f, 0.24f, 0.1f);
                        Deco(at + Vector3.up * 0.006f, new Vector3(Range(rng, 0.25f, 0.7f), 0.01f, Range(rng, 0.2f, 0.5f)), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), green, Surface.Grass);
                        DecoPrim(PrimitiveType.Sphere, at + Vector3.up * 0.04f, new Vector3(Range(rng, 0.15f, 0.3f), 0.08f, Range(rng, 0.15f, 0.3f)), Quaternion.identity, green, Surface.Grass);
                    }
                    break;
                }
                case Litter.Gravel:
                {
                    Splat(p, Range(rng, 1.2f, 2.2f), Range(rng, 1f, 1.8f), yaw, 0.004f, new Color(0.33f, 0.31f, 0.28f), Surface.Concrete);
                    for (int i = 0; i < 6; i++)
                    {
                        float s = Range(rng, 0.04f, 0.09f);
                        Deco(p + new Vector3(Range(rng, -0.7f, 0.7f), s / 3f, Range(rng, -0.7f, 0.7f)), new Vector3(s, s / 1.5f, s), Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f),
                            new Color(0.36f, 0.35f, 0.32f), Surface.Concrete);
                    }
                    break;
                }
                case Litter.Drain:
                    Splat(p, 1.3f, 1.3f, 0f, 0.003f, Shade(look.FloorColor, 0.5f), Surface.Metal);
                    Deco(p + Vector3.up * 0.01f, new Vector3(0.7f, 0.02f, 0.7f), q, new Color(0.08f, 0.08f, 0.08f), Surface.Metal);
                    for (int i = -2; i <= 2; i++)
                        Deco(p + q * new Vector3(i * 0.12f, 0.022f, 0f), new Vector3(0.04f, 0.01f, 0.62f), q, new Color(0.2f, 0.19f, 0.18f), Surface.Rust);
                    break;
                case Litter.Pallet:
                {
                    var pq = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f + Range(rng, -8f, 8f), 0f);
                    for (int i = -1; i <= 1; i++)
                        Deco(p + pq * new Vector3(0f, 0.04f, i * 0.45f), new Vector3(1.2f, 0.08f, 0.1f), pq, Shade(Wood, 0.8f), Surface.Wood);
                    for (int i = -2; i <= 2; i++)
                        Deco(p + pq * new Vector3(i * 0.26f, 0.09f, 0f), new Vector3(0.14f, 0.02f, 1f), pq, Wood, Surface.Wood);
                    break;
                }
                case Litter.Plank:
                {
                    int n = rng.Next(1, 4);
                    for (int i = 0; i < n; i++)
                        Deco(p + new Vector3(Range(rng, -0.3f, 0.3f), 0.0125f + i * 0.025f, Range(rng, -0.3f, 0.3f)), new Vector3(0.12f, 0.025f, Range(rng, 0.7f, 1.3f)),
                            Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Board, Surface.Wood);
                    break;
                }
            }
        }

        /// <summary>A painted line on a zone's floor between two zone-local points, dashed if dash > 0. Stays out of the keep-outs.</summary>
        void Paint(int zone, float x0, float z0, float x1, float z1, float width, Color color, float dash = 0f, float gap = 0f)
        {
            Vector3 a = L(zone, x0, 0f, z0), b = L(zone, x1, 0f, z1);
            float len = Vector3.Distance(a, b);
            var dir = (b - a) / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float piece = dash > 0f ? dash : 1f, step = dash > 0f ? dash + gap : 1f;
            for (float t = 0f; t < len - 0.05f; t += step)
            {
                float l = Mathf.Min(piece, len - t);
                var c = a + dir * (t + l / 2f) + Vector3.up * 0.003f;
                if (KeptClear(c, width)) Deco(c, new Vector3(width, 0.003f, l), rot, color, Surface.Concrete);
            }
        }

        // ---------------------------------------------------------------- props

        /// <summary>Two dark straps round a crate.</summary>
        void CrateBands(Vector3 center, float size)
        {
            foreach (float y in new[] { -0.38f, 0.38f })
                Deco(center + Vector3.up * y, new Vector3(size + 0.02f, 0.07f, size + 0.02f), new Color(0.16f, 0.15f, 0.14f), Surface.Metal);
        }

        /// <summary>Corrugation ribs, rails, corner castings and door bars on a shipping container, given by its zone-local corners.</summary>
        void ContainerDetails(int zone, float x0, float z0, float x1, float z1, float height, Color color)
        {
            bool alongX = x1 - x0 >= z1 - z0;
            float len = alongX ? x1 - x0 : z1 - z0, wid = alongX ? z1 - z0 : x1 - x0;
            var center = L(zone, (x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            var rot = Quaternion.LookRotation(alongX ? Vector3.right : Vector3.forward, Vector3.up);
            Vector3 Local(float side, float y, float along) => center + rot * new Vector3(side, y, along);
            var rib = Shade(color, 0.75f);
            var dark = Shade(color, 0.45f);
            foreach (float s in new[] { -1f, 1f })
            {
                for (float a = -len / 2f + 0.35f; a < len / 2f - 0.3f; a += 0.45f)
                    Deco(Local(s * (wid / 2f + 0.015f), height / 2f, a), new Vector3(0.03f, height - 0.3f, 0.07f), rot, rib, Surface.Metal);
                foreach (float y in new[] { 0.08f, height - 0.08f })
                    Deco(Local(s * (wid / 2f + 0.02f), y, 0f), new Vector3(0.04f, 0.16f, len), rot, dark, Surface.Rust);
                foreach (float e in new[] { -1f, 1f })
                    foreach (float y in new[] { 0.1f, height - 0.1f })
                        Deco(Local(s * (wid / 2f - 0.08f), y, e * (len / 2f - 0.08f)), new Vector3(0.2f, 0.2f, 0.2f), rot, Soot, Surface.Rust);
            }
            // Door bars on one end.
            foreach (float s in new[] { -0.75f, -0.25f, 0.25f, 0.75f })
                Rod(Local(s * wid / 2f, 0.2f, len / 2f + 0.03f), Local(s * wid / 2f, height - 0.2f, len / 2f + 0.03f), 0.05f, dark, Surface.Rust);
            Deco(Local(0f, height / 2f, len / 2f + 0.01f), new Vector3(0.03f, height - 0.2f, 0.02f), rot, Soot, Surface.Metal);
        }

        /// <summary>Wheels, windows, lights and bumpers on a wrecked car, given its footprint centre and size.</summary>
        void CarDetails(Vector3 floorCenter, Vector3 size, bool alongZ, System.Random rng)
        {
            var rot = Quaternion.LookRotation(alongZ ? Vector3.forward : Vector3.right, Vector3.up);
            float len = alongZ ? size.z : size.x, wid = alongZ ? size.x : size.z;
            Vector3 Local(float side, float y, float along) => floorCenter + rot * new Vector3(side, y, along);
            var tyre = new Color(0.05f, 0.05f, 0.05f);
            foreach (float s in new[] { -1f, 1f })
                foreach (float a in new[] { -len / 2f + 0.75f, len / 2f - 0.75f })
                {
                    Rod(Local(s * (wid / 2f - 0.08f), 0.34f, a), Local(s * (wid / 2f + 0.06f), 0.34f, a), 0.68f, tyre, Surface.Rubber);
                    Rod(Local(s * (wid / 2f + 0.05f), 0.34f, a), Local(s * (wid / 2f + 0.07f), 0.34f, a), 0.3f, new Color(0.3f, 0.3f, 0.3f), Surface.Metal);
                }
            // Window band round the top, broken lights and bumpers.
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(Local(s * (wid / 2f + 0.005f), 1.2f, -0.1f), new Vector3(0.01f, 0.36f, len * 0.5f), rot, Glass, Surface.Metal);
                Deco(Local(0f, 1.2f, s * (len / 2f + 0.005f)), new Vector3(wid * 0.8f, 0.36f, 0.01f), rot, Glass, Surface.Metal);
                Deco(Local(0f, 0.45f, s * (len / 2f + 0.03f)), new Vector3(wid + 0.05f, 0.14f, 0.07f), rot, new Color(0.12f, 0.12f, 0.12f), Surface.Metal);
                foreach (float l in new[] { -1f, 1f })
                    Deco(Local(l * (wid / 2f - 0.3f), 0.82f, s * (len / 2f + 0.008f)), new Vector3(0.28f, 0.12f, 0.015f), rot,
                        s > 0f ? new Color(0.22f, 0.22f, 0.2f) : new Color(0.25f, 0.03f, 0.02f), Surface.Metal);
            }
            for (int i = 0; i < 3; i++)
                Deco(Local(Range(rng, -wid / 3f, wid / 3f), size.y + 0.004f, Range(rng, -len / 3f, len / 3f)), new Vector3(Range(rng, 0.3f, 0.7f), 0.006f, Range(rng, 0.3f, 0.8f)),
                    rot * Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f), new Color(0.42f, 0.24f, 0.13f), Surface.Rust);
        }

        /// <summary>Uprights, beams and boxed stock on both long sides of a steel shelf run (long along x).</summary>
        void Racking(Vector3 floorCenter, float length, float depth, float height, System.Random rng)
        {
            var orange = new Color(0.55f, 0.27f, 0.08f);
            var stock = new[] { new Color(0.45f, 0.35f, 0.22f), new Color(0.38f, 0.3f, 0.2f), new Color(0.3f, 0.32f, 0.3f), new Color(0.5f, 0.42f, 0.28f) };
            foreach (float s in new[] { -1f, 1f })
            {
                float z = s * (depth / 2f + 0.015f);
                for (float x = -length / 2f + 0.05f; x <= length / 2f; x += length / Mathf.Max(1, Mathf.RoundToInt(length / 2.3f)))
                    Deco(floorCenter + new Vector3(Mathf.Min(x, length / 2f - 0.05f), height / 2f, z), new Vector3(0.08f, height, 0.04f), Quaternion.identity, orange, Surface.Metal);
                foreach (float y in new[] { 0.1f, 1.15f, height - 0.06f })
                    Deco(floorCenter + new Vector3(0f, y, s * (depth / 2f + 0.02f)), new Vector3(length, 0.1f, 0.03f), Quaternion.identity, orange, Surface.Metal);
                foreach (var (y0, y1) in new[] { (0.16f, 1.1f), (1.21f, height - 0.12f) })
                    for (float x = -length / 2f + 0.15f; x < length / 2f - 0.3f;)
                    {
                        float w = Range(rng, 0.35f, 0.9f), h = Range(rng, 0.25f, y1 - y0);
                        if (x + w > length / 2f - 0.15f) break;
                        if (rng.NextDouble() < 0.8)
                            Deco(floorCenter + new Vector3(x + w / 2f, y0 + h / 2f, s * (depth / 2f + 0.008f)), new Vector3(w, h, 0.016f), Quaternion.identity,
                                stock[rng.Next(stock.Length)], rng.NextDouble() < 0.7 ? Surface.Plaster : Surface.Wood);
                        x += w + Range(rng, 0.05f, 0.3f);
                    }
            }
        }

        /// <summary>Rows of books on both long sides of a bookcase (long along z).</summary>
        void Books(Vector3 floorCenter, float width, float length, float height, System.Random rng)
        {
            var covers = new[]
            {
                new Color(0.35f, 0.08f, 0.06f), new Color(0.12f, 0.2f, 0.12f), new Color(0.3f, 0.22f, 0.12f),
                new Color(0.1f, 0.12f, 0.25f), new Color(0.42f, 0.32f, 0.12f), new Color(0.2f, 0.16f, 0.12f),
            };
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (width / 2f + 0.012f);
                for (float y0 = 0.1f; y0 < height - 0.3f; y0 += 0.52f)
                {
                    Deco(floorCenter + new Vector3(s * (width / 2f + 0.015f), y0, 0f), new Vector3(0.03f, 0.03f, length), Quaternion.identity, DarkWood, Surface.Wood);
                    for (float z = -length / 2f + 0.06f; z < length / 2f - 0.1f;)
                    {
                        float w = Range(rng, 0.12f, 0.5f), h = Range(rng, 0.26f, 0.44f);
                        if (z + w > length / 2f - 0.06f) break;
                        if (rng.NextDouble() < 0.88)
                            Deco(floorCenter + new Vector3(x, y0 + 0.015f + h / 2f, z + w / 2f), new Vector3(0.024f, h, w), Quaternion.identity, covers[rng.Next(covers.Length)], Surface.Fabric);
                        z += w + Range(rng, 0.01f, 0.12f);
                    }
                }
            }
        }

        /// <summary>Panes of dim coloured glass in a leaded frame, high on a wall.</summary>
        void StainedGlass(Vector3 face, Vector3 inward, System.Random rng)
        {
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var right = rot * Vector3.right;
            var center = face + inward * (SkinDepth + 0.03f) + Vector3.up * 5.2f;
            Deco(center, new Vector3(1.95f, 2.85f, 0.06f), rot, new Color(0.06f, 0.06f, 0.07f), Surface.Metal);
            var tints = new[] { new Color(0.4f, 0.06f, 0.05f), new Color(0.06f, 0.12f, 0.4f), new Color(0.45f, 0.28f, 0.05f), new Color(0.08f, 0.3f, 0.12f), new Color(0.22f, 0.07f, 0.32f) };
            for (int c = 0; c < 3; c++)
                for (int r = 0; r < 4; r++)
                {
                    var at = center + right * ((c - 1) * 0.6f) + Vector3.up * ((r - 1.5f) * 0.68f) + inward * 0.032f;
                    if (rng.NextDouble() < 0.15) Deco(at, new Vector3(0.54f, 0.62f, 0.005f), rot, new Color(0.015f, 0.015f, 0.02f), Surface.Plain);
                    else Glow(at, new Vector3(0.54f, 0.62f, 0.005f), rot, tints[rng.Next(tints.Length)]);
                }
        }

        // ---------------------------------------------------------------- zones

        /// <summary>All the colliderless dressing that needs the solid map in place to keep clear of it.</summary>
        void DressZones()
        {
            // Colliders made this frame aren't in the physics scene until synced, and the clearance checks need them.
            Physics.SyncTransforms();
            CollectKeepOuts();
            // Props first: they're solid, so the wall clutter and floor litter placed after keep clear of them.
            // The rooms' own furniture before the general barrels and crates, so it gets the pick of the walls.
            _propSpots.Clear();
            DressRooms();
            DressLab();
            DressProps();
            DressWallFaces();
            DoorSigns();
            DressYards();
            DressHalls();
            DressWorks();
            DressNewRooms();
            for (int z = 0; z < ZoneCount; z++) ScatterFloor(z);
        }

        /// <summary>The open-air zones: Gate Yard, Courtyard, Loading Dock, Crane Yard.</summary>
        void DressYards()
        {
            var rng = new System.Random(811);

            // Gate Yard: a faded road line up the old gate passage and across the yard, its kerb lines and a stop line.
            Paint(0, 0f, -21.4f, 0f, 2.6f, 0.12f, FadedYellow, 1.2f, 1f);
            Paint(0, -4.3f, -21.6f, -4.3f, -14.4f, 0.1f, FadedWhite);
            Paint(0, 4.3f, -21.6f, 4.3f, -14.4f, 0.1f, FadedWhite);
            Paint(0, -4f, -13.6f, 4f, -13.6f, 0.3f, FadedWhite);
            Paint(0, -3.5f, -8f, 3.5f, -8f, 0.1f, FadedYellow, 0.4f, 0.4f);

            // Courtyard: water in the fountain, a mossy ring round its foot, bases and caps on the pillars,
            // and rubble heaped where the north-west corner fell in.
            DecoPrim(PrimitiveType.Cylinder, L(5, 4f, 0.805f, -1f), new Vector3(4.3f, 0.004f, 4.3f), Quaternion.identity, new Color(0.04f, 0.06f, 0.07f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, L(5, 4f, 0.003f, -1f), new Vector3(5.5f, 0.003f, 5.5f), Quaternion.identity, new Color(0.17f, 0.2f, 0.12f), Surface.Grass);
            foreach (var p in new[] { new Vector2(-3f, -8f), new Vector2(11f, -8f), new Vector2(-3f, 6f), new Vector2(11f, 6f) })
            {
                Deco(L(5, p.x, 0.06f, p.y), new Vector3(1.6f, 0.12f, 1.6f), new Color(0.4f, 0.38f, 0.35f), Surface.Concrete);
                Deco(L(5, p.x, WallHeight - 0.15f, p.y), new Vector3(1.65f, 0.3f, 1.65f), new Color(0.4f, 0.38f, 0.35f), Surface.Concrete);
            }
            for (int i = 0; i < 16; i++)
            {
                var p = L(5, Range(rng, -18f, -9.5f), 0f, Range(rng, 2.5f, 6.4f));
                if (FloorFree(p, 0.5f)) PlaceLitter(Litter.Rubble, p, 5, rng);
            }

            // Loading Dock: hazard striping along the dock edge, rubber bumpers on its face, bay lines, the truck and containers.
            for (float z = -8f; z < 26f - 0.01f; z += 0.6f)
            {
                if (Mathf.Abs(z + 0.3f + 4f) < 1.6f || Mathf.Abs(z + 0.3f - 20f) < 1.6f) continue;
                bool yellow = Mathf.RoundToInt((z + 8f) / 0.6f) % 2 == 0;
                Deco(L(6, 9.17f, 1.404f, z + 0.3f), new Vector3(0.3f, 0.008f, 0.6f), yellow ? HazardYellow : Soot, Surface.Concrete);
            }
            foreach (float z in new[] { -7f, -1f, 3f, 8f, 13f, 16.5f, 24f })
                Deco(L(6, 8.96f, 0.85f, z), new Vector3(0.08f, 0.5f, 0.35f), new Color(0.06f, 0.06f, 0.06f), Surface.Rubber);
            Paint(6, -9.3f, -7f, -9.3f, 0f, 0.12f, FadedWhite);
            Paint(6, -3.7f, -7f, -3.7f, 0f, 0.12f, FadedWhite);
            Paint(6, -12f, -9f, -12f, -2f, 0.12f, FadedWhite);
            TruckDetails();
            ContainerDetails(6, -10f, 6f, -4f, 8.6f, 2.6f, Rust);
            ContainerDetails(6, 1f, -7f, 7f, -4.4f, 2.6f, ContainerBlue);

            // Crane Yard: tyre ruts through the dirt, the crane's bracing, cab and counterweight, and the containers.
            foreach (float z in new[] { -12.6f, -11f })
                Paint(8, -21f, z, 21f, z, 0.45f, new Color(0.17f, 0.14f, 0.1f), 2f, 0f);
            foreach (float x in new[] { 1f, 2.6f })
                Paint(8, x, -12f, x, -17f, 0.45f, new Color(0.17f, 0.14f, 0.1f), 2f, 0f);
            CraneDetails();
            ContainerDetails(8, 4f, 6f, 12f, 9f, 3.6f, Rust);
            ContainerDetails(8, -11f, -9.3f, -5f, -6.7f, 2.6f, ContainerGreen);
            ContainerDetails(8, 4.7f, -9f, 7.3f, -3f, 2.6f, Rust);
            ContainerDetails(8, -20f, 2f, -14f, 4.6f, 2.6f, ContainerBlue);
        }

        void TruckDetails()
        {
            var tyre = new Color(0.05f, 0.05f, 0.05f);
            var hub = new Color(0.3f, 0.3f, 0.3f);
            // Trailer: dark chassis band, wheels, a faded livery stripe.
            foreach (float x in new[] { -8.01f, -4.99f })
            {
                Deco(L(6, x, 0.55f, -2.5f), new Vector3(0.02f, 1.1f, 7f), Soot, Surface.Metal);
                Deco(L(6, x, 2.35f, -2.5f), new Vector3(0.02f, 0.3f, 6.9f), new Color(0.45f, 0.08f, 0.06f), Surface.Metal);
                float out_ = x < -6f ? -1f : 1f;
                foreach (float z in new[] { -5f, -3.9f, 0f })
                {
                    Rod(L(6, x - out_ * 0.1f, 0.48f, z), L(6, x + out_ * 0.08f, 0.48f, z), 0.96f, tyre, Surface.Rubber);
                    Rod(L(6, x + out_ * 0.07f, 0.48f, z), L(6, x + out_ * 0.09f, 0.48f, z), 0.42f, hub, Surface.Metal);
                }
            }
            // Cab: wheels, windscreen, side windows, grille and headlamps (one still glowing faintly).
            foreach (float x in new[] { -7.9f, -5.1f })
            {
                float out_ = x < -6f ? -1f : 1f;
                Rod(L(6, x - out_ * 0.1f, 0.48f, 2.5f), L(6, x + out_ * 0.08f, 0.48f, 2.5f), 0.96f, tyre, Surface.Rubber);
                Deco(L(6, x + out_ * 0.01f, 1.85f, 2.5f), new Vector3(0.02f, 0.6f, 1f), Glass, Surface.Metal);
            }
            Deco(L(6, -6.5f, 1.85f, 3.31f), new Vector3(2.4f, 0.65f, 0.02f), Glass, Surface.Metal);
            Deco(L(6, -6.5f, 0.75f, 3.32f), new Vector3(1.4f, 0.5f, 0.03f), Soot, Surface.Metal);
            Deco(L(6, -6.5f, 0.3f, 3.35f), new Vector3(2.9f, 0.22f, 0.1f), new Color(0.2f, 0.2f, 0.2f), Surface.Metal);
            Glow(L(6, -7.5f, 0.85f, 3.32f), new Vector3(0.3f, 0.18f, 0.02f), Quaternion.identity, new Color(0.45f, 0.4f, 0.28f));
            Deco(L(6, -5.5f, 0.85f, 3.32f), new Vector3(0.3f, 0.18f, 0.02f), Glass, Surface.Metal);
        }

        void CraneDetails()
        {
            var brace = Shade(CraneYellow, 0.75f);
            var mast = L(8, 12f, 0f, -10f);
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var side = Vector3.Cross(Vector3.up, n);
                var face = mast + n * 0.77f;
                for (float y = 0.2f; y < 5.4f; y += 1.3f)
                {
                    Bar(face + side * -0.65f + Vector3.up * y, face + side * 0.65f + Vector3.up * (y + 1.3f), 0.08f, 0.04f, n, brace, Surface.Metal);
                    Bar(face + side * 0.65f + Vector3.up * y, face + side * -0.65f + Vector3.up * (y + 1.3f), 0.08f, 0.04f, n, brace, Surface.Metal);
                }
            }
            // Cab on top, a counter-jib with its concrete weight, and the hook block on the cable.
            Deco(L(8, 11.4f, 5.05f, -10f), new Vector3(1.8f, 1.3f, 1.7f), CraneYellow, Surface.Metal);
            Deco(L(8, 10.49f, 5.15f, -10f), new Vector3(0.02f, 0.7f, 1.4f), Glass, Surface.Metal);
            Deco(L(8, 14f, 5.9f, -10f), new Vector3(3.2f, 0.5f, 0.5f), CraneYellow, Surface.Metal);
            Deco(L(8, 15f, 5.2f, -10f), new Vector3(1.3f, 1.1f, 1.1f), new Color(0.4f, 0.39f, 0.36f), Surface.Concrete);
            Deco(L(8, 2.5f, 4.35f, -10f), new Vector3(0.45f, 0.35f, 0.3f), new Color(0.15f, 0.15f, 0.15f), Surface.Metal);
            Deco(L(8, 2.5f, 4.05f, -10f), new Vector3(0.08f, 0.3f, 0.08f), new Color(0.15f, 0.15f, 0.15f), Surface.Rust);
            Glow(PrimitiveType.Sphere, L(8, 13f, 6.35f, -10f), Vector3.one * 0.18f, Quaternion.identity, new Color(1f, 0.15f, 0.08f));
        }

        /// <summary>The indoor zones: Depot, Chapel, Barracks, Archives.</summary>
        void DressHalls()
        {
            var rng = new System.Random(1213);

            // Depot: aisle lines, racking and stock on the shelves.
            foreach (float z in new[] { -3.4f, 2.4f })
                Paint(1, -17f, z, 17f, z, 0.1f, FadedYellow);
            Racking(L(1, -10f, 0f, -5f), 14f, 1f, 2.4f, rng);
            Racking(L(1, -10f, 0f, 4f), 14f, 1f, 2.4f, rng);
            Racking(L(1, 8f, 0f, -6f), 8f, 1f, 2.4f, rng);

            // Chapel: a runner up the nave, rails on the pews, candles on the lectern (with a small warm light),
            // and stained glass high in the nave walls.
            Deco(L(3, 0f, 0.006f, -5.15f), new Vector3(1.6f, 0.01f, 27.3f), new Color(0.3f, 0.07f, 0.06f), Surface.Fabric);
            foreach (float x in new[] { -0.82f, 0.82f })
                Deco(L(3, x, 0.008f, -5.15f), new Vector3(0.06f, 0.012f, 27.3f), new Color(0.42f, 0.32f, 0.12f), Surface.Fabric);
            if (PropModels.GetPack(Pew.Path, PackMaterial) == null)
                foreach (float z in new[] { -18f, -15f, -12f })
                    foreach (float x in new[] { -4.2f, 4.2f })
                        Deco(L(3, x, 0.92f, z), new Vector3(4.5f, 0.04f, 0.7f), Shade(DarkWood, 0.8f), Surface.Wood);
            foreach (float x in new[] { -0.3f, 0f, 0.3f })
            {
                float h = 0.12f + Mathf.Abs(x) * 0.3f;
                DecoPrim(PrimitiveType.Cylinder, L(3, x, 1.2f + h / 2f, 10f), new Vector3(0.05f, h / 2f, 0.05f), Quaternion.identity, new Color(0.72f, 0.68f, 0.56f), Surface.Plaster);
                Glow(PrimitiveType.Sphere, L(3, x, 1.2f + h + 0.035f, 10f), new Vector3(0.035f, 0.06f, 0.035f), Quaternion.identity, new Color(1f, 0.6f, 0.22f));
            }
            var candle = new GameObject("Candle Light").AddComponent<Light>();
            candle.transform.SetParent(Root, false);
            candle.transform.localPosition = L(3, 0f, 1.7f, 9.4f);
            candle.type = LightType.Point;
            candle.color = new Color(1f, 0.62f, 0.3f);
            candle.range = 5f;
            candle.intensity = 1.2f;
            candle.shadows = LightShadows.None;
            foreach (float z in new[] { 22f, 51f })
            {
                StainedGlass(new Vector3(-12f + WallHalf, 0f, z), Vector3.right, rng);
                StainedGlass(new Vector3(4f - WallHalf, 0f, z), Vector3.left, rng);
            }
            StainedGlass(new Vector3(0f, 0f, 58f - WallHalf), Vector3.back, rng);

            // Barracks: mattresses and pillows on the bunks, with the gap between the tiers.
            foreach (float z in new[] { -17f, -13f, -9f, 9f, 13f, 17f })
                foreach (float x in new[] { 0f, 5f })
                {
                    var c = L(4, x, 0f, z);
                    var ticking = new Color(0.33f, 0.34f, 0.28f);
                    Deco(c + Vector3.up * 1.65f, new Vector3(1.9f, 0.1f, 0.9f), ticking, Surface.Fabric);
                    Deco(c + new Vector3(-0.65f, 1.74f, 0f), new Vector3(0.42f, 0.08f, 0.6f), new Color(0.55f, 0.53f, 0.47f), Surface.Fabric);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Deco(c + new Vector3(0f, 1.05f, s * 0.505f), new Vector3(1.85f, 0.4f, 0.01f), new Color(0.03f, 0.03f, 0.03f), Surface.Plain);
                        Deco(c + new Vector3(0f, 0.78f, s * 0.505f), new Vector3(1.85f, 0.14f, 0.012f), ticking, Surface.Fabric);
                    }
                }

            // Archives: books on every case (the bookcase models come with their own), filing cabinets along the
            // records loft, and a rug in the reading room. The books draw from their own generator, so the rest
            // comes out the same with or without the models.
            var bookRng = new System.Random(rng.Next());
            if (PropModels.GetPack(Bookcase.Path, PackMaterial) == null)
            {
                foreach (float x in new[] { -10f, -1f })
                    Books(L(10, x, 0f, 4.7f), 0.8f, 4f, StackHeight, bookRng);
                foreach (float x in new[] { -10f, 3f, 7f })
                    Books(L(10, x, 0f, -4.7f), 0.8f, 4f, StackHeight, bookRng);
                foreach (float x in new[] { 5f, 13f })
                    Books(L(10, x, 0f, 11.5f), 0.8f, 5f, StackHeight, bookRng);
            }
            var cabinet = new Color(0.3f, 0.33f, 0.3f);
            foreach (float x in new[] { 2.5f, 3.5f, 4.5f, 5.5f, 12.5f, 13.5f, 14.5f, 15.5f })
            {
                Deco(L(10, x, 0.72f, 16.985f), new Vector3(0.88f, 1.44f, 0.03f), cabinet, Surface.Metal);
                for (int d = 0; d < 4; d++)
                {
                    Deco(L(10, x, 0.22f + d * 0.33f, 16.965f), new Vector3(0.78f, 0.28f, 0.012f), Shade(cabinet, 1.15f), Surface.Metal);
                    Deco(L(10, x, 0.28f + d * 0.33f, 16.95f), new Vector3(0.18f, 0.03f, 0.02f), Soot, Surface.Metal);
                }
            }
            Deco(L(10, -5.5f, 0.004f, 0f), new Vector3(6.2f, 0.008f, 3.6f), new Color(0.16f, 0.06f, 0.05f), Surface.Fabric);
            Deco(L(10, -5.5f, 0.007f, 0f), new Vector3(5.8f, 0.008f, 3.2f), new Color(0.3f, 0.12f, 0.08f), Surface.Fabric);
        }

        /// <summary>The working zones: Garage, Pump Station, Lantern Core, Greenhouse.</summary>
        void DressWorks()
        {
            var rng = new System.Random(1777);

            // Garage: bay lines, the wrecks' wheels and windows, and oil under them.
            Paint(2, -11f, -12f, -11f, -1.5f, 0.12f, FadedWhite);
            Paint(2, -5f, -12f, -5f, -1.5f, 0.12f, FadedWhite);
            Paint(2, -11f, -12f, -5f, -12f, 0.12f, FadedWhite);
            CarDetails(L(2, -8f, 0f, -7f), new Vector3(2f, 1.5f, 4.2f), true, rng);
            CarDetails(L(2, -6f, 0f, 6f), new Vector3(4.2f, 1.5f, 2f), false, rng);
            Splat(L(2, -7.4f, 0f, -9.6f), 1.4f, 0.9f, 20f, 0.003f, new Color(0.04f, 0.035f, 0.03f), Surface.Metal);
            Splat(L(2, -3.4f, 0f, 6.4f), 1.1f, 1.6f, 70f, 0.003f, new Color(0.04f, 0.035f, 0.03f), Surface.Metal);

            // Pump Station: flanges on the main, its drop into the pump and riser to the roof, the pump's motor and gauges, bands on the tank.
            var pipe = new Color(0.46f, 0.27f, 0.15f);
            var dark = Shade(pipe, 0.6f);
            for (float x = -6f; x <= 6.01f; x += 2f)
                DecoPrim(PrimitiveType.Cylinder, L(7, x, 4.2f, -3f), new Vector3(0.64f, 0.04f, 0.64f), Quaternion.Euler(0f, 0f, 90f), dark, Surface.Rust);
            Rod(L(7, -6f, 4.2f, -3f), L(7, -9f, 4.2f, -3f), 0.5f, pipe, Surface.Rust);
            Rod(L(7, -9f, 4.2f, -3f), L(7, -9f, 2f, -3f), 0.5f, pipe, Surface.Rust);
            DecoPrim(PrimitiveType.Sphere, L(7, -9f, 4.2f, -3f), Vector3.one * 0.54f, Quaternion.identity, pipe, Surface.Rust);
            Rod(L(7, 6f, 4.2f, -3f), L(7, 6f, WallHeight + 0.4f, -3f), 0.5f, pipe, Surface.Rust);
            DecoPrim(PrimitiveType.Sphere, L(7, 6f, 4.2f, -3f), Vector3.one * 0.54f, Quaternion.identity, pipe, Surface.Rust);
            Rod(L(7, -11.4f, 2.32f, -3f), L(7, -9.6f, 2.32f, -3f), 0.6f, new Color(0.2f, 0.3f, 0.36f), Surface.Metal);
            foreach (float x in new[] { -11f, -9.5f })
            {
                DecoPrim(PrimitiveType.Cylinder, L(7, x, 1.4f, -1.48f), new Vector3(0.3f, 0.015f, 0.3f), Quaternion.Euler(90f, 0f, 0f), new Color(0.6f, 0.58f, 0.52f), Surface.Plain);
                Glow(L(7, x + 0.25f, 1.4f, -1.48f), new Vector3(0.04f, 0.04f, 0.01f), Quaternion.identity, new Color(0.2f, 1f, 0.3f));
            }
            foreach (float y in new[] { 1.85f, 3.15f })
                DecoPrim(PrimitiveType.Cylinder, L(7, 4.2f, y, 16.5f), new Vector3(3.68f, 0.05f, 3.68f), Quaternion.identity, dark, Surface.Rust);

            // Lantern Core: metal and glowing bands on the core, a glowing ring and spokes on the floor, cables to the
            // consoles, screens on the consoles, and a glow strip under the catwalk's edge.
            var trim = new Color(0.13f, 0.14f, 0.17f);
            if (!_reactor)
            {
                foreach (float y in new[] { 0.3f, 5.7f })
                    DecoPrim(PrimitiveType.Cylinder, L(9, 0f, y, 0f), new Vector3(CoreDiameter + 0.12f, 0.15f, CoreDiameter + 0.12f), Quaternion.identity, trim, Surface.Metal);
                foreach (float y in new[] { 1.6f, 4.6f })
                    Glow(PrimitiveType.Cylinder, L(9, 0f, y, 0f), new Vector3(CoreDiameter + 0.05f, 0.04f, CoreDiameter + 0.05f), Quaternion.identity, CoreGlow);
            }
            var ring = Shade(CoreGlow, 0.6f);
            foreach (float radius in new[] { 2.2f, 4.2f })
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * 45f;
                    var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    float len = 2f * radius * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                    Glow(L(9, 0f, 0.006f, 0f) + dir * radius, new Vector3(len, 0.006f, 0.08f), Quaternion.Euler(0f, angle, 0f), ring);
                }
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, 45f + k * 90f, 0f) * Vector3.forward;
                Glow(L(9, 0f, 0.006f, 0f) + dir * 6.4f, new Vector3(0.06f, 0.006f, 3.9f), Quaternion.LookRotation(dir), ring);
                var console = L(9, 0f, 0f, 0f) + dir * 9.9f;
                Glow(console + Vector3.up * 1.03f - dir * 0.15f, new Vector3(0.7f, 0.03f, 0.4f), Quaternion.LookRotation(-dir) * Quaternion.Euler(-15f, 0f, 0f), new Color(0.15f, 0.45f, 0.4f));
            }
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                Rod(L(9, 0f, 0.035f, 0f) + d * (CoreDiameter / 2f + 0.05f), L(9, 0f, 0.035f, 0f) + d * 11f, 0.07f, new Color(0.05f, 0.05f, 0.05f), Surface.Rubber);
            Glow(L(9, 0f, 2.69f, 15.98f), new Vector3(15.2f, 0.04f, 0.02f), Quaternion.identity, Shade(CoreGlow, 0.7f));

            // Greenhouse: soil and plants in the planters, a roof of rafters with the odd surviving pane, upper
            // panes in the south frame, and a door and window on the potting shed.
            var soil = new Color(0.22f, 0.16f, 0.1f);
            var plantColors = new[] { new Color(0.14f, 0.22f, 0.08f), new Color(0.18f, 0.26f, 0.1f), new Color(0.3f, 0.24f, 0.1f) };
            void Planter(float x, float z)
            {
                Deco(L(11, x, 0.82f, z), new Vector3(5.8f, 0.04f, 1f), soil, Surface.Dirt);
                for (float px = x - 2.5f; px <= x + 2.5f; px += Range(rng, 0.6f, 1f))
                {
                    var c = plantColors[rng.Next(plantColors.Length)];
                    DecoPrim(PrimitiveType.Sphere, L(11, px, 0.92f, z + Range(rng, -0.25f, 0.25f)), new Vector3(Range(rng, 0.3f, 0.5f), 0.22f, Range(rng, 0.3f, 0.5f)), Quaternion.identity, c, Surface.Grass);
                }
            }
            foreach (float z in new[] { -8f, -4.5f }) Planter(-9f, z);
            foreach (float z in new[] { -8f, -4.5f, -1f, 2.5f }) Planter(9f, z);
            var frame = new Color(0.26f, 0.3f, 0.29f);
            var pane = new Color(0.55f, 0.75f, 0.7f, 0.1f);
            for (int i = 0; i <= 8; i++)
                Deco(L(11, 0f, WallHeight - 0.12f, -11f + i * 2.75f), new Vector3(34f, 0.16f, 0.1f), frame, Surface.Metal);
            foreach (float x in new[] { -11f, -5.5f, 0f, 5.5f, 11f })
                Deco(L(11, x, WallHeight - 0.24f, 0f), new Vector3(0.08f, 0.1f, 22f), frame, Surface.Metal);
            for (int i = 0; i < 8; i++)
                foreach (float x in new[] { -8.25f, -2.75f, 2.75f, 8.25f })
                    if (rng.NextDouble() < 0.4)
                        Glow(L(11, x, WallHeight - 0.16f, -11f + (i + 0.5f) * 2.75f), new Vector3(5.4f, 0.01f, 2.65f), Quaternion.identity, pane);
            foreach (float x in new[] { -8f, 0f, 8f })
                Glow(L(11, x, 4.6f, -9.5f), new Vector3(7.8f, 2.6f, 0.01f), Quaternion.identity, pane);
            Deco(L(11, 0f, 3.25f, -9.5f), new Vector3(24f, 0.1f, 0.12f), frame, Surface.Metal);
            Deco(L(11, -9.5f, 1.05f, 0.99f), new Vector3(1f, 2.1f, 0.02f), Shade(DarkWood, 0.8f), Surface.Wood);
            Deco(L(11, -12f, 2.6f, 0.99f), new Vector3(1.2f, 0.9f, 0.02f), Glass, Surface.Metal);
        }

        // ---------------------------------------------------------------- skyline

        /// <summary>
        /// Colliderless silhouettes beyond the outer walls, so the night skyline isn't empty: ground, a perimeter
        /// fence with floodlight poles, a ring of dark buildings with a few lit windows, a water tower, smokestacks,
        /// a radio mast and a gantry crane. Walls are 7 m, so everything here is tall enough to show over them.
        /// </summary>
        void BuildSkyline()
        {
            var rng = new System.Random(4242);
            var b = Bounds;
            var center = new Vector3(b.center.x, 0f, b.center.y);
            var ground = Greybox.Prim(PrimitiveType.Plane, "Outer Ground", Root, center + Vector3.down * 0.04f, new Vector3(60f, 1f, 60f), new Color(0.12f, 0.11f, 0.09f),
                collider: false, surface: Surface.Dirt);
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            Fence(b, 5f, rng);

            var landmarks = new List<(Vector3 at, float radius)>
            {
                (new Vector3(b.xMin - 24f, 0f, b.yMax + 22f), 16f),
                (new Vector3(b.xMax + 26f, 0f, 40f), 12f),
                (new Vector3(24f, 0f, b.yMin - 30f), 12f),
                (new Vector3(b.xMin - 22f, 0f, b.yMin - 18f), 12f),
                (new Vector3(b.xMax + 18f, 0f, -12f), 22f),
            };
            WaterTower(landmarks[0].at);
            Smokestack(landmarks[1].at, 4.4f, 46f);
            Smokestack(landmarks[2].at, 3.6f, 38f);
            RadioMast(landmarks[3].at, 58f);
            GantryCrane(landmarks[4].at);

            // A ring of buildings along each side, facing the map.
            Buildings(new Vector3(b.xMin, 0f, b.yMin), Vector3.right, Vector3.back, b.width, rng, landmarks);
            Buildings(new Vector3(b.xMax, 0f, b.yMax), Vector3.left, Vector3.forward, b.width, rng, landmarks);
            Buildings(new Vector3(b.xMin, 0f, b.yMax), Vector3.back, Vector3.left, b.height, rng, landmarks);
            Buildings(new Vector3(b.xMax, 0f, b.yMin), Vector3.forward, Vector3.right, b.height, rng, landmarks);
        }

        /// <summary>Chain-link-style fence (posts, rails, wire) round the map, with tall floodlight poles, most of them dead.</summary>
        void Fence(Rect b, float offset, System.Random rng)
        {
            var steel = new Color(0.2f, 0.21f, 0.22f);
            var corners = new[]
            {
                new Vector3(b.xMin - offset, 0f, b.yMin - offset), new Vector3(b.xMax + offset, 0f, b.yMin - offset),
                new Vector3(b.xMax + offset, 0f, b.yMax + offset), new Vector3(b.xMin - offset, 0f, b.yMax + offset),
            };
            for (int s = 0; s < 4; s++)
            {
                Vector3 a = corners[s], c = corners[(s + 1) % 4];
                float len = Vector3.Distance(a, c);
                var dir = (c - a) / len;
                for (float t = 0f; t < len; t += 4f)
                    Deco(a + dir * t + Vector3.up * 1.6f, new Vector3(0.08f, 3.2f, 0.08f), steel, Surface.Metal);
                foreach (float y in new[] { 0.15f, 1.6f, 3.15f })
                    Rod(a + Vector3.up * y, c + Vector3.up * y, 0.05f, steel, Surface.Metal);
                foreach (float y in new[] { 3.35f, 3.5f, 3.65f })
                    Rod(a + Vector3.up * y, c + Vector3.up * y, 0.015f, steel, Surface.Metal);
                var toMap = Vector3.Cross(dir, Vector3.up);
                for (float t = 18f; t < len - 8f; t += 36f)
                {
                    var foot = a + dir * t;
                    Deco(foot + Vector3.up * 7f, new Vector3(0.3f, 14f, 0.3f), steel, Surface.Metal);
                    var head = foot + Vector3.up * 13.8f + toMap * 0.6f;
                    var rot = Quaternion.LookRotation(toMap, Vector3.up) * Quaternion.Euler(25f, 0f, 0f);
                    Deco(head, new Vector3(1.4f, 0.6f, 0.4f), rot, new Color(0.15f, 0.15f, 0.16f), Surface.Metal);
                    var lens = head + rot * new Vector3(0f, 0f, 0.21f);
                    if (rng.NextDouble() < 0.35) Glow(lens, new Vector3(1.2f, 0.45f, 0.02f), rot, new Color(1f, 0.92f, 0.75f));
                    else Deco(lens, new Vector3(1.2f, 0.45f, 0.02f), rot, Glass, Surface.Metal);
                }
            }
        }

        /// <summary>
        /// Dark blocks along one side of the map: start is the corner to work from, along runs down the side, out
        /// points away from the map. Their map-facing walls have windows, a few of them lit.
        /// </summary>
        void Buildings(Vector3 start, Vector3 along, Vector3 outward, float sideLength, System.Random rng, List<(Vector3 at, float radius)> avoid)
        {
            var skins = new[]
            {
                (Surface.Concrete, new Color(0.2f, 0.2f, 0.19f)), (Surface.Brick, new Color(0.22f, 0.15f, 0.12f)), (Surface.Metal, new Color(0.17f, 0.18f, 0.19f)),
            };
            var rot = Quaternion.LookRotation(-outward, Vector3.up);
            for (float t = -6f; t < sideLength + 6f;)
            {
                float w = Range(rng, 10f, 24f), d = Range(rng, 10f, 18f), h = Range(rng, 14f, 32f), off = Range(rng, 12f, 28f);
                if (t + w > sideLength + 8f) break;
                var mid = start + along * (t + w / 2f) + outward * (off + d / 2f);
                t += w + Range(rng, 2f, 9f);
                bool near = false;
                foreach (var (at, r) in avoid)
                    if (Vector3.Distance(new Vector3(mid.x, 0f, mid.z), at) < r + Mathf.Max(w, d) / 2f) near = true;
                if (near || rng.NextDouble() < 0.15) continue;
                var (surface, color) = skins[rng.Next(skins.Length)];
                Deco(mid + Vector3.up * (h / 2f), new Vector3(w, h, d), rot, color, surface);
                Deco(mid + Vector3.up * (h + 0.4f), new Vector3(w + 0.4f, 0.8f, d + 0.4f), rot, Shade(color, 0.8f), Surface.Concrete);
                // Roof clutter: a tank or plant housing, and sometimes an aerial.
                var roof = mid + Vector3.up * (h + 0.8f);
                if (rng.NextDouble() < 0.5)
                    DecoPrim(PrimitiveType.Cylinder, roof + along * Range(rng, -w / 4f, w / 4f) + Vector3.up * 1.5f, new Vector3(3f, 1.5f, 3f), Quaternion.identity, Shade(color, 0.9f), Surface.Rust);
                else
                    Deco(roof + along * Range(rng, -w / 4f, w / 4f) + Vector3.up * 1f, new Vector3(Range(rng, 3f, 6f), 2f, Range(rng, 3f, 5f)), rot, Shade(color, 0.85f), Surface.Metal);
                if (rng.NextDouble() < 0.3)
                {
                    var foot = roof + along * Range(rng, -w / 3f, w / 3f);
                    Rod(foot, foot + Vector3.up * Range(rng, 5f, 9f), 0.12f, new Color(0.15f, 0.15f, 0.15f), Surface.Metal);
                }
                // Windows on the side facing the map.
                var face = mid - outward * (d / 2f + 0.03f);
                for (float y = 3f; y < h - 2f; y += 3.6f)
                    for (float x = -w / 2f + 1.6f; x < w / 2f - 1.2f; x += 3.2f)
                    {
                        var at = face + along * x + Vector3.up * y;
                        double roll = rng.NextDouble();
                        if (roll < 0.05) Glow(at, new Vector3(1.4f, 1.8f, 0.04f), rot, rng.NextDouble() < 0.5 ? new Color(0.5f, 0.36f, 0.16f) : new Color(0.3f, 0.34f, 0.4f));
                        else if (roll < 0.9) Deco(at, new Vector3(1.4f, 1.8f, 0.04f), rot, Glass, Surface.Metal);
                    }
            }
        }

        void WaterTower(Vector3 at)
        {
            var rust = new Color(0.32f, 0.2f, 0.14f);
            var legs = new[] { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, 4f) };
            for (int i = 0; i < 4; i++)
            {
                Rod(at + legs[i], at + legs[i] * 0.7f + Vector3.up * 26f, 0.6f, rust, Surface.Rust);
                var next = legs[(i + 1) % 4];
                for (float y = 6f; y < 25f; y += 8f)
                {
                    float k0 = 1f - 0.3f * y / 26f, k1 = 1f - 0.3f * (y + 8f) / 26f;
                    Rod(at + legs[i] * k0 + Vector3.up * y, at + next * k1 + Vector3.up * Mathf.Min(26f, y + 8f), 0.2f, rust, Surface.Rust);
                    Rod(at + next * k0 + Vector3.up * y, at + legs[i] * k1 + Vector3.up * Mathf.Min(26f, y + 8f), 0.2f, rust, Surface.Rust);
                }
            }
            DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * 26f, new Vector3(10.5f, 0.15f, 10.5f), Quaternion.identity, new Color(0.15f, 0.15f, 0.15f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * 29.6f, new Vector3(9f, 3.5f, 9f), Quaternion.identity, rust, Surface.Rust);
            DecoPrim(PrimitiveType.Sphere, at + Vector3.up * 33.1f, new Vector3(9.2f, 3f, 9.2f), Quaternion.identity, Shade(rust, 0.8f), Surface.Rust);
            Glow(PrimitiveType.Sphere, at + Vector3.up * 34.8f, Vector3.one * 0.5f, Quaternion.identity, new Color(1f, 0.12f, 0.06f));
        }

        void Smokestack(Vector3 at, float diameter, float height)
        {
            var brick = new Color(0.25f, 0.16f, 0.12f);
            DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * (height / 2f), new Vector3(diameter, height / 2f, diameter), Quaternion.identity, brick, Surface.Brick);
            for (float y = 8f; y < height; y += 10f)
                DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * y, new Vector3(diameter + 0.2f, 0.3f, diameter + 0.2f), Quaternion.identity, Soot, Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * (height - 0.6f), new Vector3(diameter + 0.5f, 0.6f, diameter + 0.5f), Quaternion.identity, Shade(brick, 0.5f), Surface.Brick);
            for (int k = 0; k < 3; k++)
                Glow(PrimitiveType.Sphere, at + Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward * (diameter / 2f + 0.3f) + Vector3.up * (height - 2f),
                    Vector3.one * 0.45f, Quaternion.identity, new Color(1f, 0.12f, 0.06f));
        }

        void RadioMast(Vector3 at, float height)
        {
            var steel = new Color(0.22f, 0.22f, 0.23f);
            Deco(at + Vector3.up * (height / 2f), new Vector3(0.8f, height, 0.8f), steel, Surface.Metal);
            for (int k = 0; k < 3; k++)
            {
                var anchor = at + Quaternion.Euler(0f, k * 120f + 30f, 0f) * Vector3.forward * 22f;
                Rod(anchor, at + Vector3.up * height * 0.8f, 0.05f, steel, Surface.Metal);
            }
            for (float y = height / 3f; y <= height + 0.1f; y += height / 3f)
                Glow(PrimitiveType.Sphere, at + Vector3.up * y, Vector3.one * 0.6f, Quaternion.identity, new Color(1f, 0.12f, 0.06f));
        }

        /// <summary>A dockside gantry crane east of the Loading Dock, its boom reaching back over the wall.</summary>
        void GantryCrane(Vector3 at)
        {
            var paint = new Color(0.4f, 0.16f, 0.1f);
            foreach (float z in new[] { -7f, 7f })
                foreach (float x in new[] { -5f, 5f })
                    Rod(at + new Vector3(x, 0f, z), at + new Vector3(x * 0.6f, 24f, z), 0.9f, paint, Surface.Rust);
            foreach (float z in new[] { -7f, 7f })
            {
                Deco(at + new Vector3(0f, 12f, z), new Vector3(8f, 0.8f, 0.8f), paint, Surface.Rust);
                Deco(at + new Vector3(0f, 24f, z), new Vector3(7f, 1f, 1f), paint, Surface.Rust);
            }
            Deco(at + new Vector3(0f, 25.5f, 0f), new Vector3(46f, 1.6f, 3f), paint, Surface.Rust);
            Deco(at + new Vector3(3f, 23.6f, 0f), new Vector3(3f, 2.2f, 2.6f), new Color(0.2f, 0.2f, 0.2f), Surface.Metal);
            Glow(at + new Vector3(1.48f, 23.8f, 0f), new Vector3(0.02f, 0.8f, 1.8f), Quaternion.identity, new Color(0.35f, 0.3f, 0.2f));
            Rod(at + new Vector3(-12f, 25f, 0f), at + new Vector3(-12f, 14f, 0f), 0.08f, Soot, Surface.Metal);
            Glow(PrimitiveType.Sphere, at + new Vector3(-22.5f, 26.6f, 0f), Vector3.one * 0.5f, Quaternion.identity, new Color(1f, 0.12f, 0.06f));
        }

        // ---------------------------------------------------------------- batching

        /// <summary>
        /// Collects colliderless decoration and merges it into one mesh per material per 48 m cell, so the
        /// dressing costs a few hundred draw calls instead of thousands of renderers. No shadows: it's all thin or
        /// flat against something that already casts one.
        /// </summary>
        sealed class DecorBatch
        {
            // Small enough that each merged piece is lit by the lamps near it (forward rendering gives an object
            // only its few strongest lights per pixel), big enough to keep the draw count down.
            const float CellSize = 16f;
            static readonly Dictionary<PrimitiveType, Mesh> s_Meshes = new Dictionary<PrimitiveType, Mesh>();
            readonly Dictionary<(Material, int, int), List<CombineInstance>> _parts = new Dictionary<(Material, int, int), List<CombineInstance>>();
            readonly HashSet<Material> _shadowed = new HashSet<Material>();

            public void Add(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material material)
            {
                var key = (material, Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));
                if (!_parts.TryGetValue(key, out var list)) _parts[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = MeshOf(type), transform = Matrix4x4.TRS(pos, rot, scale) });
            }

            /// <summary>One submesh of a model (a scanned prop). Shadowed materials cast shadows once merged.</summary>
            public void Add(Mesh mesh, int subMesh, Matrix4x4 transform, Material material, bool shadows)
            {
                Vector3 pos = transform.GetColumn(3);
                var key = (material, Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));
                if (!_parts.TryGetValue(key, out var list)) _parts[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = subMesh, transform = transform });
                if (shadows) _shadowed.Add(material);
            }

            static Mesh MeshOf(PrimitiveType type)
            {
                if (s_Meshes.TryGetValue(type, out var mesh) && mesh != null) return mesh;
                var probe = GameObject.CreatePrimitive(type);
                mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(probe);
                s_Meshes[type] = mesh;
                return mesh;
            }

            public void Build(Transform parent)
            {
                var holder = Greybox.Empty("Decor", parent, Vector3.zero);
                var owner = holder.gameObject.AddComponent<DecorMeshes>();
                var toLocal = holder.worldToLocalMatrix;
                foreach (var kv in _parts)
                {
                    var material = kv.Key.Item1;
                    var list = kv.Value;
                    bool readable = true;
                    foreach (var c in list) readable &= c.mesh.isReadable;
                    if (!readable)
                    {
                        // Built-in meshes that can't be read can't be merged: fall back to one renderer each.
                        foreach (var c in list) Single(holder, c, toLocal, material, Shadows(material));
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        var c = list[i];
                        c.transform = toLocal * c.transform;
                        list[i] = c;
                    }
                    var mesh = new Mesh { name = "Decor " + material.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
                    owner.Meshes.Add(mesh);
                    var go = new GameObject("Decor " + material.name);
                    go.transform.SetParent(holder, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = Shadows(material);
                }
                _parts.Clear();
                _shadowed.Clear();
            }

            ShadowCastingMode Shadows(Material material) => _shadowed.Contains(material) ? ShadowCastingMode.On : ShadowCastingMode.Off;

            static void Single(Transform holder, CombineInstance c, Matrix4x4 toLocal, Material material, ShadowCastingMode shadows)
            {
                var m = toLocal * c.transform;
                var go = new GameObject("Decor");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = m.GetColumn(3);
                go.transform.localRotation = m.rotation;
                go.transform.localScale = m.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = c.mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = shadows;
            }
        }
    }

    /// <summary>Frees the map's merged decoration meshes along with the map.</summary>
    public class DecorMeshes : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in Meshes)
                if (m != null) Destroy(m);
            Meshes.Clear();
        }
    }
}
