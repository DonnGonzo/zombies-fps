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
        // Ladders and jumps (the map's NavMesh links).
        const float ClimbSpeed = 2.4f;
        const float ClimbRise = 1.6f;
        // A player more than this far above or below can't be hit (on a ledge over the zombie's head).
        const float AttackHeight = 1.9f;
        // Scrambling: a zombie right under a player it has no path up to hauls itself up the side instead.
        const float ScrambleMinRise = 1.2f;
        const float ScrambleMaxRise = 6.5f;
        const float ScrambleReach = 4f;
        const float ScrambleDelay = 0.8f;
        // Climbing over: a ledge no higher than a jumping player's head is climbed straight away (quicker, and
        // without waiting) whenever the path map's way up is a long walk round. Players climb the same ledges.
        const float MantleMaxRise = 3.1f;
        const float MantleDelay = 0.25f;
        const float MantleClimbSpeed = 4f;
        // A player standing on top of something no zombie can stand on (too small for the path map) can still be
        // clawed at from below, up to this high.
        const float GrabReach = 3.3f;

        static readonly Color[] SkinTones =
        {
            new Color(0.42f, 0.5f, 0.36f),
            new Color(0.5f, 0.52f, 0.42f),
            new Color(0.38f, 0.44f, 0.4f),
            new Color(0.52f, 0.47f, 0.45f),
            new Color(0.34f, 0.36f, 0.3f),
        };

        // Clothes the dead were wearing when they turned. Picked per life, so the horde isn't a row of clones.
        static readonly Color[] ShirtColors =
        {
            new Color(0.55f, 0.52f, 0.45f),
            new Color(0.25f, 0.3f, 0.42f),
            new Color(0.42f, 0.15f, 0.13f),
            new Color(0.3f, 0.32f, 0.22f),
            new Color(0.4f, 0.34f, 0.25f),
            new Color(0.18f, 0.18f, 0.2f),
            new Color(0.45f, 0.4f, 0.18f),
        };
        static readonly Color[] PantsColors =
        {
            new Color(0.18f, 0.22f, 0.32f),
            new Color(0.3f, 0.27f, 0.2f),
            new Color(0.12f, 0.12f, 0.13f),
            new Color(0.26f, 0.25f, 0.23f),
        };
        static readonly Color[] HairColors =
        {
            new Color(0.12f, 0.1f, 0.08f),
            new Color(0.3f, 0.22f, 0.14f),
            new Color(0.45f, 0.43f, 0.4f),
            new Color(0.5f, 0.38f, 0.2f),
        };
        static readonly Color[] ShoeColors =
        {
            new Color(0.12f, 0.1f, 0.09f),
            new Color(0.25f, 0.18f, 0.12f),
            new Color(0.32f, 0.32f, 0.34f),
        };
        static readonly Color Bone = new Color(0.78f, 0.74f, 0.62f);
        static readonly Color Flesh = new Color(0.36f, 0.07f, 0.06f);
        static readonly Color Gore = new Color(0.16f, 0.02f, 0.02f);
        static readonly Color Plate = new Color(0.32f, 0.33f, 0.36f);
        static readonly Color EyeGlow = new Color(1f, 0.85f, 0.2f);

        // Rig layout (pose space, metres, before the type's body scale): hip pivot height, then the spine above it.
        const float HipHeight = 0.95f;
        const float TwoPi = Mathf.PI * 2f;

        // Where a dead body's joints settle: limp arms flung out, knees buckled, jaw hanging open.
        static readonly Quaternion DeadArmL = Quaternion.Euler(-35f, 0f, -55f);
        static readonly Quaternion DeadArmR = Quaternion.Euler(-35f, 0f, 55f);
        static readonly Quaternion DeadElbow = Quaternion.Euler(-30f, 0f, 0f);
        static readonly Quaternion DeadKnee = Quaternion.Euler(28f, 0f, 0f);
        static readonly Quaternion DeadLegL = Quaternion.Euler(-12f, 0f, -6f);
        static readonly Quaternion DeadLegR = Quaternion.Euler(-4f, 0f, 8f);
        static readonly Quaternion DeadJaw = Quaternion.Euler(32f, 0f, 0f);

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
        // The rest of the rig, animated procedurally: hips carry the legs and spine; the spine carries arms and head.
        Transform _hips, _spine, _chest, _jaw, _elbowL, _elbowR, _legL, _legR, _kneeL, _kneeR, _footL, _footR;
        GameObject _armor, _sac, _core;
        // Optional parts switched per life (variation) or per type.
        GameObject _hair, _gore, _flaps, _jawWound;
        GameObject[] _armorPads, _bossParts;
        CapsuleCollider _bodyCollider;
        Hitbox _bodyHitbox, _coreHitbox;
        // Renderers by what they're made of, recoloured per life.
        Renderer[] _skinRenderers, _shirtRenderers, _pantsRenderers, _shoeRenderers, _hairRenderers, _forearmRenderers, _shinRenderers;
        // Parts that thicken on bulky types, with their built size.
        Transform[] _bulkParts;
        Vector3[] _bulkScales;
        Color _skin;
        bool _built;
        // The imported, animated character (when one is in the project). The blocky rig above stays underneath it,
        // hidden, to carry the hitboxes, armour, acid sac and weak point, which are moved to follow the model's bones.
        ZombieModel _model;
        bool _modelOn;
        Renderer[] _rigRenderers;
        static readonly Vector3 SpineRest = new Vector3(0f, 0.06f, 0f);
        static readonly Vector3 HeadRest = new Vector3(0f, 0.61f, 0.04f);

        // Per-life look and gait (seeded from the network id, so every machine sees the same zombie).
        uint _seed;
        float _limp;
        int _limpSide;
        int _reachStyle;
        float _hunch, _tilt, _strideMul, _noise;
        bool _swingRight;
        int _swingCount;
        float _spitStart = -100f;
        // Where it's looking (the nearest player), smoothed.
        NetPlayer _lookAt;
        float _nextLookScan;
        float _lookYaw, _lookPitch, _near;
        float _vSpeed, _climb;

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
        bool _onLink, _linkClimb;
        Vector3 _linkStart, _linkEnd;
        float _linkTime, _linkDuration;
        bool _scrambling;
        Vector3 _scrambleStart, _scrambleEnd;
        float _scrambleTime, _scrambleDuration, _scrambleClimbSpeed = ClimbSpeed;
        bool _reachUp;
        float _belowSince = -1f;
        // Pose smoothing and body lean (see SmoothPose and AnimateBody).
        Transform[] _joints;
        float[] _jointRates;
        Quaternion[] _jointPrev;
        bool _jointsWarm;
        float _lastYaw, _lastSpeed, _turnLean, _accelLean;
        float _aboveSince = -1f;
        readonly RaycastHit[] _sightHits = new RaycastHit[8];

        public bool IsAlive => _mode.Value != ZombieMode.Dead;
        /// <summary>How much of a bullet's pierce this body uses up: a brute's bulk counts double and a boss stops it dead.</summary>
        public int PenetrationCost => Data.IsBoss ? 1000 : Kind == ZombieKind.Brute ? 2 : 1;
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
            // A fresh life starts from its new pose, not eased from the last one.
            _jointsWarm = false;
            _lastYaw = transform.eulerAngles.y;
            _turnLean = _accelLean = 0f;
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
                    // Gentler starts and stops than the default, and turning is done by hand (SteerFacing) so
                    // zombies swing round with some weight instead of snapping to face each new path corner.
                    _agent.acceleration = 9f;
                    _agent.angularSpeed = 540f;
                    _agent.updateRotation = false;
                    _agent.autoBraking = true;
                    _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                    // Ladders and jumps are moved by hand (TraverseLink) so they look like climbing and leaping.
                    _agent.autoTraverseOffMeshLink = false;
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
                _onLink = false;
                _scrambling = false;
                _reachUp = false;
                _belowSince = -1f;
                _aboveSince = -1f;
                _agent.updatePosition = true;
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
            _spitStart = -100f;
            _speedEstimate = 0f;
            _vSpeed = 0f;
            _climb = 0f;
            _lastPosition = transform.position;
            _lookAt = null;
            _nextLookScan = 0f;
            _lookYaw = _lookPitch = _near = 0f;
            SetFlash(false);
            foreach (var c in _colliders) c.enabled = true;
            _head.gameObject.SetActive(true);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
            if (_model != null) _model.ResetLife(_seed);
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

            // Placeholder colours: ApplyKind gives every part its real material for each life.
            var skinList = new List<Renderer>();
            var shirt = new List<Renderer>();
            var pants = new List<Renderer>();
            var shoes = new List<Renderer>();
            var hair = new List<Renderer>();
            var forearms = new List<Renderer>();
            var shins = new List<Renderer>();
            var bulk = new List<Transform>();
            var flesh = Greybox.Lit(Flesh, Surface.Skin, true);
            var gore = Greybox.Lit(Gore, Surface.Skin, true);
            var bone = Greybox.Lit(Bone, Surface.Plain, true);

            _visual = Greybox.Empty("Visual", transform, Vector3.zero);
            // The pose sits under the animated visual so a type can crouch, crawl or grow without fighting the animation.
            _pose = Greybox.Empty("Pose", _visual, Vector3.zero);
            _hips = Greybox.Empty("Hips", _pose, new Vector3(0f, HipHeight, 0f));
            Part(pants, PrimitiveType.Cube, "Pelvis", _hips, new Vector3(0f, 0f, 0f), new Vector3(0.33f, 0.2f, 0.22f));

            // Legs: hip, knee and ankle pivots, so the walk can bend the knee and keep the foot flat.
            BuildLeg(-1, pants, shins, shoes, bulk, out _legL, out _kneeL, out _footL);
            BuildLeg(1, pants, shins, shoes, bulk, out _legR, out _kneeR, out _footR);

            // Torso: belly, chest and a rounded shoulder yoke, slouched by the spine pivot.
            _spine = Greybox.Empty("Spine", _hips, new Vector3(0f, 0.06f, 0f));
            bulk.Add(Part(shirt, PrimitiveType.Cube, "Belly", _spine, new Vector3(0f, 0.12f, 0.005f), new Vector3(0.31f, 0.24f, 0.2f)).transform);
            _chest = Part(shirt, PrimitiveType.Cube, "Chest", _spine, new Vector3(0f, 0.36f, 0f), new Vector3(0.42f, 0.28f, 0.25f)).transform;
            bulk.Add(_chest);
            var yoke = Part(shirt, PrimitiveType.Capsule, "Shoulders", _spine, new Vector3(0f, 0.46f, -0.01f), new Vector3(0.17f, 0.24f, 0.17f)).transform;
            yoke.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bulk.Add(yoke);

            // Torn shirt hanging off the belly, and (some lives) a wound with ribs showing through.
            _flaps = Greybox.Empty("Flaps", _spine, Vector3.zero).gameObject;
            Part(shirt, PrimitiveType.Cube, "FlapA", _flaps.transform, new Vector3(0.09f, -0.03f, 0.09f), new Vector3(0.1f, 0.13f, 0.012f), shadow: false)
                .transform.localRotation = Quaternion.Euler(8f, 0f, 6f);
            Part(shirt, PrimitiveType.Cube, "FlapB", _flaps.transform, new Vector3(-0.1f, -0.02f, 0.095f), new Vector3(0.08f, 0.1f, 0.012f), shadow: false)
                .transform.localRotation = Quaternion.Euler(5f, 0f, -12f);
            _gore = Greybox.Empty("Wound", _spine, Vector3.zero).gameObject;
            Fixed(flesh, PrimitiveType.Cube, "Flesh", _gore.transform, new Vector3(0.06f, 0.3f, 0.127f), new Vector3(0.17f, 0.16f, 0.02f));
            for (int i = 0; i < 3; i++)
                Fixed(bone, PrimitiveType.Cube, "Rib", _gore.transform, new Vector3(0.06f, 0.25f + 0.05f * i, 0.138f), new Vector3(0.15f, 0.017f, 0.02f), shadow: false);
            Fixed(gore, PrimitiveType.Cube, "Gut", _gore.transform, new Vector3(-0.07f, 0.1f, 0.102f), new Vector3(0.1f, 0.08f, 0.02f), shadow: false);

            // Neck, with a bloody stump on top that shows once a headshot takes the head off.
            var neck = Greybox.Empty("Neck", _spine, new Vector3(0f, 0.53f, 0.01f));
            Part(skinList, PrimitiveType.Cylinder, "Neck", neck, new Vector3(0f, 0.03f, 0f), new Vector3(0.11f, 0.06f, 0.11f));
            Fixed(gore, PrimitiveType.Cylinder, "Stump", neck, new Vector3(0f, 0.09f, 0f), new Vector3(0.1f, 0.01f, 0.1f), shadow: false);

            BuildHead(skinList, hair, flesh, gore, bone);

            BuildArm(-1, shirt, forearms, skinList, bulk, out _armL, out _elbowL);
            BuildArm(1, shirt, forearms, skinList, bulk, out _armR, out _elbowR);

            // Optional parts, switched on per type.
            var plate = Greybox.Lit(Plate, Surface.Metal, true);
            _armor = Greybox.Empty("Armor", _spine, Vector3.zero).gameObject;
            Fixed(plate, PrimitiveType.Cube, "Chest", _armor.transform, new Vector3(0f, 0.36f, 0.14f), new Vector3(0.46f, 0.3f, 0.05f));
            Fixed(plate, PrimitiveType.Cube, "Belly", _armor.transform, new Vector3(0f, 0.13f, 0.115f), new Vector3(0.34f, 0.15f, 0.045f));
            Fixed(plate, PrimitiveType.Cube, "Back", _armor.transform, new Vector3(0f, 0.37f, -0.14f), new Vector3(0.44f, 0.28f, 0.04f));
            _armorPads = new[]
            {
                Fixed(plate, PrimitiveType.Cube, "ShoulderL", _armL, new Vector3(-0.02f, 0.02f, 0f), new Vector3(0.2f, 0.07f, 0.22f)).gameObject,
                Fixed(plate, PrimitiveType.Cube, "ShoulderR", _armR, new Vector3(0.02f, 0.02f, 0f), new Vector3(0.2f, 0.07f, 0.22f)).gameObject,
            };
            _armorPads[0].transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _armorPads[1].transform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            // Spitters carry a swollen, glowing acid sac on the back and a bulging throat to spit through.
            _sac = Greybox.Empty("AcidSac", _spine, Vector3.zero).gameObject;
            Greybox.Prim(PrimitiveType.Sphere, "Sac", _sac.transform, new Vector3(0f, 0.12f, -0.22f), new Vector3(0.38f, 0.46f, 0.3f), FxSystem.Acid, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Sphere, "Blister", _sac.transform, new Vector3(0.14f, 0.3f, -0.17f), Vector3.one * 0.12f, FxSystem.Acid, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Sphere, "Throat", _sac.transform, new Vector3(0f, 0.55f, 0.075f), new Vector3(0.11f, 0.09f, 0.09f), FxSystem.Acid, collider: false, unlit: true);

            // Sticks out of the back past the body collider, so shots from behind reach it. Rides the spine so
            // it stays where the glow is drawn; its size (and so the hitbox) is the same as ever.
            _core = Greybox.Prim(PrimitiveType.Sphere, "WeakPoint", _spine, new Vector3(0f, 0.09f, -0.36f), Vector3.one * 0.26f, new Color(1f, 0.55f, 0.1f), collider: true, unlit: true);
            _coreHitbox = _core.AddComponent<Hitbox>();
            _coreHitbox.Init(this, head: false);
            // A raw, fleshy rim around the weak point (in the core's own scale: 0.26).
            var rim = Fixed(gore, PrimitiveType.Cylinder, "Rim", _core.transform, new Vector3(0f, 0f, 0.25f), new Vector3(1.4f, 0.1f, 1.4f), shadow: false);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The Warden: bone spurs out of its back and broken shackles on its wrists.
            var rust = Greybox.Lit(new Color(0.36f, 0.24f, 0.17f), Surface.Rust, true);
            _bossParts = new[]
            {
                Spur(bone, _spine, new Vector3(-0.12f, 0.47f, -0.13f), new Vector3(-50f, 0f, 20f), 0.22f),
                Spur(bone, _spine, new Vector3(0.12f, 0.47f, -0.13f), new Vector3(-50f, 0f, -20f), 0.22f),
                Spur(bone, _spine, new Vector3(0f, 0.28f, -0.14f), new Vector3(-60f, 0f, 0f), 0.16f),
                Spur(bone, _armL, new Vector3(-0.06f, 0.04f, -0.04f), new Vector3(-30f, 0f, 40f), 0.14f),
                Spur(bone, _armR, new Vector3(0.06f, 0.04f, -0.04f), new Vector3(-30f, 0f, -40f), 0.14f),
                Fixed(rust, PrimitiveType.Cylinder, "ShackleL", _elbowL, new Vector3(0f, -0.22f, 0f), new Vector3(0.13f, 0.035f, 0.13f)).gameObject,
                Fixed(rust, PrimitiveType.Cylinder, "ShackleR", _elbowR, new Vector3(0f, -0.22f, 0f), new Vector3(0.13f, 0.035f, 0.13f)).gameObject,
            };

            _skinRenderers = skinList.ToArray();
            _shirtRenderers = shirt.ToArray();
            _pantsRenderers = pants.ToArray();
            _shoeRenderers = shoes.ToArray();
            _hairRenderers = hair.ToArray();
            _forearmRenderers = forearms.ToArray();
            _shinRenderers = shins.ToArray();
            _bulkParts = bulk.ToArray();
            _bulkScales = new Vector3[_bulkParts.Length];
            for (int i = 0; i < _bulkParts.Length; i++) _bulkScales[i] = _bulkParts[i].localScale;

            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = _visual.GetComponentsInChildren<Renderer>(true);
            _baseMaterials = new Material[_renderers.Length];

            _model = ZombieModel.Create(_visual, 1.78f);
            if (_model != null)
            {
                // Hidden under the model: everything but the armour, sac, weak point and the Warden's back spurs.
                var rig = new List<Renderer>();
                foreach (var r in _renderers)
                {
                    var t = r.transform;
                    if (t.IsChildOf(_sac.transform) || t.IsChildOf(_core.transform) || t.IsChildOf(_armor.transform)) continue;
                    if (t.parent == _spine && Array.IndexOf(_bossParts, r.gameObject) >= 0) continue;
                    rig.Add(r);
                }
                _rigRenderers = rig.ToArray();
                SetModelMode(GameSettings.AnimatedZombies);
            }
        }

        void SetModelMode(bool on)
        {
            _modelOn = on;
            _model.gameObject.SetActive(on);
            foreach (var r in _rigRenderers) r.enabled = !on;
            if (!on)
            {
                _spine.localPosition = SpineRest;
                _head.localPosition = HeadRest;
                _jointsWarm = false;
            }
        }

        /// <summary>
        /// The head: the hitbox sphere is the same size as the old head (radius 0.17) and pivots at its centre, so
        /// turning and tilting the head never moves what you're aiming at. Skull, brow, jaw and eyes hang off it.
        /// </summary>
        void BuildHead(List<Renderer> skin, List<Renderer> hair, Material flesh, Material gore, Material bone)
        {
            _head = Greybox.Empty("Head", _spine, HeadRest);
            var hit = _head.gameObject.AddComponent<SphereCollider>();
            hit.radius = 0.17f;
            _head.gameObject.AddComponent<Hitbox>().Init(this, head: true);

            Part(skin, PrimitiveType.Sphere, "Skull", _head, new Vector3(0f, 0.03f, 0f), new Vector3(0.27f, 0.31f, 0.29f));
            Part(skin, PrimitiveType.Cube, "Brow", _head, new Vector3(0f, 0.065f, 0.115f), new Vector3(0.24f, 0.05f, 0.07f))
                .transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Part(skin, PrimitiveType.Cube, "Nose", _head, new Vector3(0f, 0f, 0.145f), new Vector3(0.035f, 0.055f, 0.04f), shadow: false);
            // Glowing eyes sunk under the brow; they bloom in the dark.
            Greybox.Prim(PrimitiveType.Cube, "EyeL", _head, new Vector3(-0.055f, 0.032f, 0.128f), new Vector3(0.05f, 0.025f, 0.02f), EyeGlow, collider: false, unlit: true);
            Greybox.Prim(PrimitiveType.Cube, "EyeR", _head, new Vector3(0.055f, 0.032f, 0.128f), new Vector3(0.05f, 0.025f, 0.02f), EyeGlow, collider: false, unlit: true);

            // Mouth: dark inside, upper teeth, and a hinged jaw that chomps. Some lives have lost the jaw.
            Fixed(gore, PrimitiveType.Cube, "Mouth", _head, new Vector3(0f, -0.075f, 0.09f), new Vector3(0.12f, 0.05f, 0.06f), shadow: false);
            Fixed(bone, PrimitiveType.Cube, "TeethTop", _head, new Vector3(0f, -0.062f, 0.128f), new Vector3(0.11f, 0.022f, 0.02f), shadow: false);
            _jaw = Greybox.Empty("Jaw", _head, new Vector3(0f, -0.06f, 0.02f));
            Part(skin, PrimitiveType.Cube, "Jaw", _jaw, new Vector3(0f, -0.04f, 0.055f), new Vector3(0.17f, 0.06f, 0.14f));
            Fixed(bone, PrimitiveType.Cube, "TeethBottom", _jaw, new Vector3(0f, -0.012f, 0.112f), new Vector3(0.1f, 0.02f, 0.02f), shadow: false);
            _jawWound = Greybox.Empty("JawWound", _head, Vector3.zero).gameObject;
            Fixed(flesh, PrimitiveType.Cube, "Raw", _jawWound.transform, new Vector3(0f, -0.1f, 0.06f), new Vector3(0.13f, 0.05f, 0.09f), shadow: false);
            Fixed(flesh, PrimitiveType.Cube, "Tongue", _jawWound.transform, new Vector3(0f, -0.125f, 0.1f), new Vector3(0.04f, 0.015f, 0.08f), shadow: false)
                .transform.localRotation = Quaternion.Euler(30f, 0f, 0f);

            // Patchy tufts of what hair is left.
            _hair = Greybox.Empty("Hair", _head, Vector3.zero).gameObject;
            Part(hair, PrimitiveType.Cube, "TuftA", _hair.transform, new Vector3(-0.05f, 0.165f, 0.01f), new Vector3(0.1f, 0.04f, 0.13f), shadow: false)
                .transform.localRotation = Quaternion.Euler(0f, 20f, 14f);
            Part(hair, PrimitiveType.Cube, "TuftB", _hair.transform, new Vector3(0.06f, 0.155f, -0.04f), new Vector3(0.07f, 0.035f, 0.11f), shadow: false)
                .transform.localRotation = Quaternion.Euler(10f, -30f, -22f);
            Part(hair, PrimitiveType.Cube, "Back", _hair.transform, new Vector3(0f, 0.08f, -0.125f), new Vector3(0.18f, 0.12f, 0.05f), shadow: false)
                .transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        }

        /// <summary>Shoulder pivot (hangs straight down at rest), upper arm, elbow, forearm and a clawed hand.</summary>
        void BuildArm(int side, List<Renderer> sleeves, List<Renderer> forearms, List<Renderer> skin, List<Transform> bulk,
            out Transform shoulder, out Transform elbow)
        {
            string s = side < 0 ? "L" : "R";
            shoulder = Greybox.Empty("Arm" + s, _spine, new Vector3(0.25f * side, 0.47f, 0f));
            bulk.Add(Part(sleeves, PrimitiveType.Capsule, "UpperArm" + s, shoulder, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.16f, 0.12f)).transform);
            elbow = Greybox.Empty("Elbow" + s, shoulder, new Vector3(0f, -0.3f, 0f));
            bulk.Add(Part(forearms, PrimitiveType.Capsule, "Forearm" + s, elbow, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.14f, 0.1f)).transform);
            var hand = Greybox.Empty("Hand" + s, elbow, new Vector3(0f, -0.27f, 0f));
            Part(skin, PrimitiveType.Cube, "Palm" + s, hand, new Vector3(0f, -0.04f, 0f), new Vector3(0.05f, 0.09f, 0.09f));
            // Curled toward the back of the hand at rest, so a reaching arm claws downward.
            for (int i = 0; i < 2; i++)
                Part(skin, PrimitiveType.Cube, "Fingers" + s + i, hand, new Vector3(0.012f * side, -0.115f, 0.042f * i - 0.021f), new Vector3(0.018f, 0.08f, 0.034f), shadow: false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -22f * side);
        }

        /// <summary>Hip pivot, thigh, knee, shin, ankle and a shoe (or a bare foot).</summary>
        void BuildLeg(int side, List<Renderer> pants, List<Renderer> shins, List<Renderer> shoes, List<Transform> bulk,
            out Transform hip, out Transform knee, out Transform foot)
        {
            string s = side < 0 ? "L" : "R";
            hip = Greybox.Empty("Leg" + s, _hips, new Vector3(0.1f * side, -0.03f, 0f));
            bulk.Add(Part(pants, PrimitiveType.Capsule, "Thigh" + s, hip, new Vector3(0f, -0.21f, 0f), new Vector3(0.16f, 0.23f, 0.17f)).transform);
            knee = Greybox.Empty("Knee" + s, hip, new Vector3(0f, -0.44f, 0f));
            bulk.Add(Part(shins, PrimitiveType.Capsule, "Shin" + s, knee, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.22f, 0.13f)).transform);
            foot = Greybox.Empty("Foot" + s, knee, new Vector3(0f, -0.42f, 0f));
            Part(shoes, PrimitiveType.Cube, "Shoe" + s, foot, new Vector3(0f, -0.015f, 0.06f), new Vector3(0.12f, 0.09f, 0.27f));
        }

        /// <summary>A decorative textured part whose material is set per life, listed under its material group.</summary>
        Renderer Part(List<Renderer> group, PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, bool shadow = true)
        {
            var r = Greybox.Prim(type, name, parent, pos, scale, Color.grey, collider: false, surface: Surface.Fabric, moving: true).GetComponent<Renderer>();
            // Tiny parts cast next to no visible shadow; skipping them keeps 40 zombies cheap in the shadow pass.
            if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            group.Add(r);
            return r;
        }

        /// <summary>A decorative part with a fixed material (bone, gore, armour).</summary>
        static Renderer Fixed(Material material, PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, bool shadow = true)
        {
            var r = Greybox.Prim(type, name, parent, pos, scale, Color.grey, collider: false).GetComponent<Renderer>();
            r.sharedMaterial = material;
            if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        static GameObject Spur(Material bone, Transform parent, Vector3 pos, Vector3 euler, float length)
        {
            var r = Fixed(bone, PrimitiveType.Cube, "Spur", parent, pos, new Vector3(0.05f, length, 0.05f));
            r.transform.localRotation = Quaternion.Euler(euler);
            return r.gameObject;
        }

        /// <summary>A small deterministic random stream (xorshift), seeded per life from the network id.</summary>
        float Rand()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777216f;
        }

        int RandIndex(int count) => Mathf.Min((int)(Rand() * count), count - 1);

        /// <summary>Shapes and colours the body for this life's type, and sets its hitbox multipliers.</summary>
        void ApplyKind()
        {
            var d = _data;
            SetFlash(false);
            _visual.localScale = d.bodyScale;
            _armor.SetActive(d.armored);
            foreach (var pad in _armorPads) pad.SetActive(d.armored);
            _sac.SetActive(d.spitRange > 0f);
            _core.SetActive(d.weakPointMultiplier > 0f);
            foreach (var part in _bossParts) part.SetActive(d.IsBoss);
            _bodyHitbox.multiplier = d.bodyMultiplier;
            _coreHitbox.multiplier = d.weakPointMultiplier;

            // Seeded from the network id, which every machine shares, so everyone sees the same corpse.
            _seed = (uint)(NetworkObjectId * 2654435761UL) ^ 0x9E3779B9u;
            if (_seed == 0) _seed = 1;
            Rand();
            _skin = SkinTones[RandIndex(SkinTones.Length)];
            bool bulky = d.armored || d.IsBoss;

            // Height and build vary a little; brutes and the Warden are thick through the chest and arms.
            float height = Mathf.Lerp(0.95f, 1.05f, Rand());
            float bulk = (d.kind == ZombieKind.Brute ? 1.3f : d.IsBoss ? 1.15f : d.kind == ZombieKind.Runner ? 0.9f : 1f) * Mathf.Lerp(0.94f, 1.08f, Rand());
            for (int i = 0; i < _bulkParts.Length; i++)
            {
                var b = _bulkScales[i];
                _bulkParts[i].localScale = new Vector3(b.x * bulk, b.y, b.z * Mathf.Lerp(1f, bulk, 0.6f));
            }
            _armL.localPosition = new Vector3(-0.21f * bulk - 0.04f, 0.47f, 0f);
            _armR.localPosition = new Vector3(0.21f * bulk + 0.04f, 0.47f, 0f);
            _armor.transform.localScale = new Vector3(bulk, 1f, Mathf.Lerp(1f, bulk, 0.6f));

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
            // Crawlers keep their exact size: their pose is fitted to the low body collider.
            _pose.localScale = Vector3.one * (d.crawls ? 1f : height);

            // Clothes: walkers wear anything; the other types lean toward their own colour so they read at a glance.
            float kindTint = d.kind == ZombieKind.Walker ? 0f : bulky ? 0.85f : 0.5f;
            var shirtColor = Color.Lerp(ShirtColors[RandIndex(ShirtColors.Length)], d.clothColor, kindTint);
            var pantsColor = Color.Lerp(PantsColors[RandIndex(PantsColors.Length)], d.clothColor * 0.8f, kindTint * 0.6f);
            var skinMat = Greybox.Lit(_skin * d.skinTint, Surface.Skin, true);
            var shirtMat = Greybox.Lit(shirtColor, Surface.Fabric, true);
            var pantsMat = Greybox.Lit(pantsColor, Surface.Fabric, true);
            bool barefoot = !bulky && Rand() < 0.25f;
            var shoeMat = barefoot ? skinMat : Greybox.Lit(ShoeColors[RandIndex(ShoeColors.Length)], Surface.Rubber, true);
            bool longSleeves = Rand() < 0.5f;
            bool tornPants = Rand() < 0.2f;
            foreach (var r in _skinRenderers) r.sharedMaterial = skinMat;
            foreach (var r in _shirtRenderers) r.sharedMaterial = shirtMat;
            foreach (var r in _pantsRenderers) r.sharedMaterial = pantsMat;
            foreach (var r in _shoeRenderers) r.sharedMaterial = shoeMat;
            foreach (var r in _forearmRenderers) r.sharedMaterial = longSleeves ? shirtMat : skinMat;
            foreach (var r in _shinRenderers) r.sharedMaterial = tornPants ? skinMat : pantsMat;
            var hairMat = Greybox.Lit(HairColors[RandIndex(HairColors.Length)], Surface.Fabric, true);
            foreach (var r in _hairRenderers) r.sharedMaterial = hairMat;

            _hair.SetActive(!d.IsBoss && Rand() < 0.55f);
            // Spitters keep their jaw: they need a mouth to spit with.
            bool noJaw = d.spitRange <= 0f && Rand() < 0.2f;
            _jaw.gameObject.SetActive(!noJaw);
            _jawWound.SetActive(noJaw);
            _gore.SetActive(!d.armored && Rand() < 0.4f);
            _flaps.SetActive(Rand() < 0.55f);

            // Gait: a limp on some, a hunch, a broken-neck tilt, and how they hold their arms.
            _limp = !bulky && !d.crawls && Rand() < 0.3f ? Mathf.Lerp(0.5f, 1f, Rand()) : 0f;
            _limpSide = Rand() < 0.5f ? -1 : 1;
            _reachStyle = RandIndex(3);
            _hunch = Mathf.Lerp(6f, 18f, Rand()) + (bulky ? 6f : 0f);
            _tilt = Mathf.Lerp(-14f, 14f, Rand());
            _strideMul = Mathf.Lerp(0.9f, 1.1f, Rand());
            _noise = Rand() * 10f;

            for (int i = 0; i < _renderers.Length; i++) _baseMaterials[i] = _renderers[i].sharedMaterial;

            if (_model != null)
            {
                // Each type leans toward its own colour; walkers only faintly, so they keep the model's own look.
                var cloth = d.clothColor;
                float peak = Mathf.Max(cloth.r, Mathf.Max(cloth.g, cloth.b), 0.01f);
                var hue = new Color(cloth.r / peak, cloth.g / peak, cloth.b / peak);
                var tint = Color.Lerp(Color.white, hue, d.kind == ZombieKind.Walker ? 0.2f : 0.6f) * Color.Lerp(Color.white, d.skinTint, 0.7f);
                _model.SetLook(tint, d.crawls ? 1f : height);
            }
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
                if (_onLink)
                {
                    // Killed mid-climb or mid-leap: drop to the lower end rather than hang in the air.
                    _onLink = false;
                    transform.position = _linkStart.y <= _linkEnd.y ? _linkStart : _linkEnd;
                }
                if (_scrambling)
                {
                    // Killed mid-scramble: fall back to the foot of the climb.
                    _scrambling = false;
                    _agent.updatePosition = true;
                    transform.position = _scrambleStart;
                }
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
            // Alternate hands, so a zombie mauling you doesn't look like a loop.
            _swingRight = !_swingRight;
            _swingCount++;
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
            // A heave of the whole upper body and a gaping jaw, rather than a swipe.
            _spitStart = Time.time;
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
            if (_model != null) _model.Die((int)(NetworkObjectId + (ulong)_swingCount), headshot);
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
            if (_scrambling)
            {
                UpdateScramble(dt);
                return;
            }
            if (_agent.isOnOffMeshLink)
            {
                TraverseLink(dt);
                return;
            }
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
            float rise = toTarget.y;
            bool reachUp = _reachUp && rise >= AttackHeight && rise < GrabReach && CanReachUp(rise);
            bool level = Mathf.Abs(rise) < AttackHeight || reachUp;
            float attackRange = reachUp ? _data.attackRange + 0.6f : _data.attackRange;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            // Zombies crowding a protect-mission survivor are where they should be, not stuck.
            var lured = MissionBoard.Lure;
            bool goingForLure = lured.HasValue && (lured.Value - transform.position).sqrMagnitude < distance * distance;
            if (_mode.Value == ZombieMode.Chasing && !goingForLure && !_data.IsBoss
                && (TryStartScramble(rise, distance) || TryStartDrop(rise))) return;

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
                SteerFacing(dt, toTarget, distance);
                if (distance <= attackRange && level)
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
                    if (distance <= attackRange + 0.5f && level)
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
        /// Host: crosses one of the map's NavMesh links. Ladders (a big height change) are climbed hand over hand,
        /// straight up at the foot and then over the top (or the other way down); anything else is a leap.
        /// </summary>
        void TraverseLink(float dt)
        {
            if (!_onLink)
            {
                var link = _agent.currentOffMeshLinkData;
                _onLink = true;
                _linkStart = transform.position;
                _linkEnd = link.endPos;
                Vector3 flat = _linkEnd - _linkStart;
                float rise = flat.y;
                flat.y = 0f;
                float speed = Mathf.Max(1.5f, _agent.speed);
                _linkClimb = Mathf.Abs(rise) > ClimbRise;
                _linkDuration = _linkClimb ? Mathf.Abs(rise) / ClimbSpeed + flat.magnitude / speed : Mathf.Max(0.4f, flat.magnitude / speed + 0.2f);
                _linkTime = 0f;
                if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                if (_mode.Value == ZombieMode.Attacking) _mode.Value = ZombieMode.Chasing;
            }

            _linkTime += dt;
            float t = Mathf.Clamp01(_linkTime / _linkDuration);
            Vector3 p;
            if (_linkClimb)
            {
                Vector3 flat = _linkEnd - _linkStart;
                float rise = flat.y;
                flat.y = 0f;
                float climbShare = Mathf.Clamp01(Mathf.Abs(rise) / ClimbSpeed / _linkDuration);
                bool up = rise > 0f;
                // Up: climb at the foot, then step over onto the ledge. Down: step to the edge, then climb down.
                float vertical = up ? Mathf.Clamp01(t / climbShare) : Mathf.Clamp01((t - (1f - climbShare)) / climbShare);
                float across = up ? Mathf.Clamp01((t - climbShare) / Mathf.Max(0.01f, 1f - climbShare))
                    : Mathf.Clamp01(t / Mathf.Max(0.01f, 1f - climbShare));
                p = _linkStart + flat * across + Vector3.up * (rise * vertical);
            }
            else
            {
                float rise = _linkEnd.y - _linkStart.y;
                float arc = 0.5f + 0.5f * Mathf.Abs(rise);
                p = Vector3.Lerp(_linkStart, _linkEnd, t) + Vector3.up * (4f * arc * t * (1f - t));
            }
            transform.position = p;

            if (t >= 1f)
            {
                _onLink = false;
                _agent.CompleteOffMeshLink();
                _stuckAnchor = transform.position;
                _stuckSince = Time.time;
                _repathTimer = 0f;
            }
        }

        /// <summary>
        /// Host: a zombie under a player standing on something it has no path up to (a crate, a parkour block,
        /// a ledge whose way up is shut off) scrambles up the side to get at them, after a moment's pause. A
        /// player on a high spot with a ladder or a jump up is still reached the long way, by the path map.
        /// </summary>
        bool TryStartScramble(float rise, float distance)
        {
            if (rise < ScrambleMinRise || rise > ScrambleMaxRise || distance > ScrambleReach)
            {
                _belowSince = -1f;
                _reachUp = false;
                return false;
            }
            // Zombies re-path five times a second, so a path is often still being worked out. Wait for it, but keep
            // the timer running: resetting it here meant the pause before climbing never finished, and zombies
            // stood under players on crates and containers forever.
            if (_agent.pathPending) return false;
            Vector3 target = _target.transform.position;
            bool noWayUp = _agent.pathStatus != NavMeshPathStatus.PathComplete || _agent.pathEndPosition.y < target.y - 1f;
            // A ledge a player could climb, with the way up a long walk round (or unknown): climb it instead.
            bool mantle = rise <= MantleMaxRise;
            bool detour = mantle && !noWayUp && _agent.remainingDistance > distance * 2f + 6f;
            if (!noWayUp && !detour)
            {
                _belowSince = -1f;
                _reachUp = false;
                return false;
            }
            if (_belowSince < 0f) _belowSince = Time.time;
            if (Time.time - _belowSince < (mantle ? MantleDelay : ScrambleDelay)) return false;

            // Land beside the player, on the side of them we're climbing from.
            Vector3 back = transform.position - target;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : -transform.forward;
            Vector3 land = Vector3.zero;
            bool found = false;
            foreach (float off in new[] { 0.7f, 0f })
            {
                if (NavMesh.SamplePosition(target + back * off, out var hit, 1.2f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - target.y) < 0.6f)
                {
                    land = hit.position;
                    found = true;
                    break;
                }
            }
            // Nowhere to stand up there (a pillar or a crate too small for the path map): claw at them from below.
            _reachUp = !found;
            Vector3 start = transform.position;
            Vector3 top = new Vector3(start.x, land.y + 0.6f, start.z);
            // Nothing overhead on the way up, and nothing between the top of the climb and the landing.
            if (!found || !Clear(start + Vector3.up * 1f, top + Vector3.up * 1.2f) || !Clear(top + Vector3.up * 1.2f, land + Vector3.up * 1f))
            {
                _belowSince = Time.time;
                return false;
            }

            _scrambling = true;
            _belowSince = -1f;
            _scrambleClimbSpeed = land.y - start.y <= MantleMaxRise ? MantleClimbSpeed : ClimbSpeed;
            _scrambleStart = start;
            _scrambleEnd = land;
            _scrambleTime = 0f;
            float across = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(land.x, 0f, land.z));
            _scrambleDuration = (land.y - start.y) / _scrambleClimbSpeed + across / Mathf.Max(1.5f, _moveSpeed) + 0.2f;
            _agent.isStopped = true;
            _agent.updatePosition = false;
            Vector3 face = land - start;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            return true;
        }

        /// <summary>
        /// Host: a zombie stranded up on something with no way down (it scrambled onto a container after a
        /// player who has since jumped off) walks to the edge nearest the player and drops down to them.
        /// </summary>
        bool TryStartDrop(float rise)
        {
            if (rise > -ScrambleMinRise)
            {
                _aboveSince = -1f;
                return false;
            }
            if (_agent.pathPending) return false;
            Vector3 target = _target.transform.position;
            bool noWayDown = _agent.pathStatus != NavMeshPathStatus.PathComplete || _agent.pathEndPosition.y > target.y + 1f;
            if (!noWayDown)
            {
                _aboveSince = -1f;
                return false;
            }
            if (_aboveSince < 0f) _aboveSince = Time.time;
            if (Time.time - _aboveSince < ScrambleDelay) return false;

            Vector3 start = transform.position;
            Vector3 toward = target - start;
            toward.y = 0f;
            float flat = toward.magnitude;
            toward = flat > 0.01f ? toward / flat : transform.forward;
            foreach (float off in new[] { 1.5f, 2.5f, 3.5f })
            {
                Vector3 probe = start + toward * Mathf.Min(off, Mathf.Max(1f, flat));
                probe.y = target.y;
                if (!NavMesh.SamplePosition(probe, out var hit, 1.5f, NavMesh.AllAreas) || hit.position.y > start.y - 1f) continue;
                Vector3 over = new Vector3(hit.position.x, start.y, hit.position.z);
                if (!Clear(start + Vector3.up * 1f, over + Vector3.up * 1f) || !Clear(over + Vector3.up * 1f, hit.position + Vector3.up * 0.5f)) continue;

                _scrambling = true;
                _aboveSince = -1f;
                _scrambleStart = start;
                _scrambleEnd = hit.position;
                _scrambleTime = 0f;
                float across = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(hit.position.x, 0f, hit.position.z));
                _scrambleDuration = across / Mathf.Max(1.5f, _moveSpeed) + (start.y - hit.position.y) / 6f + 0.1f;
                _agent.isStopped = true;
                _agent.updatePosition = false;
                transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
                return true;
            }
            _aboveSince = Time.time;
            return false;
        }

        /// <summary>Host: hand over hand up the side, then over the edge onto the player's level (or off the edge and down).</summary>
        void UpdateScramble(float dt)
        {
            _scrambleTime += dt;
            float t = Mathf.Clamp01(_scrambleTime / _scrambleDuration);
            float rise = _scrambleEnd.y - _scrambleStart.y;
            if (rise < 0f)
            {
                // Dropping: walk off the edge, then fall.
                float walkShare = Mathf.Clamp01(1f - (-rise / 6f) / _scrambleDuration);
                float off = Mathf.Clamp01(t / Mathf.Max(0.01f, walkShare));
                float fall = Mathf.Clamp01((t - walkShare) / Mathf.Max(0.01f, 1f - walkShare));
                Vector3 step = _scrambleEnd - _scrambleStart;
                step.y = 0f;
                transform.position = _scrambleStart + step * off + Vector3.up * (rise * fall * fall);
                if (t < 1f) return;
                FinishScramble();
                return;
            }
            float climbShare = Mathf.Clamp01(rise / _scrambleClimbSpeed / _scrambleDuration);
            float vertical = Mathf.Clamp01(t / Mathf.Max(0.01f, climbShare));
            float across = Mathf.Clamp01((t - climbShare) / Mathf.Max(0.01f, 1f - climbShare));
            Vector3 flat = _scrambleEnd - _scrambleStart;
            flat.y = 0f;
            // A little extra height at the lip, so the body clears the edge.
            float lip = 0.35f * Mathf.Sin(across * Mathf.PI);
            transform.position = _scrambleStart + flat * across + Vector3.up * (rise * vertical + lip);
            if (t < 1f) return;
            FinishScramble();
        }

        void FinishScramble()
        {
            _scrambling = false;
            _agent.updatePosition = true;
            _agent.Warp(_scrambleEnd);
            _agent.isStopped = false;
            _stuckAnchor = transform.position;
            _stuckSince = Time.time;
            _repathTimer = 0f;
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

        /// <summary>
        /// Open air straight up from this zombie to the height of the player's feet, then across to them: true beside a
        /// crate the player stands on, false under a deck (the deck's floor is in the way).
        /// </summary>
        bool CanReachUp(float rise)
        {
            Vector3 up = transform.position + Vector3.up * (rise + 0.3f);
            return Clear(transform.position + Vector3.up * 1.5f, up) && Clear(up, _target.transform.position + Vector3.up * 0.3f);
        }

        /// <summary>Nothing but zombies and players between this zombie's head and the player's chest.</summary>
        bool CanSee(NetPlayer player) => Clear(_head.position, player.transform.position + Vector3.up * 1.2f);

        /// <summary>Nothing but zombies and players on the straight line between two points.</summary>
        bool Clear(Vector3 from, Vector3 to)
        {
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
            if (_model != null && _modelOn != GameSettings.AnimatedZombies) SetModelMode(!_modelOn);
            if (_deathTime > 0f)
            {
                float t = Time.time - _deathTime;
                if (_modelOn)
                {
                    // The death clip does the falling; the body only sinks away at the end.
                    float sinkM = Mathf.Clamp01((t - DeathFallTime - CorpseTime) / SinkTime);
                    _visual.localRotation = Quaternion.identity;
                    _visual.localPosition = new Vector3(0f, -0.6f * sinkM, 0f);
                    if (t < DeathFallTime + CorpseTime + 0.1f)
                    {
                        _model.Animate(dt, 0f, _data.crawls, 0f, 1f, 0, 99f, false);
                        FollowModel();
                    }
                    return;
                }
                float fall = Mathf.Clamp01(t / DeathFallTime);
                _visual.localRotation = Quaternion.AngleAxis(fall * fall * 88f, _fallAxis);
                float sink = Mathf.Clamp01((t - DeathFallTime - CorpseTime) / SinkTime);
                _visual.localPosition = new Vector3(0f, -0.6f * sink, 0f);
                // The limbs go slack as it drops, then stay put (no need to keep animating a lying corpse).
                if (t < DeathFallTime + 0.8f) RelaxCorpse(dt);
                return;
            }

            SetFlash(Time.time < _flashUntil);
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 6f);

            if (dt > 0f)
            {
                Vector3 delta = transform.position - _lastPosition;
                // Rising fast means climbing a ladder or scrambling up a ledge: the arms claw overhead.
                _vSpeed = Mathf.Lerp(_vSpeed, delta.y / dt, 1f - Mathf.Exp(-8f * dt));
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
            AnimateBody(swing);
        }

        void SetFlash(bool flashing)
        {
            if (flashing == _flashing) return;
            _flashing = flashing;
            var white = Greybox.Lit(Color.white);
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = flashing ? white : _baseMaterials[i];
            if (_model != null) _model.SetFlash(flashing);
        }

        /// <summary>
        /// Procedural animation for the whole rig, every machine, from the observed speed and the attack/spit events.
        /// Allocation-free: it only writes local rotations, so 40 zombies cost next to nothing.
        /// </summary>
        /// <param name="swing">0 none, 0..1 wind-up, 1..2 strike and recover.</param>
        void AnimateBody(float swing)
        {
            var d = _data;
            float dt = Time.deltaTime;
            float now = Time.time;
            float speed = _speedEstimate;
            bool stunned = IsStunned;
            bool runner = d.kind == ZombieKind.Runner;

            if (_modelOn)
            {
                _model.Animate(dt, speed, d.crawls, swing, d.attackWindup + d.attackRecovery, _swingCount, now - _spitStart, stunned);
                LeanBody(dt, now, speed, stunned);
                FollowModel();
                return;
            }

            // Stride length sets the cadence, so the feet keep roughly in step with the ground instead of skating.
            float stride = (runner ? 2f : d.crawls ? 0.9f : 1.25f) * d.bodyScale.y * _strideMul;
            _walkPhase += dt * Mathf.Min(speed / stride, 3f) * TwoPi;
            if (_walkPhase > 1000f) _walkPhase -= 100f * TwoPi;
            float gait = Mathf.Clamp01(speed / Mathf.Max(0.3f, d.walkSpeed * 0.7f));
            if (stunned) gait *= 0.2f;
            float run = runner ? Mathf.Clamp01((speed - 1f) / 2.5f) : 0f;

            UpdateLook(dt, now);
            _climb = Mathf.MoveTowards(_climb, _vSpeed > 0.8f ? 1f : 0f, dt * 4f);

            // Attack: arm(s) wound up overhead with the torso twisted away, then a lunging downward rake.
            float atk = 0f, atkPitch = 0f, atkElbow = 0f, atkLean = 0f, atkTwist = 0f;
            if (swing > 0f)
            {
                if (swing < 1f)
                {
                    float e = swing * swing * (3f - 2f * swing);
                    atk = Mathf.Clamp01(swing * 4f);
                    atkPitch = Mathf.Lerp(-70f, -160f, e);
                    atkElbow = Mathf.Lerp(-30f, -80f, e);
                    atkLean = Mathf.Lerp(0f, -10f, e);
                    atkTwist = Mathf.Lerp(0f, 18f, e);
                }
                else
                {
                    float s = swing - 1f;
                    float k = Mathf.Clamp01(s * 5f);
                    k = 1f - (1f - k) * (1f - k);
                    atk = 1f - Mathf.Clamp01((s - 0.35f) / 0.65f);
                    atkPitch = Mathf.Lerp(-160f, -30f, k);
                    atkElbow = Mathf.Lerp(-80f, -8f, k);
                    atkLean = Mathf.Lerp(-10f, 22f, k);
                    atkTwist = Mathf.Lerp(18f, -22f, k);
                }
            }
            // Spit: a heave that peaks fast and settles.
            float st = now - _spitStart;
            float heave = st < 0.1f ? st / 0.1f : Mathf.Clamp01(1f - (st - 0.1f) / 0.6f);

            if (d.crawls) AnimateCrawl(now, gait, atk, atkLean, heave);
            else AnimateUpright(now, gait, run, stunned, d.armored || d.IsBoss, atk, atkPitch, atkElbow, atkLean, atkTwist, heave);

            SmoothPose(dt);
            LeanBody(dt, now, speed, stunned);
        }

        void LeanBody(float dt, float now, float speed, bool stunned)
        {
            var d = _data;
            // Weight: lean into speeding up, rock back when stopping, bank into turns. Measured from how the body
            // actually moves, so it's the same on every machine.
            float yaw = transform.eulerAngles.y;
            if (dt > 0f)
            {
                float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
                _turnLean = Mathf.Lerp(_turnLean, Mathf.Clamp(-yawRate * 0.045f, -9f, 9f), 1f - Mathf.Exp(-5f * dt));
                float accel = (speed - _lastSpeed) / dt;
                _accelLean = Mathf.Lerp(_accelLean, Mathf.Clamp(accel * 2.2f, -7f, 7f), 1f - Mathf.Exp(-4f * dt));
            }
            _lastYaw = yaw;
            _lastSpeed = speed;

            // Whole-body reactions: a jolt back when shot, a drunken wobble when stunned.
            float wobble = stunned ? Mathf.Sin(now * 9f) * 8f : 0f;
            _visual.localRotation = Quaternion.Euler(-_flinch * 8f + (stunned ? 10f : 0f) + (d.crawls ? 0f : _accelLean), 0f, wobble + _turnLean);
        }

        /// <summary>
        /// Keeps the hidden rig on the model's bones: the spine (and with it the armour, acid sac and weak point) on
        /// the model's spine, and the head hitbox on its skull, so you hit what you see.
        /// </summary>
        void FollowModel()
        {
            var spine = _model.Spine;
            Vector3 up = _model.Neck.position - spine.position;
            if (up.sqrMagnitude < 1e-6f) up = transform.up;
            up.Normalize();
            Vector3 forward = Vector3.Cross(_model.transform.right, up);
            if (forward.sqrMagnitude < 1e-6f) forward = transform.forward;
            _spine.SetPositionAndRotation(spine.position, Quaternion.LookRotation(forward, up));
            _head.position = _model.HeadCenter;
        }

        /// <summary>
        /// Follow-through: each joint eases toward the pose just computed instead of jumping to it, heavier parts
        /// (spine, head) more slowly than the limbs. Smooths every switch between walking, reaching, striking and
        /// flinching, and gives the body a little lag and weight.
        /// </summary>
        void SmoothPose(float dt)
        {
            if (_joints == null)
            {
                _joints = new[] { _hips, _spine, _head, _jaw, _armL, _elbowL, _armR, _elbowR, _legL, _kneeL, _footL, _legR, _kneeR, _footR };
                _jointRates = new[] { 18f, 9f, 8f, 22f, 13f, 15f, 13f, 15f, 24f, 26f, 26f, 24f, 26f, 26f };
                _jointPrev = new Quaternion[_joints.Length];
                _jointsWarm = false;
            }
            for (int i = 0; i < _joints.Length; i++)
            {
                var j = _joints[i];
                if (j == null) continue;
                var target = j.localRotation;
                if (_jointsWarm && dt > 0f)
                    j.localRotation = Quaternion.Slerp(_jointPrev[i], target, 1f - Mathf.Exp(-_jointRates[i] * dt));
                _jointPrev[i] = j.localRotation;
            }
            _jointsWarm = true;
        }

        void AnimateUpright(float now, float gait, float run, bool stunned, bool bothHands,
            float atk, float atkPitch, float atkElbow, float atkLean, float atkTwist, float heave)
        {
            float p = _walkPhase;
            float sinP = Mathf.Sin(p), cosP = Mathf.Cos(p);

            // Legs: the left leg swings on the phase, the right half a cycle behind. A limping leg barely bends
            // and scuffs its toe, and the body drops onto it each time it takes the weight.
            float hipAmp = Mathf.Lerp(22f, 42f, run), kneeAmp = Mathf.Lerp(42f, 90f, run);
            PoseLeg(_legL, _kneeL, _footL, p, -1, gait, hipAmp, kneeAmp, _limpSide < 0 ? _limp : 0f);
            PoseLeg(_legR, _kneeR, _footR, p + Mathf.PI, 1, gait, hipAmp, kneeAmp, _limpSide > 0 ? _limp : 0f);

            float badStance = Mathf.Max(0f, -Mathf.Cos(_limpSide < 0 ? p : p + Mathf.PI)) * _limp * gait;
            float bob = Mathf.Lerp(0.03f, 0.06f, run) * gait * (Mathf.Abs(cosP) - 1f);
            _hips.localPosition = new Vector3(0f, HipHeight + bob - 0.07f * badStance, 0f);
            float hipYaw = 7f * gait * sinP;
            float hipRoll = -_limpSide * 9f * badStance;
            _hips.localRotation = Quaternion.Euler(run * 5f, hipYaw, hipRoll);

            // Spine: a slouch, a slow drunken lurch, counter-twist against the hips, and the attack's lean.
            float lurch = Mathf.Sin(now * 0.8f + _noise) * 5f * (1f - run);
            float pitch = _hunch + run * 12f + atkLean * atk + heave * 25f - _flinch * 22f + _near * 4f;
            float side = bothHands ? 0f : _swingRight ? 1f : -1f;
            float yaw = -hipYaw * 1.4f + atkTwist * atk * side;
            float roll = 4f * gait * sinP * (1f - run) + lurch - hipRoll * 0.6f;
            _spine.localRotation = Quaternion.Euler(pitch, yaw, roll);

            // Head: looks at the nearest player through the slouch, nods with the steps, lolls to one side.
            float nod = 4f * gait * Mathf.Sin(2f * p);
            float loll = _tilt + Mathf.Sin(now * 1.3f + _noise) * 4f + (stunned ? Mathf.Sin(now * 7f) * 20f : 0f);
            _head.localRotation = Quaternion.Euler(-(pitch + run * 8f) * 0.6f, 0f, 0f)
                * Quaternion.Euler(_lookPitch + nod - _flinch * 25f + heave * 20f, _lookYaw - hipYaw - yaw, loll);
            PoseJaw(now, atk, heave);

            // Arms: hanging and swinging against the legs, pumping on a runner, reaching for the player when close
            // (some always reach out in front), clawing overhead while climbing, and the strike on top.
            float reachL, reachR;
            if (stunned) reachL = reachR = 0f;
            else if (run > 0f || _data.kind == ZombieKind.Runner) reachL = reachR = _near * _near;
            else if (_reachStyle == 0) reachL = reachR = Mathf.Max(0.65f, _near);
            else if (_reachStyle == 1)
            {
                reachR = Mathf.Max(0.7f, _near);
                reachL = _near * 0.6f;
            }
            else reachL = reachR = _near;
            float atkL = bothHands || !_swingRight ? atk : atk * 0.25f;
            float atkR = bothHands || _swingRight ? atk : atk * 0.25f;
            PoseArm(_armL, _elbowL, -1, p, now, gait, run, reachL, atkL, atkPitch, atkElbow);
            PoseArm(_armR, _elbowR, 1, p + Mathf.PI, now, gait, run, reachR, atkR, atkPitch, atkElbow);
        }

        /// <summary>On all fours: alternate arms claw forward and haul the body along while the legs drag behind.</summary>
        void AnimateCrawl(float now, float gait, float atk, float atkLean, float heave)
        {
            // Pose space here: the body lies along +Y (forward) with its front facing the ground (+Z).
            float p = _walkPhase;
            float sinP = Mathf.Sin(p), cosP = Mathf.Cos(p);
            _hips.localPosition = new Vector3(0f, HipHeight + 0.05f * gait * Mathf.Sin(2f * p), 0f);
            _hips.localRotation = Quaternion.Euler(0f, 7f * gait * sinP, 0f);
            PoseDragLeg(_legL, _kneeL, _footL, p, -1, gait);
            PoseDragLeg(_legR, _kneeR, _footR, p + Mathf.PI, 1, gait);

            // A lunge rears the chest up off the ground, then slams it down.
            float pitch = atkLean * atk * 1.2f - 8f * heave - _flinch * 15f;
            _spine.localRotation = Quaternion.Euler(pitch, -9f * gait * sinP, 6f * gait * cosP + Mathf.Sin(now * 0.8f + _noise) * 3f);
            // Head craned back to face forward rather than at the floor.
            _head.localRotation = Quaternion.Euler(-70f, 0f, 0f)
                * Quaternion.Euler(_lookPitch * 0.6f + 6f * gait * Mathf.Sin(2f * p) - _flinch * 20f, _lookYaw * 0.7f, _tilt * 0.5f);
            PoseJaw(now, atk, heave);

            PoseCrawlArm(_armL, _elbowL, -1, p, gait, atk);
            PoseCrawlArm(_armR, _elbowR, 1, p + Mathf.PI, gait, atk);
        }

        void PoseLeg(Transform hip, Transform knee, Transform foot, float phase, int side, float gait, float hipAmp, float kneeAmp, float limp)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            // Negative pitch swings the leg forward; the knee bends most mid-swing (c > 0) and a little in stance.
            float h = -hipAmp * gait * s * (1f - 0.4f * limp);
            float k = 4f + (kneeAmp * Mathf.Max(0f, c) + 8f * Mathf.Max(0f, -c)) * gait * (1f - 0.7f * limp);
            hip.localRotation = Quaternion.Euler(h, 0f, 2.5f * side);
            knee.localRotation = Quaternion.Euler(k, 0f, 0f);
            float scuff = limp * gait * 14f * Mathf.Max(0f, c);
            foot.localRotation = Quaternion.Euler(-(h + k) * 0.85f + scuff, 0f, 0f);
        }

        void PoseDragLeg(Transform hip, Transform knee, Transform foot, float phase, int side, float gait)
        {
            float s = Mathf.Sin(phase);
            hip.localRotation = Quaternion.Euler(-12f + 6f * gait * s, 0f, 6f * side);
            knee.localRotation = Quaternion.Euler(18f + 20f * gait * Mathf.Max(0f, s), 0f, 0f);
            foot.localRotation = Quaternion.Euler(10f, 0f, 0f);
        }

        void PoseArm(Transform shoulder, Transform elbow, int side, float phase, float now, float gait, float run,
            float reach, float atk, float atkPitch, float atkElbow)
        {
            float s = Mathf.Sin(phase);
            // Hanging (negative pitch raises the arm forward) or, on a runner, pumping with the elbow bent.
            float pitch = Mathf.Lerp(6f + 16f * gait * s, -12f + 48f * gait * s, run);
            float elbowAngle = Mathf.Lerp(-12f - 8f * gait, -88f + 10f * s, run);
            float roll = Mathf.Lerp(6f, 10f, run);
            // Reach: up toward the player's chest, the fingers clawing.
            float reachPitch = -78f + 7f * Mathf.Sin(now * 1.7f + side * 1.3f + _noise) - _lookPitch * 0.5f + 5f * gait * s;
            pitch = Mathf.Lerp(pitch, reachPitch, reach);
            elbowAngle = Mathf.Lerp(elbowAngle, -18f + 9f * Mathf.Sin(now * 3.1f + side + _noise), reach);
            float yaw = 9f * reach;
            roll = Mathf.Lerp(roll, 3f, reach);
            if (_climb > 0f)
            {
                pitch = Mathf.Lerp(pitch, -160f + 25f * Mathf.Sin(now * 9f + side * 1.57f), _climb);
                elbowAngle = Mathf.Lerp(elbowAngle, -35f, _climb);
            }
            if (atk > 0f)
            {
                pitch = Mathf.Lerp(pitch, atkPitch, atk);
                elbowAngle = Mathf.Lerp(elbowAngle, atkElbow, atk);
                yaw = Mathf.Lerp(yaw, 14f, atk);
            }
            pitch -= _flinch * 25f;
            // Yaw turns the arm in across the body; roll swings it out to the side.
            shoulder.localRotation = Quaternion.Euler(pitch, -side * yaw, side * roll);
            elbow.localRotation = Quaternion.Euler(elbowAngle, 0f, 0f);
        }

        void PoseCrawlArm(Transform shoulder, Transform elbow, int side, float phase, float gait, float atk)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            // -100 is a hand planted under the shoulder, -165 reaching out ahead; the elbow lifts on the way forward.
            float pitch = -132f - 33f * s * Mathf.Max(gait, 0.15f);
            float elbowAngle = -10f - 50f * gait * Mathf.Max(0f, c);
            pitch = Mathf.Lerp(pitch, -172f, atk) - _flinch * 15f;
            elbowAngle = Mathf.Lerp(elbowAngle, -5f, atk);
            shoulder.localRotation = Quaternion.Euler(pitch, 0f, side * 12f);
            elbow.localRotation = Quaternion.Euler(elbowAngle, 0f, 0f);
        }

        void PoseJaw(float now, float atk, float heave)
        {
            // Idle working of the jaw, chomping when a player is close, wide open to bite or spit.
            float open = 5f + 4f * Mathf.Sin(now * 2.3f + _noise)
                + _near * 14f * Mathf.Max(0f, Mathf.Sin(now * 6f + _noise))
                + atk * 22f + heave * 40f;
            _jaw.localRotation = Quaternion.Euler(open, 0f, 0f);
        }

        /// <summary>
        /// Host: turns the body toward where it's walking at a limited, speed-dependent rate (slow shuffling turns,
        /// quicker when moving), and toward the player once it's close and slowing down.
        /// </summary>
        void SteerFacing(float dt, Vector3 toTarget, float distance)
        {
            Vector3 v = _agent.velocity;
            v.y = 0f;
            float speed = v.magnitude;
            Vector3 dir = speed > 0.25f ? v / speed : distance > 0.01f ? toTarget / distance : Vector3.zero;
            if (distance < 2.5f && distance > 0.01f) dir = Vector3.Slerp(dir.sqrMagnitude > 0f ? dir : toTarget / distance, toTarget / distance, 0.7f);
            if (dir.sqrMagnitude < 0.01f) return;
            float rate = Mathf.Lerp(150f, _data.kind == ZombieKind.Runner ? 420f : 300f, Mathf.Clamp01(speed / 3f));
            var want = Quaternion.LookRotation(dir, Vector3.up);
            // Ease into the turn: fast while far off the heading, slowing as it lines up.
            float angle = Quaternion.Angle(transform.rotation, want);
            float step = Mathf.Min(rate * dt, angle * (1f - Mathf.Exp(-6f * dt)) + 20f * dt);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, step);
        }

        /// <summary>Tracks the nearest living player (rescanned a few times a second) for the head and arms.</summary>
        void UpdateLook(float dt, float now)
        {
            if (now >= _nextLookScan)
            {
                _nextLookScan = now + 0.4f;
                _lookAt = null;
                float best = float.MaxValue;
                var players = NetPlayer.All;
                for (int i = 0; i < players.Count; i++)
                {
                    var pl = players[i];
                    if (pl == null || !pl.IsAlive) continue;
                    float sqr = (pl.transform.position - transform.position).sqrMagnitude;
                    if (sqr < best)
                    {
                        best = sqr;
                        _lookAt = pl;
                    }
                }
            }

            float yaw = 0f, pitch = 0f, near = 0f;
            if (_lookAt != null && _lookAt.IsSpawned && _lookAt.IsAlive)
            {
                Vector3 local = transform.InverseTransformPoint(_lookAt.transform.position + Vector3.up * 1.5f);
                local.y -= (_data.crawls ? 0.6f : 1.6f) * _data.bodyScale.y;
                float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
                pitch = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(0.1f, flat)) * Mathf.Rad2Deg, -35f, 35f);
                near = 1f - Mathf.Clamp01((flat - 2.5f) / 6f);
            }
            float k = 1f - Mathf.Exp(-6f * dt);
            _lookYaw = Mathf.Lerp(_lookYaw, yaw, k);
            _lookPitch = Mathf.Lerp(_lookPitch, pitch, k);
            _near = Mathf.Lerp(_near, near, 1f - Mathf.Exp(-3f * dt));
        }

        /// <summary>Eases every joint toward a slack corpse pose while the body falls.</summary>
        void RelaxCorpse(float dt)
        {
            float k = 1f - Mathf.Exp(-7f * dt);
            _armL.localRotation = Quaternion.Slerp(_armL.localRotation, DeadArmL, k);
            _armR.localRotation = Quaternion.Slerp(_armR.localRotation, DeadArmR, k);
            _elbowL.localRotation = Quaternion.Slerp(_elbowL.localRotation, DeadElbow, k);
            _elbowR.localRotation = Quaternion.Slerp(_elbowR.localRotation, DeadElbow, k);
            _legL.localRotation = Quaternion.Slerp(_legL.localRotation, DeadLegL, k);
            _legR.localRotation = Quaternion.Slerp(_legR.localRotation, DeadLegR, k);
            _kneeL.localRotation = Quaternion.Slerp(_kneeL.localRotation, DeadKnee, k);
            _kneeR.localRotation = Quaternion.Slerp(_kneeR.localRotation, DeadKnee, k);
            _jaw.localRotation = Quaternion.Slerp(_jaw.localRotation, DeadJaw, k);
            _spine.localRotation = Quaternion.Slerp(_spine.localRotation, Quaternion.identity, k);
            _hips.localRotation = Quaternion.Slerp(_hips.localRotation, Quaternion.identity, k);
            _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(10f, 0f, _tilt >= 0f ? 30f : -30f), k);
        }
    }
}
