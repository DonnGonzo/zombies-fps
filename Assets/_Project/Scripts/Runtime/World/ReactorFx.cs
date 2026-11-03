using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Lantern Core reactor's easter egg look, on every machine. Dormant it's as the map built it. Waking (a
    /// step running) it glows white-blue, a charge light throbs, lightning arcs from it to the tesla coils, the
    /// ceiling and the floor, and every zombie of the trial climbs out where a bolt lands. Awake (step one done)
    /// it keeps a steady glow and throws the odd arc to the coils.
    /// </summary>
    public class ReactorFx : MonoBehaviour
    {
        public enum State { Dormant, Waking, Awake }

        const int BoltCount = 10;
        const int BoltPoints = 14;
        const float BodyRadius = 1.05f;
        static readonly Color BoltColor = new Color(0.75f, 0.92f, 1f);
        static readonly Color ChargeColor = new Color(0.45f, 0.8f, 1f);

        struct Bolt
        {
            public LineRenderer line;
            public Vector3 from, to;
            public float until, nextJitter, width;
        }

        readonly Bolt[] _bolts = new Bolt[BoltCount];
        readonly List<Vector3> _coilTops = new List<Vector3>();
        readonly List<Renderer> _coilGlows = new List<Renderer>();
        Vector3 _base;
        Light _charge;
        Material _shellMat, _coilMat;
        Renderer _shell;
        AudioSource _audio, _surge;
        State _state;
        float _stateAt;
        float _nextArc;

        public static ReactorFx Build(Transform parent, Vector3 reactorBase, IReadOnlyList<Vector3> coilTops)
        {
            var go = new GameObject("Reactor Fx");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = reactorBase;
            var fx = go.AddComponent<ReactorFx>();
            fx._base = reactorBase;
            if (coilTops != null) fx._coilTops.AddRange(coilTops);

            fx._shellMat = new Material(Greybox.Unlit(new Color(ChargeColor.r, ChargeColor.g, ChargeColor.b, 0.2f)));
            var shell = Greybox.Prim(PrimitiveType.Cylinder, "Charge Shell", go.transform, Vector3.up * 3f, new Vector3(BodyRadius * 2.2f, 2.9f, BodyRadius * 2.2f), ChargeColor, collider: false);
            fx._shell = shell.GetComponent<Renderer>();
            fx._shell.sharedMaterial = fx._shellMat;
            fx._shell.shadowCastingMode = ShadowCastingMode.Off;
            fx._shell.enabled = false;

            fx._coilMat = new Material(Greybox.Unlit(new Color(ChargeColor.r, ChargeColor.g, ChargeColor.b, 0.5f)));
            foreach (var top in fx._coilTops)
            {
                var ball = Greybox.Prim(PrimitiveType.Sphere, "Coil Charge", go.transform, top - reactorBase, Vector3.one * 0.62f, ChargeColor, collider: false);
                var r = ball.GetComponent<Renderer>();
                r.sharedMaterial = fx._coilMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.enabled = false;
                fx._coilGlows.Add(r);
            }

            fx._charge = new GameObject("Charge Light").AddComponent<Light>();
            fx._charge.transform.SetParent(go.transform, false);
            fx._charge.transform.localPosition = Vector3.up * 3.5f;
            fx._charge.type = LightType.Point;
            fx._charge.color = ChargeColor;
            fx._charge.range = 22f;
            fx._charge.intensity = 0f;
            fx._charge.shadows = LightShadows.None;
            fx._charge.enabled = false;

            var boltMat = Greybox.Unlit(BoltColor);
            for (int i = 0; i < BoltCount; i++)
            {
                var line = new GameObject("Bolt").AddComponent<LineRenderer>();
                line.transform.SetParent(go.transform, false);
                line.useWorldSpace = true;
                line.positionCount = BoltPoints;
                line.sharedMaterial = boltMat;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCapVertices = 2;
                line.enabled = false;
                fx._bolts[i].line = line;
            }

            fx._audio = GameSettings.Effect(go.AddComponent<AudioSource>());
            fx._audio.spatialBlend = 0.85f;
            fx._audio.minDistance = 4f;
            fx._audio.maxDistance = 45f;
            fx._audio.rolloffMode = AudioRolloffMode.Linear;
            fx._audio.playOnAwake = false;
            fx._surge = GameSettings.Effect(go.AddComponent<AudioSource>());
            fx._surge.clip = ProceduralAudio.ReactorSurge;
            fx._surge.loop = true;
            fx._surge.spatialBlend = 0.9f;
            fx._surge.minDistance = 5f;
            fx._surge.maxDistance = 40f;
            fx._surge.rolloffMode = AudioRolloffMode.Linear;
            fx._surge.playOnAwake = false;
            return fx;
        }

        public void SetState(State state)
        {
            if (state == _state) return;
            bool wasWaking = _state == State.Waking;
            _state = state;
            _stateAt = Time.time;
            _shell.enabled = state != State.Dormant;
            _charge.enabled = state != State.Dormant;
            foreach (var r in _coilGlows) r.enabled = state != State.Dormant;
            if (state == State.Waking)
            {
                _surge.Play();
                _audio.PlayOneShot(ProceduralAudio.Thunder, 1f);
                if (FxSystem.Instance != null) FxSystem.Instance.Flash(_base + Vector3.up * 4f, BoltColor, 30f, 0.4f);
            }
            else if (_surge.isPlaying) _surge.Stop();
            if (wasWaking && state == State.Awake)
            {
                // The last of them down: one big discharge.
                _audio.PlayOneShot(ProceduralAudio.Thunder, 1f);
                for (int i = 0; i < _coilTops.Count; i++) Fire(BodyPoint(), _coilTops[i], 0.35f, 0.09f);
            }
        }

        /// <summary>A bolt from the reactor's top down to a point on the floor, with a flash and a crack.</summary>
        public void Strike(Vector3 at)
        {
            Fire(_base + new Vector3(Random.Range(-0.4f, 0.4f), 5.6f, Random.Range(-0.4f, 0.4f)), at + Vector3.up * 0.05f, 0.3f, 0.08f);
            if (FxSystem.Instance != null)
            {
                FxSystem.Instance.Flash(at + Vector3.up * 0.6f, BoltColor, 9f, 0.25f);
                FxSystem.Instance.Impact(at + Vector3.up * 0.1f, Vector3.up, BoltColor, 14, 4f, 0.05f);
                FxSystem.Instance.PlayAt(at, ProceduralAudio.Zap, 0.9f);
            }
        }

        Vector3 BodyPoint()
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            return _base + new Vector3(Mathf.Cos(a) * BodyRadius, Random.Range(1.2f, 5.4f), Mathf.Sin(a) * BodyRadius);
        }

        void Fire(Vector3 from, Vector3 to, float life, float width)
        {
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < BoltCount; i++)
                if (_bolts[i].until < oldest)
                {
                    oldest = _bolts[i].until;
                    slot = i;
                }
            ref var b = ref _bolts[slot];
            b.from = from;
            b.to = to;
            b.until = Time.time + life;
            b.nextJitter = 0f;
            b.width = width;
            b.line.enabled = true;
        }

        /// <summary>A random arc for the waking reactor: to a coil, up to the ceiling, or down to the floor nearby.</summary>
        void RandomArc()
        {
            float roll = Random.value;
            Vector3 to;
            if (_coilTops.Count > 0 && roll < 0.55f) to = _coilTops[Random.Range(0, _coilTops.Count)];
            else if (roll < 0.78f) to = _base + new Vector3(Random.Range(-4f, 4f), ZoneMap.WallHeight - 0.05f, Random.Range(-4f, 4f));
            else
            {
                float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(1.8f, 4.5f);
                to = _base + new Vector3(Mathf.Cos(a) * d, 0.03f, Mathf.Sin(a) * d);
            }
            Fire(BodyPoint(), to, Random.Range(0.08f, 0.2f), Random.Range(0.04f, 0.08f));
            if (Random.value < 0.35f) _audio.PlayOneShot(ProceduralAudio.Zap, Random.Range(0.35f, 0.7f));
        }

        void Update()
        {
            float t = Time.time;
            if (_state == State.Waking)
            {
                float rise = Mathf.Clamp01((t - _stateAt) / 3f);
                float throb = 0.75f + Mathf.Sin(t * 9f) * 0.15f + Mathf.PerlinNoise(t * 13f, 1f) * 0.3f;
                _charge.intensity = 4.5f * rise * throb;
                SetAlpha(_shellMat, (0.12f + 0.12f * throb) * rise);
                SetAlpha(_coilMat, 0.35f + 0.4f * Mathf.PerlinNoise(t * 11f, 3f));
                _surge.pitch = 0.9f + 0.15f * rise;
                if (t >= _nextArc)
                {
                    RandomArc();
                    _nextArc = t + Random.Range(0.05f, 0.25f) / Mathf.Max(0.3f, rise);
                }
            }
            else if (_state == State.Awake)
            {
                _charge.intensity = 2.2f + Mathf.Sin(t * 1.6f) * 0.4f;
                SetAlpha(_shellMat, 0.1f + Mathf.Sin(t * 1.6f) * 0.03f);
                SetAlpha(_coilMat, 0.25f + Mathf.Sin(t * 2.3f) * 0.08f);
                if (t >= _nextArc && _coilTops.Count > 0)
                {
                    Fire(BodyPoint(), _coilTops[Random.Range(0, _coilTops.Count)], 0.12f, 0.04f);
                    _audio.PlayOneShot(ProceduralAudio.Zap, 0.3f);
                    _nextArc = t + Random.Range(2f, 5f);
                }
            }

            for (int i = 0; i < BoltCount; i++)
            {
                ref var b = ref _bolts[i];
                if (!b.line.enabled) continue;
                if (t >= b.until)
                {
                    b.line.enabled = false;
                    continue;
                }
                if (t < b.nextJitter) continue;
                // Re-draw the jagged path every few frames, so the bolt crawls while it lasts.
                b.nextJitter = t + 0.03f;
                Jag(b.line, b.from, b.to);
                float w = b.width * Random.Range(0.6f, 1.2f);
                b.line.startWidth = w;
                b.line.endWidth = w * 0.4f;
            }
        }

        static void Jag(LineRenderer line, Vector3 from, Vector3 to)
        {
            var d = to - from;
            float len = d.magnitude;
            var side = Vector3.Cross(d, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            var up = Vector3.Cross(side, d).normalized;
            for (int i = 0; i < BoltPoints; i++)
            {
                float k = i / (float)(BoltPoints - 1);
                // Loose in the middle, pinned at the ends.
                float sway = Mathf.Sin(k * Mathf.PI) * len * 0.08f;
                var p = from + d * k;
                if (i > 0 && i < BoltPoints - 1) p += side * Random.Range(-sway, sway) + up * Random.Range(-sway, sway);
                line.SetPosition(i, p);
            }
        }

        static void SetAlpha(Material m, float a)
        {
            var c = m.color;
            c.a = Mathf.Clamp01(a);
            m.color = c;
        }

        void OnDestroy()
        {
            if (_shellMat != null) Destroy(_shellMat);
            if (_coilMat != null) Destroy(_coilMat);
        }
    }
}
