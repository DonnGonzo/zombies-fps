using System;
using System.Collections.Generic;
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
    /// A host-owned zombie of any type (walker, runner, crawler, brute, spitter or boss). The host runs the
    /// NavMesh AI, health and attacks; clients get the position from NetworkTransform and play the same
    /// animations from a handful of events (hit, swing, spit, death). Pooled objects change type per life.
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

        static readonly List<NetZombie> s_Bosses = new List<NetZombie>();
        /// <summary>Bosses currently spawned, on every machine (for the HUD's health bars).</summary>
        public static IReadOnlyList<NetZombie> Bosses => s_Bosses;

        NetworkVariable<ZombieMode> _mode = new NetworkVariable<ZombieMode>(ZombieMode.Chasing);
        NetworkVariable<ZombieKind> _kind = new NetworkVariable<ZombieKind>(ZombieKind.Walker);
        /// <summary>Health left as a share of full, kept up to date for bosses only.</summary>
        public NetworkVariable<float> HealthFraction = new NetworkVariable<float>(1f);

        ZombieData _data;
        AudioSource _audio;
        Collider[] _colliders;
        Renderer[] _renderers;
        Material[] _baseMaterials;
        Transform _visual, _pose, _head, _armL, _armR;
        GameObject _armor, _sac, _core;
        CapsuleCollider _bodyCollider;
        Hitbox _bodyHitbox, _coreHitbox;
        Renderer[] _skinRenderers, _clothRenderers;
        Color _skin;
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
        float _stunUntil, _slowUntil, _slowScale = 1f, _holdUntil;
        NetPlayer _target;
        float _despawnAt;
        long _pendingHealth;
        ZombieSpeedTier _pendingTier;
        long _maxHealth;
        Vector3 _stuckAnchor;
        float _stuckSince;
        float _offMeshSince;
        float _nextSpitTime;
        float _spitAt = -1f;
        readonly RaycastHit[] _sightHits = new RaycastHit[8];

        public bool IsAlive => _mode.Value != ZombieMode.Dead;
        public long Health => _health;
        public ZombieKind Kind => _kind.Value;
        public ZombieData Data => _data != null ? _data : GameConfig.ZombieType(_kind.Value);

        /// <summary>Raised on the host when this zombie dies (for the round manager's counters).</summary>
        public event Action<NetZombie, DamageInfo> Died;
        /// <summary>Raised on the host when a stuck zombie is removed without dying, so it can be spawned again.</summary>
        public event Action<NetZombie> Recycled;

        /// <summary>Builds the body ahead of time, so the pool can do it before the round starts.</summary>
        public void Prebuild() => Build(SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)]);

        /// <summary>Host: call before Spawn(). Pooled zombies come back here for every new life.</summary>
        public void ServerSetup(ZombieKind kind, long health, ZombieSpeedTier tier)
        {
            _pendingHealth = health;
            _pendingTier = tier;
            _kind.Value = kind;
            _mode.Value = ZombieMode.Chasing;
            HealthFraction.Value = 1f;
        }

        public override void OnNetworkSpawn()
        {
            _data = GameConfig.ZombieType(_kind.Value);
            Prebuild();
            ApplyKind();
            ResetVisuals();
            if (_data.IsBoss) s_Bosses.Add(this);
            _lastPosition = transform.position;
            _nextGroan = Time.time + UnityEngine.Random.Range(0.5f, 3f);
            _walkPhase = UnityEngine.Random.Range(0f, 10f);
            _mode.OnValueChanged += OnModeChanged;
            if (_mode.Value == ZombieMode.Dead) EnterDeadVisual(-transform.forward, false);

            if (IsServer)
            {
                _health = Math.Max(1L, (long)Math.Round(_pendingHealth * (double)_data.healthMultiplier));
                _maxHealth = _health;
                _moveSpeed = _data.SpeedFor(_pendingTier) * UnityEngine.Random.Range(0.9f, 1.1f);
                if (_agent == null)
                {
                    _agent = gameObject.AddComponent<NavMeshAgent>();
                    _agent.acceleration = 14f;
                    _agent.angularSpeed = 540f;
                    _agent.autoBraking = true;
                    _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                }
                _agent.height = 1.8f * _data.bodyScale.y;
                _agent.radius = Mathf.Min(0.32f * Mathf.Max(_data.bodyScale.x, _data.bodyScale.z), 0.6f);
                _agent.stoppingDistance = Mathf.Min(1.1f, _data.attackRange * 0.65f);
                _agent.avoidancePriority = _data.IsBoss ? 10 : 50;
                _agent.enabled = true;
                _agent.speed = _moveSpeed;
                _agent.Warp(transform.position);

                _target = null;
                _repathTimer = 0f;
                _attackTimer = 0f;
                _attackLanded = false;
                _staggerUntil = 0f;
                _stunUntil = 0f;
                _slowUntil = 0f;
                _holdUntil = 0f;
                _despawnAt = float.MaxValue;
                _stuckAnchor = transform.position;
                _stuckSince = Time.time;
                _offMeshSince = -1f;
                _spitAt = -1f;
                _nextSpitTime = Time.time + UnityEngine.Random.Range(1f, _data.spitCooldown);
            }
        }

        public override void OnNetworkDespawn()
        {
            s_Bosses.Remove(this);
            _mode.OnValueChanged -= OnModeChanged;
            Died = null;
            Recycled = null;
        }

        /// <summary>Puts the body back to a fresh, standing zombie (pooled objects keep last life's pose).</summary>
        void ResetVisuals()
        {
            _stunVisualUntil = 0f;
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

            _skin = skin;
            _bodyCollider = gameObject.AddComponent<CapsuleCollider>();
            _bodyHitbox = gameObject.AddComponent<Hitbox>();
            _bodyHitbox.Init(this, head: false);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _audio = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
            _audio.spatialBlend = 1f;
            _audio.minDistance = 2f;
            _audio.maxDistance = 30f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.playOnAwake = false;

            var cloth = new Color(0.22f, 0.2f, 0.18f);
            _visual = Greybox.Empty("Visual", transform, Vector3.zero);
            // The pose sits under the animated visual so a type can crouch, crawl or grow without fighting the animation.
            _pose = Greybox.Empty("Pose", _visual, Vector3.zero);
            var torso = Greybox.Prim(PrimitiveType.Capsule, "Torso", _pose, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.5f, 0.38f), cloth, collider: false);
            var legL = Greybox.Prim(PrimitiveType.Capsule, "LegL", _pose, new Vector3(-0.13f, 0.4f, 0f), new Vector3(0.2f, 0.4f, 0.2f), cloth, collider: false);
            var legR = Greybox.Prim(PrimitiveType.Capsule, "LegR", _pose, new Vector3(0.13f, 0.4f, 0f), new Vector3(0.2f, 0.4f, 0.2f), cloth, collider: false);

            var headGo = Greybox.Prim(PrimitiveType.Sphere, "Head", _pose, new Vector3(0f, 1.62f, 0.04f), Vector3.one * 0.34f, skin);
            headGo.AddComponent<Hitbox>().Init(this, head: true);
            _head = headGo.transform;
            var eye = new Color(1f, 0.85f, 0.2f);
            Greybox.Prim(PrimitiveType.Cube, "EyeL", _head, new Vector3(-0.17f, 0.08f, 0.44f), new Vector3(0.12f, 0.06f, 0.06f), eye, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Cube, "EyeR", _head, new Vector3(0.17f, 0.08f, 0.44f), new Vector3(0.12f, 0.06f, 0.06f), eye, collider: false, unlit: true);

            _armL = Greybox.Empty("ArmL", _pose, new Vector3(-0.32f, 1.32f, 0f));
            _armR = Greybox.Empty("ArmR", _pose, new Vector3(0.32f, 1.32f, 0f));
            var foreL = Greybox.Box("ForearmL", _armL, new Vector3(0f, 0f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), skin, collider: false);
            var foreR = Greybox.Box("ForearmR", _armR, new Vector3(0f, 0f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), skin, collider: false);
            _skinRenderers = new[] { headGo.GetComponent<Renderer>(), foreL.GetComponent<Renderer>(), foreR.GetComponent<Renderer>() };
            _clothRenderers = new[] { torso.GetComponent<Renderer>(), legL.GetComponent<Renderer>(), legR.GetComponent<Renderer>() };

            // Optional parts, switched on per type.
            var plate = new Color(0.32f, 0.33f, 0.36f);
            _armor = Greybox.Empty("Armor", _pose, Vector3.zero).gameObject;
            Greybox.Box("Chest", _armor.transform, new Vector3(0f, 1.05f, 0.2f), new Vector3(0.5f, 0.5f, 0.08f), plate, collider: false);
            Greybox.Box("ShoulderL", _armor.transform, new Vector3(-0.33f, 1.38f, 0f), new Vector3(0.26f, 0.1f, 0.3f), plate, collider: false);
            Greybox.Box("ShoulderR", _armor.transform, new Vector3(0.33f, 1.38f, 0f), new Vector3(0.26f, 0.1f, 0.3f), plate, collider: false);

            _sac = Greybox.Prim(PrimitiveType.Sphere, "AcidSac", _pose, new Vector3(0f, 1.1f, -0.26f), new Vector3(0.42f, 0.5f, 0.32f), FxSystem.Acid, collider: false, unlit: true);

            // Sticks out of the back past the body collider, so shots from behind reach it.
            _core = Greybox.Prim(PrimitiveType.Sphere, "WeakPoint", _pose, new Vector3(0f, 1.1f, -0.36f), Vector3.one * 0.26f, new Color(1f, 0.55f, 0.1f), collider: true, unlit: true);
            _coreHitbox = _core.AddComponent<Hitbox>();
            _coreHitbox.Init(this, head: false);

            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = _visual.GetComponentsInChildren<Renderer>(true);
            _baseMaterials = new Material[_renderers.Length];
        }

        /// <summary>Shapes and colours the body for this life's type, and sets its hitbox multipliers.</summary>
        void ApplyKind()
        {
            var d = _data;
            SetFlash(false);
            _visual.localScale = d.bodyScale;
            _armor.SetActive(d.armored);
            _sac.SetActive(d.spitRange > 0f);
            _core.SetActive(d.weakPointMultiplier > 0f);
            _bodyHitbox.multiplier = d.bodyMultiplier;
            _coreHitbox.multiplier = d.weakPointMultiplier;

            if (d.crawls)
            {
                // Pitched forward on all fours, centred over the agent.
                _pose.localPosition = new Vector3(0f, 0.3f, -0.6f);
                _pose.localRotation = Quaternion.Euler(80f, 0f, 0f);
                _bodyCollider.direction = 2;
                _bodyCollider.center = new Vector3(0f, 0.42f, 0.2f);
                _bodyCollider.height = 1.5f;
                _bodyCollider.radius = 0.28f;
            }
            else
            {
                _pose.localPosition = Vector3.zero;
                _pose.localRotation = Quaternion.identity;
                _bodyCollider.direction = 1;
                _bodyCollider.center = new Vector3(0f, 0.75f * d.bodyScale.y, 0f);
                _bodyCollider.height = 1.5f * d.bodyScale.y;
                _bodyCollider.radius = 0.3f * Mathf.Max(d.bodyScale.x, d.bodyScale.z);
            }

            var skin = Greybox.Lit(_skin * d.skinTint);
            var cloth = Greybox.Lit(d.clothColor);
            foreach (var r in _skinRenderers) r.sharedMaterial = skin;
            foreach (var r in _clothRenderers) r.sharedMaterial = cloth;
            for (int i = 0; i < _renderers.Length; i++) _baseMaterials[i] = _renderers[i].sharedMaterial;
        }

        // ---------------------------------------------------------------- damage (host)

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!IsServer || _mode.Value == ZombieMode.Dead) return default;
            long dealt = Math.Min(info.Amount, _health);
            _health -= dealt;
            if (_data.IsBoss) HealthFraction.Value = _maxHealth > 0 ? Mathf.Clamp01((float)((double)_health / _maxHealth)) : 0f;
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

        /// <summary>Host: a concussion bang. Stunned in place (bosses only slowed), then slowed for a while.</summary>
        public void ServerStun(float stunTime, float slowTime, float slowScale)
        {
            if (!IsServer || _mode.Value == ZombieMode.Dead) return;
            if (!_data.IsBoss) _stunUntil = Mathf.Max(_stunUntil, Time.time + stunTime);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + stunTime + slowTime);
            _slowScale = _data.IsBoss ? Mathf.Max(slowScale, 0.7f) : slowScale;
            StunRpc(_data.IsBoss ? 0f : stunTime);
        }

        /// <summary>Host: a melee hit stops it for a moment (not bosses or brutes).</summary>
        public void ServerStagger(float time)
        {
            if (!IsServer || _mode.Value == ZombieMode.Dead || _data.IsBoss || _data.armored) return;
            _holdUntil = Mathf.Max(_holdUntil, Time.time + time);
        }

        /// <summary>Whether the zombie is stunned right now (every machine, for the wobble).</summary>
        public bool IsStunned => Time.time < _stunVisualUntil;
        float _stunVisualUntil;

        [Rpc(SendTo.ClientsAndHost)]
        void StunRpc(float time) => _stunVisualUntil = Time.time + time;

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

        [Rpc(SendTo.ClientsAndHost)]
        void SpitRpc(Vector3 from, Vector3 to, float flightTime)
        {
            _swingStart = Time.time;
            _audio.PlayOneShot(ProceduralAudio.ZombieSwipe, 0.5f);
            AcidSpit.Launch(from, to, flightTime, _data, IsServer);
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
            float speed = Time.time < _staggerUntil ? _moveSpeed * _data.staggerSpeedScale : _moveSpeed;
            if (Time.time < _slowUntil) speed *= _slowScale;
            _agent.speed = speed;

            // Stunned (concussion) or knocked back (melee): stands still and can't attack.
            if (Time.time < _stunUntil || Time.time < _holdUntil)
            {
                _agent.isStopped = true;
                _spitAt = -1f;
                if (_mode.Value == ZombieMode.Attacking && !_attackLanded) _mode.Value = ZombieMode.Chasing;
                return;
            }

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

            // Zombies crowding a protect-mission survivor are where they should be, not stuck.
            var lured = MissionBoard.Lure;
            bool atLure = lured.HasValue && (lured.Value - transform.position).sqrMagnitude < 6f * 6f;
            if (_mode.Value == ZombieMode.Chasing && distance > StuckMinTargetDistance && !atLure)
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

            if (_mode.Value == ZombieMode.Chasing && UpdateSpit(distance, toTarget)) return;

            if (_mode.Value == ZombieMode.Chasing)
            {
                _agent.isStopped = false;
                _repathTimer -= dt;
                if (_repathTimer <= 0f)
                {
                    _target = FindTarget() ?? _target;
                    // A survivor being protected draws zombies that are closer to them than to any player.
                    Vector3 dest = _target.transform.position;
                    var lure = MissionBoard.Lure;
                    if (lure.HasValue && (lure.Value - transform.position).sqrMagnitude < distance * distance) dest = lure.Value;
                    _agent.SetDestination(dest);
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
                    if (distance <= _data.attackRange + 0.5f)
                    {
                        _target.ServerApplyDamage(_data.damage, transform.position);
                        // Crawlers go for the legs.
                        if (_data.slowTime > 0f) _target.ServerSlow(_data.slowOnHit, _data.slowTime);
                    }
                }
                if (_attackTimer >= _data.attackWindup + _data.attackRecovery) _mode.Value = ZombieMode.Chasing;
            }
        }

        /// <summary>
        /// Host: spitters stop to lob acid at a player they can see. The acid lands where the player stood,
        /// so standing still gets you burned. Returns true while the spit holds the zombie in place.
        /// </summary>
        bool UpdateSpit(float distance, Vector3 toTarget)
        {
            if (_data.spitRange <= 0f) return false;
            if (_spitAt < 0f)
            {
                bool inRange = distance <= _data.spitRange && distance > _data.attackRange + 1f;
                if (!inRange || Time.time < _nextSpitTime || !CanSee(_target)) return false;
                _spitAt = Time.time + _data.spitWindup;
                _nextSpitTime = Time.time + _data.spitCooldown;
            }

            _agent.isStopped = true;
            if (distance > 0.01f)
            {
                var face = Quaternion.LookRotation(toTarget / distance, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 540f * Time.deltaTime);
            }
            if (Time.time < _spitAt) return true;

            _spitAt = -1f;
            Vector3 from = _head.position;
            Vector3 to = _target.transform.position;
            float flight = Mathf.Clamp(Vector3.Distance(from, to) / Mathf.Max(1f, _data.spitSpeed), 0.35f, 1.4f);
            SpitRpc(from, to, flight);
            return false;
        }

        /// <summary>Nothing but zombies and players between this zombie's head and the player's chest.</summary>
        bool CanSee(NetPlayer player)
        {
            Vector3 from = _head.position;
            Vector3 to = player.transform.position + Vector3.up * 1.2f;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(from, dir / dist, _sightHits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = _sightHits[i].collider;
                if (c.GetComponentInParent<NetZombie>() != null || c.GetComponentInParent<NetPlayer>() != null) continue;
                return false;
            }
            return true;
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
            if (IsStunned) sway += Mathf.Sin(Time.time * 9f) * 14f;
            _visual.localRotation = Quaternion.Euler(8f - _flinch * 18f + (IsStunned ? 12f : 0f), 0f, sway);

            float armPitch;
            if (swing <= 0f) armPitch = 8f + Mathf.Sin(_walkPhase * 0.5f) * 6f;
            else if (swing < 1f) armPitch = Mathf.Lerp(8f, -70f, swing * swing);
            else armPitch = Mathf.Lerp(-70f, 25f, Mathf.Clamp01((swing - 1f) * 4f)) + Mathf.Lerp(0f, -17f, Mathf.Clamp01(swing - 1.25f));
            _armL.localRotation = Quaternion.Euler(armPitch + Mathf.Sin(_walkPhase) * 5f, -6f, 0f);
            _armR.localRotation = Quaternion.Euler(armPitch - Mathf.Sin(_walkPhase) * 5f, 6f, 0f);
        }
    }
}
