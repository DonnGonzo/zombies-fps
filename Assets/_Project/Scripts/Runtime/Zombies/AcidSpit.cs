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
            go.transform.position = from;
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

            if (_server && Time.time >= _nextTick)
            {
                _nextTick = Time.time + DamageTick;
                foreach (var p in NetPlayer.All)
                {
                    if (!p.IsAlive) continue;
                    Vector3 d = p.transform.position - _to;
                    if (Mathf.Abs(d.y) > MaxHeightDifference) continue;
                    d.y = 0f;
                    if (d.sqrMagnitude <= _radius * _radius) p.ServerApplyDamage(_dps * DamageTick, _to);
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
            _puddle = Greybox.Prim(PrimitiveType.Cylinder, "Puddle", transform, new Vector3(0f, 0.03f, 0f),
                new Vector3(_radius * 2f, 0.02f, _radius * 2f), FxSystem.Acid * 0.8f, collider: false, unlit: true).transform;
            FxSystem.Instance?.Impact(_to + Vector3.up * 0.1f, Vector3.up, FxSystem.Acid, 14, 3f, 0.08f);
        }
    }
}
