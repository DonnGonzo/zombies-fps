using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Chapel as a gothic church. A nave of fluted columns and pointed arches opens on to a side aisle either
    /// side; a great arch frames the chancel, half a metre up at the north end behind a communion rail, with carved
    /// choir stalls, the pipe organ, and the high altar on its own step before a gilded three-bay reredos. Stone
    /// ribs cross a night-blue vault picked out in gold stars, chandeliers hang from the bosses, and lancets and a
    /// rose of leaded stained glass light the walls. Knights lie on their tombs in the aisles, a font stands by the
    /// door and a pulpit by the chancel step. The Chapel skips the generic wall clutter, lamps and roof beams.
    /// </summary>
    public partial class ZoneMap
    {
        const int ChapelZone = 3;

        static readonly Color ChapelStone = new Color(0.66f, 0.61f, 0.52f);
        static readonly Color PierStone = new Color(0.6f, 0.57f, 0.52f);
        static readonly Color PlinthStone = new Color(0.32f, 0.3f, 0.28f);
        static readonly Color ChancelTile = new Color(0.5f, 0.38f, 0.32f);
        static readonly Color Crimson = new Color(0.4f, 0.05f, 0.06f);
        static readonly Color Linen = new Color(0.82f, 0.8f, 0.74f);
        static readonly Color IconBlue = new Color(0.07f, 0.1f, 0.26f);
        static readonly Color ChapelGlow = new Color(1f, 0.74f, 0.45f);

        // Everything below is zone-local. The nave is x -8..8 the whole length (z -21..21), the aisles x 8..20 either
        // side of its south part (z -21..7); the chancel is the nave's north end.

        /// <summary>The arcades between nave and aisles: the columns stand on x = ±ArcadeX.</summary>
        const float ArcadeX = 8f;
        const float ColumnRadius = 0.45f, ArcadeSpring = 3.75f, ArcadeApex = 6.8f, RibSpring = 5.62f, RibRise = 1.3f;
        /// <summary>Along each arcade: a half column against the south wall, three columns, and the chancel arch's column.</summary>
        static readonly float[] ArcadeSupports = { -20.7f, -14f, -7f, 0f, 6.85f };
        const float ChancelArchZ = 6.85f;

        const float ChancelFront = 8.5f, ChancelHeight = 0.5f, RailZ = 8.85f;
        const float PredellaHalf = 3.6f, PredellaFront = 15.4f, PredellaTop = 0.75f;
        const float AltarZ = 17.8f, AltarTop = PredellaTop + 1f;
        const float ChapelNorth = 21f - WallHalf, ReredosFront = ChapelNorth - 0.55f, ReredosBase = 2f;
        const float OrganX = 5.55f, OrganZ0 = 12.8f, OrganZ1 = 18.4f, OrganCase = 2.6f;

        static readonly float[] PewRows = { -16.5f, -14f, -11.5f, -9f, -6.5f, -4f };
        /// <summary>The knights' tombs in the aisles (x, z), long north-south.</summary>
        static readonly Vector2[] ChapelTombs = { new Vector2(-14.5f, -16f), new Vector2(-14.5f, -8.5f), new Vector2(14.5f, -16f), new Vector2(14.5f, -8.5f) };
        static readonly Vector2 FontSpot = new Vector2(-5.2f, -18.6f), PulpitSpot = new Vector2(-6.2f, 5f), ShrineSpot = new Vector2(10.2f, 6.27f);

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildChapel()
        {
            // The columns: a box each to stand behind, drawn round in the makeover. The south half columns are in the wall.
            foreach (float x in new[] { -ArcadeX, ArcadeX })
                for (int i = 1; i < ArcadeSupports.Length; i++)
                {
                    var box = Greybox.Box("Column", Root, L(ChapelZone, x, WallHeight / 2f, ArcadeSupports[i]), new Vector3(0.9f, WallHeight, 0.9f), PierStone, surface: Surface.Fluted);
                    box.GetComponent<Renderer>().enabled = false;
                }

            // The chancel, half a metre up across the north end with a step along its front, and the altar's own step.
            Block("Chancel", ChapelZone, -ArcadeX, ChancelFront, ArcadeX, 21f, ChancelHeight, ChancelTile, Surface.Mosaic);
            Greybox.Box("Chancel Step", Root, L(ChapelZone, 0f, ChancelHeight / 4f, ChancelFront - StepRun / 2f), new Vector3(ArcadeX * 2f, ChancelHeight / 2f, StepRun), Shade(ChapelStone, 0.92f), surface: Surface.Ashlar);
            Block("Predella", ChapelZone, -PredellaHalf, PredellaFront, PredellaHalf, 21f, PredellaTop, Shade(ChapelStone, 0.95f), Surface.Ashlar);
            Greybox.Box("High Altar", Root, L(ChapelZone, 0f, PredellaTop + 0.5f, AltarZ), new Vector3(3f, 1f, 1.1f), ChapelStone, surface: Surface.Ashlar);
            Greybox.Box("Reredos", Root, L(ChapelZone, 0f, ReredosBase / 2f, (ReredosFront + ChapelNorth) / 2f), new Vector3(10.4f, ReredosBase, ChapelNorth - ReredosFront), ChapelStone, surface: Surface.Ashlar);

            // The communion rail, open in the middle.
            foreach (float s in new[] { -1f, 1f })
            {
                var rail = Greybox.Box("Communion Rail", Root, L(ChapelZone, s * 4.6f, ChancelHeight + 0.48f, RailZ), new Vector3(6f, 0.96f, 0.22f), DarkWood, surface: Surface.Wood);
                rail.GetComponent<Renderer>().enabled = false;
            }

            // Choir stalls: two rows facing across the chancel, the back row a step up; the organ on the east side.
            Block("Stall Step", ChapelZone, -ArcadeX, 9.3f, -6.6f, 15.7f, ChancelHeight + 0.25f, DarkWood, Surface.Wood);
            Block("Stall Step", ChapelZone, 6.6f, 9.3f, ArcadeX, 12.5f, ChancelHeight + 0.25f, DarkWood, Surface.Wood);
            foreach (float z in new[] { 10.95f, 14.1f })
            {
                PlacePackItem(Pew, L(ChapelZone, -7.05f, ChancelHeight + 0.25f, z), Quaternion.Euler(0f, 90f, 0f), true);
                PlacePackItem(Pew, L(ChapelZone, -5.95f, ChancelHeight, z), Quaternion.Euler(0f, 90f, 0f), true);
            }
            PlacePackItem(Pew, L(ChapelZone, 7.05f, ChancelHeight + 0.25f, 10.9f), Quaternion.Euler(0f, -90f, 0f), true);
            PlacePackItem(Pew, L(ChapelZone, 5.95f, ChancelHeight, 10.9f), Quaternion.Euler(0f, -90f, 0f), true);
            Block("Organ", ChapelZone, OrganX, OrganZ0, ArcadeX, OrganZ1, OrganCase, Shade(DarkWood, 0.9f), Surface.Wood);

            // Pews either side of the nave's middle aisle, facing the altar.
            foreach (float z in PewRows)
                foreach (float x in new[] { -4f, 4f })
                    Reskin(Greybox.Box("Pew", Root, L(ChapelZone, x, 0.45f, z), new Vector3(3.1f, 0.9f, 0.6f), new Color(0.3f, 0.19f, 0.11f), surface: Surface.Wood), Pew, false, Pew.Size.y);

            // The pulpit and the font (drawn round), the knights' tomb chests and the shrine's altar.
            Greybox.Box("Pulpit", Root, L(ChapelZone, PulpitSpot.x, 1f, PulpitSpot.y), new Vector3(1.4f, 2f, 1.4f), DarkWood, surface: Surface.Wood).GetComponent<Renderer>().enabled = false;
            Greybox.Box("Font", Root, L(ChapelZone, FontSpot.x, 0.5f, FontSpot.y), new Vector3(1.2f, 1f, 1.2f), PlinthStone, surface: Surface.Masonry).GetComponent<Renderer>().enabled = false;
            foreach (var t in ChapelTombs)
                Greybox.Box("Tomb", Root, L(ChapelZone, t.x, 0.45f, t.y), new Vector3(1.05f, 0.9f, 2.25f), PlinthStone, surface: Surface.Masonry);
            Greybox.Box("Shrine", Root, L(ChapelZone, ShrineSpot.x, 0.5f, ShrineSpot.y), new Vector3(1.9f, 1f, 0.85f), ChapelStone, surface: Surface.Ashlar);
        }

        // ---------------------------------------------------------------- dressing

        void ChapelMakeover()
        {
            ChapelArcades();
            ChapelVault();
            ChapelChancel();
            ChapelReredos();
            ChapelOrgan();
            ChapelNave();
            ChapelAisles();
            ChapelWindows();
            ChapelLights();
        }

        /// <summary>The columns, the pointed arches between them with the wall over them, and the chancel arch.</summary>
        void ChapelArcades()
        {
            foreach (float x in new[] { -ArcadeX, ArcadeX })
            {
                for (int i = 0; i < ArcadeSupports.Length; i++)
                {
                    float z = ArcadeSupports[i];
                    ChapelColumn(L(ChapelZone, x, 0f, z));
                    // The wall over the column, and a vaulting shaft up each face of it to the ribs.
                    Deco(L(ChapelZone, x, (ArcadeSpring + WallHeight) / 2f, z), new Vector3(0.7f, WallHeight - ArcadeSpring, 0.9f), ChapelStone, Surface.Ashlar);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var shaft = L(ChapelZone, x + s * 0.42f, 0f, z);
                        Rod(shaft + Vector3.up * ArcadeSpring, shaft + Vector3.up * (RibSpring - 0.1f), 0.2f, PierStone, Surface.Fluted);
                        Deco(shaft + Vector3.up * (RibSpring - 0.08f), new Vector3(0.34f, 0.14f, 0.34f), ChapelStone, Surface.Ashlar);
                        Deco(shaft + Vector3.up * (RibSpring - 0.16f), new Vector3(0.3f, 0.03f, 0.3f), Shade(OldGold, 0.8f), Surface.Metal);
                    }
                    if (i == 0) continue;
                    float z0 = ArcadeSupports[i - 1] + ColumnRadius, z1 = z - ColumnRadius;
                    ArcadeArch(L(ChapelZone, x, ArcadeSpring, z0), L(ChapelZone, x, ArcadeSpring, z1), Vector3.right, 0.7f);
                }
                // A cornice along the top of the arcade, both faces.
                float a = ArcadeSupports[0], b = ArcadeSupports[ArcadeSupports.Length - 1];
                Deco(L(ChapelZone, x, 6.86f, (a + b) / 2f), new Vector3(0.9f, 0.16f, b - a), Shade(ChapelStone, 0.95f), Surface.Ashlar);
                Deco(L(ChapelZone, x, 6.75f, (a + b) / 2f), new Vector3(0.82f, 0.04f, b - a), Shade(OldGold, 0.75f), Surface.Metal);
            }

            // The chancel arch: the full width of the nave, springing from the last pair of columns, with a gilded soffit.
            var left = L(ChapelZone, -ArcadeX + 0.4f, ArcadeSpring, ChancelArchZ);
            var right = L(ChapelZone, ArcadeX - 0.4f, ArcadeSpring, ChancelArchZ);
            var pts = ArcadeArch(left, right, Vector3.forward, 0.8f);
            ArchRing(Outward(pts, Vector3.forward, -0.22f), Vector3.forward, 0.36f, 0.03f, OldGold, Surface.Metal);
        }

        /// <summary>A fluted column on a dark plinth, with a moulded base, a gilded necking, a bell capital and a square abacus.</summary>
        void ChapelColumn(Vector3 foot)
        {
            Deco(foot + Vector3.up * 0.22f, new Vector3(1.2f, 0.44f, 1.2f), PlinthStone, Surface.Masonry);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 0.52f, new Vector3(1.1f, 0.08f, 1.1f), Quaternion.identity, ChapelStone, Surface.Ashlar);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 1.9f, new Vector3(ColumnRadius * 2f, 1.3f, ColumnRadius * 2f), Quaternion.identity, PierStone, Surface.Fluted);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 3.22f, new Vector3(0.95f, 0.025f, 0.95f), Quaternion.identity, OldGold, Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 3.42f, new Vector3(1.12f, 0.17f, 1.12f), Quaternion.identity, ChapelStone, Surface.Ashlar);
            Deco(foot + Vector3.up * 3.67f, new Vector3(1.3f, 0.16f, 1.3f), Shade(ChapelStone, 1.05f), Surface.Ashlar);
        }

        /// <summary>
        /// A pointed arch from a to b (springing points at the same height) up to ArcadeApex: the wall over it in
        /// slices down to the curve, the arch ring of voussoirs, and a hood moulding round the ring. Returns the curve.
        /// </summary>
        Vector3[] ArcadeArch(Vector3 a, Vector3 b, Vector3 across, float thick)
        {
            var pts = ArchCurve(a, b, ArcadeApex - a.y, 12);
            var along = b - a;
            along.y = 0f;
            along.Normalize();
            for (int k = 0; k < pts.Length - 1; k++)
            {
                float bottom = Mathf.Max(pts[k].y, pts[k + 1].y), span = Vector3.Dot(pts[k + 1] - pts[k], along);
                if (span < 0.005f) continue;
                var mid = (pts[k] + pts[k + 1]) / 2f;
                var size = across * thick + along * (span + 0.01f);
                Deco(new Vector3(mid.x, (bottom + WallHeight) / 2f, mid.z), new Vector3(Mathf.Abs(size.x), WallHeight - bottom, Mathf.Abs(size.z)), ChapelStone, Surface.Ashlar);
            }
            ArchRing(pts, across, thick + 0.1f, 0.3f, Shade(ChapelStone, 1.08f), Surface.Ashlar);
            ArchRing(Outward(pts, across, 0.2f), across, thick + 0.16f, 0.08f, Shade(ChapelStone, 0.9f), Surface.Ashlar);
            return pts;
        }

        /// <summary>
        /// Points along an arch from a to b (its springing points, level), rising by rise in the middle: a pointed arch
        /// of two arcs when it rises more than half its span, otherwise a half ellipse.
        /// </summary>
        static Vector3[] ArchCurve(Vector3 a, Vector3 b, float rise, int perHalf)
        {
            var span = b - a;
            span.y = 0f;
            float half = span.magnitude / 2f;
            var dir = span / (2f * half);
            var pts = new Vector3[perHalf * 2 + 1];
            if (rise > half)
            {
                // Each arc is centred on the springing line, its radius set so the two meet at the apex.
                float r = (half * half + rise * rise) / (2f * half);
                float end = Mathf.Atan2(rise, half - r);
                for (int k = 0; k <= perHalf; k++)
                {
                    float th = Mathf.Lerp(Mathf.PI, end, (float)k / perHalf);
                    var up = Vector3.up * (Mathf.Sin(th) * r);
                    pts[k] = a + dir * (r + Mathf.Cos(th) * r) + up;
                    pts[pts.Length - 1 - k] = b - dir * (r + Mathf.Cos(th) * r) + up;
                }
            }
            else
                for (int k = 0; k < pts.Length; k++)
                {
                    float ph = Mathf.PI * k / (pts.Length - 1);
                    pts[k] = a + dir * (half - Mathf.Cos(ph) * half) + Vector3.up * (Mathf.Sin(ph) * rise);
                }
            return pts;
        }

        /// <summary>The curve moved out from the arch's opening by <paramref name="by"/> (in, if negative).</summary>
        static Vector3[] Outward(Vector3[] pts, Vector3 across, float by)
        {
            var mid = (pts[0] + pts[pts.Length - 1]) / 2f;
            var moved = new Vector3[pts.Length];
            for (int k = 0; k < pts.Length; k++)
            {
                var t = (pts[Mathf.Min(k + 1, pts.Length - 1)] - pts[Mathf.Max(k - 1, 0)]).normalized;
                var n = Vector3.Cross(t, across).normalized;
                if (Vector3.Dot(n, pts[k] - mid) < 0f) n = -n;
                moved[k] = pts[k] + n * by;
            }
            return moved;
        }

        /// <summary>Square bars along a curve, <paramref name="width"/> across it and <paramref name="thick"/> deep, lapped at the joints.</summary>
        void ArchRing(Vector3[] pts, Vector3 across, float width, float thick, Color color, Surface surface)
        {
            for (int k = 0; k < pts.Length - 1; k++)
            {
                var d = (pts[k + 1] - pts[k]).normalized;
                float lap = thick * 0.3f;
                Bar(pts[k] - d * lap, pts[k + 1] + d * lap, width, thick, Vector3.Cross(across, d), color, surface);
            }
        }

        /// <summary>A stone rib from a to b (springing points, level) rising by rise in the middle.</summary>
        void Rib(Vector3 a, Vector3 b, float rise, float width, float thick)
        {
            var flat = b - a;
            flat.y = 0f;
            ArchRing(ArchCurve(a, b, rise, 10), Vector3.Cross(Vector3.up, flat.normalized), width, thick, Shade(ChapelStone, 1.05f), Surface.Ashlar);
        }

        /// <summary>The ribs: across each bay and corner to corner, a carved boss where they cross, corbels where they meet a wall, gold stars on the vault.</summary>
        void ChapelVault()
        {
            // Bays of the nave and chancel (x ±7.58), then of each aisle (x 8.42 to 19.55).
            float[] naveZ = { -20.7f, -14f, -7f, 0f, ChancelArchZ, 14f, ChapelNorth };
            float[] aisleZ = { -20.7f, -14f, -7f, 0f, 7f - WallHalf };
            const float nx = ArcadeX - 0.42f, ax0 = ArcadeX + 0.42f, ax1 = 20f - WallHalf - 0.15f;
            var bays = new List<(float x0, float x1, float z0, float z1)>();
            for (int i = 0; i < naveZ.Length - 1; i++) bays.Add((-nx, nx, naveZ[i], naveZ[i + 1]));
            for (int i = 0; i < aisleZ.Length - 1; i++)
            {
                bays.Add((-ax1, -ax0, aisleZ[i], aisleZ[i + 1]));
                bays.Add((ax0, ax1, aisleZ[i], aisleZ[i + 1]));
            }
            var corbels = new HashSet<Vector2Int>();
            foreach (var (x0, x1, z0, z1) in bays)
            {
                Vector3 P(float x, float z) => L(ChapelZone, x, RibSpring, z);
                Rib(P(x0, z0), P(x1, z1), RibRise, 0.24f, 0.26f);
                Rib(P(x1, z0), P(x0, z1), RibRise, 0.24f, 0.26f);
                // Across the bay at its south side, unless that's the chancel arch or the south wall.
                if (z0 > -20f && Mathf.Abs(z0 - ChancelArchZ) > 0.1f) Rib(P(x0, z0), P(x1, z0), RibRise, 0.32f, 0.3f);
                var boss = L(ChapelZone, (x0 + x1) / 2f, WallHeight - 0.12f, (z0 + z1) / 2f);
                DecoPrim(PrimitiveType.Cylinder, boss, new Vector3(0.62f, 0.06f, 0.62f), Quaternion.identity, Shade(OldGold, 0.85f), Surface.Metal);
                DecoPrim(PrimitiveType.Sphere, boss + Vector3.down * 0.08f, new Vector3(0.4f, 0.26f, 0.4f), Quaternion.identity, Shade(ChapelStone, 1.1f), Surface.Ashlar);
                foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
                {
                    // The arcade columns carry their own shafts; on the walls a shaft rises from the floor to each rib,
                    // or where something needs the wall below, a corbel carries it.
                    if (Mathf.Abs(Mathf.Abs(x) - ArcadeX) < 0.6f && System.Array.IndexOf(ArcadeSupports, z) >= 0) continue;
                    if (!corbels.Add(new Vector2Int(Mathf.RoundToInt(x * 10f), Mathf.RoundToInt(z * 10f)))) continue;
                    WallShaft(x, z);
                }
            }
            // A ridge rib down the middle of the nave and chancel.
            Deco(L(ChapelZone, 0f, WallHeight - 0.06f, 0f), new Vector3(0.18f, 0.1f, 41.4f), Shade(ChapelStone, 1.05f), Surface.Ashlar);

            // Gold stars on the blue.
            var rng = new System.Random(3307);
            foreach (var r in ZoneRects(ChapelZone))
            {
                int n = Mathf.RoundToInt(r.width * r.height / 5f);
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector3(Range(rng, r.xMin + 0.4f, r.xMax - 0.4f), WallHeight - 0.006f, Range(rng, r.yMin + 0.4f, r.yMax - 0.4f));
                    float s = Range(rng, 0.1f, 0.2f), yaw = Range(rng, 0f, 90f);
                    foreach (float turn in new[] { 0f, 45f })
                        Deco(p, new Vector3(s, 0.008f, s), Quaternion.Euler(0f, yaw + turn, 0f), OldGold, Surface.Metal);
                }
            }
        }

        /// <summary>
        /// A wall shaft up to the rib springing at (x, z): from the floor, from above the stall panelling, or just a
        /// corbel where a door, window, machine or the organ needs the wall below.
        /// </summary>
        void WallShaft(float x, float z)
        {
            var at = L(ChapelZone, x, 0f, z);
            float bottom = 0f;
            bool Near(Vector3 p) => new Vector2(p.x - at.x, p.z - at.z).sqrMagnitude < 2.3f * 2.3f;
            foreach (var d in DoorPoints) if (Near(new Vector3(d.x, 0f, d.y))) bottom = RibSpring;
            foreach (var spots in new[] { WindowSpots, PerkSpots, WallBuySpots, GeneratorSpots })
                foreach (var s in spots) if (s.Zone == ChapelZone && Near(s.Resolve().face)) bottom = RibSpring;
            if (x > 0f && z > OrganZ0 - 0.5f && z < OrganZ1 + 0.5f) bottom = RibSpring;
            if (x < 0f && Mathf.Abs(x) < ArcadeX && z > 9f && z < 16f) bottom = 2.95f;
            if (bottom < RibSpring)
            {
                if (bottom < 0.1f) Deco(at + Vector3.up * 0.3f, new Vector3(0.5f, 0.6f, 0.5f), PlinthStone, Surface.Masonry);
                float from = Mathf.Max(bottom, 0.6f);
                Rod(at + Vector3.up * from, at + Vector3.up * (RibSpring - 0.1f), 0.26f, PierStone, Surface.Fluted);
                Deco(at + Vector3.up * (RibSpring - 0.08f), new Vector3(0.38f, 0.14f, 0.38f), ChapelStone, Surface.Ashlar);
                Deco(at + Vector3.up * (RibSpring - 0.16f), new Vector3(0.32f, 0.03f, 0.32f), Shade(OldGold, 0.8f), Surface.Metal);
                return;
            }
            var c = at + Vector3.up * (RibSpring - 0.2f);
            Deco(c, new Vector3(0.42f, 0.34f, 0.42f), ChapelStone, Surface.Ashlar);
            Deco(c + Vector3.down * 0.22f, new Vector3(0.26f, 0.12f, 0.26f), Shade(ChapelStone, 0.9f), Surface.Ashlar);
            Deco(c + Vector3.up * 0.18f, new Vector3(0.46f, 0.03f, 0.46f), Shade(OldGold, 0.8f), Surface.Metal);
        }

        /// <summary>The chancel: its rail, the stalls' panelling, the altar dressed for mass, the runner, the sanctuary lamp.</summary>
        void ChapelChancel()
        {
            // A stone nosing on the step, and a kneeler along the rail.
            Deco(L(ChapelZone, 0f, ChancelHeight + 0.004f, ChancelFront + 0.06f), new Vector3(ArcadeX * 2f, 0.01f, 0.12f), Shade(ChapelStone, 1.1f), Surface.Ashlar);
            foreach (float s in new[] { -1f, 1f })
            {
                var c = L(ChapelZone, s * 4.6f, ChancelHeight, RailZ);
                Deco(c + Vector3.up * 0.92f, new Vector3(6f, 0.08f, 0.24f), DarkWood, Surface.Wood);
                Deco(c + Vector3.up * 0.965f, new Vector3(5.96f, 0.012f, 0.16f), Shade(OldGold, 0.85f), Surface.Metal);
                Deco(c + Vector3.up * 0.1f, new Vector3(6f, 0.12f, 0.2f), DarkWood, Surface.Wood);
                for (float x = -2.85f; x <= 2.86f; x += 0.3f)
                    DecoPrim(PrimitiveType.Cylinder, c + new Vector3(x, 0.52f, 0f), new Vector3(0.07f, 0.36f, 0.07f), Quaternion.identity, Shade(DarkWood, 1.15f), Surface.Wood);
                foreach (float x in new[] { -3f, 3f })
                    Deco(c + new Vector3(x, 0.5f, 0f), new Vector3(0.14f, 1f, 0.26f), DarkWood, Surface.Wood);
                Deco(c + new Vector3(0f, -0.19f, -0.48f), new Vector3(6f, 0.1f, 0.3f), Crimson, Surface.Fabric);
            }

            // Carved panelling behind the stalls with a cresting of gilt finials.
            foreach (var (x, z0, z1) in new[] { (-7.7f, 9.3f, 15.7f), (7.7f, 9.3f, 12.5f) })
            {
                float s = Mathf.Sign(x), zc = (z0 + z1) / 2f, len = z1 - z0;
                var face = L(ChapelZone, x - s * 0.04f, 0f, zc);
                Deco(face + Vector3.up * 1.75f, new Vector3(0.08f, 2f, len), Shade(DarkWood, 0.85f), Surface.Wood);
                for (float z = -len / 2f + 0.4f; z < len / 2f - 0.2f; z += 0.8f)
                {
                    Deco(face + new Vector3(-s * 0.05f, 1.9f, z), new Vector3(0.03f, 1.5f, 0.05f), Shade(OldGold, 0.75f), Surface.Metal);
                    foreach (var p in ArchCurve(face + new Vector3(-s * 0.05f, 2.45f, z + 0.05f), face + new Vector3(-s * 0.05f, 2.45f, z + 0.75f), 0.3f, 3))
                        Deco(p, new Vector3(0.03f, 0.05f, 0.05f), Shade(OldGold, 0.75f), Surface.Metal);
                }
                Deco(face + new Vector3(-s * 0.1f, 2.82f, 0f), new Vector3(0.3f, 0.14f, len + 0.1f), DarkWood, Surface.Wood);
                for (float z = -len / 2f + 0.4f; z < len / 2f; z += 0.8f)
                    DecoPrim(PrimitiveType.Sphere, face + new Vector3(-s * 0.1f, 2.95f, z), Vector3.one * 0.1f, Quaternion.identity, OldGold, Surface.Metal);
            }

            // The runner up the chancel to the altar step.
            ChapelRunner(ChancelFront, PredellaFront, ChancelHeight);
            Deco(L(ChapelZone, 0f, ChancelHeight / 2f + 0.006f, ChancelFront - StepRun / 2f), new Vector3(2f, 0.01f, StepRun), Crimson, Surface.Fabric);
            Deco(L(ChapelZone, 0f, PredellaTop + 0.004f, PredellaFront + 0.06f), new Vector3(PredellaHalf * 2f, 0.01f, 0.12f), Shade(OldGold, 0.8f), Surface.Metal);

            // The high altar: a stone mensa over a crimson frontal with gold orphreys and a cross, a white cloth on top.
            var top = L(ChapelZone, 0f, AltarTop, AltarZ);
            Deco(top + Vector3.up * 0.06f, new Vector3(3.25f, 0.12f, 1.25f), Shade(ChapelStone, 1.1f), Surface.Ashlar);
            Deco(top + Vector3.up * 0.126f, new Vector3(3.1f, 0.012f, 1.1f), Linen, Surface.Fabric);
            Deco(top + new Vector3(0f, -0.03f, -0.632f), new Vector3(3.1f, 0.3f, 0.012f), Linen, Surface.Fabric);
            var front = L(ChapelZone, 0f, PredellaTop + 0.42f, AltarZ - 0.56f);
            Deco(front, new Vector3(2.8f, 0.78f, 0.02f), Crimson, Surface.Fabric);
            foreach (float x in new[] { -0.95f, 0.95f })
                Deco(front + new Vector3(x, 0f, -0.012f), new Vector3(0.16f, 0.78f, 0.01f), Shade(OldGold, 0.9f), Surface.Metal);
            Deco(front + new Vector3(0f, -0.38f, -0.012f), new Vector3(2.8f, 0.04f, 0.01f), OldGold, Surface.Metal);
            Deco(front + new Vector3(0f, 0.05f, -0.014f), new Vector3(0.1f, 0.5f, 0.01f), OldGold, Surface.Metal);
            Deco(front + new Vector3(0f, 0.13f, -0.014f), new Vector3(0.36f, 0.1f, 0.01f), OldGold, Surface.Metal);

            // On it: a gilt cross between candlesticks, the chalice and the missal.
            var on = top + Vector3.up * 0.132f;
            Deco(on + new Vector3(0f, 0.05f, 0.3f), new Vector3(0.32f, 0.1f, 0.22f), OldGold, Surface.Metal);
            Deco(on + new Vector3(0f, 0.48f, 0.3f), new Vector3(0.06f, 0.8f, 0.06f), Shade(OldGold, 1.15f), Surface.Metal);
            Deco(on + new Vector3(0f, 0.68f, 0.3f), new Vector3(0.38f, 0.06f, 0.06f), Shade(OldGold, 1.15f), Surface.Metal);
            foreach (var (item, x, z) in new[] { ("CandleStick_Triple", -1.3f, 0.25f), ("CandleStick", -0.6f, 0.3f), ("CandleStick", 0.6f, 0.3f), ("CandleStick_Triple", 1.3f, 0.25f), ("Chalice", -0.35f, -0.2f), ("Book_7", 0.45f, -0.2f) })
            {
                var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                if (model != null) AddModel(model, on + new Vector3(x, 0f, z), Quaternion.Euler(0f, 180f, 0f), false);
            }
            CandleLight(on + new Vector3(-1f, 0.7f, -0.3f));
            CandleLight(on + new Vector3(1f, 0.7f, -0.3f));
            foreach (float x in new[] { -4.2f, 4.2f })
                if (PlaceFree(CandleStand, L(ChapelZone, x, ChancelHeight, 16.6f), Quaternion.identity, true))
                    CandleLight(L(ChapelZone, x, ChancelHeight + 1.45f, 16.6f));

            // The sanctuary lamp, burning red over the chancel.
            var lamp = L(ChapelZone, 0f, 4.5f, 13.9f);
            Rod(L(ChapelZone, 0f, WallHeight, 13.9f), lamp + Vector3.up * 0.25f, 0.02f, Soot, Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, lamp + Vector3.up * 0.22f, new Vector3(0.36f, 0.025f, 0.36f), Quaternion.identity, OldGold, Surface.Metal);
            Glow(PrimitiveType.Sphere, lamp + Vector3.up * 0.1f, Vector3.one * 0.2f, Quaternion.identity, new Color(0.9f, 0.08f, 0.05f));
            DecoPrim(PrimitiveType.Sphere, lamp, new Vector3(0.3f, 0.18f, 0.3f), Quaternion.identity, OldGold, Surface.Metal);
            ChapelLight(lamp, 3.5f, 0.9f, new Color(1f, 0.25f, 0.15f));
        }

        /// <summary>A crimson runner with gold edges down the middle of the floor, z0 to z1, at height y.</summary>
        void ChapelRunner(float z0, float z1, float y)
        {
            float zc = (z0 + z1) / 2f, len = z1 - z0;
            Deco(L(ChapelZone, 0f, y + 0.006f, zc), new Vector3(1.9f, 0.01f, len), Crimson, Surface.Fabric);
            foreach (float x in new[] { -0.82f, 0.82f })
                Deco(L(ChapelZone, x, y + 0.008f, zc), new Vector3(0.07f, 0.012f, len), Shade(OldGold, 0.75f), Surface.Fabric);
        }

        /// <summary>
        /// The reredos behind the altar: a stone screen of three pointed niches between pinnacled posts under gilded
        /// gables, a crimson dossal on its base; a gold cross on a sunburst in the middle, quatrefoils and candles either side.
        /// </summary>
        void ChapelReredos()
        {
            float back = ChapelNorth - 0.08f, posts = ReredosFront + 0.3f;
            Deco(L(ChapelZone, 0f, 4.05f, back), new Vector3(10.4f, 4.1f, 0.16f), ChapelStone, Surface.Ashlar);
            Deco(L(ChapelZone, 0f, ReredosBase + 0.03f, (ReredosFront + ChapelNorth) / 2f), new Vector3(10.5f, 0.08f, ChapelNorth - ReredosFront + 0.06f), Shade(OldGold, 0.85f), Surface.Metal);
            var dossal = L(ChapelZone, 0f, 1.05f, ReredosFront - 0.012f);
            Deco(dossal, new Vector3(4.8f, 1.7f, 0.02f), Crimson, Surface.Fabric);
            foreach (float x in new[] { -2.35f, 2.35f })
                Deco(dossal + new Vector3(x, 0f, -0.01f), new Vector3(0.1f, 1.7f, 0.01f), OldGold, Surface.Metal);
            Deco(dossal + new Vector3(0f, 0.83f, -0.01f), new Vector3(4.8f, 0.06f, 0.01f), OldGold, Surface.Metal);

            // Posts with pinnacles.
            foreach (float x in new[] { -5.05f, -1.75f, 1.75f, 5.05f })
            {
                Deco(L(ChapelZone, x, 4.2f, posts), new Vector3(0.3f, 4.4f, 0.5f), Shade(ChapelStone, 1.05f), Surface.Ashlar);
                Deco(L(ChapelZone, x, 6.55f, posts), new Vector3(0.2f, 0.3f, 0.2f), Quaternion.Euler(0f, 45f, 0f), Shade(ChapelStone, 1.1f), Surface.Ashlar);
                DecoPrim(PrimitiveType.Sphere, L(ChapelZone, x, 6.78f, posts), Vector3.one * 0.16f, Quaternion.identity, OldGold, Surface.Metal);
            }

            // The niches: dark blue backs under pointed heads, gilt rims, and a gilded gable over each.
            foreach (var (x0, x1, spring, rise) in new[] { (-1.6f, 1.6f, 4.7f, 1.2f), (-4.9f, -1.9f, 4.1f, 0.95f), (1.9f, 4.9f, 4.1f, 0.95f) })
            {
                float xc = (x0 + x1) / 2f, z = back - 0.09f;
                Deco(L(ChapelZone, xc, (ReredosBase + 0.1f + spring) / 2f, z), new Vector3(x1 - x0, spring - ReredosBase - 0.1f, 0.02f), IconBlue, Surface.Plain);
                var pts = ArchCurve(L(ChapelZone, x0, spring, z), L(ChapelZone, x1, spring, z), rise, 10);
                for (int k = 0; k < pts.Length - 1; k++)
                {
                    float h = Mathf.Min(pts[k].y, pts[k + 1].y) - spring;
                    if (h > 0.01f) Deco(new Vector3((pts[k].x + pts[k + 1].x) / 2f, spring + h / 2f, z), new Vector3(Mathf.Abs(pts[k + 1].x - pts[k].x) + 0.01f, h, 0.02f), IconBlue, Surface.Plain);
                }
                ArchRing(Outward(pts, Vector3.forward, 0.04f), Vector3.forward, 0.1f, 0.1f, OldGold, Surface.Metal);
                foreach (float x in new[] { x0, x1 })
                    Deco(L(ChapelZone, x, (ReredosBase + spring) / 2f, z - 0.03f), new Vector3(0.1f, spring - ReredosBase, 0.1f), OldGold, Surface.Metal);
                float apex = spring + rise, peak = apex + (x1 - x0 > 3.1f ? 0.95f : 0.75f);
                var g0 = L(ChapelZone, x0 - 0.12f, apex - 0.1f, z - 0.1f);
                var g1 = L(ChapelZone, xc, peak, z - 0.1f);
                var g2 = L(ChapelZone, x1 + 0.12f, apex - 0.1f, z - 0.1f);
                Bar(g0, g1, 0.12f, 0.12f, Vector3.Cross(Vector3.forward, g1 - g0), OldGold, Surface.Metal);
                Bar(g1, g2, 0.12f, 0.12f, Vector3.Cross(Vector3.forward, g2 - g1), OldGold, Surface.Metal);
                DecoPrim(PrimitiveType.Sphere, g1 + Vector3.up * 0.06f, Vector3.one * 0.16f, Quaternion.identity, OldGold, Surface.Metal);
            }

            // The middle niche: a gold cross on a sunburst.
            var glory = L(ChapelZone, 0f, 4.15f, back - 0.11f);
            var facing = Quaternion.Euler(90f, 0f, 0f);
            DecoPrim(PrimitiveType.Cylinder, glory, new Vector3(0.9f, 0.006f, 0.9f), facing, OldGold, Surface.Metal);
            for (int k = 0; k < 16; k++)
            {
                float len = k % 2 == 0 ? 0.8f : 0.5f;
                var turn = Quaternion.Euler(0f, 0f, k * 22.5f);
                Deco(glory + turn * Vector3.up * (0.42f + len / 2f) + Vector3.forward * 0.004f, new Vector3(0.06f, len, 0.01f), turn, Shade(OldGold, 0.95f), Surface.Metal);
            }
            Deco(glory + new Vector3(0f, -0.6f, -0.05f), new Vector3(0.2f, 2.2f, 0.08f), Shade(OldGold, 1.15f), Surface.Metal);
            Deco(glory + new Vector3(0f, 0f, -0.05f), new Vector3(1.2f, 0.2f, 0.08f), Shade(OldGold, 1.15f), Surface.Metal);

            // The side niches: a quatrefoil with a crimson heart, and a candle stand on the ledge below.
            var stand = PropModels.GetPack(CandleStand.Path, PackMaterial);
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                var q = L(ChapelZone, x, 3.55f, back - 0.11f);
                foreach (var (dx, dy) in new[] { (0.17f, 0f), (-0.17f, 0f), (0f, 0.17f), (0f, -0.17f) })
                    DecoPrim(PrimitiveType.Cylinder, q + new Vector3(dx, dy, 0f), new Vector3(0.42f, 0.006f, 0.42f), facing, OldGold, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, q + Vector3.back * 0.012f, new Vector3(0.36f, 0.006f, 0.36f), facing, Crimson, Surface.Fabric);
                if (stand != null)
                {
                    AddModel(stand, L(ChapelZone, x, ReredosBase + 0.07f, (ReredosFront + ChapelNorth) / 2f), Quaternion.identity, false);
                    CandleLight(L(ChapelZone, x, ReredosBase + 1.5f, ReredosFront - 0.2f));
                }
            }
            ChapelLight(L(ChapelZone, 0f, 3.4f, 18.9f), 6f, 1.2f, new Color(1f, 0.8f, 0.5f));
        }

        /// <summary>The pipe organ: its case, a console with the keys, three towers of tin pipes and two flats between them.</summary>
        void ChapelOrgan()
        {
            float zc = (OrganZ0 + OrganZ1) / 2f, len = OrganZ1 - OrganZ0;
            var face = L(ChapelZone, OrganX, 0f, zc);
            foreach (float y in new[] { 0.62f, OrganCase - 0.04f })
                Deco(face + new Vector3(-0.01f, y, 0f), new Vector3(0.04f, 0.06f, len), Shade(OldGold, 0.8f), Surface.Metal);
            for (float z = -len / 2f + 0.7f; z < len / 2f - 0.3f; z += 1.4f)
                Deco(face + new Vector3(-0.01f, 1.6f, z), new Vector3(0.03f, 1.6f, 1.1f), Shade(DarkWood, 1.2f), Surface.Wood);
            // The console and its bench.
            Deco(face + new Vector3(-0.2f, 1.32f, 0f), new Vector3(0.42f, 0.08f, 1.7f), DarkWood, Surface.Wood);
            Deco(face + new Vector3(-0.24f, 1.37f, 0f), new Vector3(0.24f, 0.02f, 1.5f), Linen, Surface.Plain);
            Deco(face + new Vector3(-0.17f, 1.39f, 0f), new Vector3(0.1f, 0.02f, 1.5f), Soot, Surface.Plain);
            Deco(face + new Vector3(-0.85f, 0.5f + 0.24f, 0f), new Vector3(0.36f, 0.08f, 1.4f), DarkWood, Surface.Wood);
            foreach (float z in new[] { -0.62f, 0.62f })
                Deco(face + new Vector3(-0.85f, 0.5f + 0.1f, z), new Vector3(0.3f, 0.2f, 0.06f), DarkWood, Surface.Wood);

            // A dark back panel the pipes stand before.
            Deco(L(ChapelZone, ArcadeX - WallHalf - 0.06f, 4.3f, zc), new Vector3(0.1f, 3.4f, len), Shade(DarkWood, 0.8f), Surface.Wood);
            var tin = new Color(0.74f, 0.74f, 0.72f);
            foreach (var (z, tallest, fall, x) in new[] { (zc - 1.95f, 2.7f, 0.18f, 6f), (zc, 3.5f, 0.2f, 6f), (zc + 1.95f, 2.7f, 0.18f, 6f), (zc - 0.98f, 1.7f, 0.08f, 6.45f), (zc + 0.98f, 1.7f, 0.08f, 6.45f) })
            {
                int n = x < 6.2f ? 7 : 5;
                float top = 0f;
                for (int i = 0; i < n; i++)
                {
                    float off = (i - (n - 1) / 2f) * 0.22f, h = tallest - Mathf.Abs(i - (n - 1) / 2f) * fall;
                    var foot = L(ChapelZone, x, OrganCase, z + off);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (h / 2f), new Vector3(0.18f, h / 2f, 0.18f), Quaternion.identity, tin, Surface.Plain);
                    Deco(foot + new Vector3(-0.09f, 0.42f, 0f), new Vector3(0.02f, 0.08f, 0.09f), Soot, Surface.Plain);
                    DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 0.25f, new Vector3(0.19f, 0.02f, 0.19f), Quaternion.identity, OldGold, Surface.Metal);
                    top = Mathf.Max(top, h);
                }
                // A carved cornice over the tower, and a gilt finial.
                var cap = L(ChapelZone, x + 0.2f, OrganCase + top + 0.12f, z);
                Deco(cap, new Vector3(0.7f, 0.16f, n * 0.22f + 0.2f), DarkWood, Surface.Wood);
                Deco(cap + Vector3.down * 0.09f, new Vector3(0.66f, 0.03f, n * 0.22f + 0.16f), Shade(OldGold, 0.8f), Surface.Metal);
                DecoPrim(PrimitiveType.Sphere, cap + Vector3.up * 0.16f, Vector3.one * 0.16f, Quaternion.identity, OldGold, Surface.Metal);
                foreach (float s in new[] { -1f, 1f })
                    Deco(L(ChapelZone, x + 0.2f, OrganCase + (top + 0.1f) / 2f, z + s * (n * 0.11f + 0.08f)), new Vector3(0.6f, top + 0.1f, 0.08f), DarkWood, Surface.Wood);
            }
        }

        /// <summary>The nave: the runner and the floor medallion, the font by the door, the pulpit, the lectern, banners on the columns.</summary>
        void ChapelNave()
        {
            ChapelRunner(-21f + WallHalf + 0.1f, 1.9f, 0f);
            ChapelRunner(6.1f, ChancelFront - StepRun, 0f);

            // A star inlaid in the floor where the nave meets the chancel.
            var star = L(ChapelZone, 0f, 0f, 4f);
            DecoPrim(PrimitiveType.Cylinder, star + Vector3.up * 0.004f, new Vector3(3.8f, 0.004f, 3.8f), Quaternion.identity, Shade(OldGold, 0.6f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, star + Vector3.up * 0.007f, new Vector3(3.5f, 0.004f, 3.5f), Quaternion.identity, new Color(0.15f, 0.14f, 0.14f), Surface.MosaicGrey);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(0f, k * 45f, 0f);
                Deco(star + Vector3.up * 0.011f + r * new Vector3(0f, 0f, 0.85f), new Vector3(k % 2 == 0 ? 0.5f : 0.3f, 0.005f, k % 2 == 0 ? 1.7f : 1.1f), r, Shade(OldGold, 0.8f), Surface.Metal);
            }
            DecoPrim(PrimitiveType.Cylinder, star + Vector3.up * 0.014f, new Vector3(0.7f, 0.004f, 0.7f), Quaternion.identity, Crimson, Surface.Fabric);

            // The font: a stone bowl on a stem, with water in it.
            var font = L(ChapelZone, FontSpot.x, 0f, FontSpot.y);
            DecoPrim(PrimitiveType.Cylinder, font + Vector3.up * 0.06f, new Vector3(1.3f, 0.06f, 1.3f), Quaternion.identity, PlinthStone, Surface.Masonry);
            DecoPrim(PrimitiveType.Cylinder, font + Vector3.up * 0.4f, new Vector3(0.55f, 0.28f, 0.55f), Quaternion.identity, ChapelStone, Surface.Ashlar);
            DecoPrim(PrimitiveType.Cylinder, font + Vector3.up * 0.83f, new Vector3(1.2f, 0.17f, 1.2f), Quaternion.identity, Shade(ChapelStone, 1.05f), Surface.Ashlar);
            DecoPrim(PrimitiveType.Cylinder, font + Vector3.up * 1.0f, new Vector3(1.24f, 0.015f, 1.24f), Quaternion.identity, OldGold, Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, font + Vector3.up * 1.003f, new Vector3(1.02f, 0.015f, 1.02f), Quaternion.identity, new Color(0.04f, 0.07f, 0.09f), Surface.Metal);

            // The pulpit: a wooden drum on a stone stem, with a sounding board over it on a post from the column.
            var pulpit = L(ChapelZone, PulpitSpot.x, 0f, PulpitSpot.y);
            DecoPrim(PrimitiveType.Cylinder, pulpit + Vector3.up * 0.45f, new Vector3(0.6f, 0.45f, 0.6f), Quaternion.identity, ChapelStone, Surface.Ashlar);
            DecoPrim(PrimitiveType.Cylinder, pulpit + Vector3.up * 1.45f, new Vector3(1.4f, 0.55f, 1.4f), Quaternion.identity, DarkWood, Surface.Wood);
            foreach (float y in new[] { 0.92f, 2f })
                DecoPrim(PrimitiveType.Cylinder, pulpit + Vector3.up * y, new Vector3(1.44f, 0.025f, 1.44f), Quaternion.identity, OldGold, Surface.Metal);
            Deco(pulpit + new Vector3(0.45f, 2.06f, -0.45f), new Vector3(0.5f, 0.04f, 0.36f), Quaternion.Euler(-20f, 135f, 0f), DarkWood, Surface.Wood);
            Bar(pulpit + new Vector3(-0.85f, 1.9f, 0f), pulpit + new Vector3(-0.85f, 3.6f, 0f), 0.14f, 0.14f, Vector3.right, DarkWood, Surface.Wood);
            DecoPrim(PrimitiveType.Cylinder, pulpit + Vector3.up * 3.6f, new Vector3(1.8f, 0.05f, 1.8f), Quaternion.identity, DarkWood, Surface.Wood);
            DecoPrim(PrimitiveType.Cylinder, pulpit + Vector3.up * 3.55f, new Vector3(1.84f, 0.02f, 1.84f), Quaternion.identity, OldGold, Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, pulpit + Vector3.up * 3.75f, Vector3.one * 0.2f, Quaternion.identity, OldGold, Surface.Metal);

            PlaceFree(BookStand, L(ChapelZone, 6.2f, 0f, 5f), Quaternion.Euler(0f, 200f, 0f), true);

            // Banners out from the columns over the nave.
            foreach (float z in new[] { -14f, 0f })
                foreach (float x in new[] { -ArcadeX, ArcadeX })
                {
                    var inward = x < 0f ? Vector3.right : Vector3.left;
                    HangItem(z < -1f ? HangingBanner1 : HangingBanner2, L(ChapelZone, x, 0f, z) + inward * ColumnRadius, Quaternion.LookRotation(inward), 5.3f);
                }
        }

        /// <summary>The aisles: the knights on their tombs, ledger stones, candle stands, and the votive shrine.</summary>
        void ChapelAisles()
        {
            var rng = new System.Random(3303);
            foreach (var t in ChapelTombs)
            {
                var top = L(ChapelZone, t.x, 0.9f, t.y);
                Deco(top + Vector3.up * 0.06f, new Vector3(1.14f, 0.12f, 2.36f), Shade(ChapelStone, 1.05f), Surface.Ashlar);
                // Blind arcading down each long side, and the knight lying on the lid, head to the north.
                foreach (float s in new[] { -1f, 1f })
                    for (int k = -1; k <= 1; k++)
                    {
                        var panel = L(ChapelZone, t.x + s * 0.53f, 0.45f, t.y + k * 0.68f);
                        Deco(panel, new Vector3(0.02f, 0.5f, 0.5f), Shade(PlinthStone, 0.7f), Surface.Masonry);
                        Deco(panel + Vector3.up * 0.27f, new Vector3(0.025f, 0.03f, 0.54f), Shade(OldGold, 0.8f), Surface.Metal);
                    }
                Deco(top + new Vector3(0f, 0.24f, -0.1f), new Vector3(0.5f, 0.24f, 1.5f), Shade(ChapelStone, 1.12f), Surface.Ashlar);
                DecoPrim(PrimitiveType.Sphere, top + new Vector3(0f, 0.28f, 0.78f), new Vector3(0.28f, 0.24f, 0.26f), Quaternion.identity, Shade(ChapelStone, 1.12f), Surface.Ashlar);
                Deco(top + new Vector3(0f, 0.3f, -0.75f), new Vector3(0.36f, 0.3f, 0.18f), Shade(ChapelStone, 1.08f), Surface.Ashlar);
                var sword = PropModels.GetPack(Fantasy + "Sword_Bronze", PackMaterial);
                if (sword != null) AddModel(sword, top + new Vector3(0f, 0.37f, 0f), Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, 90f, 0f), false);
                var candle = PropModels.GetPack(Fantasy + (rng.NextDouble() < 0.5 ? "Candle_1" : "Candle_2"), PackMaterial);
                if (candle != null)
                    foreach (float x in new[] { -0.4f, 0.4f })
                        AddModel(candle, top + new Vector3(x, 0.12f, -1.05f), Quaternion.identity, false);
                if (PlaceFree(CandleStand, L(ChapelZone, t.x, 0f, t.y - 1.75f), Quaternion.identity, true))
                    CandleLight(L(ChapelZone, t.x, 1.45f, t.y - 1.75f));
            }

            // Ledger stones in the aisle floors: dark slabs with a gold border and cross.
            foreach (var p in new[] { new Vector2(-11.2f, -12.2f), new Vector2(-11.2f, -4.4f), new Vector2(11.2f, -12.2f), new Vector2(11.2f, -4.4f) })
            {
                var c = L(ChapelZone, p.x, 0f, p.y);
                Deco(c + Vector3.up * 0.004f, new Vector3(1f, 0.008f, 1.9f), Shade(OldGold, 0.6f), Surface.Metal);
                Deco(c + Vector3.up * 0.007f, new Vector3(0.9f, 0.008f, 1.8f), new Color(0.13f, 0.13f, 0.14f), Surface.Masonry);
                Deco(c + Vector3.up * 0.011f + Vector3.forward * 0.2f, new Vector3(0.06f, 0.004f, 0.8f), Shade(OldGold, 0.8f), Surface.Metal);
                Deco(c + Vector3.up * 0.011f + Vector3.forward * 0.4f, new Vector3(0.4f, 0.004f, 0.06f), Shade(OldGold, 0.8f), Surface.Metal);
            }

            // The votive shrine in the east aisle: blue and gold frontal, a rack of candles, an icon in a gilt frame.
            var shrine = L(ChapelZone, ShrineSpot.x, 1f, ShrineSpot.y);
            Deco(shrine + Vector3.up * 0.04f, new Vector3(2f, 0.08f, 0.95f), Shade(ChapelStone, 1.1f), Surface.Ashlar);
            Deco(shrine + new Vector3(0f, -0.48f, -0.432f), new Vector3(1.7f, 0.8f, 0.02f), IconBlue, Surface.Fabric);
            Deco(shrine + new Vector3(0f, -0.48f, -0.444f), new Vector3(0.12f, 0.8f, 0.01f), OldGold, Surface.Metal);
            for (int tier = 0; tier < 3; tier++)
            {
                float y = 0.1f + tier * 0.13f, z = -0.2f + tier * 0.16f;
                Deco(shrine + new Vector3(0f, y, z), new Vector3(1.6f, 0.04f, 0.14f), Soot, Surface.Metal);
                for (int i = 0; i < 7; i++)
                {
                    if (rng.NextDouble() < 0.2) continue;
                    var at = shrine + new Vector3(-0.66f + i * 0.22f, y + 0.08f, z);
                    Deco(at, new Vector3(0.04f, 0.12f, 0.04f), Linen, Surface.Plain);
                    Glow(at + Vector3.up * 0.085f, new Vector3(0.025f, 0.045f, 0.025f), Quaternion.identity, new Color(1f, 0.62f, 0.22f));
                }
            }
            CandleLight(shrine + new Vector3(0f, 0.8f, -0.4f));
            var icon = L(ChapelZone, ShrineSpot.x, 2.55f, 7f - WallHalf - 0.03f);
            Deco(icon, new Vector3(1.3f, 1.7f, 0.05f), OldGold, Surface.Metal);
            Deco(icon + Vector3.back * 0.03f, new Vector3(1.1f, 1.5f, 0.01f), IconBlue, Surface.Plain);
            var facing = Quaternion.Euler(90f, 0f, 0f);
            DecoPrim(PrimitiveType.Cylinder, icon + new Vector3(0f, 0.32f, -0.04f), new Vector3(0.5f, 0.005f, 0.5f), facing, Shade(OldGold, 1.1f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, icon + new Vector3(0f, 0.32f, -0.046f), new Vector3(0.22f, 0.005f, 0.22f), facing, new Color(0.62f, 0.48f, 0.38f), Surface.Plain);
            Deco(icon + new Vector3(0f, -0.3f, -0.042f), new Vector3(0.56f, 0.84f, 0.01f), Crimson, Surface.Fabric);
            Deco(icon + new Vector3(0f, -0.3f, -0.048f), new Vector3(0.06f, 0.84f, 0.005f), OldGold, Surface.Metal);
        }

        void ChapelLight(Vector3 at, float range, float intensity, Color color)
        {
            var light = new GameObject("Chapel Light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>Chandeliers from the bosses down the nave, the aisles and the chancel, each with its light.</summary>
        void ChapelLights()
        {
            foreach (float z in new[] { -17.35f, -10.5f, -3.5f, 3.4f, 10.4f })
            {
                var at = L(ChapelZone, 0f, WallHeight - 0.25f, z);
                bool hung = z < 8f ? HangChandelier(at, Chandelier) : HangChandelier(at, CandleChandelier);
                ChapelLight(at + Vector3.down * (hung && z < 8f ? 2.2f : 1.6f), 13f, 1.6f, ChapelGlow);
            }
            foreach (float x in new[] { -14f, 14f })
                foreach (float z in new[] { -17.35f, -10.5f, -3.5f, 3.35f })
                {
                    var at = L(ChapelZone, x, WallHeight - 0.25f, z);
                    HangChandelier(at, CandleChandelier);
                    ChapelLight(at + Vector3.down * 1.6f, 9f, 1.25f, ChapelGlow);
                }
        }

        // ---------------------------------------------------------------- stained glass

        /// <summary>The lancets (a wall face at the window's foot, the way into the room, which glass) and the rose over the door.</summary>
        void ChapelWindows()
        {
            var lancets = new List<(Vector3 face, Vector3 inward, float width, float bottom)>();
            void Wall(float line, bool alongZ, Vector3 inward, float width, float bottom, params float[] at)
            {
                foreach (float t in at)
                    lancets.Add((alongZ ? L(ChapelZone, line, 0f, t) : L(ChapelZone, t, 0f, line), inward, width, bottom));
            }
            // One to each bay of the aisles.
            Wall(-20f + WallHalf, true, Vector3.right, 1.15f, 2.95f, -17.35f, -10.5f, -3.5f, 3.35f);
            Wall(20f - WallHalf, true, Vector3.left, 1.15f, 2.95f, -17.35f, -10.5f, -3.5f, 3.35f);
            Wall(-21f + WallHalf, false, Vector3.forward, 1.15f, 2.95f, -17.5f, -10.5f, -5f, 5f, 10.5f, 17.5f);
            Wall(-ArcadeX + WallHalf, true, Vector3.right, 1.15f, 2.95f, 10.5f, 14f, 17.5f);
            Wall(ArcadeX - WallHalf, true, Vector3.left, 1.15f, 2.95f, 10.5f);
            Wall(ChapelNorth, false, Vector3.back, 0.9f, 3f, -6.45f, 6.45f);
            for (int i = 0; i < lancets.Count; i++)
            {
                var (face, inward, width, bottom) = lancets[i];
                Lancet(face, inward, width, bottom, i % 4);
            }
            Rose(L(ChapelZone, 0f, 5.15f, -21f + WallHalf), Vector3.forward, 1.25f);
        }

        const float LancetRise = 0.62f;

        /// <summary>A lancet: glass three times as tall as wide with a pointed head, in a stone surround with a sill.</summary>
        void Lancet(Vector3 face, Vector3 inward, float width, float bottom, int kind)
        {
            float height = width * 3f, spring = bottom + height - LancetRise * width;
            var along = Vector3.Cross(Vector3.up, inward);
            var glass = GlassMaterial(kind);
            var center = face + inward * 0.035f + Vector3.up * (bottom + height / 2f);
            if (glass != null) _decor.Add(PrimitiveType.Quad, center, Quaternion.LookRotation(-inward, Vector3.up), new Vector3(width, height, 1f), glass);
            else Deco(center, new Vector3(width, height, 0.01f), Quaternion.LookRotation(inward), Glass, Surface.Metal);
            var rot = Quaternion.LookRotation(inward);
            foreach (float s in new[] { -1f, 1f })
                Deco(face + along * (s * width / 2f) + inward * 0.08f + Vector3.up * ((bottom + spring) / 2f), new Vector3(0.14f, spring - bottom, 0.16f), rot, ChapelStone, Surface.Ashlar);
            Deco(face + inward * 0.11f + Vector3.up * (bottom - 0.05f), new Vector3(width + 0.36f, 0.1f, 0.22f), rot, Shade(ChapelStone, 1.05f), Surface.Ashlar);
            var a = face + along * (-width / 2f) + inward * 0.08f + Vector3.up * spring;
            var b = face + along * (width / 2f) + inward * 0.08f + Vector3.up * spring;
            ArchRing(ArchCurve(a, b, LancetRise * width, 8), inward, 0.16f, 0.14f, ChapelStone, Surface.Ashlar);
        }

        /// <summary>A rose window: round glass in a ring of voussoirs.</summary>
        void Rose(Vector3 center, Vector3 inward, float radius)
        {
            var glass = GlassMaterial(4);
            var at = center + inward * 0.035f;
            if (glass != null) _decor.Add(PrimitiveType.Quad, at, Quaternion.LookRotation(-inward, Vector3.up), new Vector3(radius * 2f, radius * 2f, 1f), glass);
            var along = Vector3.Cross(Vector3.up, inward);
            var ring = new Vector3[33];
            for (int k = 0; k < ring.Length; k++)
            {
                float a = k * Mathf.PI * 2f / (ring.Length - 1);
                ring[k] = center + inward * 0.09f + (along * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * radius;
            }
            ArchRing(ring, inward, 0.18f, 0.2f, ChapelStone, Surface.Ashlar);
            ArchRing(Outward(ring, inward, -0.12f), inward, 0.22f, 0.04f, Shade(OldGold, 0.8f), Surface.Metal);
        }

        static Material s_GlassBase;
        static bool s_GlassChecked;
        static readonly Dictionary<int, Material> s_Glass = new Dictionary<int, Material>();

        /// <summary>Stained glass: kinds 0-3 are lancets with different medallions, 4 the rose. Null if the shader can't run.</summary>
        static Material GlassMaterial(int kind)
        {
            if (s_Glass.TryGetValue(kind, out var cached) && cached != null) return cached;
            if (!s_GlassChecked)
            {
                s_GlassChecked = true;
                var m = Resources.Load<Material>("Materials/StainedGlass");
                if (m != null && m.shader != null && m.shader.isSupported) s_GlassBase = m;
            }
            if (s_GlassBase == null) return null;
            var mat = new Material(s_GlassBase) { name = "Stained Glass " + kind, mainTexture = kind < 4 ? LancetTexture(kind) : RoseTexture() };
            mat.SetFloat("_Intensity", 1f);
            s_Glass[kind] = mat;
            return mat;
        }

        static readonly Color GlassBlue = new Color(0.07f, 0.15f, 0.52f), GlassRuby = new Color(0.62f, 0.05f, 0.07f), GlassGold = new Color(0.86f, 0.6f, 0.12f);
        static readonly Color GlassPale = new Color(0.95f, 0.88f, 0.62f), GlassLead = new Color(0.03f, 0.03f, 0.035f);
        static readonly Color[] MedallionGlass = { new Color(0.62f, 0.05f, 0.07f), new Color(0.06f, 0.42f, 0.18f), new Color(0.85f, 0.42f, 0.06f), new Color(0.36f, 0.1f, 0.55f) };
        const float LeadWidth = 0.011f;

        static float GlassGrain(int x, int y)
        {
            unchecked
            {
                uint h = (uint)((x >> 1) * 374761393 + (y >> 1) * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return 0.86f + ((h ^ (h >> 16)) & 0xFFFF) / 65535f * 0.26f;
            }
        }

        /// <summary>
        /// A lancet's glass, a unit wide and three tall: a ruby and gold border, a field of blue quarries, three
        /// medallions up the middle with a pale quatrefoil in each, and a gold trefoil in the head; leaded throughout.
        /// </summary>
        static Texture2D LancetTexture(int kind)
        {
            const int W = 64, H = 192;
            const float border = 0.09f;
            float spring = 3f - LancetRise, r = 0.25f + LancetRise * LancetRise, cx = r - 0.5f;
            var medal = MedallionGlass[kind % MedallionGlass.Length];
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H * 3f;
                    var p = new Vector2(u, v);
                    // How far in from the outline (negative outside it).
                    float edge = Mathf.Min(0.5f - Mathf.Abs(u), v);
                    if (v > spring) edge = Mathf.Min(edge, r - Mathf.Max(Vector2.Distance(p, new Vector2(cx, spring)), Vector2.Distance(p, new Vector2(-cx, spring))));
                    if (edge < 0f) { px[y * W + x] = Color.clear; continue; }
                    Color c;
                    bool lead = edge < LeadWidth;
                    if (edge < border)
                    {
                        float run = v > spring ? Mathf.Atan2(v - spring, u) * 0.6f : v;
                        int block = Mathf.FloorToInt(run / 0.2f);
                        c = block % 2 == 0 ? GlassRuby : GlassGold;
                        lead |= Mathf.Abs(edge - border) < LeadWidth || Mathf.Repeat(run, 0.2f) < LeadWidth * 1.5f;
                    }
                    else
                    {
                        float qa = (u + v) / 0.2f, qb = (v - u) / 0.2f;
                        float da = Mathf.Abs(Mathf.Repeat(qa + 0.5f, 1f) - 0.5f) * 0.2f / 1.414f, db = Mathf.Abs(Mathf.Repeat(qb + 0.5f, 1f) - 0.5f) * 0.2f / 1.414f;
                        lead = da < LeadWidth * 0.8f || db < LeadWidth * 0.8f;
                        int cell = Mathf.FloorToInt(qa) * 7 + Mathf.FloorToInt(qb) * 13;
                        c = GlassBlue * (0.85f + Mathf.Repeat(cell * 0.37f, 0.3f));
                        foreach (float my in new[] { 0.62f, 1.38f, 2.14f })
                        {
                            float d = Vector2.Distance(p, new Vector2(0f, my));
                            if (d > 0.3f + LeadWidth) continue;
                            lead = Mathf.Abs(d - 0.3f) < LeadWidth || Mathf.Abs(d - 0.24f) < LeadWidth;
                            c = d > 0.24f ? GlassGold : medal;
                            if (d < 0.24f)
                                foreach (var o in new[] { new Vector2(0.09f, 0f), new Vector2(-0.09f, 0f), new Vector2(0f, 0.09f), new Vector2(0f, -0.09f) })
                                {
                                    float dl = Vector2.Distance(p, new Vector2(o.x, my + o.y));
                                    if (dl < 0.075f) c = GlassPale;
                                    if (Mathf.Abs(dl - 0.075f) < LeadWidth) lead = true;
                                }
                            if (Vector2.Distance(p, new Vector2(0f, my)) < 0.035f) c = medal;
                        }
                        foreach (var t in new[] { new Vector2(0f, 2.8f), new Vector2(0.075f, 2.67f), new Vector2(-0.075f, 2.67f) })
                        {
                            float d = Vector2.Distance(p, t);
                            if (d < 0.065f) c = GlassGold;
                            if (Mathf.Abs(d - 0.065f) < LeadWidth) lead = true;
                        }
                    }
                    c = lead ? GlassLead : c * GlassGrain(x, y);
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            return GlassTexture("Lancet " + kind, W, H, px);
        }

        /// <summary>A rose: a gold heart, twelve ruby and blue petals, and a ring of amber and green roundels on blue.</summary>
        static Texture2D RoseTexture()
        {
            const int S = 128;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f, d = Mathf.Sqrt(u * u + v * v);
                    if (d > 0.5f) { px[y * S + x] = Color.clear; continue; }
                    float turn = Mathf.Repeat(Mathf.Atan2(v, u) / (Mathf.PI * 2f), 1f);
                    Color c;
                    bool lead = d > 0.5f - LeadWidth;
                    if (d < 0.1f)
                    {
                        c = d < 0.045f ? GlassRuby : GlassGold;
                        lead |= Mathf.Abs(d - 0.045f) < LeadWidth;
                    }
                    else if (d < 0.34f)
                    {
                        float s = turn * 12f;
                        int petal = Mathf.FloorToInt(s);
                        c = petal % 2 == 0 ? GlassRuby : GlassBlue;
                        lead |= Mathf.Abs(d - 0.1f) < LeadWidth || Mathf.Abs(d - 0.34f) < LeadWidth;
                        lead |= Mathf.Min(s - petal, petal + 1 - s) * Mathf.PI * 2f * d / 12f < LeadWidth;
                        float mid = (petal + 0.5f) / 12f * Mathf.PI * 2f;
                        float dj = Vector2.Distance(new Vector2(u, v), new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * 0.24f);
                        if (dj < 0.035f) c = GlassPale;
                        lead |= Mathf.Abs(dj - 0.035f) < LeadWidth;
                    }
                    else
                    {
                        float s = turn * 24f;
                        int cell = Mathf.FloorToInt(s);
                        float mid = (cell + 0.5f) / 24f * Mathf.PI * 2f;
                        float dr = Vector2.Distance(new Vector2(u, v), new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * 0.42f);
                        c = dr < 0.045f ? (cell % 2 == 0 ? GlassGold : MedallionGlass[1]) : GlassBlue;
                        lead |= Mathf.Abs(dr - 0.045f) < LeadWidth;
                    }
                    c = lead ? GlassLead : c * GlassGrain(x, y);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            return GlassTexture("Rose", S, S, px);
        }

        static Texture2D GlassTexture(string name, int w, int h, Color[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
