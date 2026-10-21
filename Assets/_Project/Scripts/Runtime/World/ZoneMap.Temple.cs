using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Sunken Temple, the drowned shrine of a forgotten serpent god.
    ///
    /// The hall: a raised stone causeway runs its length between two black pools, a glowing rune circle on it under
    /// an oculus where the vault has fallen in and moonlight comes down through hanging roots. Two rows of drum
    /// columns wound with bronze serpents carry the architraves; two of them have broken, one toppled across the
    /// east pool, and the ceiling's rubble lies in the west pool beside a great stone head. Hooded guardians with
    /// bronze swords stand against the walls, under a serpent frieze, with skull niches and a tide line below.
    ///
    /// The sanctum (the north wing): a stepped dais of three tiers, the coiled serpent idol on top with its hood
    /// spread before a rune halo, offering tables and braziers on the tiers, a looted sarcophagus in the west wing
    /// and the temple's hoard in the east.
    ///
    /// The whole room is dressed here: it gets none of the generic room sets, wall clutter, lamps or roof members
    /// (see <see cref="HandDressed"/>), and draws its own ceiling round the oculus.
    /// </summary>
    public partial class ZoneMap
    {
        static readonly Color TempleStone = new Color(0.38f, 0.4f, 0.37f);
        static readonly Color DrownedStone = new Color(0.25f, 0.28f, 0.26f);
        static readonly Color TempleTeal = new Color(0.12f, 0.32f, 0.33f);
        static readonly Color Verdigris = new Color(0.24f, 0.45f, 0.39f);
        static readonly Color Moss = new Color(0.17f, 0.25f, 0.1f);
        static readonly Color IdolJade = new Color(0.2f, 0.33f, 0.28f);
        static readonly Color RuneGlow = new Color(0.32f, 1f, 0.86f);
        static readonly Color MoonLight = new Color(0.62f, 0.75f, 1f);
        static readonly Color BoneWhite = new Color(0.72f, 0.68f, 0.58f);

        /// <summary>Zones dressed entirely by hand, which the generic dressing passes leave alone.</summary>
        static bool HandDressed(int zone) => zone == ChapelZone || zone == Temple || zone == GreenhouseZone || zone == DepotZone || zone == LoadingDockZone;

        const float CausewayHalf = 1.6f, CausewayTop = 0.3f, CausewaySouth = -15f, CausewayNorth = 19f;
        const float PoolOuter = 6.2f, PoolSouth = -15f, PoolNorth = 17f, KerbTop = 0.2f, WaterLevel = 0.12f;
        const float ColonnadeX = 7.5f, DrumRadius = 0.55f, CapitalTop = 6.1f;
        static readonly float[] TempleColumnZ = { -12f, -5f, 2f, 9f, 16f };
        /// <summary>The broken columns: (x, z, height of what still stands).</summary>
        static readonly (float x, float z, float height)[] BrokenColumns = { (-ColonnadeX, 9f, 2.6f), (ColonnadeX, -5f, 1.4f) };
        const float OculusX0 = -4f, OculusX1 = 4f, OculusZ0 = -4f, OculusZ1 = 6f;
        /// <summary>The dais tiers in the north wing: x half-width, front z and top.</summary>
        static readonly (float half, float front, float top)[] Tiers = { (7f, 24f, 0.5f), (5f, 26.5f, 1f), (3f, 29f, 1.5f) };
        static readonly Vector3 IdolBase = new Vector3(0f, 1.5f, 31.9f);
        static readonly (float z, bool west)[] GuardianSpots = { (-1.5f, true), (5.5f, true), (-8.5f, false), (12.5f, false) };
        static readonly Vector3 GiantHead = new Vector3(-4f, 0f, -10f);
        static readonly Vector3 SarcophagusSpot = new Vector3(-9.6f, 0f, 29f);

        static float BrokenHeight(float x, float z)
        {
            foreach (var b in BrokenColumns)
                if (Mathf.Abs(b.x - x) < 0.1f && Mathf.Abs(b.z - z) < 0.1f) return b.height;
            return WallHeight;
        }

        // ---------------------------------------------------------------- solid (before the bake)

        void BuildTemple()
        {
            GameObject Hidden(string name, Vector3 local, Vector3 size, Quaternion rot)
            {
                var box = Greybox.Box(name, Root, L(Temple, local.x, local.y, local.z), size, DrownedStone, surface: Surface.Masonry);
                box.transform.localRotation = rot;
                box.GetComponent<Renderer>().enabled = false;
                return box;
            }

            // The causeway, and a low kerb round each pool.
            Block("Causeway", Temple, -CausewayHalf, CausewaySouth, CausewayHalf, CausewayNorth, CausewayTop, DrownedStone, Surface.Ashlar);
            foreach (float s in new[] { -1f, 1f })
            {
                float inner = s * CausewayHalf, outer = s * (PoolOuter + 0.3f);
                Block("Pool Kerb", Temple, Mathf.Min(s * PoolOuter, outer), PoolSouth - 0.3f, Mathf.Max(s * PoolOuter, outer), PoolNorth + 0.3f, KerbTop, TempleStone, Surface.Ashlar);
                Block("Pool Kerb", Temple, Mathf.Min(inner, s * PoolOuter), PoolSouth - 0.3f, Mathf.Max(inner, s * PoolOuter), PoolSouth, KerbTop, TempleStone, Surface.Ashlar);
                Block("Pool Kerb", Temple, Mathf.Min(inner, s * PoolOuter), PoolNorth, Mathf.Max(inner, s * PoolOuter), PoolNorth + 0.3f, KerbTop, TempleStone, Surface.Ashlar);
            }

            // The columns, drawn in the dressing: a box each to stand behind, as high as what's left of it.
            foreach (float x in new[] { -ColonnadeX, ColonnadeX })
                foreach (float z in TempleColumnZ)
                {
                    float h = BrokenHeight(x, z);
                    Hidden("Column", new Vector3(x, h / 2f, z), new Vector3(1.2f, h, 1.2f), Quaternion.identity);
                }

            // The fallen column across the east pool, its drums in the west pool, the rubble under the oculus and the
            // great stone head: cover out in the water.
            var (fallA, fallB) = FallenShaft;
            var fall = fallB - fallA;
            Hidden("Fallen Column", (fallA + fallB) / 2f + Vector3.up * DrumRadius, new Vector3(DrumRadius * 2f, DrumRadius * 2f, fall.magnitude), Quaternion.LookRotation(fall));
            foreach (var (at, yaw, upright) in FallenDrums)
                Hidden("Fallen Drum", at + Vector3.up * (upright ? 0.4f : DrumRadius), upright ? new Vector3(1.1f, 0.8f, 1.1f) : new Vector3(1.1f, 1.1f, 0.9f), Quaternion.Euler(0f, yaw, 0f));
            Hidden("Rubble", new Vector3(-4.1f, 0.25f, 1f), new Vector3(3.2f, 0.5f, 3.6f), Quaternion.identity);
            Hidden("Stone Head", GiantHead + Vector3.up * 0.8f, new Vector3(1.6f, 1.6f, 1.6f), Quaternion.Euler(0f, 20f, 0f));

            // The guardians against the walls.
            foreach (var (z, west) in GuardianSpots)
                Hidden("Guardian", new Vector3(west ? -10.75f : 10.75f, 1.9f, z), new Vector3(1.4f, 3.8f, 1.4f), Quaternion.identity);

            // Braziers on pedestals at each end of the causeway.
            foreach (var p in HallBraziers)
                Greybox.Box("Brazier Pedestal", Root, L(Temple, p.x, 0.3f, p.y), new Vector3(0.9f, 0.6f, 0.9f), TempleStone, surface: Surface.Ashlar);

            // The sanctum: three tiers, a step up to the first all along its front and stairs up the middle to the
            // others, the idol on top, offering tables, the sarcophagus and the hoard.
            foreach (var (half, front, top) in Tiers)
                Block("Dais", Temple, -half, front, half, 34f, top, DrownedStone, Surface.Ashlar);
            Greybox.Box("Dais Step", Root, L(Temple, 0f, 0.125f, Tiers[0].front - StepRun / 2f), new Vector3(Tiers[0].half * 2f, 0.25f, StepRun), TempleStone, surface: Surface.Ashlar);
            for (int i = 1; i < Tiers.Length; i++)
            {
                float h = Tiers[i - 1].top + 0.25f, w = i == 1 ? 4f : 3f;
                Greybox.Box("Dais Step", Root, L(Temple, 0f, h / 2f, Tiers[i].front - StepRun / 2f), new Vector3(w, h, StepRun), TempleStone, surface: Surface.Ashlar);
            }
            Hidden("Serpent Idol", IdolBase + new Vector3(0f, 2.35f, 0f), new Vector3(4f, 4.7f, 3.6f), Quaternion.identity);
            foreach (float s in new[] { -1f, 1f })
                Greybox.Box("Offering Table", Root, L(Temple, s * 3.4f, Tiers[1].top + 0.45f, 27.6f), new Vector3(1.6f, 0.9f, 0.8f), TempleStone, surface: Surface.Ashlar);
            Hidden("Sarcophagus", SarcophagusSpot + Vector3.up * 0.55f, new Vector3(1.4f, 1.1f, 2.5f), Quaternion.identity);
            Hidden("Hoard", new Vector3(16.6f, 0.3f, 31.6f), new Vector3(3f, 0.6f, 2.2f), Quaternion.Euler(0f, -8f, 0f));
        }

        static readonly (Vector3 a, Vector3 b) FallenShaft = (new Vector3(5.9f, 0f, -5.6f), new Vector3(2.7f, 0f, -8.2f));
        static readonly (Vector3 at, float yaw, bool upright)[] FallenDrums =
        {
            (new Vector3(-4.6f, 0f, 10.6f), 64f, false), (new Vector3(-3.3f, 0f, 12.8f), 0f, true), (new Vector3(4.4f, 0f, -2.2f), 0f, true),
        };
        static readonly Vector2[] HallBraziers = { new Vector2(-2.8f, -16.3f), new Vector2(2.8f, -16.3f), new Vector2(-2.6f, 18.4f), new Vector2(2.6f, 18.4f) };

        // ---------------------------------------------------------------- dressing

        void TempleMakeover()
        {
            var rng = new System.Random(1772);
            TempleWater(rng);
            TempleCauseway();
            TempleColonnade(rng);
            TempleRuins(rng);
            TempleCeiling(rng);
            TempleWalls(rng);
            foreach (var (z, west) in GuardianSpots) Guardian(L(Temple, west ? -10.75f : 10.75f, 0f, z), west ? Vector3.right : Vector3.left, !west && z < 0f);
            TempleDais();
            SerpentIdol();
            TempleWings(rng);
            TempleLights();
        }

        // ---------------------------------------------------------------- the pools and the causeway

        void TempleWater(System.Random rng)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s * CausewayHalf, x1 = s * PoolOuter, cx = (x0 + x1) / 2f, w = Mathf.Abs(x1 - x0);
                float cz = (PoolSouth + PoolNorth) / 2f, len = PoolNorth - PoolSouth;
                // A black bottom that takes the light like wet stone, slime on it, and the water's surface over it.
                Deco(L(Temple, cx, 0.006f, cz), new Vector3(w, 0.01f, len), new Color(0.02f, 0.045f, 0.045f), Surface.Metal);
                for (int i = 0; i < 14; i++)
                    Splat(L(Temple, Range(rng, Mathf.Min(x0, x1) + 0.5f, Mathf.Max(x0, x1) - 0.5f), 0f, Range(rng, PoolSouth + 0.6f, PoolNorth - 0.6f)),
                        Range(rng, 0.8f, 2.2f), Range(rng, 0.6f, 1.8f), Range(rng, 0f, 180f), 0.013f, new Color(0.08f, 0.14f, 0.06f), Surface.Grass);
                Glow(L(Temple, cx, WaterLevel, cz), new Vector3(w, 0.004f, len), Quaternion.identity, new Color(0.05f, 0.19f, 0.18f, 0.55f));
                // Moulded tops on the kerbs, wet and green at the waterline.
                Deco(L(Temple, s * (PoolOuter + 0.15f), KerbTop + 0.015f, cz), new Vector3(0.42f, 0.03f, len + 0.62f), Shade(TempleStone, 1.1f), Surface.Ashlar);
                Deco(L(Temple, s * (PoolOuter - 0.005f), WaterLevel - 0.02f, cz), new Vector3(0.012f, 0.06f, len), Moss, Surface.Grass);
                Deco(L(Temple, s * (CausewayHalf + 0.005f), WaterLevel - 0.02f, cz), new Vector3(0.012f, 0.06f, len), Moss, Surface.Grass);

                // Lily pads and reeds; a few flowers.
                for (int i = 0; i < 22; i++)
                {
                    var p = L(Temple, Range(rng, Mathf.Min(x0, x1) + 0.3f, Mathf.Max(x0, x1) - 0.3f), WaterLevel + 0.004f, Range(rng, PoolSouth + 0.4f, PoolNorth - 0.4f));
                    if (Mathf.Abs(p.z - L(Temple, 0f, 0f, 1f).z) < 4f && s < 0f) continue;
                    float r = Range(rng, 0.3f, 0.6f);
                    DecoPrim(PrimitiveType.Cylinder, p, new Vector3(r, 0.004f, r * 0.9f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Color(0.14f, 0.27f, 0.1f), Surface.Grass);
                    if (rng.NextDouble() < 0.18)
                        DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 0.04f, new Vector3(0.12f, 0.07f, 0.12f), Quaternion.identity, new Color(0.75f, 0.62f, 0.68f), Surface.Plain);
                }
                for (float z = PoolSouth + 0.8f; z < PoolNorth - 0.5f; z += Range(rng, 1.4f, 3.6f))
                {
                    var foot = L(Temple, s * (PoolOuter - Range(rng, 0.25f, 0.7f)), 0f, z);
                    int n = rng.Next(5, 10);
                    for (int k = 0; k < n; k++)
                    {
                        var b = foot + new Vector3(Range(rng, -0.25f, 0.25f), 0f, Range(rng, -0.3f, 0.3f));
                        var tip = b + new Vector3(Range(rng, -0.2f, 0.2f), Range(rng, 0.7f, 1.4f), Range(rng, -0.2f, 0.2f));
                        Rod(b, tip, Range(rng, 0.018f, 0.03f), rng.NextDouble() < 0.3 ? new Color(0.34f, 0.32f, 0.16f) : new Color(0.2f, 0.3f, 0.12f), Surface.Grass);
                    }
                }
            }
        }

        void TempleCauseway()
        {
            float cz = (CausewaySouth + CausewayNorth) / 2f, len = CausewayNorth - CausewaySouth;
            // Edge stones with a bronze line inside them, and the joints of the paving.
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(L(Temple, s * (CausewayHalf - 0.15f), CausewayTop + 0.01f, cz), new Vector3(0.32f, 0.02f, len), Shade(TempleStone, 1.05f), Surface.Ashlar);
                Deco(L(Temple, s * (CausewayHalf - 0.33f), CausewayTop + 0.012f, cz), new Vector3(0.04f, 0.02f, len), Verdigris, Surface.Metal);
            }
            for (float z = CausewaySouth + 1.4f; z < CausewayNorth - 0.4f; z += 1.4f)
                Deco(L(Temple, 0f, CausewayTop + 0.003f, z), new Vector3(CausewayHalf * 2f - 0.7f, 0.006f, 0.03f), Shade(DrownedStone, 0.5f), Surface.Ashlar);

            // The rune circle under the oculus: a dark inlay ringed in light, runes between the rings, an eye at its heart.
            var c = L(Temple, 0f, CausewayTop + 0.004f, 1f);
            DecoPrim(PrimitiveType.Cylinder, c, new Vector3(2.9f, 0.004f, 2.9f), Quaternion.identity, new Color(0.06f, 0.1f, 0.1f), Surface.Ashlar);
            Ring(c + Vector3.up * 0.004f, 1.36f, 0.06f, 36, RuneGlow);
            Ring(c + Vector3.up * 0.004f, 0.88f, 0.04f, 28, RuneGlow);
            for (int k = 0; k < 12; k++)
            {
                var q = Quaternion.Euler(0f, k * 30f, 0f);
                var at = c + q * Vector3.forward * 1.12f + Vector3.up * 0.005f;
                Glow(at, new Vector3(0.04f, 0.004f, 0.24f), q * Quaternion.Euler(0f, k % 2 == 0 ? 0f : 35f, 0f), RuneGlow);
                Glow(at + q * Vector3.right * 0.06f, new Vector3(0.12f, 0.004f, 0.035f), q, RuneGlow);
            }
            Glow(PrimitiveType.Sphere, c + Vector3.up * 0.004f, new Vector3(0.5f, 0.006f, 0.22f), Quaternion.identity, RuneGlow);
            DecoPrim(PrimitiveType.Sphere, c + Vector3.up * 0.007f, new Vector3(0.08f, 0.006f, 0.2f), Quaternion.identity, Soot, Surface.Plain);
        }

        /// <summary>A flat ring of glowing bars, lying level.</summary>
        void Ring(Vector3 center, float radius, float width, int segments, Color color)
        {
            float chord = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int k = 0; k < segments; k++)
            {
                var q = Quaternion.Euler(0f, (k + 0.5f) * 360f / segments, 0f);
                Glow(center + q * Vector3.forward * radius, new Vector3(chord, 0.004f, width), q, color);
            }
        }

        // ---------------------------------------------------------------- columns

        void TempleColonnade(System.Random rng)
        {
            foreach (float x in new[] { -ColonnadeX, ColonnadeX })
                foreach (float z in TempleColumnZ)
                    TempleColumn(L(Temple, x, 0f, z), BrokenHeight(x, z), rng);

            // Architraves along the columns: carved beams with a bronze serpent course; the east one has broken
            // over its fallen column.
            foreach (float x in new[] { -ColonnadeX, ColonnadeX })
            {
                var runs = x > 0f ? new[] { (-12.7f, -8.4f), (-1.6f, 16.7f) } : new[] { (-12.7f, 16.7f) };
                foreach (var (a, b) in runs)
                {
                    var mid = L(Temple, x, 6.55f, (a + b) / 2f);
                    Deco(mid, new Vector3(1.3f, 0.9f, b - a), TempleStone, Surface.Ashlar);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Deco(mid + new Vector3(s * 0.66f, -0.38f, 0f), new Vector3(0.04f, 0.06f, b - a), Verdigris, Surface.Metal);
                        Deco(mid + new Vector3(s * 0.66f, 0.1f, 0f), new Vector3(0.03f, 0.36f, b - a), TempleTeal, Surface.Tile);
                        for (float t = a + 0.35f; t < b - 0.3f; t += 0.7f)
                            Deco(L(Temple, x + s * 0.68f, 6.65f, t), new Vector3(0.02f, 0.14f, 0.14f), Quaternion.Euler(45f, 0f, 0f), Shade(OldGold, 0.85f), Surface.Metal);
                    }
                }
                if (x > 0f)
                    foreach (float end in new[] { -8.4f, -1.6f })
                        RaggedEnd(L(Temple, x, 6.55f, end), end < -5f ? Vector3.forward : Vector3.back, 1.3f, 0.9f, rng);
            }
        }

        /// <summary>
        /// A column of stone drums on a plinth, a bronze serpent wound up it, a flared capital and abacus; if it has
        /// broken, just the stump with a jagged top.
        /// </summary>
        void TempleColumn(Vector3 foot, float height, System.Random rng)
        {
            Deco(foot + Vector3.up * 0.25f, new Vector3(1.5f, 0.5f, 1.5f), DrownedStone, Surface.Masonry);
            Deco(foot + Vector3.up * 0.06f, new Vector3(1.56f, 0.12f, 1.56f), Shade(Moss, 1.2f), Surface.Grass);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 0.58f, new Vector3(1.32f, 0.08f, 1.32f), Quaternion.identity, TempleStone, Surface.Ashlar);
            float top = Mathf.Min(height, 5.6f), y = 0.66f;
            int drum = 0;
            while (y < top - 0.05f)
            {
                float h = Mathf.Min(Range(rng, 0.95f, 1.2f), top - y);
                var shade = Shade(TempleStone, Range(rng, 0.88f, 1.05f));
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (y + h / 2f) + new Vector3(Range(rng, -0.02f, 0.02f), 0f, Range(rng, -0.02f, 0.02f)),
                    new Vector3(DrumRadius * 2f, h / 2f, DrumRadius * 2f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), shade, Surface.Fluted);
                // A dark joint between drums, and the tide stain on the lowest.
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * y, new Vector3(DrumRadius * 2f - 0.04f, 0.012f, DrumRadius * 2f - 0.04f), Quaternion.identity, Soot, Surface.Plain);
                if (drum++ == 0) DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * (y + 0.2f), new Vector3(DrumRadius * 2f + 0.01f, 0.2f, DrumRadius * 2f + 0.01f), Quaternion.identity, Shade(DrownedStone, 0.8f), Surface.Masonry);
                y += h;
            }

            // The serpent: a bronze body coiling up the shaft, its head at the top looking down the hall.
            float coilTop = Mathf.Min(top - 0.2f, 5.3f), phase = Range(rng, 0f, 360f);
            // Two turns up a whole column; a stump keeps what its height allows.
            float turns = 720f * (coilTop - 0.8f) / 4.5f;
            Vector3 Coil(float u) => foot + Quaternion.Euler(0f, phase + u * turns, 0f) * Vector3.forward * (DrumRadius + 0.05f) + Vector3.up * Mathf.Lerp(0.8f, coilTop, u);
            int steps = Mathf.Max(3, Mathf.RoundToInt(turns / 12f));
            for (int k = 0; k < steps; k++)
            {
                float u0 = (float)k / steps, u1 = (float)(k + 1) / steps;
                float d = Mathf.Lerp(0.07f, 0.13f, Mathf.Sin(u0 * Mathf.PI));
                Rod(Coil(u0), Coil(u1), d, Verdigris, Surface.Metal);
                DecoPrim(PrimitiveType.Sphere, Coil(u1), new Vector3(d, d, d), Quaternion.identity, Verdigris, Surface.Metal);
            }
            if (height >= WallHeight)
            {
                var neck = Coil(1f);
                var outward = (neck - foot);
                outward.y = 0f;
                outward.Normalize();
                DecoPrim(PrimitiveType.Sphere, neck + outward * 0.1f + Vector3.up * 0.05f, new Vector3(0.18f, 0.13f, 0.28f), Quaternion.LookRotation(outward), Verdigris, Surface.Metal);
                Glow(PrimitiveType.Sphere, neck + outward * 0.19f + Vector3.up * 0.09f, new Vector3(0.05f, 0.03f, 0.05f), Quaternion.identity, RuneGlow);

                // The capital: a flared bell with a bronze necking, and a square abacus under the architrave.
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 5.62f, new Vector3(1.18f, 0.04f, 1.18f), Quaternion.identity, Verdigris, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 5.75f, new Vector3(1.3f, 0.1f, 1.3f), Quaternion.identity, TempleStone, Surface.Ashlar);
                DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 5.9f, new Vector3(1.5f, 0.06f, 1.5f), Quaternion.identity, Shade(TempleStone, 1.05f), Surface.Ashlar);
                Deco(foot + Vector3.up * 6.02f, new Vector3(1.55f, 0.18f, 1.55f), Shade(TempleStone, 0.95f), Surface.Ashlar);
                return;
            }
            // Broken: a jagged top of leaning shards.
            for (int k = 0; k < 5; k++)
            {
                var q = Quaternion.Euler(Range(rng, -18f, 18f), k * 72f + Range(rng, -20f, 20f), Range(rng, -18f, 18f));
                var at = foot + Quaternion.Euler(0f, k * 72f, 0f) * Vector3.forward * Range(rng, 0.1f, 0.3f) + Vector3.up * (top + Range(rng, -0.05f, 0.12f));
                Deco(at, new Vector3(Range(rng, 0.3f, 0.5f), Range(rng, 0.2f, 0.45f), Range(rng, 0.3f, 0.5f)), q, Shade(TempleStone, Range(rng, 0.8f, 1f)), Surface.Fluted);
            }
        }

        /// <summary>The broken end of a beam: a few blocks sticking out unevenly past it.</summary>
        void RaggedEnd(Vector3 end, Vector3 outward, float width, float depth, System.Random rng)
        {
            var across = Vector3.Cross(Vector3.up, outward);
            for (int k = 0; k < 4; k++)
            {
                float reach = Range(rng, 0.1f, 0.6f);
                var at = end + outward * (reach / 2f) + across * Range(rng, -width / 3f, width / 3f) + Vector3.up * Range(rng, -depth / 4f, depth / 4f);
                Deco(at, new Vector3(Range(rng, 0.3f, 0.6f), Range(rng, 0.3f, 0.5f), reach + 0.1f), Quaternion.LookRotation(outward) * Quaternion.Euler(Range(rng, -10f, 10f), 0f, Range(rng, -10f, 10f)),
                    Shade(TempleStone, Range(rng, 0.85f, 1f)), Surface.Ashlar);
            }
        }

        // ---------------------------------------------------------------- what has fallen

        void TempleRuins(System.Random rng)
        {
            // The toppled column: its shaft lying across the east pool, the drums parted at the joints.
            var (a, b) = FallenShaft;
            var dir = (b - a).normalized;
            float len = (b - a).magnitude, along = 0f;
            var lie = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            while (along < len - 0.1f)
            {
                float h = Mathf.Min(Range(rng, 1f, 1.3f), len - along);
                var at = L(Temple, 0f, DrumRadius, 0f) + a + dir * (along + h / 2f);
                DecoPrim(PrimitiveType.Cylinder, at, new Vector3(DrumRadius * 2f, h / 2f - 0.02f, DrumRadius * 2f), lie * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Shade(TempleStone, Range(rng, 0.85f, 1f)), Surface.Fluted);
                along += h;
            }
            var neckAt = L(Temple, 0f, DrumRadius, 0f) + b + dir * 0.1f;
            DecoPrim(PrimitiveType.Cylinder, neckAt, new Vector3(1.3f, 0.1f, 1.3f), lie, TempleStone, Surface.Ashlar);
            Deco(neckAt + dir * 0.3f + Vector3.up * 0.05f, new Vector3(1.5f, 1.5f, 0.2f), Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 12f), Shade(TempleStone, 0.95f), Surface.Ashlar);
            foreach (var (at, yaw, upright) in FallenDrums)
            {
                var p = L(Temple, at.x, at.y, at.z);
                if (upright)
                    DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.4f, new Vector3(1.1f, 0.4f, 1.1f), Quaternion.Euler(Range(rng, -6f, 6f), yaw, Range(rng, -6f, 6f)), Shade(TempleStone, 0.9f), Surface.Fluted);
                else
                    DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * DrumRadius, new Vector3(1.1f, 0.45f, 1.1f), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f), Shade(TempleStone, 0.92f), Surface.Fluted);
            }

            // The ceiling's rubble in the west pool under the oculus, chunks of the vault in a heap.
            for (int i = 0; i < 26; i++)
            {
                float u = Range(rng, -1f, 1f), v = Range(rng, -1f, 1f);
                float rise = 0.5f * (1f - 0.6f * (u * u + v * v));
                var p = L(Temple, -4.1f + u * 1.6f, Mathf.Max(0.12f, rise), 1f + v * 1.8f);
                Deco(p, new Vector3(Range(rng, 0.35f, 0.9f), Range(rng, 0.25f, 0.5f), Range(rng, 0.35f, 0.9f)), Quaternion.Euler(Range(rng, -25f, 25f), Range(rng, 0f, 90f), Range(rng, -25f, 25f)),
                    Shade(i % 4 == 0 ? DrownedStone : TempleStone, Range(rng, 0.8f, 1.05f)), i % 3 == 0 ? Surface.Masonry : Surface.Ashlar);
            }
            // A slab of the coffered ceiling, tipped up against the heap.
            Deco(L(Temple, -3.2f, 0.55f, -0.6f), new Vector3(2f, 0.3f, 1.4f), Quaternion.Euler(-28f, 15f, 0f), DrownedStone, Surface.Masonry);

            // The great stone head of an older idol, half sunk, staring up.
            var head = L(Temple, GiantHead.x, GiantHead.y, GiantHead.z);
            var tilt = Quaternion.Euler(-35f, 20f, 8f);
            DecoPrim(PrimitiveType.Sphere, head + Vector3.up * 0.7f, new Vector3(1.5f, 1.8f, 1.5f), tilt, Shade(TempleStone, 0.9f), Surface.Masonry);
            var face = head + Vector3.up * 0.7f + tilt * Vector3.forward * 0.68f;
            Deco(face + tilt * new Vector3(0f, -0.05f, 0.05f), new Vector3(0.18f, 0.45f, 0.2f), tilt, Shade(TempleStone, 0.85f), Surface.Masonry);
            foreach (float s in new[] { -1f, 1f })
            {
                DecoPrim(PrimitiveType.Sphere, face + tilt * new Vector3(s * 0.3f, 0.25f, -0.05f), new Vector3(0.28f, 0.14f, 0.12f), tilt, Soot, Surface.Plain);
                Deco(face + tilt * new Vector3(s * 0.3f, 0.36f, 0f), new Vector3(0.36f, 0.06f, 0.12f), tilt, Shade(TempleStone, 0.8f), Surface.Masonry);
            }
            Deco(face + tilt * new Vector3(0f, -0.38f, -0.03f), new Vector3(0.42f, 0.05f, 0.1f), tilt, Soot, Surface.Plain);
            // Its headdress, a cobra hood carved behind it, and moss over its brow.
            DecoPrim(PrimitiveType.Sphere, head + Vector3.up * 0.75f + tilt * Vector3.back * 0.3f, new Vector3(2.1f, 2.3f, 0.5f), tilt, Shade(DrownedStone, 1.1f), Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, head + Vector3.up * 1.4f, new Vector3(1.2f, 0.4f, 1.1f), tilt, Moss, Surface.Grass);
        }

        // ---------------------------------------------------------------- the vault and the oculus

        void TempleCeiling(System.Random rng)
        {
            var vault = new Color(0.23f, 0.25f, 0.23f);
            // The ceiling in 2 m stones (lit by what's under each), open over the oculus.
            void Tiles(float x0, float x1, float z0, float z1)
            {
                for (float x = x0; x < x1 - 0.01f; x += 2f)
                    for (float z = z0; z < z1 - 0.01f; z += 2f)
                    {
                        if (x >= OculusX0 - 0.01f && x + 2f <= OculusX1 + 0.01f && z >= OculusZ0 - 0.01f && z + 2f <= OculusZ1 + 0.01f) continue;
                        Deco(L(Temple, x + 1f, WallHeight + RoofThickness / 2f, z + 1f), new Vector3(2f, RoofThickness, 2f), Shade(vault, Range(rng, 0.9f, 1.05f)), Surface.Masonry);
                    }
            }
            Tiles(-12f, 12f, -18f, 20f);
            Tiles(-12f, 20f, 20f, 34f);

            // Beams across the hall at each column, wall to column to column, missing where the east column fell and
            // broken off at the oculus.
            var beam = Shade(TempleStone, 0.92f);
            foreach (float z in TempleColumnZ)
            {
                bool fell = Mathf.Abs(z - (-5f)) < 0.1f;
                foreach (var (a, b) in new[] { (-11.7f, -ColonnadeX), (ColonnadeX, 11.7f) })
                    if (!(fell && a > 0f)) Deco(L(Temple, (a + b) / 2f, 6.62f, z), new Vector3(b - a, 0.76f, 0.7f), beam, Surface.Ashlar);
                if (fell) continue;
                if (z > OculusZ0 && z < OculusZ1)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Deco(L(Temple, s * (ColonnadeX + OculusX1 + 0.3f) / 2f, 6.62f, z), new Vector3(ColonnadeX - OculusX1 - 0.3f, 0.76f, 0.7f), beam, Surface.Ashlar);
                        RaggedEnd(L(Temple, s * (OculusX1 + 0.3f), 6.62f, z), s < 0f ? Vector3.right : Vector3.left, 0.7f, 0.76f, rng);
                    }
                else Deco(L(Temple, 0f, 6.62f, z), new Vector3(ColonnadeX * 2f, 0.76f, 0.7f), beam, Surface.Ashlar);
            }
            // Coffers between the beams: a sunken dark panel with a bronze star in each.
            for (int i = 0; i < TempleColumnZ.Length - 1; i++)
                for (float x = -6f; x < 6.1f; x += 3f)
                {
                    float z = (TempleColumnZ[i] + TempleColumnZ[i + 1]) / 2f;
                    if (x > OculusX0 - 1.6f && x < OculusX1 + 1.6f && z > OculusZ0 - 1.6f && z < OculusZ1 + 1.6f) continue;
                    var at = L(Temple, x, WallHeight - 0.005f, z);
                    Deco(at, new Vector3(2.4f, 0.01f, 5.8f), Shade(vault, 0.6f), Surface.Masonry);
                    foreach (float turn in new[] { 0f, 45f })
                        Deco(at + Vector3.down * 0.01f, new Vector3(0.36f, 0.01f, 0.36f), Quaternion.Euler(0f, turn, 0f), Verdigris, Surface.Metal);
                }
            // Beams across the wing, and its east end.
            for (float x = -10f; x < 20f; x += 4f)
                Deco(L(Temple, x, 6.7f, 27f), new Vector3(0.6f, 0.6f, 14f - WallThickness), beam, Surface.Ashlar);

            // The oculus: a shaft up through the vault, its rim broken and ragged, cracks running out from it and
            // roots and vines hanging through.
            float ox = (OculusX0 + OculusX1) / 2f, oz = (OculusZ0 + OculusZ1) / 2f, ow = OculusX1 - OculusX0, ol = OculusZ1 - OculusZ0;
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(L(Temple, ox + s * (ow / 2f + 0.1f), WallHeight + 0.6f, oz), new Vector3(0.2f, 1.2f, ol + 0.4f), Shade(vault, 0.7f), Surface.Masonry);
                Deco(L(Temple, ox, WallHeight + 0.6f, oz + s * (ol / 2f + 0.1f)), new Vector3(ow + 0.4f, 1.2f, 0.2f), Shade(vault, 0.7f), Surface.Masonry);
            }
            float perimeter = 2f * (ow + ol);
            for (float t = 0f; t < perimeter; t += Range(rng, 0.45f, 0.9f))
            {
                var (p, inward) = OculusEdge(t);
                var at = p + inward * Range(rng, -0.25f, 0.35f) + Vector3.up * (WallHeight - Range(rng, 0.05f, 0.3f));
                Deco(at, new Vector3(Range(rng, 0.4f, 0.8f), Range(rng, 0.25f, 0.5f), Range(rng, 0.4f, 0.8f)), Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 90f), Range(rng, -15f, 15f)),
                    Shade(vault, Range(rng, 0.85f, 1.15f)), Surface.Masonry);
                if (rng.NextDouble() < 0.3)
                {
                    var crack = p - inward * Range(rng, 0.8f, 2.4f);
                    var mid = (p + crack) / 2f;
                    Deco(new Vector3(mid.x, WallHeight - 0.012f, mid.z), new Vector3(0.03f, 0.01f, Vector3.Distance(p, crack)), Quaternion.LookRotation(inward) * Quaternion.Euler(0f, Range(rng, -25f, 25f), 0f), Soot, Surface.Plain);
                }
                if (rng.NextDouble() < 0.45) HangingRoot(p + inward * Range(rng, -0.1f, 0.3f) + Vector3.up * (WallHeight - 0.2f), rng);
            }
            // Moonlight: the sky glow in the shaft and a faint beam down to the causeway.
            var beamFoot = L(Temple, ox, 0f, oz);
            for (int k = 0; k < 3; k++)
            {
                float shrink = 1f - k * 0.22f;
                Glow(beamFoot + Vector3.up * (WallHeight / 2f + 0.15f), new Vector3(ow * shrink - 0.3f, WallHeight - 0.3f, ol * shrink - 0.3f), Quaternion.identity, new Color(0.55f, 0.68f, 0.85f, 0.025f));
            }
        }

        /// <summary>A point t metres round the oculus rim, and the way out from the hole into the ceiling.</summary>
        (Vector3 at, Vector3 inward) OculusEdge(float t)
        {
            float w = OculusX1 - OculusX0, l = OculusZ1 - OculusZ0;
            if (t < w) return (L(Temple, OculusX0 + t, 0f, OculusZ0), Vector3.back);
            t -= w;
            if (t < l) return (L(Temple, OculusX1, 0f, OculusZ0 + t), Vector3.right);
            t -= l;
            if (t < w) return (L(Temple, OculusX1 - t, 0f, OculusZ1), Vector3.forward);
            t -= w;
            return (L(Temple, OculusX0, 0f, OculusZ1 - t), Vector3.left);
        }

        /// <summary>A root or a vine hanging from a point, in a few kinked lengths, leaves on the vines.</summary>
        void HangingRoot(Vector3 top, System.Random rng)
        {
            bool vine = rng.NextDouble() < 0.5;
            var color = vine ? new Color(0.16f, 0.24f, 0.1f) : new Color(0.2f, 0.14f, 0.09f);
            int n = rng.Next(3, 7);
            float dia = vine ? 0.035f : Range(rng, 0.05f, 0.11f);
            var p = top;
            for (int k = 0; k < n; k++)
            {
                var next = p + new Vector3(Range(rng, -0.2f, 0.2f), -Range(rng, 0.4f, 0.8f), Range(rng, -0.2f, 0.2f));
                Rod(p, next, dia * (1f - k * 0.1f), color, vine ? Surface.Grass : Surface.Wood);
                if (vine)
                    DecoPrim(PrimitiveType.Sphere, next + new Vector3(Range(rng, -0.08f, 0.08f), 0f, Range(rng, -0.08f, 0.08f)), new Vector3(0.16f, 0.03f, 0.1f),
                        Quaternion.Euler(Range(rng, -40f, 40f), Range(rng, 0f, 360f), 0f), Moss, Surface.Grass);
                p = next;
            }
        }

        // ---------------------------------------------------------------- walls

        void TempleWalls(System.Random rng)
        {
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Zone != Temple) continue;

                // A green line where the water once stood, a bronze-capped plinth over the tide-darkened foot.
                Deco(f.At(f.Length / 2f, 0.93f, 0.015f), new Vector3(f.Length, 0.06f, 0.03f), f.Facing, Moss, Surface.Grass);
                ClearStretches(f, 1.02f, 0.05f, 0.1f, (a, b) =>
                    Deco(f.At((a + b) / 2f, 1.02f, 0.05f), new Vector3(b - a, 0.1f, 0.1f), f.Facing, Shade(TempleStone, 0.9f), Surface.Ashlar));

                // The frieze: a teal band between bronze lines, a serpent winding the length of it, and a cornice above.
                ClearStretches(f, 4.8f, 0.42f, 0.08f, (a, b) =>
                {
                    Deco(f.At((a + b) / 2f, 4.8f, 0.02f), new Vector3(b - a, 0.76f, 0.03f), f.Facing, TempleTeal, Surface.Tile);
                    foreach (float y in new[] { 4.38f, 5.22f })
                        Deco(f.At((a + b) / 2f, y, 0.04f), new Vector3(b - a, 0.07f, 0.06f), f.Facing, Verdigris, Surface.Metal);
                    Vector3 Body(float t) => f.At(t, 4.8f + 0.2f * Mathf.Sin(t * 1.6f), 0.06f);
                    for (float t = a + 0.15f; t < b - 0.35f; t += 0.3f)
                        Rod(Body(t), Body(t + 0.32f), 0.1f, Shade(OldGold, 0.8f), Surface.Metal);
                    for (float t = a + 0.5f; t < b - 0.3f; t += 2.3f)
                        Glow(f.At(t, 4.8f - 0.2f * Mathf.Sin(t * 1.6f), 0.035f), new Vector3(0.12f, 0.12f, 0.01f), f.Facing * Quaternion.Euler(0f, 0f, 45f), Shade(RuneGlow, 0.7f));
                });
                ClearStretches(f, 6.78f, 0.14f, 0.26f, (a, b) =>
                {
                    Deco(f.At((a + b) / 2f, 6.78f, 0.13f), new Vector3(b - a, 0.26f, 0.26f), f.Facing, Shade(TempleStone, 0.95f), Surface.Ashlar);
                    Deco(f.At((a + b) / 2f, 6.6f, 0.08f), new Vector3(b - a, 0.08f, 0.16f), f.Facing, Shade(TempleStone, 0.85f), Surface.Ashlar);
                });

                // Moss and water streaks down from the cornice, narrowing as they run, and vines trailing from it.
                for (float t = Range(rng, 0.5f, 2f); t < f.Length - 0.5f; t += Range(rng, 1.5f, 4f))
                {
                    float w = Range(rng, 0.3f, 0.9f), top = Range(rng, 5.9f, 6.6f), h = Range(rng, 1.2f, 3.4f);
                    if (!WallFree(f, t, top - h / 2f, w / 2f, h / 2f, 0.05f)) continue;
                    var color = rng.NextDouble() < 0.6 ? Shade(Moss, Range(rng, 0.55f, 0.9f)) : Shade(DrownedStone, 0.55f);
                    float x = t, y = top;
                    for (int k = 0; k < 4; k++)
                    {
                        float seg = h / 4f, wk = w * (1f - k * 0.22f);
                        Deco(f.At(x, y - seg / 2f, 0.008f + k * 0.001f), new Vector3(wk, seg + 0.05f, 0.01f), f.Facing, color, Surface.Grass);
                        x += Range(rng, -0.08f, 0.08f);
                        y -= seg;
                    }
                    if (rng.NextDouble() < 0.35) HangingRoot(f.At(t, 6.62f, 0.3f), rng);
                }

                // Skull niches between the pilasters.
                for (float t = 2.5f; t < f.Length - 2f; t += Range(rng, 4.5f, 7f))
                {
                    if (rng.NextDouble() < 0.35 || !WallFree(f, t, 2.2f, 0.6f, 0.9f, 0.3f) || !KeptClear(f.At(t, 0f, 0.5f), 0.6f)) continue;
                    SkullNiche(f, t, rng);
                }
            }

            // Pilasters on the hall's side walls opposite each column, where the wall is free.
            foreach (float z in TempleColumnZ)
                foreach (float s in new[] { -1f, 1f })
                {
                    var foot = L(Temple, s * (12f - WallHalf), 0f, z);
                    if (!KeptClear(foot - Vector3.right * s * 0.3f, 0.6f)) continue;
                    var inward = Vector3.left * s;
                    Deco(foot + inward * 0.17f + Vector3.up * 0.3f, new Vector3(0.5f, 0.6f, 1.1f), DrownedStone, Surface.Masonry);
                    Deco(foot + inward * 0.15f + Vector3.up * 3.45f, new Vector3(0.3f, 5.7f, 0.85f), TempleStone, Surface.Fluted);
                    Deco(foot + inward * 0.2f + Vector3.up * 6.3f, new Vector3(0.4f, 0.24f, 1.05f), Shade(TempleStone, 1.05f), Surface.Ashlar);
                    Deco(foot + inward * 0.31f + Vector3.up * 6.15f, new Vector3(0.02f, 0.05f, 0.95f), Verdigris, Surface.Metal);
                }
        }

        /// <summary>A round-headed niche in the wall with a skull and a stub of candle on its sill.</summary>
        void SkullNiche(WallFace f, float t, System.Random rng)
        {
            var q = f.Facing;
            Deco(f.At(t, 2.15f, 0.005f), new Vector3(0.75f, 1.1f, 0.01f), q, new Color(0.035f, 0.04f, 0.04f), Surface.Plain);
            DecoPrim(PrimitiveType.Cylinder, f.At(t, 2.7f, 0.006f), new Vector3(0.75f, 0.005f, 0.75f), q * Quaternion.Euler(90f, 0f, 0f), new Color(0.035f, 0.04f, 0.04f), Surface.Plain);
            foreach (float s in new[] { -1f, 1f })
                Deco(f.At(t + s * 0.45f, 2.2f, 0.06f), new Vector3(0.14f, 1.3f, 0.12f), q, Shade(TempleStone, 1.05f), Surface.Ashlar);
            Deco(f.At(t, 1.58f, 0.12f), new Vector3(1.05f, 0.1f, 0.24f), q, Shade(TempleStone, 1.05f), Surface.Ashlar);
            Deco(f.At(t, 3.12f, 0.06f), new Vector3(1.05f, 0.14f, 0.12f), q, Shade(TempleStone, 1.05f), Surface.Ashlar);
            Skull(f.At(t + Range(rng, -0.12f, 0.08f), 1.63f, 0.12f), q * Quaternion.Euler(0f, Range(rng, -25f, 25f), 0f));
            var candle = f.At(t + 0.24f, 1.63f, 0.14f);
            float h = Range(rng, 0.08f, 0.2f);
            DecoPrim(PrimitiveType.Cylinder, candle + Vector3.up * (h / 2f), new Vector3(0.05f, h / 2f, 0.05f), Quaternion.identity, new Color(0.78f, 0.74f, 0.62f), Surface.Plain);
            Glow(PrimitiveType.Sphere, candle + Vector3.up * (h + 0.03f), new Vector3(0.025f, 0.05f, 0.025f), Quaternion.identity, new Color(1f, 0.75f, 0.35f));
        }

        /// <summary>A skull on a surface, facing out along rot.</summary>
        void Skull(Vector3 at, Quaternion rot)
        {
            DecoPrim(PrimitiveType.Sphere, at + Vector3.up * 0.11f, new Vector3(0.17f, 0.17f, 0.2f), rot, BoneWhite, Surface.Plain);
            Deco(at + Vector3.up * 0.04f + rot * Vector3.forward * 0.06f, new Vector3(0.11f, 0.06f, 0.08f), rot, Shade(BoneWhite, 0.95f), Surface.Plain);
            foreach (float s in new[] { -1f, 1f })
                DecoPrim(PrimitiveType.Sphere, at + Vector3.up * 0.115f + rot * new Vector3(s * 0.04f, 0f, 0.085f), new Vector3(0.05f, 0.045f, 0.03f), rot, Soot, Surface.Plain);
        }

        // ---------------------------------------------------------------- guardians

        /// <summary>
        /// A hooded stone guardian on a plinth facing out from the wall, hands on the pommel of a bronze sword planted
        /// before it, eyes glowing in the dark of its hood; or one whose head has fallen at its feet.
        /// </summary>
        void Guardian(Vector3 foot, Vector3 facing, bool headless)
        {
            var q = Quaternion.LookRotation(facing);
            var stone = Shade(TempleStone, 0.95f);
            Deco(foot + Vector3.up * 0.3f, new Vector3(1.4f, 0.6f, 1.3f), q, DrownedStone, Surface.Masonry);
            Deco(foot + Vector3.up * 0.63f, new Vector3(1.5f, 0.08f, 1.4f), q, Shade(TempleStone, 1.05f), Surface.Ashlar);
            Deco(foot + Vector3.up * 0.3f + facing * 0.66f, new Vector3(0.9f, 0.3f, 0.02f), q, Verdigris, Surface.Metal);
            // The robe: a long bell to the shoulders, the hood over it.
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 1.45f, new Vector3(1.05f, 0.78f, 0.9f), q, stone, Surface.Masonry);
            DecoPrim(PrimitiveType.Cylinder, foot + Vector3.up * 2.55f, new Vector3(0.85f, 0.34f, 0.7f), q, stone, Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, foot + Vector3.up * 2.95f, new Vector3(1f, 0.42f, 0.78f), q, stone, Surface.Masonry);
            // Folds down the robe.
            for (int k = -2; k <= 2; k++)
                Deco(foot + Vector3.up * 1.5f + q * new Vector3(k * 0.18f, 0f, 0.42f - Mathf.Abs(k) * 0.04f), new Vector3(0.06f, 1.5f, 0.06f), q, Shade(stone, 0.85f), Surface.Masonry);
            // The arms forward to the pommel, and the sword.
            foreach (float s in new[] { -1f, 1f })
                Rod(foot + Vector3.up * 2.75f + q * new Vector3(s * 0.42f, 0f, 0.05f), foot + Vector3.up * 2.05f + q * new Vector3(s * 0.08f, 0f, 0.55f), 0.24f, stone, Surface.Masonry);
            var hands = foot + Vector3.up * 2.05f + facing * 0.58f;
            DecoPrim(PrimitiveType.Sphere, hands, new Vector3(0.3f, 0.2f, 0.22f), q, stone, Surface.Masonry);
            var bronze = Shade(Verdigris, 0.9f);
            Deco(hands + Vector3.down * 0.15f, new Vector3(0.06f, 0.25f, 0.06f), q, Shade(OldGold, 0.7f), Surface.Metal);
            Deco(hands + Vector3.down * 0.3f, new Vector3(0.62f, 0.07f, 0.1f), q, bronze, Surface.Metal);
            Deco(foot + Vector3.up * 1.22f + facing * 0.6f, new Vector3(0.16f, 1.2f, 0.035f), q, bronze, Surface.Metal);
            Deco(foot + Vector3.up * 0.66f + facing * 0.6f, new Vector3(0.1f, 0.08f, 0.035f), q * Quaternion.Euler(0f, 0f, 45f), bronze, Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, hands + Vector3.up * 0.14f, new Vector3(0.1f, 0.1f, 0.1f), Quaternion.identity, Shade(OldGold, 0.8f), Surface.Metal);

            var hood = foot + Vector3.up * 3.35f;
            if (headless)
            {
                // A broken neck, and the hooded head lying on the floor beside the plinth.
                Deco(hood + Vector3.down * 0.25f, new Vector3(0.36f, 0.16f, 0.36f), q * Quaternion.Euler(8f, 20f, 0f), Shade(stone, 0.8f), Surface.Masonry);
                var fallen = foot + q * new Vector3(0.95f, 0f, 0.6f);
                DecoPrim(PrimitiveType.Sphere, fallen + Vector3.up * 0.3f, new Vector3(0.62f, 0.66f, 0.66f), q * Quaternion.Euler(70f, 40f, 0f), stone, Surface.Masonry);
                return;
            }
            DecoPrim(PrimitiveType.Sphere, hood, new Vector3(0.62f, 0.78f, 0.66f), q, stone, Surface.Masonry);
            Deco(hood + Vector3.up * 0.3f + facing * 0.08f, new Vector3(0.28f, 0.2f, 0.4f), q * Quaternion.Euler(-30f, 0f, 0f), stone, Surface.Masonry);
            // The dark of the hood, and the eyes in it.
            DecoPrim(PrimitiveType.Sphere, hood + facing * 0.24f + Vector3.down * 0.05f, new Vector3(0.38f, 0.44f, 0.22f), q, new Color(0.03f, 0.035f, 0.035f), Surface.Plain);
            foreach (float s in new[] { -1f, 1f })
                Glow(PrimitiveType.Sphere, hood + facing * 0.33f + q * new Vector3(s * 0.08f, 0f, 0f), new Vector3(0.06f, 0.035f, 0.03f), q, RuneGlow);
        }

        // ---------------------------------------------------------------- the sanctum

        void TempleDais()
        {
            // Each tier: a moulded lip, a bronze line under it, and carved panels down its front.
            for (int i = 0; i < Tiers.Length; i++)
            {
                var (half, front, top) = Tiers[i];
                float depth = 34f - WallHalf - front, below = i == 0 ? 0f : Tiers[i - 1].top;
                Deco(L(Temple, 0f, top - 0.04f, front), new Vector3(half * 2f + 0.14f, 0.1f, 0.14f), Shade(TempleStone, 1.08f), Surface.Ashlar);
                foreach (float s in new[] { -1f, 1f })
                {
                    Deco(L(Temple, s * half, top - 0.04f, front + depth / 2f), new Vector3(0.14f, 0.1f, depth), Shade(TempleStone, 1.08f), Surface.Ashlar);
                    Deco(L(Temple, s * (half + 0.012f), top - 0.14f, front + depth / 2f), new Vector3(0.03f, 0.03f, depth), Verdigris, Surface.Metal);
                }
                Deco(L(Temple, 0f, top - 0.14f, front - 0.012f), new Vector3(half * 2f, 0.03f, 0.03f), Verdigris, Surface.Metal);
                // Teal panels down the riser, either side of the stairs.
                if (i == 0) continue;
                for (float x = -half + 0.7f; x < half - 0.4f; x += 1.1f)
                    if (Mathf.Abs(x) > 2.5f)
                        Deco(L(Temple, x, (below + top - 0.18f) / 2f, front - 0.006f), new Vector3(0.75f, top - below - 0.3f, 0.012f), TempleTeal, Surface.Tile);
            }
            // A runner of dark cloth up the middle steps, gold at its edges.
            Deco(L(Temple, 0f, Tiers[0].top + 0.004f, (Tiers[0].front + Tiers[2].front) / 2f), new Vector3(1.6f, 0.008f, Tiers[2].front - Tiers[0].front), new Color(0.08f, 0.16f, 0.16f), Surface.Fabric);

            // A mosaic before the dais: a serpent eye in teal and gold.
            var m = L(Temple, 0f, 0.005f, 21.7f);
            DecoPrim(PrimitiveType.Cylinder, m, new Vector3(4.2f, 0.004f, 4.2f), Quaternion.identity, Shade(OldGold, 0.75f), Surface.Mosaic);
            DecoPrim(PrimitiveType.Cylinder, m + Vector3.up * 0.004f, new Vector3(3.8f, 0.004f, 3.8f), Quaternion.identity, TempleTeal, Surface.MosaicGrey);
            DecoPrim(PrimitiveType.Cylinder, m + Vector3.up * 0.008f, new Vector3(2.6f, 0.004f, 1.3f), Quaternion.identity, Shade(OldGold, 0.85f), Surface.Mosaic);
            DecoPrim(PrimitiveType.Cylinder, m + Vector3.up * 0.012f, new Vector3(0.3f, 0.004f, 1.1f), Quaternion.identity, Soot, Surface.Plain);

            // Offerings on the tables: candles, coin, a chalice, a skull.
            foreach (float s in new[] { -1f, 1f })
            {
                var top = L(Temple, s * 3.4f, Tiers[1].top + 0.9f, 27.6f);
                Deco(top + Vector3.up * 0.004f, new Vector3(1.7f, 0.01f, 0.5f), new Color(0.1f, 0.2f, 0.2f), Surface.Fabric);
                foreach (var (item, x) in new[] { ("CandleStick_Triple", -0.6f), ("Coin_Pile_2", -0.15f), ("Chalice", 0.3f), ("Coin_Pile", 0.55f) })
                {
                    var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                    if (model != null) AddModel(model, top + new Vector3(s * x, 0.01f, 0f), Quaternion.Euler(0f, 180f, 0f), false);
                }
                Skull(top + new Vector3(-s * 0.1f, 0.01f, 0.18f), Quaternion.Euler(0f, 180f + s * 20f, 0f));
                Brazier(L(Temple, s * 6f, Tiers[0].top, 25.1f));
            }
            PlaceFree(Bones, L(Temple, -4.9f, 0f, 22.8f), Quaternion.Euler(0f, 25f, 0f), false);
            PlaceFree(Bones, L(Temple, 5.2f, 0f, 23f), Quaternion.Euler(0f, -40f, 0f), false);
        }

        /// <summary>
        /// The serpent god: a coiled body heaped on the top tier, its neck rising out of the coils, the hood spread and
        /// the jaws open over the stairs, eyes alight; a ring of runes on the wall behind it.
        /// </summary>
        void SerpentIdol()
        {
            var b = L(Temple, IdolBase.x, IdolBase.y, IdolBase.z);
            var band = Shade(OldGold, 0.85f);
            // The heap's core, stepped in under each coil so it fills them without showing.
            foreach (var (y0, y1, dia) in new[] { (0f, 0.6f, 3f), (0.6f, 1.1f, 2.5f), (1.1f, 1.55f, 2f), (1.55f, 1.95f, 1.5f) })
                DecoPrim(PrimitiveType.Cylinder, b + Vector3.up * ((y0 + y1) / 2f), new Vector3(dia, (y1 - y0) / 2f, dia), Quaternion.identity, Shade(IdolJade, 0.8f), Surface.Masonry);
            var coils = new[] { (0.28f, 1.62f, 0.58f), (0.8f, 1.42f, 0.54f), (1.28f, 1.18f, 0.5f), (1.72f, 0.92f, 0.46f) };
            for (int c = 0; c < coils.Length; c++)
            {
                var (y, r, d) = coils[c];
                const int n = 16;
                for (int k = 0; k < n; k++)
                {
                    // Each coil rises a little round its turn, so the rings read as one body.
                    float a0 = k * 360f / n, a1 = (k + 1) * 360f / n;
                    var p0 = b + Quaternion.Euler(0f, a0 + c * 40f, 0f) * Vector3.forward * r + Vector3.up * (y + 0.12f * k / n);
                    var p1 = b + Quaternion.Euler(0f, a1 + c * 40f, 0f) * Vector3.forward * r + Vector3.up * (y + 0.12f * (k + 1) / n);
                    Rod(p0, p1, d, IdolJade, Surface.Masonry);
                    if (k % 4 == 0) Rod(p0 + (p1 - p0) * 0.45f, p0 + (p1 - p0) * 0.55f, d + 0.04f, band, Surface.Metal);
                }
            }
            // The tail tip trailing off the coil onto the tier.
            Rod(b + new Vector3(1.5f, 0.25f, -0.6f), b + new Vector3(2.4f, 0.08f, -1.3f), 0.3f, IdolJade, Surface.Masonry);
            Rod(b + new Vector3(2.4f, 0.08f, -1.3f), b + new Vector3(2.75f, 0.05f, -2.2f), 0.16f, IdolJade, Surface.Masonry);

            // The neck in an S up out of the coils, the belly plates gold.
            var neck = new[] { new Vector3(0f, 1.95f, 0.3f), new Vector3(0f, 2.6f, 0.6f), new Vector3(0f, 3.3f, 0.55f), new Vector3(0f, 3.85f, 0.15f), new Vector3(0f, 4.2f, -0.45f) };
            for (int k = 0; k < neck.Length - 1; k++)
            {
                float d = Mathf.Lerp(0.62f, 0.44f, (float)k / (neck.Length - 2));
                Rod(b + neck[k], b + neck[k + 1], d, IdolJade, Surface.Masonry);
                DecoPrim(PrimitiveType.Sphere, b + neck[k + 1], new Vector3(d, d, d), Quaternion.identity, IdolJade, Surface.Masonry);
                var mid = b + (neck[k] + neck[k + 1]) / 2f + Vector3.back * (d * 0.42f);
                Bar(mid + (neck[k + 1] - neck[k]) * 0.4f, mid - (neck[k + 1] - neck[k]) * 0.4f, d * 0.6f, 0.05f, Vector3.back, band, Surface.Metal);
            }

            // The hood, spread behind the head, its spectacle marks gold.
            var hoodAt = b + new Vector3(0f, 4.15f, -0.15f);
            DecoPrim(PrimitiveType.Sphere, hoodAt, new Vector3(2f, 2.1f, 0.3f), Quaternion.Euler(-8f, 0f, 0f), IdolJade, Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, hoodAt + new Vector3(0f, -0.05f, -0.1f), new Vector3(1.55f, 1.65f, 0.18f), Quaternion.Euler(-8f, 0f, 0f), Shade(IdolJade, 1.25f), Surface.Masonry);
            foreach (float s in new[] { -1f, 1f })
            {
                DecoPrim(PrimitiveType.Sphere, hoodAt + new Vector3(s * 0.48f, 0.35f, -0.22f), new Vector3(0.36f, 0.28f, 0.06f), Quaternion.identity, band, Surface.Metal);
                DecoPrim(PrimitiveType.Sphere, hoodAt + new Vector3(s * 0.48f, 0.35f, -0.25f), new Vector3(0.14f, 0.12f, 0.04f), Quaternion.identity, Soot, Surface.Plain);
            }
            for (int k = 0; k < 4; k++)
                Deco(hoodAt + new Vector3(0f, -0.55f + k * 0.22f, -0.2f + k * 0.03f), new Vector3(0.5f - k * 0.06f, 0.08f, 0.04f), Quaternion.Euler(-8f, 0f, 0f), band, Surface.Metal);

            // The head, jaws gaping, fangs, a forked tongue and glowing eyes.
            var head = b + new Vector3(0f, 4.42f, -0.85f);
            DecoPrim(PrimitiveType.Sphere, head, new Vector3(0.62f, 0.4f, 1.05f), Quaternion.Euler(8f, 0f, 0f), IdolJade, Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, head + new Vector3(0f, 0.08f, 0.12f), new Vector3(0.7f, 0.34f, 0.7f), Quaternion.identity, IdolJade, Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, head + new Vector3(0f, -0.3f, -0.05f), new Vector3(0.5f, 0.14f, 0.9f), Quaternion.Euler(-24f, 0f, 0f), Shade(IdolJade, 0.9f), Surface.Masonry);
            DecoPrim(PrimitiveType.Sphere, head + new Vector3(0f, -0.15f, -0.2f), new Vector3(0.44f, 0.16f, 0.62f), Quaternion.Euler(-12f, 0f, 0f), new Color(0.22f, 0.05f, 0.05f), Surface.Plain);
            foreach (float s in new[] { -1f, 1f })
            {
                Rod(head + new Vector3(s * 0.13f, -0.08f, -0.36f), head + new Vector3(s * 0.12f, -0.34f, -0.4f), 0.045f, BoneWhite, Surface.Plain);
                Glow(PrimitiveType.Sphere, head + new Vector3(s * 0.22f, 0.12f, -0.22f), new Vector3(0.1f, 0.07f, 0.12f), Quaternion.identity, RuneGlow);
                Deco(head + new Vector3(s * 0.2f, 0.19f, -0.18f), new Vector3(0.14f, 0.04f, 0.22f), Quaternion.Euler(0f, s * 15f, s * 15f), Shade(IdolJade, 0.75f), Surface.Masonry);
            }
            var tongue = head + new Vector3(0f, -0.25f, -0.4f);
            Rod(tongue, tongue + new Vector3(0f, -0.08f, -0.35f), 0.04f, new Color(0.45f, 0.06f, 0.08f), Surface.Plain);
            foreach (float s in new[] { -1f, 1f })
                Rod(tongue + new Vector3(0f, -0.08f, -0.35f), tongue + new Vector3(s * 0.06f, -0.12f, -0.48f), 0.03f, new Color(0.45f, 0.06f, 0.08f), Surface.Plain);
            Glow(PrimitiveType.Sphere, head + new Vector3(0f, -0.18f, -0.25f), new Vector3(0.22f, 0.06f, 0.3f), Quaternion.identity, Shade(RuneGlow, 0.5f));

            // The rune halo on the wall behind: a dark disc, two rings of light and runes between them.
            var wall = L(Temple, 0f, 4.4f, 34f - WallHalf - 0.01f);
            var flat = Quaternion.Euler(90f, 0f, 0f);
            DecoPrim(PrimitiveType.Cylinder, wall, new Vector3(4.6f, 0.01f, 4.6f), flat, new Color(0.05f, 0.08f, 0.08f), Surface.Masonry);
            DecoPrim(PrimitiveType.Cylinder, wall + Vector3.back * 0.005f, new Vector3(4.8f, 0.005f, 4.8f), flat, Shade(OldGold, 0.7f), Surface.Metal);
            foreach (var (r, w, n) in new[] { (2.05f, 0.07f, 40), (1.55f, 0.05f, 32) })
                for (int k = 0; k < n; k++)
                {
                    float a = (k + 0.5f) * 360f / n;
                    var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                    Glow(wall + dir * r + Vector3.back * 0.02f, new Vector3(2f * Mathf.PI * r / n * 1.08f, w, 0.01f), Quaternion.Euler(0f, 0f, a), RuneGlow);
                }
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f;
                var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                Glow(wall + dir * 1.8f + Vector3.back * 0.02f, new Vector3(0.05f, 0.22f, 0.01f), Quaternion.Euler(0f, 0f, a + (k % 2) * 30f), RuneGlow);
                Glow(wall + dir * 1.8f + Vector3.back * 0.02f + Quaternion.Euler(0f, 0f, a) * Vector3.right * 0.06f, new Vector3(0.12f, 0.04f, 0.01f), Quaternion.Euler(0f, 0f, a), RuneGlow);
            }
        }

        void TempleWings(System.Random rng)
        {
            // The west wing: a sarcophagus with its lid shoved aside and the bones pulled out of it.
            var s = L(Temple, SarcophagusSpot.x, 0f, SarcophagusSpot.z);
            Deco(s + Vector3.up * 0.12f, new Vector3(1.5f, 0.24f, 2.6f), DrownedStone, Surface.Masonry);
            Deco(s + Vector3.up * 0.55f, new Vector3(1.3f, 0.7f, 2.4f), TempleStone, Surface.Ashlar);
            Deco(s + Vector3.up * 0.91f, new Vector3(1.1f, 0.03f, 2.2f), new Color(0.02f, 0.02f, 0.02f), Surface.Plain);
            foreach (float side in new[] { -1f, 1f })
                Deco(s + new Vector3(side * 0.655f, 0.55f, 0f), new Vector3(0.02f, 0.4f, 2f), TempleTeal, Surface.Tile);
            var lid = Quaternion.Euler(0f, 14f, -6f);
            Deco(s + new Vector3(0.45f, 1.02f, -0.35f), new Vector3(1.42f, 0.18f, 2.5f), lid, Shade(TempleStone, 1.05f), Surface.Ashlar);
            DecoPrim(PrimitiveType.Capsule, s + new Vector3(0.45f, 1.2f, -0.35f), new Vector3(0.5f, 0.8f, 0.3f), lid * Quaternion.Euler(90f, 0f, 0f), Shade(TempleStone, 1.1f), Surface.Ashlar);
            Skull(s + new Vector3(-0.2f, 0.92f, 0.7f), Quaternion.Euler(0f, 160f, 0f));
            PlaceFree(Bones, s + new Vector3(1.4f, 0f, 1.2f), Quaternion.Euler(0f, 70f, 0f), false);
            if (PlaceFree(CandleStand, s + new Vector3(1.3f, 0f, -1.6f), Quaternion.identity, true)) CandleLight(s + new Vector3(1.3f, 1.45f, -1.6f));
            PlaceFree(BigVase, L(Temple, -11f, 0f, 33f), Quaternion.identity, true);
            PlaceFree(SmallVase, L(Temple, -10.2f, 0f, 33.1f), Quaternion.Euler(0f, 40f, 0f), true);

            // The east wing: the hoard. Chests, urns and a heap of gold coin.
            foreach (var (item, x, z, yaw) in new[]
            {
                (WoodChest, 19f, 33.2f, 185f), (WoodChest, 14.4f, 33.1f, 170f), (IronCrate, 19.1f, 30.2f, 15f),
                (BigVase, 13.3f, 31.6f, 0f), (SmallVase, 19.2f, 28.9f, 0f), (BigVase, 17.4f, 33.2f, 30f),
            })
                PlaceFree(item, L(Temple, x, 0f, z), Quaternion.Euler(0f, yaw, 0f), true);
            var heap = L(Temple, 16.6f, 0f, 31.6f);
            var gold = new Color(0.72f, 0.55f, 0.18f);
            for (int i = 0; i < 18; i++)
            {
                float u = Range(rng, -1f, 1f), v = Range(rng, -1f, 1f);
                var p = heap + Quaternion.Euler(0f, -8f, 0f) * new Vector3(u * 1.3f, 0f, v * 0.9f);
                float r = Range(rng, 0.5f, 1.1f), h = 1.2f * (1f - 0.5f * (u * u + v * v));
                DecoPrim(PrimitiveType.Sphere, p, new Vector3(r, h, r), Quaternion.identity, Shade(gold, Range(rng, 0.85f, 1.1f)), Surface.Metal);
            }
            foreach (var (item, x, y, z) in new[] { ("Coin_Pile", 16.1f, 0.5f, 31.3f), ("Coin_Pile_2", 17.3f, 0.42f, 31.9f), ("Chalice", 16.7f, 0.55f, 31.7f), ("Coin_Pile", 13.8f, 0f, 29.6f), ("Vase_Rubble_Medium", 19.1f, 0f, 31.7f) })
            {
                var at = L(Temple, x, y, z);
                var model = PropModels.GetPack(Fantasy + item, PackMaterial);
                if (model != null && KeptClear(at, 0.3f)) AddModel(model, at, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), false);
            }

            // Urns in the hall's corners, bones against its walls.
            foreach (var (item, x, z) in new[] { (BigVase, -11f, -17f), (SmallVase, -10.3f, -17.1f), (BigVase, 11f, -17f), (BigVase, 11f, 19f), (SmallVase, -11.1f, 19f) })
                PlaceFree(item, L(Temple, x, 0f, z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), true);
            PlaceFree(Bones, L(Temple, -10.9f, 0f, 9f), Quaternion.Euler(0f, 90f, 0f), false);
            PlaceFree(Bones, L(Temple, 10.9f, 0f, -14f), Quaternion.Euler(0f, -90f, 0f), false);
        }

        // ---------------------------------------------------------------- light

        void TempleLights()
        {
            // The moon down through the oculus.
            var moon = new GameObject("Temple Moonlight").AddComponent<Light>();
            moon.transform.SetParent(Root, false);
            moon.transform.localPosition = L(Temple, 0.6f, 16f, 1.4f);
            moon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            moon.type = LightType.Spot;
            moon.spotAngle = 58f;
            moon.range = 24f;
            moon.intensity = 6f;
            moon.color = MoonLight;
            moon.shadows = LightShadows.None;

            // A cold glow off the water, the idol's eyes and halo, and warm light in the wings.
            ChapelLight(L(Temple, 0f, 4.5f, -10f), 15f, 0.55f, new Color(0.35f, 0.75f, 0.7f));
            ChapelLight(L(Temple, 0f, 4.5f, 12f), 15f, 0.55f, new Color(0.35f, 0.75f, 0.7f));
            ChapelLight(L(Temple, 0f, 4.6f, 29.4f), 10f, 1.4f, RuneGlow);
            ChapelLight(L(Temple, 13.5f, 4f, 27f), 11f, 0.8f, new Color(1f, 0.68f, 0.38f));
            HangChandelier(L(Temple, -9.6f, WallHeight - 0.25f, 25f), TempleChandelier);
            ChapelLight(L(Temple, -9.6f, 4.4f, 25f), 9f, 0.9f, new Color(1f, 0.68f, 0.38f));

            foreach (var p in HallBraziers) Brazier(L(Temple, p.x, 0.6f, p.y));

            // Torches on the end columns, lighting the aisles.
            foreach (float x in new[] { -ColonnadeX, ColonnadeX })
                foreach (float z in new[] { -12f, 16f })
                {
                    var outward = x < 0f ? Vector3.left : Vector3.right;
                    var foot = L(Temple, x, 0f, z);
                    if (HangItem(WallTorch, foot + outward * DrumRadius, Quaternion.LookRotation(outward), 2.4f))
                        CandleLight(foot + outward * (DrumRadius + 0.32f) + Vector3.up * 2.75f);
                }
        }

        /// <summary>A fire bowl on a stand, with its light.</summary>
        void Brazier(Vector3 at)
        {
            var bowl = PropModels.GetPack(Cauldron.Path, PackMaterial);
            if (bowl != null) AddModel(bowl, at, Quaternion.identity, true);
            else DecoPrim(PrimitiveType.Cylinder, at + Vector3.up * 0.4f, new Vector3(0.9f, 0.4f, 0.9f), Quaternion.identity, Soot, Surface.Metal);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f * Mathf.Deg2Rad;
                Glow(PrimitiveType.Sphere, at + new Vector3(Mathf.Cos(a) * 0.15f, 0.85f + (k % 2) * 0.08f, Mathf.Sin(a) * 0.15f), new Vector3(0.26f, 0.42f, 0.26f), Quaternion.identity, new Color(1f, 0.5f, 0.15f));
            }
            Glow(PrimitiveType.Sphere, at + Vector3.up * 1.05f, new Vector3(0.2f, 0.45f, 0.2f), Quaternion.identity, new Color(1f, 0.78f, 0.35f));
            var light = new GameObject("Brazier Light").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.transform.localPosition = at + Vector3.up * 1.3f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = 8f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<Flicker>().Seed = Mathf.RoundToInt(at.x * 5f + at.z * 3f);
        }
    }
}
