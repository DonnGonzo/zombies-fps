using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.Zombies
{
    /// <summary>Networked zombie behaviour state. Public so Netcode can generate its serializer.</summary>
    public enum ZombieMode : byte
    {
        Chasing,
        Attacking,
        Dead,
    }

    /// <summary>
    /// A host-owned zombie. The host runs the NavMesh AI, health and attacks; clients get the position
    /// from NetworkTransform and play the same animations from a handful of events (hit, swing, death).
    /// </summary>
    public class NetZombie : NetworkBehaviour, IDamageable
    {
        const float RepathInterval = 0.2f;
        const float DeathFallTime = 0.45f;
        const float CorpseTime = 2.2f;
        const float SinkTime = 1.0f;
        // A chasing zombie that makes no progress this long while far from its target is recycled.
        const float StuckTime = 15f;
        const float StuckMoveDistance = 0.5f;
        const float StuckMinTargetDistance = 8f;

        static readonly Color[] SkinTones =
        {
            new Color(0.42f, 0.5f, 0.36f),
            new Color(0.5f, 0.52f, 0.42f),
            new Color(0.38f, 0.44f, 0.4f),
        };

        NetworkVariable<ZombieMode> _mode = new NetworkVariable<ZombieMode>(ZombieMode.Chasing);

        ZombieData _data;
        AudioSource _audio;
        Collider[] _colliders;
        Renderer[] _renderers;
        Material[] _baseMaterials;
        Transform _visual, _head, _armL, _armR;
        bool _built;

        // Visual state (every machine).
        float _flashUntil;
        bool _flashing;
        float _flinch;
        float _swingStart = -100f;
        float _deathTime;
        float _deadSeenAt = -1f;
        Vector3 _fallAxis;
        float _nextGroan;
        float _walkPhase;
        Vector3 _lastPosition;
        float _speedEstimate;

        // Server state.
        NavMeshAgent _agent;
        long _health;
        float _moveSpeed;
        float _repathTimer;
        float _attackTimer;
        bool _attackLanded;
        float _staggerUntil;
        NetPlayer _target;
        float _despawnAt;
        long _pendingHealth;
        ZombieSpeedTier _pendingTier;
        Vector3 _stuckAnchor;
        float _stuckSince;
        float _offMeshSince;

        public bool IsAlive => _mode.Value != ZombieMode.Dead;
        public long Health => _health;

        /// <summary>Raised on the host when this zombie dies (for the round manager's counters).</summary>
        public event Action<NetZombie, DamageInfo> Died;
        /// <summary>Raised on the host when a stuck zombie is removed without dying, so it can be spawned again.</summary>
        public event Action<NetZombie> Recycled;

        /// <summary>Builds the body ahead of time, so the pool can do it before the round starts.</summary>
        public void Prebuild() => Build(SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)]);

        /// <summary>Host: call before Spawn(). Pooled zombies come back here for every new life.</summary>
        public void ServerSetup(long health, ZombieSpeedTier tier)
        {
            _pendingHealth = health;
            _pendingTier = tier;
            _mode.Value = ZombieMode.Chasing;
        }

        public override void OnNetworkSpawn()
        {
            _data = GameConfig.Zombie;
            Prebuild();
            ResetVisuals();
            _lastPosition = transform.position;
            _nextGroan = Time.time + UnityEngine.Random.Range(0.5f, 3f);
            _walkPhase = UnityEngine.Random.Range(0f, 10f);
            _mode.OnValueChanged += OnModeChanged;
            if (_mode.Value == ZombieMode.Dead) EnterDeadVisual(-transform.forward, false);

            if (IsServer)
            {
                _health = Math.Max(1L, (long)Math.Round(_pendingHealth * (double)_data.healthMultiplier));
                _moveSpeed = _data.SpeedFor(_pendingTier) * UnityEngine.Random.Range(0.9f, 1.1f);
                if (_agent == null)
                {
                    _agent = gameObject.AddComponent<NavMeshAgent>();
                    _agent.height = 1.8f;
                    _agent.radius = 0.32f;
                    _agent.acceleration = 14f;
                    _agent.angularSpeed = 540f;
                    _agent.stoppingDistance = 1.1f;
                    _agent.autoBraking = true;
                    _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                }
                _agent.enabled = true;
                _agent.speed = _moveSpeed;
                _agent.Warp(transform.position);

                _target = null;
                _repathTimer = 0f;
                _attackTimer = 0f;
                _attackLanded = false;
                _staggerUntil = 0f;
                _despawnAt = float.MaxValue;
                _stuckAnchor = transform.position;
                _stuckSince = Time.time;
                _offMeshSince = -1f;
            }
        }

        public override void OnNetworkDespawn()
        {
            _mode.OnValueChanged -= OnModeChanged;
            Died = null;
            Recycled = null;
        }

        /// <summary>Puts the body back to a fresh, standing zombie (pooled objects keep last life's pose).</summary>
        void ResetVisuals()
        {
            _deathTime = 0f;
            _deadSeenAt = -1f;
            _flashUntil = 0f;
            _flinch = 0f;
            _swingStart = -100f;
            _speedEstimate = 0f;
            SetFlash(false);
            foreach (var c in _colliders) c.enabled = true;
            _head.gameObject.SetActive(true);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }

        void Build(Color skin)
        {
            if (_built) return;
            _built = true;

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

        // ---------------------------------------------------------------- damage (host)

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!IsServer || _mode.Value == ZombieMode.Dead) return default;
            long dealt = Math.Min(info.Amount, _health);
            _health -= dealt;
            _staggerUntil = Time.time + _data.staggerTime;
            bool killed = _health <= 0;

            if (killed)
            {
                _mode.Value = ZombieMode.Dead;
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                _agent.enabled = false;
                _despawnAt = Time.time + DeathFallTime + CorpseTime + SinkTime;
                DeathRpc(info.Direction, info.IsHeadshot);
                Died?.Invoke(this, info);
            }
            else
            {
                HitRpc(info.IsHeadshot);
            }
            return new DamageResult(dealt, killed);
        }

        // ---------------------------------------------------------------- RPCs (host -> everyone)

        [Rpc(SendTo.ClientsAndHost)]
        void HitRpc(bool headshot)
        {
            _flashUntil = Time.time + 0.06f;
            _flinch = Mathf.Min(_flinch + (headshot ? 1f : 0.6f), 1.5f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SwingRpc()
        {
            _swingStart = Time.time;
            _audio.PlayOneShot(ProceduralAudio.ZombieSwipe, 0.6f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DeathRpc(Vector3 direction, bool headshot)
        {
            EnterDeadVisual(direction, headshot);
        }

        void OnModeChanged(ZombieMode previous, ZombieMode current)
        {
            // The death event normally arrives too and carries the shot direction; this is the fallback.
            if (current == ZombieMode.Dead) _deadSeenAt = Time.time;
        }

        void EnterDeadVisual(Vector3 direction, bool headshot)
        {
            if (_deathTime > 0f) return;
            _deathTime = Time.time;
            foreach (var c in _colliders) c.enabled = false;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
            direction.Normalize();
            _fallAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, direction));
            if (headshot)
            {
                _head.gameObject.SetActive(false);
                FxSystem.Instance?.Impact(_head.position, Vector3.up, FxSystem.Blood, 18, 4.5f, 0.09f);
            }
            _audio.Stop();
            SetFlash(false);
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (!_built || !IsSpawned) return;
            if (IsServer) ServerUpdate();
            UpdateVisuals();
        }

        void ServerUpdate()
        {
            if (_mode.Value == ZombieMode.Dead)
            {
                if (Time.time >= _despawnAt) NetworkObject.Despawn(true);
                return;
            }
            if (!_agent.isOnNavMesh)
            {
                if (_offMeshSince < 0f) _offMeshSince = Time.time;
                else if (Time.time - _offMeshSince > 2f) ServerRecycle();
                return;
            }
            _offMeshSince = -1f;

            float dt = Time.deltaTime;
            var rounds = RoundManager.Instance;
            bool gameOver = rounds != null && rounds.IsGameOver;
            _agent.speed = Time.time < _staggerUntil ? _moveSpeed * _data.staggerSpeedScale : _moveSpeed;

            if (_target == null || !_target.IsSpawned || !_target.IsAlive) _target = FindTarget();
            if (_target == null || gameOver)
            {
                _agent.isStopped = true;
                _mode.Value = ZombieMode.Chasing;
                return;
            }

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (_mode.Value == ZombieMode.Chasing && distance > StuckMinTargetDistance)
            {
                if ((transform.position - _stuckAnchor).sqrMagnitude > StuckMoveDistance * StuckMoveDistance)
                {
                    _stuckAnchor = transform.position;
                    _stuckSince = Time.time;
                }
                else if (Time.time - _stuckSince > StuckTime)
                {
                    ServerRecycle();
                    return;
                }
            }
            else
            {
                _stuckAnchor = transform.position;
                _stuckSince = Time.time;
            }

            if (_mode.Value == ZombieMode.Chasing)
            {
                _agent.isStopped = false;
                _repathTimer -= dt;
                if (_repathTimer <= 0f)
                {
                    _target = FindTarget() ?? _target;
                    _agent.SetDestination(_target.transform.position);
                    _repathTimer = RepathInterval;
                }
                if (distance <= _data.attackRange)
                {
                    _mode.Value = ZombieMode.Attacking;
                    _attackTimer = 0f;
                    _attackLanded = false;
                    _agent.isStopped = true;
                    SwingRpc();
                }
            }
            else
            {
                if (distance > 0.01f)
                {
                    var face = Quaternion.LookRotation(toTarget / distance, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 540f * dt);
                }
                _attackTimer += dt;
                if (!_attackLanded && _attackTimer >= _data.attackWindup)
                {
                    _attackLanded = true;
                    // Backing off during the wind-up dodges the hit.
                    if (distance <= _data.attackRange + 0.5f) _target.ServerApplyDamage(_data.damage, transform.position);
                }
                if (_attackTimer >= _data.attackWindup + _data.attackRecovery) _mode.Value = ZombieMode.Chasing;
            }
        }

        /// <summary>Host: removes a zombie that can't reach anyone, without a kill. The round manager respawns it.</summary>
        void ServerRecycle()
        {
            if (!IsSpawned) return;
            Recycled?.Invoke(this);
            NetworkObject.Despawn(true);
        }

        /// <summary>Host: removes the zombie straight away (restart or skipping rounds in test mode).</summary>
        public void ServerRemove()
        {
            if (IsSpawned) NetworkObject.Despawn(true);
        }

        NetPlayer FindTarget()
        {
            NetPlayer best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in NetPlayer.All)
            {
                if (!p.IsAlive) continue;
                float sqr = (p.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                }
            }
            return best;
        }

        void UpdateVisuals()
        {
            float dt = Time.deltaTime;
            if (_deathTime <= 0f && _deadSeenAt >= 0f && Time.time - _deadSeenAt > 0.25f) EnterDeadVisual(-transform.forward, false);
            if (_deathTime > 0f)
            {
                float t = Time.time - _deathTime;
                float fall = Mathf.Clamp01(t / DeathFallTime);
                _visual.localRotation = Quaternion.AngleAxis(fall * fall * 88f, _fallAxis);
                float sink = Mathf.Clamp01((t - DeathFallTime - CorpseTime) / SinkTime);
                _visual.localPosition = new Vector3(0f, -0.6f * sink, 0f);
                return;
            }

            SetFlash(Time.time < _flashUntil);
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 6f);

            if (dt > 0f)
            {
                Vector3 delta = transform.position - _lastPosition;
                delta.y = 0f;
                _speedEstimate = Mathf.Lerp(_speedEstimate, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            }
            _lastPosition = transform.position;

            if (Time.time >= _nextGroan)
            {
                _audio.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                _audio.PlayOneShot(ProceduralAudio.RandomGroan(), 0.8f);
                _nextGroan = Time.time + UnityEngine.Random.Range(3f, 7f);
            }

            float swingT = Time.time - _swingStart;
            float total = _data.attackWindup + _data.attackRecovery;
            float swing = 0f;
            if (swingT < total)
                swing = swingT < _data.attackWindup ? swingT / _data.attackWindup : 1f + (swingT - _data.attackWindup) / _data.attackRecovery;
            AnimateBody(Mathf.Clamp01(_speedEstimate / Mathf.Max(0.5f, _data.jogSpeed)), swing);
        }

        void SetFlash(bool flashing)
        {
            if (flashing == _flashing) return;
            _flashing = flashing;
            var white = Greybox.Lit(Color.white);
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = flashing ? white : _baseMaterials[i];
        }

        /// <param name="move01">Walk cycle strength.</param>
        /// <param name="swing">0 none, 0..1 wind-up, 1..2 strike and recover.</param>
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
    }
}
