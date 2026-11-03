using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// A thrown grenade, knife or concussion bang. Every machine flies its own copy with the same simple
    /// physics; only the host's copy decides where it goes off and what it hits, and tells everyone (Detonate).
    /// Copies on clients just get removed when that word arrives.
    /// </summary>
    public class Throwable : MonoBehaviour
    {
        const float Radius = 0.07f;
        const float Bounce = 0.35f;
        const float GroundFriction = 4f;

        static readonly Dictionary<long, Throwable> s_Live = new Dictionary<long, Throwable>();
        static readonly RaycastHit[] s_Hits = new RaycastHit[12];

        public long Id { get; private set; }
        public ThrowableKind Kind { get; private set; }
        public ulong Thrower { get; private set; }

        Vector3 _velocity;
        float _fuse;
        float _age;
        bool _authoritative;
        bool _stuck;
        float _removeAt = -1f;
        Transform _spin;
        Action<Throwable, Vector3, Hitbox> _onHit;

        /// <summary>
        /// Starts a throwable flying. The host's copy is authoritative and calls onDetonate when the fuse runs
        /// out, or (knives) on the first thing it hits, with the zombie hitbox if there was one.
        /// </summary>
        public static Throwable Launch(long id, ThrowableKind kind, ulong thrower, Vector3 position, Vector3 velocity, float fuse,
            bool authoritative, Action<Throwable, Vector3, Hitbox> onDetonate)
        {
            if (s_Live.TryGetValue(id, out var existing) && existing != null) return existing;
            var go = new GameObject(kind.ToString());
            go.transform.position = position;
            var t = go.AddComponent<Throwable>();
            t.Id = id;
            t.Kind = kind;
            t.Thrower = thrower;
            t._velocity = velocity;
            t._fuse = fuse;
            t._authoritative = authoritative;
            t._onHit = onDetonate;
            t.BuildVisual();
            s_Live[id] = t;
            return t;
        }

        /// <summary>The host said this one went off (or hit something) at a point: show it there and clean up.</summary>
        public static void Detonated(long id, Vector3 point)
        {
            if (!s_Live.TryGetValue(id, out var t) || t == null) return;
            s_Live.Remove(id);
            if (t.Kind == ThrowableKind.Knife)
            {
                // Leave the knife where it landed for a moment.
                t.transform.position = point;
                t._stuck = true;
                t._removeAt = Time.time + 1.5f;
                return;
            }
            Destroy(t.gameObject);
        }

        public static void ClearAll()
        {
            foreach (var t in s_Live.Values)
                if (t != null) Destroy(t.gameObject);
            s_Live.Clear();
        }

        void BuildVisual() => _spin = BuildModel(Kind, transform);

        /// <summary>The throwable's model, centred on <paramref name="parent"/>; also held in the hand while it's thrown.</summary>
        public static Transform BuildModel(ThrowableKind kind, Transform parent)
        {
            var root = Greybox.Empty("Spin", parent, Vector3.zero);
            var steel = new Color(0.36f, 0.37f, 0.39f);
            switch (kind)
            {
                case ThrowableKind.Grenade:
                    Greybox.Prim(PrimitiveType.Sphere, "Body", root, Vector3.zero, new Vector3(0.11f, 0.13f, 0.11f), new Color(0.24f, 0.28f, 0.18f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Band", root, Vector3.zero, new Vector3(0.113f, 0.004f, 0.113f), new Color(0.12f, 0.14f, 0.09f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Fuse", root, new Vector3(0f, 0.068f, 0f), new Vector3(0.036f, 0.014f, 0.036f), steel, collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Box("Spoon", root, new Vector3(0.03f, 0.05f, 0f), new Vector3(0.012f, 0.08f, 0.026f), steel, collider: false,
                        surface: Surface.Metal, moving: true).transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                    Greybox.Prim(PrimitiveType.Cylinder, "PinRing", root, new Vector3(-0.028f, 0.075f, 0f), new Vector3(0.026f, 0.002f, 0.026f), new Color(0.6f, 0.6f, 0.62f), collider: false,
                        surface: Surface.Metal, moving: true).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case ThrowableKind.Knife:
                    Greybox.Box("Blade", root, new Vector3(0f, 0f, 0.06f), new Vector3(0.012f, 0.04f, 0.16f), new Color(0.78f, 0.8f, 0.83f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Box("Spine", root, new Vector3(0f, 0.018f, 0.05f), new Vector3(0.013f, 0.006f, 0.13f), new Color(0.3f, 0.31f, 0.33f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Box("Guard", root, new Vector3(0f, 0f, -0.017f), new Vector3(0.016f, 0.055f, 0.01f), new Color(0.15f, 0.15f, 0.16f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Box("Grip", root, new Vector3(0f, 0f, -0.065f), new Vector3(0.025f, 0.035f, 0.09f), new Color(0.12f, 0.1f, 0.08f), collider: false,
                        surface: Surface.Rubber, moving: true);
                    break;
                default:
                    Greybox.Prim(PrimitiveType.Cylinder, "Body", root, Vector3.zero, new Vector3(0.08f, 0.07f, 0.08f), new Color(0.2f, 0.25f, 0.35f), collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "CapTop", root, new Vector3(0f, 0.06f, 0f), new Vector3(0.084f, 0.012f, 0.084f), steel, collider: false,
                        surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "CapBottom", root, new Vector3(0f, -0.065f, 0f), new Vector3(0.084f, 0.008f, 0.084f), steel, collider: false,
                        surface: Surface.Metal, moving: true);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Greybox.Box("Port", root, new Vector3(Mathf.Cos(a) * 0.04f, 0.015f, Mathf.Sin(a) * 0.04f), new Vector3(0.012f, 0.03f, 0.012f), new Color(0.03f, 0.03f, 0.035f),
                            collider: false, surface: Surface.Rubber, moving: true);
                    }
                    Greybox.Prim(PrimitiveType.Sphere, "Light", root, new Vector3(0f, 0.075f, 0f), Vector3.one * 0.04f, new Color(0.5f, 0.8f, 1f), collider: false, unlit: true);
                    break;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) Greybox.DisableShadows(r.gameObject);
            return root;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_removeAt > 0f)
            {
                if (Time.time >= _removeAt) Destroy(gameObject);
                return;
            }
            // A client copy whose word from the host never came (lost packet, host left).
            if (!_authoritative && _age > _fuse + 3f && Kind != ThrowableKind.Knife) Detonated(Id, transform.position);
            if (!_authoritative && Kind == ThrowableKind.Knife && _age > 4f) Detonated(Id, transform.position);

            if (!_stuck) Fly(dt);

            if (_authoritative && Kind != ThrowableKind.Knife)
            {
                _fuse -= dt;
                if (_fuse <= 0f) Fire(transform.position, null);
            }
            if (_authoritative && Kind == ThrowableKind.Knife && _age > 3f) Fire(transform.position, null);
        }

        void Fly(float dt)
        {
            _velocity += Physics.gravity * (Kind == ThrowableKind.Knife ? 0.35f : 1f) * dt;
            Vector3 step = _velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f)
            {
                Vector3 dir = step / dist;
                int count = Physics.SphereCastNonAlloc(transform.position, Radius, dir, s_Hits, dist, ~0, QueryTriggerInteraction.Ignore);
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var c = s_Hits[i].collider;
                    if (c.GetComponentInParent<NetPlayer>() != null) continue;
                    // Grenades and bangs bounce off bodies too, but a zombie's hitbox only matters to knives.
                    if (s_Hits[i].distance < bestDist && s_Hits[i].distance > 0f)
                    {
                        bestDist = s_Hits[i].distance;
                        best = i;
                    }
                }
                if (best >= 0)
                {
                    var hit = s_Hits[best];
                    transform.position = hit.point + hit.normal * (Radius + 0.01f);
                    if (Kind == ThrowableKind.Knife)
                    {
                        _stuck = true;
                        var hitbox = hit.collider.GetComponent<Hitbox>();
                        if (_authoritative) Fire(hit.point, hitbox);
                        return;
                    }
                    _velocity = Vector3.Reflect(_velocity, hit.normal) * Bounce;
                    if (hit.normal.y > 0.6f && _velocity.y < 1.2f)
                    {
                        // Rolling to a stop on the floor.
                        _velocity.y = 0f;
                        _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, GroundFriction * dt * 4f);
                    }
                }
                else
                {
                    transform.position += step;
                }
            }

            if (Kind == ThrowableKind.Knife)
            {
                if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);
                _spin.localRotation = Quaternion.Euler(_age * 1400f, 0f, 0f);
            }
            else
            {
                _spin.Rotate(_velocity.magnitude * 120f * dt, 0f, _velocity.magnitude * 60f * dt, Space.Self);
            }
        }

        void Fire(Vector3 point, Hitbox hitbox)
        {
            if (!_authoritative) return;
            _authoritative = false;
            var cb = _onHit;
            _onHit = null;
            cb?.Invoke(this, point, hitbox);
        }

        void OnDestroy()
        {
            if (s_Live.TryGetValue(Id, out var t) && t == this) s_Live.Remove(Id);
        }
    }
}
