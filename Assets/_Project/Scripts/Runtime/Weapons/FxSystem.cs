using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Pooled hit effects: debris chunks and bullet tracers. Everything is created up front;
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

        const int ChunkCount = 160;
        const int TracerCount = 24;

        struct Chunk
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public float Life, MaxLife, Size;
        }

        struct Tracer
        {
            public LineRenderer Line;
            public float Life;
        }

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
        Material _tracerMat;
        int _nextChunk, _nextTracer;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < ChunkCount; i++)
            {
                var go = Greybox.Prim(PrimitiveType.Cube, "Chunk", transform, Vector3.zero, Vector3.one * 0.05f, Dust, collider: false);
                Greybox.DisableShadows(go);
                go.SetActive(false);
                _chunks.Add(new Chunk { T = go.transform, R = go.GetComponent<Renderer>() });
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

        public void Impact(Vector3 point, Vector3 normal, Color color, int count, float speed = 3f, float size = 0.06f)
        {
            var mat = Greybox.Lit(color);
            for (int i = 0; i < count; i++)
            {
                int index = _nextChunk;
                _nextChunk = (_nextChunk + 1) % _chunks.Count;
                var c = _chunks[index];
                c.R.sharedMaterial = mat;
                c.T.position = point + normal * 0.03f;
                c.T.rotation = Random.rotation;
                c.Size = size * Random.Range(0.6f, 1.4f);
                c.T.localScale = Vector3.one * c.Size;
                c.Velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.5f, 1.2f);
                c.MaxLife = c.Life = Random.Range(0.35f, 0.7f);
                c.T.gameObject.SetActive(true);
                _chunks[index] = c;
            }
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
                    c.Velocity += Physics.gravity * dt;
                    c.T.position += c.Velocity * dt;
                    c.T.localScale = Vector3.one * (c.Size * (c.Life / c.MaxLife));
                }
                _chunks[i] = c;
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
