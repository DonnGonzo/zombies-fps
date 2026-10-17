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

        readonly List<Chunk> _chunks = new List<Chunk>(ChunkCount);
        readonly List<Tracer> _tracers = new List<Tracer>(TracerCount);
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

            var tracerMat = Greybox.Unlit(new Color(1f, 0.9f, 0.6f, 0.8f));
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        public void ShowTracer(Vector3 from, Vector3 to)
        {
            var tr = _tracers[_nextTracer];
            tr.Line.SetPosition(0, from);
            tr.Line.SetPosition(1, to);
            tr.Line.enabled = true;
            tr.Life = 0.045f;
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
