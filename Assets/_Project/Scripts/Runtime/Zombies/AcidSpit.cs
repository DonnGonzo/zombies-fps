using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// A spitter's glob of acid: it arcs to where the player stood and leaves a burning puddle. Every machine
    /// shows its own copy; only the host's copy hurts players, so damage stays host-authoritative.
    /// </summary>
    public class AcidSpit : MonoBehaviour
    {
        const float ArcHeight = 1.6f;
        const float DamageTick = 0.5f;
        const float FadeTime = 0.6f;
        const float MaxHeightDifference = 1.6f;

        Vector3 _from, _to;
        float _flightTime, _launchedAt;
        float _dps, _radius, _puddleTime;
        bool _server;
        bool _landed;
        float _landedAt, _nextTick;
        Transform _glob, _puddle;
        Light _light;
        Transform[] _bubbles;
        Vector3[] _bubbleSpots;

        public static void Launch(Vector3 from, Vector3 to, float flightTime, ZombieData data, bool server)
        {
            var go = new GameObject("Acid Spit");
            var spit = go.AddComponent<AcidSpit>();
            spit._from = from;
            spit._to = GroundBelow(to);
            spit._flightTime = Mathf.Max(0.1f, flightTime);
            spit._launchedAt = Time.time;
            spit._dps = data.puddleDps;
            spit._radius = data.puddleRadius;
            spit._puddleTime = data.puddleTime;
            spit._server = server;
            spit._glob = Greybox.Prim(PrimitiveType.Sphere, "Glob", go.transform, Vector3.zero, Vector3.one * 0.28f, FxSystem.Acid, collider: false, unlit: true).transform;
            // A darker wet skin round a glowing core, so the glob reads as liquid rather than a lamp.
            Greybox.Prim(PrimitiveType.Sphere, "Skin", spit._glob, Vector3.zero, Vector3.one * 1.15f, new Color(0.3f, 0.5f, 0.08f, 0.55f), collider: false, unlit: true);
            spit._light = new GameObject("Acid Light").AddComponent<Light>();
            spit._light.transform.SetParent(go.transform, false);
            spit._light.type = LightType.Point;
            spit._light.color = FxSystem.Acid;
            spit._light.range = 3f;
            spit._light.intensity = 1.4f;
            spit._light.shadows = LightShadows.None;
            Greybox.DisableShadows(go);
            go.transform.position = from;
        }

        Vector3 RandomSpot()
        {
            var p = Random.insideUnitCircle * (_radius * 0.75f);
            return new Vector3(p.x, 0.045f, p.y);
        }

        static Vector3 GroundBelow(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<NetPlayer>() == null && hit.collider.GetComponentInParent<NetZombie>() == null)
                return hit.point;
            return point;
        }

        void Update()
        {
            if (!_landed)
            {
                float t = Mathf.Clamp01((Time.time - _launchedAt) / _flightTime);
                transform.position = Vector3.Lerp(_from, _to, t) + Vector3.up * (ArcHeight * 4f * t * (1f - t));
                // Wobbles in flight.
                float w = Mathf.Sin(Time.time * 30f) * 0.12f;
                _glob.localScale = new Vector3(0.28f * (1f + w), 0.28f * (1f - w), 0.28f * (1f + w * 0.5f));
                if (t >= 1f) Land();
                return;
            }

            float age = Time.time - _landedAt;
            if (age >= _puddleTime)
            {
                Destroy(gameObject);
                return;
            }
            float fade = Mathf.Clamp01((_puddleTime - age) / FadeTime);
            _puddle.localScale = new Vector3(_radius * 2f * fade, 0.02f, _radius * 2f * fade);
            _light.intensity = (0.9f + 0.4f * Mathf.PerlinNoise(Time.time * 3f, 0.7f)) * fade;
            // Bubbles swell and pop at random spots.
            for (int i = 0; i < _bubbles.Length; i++)
            {
                float cycle = Time.time * (1.1f + i * 0.23f) + i * 0.37f;
                float phase = cycle - Mathf.Floor(cycle);
                if (phase < 0.02f) _bubbleSpots[i] = RandomSpot();
                _bubbles[i].localPosition = _bubbleSpots[i] * fade;
                _bubbles[i].localScale = Vector3.one * (Mathf.Min(_radius, 1.5f) * 0.12f * phase * fade);
            }

            if (_server && Time.time >= _nextTick)
            {
                _nextTick = Time.time + DamageTick;
                foreach (var p in NetPlayer.All)
                {
                    if (!p.IsAlive) continue;
                    Vector3 d = p.transform.position - _to;
                    if (Mathf.Abs(d.y) > MaxHeightDifference) continue;
                    d.y = 0f;
                    if (d.sqrMagnitude <= _radius * _radius) p.ServerApplyDamage(_dps * DamageTick, _to, acid: true);
                }
            }
        }

        void Land()
        {
            _landed = true;
            _landedAt = Time.time;
            _nextTick = Time.time;
            transform.position = _to;
            Destroy(_glob.gameObject);
            // A wet, glossy pool with a glowing heart, bubbling, over a burn mark left on the ground.
            _puddle = Greybox.Prim(PrimitiveType.Cylinder, "Puddle", transform, new Vector3(0f, 0.03f, 0f),
                new Vector3(_radius * 2f, 0.02f, _radius * 2f), new Color(0.22f, 0.4f, 0.05f), collider: false, surface: Surface.Rubber, moving: true).transform;
            Greybox.Prim(PrimitiveType.Cylinder, "Glow", _puddle, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 0.1f, 0.7f),
                new Color(0.32f, 0.65f, 0.1f), collider: false, unlit: true);
            _bubbles = new Transform[5];
            _bubbleSpots = new Vector3[_bubbles.Length];
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbleSpots[i] = RandomSpot();
                _bubbles[i] = Greybox.Prim(PrimitiveType.Sphere, "Bubble", transform, _bubbleSpots[i], Vector3.zero, FxSystem.Acid, collider: false, unlit: true).transform;
            }
            Greybox.DisableShadows(gameObject);
            _light.transform.localPosition = Vector3.up * 0.4f;
            _light.range = Mathf.Max(3f, _radius * 3f);
            FxSystem.Instance?.Impact(_to + Vector3.up * 0.1f, Vector3.up, FxSystem.Acid, 14, 3f, 0.08f);
            FxSystem.Instance?.AcidStain(_to, _radius * 2.3f, _puddleTime + 6f);
        }
    }
}
