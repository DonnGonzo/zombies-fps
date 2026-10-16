using System;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Player;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// A pooled NavMesh zombie: chase, telegraphed swing, hit reactions and a death fall.
    /// In Phase 1 this becomes host-owned (NetworkObject) with clients only playing the visuals.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Zombie : MonoBehaviour, IDamageable
    {
        enum State { Chasing, Attacking, Dead }

        const float RepathInterval = 0.2f;
        const float DeathFallTime = 0.45f;
        const float CorpseTime = 2.2f;
        const float SinkTime = 1.0f;

        ZombieData _data;
        NavMeshAgent _agent;
        AudioSource _audio;
        Collider[] _colliders;
        Renderer[] _renderers;
        Material[] _baseMaterials;
        Transform _visual, _head, _armL, _armR;

        Transform _target;
        PlayerHealth _targetHealth;

        State _state;
        long _health;
        float _moveSpeed;
        float _repathTimer;
        float _attackTimer;
        bool _attackLanded;
        float _staggerUntil;
        float _flashUntil;
        bool _flashing;
        float _flinch;
        float _deathTime;
        Vector3 _fallAxis;
        float _nextGroan;
        float _walkPhase;

        public long Health => _health;
        public long MaxHealth { get; private set; }
        public bool IsAlive => _state != State.Dead;
        public bool IsActive => gameObject.activeSelf;

        public event Action<Zombie, DamageInfo, DamageResult> Damaged;
        public event Action<Zombie, DamageInfo> Died;
        public event Action<Zombie> Despawned;

        /// <summary>Builds the greybox body. Called once when the pool creates this zombie.</summary>
        public void Build(Color skin)
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.height = 1.8f;
            _agent.radius = 0.32f;
            _agent.acceleration = 14f;
            _agent.angularSpeed = 540f;
            _agent.stoppingDistance = 1.1f;
            _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var body = gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.75f, 0f);
            body.height = 1.5f;
            body.radius = 0.3f;
            gameObject.AddComponent<Hitbox>().Init(this, head: false);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 2f;
            _audio.maxDistance = 30f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.playOnAwake = false;

            var cloth = new Color(0.22f, 0.2f, 0.18f);
            _visual = Greybox.Empty("Visual", transform, Vector3.zero);
            Greybox.Prim(PrimitiveType.Capsule, "Torso", _visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.5f, 0.38f), cloth, collider: false);
            Greybox.Prim(PrimitiveType.Capsule, "LegL", _visual, new Vector3(-0.13f, 0.4f, 0f), new Vector3(0.2f, 0.4f, 0.2f), cloth, collider: false);
            Greybox.Prim(PrimitiveType.Capsule, "LegR", _visual, new Vector3(0.13f, 0.4f, 0f), new Vector3(0.2f, 0.4f, 0.2f), cloth, collider: false);

            var headGo = Greybox.Prim(PrimitiveType.Sphere, "Head", _visual, new Vector3(0f, 1.62f, 0.04f), Vector3.one * 0.34f, skin);
            headGo.AddComponent<Hitbox>().Init(this, head: true);
            _head = headGo.transform;
            var eye = new Color(1f, 0.85f, 0.2f);
            Greybox.Prim(PrimitiveType.Cube, "EyeL", _head, new Vector3(-0.17f, 0.08f, 0.44f), new Vector3(0.12f, 0.06f, 0.06f), eye, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Cube, "EyeR", _head, new Vector3(0.17f, 0.08f, 0.44f), new Vector3(0.12f, 0.06f, 0.06f), eye, collider: false, unlit: true);

            _armL = Greybox.Empty("ArmL", _visual, new Vector3(-0.32f, 1.32f, 0f));
            _armR = Greybox.Empty("ArmR", _visual, new Vector3(0.32f, 1.32f, 0f));
            Greybox.Box("ForearmL", _armL, new Vector3(0f, 0f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), skin, collider: false);
            Greybox.Box("ForearmR", _armR, new Vector3(0f, 0f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), skin, collider: false);

            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = _visual.GetComponentsInChildren<Renderer>(true);
            _baseMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseMaterials[i] = _renderers[i].sharedMaterial;
        }

        /// <summary>Activates this pooled zombie at a point on the NavMesh.</summary>
        public void Spawn(Vector3 position, Quaternion rotation, ZombieData data, long health, ZombieSpeedTier speedTier,
            Transform target, PlayerHealth targetHealth)
        {
            _data = data;
            _target = target;
            _targetHealth = targetHealth;
            MaxHealth = Math.Max(1L, (long)Math.Round(health * (double)data.healthMultiplier));
            _health = MaxHealth;
            // A little per-zombie variation so a crowd doesn't move in lockstep.
            _moveSpeed = data.SpeedFor(speedTier) * UnityEngine.Random.Range(0.9f, 1.1f);

            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            _agent.enabled = true;
            _agent.Warp(position);
            _agent.speed = _moveSpeed;
            _agent.isStopped = false;

            foreach (var c in _colliders) c.enabled = true;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = _baseMaterials[i];
            _flashing = false;
            _head.gameObject.SetActive(true);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;

            _state = State.Chasing;
            _repathTimer = 0f;
            _staggerUntil = 0f;
            _flashUntil = 0f;
            _flinch = 0f;
            _walkPhase = UnityEngine.Random.Range(0f, 10f);
            _nextGroan = Time.time + UnityEngine.Random.Range(0.5f, 3f);
        }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (_state == State.Dead) return default;
            long dealt = Math.Min(info.Amount, _health);
            _health -= dealt;

            _flashUntil = Time.time + 0.06f;
            _flinch = Mathf.Min(_flinch + (info.IsHeadshot ? 1f : 0.6f), 1.5f);
            _staggerUntil = Time.time + _data.staggerTime;

            bool killed = _health <= 0;
            var result = new DamageResult(dealt, killed);
            Damaged?.Invoke(this, info, result);
            if (killed) Die(info);
            return result;
        }

        void Die(in DamageInfo info)
        {
            _state = State.Dead;
            _deathTime = Time.time;
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            _agent.enabled = false;
            foreach (var c in _colliders) c.enabled = false;

            Vector3 dir = info.Direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            dir.Normalize();
            // Fall away from the shot.
            _fallAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, dir));

            if (info.IsHeadshot)
            {
                _head.gameObject.SetActive(false);
                FxSystem.Instance?.Impact(_head.position, Vector3.up, FxSystem.Blood, 18, 4.5f, 0.09f);
            }
            _audio.Stop();
            if (_flashing)
            {
                _flashing = false;
                for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = _baseMaterials[i];
            }
            Died?.Invoke(this, info);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_state == State.Dead)
            {
                UpdateDeath();
                return;
            }

            if (!_agent.isOnNavMesh) return;

            bool flashing = Time.time < _flashUntil;
            if (flashing != _flashing)
            {
                _flashing = flashing;
                var white = Greybox.Lit(Color.white);
                for (int i = 0; i < _renderers.Length; i++)
                    _renderers[i].sharedMaterial = flashing ? white : _baseMaterials[i];
            }

            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 6f);
            bool targetAlive = _targetHealth != null && _targetHealth.IsAlive;
            _agent.speed = Time.time < _staggerUntil ? _moveSpeed * _data.staggerSpeedScale : _moveSpeed;

            if (!targetAlive)
            {
                _agent.isStopped = true;
                _state = State.Chasing;
                AnimateBody(0f, 0f);
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (_state == State.Chasing)
            {
                _agent.isStopped = false;
                _repathTimer -= dt;
                if (_repathTimer <= 0f)
                {
                    _agent.SetDestination(_target.position);
                    _repathTimer = RepathInterval;
                }
                if (distance <= _data.attackRange)
                {
                    _state = State.Attacking;
                    _attackTimer = 0f;
                    _attackLanded = false;
                    _agent.isStopped = true;
                    _audio.PlayOneShot(ProceduralAudio.ZombieSwipe, 0.6f);
                }
                if (Time.time >= _nextGroan)
                {
                    _audio.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                    _audio.PlayOneShot(ProceduralAudio.RandomGroan(), 0.8f);
                    _nextGroan = Time.time + UnityEngine.Random.Range(3f, 7f);
                }
                AnimateBody(_agent.velocity.magnitude / Mathf.Max(0.1f, _moveSpeed), 0f);
            }
            else // Attacking
            {
                if (distance > 0.01f)
                {
                    var face = Quaternion.LookRotation(toTarget / distance, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 540f * dt);
                }
                _attackTimer += dt;
                float windup = _data.attackWindup;
                if (!_attackLanded && _attackTimer >= windup)
                {
                    _attackLanded = true;
                    // Backing off during the wind-up dodges the hit.
                    if (distance <= _data.attackRange + 0.5f)
                        _targetHealth.TakeDamage(_data.damage, transform.position);
                }
                float swing = _attackTimer < windup ? _attackTimer / windup : 1f + (_attackTimer - windup) / _data.attackRecovery;
                AnimateBody(0f, swing);
                if (_attackTimer >= windup + _data.attackRecovery) _state = State.Chasing;
            }
        }

        /// <param name="move01">Walk cycle strength.</param>
        /// <param name="swing">0..1 wind-up, 1..2 strike and recover.</param>
        void AnimateBody(float move01, float swing)
        {
            _walkPhase += Time.deltaTime * (2f + 6f * move01);
            float sway = Mathf.Sin(_walkPhase) * 6f * Mathf.Clamp01(move01 + 0.2f);
            _visual.localRotation = Quaternion.Euler(8f - _flinch * 18f, 0f, sway);

            float armPitch;
            if (swing <= 0f) armPitch = 8f + Mathf.Sin(_walkPhase * 0.5f) * 6f;
            else if (swing < 1f) armPitch = Mathf.Lerp(8f, -70f, swing * swing);
            else armPitch = Mathf.Lerp(-70f, 25f, Mathf.Clamp01((swing - 1f) * 4f)) + Mathf.Lerp(0f, -17f, Mathf.Clamp01(swing - 1.25f));
            _armL.localRotation = Quaternion.Euler(armPitch + Mathf.Sin(_walkPhase) * 5f, -6f, 0f);
            _armR.localRotation = Quaternion.Euler(armPitch - Mathf.Sin(_walkPhase) * 5f, 6f, 0f);
        }

        void UpdateDeath()
        {
            float t = Time.time - _deathTime;
            float fall = Mathf.Clamp01(t / DeathFallTime);
            fall = fall * fall;
            _visual.localRotation = Quaternion.AngleAxis(fall * 88f, _fallAxis);

            if (t > DeathFallTime + CorpseTime)
            {
                float sink = Mathf.Clamp01((t - DeathFallTime - CorpseTime) / SinkTime);
                _visual.localPosition = new Vector3(0f, -0.6f * sink, 0f);
                if (sink >= 1f)
                {
                    gameObject.SetActive(false);
                    Despawned?.Invoke(this);
                }
            }
        }
    }
}
