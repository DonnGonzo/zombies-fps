using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Pooled hit effects: debris chunks, blood droplets, glowing sparks and embers, smoke and dust puffs,
    /// bullet tracers and marks left on the map (<see cref="Decals"/>). Everything is created up front;
    /// nothing is instantiated during play (design doc rule 4).
    /// </summary>
    public class FxSystem : MonoBehaviour
    {
        public static FxSystem Instance { get; private set; }

        public static readonly Color Blood = new Color(0.45f, 0.03f, 0.03f);
        public static readonly Color Dust = new Color(0.55f, 0.53f, 0.5f);
        public static readonly Color Blast = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Spark = new Color(0.7f, 0.45f, 1f);
        public static readonly Color Acid = new Color(0.45f, 0.9f, 0.15f);

        const int ChunkCount = 200;
        const int TracerCount = 24;
        const int PuffCount = 56;

        enum ChunkMode : byte
        {
            /// <summary>Tumbling bits of stuff.</summary>
            Debris,
            /// <summary>Round drops that stretch along their flight (blood, acid).</summary>
            Droplet,
            /// <summary>Fast, thin glowing streaks.</summary>
            Spark,
            /// <summary>Slower glowing bits that float and drift (explosions).</summary>
            Ember,
        }

        struct Chunk
        {
            public Transform T;
            public Renderer R;
            public MeshFilter F;
            public Vector3 Velocity, Spin;
            public float Life, MaxLife, Size, Drag, Gravity;
            public ChunkMode Mode;
        }

        struct Tracer
        {
            public LineRenderer Line;
            public float Life;
        }

        /// <summary>A camera-facing soft puff of smoke, dust, mist or fire that grows and fades.</summary>
        struct Puff
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public Color Color;
            public float Life, MaxLife, From, To, Roll, RollSpeed;
        }

        /// <summary>A camera-facing flipbook (Ivan's Brackeys VFX: fireballs, rolling smoke) played once.</summary>
        struct Sheet
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public float Life, MaxLife, From, To, Roll;
            public int Frames;
        }

        const int SheetCount = 16;
        readonly List<Sheet> _sheets = new List<Sheet>(SheetCount);
        int _nextSheet;
        Material _fireball, _blastSmoke;
        MaterialPropertyBlock _sheetBlock;
        static readonly int FrameId = Shader.PropertyToID("_Frame");

        const int FlashCount = 6;
        const int VoiceCount = 10;

        struct FlashLight
        {
            public Light Light;
            public float Life, MaxLife, Intensity;
        }

        readonly List<FlashLight> _flashes = new List<FlashLight>(FlashCount);
        readonly List<AudioSource> _voices = new List<AudioSource>(VoiceCount);
        int _nextFlash, _nextVoice;

        readonly List<Chunk> _chunks = new List<Chunk>(ChunkCount);
        readonly List<Tracer> _tracers = new List<Tracer>(TracerCount);
        readonly List<Puff> _puffs = new List<Puff>(PuffCount);
        Material _tracerMat;
        int _nextChunk, _nextTracer, _nextPuff;

        Mesh _cube, _sphere;
        Material _bloodA, _bloodB, _dustA, _dustB, _soot, _sparkHot, _sparkWarm, _emberA, _emberB, _acidGlow;
        MaterialPropertyBlock _puffBlock;
        Decals _decals;
        Camera _cam;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            Instance = this;
            _bloodA = Greybox.Lit(new Color(0.3f, 0.012f, 0.016f), Surface.Rubber, true);
            _bloodB = Greybox.Lit(new Color(0.17f, 0.006f, 0.01f), Surface.Rubber, true);
            _dustA = Greybox.Lit(new Color(0.5f, 0.48f, 0.45f), Surface.Concrete, true);
            _dustB = Greybox.Lit(new Color(0.34f, 0.33f, 0.31f), Surface.Concrete, true);
            _soot = Greybox.Lit(new Color(0.09f, 0.085f, 0.08f), Surface.Concrete, true);
            _sparkHot = Greybox.Unlit(new Color(1f, 0.85f, 0.55f));
            _sparkWarm = Greybox.Unlit(new Color(1f, 0.55f, 0.18f));
            _emberA = Greybox.Unlit(new Color(1f, 0.5f, 0.12f));
            _emberB = Greybox.Unlit(new Color(1f, 0.75f, 0.3f));
            _acidGlow = Greybox.Unlit(Acid);

            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere = probe.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(probe);
            for (int i = 0; i < ChunkCount; i++)
            {
                var go = Greybox.Prim(PrimitiveType.Cube, "Chunk", transform, Vector3.zero, Vector3.one * 0.05f, Dust, collider: false);
                Greybox.DisableShadows(go);
                go.SetActive(false);
                var filter = go.GetComponent<MeshFilter>();
                if (_cube == null) _cube = filter.sharedMesh;
                _chunks.Add(new Chunk { T = go.transform, R = go.GetComponent<Renderer>(), F = filter });
            }

            // Smoke and dust: soft camera-facing quads on a plain blended material, tinted per puff.
            var sprites = Shader.Find("Sprites/Default");
            if (sprites != null)
            {
                // Kenney's soft smoke from the Brackeys bundle where it's in the project, else the drawn one.
                var smoke = Resources.Load<Texture2D>("Fx/" + FxSprites.Smoke);
                var puffMat = new Material(sprites) { name = "Puff", mainTexture = smoke != null ? smoke : FxTextures.Puff() };
                _puffBlock = new MaterialPropertyBlock();
                for (int i = 0; i < PuffCount; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = "Puff";
                    go.transform.SetParent(transform, false);
                    DestroyImmediate(go.GetComponent<Collider>());
                    var r = go.GetComponent<Renderer>();
                    r.sharedMaterial = puffMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.SetActive(false);
                    _puffs.Add(new Puff { T = go.transform, R = r });
                }
            }

            _decals = gameObject.AddComponent<Decals>();

            _fireball = FxSprites.Make(FxSprites.Explosion, true, Color.white, 1.6f);
            _blastSmoke = FxSprites.Make(FxSprites.ExplosionSmoke, false, new Color(0.75f, 0.72f, 0.7f, 0.9f));
            if (_fireball != null)
            {
                _sheetBlock = new MaterialPropertyBlock();
                for (int i = 0; i < SheetCount; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = "Flipbook";
                    go.transform.SetParent(transform, false);
                    DestroyImmediate(go.GetComponent<Collider>());
                    var r = go.GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.SetActive(false);
                    _sheets.Add(new Sheet { T = go.transform, R = r });
                }
            }

            var tracerMat = _tracerMat = Greybox.Unlit(new Color(1f, 0.9f, 0.6f, 0.8f));
            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = tracerMat;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = 0.025f;
                line.endWidth = 0.01f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _tracers.Add(new Tracer { Line = line });
            }

            for (int i = 0; i < FlashCount; i++)
            {
                var go = new GameObject("Flash");
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                _flashes.Add(new FlashLight { Light = light });
            }
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("FxAudio");
                go.transform.SetParent(transform, false);
                var src = GameSettings.Effect(go.AddComponent<AudioSource>());
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.minDistance = 4f;
                src.maxDistance = 70f;
                src.rolloffMode = AudioRolloffMode.Linear;
                _voices.Add(src);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>A brief burst of light, for explosions and bangs.</summary>
        public void Flash(Vector3 point, Color color, float range, float duration)
        {
            var f = _flashes[_nextFlash];
            f.Light.transform.position = point + Vector3.up * 0.4f;
            f.Light.color = color;
            f.Light.range = range;
            f.Intensity = 6f;
            f.Light.intensity = f.Intensity;
            f.Light.enabled = true;
            f.MaxLife = f.Life = Mathf.Max(0.05f, duration);
            _flashes[_nextFlash] = f;
            _nextFlash = (_nextFlash + 1) % _flashes.Count;
        }

        /// <summary>A sound in the world, from a small pool of 3D sources.</summary>
        public void PlayAt(Vector3 point, AudioClip clip, float volume)
        {
            if (clip == null) return;
            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            src.transform.position = point;
            src.pitch = Random.Range(0.95f, 1.05f);
            src.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// A burst of bits where something was hit. The colour picks the look: Blood sprays droplets and leaves
        /// splats on the ground (a pool under a kill), Dust chips the surface and leaves a bullet hole (sparks off
        /// metal), Blast throws embers and smoke and scorches the ground, Spark, Acid and white glow.
        /// </summary>
        public void Impact(Vector3 point, Vector3 normal, Color color, int count, float speed = 3f, float size = 0.06f)
        {
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();
            if (color == Blood) BloodHit(point, normal, count, speed, size);
            else if (color == Dust) DustHit(point, normal, count, speed, size);
            else if (color == Blast) BlastHit(point, normal, count, speed, size);
            else if (color == Acid)
            {
                for (int i = 0; i < count; i++)
                    Emit(ChunkMode.Droplet, _acidGlow, point, normal, 0.8f, speed, size * 0.8f, 0.4f, 0.75f);
            }
            else if (color == Spark || color == Color.white)
            {
                var mat = Greybox.Unlit(color);
                for (int i = 0; i < count; i++)
                    Emit(ChunkMode.Spark, i % 3 == 0 ? _sparkHot : mat, point, normal, 0.9f, speed * 1.3f, size * 0.7f, 0.15f, 0.4f);
            }
            else
            {
                var mat = Greybox.Lit(color);
                for (int i = 0; i < count; i++) Emit(ChunkMode.Debris, mat, point, normal, 0.8f, speed, size, 0.35f, 0.7f);
            }
        }

        /// <summary>A puddle-shaped acid burn on the ground, lasting about as long as the puddle.</summary>
        public void AcidStain(Vector3 point, float diameter, float life)
        {
            if (Decals.FindSurface(point + Vector3.up * 0.3f, Vector3.down, 1.2f, out var hit))
                _decals.Place(DecalKind.Acid, hit.point, hit.normal, diameter, life);
        }

        float _lastFlesh, _lastSurface;

        void BloodHit(Vector3 point, Vector3 normal, int count, float speed, float size)
        {
            // One wet hit per volley (a shotgun's pellets would otherwise stack ten of them).
            if (Time.time - _lastFlesh > 0.06f)
            {
                _lastFlesh = Time.time;
                PlayAt(point, ProceduralAudio.FleshHit(), count >= 9 ? 0.75f : 0.5f);
            }
            for (int i = 0; i < count; i++)
                Emit(ChunkMode.Droplet, i % 3 == 0 ? _bloodB : _bloodA, point, normal, 0.85f, speed, size * 0.75f, 0.35f, 0.7f);
            bool kill = count >= 12;
            Vector3 drift = normal * 0.35f + Vector3.up * 0.1f;
            EmitPuff(point + normal * 0.05f, drift, new Color(0.32f, 0.02f, 0.02f, 0.6f), size * 2f, size * (kill ? 9f : 6f), Random.Range(0.3f, 0.45f));
            if (kill) EmitPuff(point, drift * 0.5f, new Color(0.25f, 0.015f, 0.015f, 0.5f), size * 3f, size * 12f, 0.6f);

            // Blood lands on the ground below, not on the zombie, and sometimes sprays the wall behind it.
            if (kill || Random.value < 0.75f)
            {
                Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f)) * (kill ? 0.3f : 1f);
                if (Decals.FindSurface(point + jitter + Vector3.up * 0.05f, Vector3.down, 3f, out var floor) && floor.normal.y > 0.5f)
                {
                    float width = kill ? Random.Range(1.3f, 1.9f) : Random.Range(0.35f, 0.7f) * (count >= 8 ? 1.35f : 1f);
                    _decals.Place(kill ? DecalKind.BloodPool : DecalKind.Blood, floor.point, floor.normal, width, kill ? 90f : 60f);
                }
            }
            if (!kill && Mathf.Abs(normal.y) < 0.7f && Random.value < 0.5f
                && Decals.FindSurface(point, -normal, 2.2f, out var wall) && Mathf.Abs(wall.normal.y) < 0.6f)
                _decals.Place(DecalKind.Blood, wall.point, wall.normal, Random.Range(0.4f, 0.85f), 60f);
        }

        void DustHit(Vector3 point, Vector3 normal, int count, float speed, float size)
        {
            if (Time.time - _lastSurface > 0.08f)
            {
                _lastSurface = Time.time;
                PlayAt(point, ProceduralAudio.SurfaceHit, 0.3f);
            }
            for (int i = 0; i < count; i++)
                Emit(ChunkMode.Debris, i % 2 == 0 ? _dustA : _dustB, point, normal, 0.8f, speed, size * 0.8f, 0.35f, 0.7f);
            bool big = count > 6;
            int puffs = big ? 3 : 1;
            for (int i = 0; i < puffs; i++)
                EmitPuff(point + normal * 0.04f, normal * 0.3f + Random.insideUnitSphere * 0.15f + Vector3.up * 0.1f,
                    new Color(0.62f, 0.6f, 0.56f, big ? 0.5f : 0.4f), size * 1.5f, size * (big ? 14f : 7f), Random.Range(0.6f, 0.95f));

            // A bullet's mark (bigger bursts are doors and breakdowns, not shots; the knife's little puff is skipped).
            if (big || size < 0.045f || !Decals.FindSurface(point + normal * 0.05f, -normal, 0.15f, out var hit)) return;
            _decals.Place(DecalKind.BulletHole, hit.point, hit.normal, Random.Range(0.07f, 0.11f), 45f);
            if (IsMetal(hit.collider))
                for (int i = 0; i < 5; i++)
                    Emit(ChunkMode.Spark, i % 2 == 0 ? _sparkHot : _sparkWarm, point, normal, 0.7f, Random.Range(5f, 9f), 0.02f, 0.08f, 0.25f);
        }

        static bool IsMetal(Collider c)
        {
            var r = c.GetComponent<Renderer>();
            var m = r != null ? r.sharedMaterial : null;
            return m != null && (m.name.StartsWith("Metal") || m.name.StartsWith("Rust"));
        }

        void BlastHit(Vector3 point, Vector3 normal, int count, float speed, float size)
        {
            float scale = size / 0.09f;
            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0) Emit(ChunkMode.Ember, i % 4 == 0 ? _emberB : _emberA, point, normal, 1f, speed, size * 0.6f, 0.4f, 0.9f);
                else Emit(ChunkMode.Debris, i % 3 == 0 ? _soot : _dustB, point, normal, 0.9f, speed * 0.8f, size, 0.5f, 0.9f);
            }
            // A fireball (drawn flipbook where it's in the project), then rolling smoke.
            if (_fireball != null)
            {
                Play(_fireball, point + normal * 0.3f * scale, Vector3.up * 0.4f, 1.6f * scale, 2.6f * scale, 0.9f);
                if (_blastSmoke != null) Play(_blastSmoke, point + normal * 0.4f * scale, Vector3.up * 0.7f, 2f * scale, 3.6f * scale, 2.4f);
            }
            else for (int i = 0; i < 3; i++)
                EmitPuff(point + Random.insideUnitSphere * 0.15f * scale, Random.insideUnitSphere * 0.6f, new Color(1f, 0.62f, 0.22f, 0.9f),
                    0.25f * scale, Random.Range(1f, 1.5f) * scale, Random.Range(0.18f, 0.28f));
            int smoke = 2 + count / 6;
            for (int i = 0; i < smoke; i++)
                EmitPuff(point + Random.insideUnitSphere * 0.3f * scale, Vector3.up * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.5f,
                    new Color(0.13f, 0.12f, 0.11f, 0.6f), 0.4f * scale, Random.Range(1.6f, 2.4f) * scale, Random.Range(1.2f, 2.2f));

            if (Decals.FindSurface(point + normal * 0.15f, -normal, 0.4f, out var hit)
                || Decals.FindSurface(point + Vector3.up * 0.2f, Vector3.down, 3f, out hit))
                _decals.Place(DecalKind.Scorch, hit.point, hit.normal, Mathf.Clamp(0.25f + count * 0.07f, 0.8f, 3f), 75f);
        }

        void Emit(ChunkMode mode, Material mat, Vector3 point, Vector3 normal, float spread, float speed, float size, float minLife, float maxLife)
        {
            int index = _nextChunk;
            _nextChunk = (_nextChunk + 1) % _chunks.Count;
            var c = _chunks[index];
            c.Mode = mode;
            c.R.sharedMaterial = mat;
            c.F.sharedMesh = mode == ChunkMode.Droplet ? _sphere : _cube;
            c.T.position = point + normal * 0.03f;
            c.T.rotation = Random.rotation;
            c.Size = size * Random.Range(0.6f, 1.4f);
            c.T.localScale = Vector3.one * c.Size;
            c.Velocity = (normal + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.5f, 1.2f);
            c.Spin = Random.insideUnitSphere * 720f;
            c.Drag = mode == ChunkMode.Ember ? 2.2f : mode == ChunkMode.Spark ? 1f : 0.3f;
            c.Gravity = mode == ChunkMode.Ember ? 0.25f : mode == ChunkMode.Spark ? 0.6f : 1f;
            c.MaxLife = c.Life = Random.Range(minLife, maxLife);
            c.T.gameObject.SetActive(true);
            _chunks[index] = c;
        }

        void EmitPuff(Vector3 point, Vector3 velocity, Color color, float from, float to, float life)
        {
            if (_puffs.Count == 0) return;
            int index = _nextPuff;
            _nextPuff = (_nextPuff + 1) % _puffs.Count;
            var p = _puffs[index];
            p.T.position = point;
            p.Velocity = velocity;
            p.Color = color;
            p.From = from;
            p.To = to;
            p.MaxLife = p.Life = life;
            p.Roll = Random.Range(0f, 360f);
            p.RollSpeed = Random.Range(-60f, 60f);
            p.T.localScale = Vector3.one * from;
            // Fades in over its first few frames (see Update).
            _puffBlock.SetColor(ColorId, new Color(color.r, color.g, color.b, 0f));
            p.R.SetPropertyBlock(_puffBlock);
            p.T.gameObject.SetActive(true);
            _puffs[index] = p;
        }

        /// <summary>Plays flipbook material <paramref name="mat"/> once, facing the camera, growing as it goes.</summary>
        void Play(Material mat, Vector3 point, Vector3 velocity, float from, float to, float life)
        {
            if (_sheets.Count == 0 || mat == null) return;
            int index = _nextSheet;
            _nextSheet = (_nextSheet + 1) % _sheets.Count;
            var s = _sheets[index];
            var grid = mat.GetVector("_Grid");
            s.Frames = Mathf.Max(1, (int)(grid.x * grid.y));
            s.R.sharedMaterial = mat;
            s.T.position = point;
            s.Velocity = velocity;
            s.From = from;
            s.To = to;
            s.MaxLife = s.Life = life;
            s.Roll = Random.Range(0f, 360f);
            s.T.localScale = Vector3.one * from;
            _sheetBlock.SetFloat(FrameId, 0f);
            s.R.SetPropertyBlock(_sheetBlock);
            s.T.gameObject.SetActive(true);
            _sheets[index] = s;
        }

        public void ShowTracer(Vector3 from, Vector3 to) => ShowTracer(from, to, Color.clear, false);

        /// <summary>
        /// A tracer in an upgraded gun's glow colour (clear for the usual warm streak). A bullet that went
        /// through zombies leaves a heavier streak that lingers a moment longer.
        /// </summary>
        public void ShowTracer(Vector3 from, Vector3 to, Color glow, bool piercing)
        {
            var tr = _tracers[_nextTracer];
            tr.Line.SetPosition(0, from);
            tr.Line.SetPosition(1, to);
            tr.Line.sharedMaterial = glow.a > 0f ? Greybox.Unlit(new Color(glow.r, glow.g, glow.b, 0.85f)) : _tracerMat;
            tr.Line.startWidth = piercing ? 0.05f : 0.025f;
            tr.Line.endWidth = piercing ? 0.025f : 0.01f;
            tr.Line.enabled = true;
            tr.Life = piercing ? 0.09f : 0.045f;
            _tracers[_nextTracer] = tr;
            _nextTracer = (_nextTracer + 1) % _tracers.Count;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _chunks.Count; i++)
            {
                var c = _chunks[i];
                if (c.Life <= 0f) continue;
                c.Life -= dt;
                if (c.Life <= 0f)
                {
                    c.T.gameObject.SetActive(false);
                }
                else
                {
                    c.Velocity += Physics.gravity * (c.Gravity * dt);
                    c.Velocity *= 1f / (1f + c.Drag * dt);
                    c.T.position += c.Velocity * dt;
                    float left = c.Life / c.MaxLife;
                    if (c.Mode == ChunkMode.Debris)
                    {
                        // Full size most of the way, shrinking away at the end.
                        c.T.Rotate(c.Spin * dt, Space.Self);
                        c.T.localScale = Vector3.one * (c.Size * Mathf.Clamp01(left * 3f));
                    }
                    else
                    {
                        // Drops and sparks streak along the way they're flying.
                        float v = c.Velocity.magnitude;
                        if (v > 0.01f) c.T.rotation = Quaternion.LookRotation(c.Velocity / v);
                        float s = c.Size * (c.Mode == ChunkMode.Droplet ? Mathf.Clamp01(left * 2.5f) : left);
                        float thin = c.Mode == ChunkMode.Droplet ? 1f : 0.3f;
                        float stretch = c.Mode == ChunkMode.Droplet ? 1f + v * 0.12f : c.Mode == ChunkMode.Spark ? 2f + v * 0.9f : 1.5f + v * 0.5f;
                        c.T.localScale = new Vector3(s * thin, s * thin, s * stretch);
                    }
                }
                _chunks[i] = c;
            }

            if (_puffs.Count > 0)
            {
                if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
                var camRot = _cam != null ? _cam.transform.rotation : Quaternion.identity;
                for (int i = 0; i < _puffs.Count; i++)
                {
                    var p = _puffs[i];
                    if (p.Life <= 0f) continue;
                    p.Life -= dt;
                    if (p.Life <= 0f)
                    {
                        p.T.gameObject.SetActive(false);
                        _puffs[i] = p;
                        continue;
                    }
                    float t = 1f - p.Life / p.MaxLife;
                    p.Velocity *= 1f / (1f + 1.5f * dt);
                    p.T.position += p.Velocity * dt;
                    p.Roll += p.RollSpeed * dt;
                    p.T.rotation = camRot * Quaternion.Euler(0f, 0f, p.Roll);
                    float grow = 1f - (1f - t) * (1f - t);
                    p.T.localScale = Vector3.one * Mathf.Lerp(p.From, p.To, grow);
                    float alpha = p.Color.a * Mathf.Clamp01(t * 8f) * (1f - t) * (1f - t);
                    _puffBlock.SetColor(ColorId, new Color(p.Color.r, p.Color.g, p.Color.b, alpha));
                    p.R.SetPropertyBlock(_puffBlock);
                    _puffs[i] = p;
                }
            }

            if (_sheets.Count > 0)
            {
                if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
                var camRot = _cam != null ? _cam.transform.rotation : Quaternion.identity;
                for (int i = 0; i < _sheets.Count; i++)
                {
                    var s = _sheets[i];
                    if (s.Life <= 0f) continue;
                    s.Life -= dt;
                    if (s.Life <= 0f)
                    {
                        s.T.gameObject.SetActive(false);
                        _sheets[i] = s;
                        continue;
                    }
                    float t = 1f - s.Life / s.MaxLife;
                    s.Velocity *= 1f / (1f + 1.2f * dt);
                    s.T.position += s.Velocity * dt;
                    s.T.rotation = camRot * Quaternion.Euler(0f, 0f, s.Roll);
                    s.T.localScale = Vector3.one * Mathf.Lerp(s.From, s.To, 1f - (1f - t) * (1f - t));
                    _sheetBlock.SetFloat(FrameId, Mathf.Min(s.Frames - 1, t * s.Frames));
                    s.R.SetPropertyBlock(_sheetBlock);
                    _sheets[i] = s;
                }
            }

            for (int i = 0; i < _flashes.Count; i++)
            {
                var f = _flashes[i];
                if (f.Life <= 0f) continue;
                f.Life -= dt;
                if (f.Life <= 0f) f.Light.enabled = false;
                else f.Light.intensity = f.Intensity * (f.Life / f.MaxLife);
                _flashes[i] = f;
            }

            for (int i = 0; i < _tracers.Count; i++)
            {
                var tr = _tracers[i];
                if (tr.Life <= 0f) continue;
                tr.Life -= dt;
                if (tr.Life <= 0f) tr.Line.enabled = false;
                _tracers[i] = tr;
            }
        }
    }
}
