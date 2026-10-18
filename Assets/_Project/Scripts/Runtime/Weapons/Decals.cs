using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Net;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Weapons
{
    public enum DecalKind
    {
        Blood,
        BloodPool,
        BulletHole,
        Scorch,
        Acid,
    }

    /// <summary>
    /// Pooled marks left on the map: blood splats, pools under kills, bullet holes, scorch marks and acid
    /// stains. A fixed set of quads is made up front and the oldest is reused; each mark dulls a little with
    /// time and fades out. Marks only go on static map pieces, never on zombies, players or doors that move.
    /// Purely visual. If the decal shader can't run here, nothing is placed.
    /// </summary>
    public class Decals : MonoBehaviour
    {
        const int Count = 160;
        const float FadeOut = 4f;

        struct Decal
        {
            public Transform T;
            public Renderer R;
            public float Born, Life, Size, Aspect, Alpha;
            public Vector4 Cell;
            public Color Tint;
            public bool Grows;
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CellId = Shader.PropertyToID("_MainTex_ST");
        static readonly RaycastHit[] s_Hits = new RaycastHit[12];

        readonly List<Decal> _decals = new List<Decal>(Count);
        MaterialPropertyBlock _block;
        Material _wet, _dry;
        int _next;

        void Awake()
        {
            var baseMat = Resources.Load<Material>("Materials/Decal");
            if (baseMat == null || baseMat.shader == null || !baseMat.shader.isSupported) return;
            var atlas = FxTextures.DecalAtlas();
            _wet = new Material(baseMat) { name = "Decal Wet", mainTexture = atlas };
            _wet.SetFloat("_Glossiness", 0.75f);
            _dry = new Material(baseMat) { name = "Decal Dry", mainTexture = atlas };
            _dry.SetFloat("_Glossiness", 0.08f);
            _block = new MaterialPropertyBlock();
            var mesh = QuadMesh();
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Decal");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _dry;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                go.SetActive(false);
                _decals.Add(new Decal { T = go.transform, R = r });
            }
        }

        /// <summary>A flat 1 m square facing +Y, with white vertex colours (the decal shader multiplies by them).</summary>
        static Mesh QuadMesh()
        {
            var mesh = new Mesh { name = "Decal Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
            mesh.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Map pieces only: not triggers, zombies, players, doors (they sink away) or anything physics moves.
        /// </summary>
        public static bool IsStatic(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return false;
            if (c.GetComponent<Hitbox>() != null) return false;
            return c.GetComponentInParent<NetZombie>() == null && c.GetComponentInParent<NetPlayer>() == null
                   && c.GetComponentInParent<Door>() == null;
        }

        /// <summary>The nearest static map surface along a ray, skipping zombies and players in the way.</summary>
        public static bool FindSurface(Vector3 origin, Vector3 dir, float distance, out RaycastHit hit)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(origin, dir, s_Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (s_Hits[i].distance >= best || !IsStatic(s_Hits[i].collider)) continue;
                best = s_Hits[i].distance;
                hit = s_Hits[i];
                found = true;
            }
            return found;
        }

        /// <summary>Lays a mark flat on a surface. Size is its width in metres; it lasts about life seconds.</summary>
        public void Place(DecalKind kind, Vector3 point, Vector3 normal, float size, float life)
        {
            if (_decals.Count == 0 || normal.sqrMagnitude < 0.0001f) return;
            normal.Normalize();
            int index = _next;
            _next = (_next + 1) % _decals.Count;
            var d = _decals[index];

            Vector3 side = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            var rot = Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Quaternion.LookRotation(side, normal);
            // A few millimetres off the surface, staggered so overlapping marks don't flicker against each other.
            d.T.SetPositionAndRotation(point + normal * (0.004f + (index % 6) * 0.0008f), rot);

            int variant = Random.Range(0, 4);
            int cell = kind switch
            {
                DecalKind.Blood => variant,
                DecalKind.BulletHole => 4 + variant,
                DecalKind.Scorch => 8 + variant,
                DecalKind.BloodPool => 12 + (variant & 1),
                _ => 14 + (variant & 1),
            };
            const float c = 1f / FxTextures.AtlasCells;
            d.Cell = new Vector4(c, c, (cell % FxTextures.AtlasCells) * c, (cell / FxTextures.AtlasCells) * c);
            d.Size = size;
            d.Aspect = kind == DecalKind.Blood || kind == DecalKind.Scorch ? Random.Range(0.8f, 1.25f) : 1f;
            d.Grows = kind == DecalKind.BloodPool || kind == DecalKind.Acid;
            d.Born = Time.time;
            d.Life = Mathf.Max(FadeOut + 1f, life);
            float shade = Random.Range(0.85f, 1f);
            d.Tint = new Color(shade, shade, shade, 1f);
            d.Alpha = -1f;
            d.R.sharedMaterial = kind == DecalKind.BulletHole || kind == DecalKind.Scorch ? _dry : _wet;
            _decals[index] = d;
            Apply(index, 0f);
            d.T.gameObject.SetActive(true);
        }

        void Apply(int index, float age)
        {
            var d = _decals[index];
            // Dulls slowly, then fades away at the end.
            float alpha = Mathf.Lerp(1f, 0.72f, age / d.Life) * Mathf.Clamp01((d.Life - age) / FadeOut);
            float grow = d.Grows ? Mathf.Lerp(0.35f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 1.6f), 2f)) : 1f;
            d.T.localScale = new Vector3(d.Size * d.Aspect * grow, 1f, d.Size / d.Aspect * grow);
            if (Mathf.Abs(alpha - d.Alpha) < 0.01f) return;
            d.Alpha = alpha;
            _decals[index] = d;
            _block.Clear();
            _block.SetVector(CellId, d.Cell);
            _block.SetColor(ColorId, new Color(d.Tint.r, d.Tint.g, d.Tint.b, alpha));
            d.R.SetPropertyBlock(_block);
        }

        void Update()
        {
            if (_decals.Count == 0) return;
            float now = Time.time;
            int phase = Time.frameCount & 3;
            for (int i = 0; i < _decals.Count; i++)
            {
                var d = _decals[i];
                if (!d.T.gameObject.activeSelf) continue;
                float age = now - d.Born;
                if (age >= d.Life)
                {
                    d.T.gameObject.SetActive(false);
                    continue;
                }
                // Spreading puddles update every frame; the rest a quarter of them each frame.
                bool growing = d.Grows && age < 1.7f;
                if (growing || (i & 3) == phase || age > d.Life - FadeOut) Apply(i, age);
            }
        }
    }

    /// <summary>Textures for the hit effects, drawn in code at start: the decal atlas and a soft smoke puff.</summary>
    public static class FxTextures
    {
        public const int AtlasCells = 4;
        const int Cell = 128;

        /// <summary>
        /// Four by four cells: row 0 blood splats, row 1 bullet holes, row 2 scorch marks, row 3 two blood
        /// pools and two acid stains. Colour is baked in; alpha is soft at the edges and clear at cell borders.
        /// </summary>
        public static Texture2D DecalAtlas()
        {
            int size = Cell * AtlasCells;
            var px = new Color32[size * size];
            for (int cell = 0; cell < AtlasCells * AtlasCells; cell++)
            {
                int cx = cell % AtlasCells, cy = cell / AtlasCells;
                var rng = new System.Random(cell * 7717 + 31);
                var p = new CellParams(rng);
                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    // -1..1 across the cell.
                    float u = (x + 0.5f) / Cell * 2f - 1f, v = (y + 0.5f) / Cell * 2f - 1f;
                    Color c = cy switch
                    {
                        0 => BloodSplat(u, v, p),
                        1 => BulletHole(u, v, p),
                        2 => Scorch(u, v, p),
                        _ => cx < 2 ? BloodPool(u, v, p) : AcidStain(u, v, p),
                    };
                    // Keep cell borders clear so mipmaps don't bleed neighbours in.
                    float edge = Mathf.Min(Mathf.Min(x, Cell - 1 - x), Mathf.Min(y, Cell - 1 - y));
                    if (edge < 3f) c.a = 0f;
                    px[(cy * Cell + y) * size + cx * Cell + x] = c;
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "Decal Atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>A soft, slightly lumpy white puff for smoke, dust and mist (tinted per particle).</summary>
        public static Texture2D Puff()
        {
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float noise = Mathf.PerlinNoise(x * 0.11f + 3.1f, y * 0.11f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.27f + 11f, y * 0.27f + 2f) * 0.4f;
                float a = 1f - Mathf.SmoothStep(0.2f, 1f, d + (noise - 0.5f) * 0.45f);
                a *= 0.75f + 0.25f * noise;
                float g = 0.82f + 0.18f * noise;
                px[y * n + x] = new Color(g, g, g, Mathf.Clamp01(a));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = "Puff", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Random shape settings for one cell.</summary>
        readonly struct CellParams
        {
            public readonly float P1, P2, P3, Seed;
            public readonly Vector3[] Drops;
            public readonly Vector2[] Rays;

            public CellParams(System.Random rng)
            {
                float R() => (float)rng.NextDouble();
                P1 = R() * 6.28f;
                P2 = R() * 6.28f;
                P3 = R() * 6.28f;
                Seed = R() * 100f;
                Drops = new Vector3[12];
                for (int i = 0; i < Drops.Length; i++)
                {
                    float a = R() * 6.28f, dist = 0.45f + R() * 0.42f;
                    Drops[i] = new Vector3(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist, 0.015f + R() * 0.05f * (1f - dist * 0.6f));
                }
                Rays = new Vector2[6];
                for (int i = 0; i < Rays.Length; i++) Rays[i] = new Vector2(R() * 6.28f, 0.55f + R() * 0.35f);
            }
        }

        static float Noise(float u, float v, float freq, float seed) => Mathf.PerlinNoise(u * freq + seed, v * freq + seed * 1.37f);

        static float Wobble(float angle, in CellParams p, float amount)
            => 1f + amount * (0.5f * Mathf.Sin(3f * angle + p.P1) + 0.3f * Mathf.Sin(5f * angle + p.P2) + 0.2f * Mathf.Sin(11f * angle + p.P3));

        /// <summary>Alpha of the shape's rays: thin streaks out from the middle that taper to a point.</summary>
        static float RayAlpha(float angle, float d, in CellParams p, float width)
        {
            float best = 0f;
            foreach (var ray in p.Rays)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, ray.x * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (d > ray.y) continue;
                float taper = 1f - d / ray.y;
                float w = width * taper;
                float a = 1f - Mathf.SmoothStep(w * 0.6f, w, diff * d);
                best = Mathf.Max(best, a * Mathf.Clamp01(taper * 3f));
            }
            return best;
        }

        static Color BloodSplat(float u, float v, in CellParams p)
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            float r = 0.36f * Wobble(angle, p, 0.32f) * (0.9f + 0.2f * Noise(u, v, 3f, p.Seed));
            float a = Mathf.Clamp01((r - d) * 22f + 0.5f);
            a = Mathf.Max(a, RayAlpha(angle, d, p, 0.07f));
            foreach (var drop in p.Drops)
            {
                float dx = u - drop.x, dy = v - drop.y;
                a = Mathf.Max(a, Mathf.Clamp01((drop.z - Mathf.Sqrt(dx * dx + dy * dy)) * 60f + 0.5f));
            }
            float thick = Mathf.Clamp01(1f - d / Mathf.Max(0.05f, r));
            float n = Noise(u, v, 6f, p.Seed + 5f);
            var c = Color.Lerp(new Color(0.42f, 0.035f, 0.03f), new Color(0.17f, 0.008f, 0.01f), thick * 0.8f + n * 0.2f);
            c.a = a * 0.95f;
            return c;
        }

        static Color BloodPool(float u, float v, in CellParams p)
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            float r = 0.62f * Wobble(angle, p, 0.14f) * (0.92f + 0.12f * Noise(u, v, 2.5f, p.Seed));
            float a = Mathf.Clamp01((r - d) * 14f + 0.5f);
            for (int i = 0; i < 6; i++)
            {
                var drop = p.Drops[i];
                float dx = u - drop.x * 1.05f, dy = v - drop.y * 1.05f;
                a = Mathf.Max(a, Mathf.Clamp01((drop.z * 1.4f - Mathf.Sqrt(dx * dx + dy * dy)) * 50f + 0.5f));
            }
            float thick = Mathf.Clamp01(1f - d / r);
            var c = Color.Lerp(new Color(0.3f, 0.02f, 0.02f), new Color(0.11f, 0.004f, 0.006f), Mathf.Sqrt(thick));
            c.a = a * 0.96f;
            return c;
        }

        static Color BulletHole(float u, float v, in CellParams p)
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            float n = Noise(u, v, 9f, p.Seed);
            float core = 0.17f * Wobble(angle, p, 0.2f);
            float chip = 0.36f * Wobble(angle + 1f, p, 0.35f) * (0.85f + 0.3f * n);
            float soot = 0.85f;
            Color c;
            float a;
            if (d < core)
            {
                c = new Color(0.015f, 0.015f, 0.015f);
                a = 1f;
            }
            else if (d < chip)
            {
                // Broken material round the hole, lighter at the lip where it's freshly chipped.
                float t = (d - core) / Mathf.Max(0.01f, chip - core);
                float g = Mathf.Lerp(0.34f, 0.16f, t) * (0.8f + 0.4f * n);
                c = new Color(g, g * 0.97f, g * 0.93f);
                a = Mathf.Lerp(0.95f, 0.75f, t);
            }
            else
            {
                float t = Mathf.Clamp01((d - chip) / (soot - chip));
                c = new Color(0.06f, 0.055f, 0.05f);
                a = (1f - Mathf.SmoothStep(0f, 1f, t)) * 0.55f * (0.6f + 0.4f * n);
            }
            float crack = RayAlpha(angle, d, p, 0.025f) * 0.8f;
            if (crack > a)
            {
                c = Color.Lerp(c, new Color(0.04f, 0.04f, 0.04f), crack);
                a = crack;
            }
            c.a = a;
            return c;
        }

        static Color Scorch(float u, float v, in CellParams p)
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            float n = Noise(u, v, 4f, p.Seed) * 0.7f + Noise(u, v, 11f, p.Seed + 3f) * 0.3f;
            float streaks = 0.7f + 0.3f * Mathf.Sin(angle * 17f + n * 6f);
            float a = 1f - Mathf.SmoothStep(0.15f, 0.95f, d + (n - 0.5f) * 0.4f);
            a = Mathf.Clamp01(a * (d > 0.3f ? streaks : 1f) * 1.15f);
            // A paler ring of ash in the middle, sooty black around it.
            float ash = 1f - Mathf.SmoothStep(0f, 0.3f, d);
            var c = Color.Lerp(new Color(0.035f, 0.03f, 0.025f), new Color(0.16f, 0.15f, 0.14f), ash * 0.6f * n);
            c.a = a * 0.93f;
            return c;
        }

        static Color AcidStain(float u, float v, in CellParams p)
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            float n = Noise(u, v, 5f, p.Seed);
            float r = 0.68f * Wobble(angle, p, 0.18f) * (0.85f + 0.3f * n);
            float a = Mathf.Clamp01((r - d) * 12f + 0.5f);
            float rim = Mathf.Clamp01(1f - Mathf.Abs(d - r * 0.92f) * 14f);
            var c = Color.Lerp(new Color(0.32f, 0.48f, 0.06f), new Color(0.1f, 0.12f, 0.03f), rim * 0.85f + (1f - n) * 0.15f);
            c.a = a * 0.8f;
            return c;
        }
    }
}
