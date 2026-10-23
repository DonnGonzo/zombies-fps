using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public enum GeneratorState
    {
        Idle,
        Active,
        Done,
    }

    /// <summary>
    /// A generator prop for hold missions, one per zone. Purely local: the mission board tells every machine
    /// which one is in use and how far along it is, and this only changes its lights.
    /// </summary>
    public class Generator : MonoBehaviour
    {
        static readonly Color IdleColor = new Color(0.35f, 0.12f, 0.1f);
        static readonly Color ActiveColor = new Color(1f, 0.7f, 0.2f);
        static readonly Color DoneColor = new Color(0.35f, 1f, 0.45f);

        Renderer _strip;
        Transform _gauge;
        Light _light;
        GeneratorState _state;
        float _progress;
        bool _mains;
        float _mainsAt = -100f;

        public GeneratorState State => _state;
        /// <summary>The start-up generator, once its code is in: it runs (green) whenever no mission is using it.</summary>
        public bool Mains => _mains;

        public void Init()
        {
            Greybox.Box("Body", transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 0.9f), new Color(0.3f, 0.33f, 0.3f));
            Greybox.Box("Exhaust", transform, new Vector3(0.45f, 1.4f, 0.2f), new Vector3(0.15f, 0.4f, 0.15f), new Color(0.2f, 0.2f, 0.2f), collider: false);
            _strip = Greybox.Prim(PrimitiveType.Cube, "Strip", transform, new Vector3(0f, 0.95f, -0.46f), new Vector3(1.1f, 0.12f, 0.02f), IdleColor, collider: false, unlit: true).GetComponent<Renderer>();
            Greybox.Prim(PrimitiveType.Cube, "GaugeBack", transform, new Vector3(0f, 0.6f, -0.46f), new Vector3(1.1f, 0.1f, 0.02f), new Color(0.08f, 0.08f, 0.08f), collider: false, unlit: true);
            _gauge = Greybox.Prim(PrimitiveType.Cube, "Gauge", transform, new Vector3(0f, 0.6f, -0.47f), new Vector3(1.1f, 0.1f, 0.02f), ActiveColor, collider: false, unlit: true).transform;

            var lightGo = new GameObject("Generator Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.8f, -0.8f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 6f;
            _light.shadows = LightShadows.None;
            SetState(GeneratorState.Idle, 0f);
        }

        public void SetState(GeneratorState state, float progress)
        {
            _state = state;
            _progress = Mathf.Clamp01(progress);
            bool running = state == GeneratorState.Done || (state == GeneratorState.Idle && _mains);
            var color = running ? DoneColor : state == GeneratorState.Active ? ActiveColor : IdleColor;
            _strip.sharedMaterial = Greybox.Unlit(color);
            _light.color = color;
            _light.enabled = state != GeneratorState.Idle || _mains;
            _gauge.gameObject.SetActive(state == GeneratorState.Active);
            _gauge.localScale = new Vector3(1.1f * Mathf.Max(0.01f, _progress), 0.1f, 0.02f);
            _gauge.localPosition = new Vector3(-0.55f + 0.55f * _progress, 0.6f, -0.47f);
        }

        /// <summary>Starts (or stops) the generator as the facility's power supply; a mission's state still shows over it.</summary>
        public void SetMains(bool on, bool instant)
        {
            if (on == _mains) return;
            _mains = on;
            _mainsAt = instant ? -100f : Time.time;
            SetState(_state, _progress);
        }

        void Update()
        {
            // An active generator pulses so it's easy to find; one just started stutters before it catches.
            if (_state == GeneratorState.Active) _light.intensity = 1.6f + Mathf.Sin(Time.time * 5f) * 0.8f;
            else if (_mains && Time.time - _mainsAt < 1.6f) _light.intensity = Mathf.PerlinNoise(Time.time * 18f, 0.3f) > 0.45f ? 2.4f : 0.2f;
            else _light.intensity = 1.4f;
        }
    }
}
