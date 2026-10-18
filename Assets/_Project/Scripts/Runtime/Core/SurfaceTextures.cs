using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>What a greybox surface is made of. Picks the texture, bumpiness and shine of its material.</summary>
    public enum Surface
    {
        Plain,
        Concrete,
        Brick,
        Metal,
        Rust,
        Wood,
        Tile,
        Plaster,
        Asphalt,
        Dirt,
        Grass,
        Fabric,
        Skin,
        Rubber,
    }

    /// <summary>
    /// Makes tileable detail and normal textures for each <see cref="Surface"/> in code when first needed, so
    /// the game has real-looking concrete, brick, planks and so on without any texture files.
    /// Detail textures are grey around 0.5 (the shader doubles them), with height in alpha.
    /// </summary>
    public static class SurfaceTextures
    {
        public const int Size = 256;

        /// <summary>How a surface's material is set up.</summary>
        public readonly struct Look
        {
            public readonly float Scale, Detail, Bump, Gloss, Metallic, Grime;
            public Look(float scale, float detail, float bump, float gloss, float metallic, float grime)
            {
                Scale = scale; Detail = detail; Bump = bump; Gloss = gloss; Metallic = metallic; Grime = grime;
            }
        }

        /// <summary>Tiles per metre, detail strength, normal strength, smoothness, metallic and grime, per surface.</summary>
        public static Look LookOf(Surface s) => s switch
        {
            Surface.Concrete => new Look(0.35f, 0.8f, 0.8f, 0.18f, 0f, 0.5f),
            Surface.Brick => new Look(0.8f, 0.85f, 1f, 0.1f, 0f, 0.45f),
            Surface.Metal => new Look(0.5f, 0.5f, 0.45f, 0.6f, 0.75f, 0.3f),
            Surface.Rust => new Look(0.45f, 0.85f, 0.7f, 0.25f, 0.35f, 0.45f),
            Surface.Wood => new Look(0.5f, 0.8f, 0.6f, 0.25f, 0f, 0.35f),
            Surface.Tile => new Look(0.5f, 0.6f, 0.8f, 0.6f, 0f, 0.45f),
            Surface.Plaster => new Look(0.3f, 0.5f, 0.4f, 0.1f, 0f, 0.55f),
            Surface.Asphalt => new Look(0.6f, 0.7f, 0.6f, 0.22f, 0f, 0.4f),
            Surface.Dirt => new Look(0.4f, 0.9f, 0.9f, 0.05f, 0f, 0.2f),
            Surface.Grass => new Look(0.7f, 0.9f, 0.5f, 0.12f, 0f, 0.2f),
            Surface.Fabric => new Look(5f, 0.45f, 0.35f, 0.05f, 0f, 0.45f),
            Surface.Skin => new Look(3f, 0.6f, 0.45f, 0.35f, 0f, 0.4f),
            Surface.Rubber => new Look(2f, 0.3f, 0.3f, 0.3f, 0f, 0.2f),
            _ => new Look(0.5f, 0.35f, 0.3f, 0.2f, 0f, 0.3f),
        };

        static readonly Dictionary<Surface, (Texture2D detail, Texture2D normal)> s_Cache = new Dictionary<Surface, (Texture2D, Texture2D)>();

        public static (Texture2D detail, Texture2D normal) Get(Surface s)
        {
            if (s_Cache.TryGetValue(s, out var t) && t.detail != null) return t;
            t = Make(s);
            s_Cache[s] = t;
            return t;
        }

        /// <summary>Makes every surface's textures up front, so the first zombie or wall of a kind doesn't hitch.</summary>
        public static void Prewarm()
        {
            foreach (Surface s in System.Enum.GetValues(typeof(Surface))) Get(s);
        }

        // ---------------------------------------------------------------- generation

        static (Texture2D, Texture2D) Make(Surface s)
        {
            var height = new float[Size * Size];
            var tint = new Color[Size * Size];
            int seed = (int)s * 7919 + 13;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size, v = (float)y / Size;
                Sample(s, u, v, seed, out float h, out Color c);
                height[y * Size + x] = Mathf.Clamp01(h);
                tint[y * Size + x] = c;
            }

            var detail = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { name = s + " Detail", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = s + " Normal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var dp = new Color32[Size * Size];
            var np = new Color32[Size * Size];
            const float bump = 6f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                var c = tint[i];
                dp[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), height[i]);
                float hl = height[y * Size + (x + Size - 1) % Size], hr = height[y * Size + (x + 1) % Size];
                float hd = height[((y + Size - 1) % Size) * Size + x], hu = height[((y + 1) % Size) * Size + x];
                var n = new Vector3((hl - hr) * bump, (hd - hu) * bump, 1f).normalized;
                np[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
            }
            detail.SetPixels32(dp);
            detail.Apply(true, true);
            normal.SetPixels32(np);
            normal.Apply(true, true);
            return (detail, normal);
        }

        /// <summary>Height (0-1) and a detail colour (0.5 grey = no change) at a point of the tile.</summary>
        static void Sample(Surface s, float u, float v, int seed, out float h, out Color c)
        {
            float fine = Fbm(u, v, 32, 3, seed);
            switch (s)
            {
                case Surface.Concrete:
                {
                    float blot = Fbm(u, v, 4, 4, seed + 1);
                    float pores = Worley(u, v, 24, seed + 2);
                    float crack = Crack(u, v, 6, seed + 3);
                    h = 0.55f + (fine - 0.5f) * 0.35f - (1f - Mathf.SmoothStep(0f, 0.12f, pores)) * 0.25f - crack * 0.5f;
                    float g = 0.5f + (blot - 0.5f) * 0.35f + (fine - 0.5f) * 0.2f - crack * 0.25f;
                    c = Grey(g);
                    break;
                }
                case Surface.Brick:
                {
                    const int rows = 8, cols = 4;
                    float row = v * rows;
                    int ri = Mathf.FloorToInt(row);
                    float col = u * cols + (ri % 2 == 0 ? 0f : 0.5f);
                    int ci = Mathf.FloorToInt(col);
                    float fx = col - ci, fy = row - ri;
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx) * 2.2f, Mathf.Min(fy, 1f - fy));
                    float mortar = 1f - Mathf.SmoothStep(0.04f, 0.09f, edge);
                    float brickTone = Hash(((ci % cols) + cols) % cols, ri, seed);
                    h = Mathf.Lerp(0.75f + (fine - 0.5f) * 0.25f, 0.25f, mortar);
                    float g = Mathf.Lerp(0.42f + brickTone * 0.16f + (fine - 0.5f) * 0.18f, 0.62f, mortar);
                    c = new Color(g * (1.02f + brickTone * 0.06f), g, g * (0.96f - brickTone * 0.05f));
                    break;
                }
                case Surface.Metal:
                {
                    float streak = Fbm(u * 0.15f, v, 64, 2, seed + 4);
                    float scratch = Scratches(u, v, seed + 5);
                    float panel = Seam(u, v, 2, 0.01f);
                    h = 0.6f - scratch * 0.25f - panel * 0.5f + (streak - 0.5f) * 0.1f;
                    c = Grey(0.5f + (streak - 0.5f) * 0.25f + scratch * 0.2f - panel * 0.2f);
                    break;
                }
                case Surface.Rust:
                {
                    float rust = Fbm(u, v, 6, 4, seed + 6);
                    float flake = Fbm(u, v, 40, 2, seed + 7);
                    float r = Mathf.SmoothStep(0.45f, 0.65f, rust);
                    h = 0.5f + r * (flake - 0.4f) * 0.6f;
                    float g = 0.5f + (fine - 0.5f) * 0.15f - r * 0.06f;
                    c = Color.Lerp(Grey(g), new Color(0.72f, 0.42f, 0.24f) * (0.8f + flake * 0.4f), r * 0.8f);
                    break;
                }
                case Surface.Wood:
                {
                    const int planks = 8;
                    float p = v * planks;
                    int pi = Mathf.FloorToInt(p);
                    float fy = p - pi;
                    float offset = Hash(pi, 3, seed);
                    float grain = Fbm(u * 0.25f + offset, v * 4f, 16, 3, seed + 8);
                    float rings = Mathf.Sin((v * planks * 6f + grain * 9f) * Mathf.PI) * 0.5f + 0.5f;
                    float endJoint = Mathf.Abs(Mathf.Repeat(u + offset, 1f) - 0.5f) < 0.006f ? 1f : 0f;
                    float gap = 1f - Mathf.SmoothStep(0.02f, 0.06f, Mathf.Min(fy, 1f - fy));
                    float tone = 0.42f + Hash(pi, 7, seed) * 0.16f + rings * 0.08f + (grain - 0.5f) * 0.15f;
                    h = 0.7f + rings * 0.1f - Mathf.Max(gap, endJoint) * 0.6f;
                    float g = tone - Mathf.Max(gap, endJoint) * 0.25f;
                    c = new Color(g * 1.05f, g, g * 0.92f);
                    break;
                }
                case Surface.Tile:
                {
                    const int n = 4;
                    float tx = u * n, ty = v * n;
                    int ix = Mathf.FloorToInt(tx), iy = Mathf.FloorToInt(ty);
                    float fx = tx - ix, fy = ty - iy;
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                    float grout = 1f - Mathf.SmoothStep(0.02f, 0.05f, edge);
                    float tone = Hash(ix, iy, seed);
                    float chip = Mathf.SmoothStep(0.8f, 0.9f, Fbm(u, v, 12, 3, seed + 9)) * (tone > 0.7f ? 1f : 0f);
                    h = Mathf.Lerp(0.8f - chip * 0.4f, 0.3f, grout);
                    float g = Mathf.Lerp(0.55f + (tone - 0.5f) * 0.12f + (fine - 0.5f) * 0.06f - chip * 0.15f, 0.38f, grout);
                    c = Grey(g);
                    break;
                }
                case Surface.Plaster:
                {
                    float blot = Fbm(u, v, 3, 4, seed + 10);
                    float crack = Crack(u, v, 4, seed + 11);
                    float peel = Mathf.SmoothStep(0.62f, 0.66f, Fbm(u, v, 5, 4, seed + 12));
                    h = 0.6f + (fine - 0.5f) * 0.15f - crack * 0.4f - peel * 0.3f;
                    c = Grey(0.52f + (blot - 0.5f) * 0.3f - crack * 0.2f - peel * 0.12f);
                    break;
                }
                case Surface.Asphalt:
                {
                    float gravel = Worley(u, v, 48, seed + 13);
                    float patch = Fbm(u, v, 3, 3, seed + 14);
                    h = 0.4f + Mathf.SmoothStep(0.0f, 0.5f, gravel) * 0.4f + (fine - 0.5f) * 0.1f;
                    float g = 0.45f + (Hash(Mathf.FloorToInt(u * 48), Mathf.FloorToInt(v * 48), seed) - 0.5f) * 0.25f + (patch - 0.5f) * 0.2f;
                    c = Grey(g);
                    break;
                }
                case Surface.Dirt:
                {
                    float clump = Fbm(u, v, 8, 4, seed + 15);
                    float stones = 1f - Mathf.SmoothStep(0.1f, 0.25f, Worley(u, v, 14, seed + 16));
                    h = 0.4f + clump * 0.3f + stones * 0.3f;
                    float g = 0.45f + (clump - 0.5f) * 0.3f + stones * 0.12f;
                    c = new Color(g * 1.05f, g, g * 0.9f);
                    break;
                }
                case Surface.Grass:
                {
                    float blades = Fbm(u * 4f, v * 0.5f, 32, 2, seed + 17);
                    float patch = Fbm(u, v, 4, 3, seed + 18);
                    float bare = Mathf.SmoothStep(0.55f, 0.7f, patch);
                    h = 0.5f + (blades - 0.5f) * 0.6f * (1f - bare);
                    float g = 0.5f + (blades - 0.5f) * 0.4f;
                    c = Color.Lerp(new Color(g * 0.95f, g * 1.05f, g * 0.85f), new Color(0.52f, 0.45f, 0.36f), bare * 0.7f);
                    break;
                }
                case Surface.Fabric:
                {
                    float wx = Mathf.Sin(u * Size * 0.5f * Mathf.PI), wy = Mathf.Sin(v * Size * 0.5f * Mathf.PI);
                    float weave = (wx * wy) * 0.5f + 0.5f;
                    float stain = Mathf.SmoothStep(0.55f, 0.7f, Fbm(u, v, 3, 4, seed + 19));
                    float tear = Mathf.SmoothStep(0.72f, 0.75f, Fbm(u, v, 6, 3, seed + 20));
                    h = 0.5f + (weave - 0.5f) * 0.3f - tear * 0.4f;
                    float g = 0.5f + (weave - 0.5f) * 0.12f + (fine - 0.5f) * 0.1f - stain * 0.15f - tear * 0.2f;
                    c = Color.Lerp(Grey(g), new Color(0.42f, 0.18f, 0.14f), stain * 0.45f);
                    break;
                }
                case Surface.Skin:
                {
                    float blotch = Fbm(u, v, 5, 4, seed + 21);
                    float veins = 1f - Mathf.SmoothStep(0f, 0.05f, Mathf.Abs(Fbm(u, v, 8, 3, seed + 22) - 0.5f));
                    float wound = Mathf.SmoothStep(0.66f, 0.72f, Fbm(u, v, 4, 3, seed + 23));
                    h = 0.55f + (fine - 0.5f) * 0.2f - wound * 0.35f + veins * 0.08f;
                    float g = 0.5f + (blotch - 0.5f) * 0.3f;
                    c = Grey(g);
                    c = Color.Lerp(c, new Color(0.38f, 0.4f, 0.5f), veins * 0.35f);
                    c = Color.Lerp(c, new Color(0.45f, 0.06f, 0.05f), wound * 0.8f);
                    break;
                }
                case Surface.Rubber:
                {
                    h = 0.5f + (fine - 0.5f) * 0.2f;
                    c = Grey(0.5f + (fine - 0.5f) * 0.08f);
                    break;
                }
                default:
                {
                    float blot = Fbm(u, v, 4, 3, seed + 24);
                    h = 0.5f + (fine - 0.5f) * 0.3f;
                    c = Grey(0.5f + (fine - 0.5f) * 0.15f + (blot - 0.5f) * 0.15f);
                    break;
                }
            }
        }

        static Color Grey(float g) => new Color(g, g, g);

        // ---------------------------------------------------------------- tileable noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Value noise that wraps every <paramref name="period"/> cells, so the tile has no seams.</summary>
        static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(ix, period), x1 = Wrap(ix + 1, period), y0 = Wrap(iy, period), y1 = Wrap(iy + 1, period);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Wrap(int i, int p) => ((i % p) + p) % p;

        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, period, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Distance to the nearest scattered point (0 at a point, about 1 between them), wrapping.</summary>
        static float Worley(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float best = 9f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx, cy = iy + dy;
                float px = cx + Hash(Wrap(cx, period), Wrap(cy, period), seed);
                float py = cy + Hash(Wrap(cx, period), Wrap(cy, period), seed + 1);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Thin wandering cracks: 1 on a crack, 0 elsewhere.</summary>
        static float Crack(float u, float v, int period, int seed)
        {
            float n = Fbm(u, v, period, 4, seed);
            float line = Mathf.Abs(n - 0.5f);
            float mask = Mathf.SmoothStep(0.55f, 0.7f, Fbm(u, v, 3, 2, seed + 5));
            return (1f - Mathf.SmoothStep(0.004f, 0.012f, line)) * mask;
        }

        /// <summary>Short straight scratches, as on a used metal surface.</summary>
        static float Scratches(float u, float v, int seed)
        {
            float best = 0f;
            for (int k = 0; k < 3; k++)
            {
                float angle = Hash(k, 1, seed) * Mathf.PI;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                float ru = u * ca - v * sa, rv = u * sa + v * ca;
                float cell = Mathf.Repeat(rv * 40f, 1f);
                float on = Hash(Mathf.FloorToInt(rv * 40f), k, seed) > 0.85f ? 1f : 0f;
                float along = Noise(Mathf.Repeat(ru, 1f), 0.5f, 6, seed + k);
                float s = on * (1f - Mathf.SmoothStep(0f, 0.08f, Mathf.Abs(cell - 0.5f))) * Mathf.SmoothStep(0.5f, 0.7f, along);
                best = Mathf.Max(best, s);
            }
            return best;
        }

        /// <summary>Panel seams on a grid of n by n panels per tile.</summary>
        static float Seam(float u, float v, int n, float width)
        {
            float fx = Mathf.Repeat(u * n, 1f), fy = Mathf.Repeat(v * n, 1f);
            float d = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy)) / n;
            return 1f - Mathf.SmoothStep(width * 0.5f, width, d);
        }
    }
}
