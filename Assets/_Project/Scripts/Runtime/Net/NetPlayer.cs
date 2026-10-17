using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Player;
using ZombiesFPS.UI;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    public enum LifeState : byte
    {
        Alive,
        Downed,
        Dead,
    }

    /// <summary>
    /// One connected player. The owner moves itself (owner-authority NetworkTransform) and predicts its
    /// own shots; the host owns everything that costs health or money: damage, downs, revives, bleed-out,
    /// points and kills. Remote players see a greybox body with a gun.
    /// </summary>
    public class NetPlayer : NetworkBehaviour
    {
        public const float MaxHealth = 100f;
        public const float RegenDelay = 4f;
        public const float RegenPerSecond = 30f;
        public const float BleedOutTime = 30f;
        public const float ReviveTime = 3f;
        public const float ReviveRange = 2.2f;
        const float EyeHeight = 1.62f;

        static readonly List<NetPlayer> s_All = new List<NetPlayer>();
        public static IReadOnlyList<NetPlayer> All => s_All;
        public static NetPlayer Local { get; private set; }
        /// <summary>The name typed in the menu; the owner publishes it on spawn.</summary>
        public static string LocalName = "Survivor";

        static readonly Color[] SuitColors =
        {
            new Color(0.25f, 0.35f, 0.55f),
            new Color(0.55f, 0.3f, 0.22f),
            new Color(0.3f, 0.5f, 0.3f),
            new Color(0.5f, 0.45f, 0.2f),
        };

        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> Health = new NetworkVariable<float>(MaxHealth);
        public NetworkVariable<LifeState> State = new NetworkVariable<LifeState>(LifeState.Alive);
        public NetworkVariable<float> BleedOutRemaining = new NetworkVariable<float>(0f);
        public NetworkVariable<float> ReviveProgress = new NetworkVariable<float>(0f);
        public NetworkVariable<long> Points = new NetworkVariable<long>(0);
        public NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public NetworkVariable<int> Headshots = new NetworkVariable<int>(0);

        // Owner-only rig.
        PlayerMotor _motor;
        PlayerLook _look;
        HitscanGun _gun;
        Transform _pivot;
        AudioSource _uiAudio;
        bool _sentReviveHeld;

        // Remote body.
        Transform _body;
        Transform _remoteMuzzle;
        AudioSource _remoteAudio;

        // Server-only.
        float _lastDamageTime = -100f;
        bool _reviveHeld;
        int _lastRevivedFrame = -10;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        public string DisplayName => PlayerName.Value.IsEmpty ? "Player " + (OwnerClientId + 1) : PlayerName.Value.ToString();
        public bool IsAlive => State.Value == LifeState.Alive;
        public HitscanGun Gun => _gun;
        public PlayerLook Look => _look;
        /// <summary>Owner-side time of the last health drop, for the damage vignette.</summary>
        public float LocalLastDamageTime { get; private set; } = -100f;
        public Color SuitColor => SuitColors[(int)(OwnerClientId % (ulong)SuitColors.Length)];

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
            State.OnValueChanged += OnStateChanged;
            if (IsOwner)
            {
                Local = this;
                PlayerName.Value = new FixedString32Bytes(Truncate(LocalName, 20));
                Health.OnValueChanged += OnOwnerHealthChanged;
                BuildLocalRig();
            }
            else
            {
                BuildRemoteBody();
            }
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
            State.OnValueChanged -= OnStateChanged;
            Health.OnValueChanged -= OnOwnerHealthChanged;
            if (Local == this) Local = null;
        }

        /// <summary>Forget every player (used when leaving a session, in case despawn callbacks didn't run).</summary>
        public static void ClearRegistry()
        {
            s_All.Clear();
            Local = null;
        }

        static string Truncate(string s, int max) => string.IsNullOrEmpty(s) ? "Survivor" : s.Length <= max ? s : s.Substring(0, max);

        // ---------------------------------------------------------------- building

        void BuildLocalRig()
        {
            var controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;

            _motor = gameObject.AddComponent<PlayerMotor>();
            _look = gameObject.AddComponent<PlayerLook>();
            _gun = gameObject.AddComponent<HitscanGun>();

            _pivot = Greybox.Empty("Camera Pivot", transform, new Vector3(0f, EyeHeight, 0f));
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(_pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();

            var gunAudio = camGo.AddComponent<AudioSource>();
            gunAudio.playOnAwake = false;
            _uiAudio = camGo.AddComponent<AudioSource>();
            _uiAudio.playOnAwake = false;

            var view = Greybox.Empty("Weapon", camGo.transform, Vector3.zero).gameObject.AddComponent<WeaponView>();
            _look.Init(_pivot, camGo.transform);
            _gun.Init(GameConfig.Gun, cam, _look, _motor, () => IsAlive, gunAudio, _uiAudio, view);
            view.Init(_gun, _motor, _look);
            _gun.Fired += (origin, dir) => ShootRpc(origin, dir);

            new GameObject("HUD").AddComponent<PrototypeHud>().Init(this);
        }

        void BuildRemoteBody()
        {
            var suit = SuitColor;
            var skin = new Color(0.78f, 0.62f, 0.5f);
            _body = Greybox.Empty("Body", transform, Vector3.zero);
            Greybox.Prim(PrimitiveType.Capsule, "Torso", _body, new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 0.5f, 0.38f), suit, collider: false);
            Greybox.Prim(PrimitiveType.Capsule, "LegL", _body, new Vector3(-0.13f, 0.42f, 0f), new Vector3(0.2f, 0.42f, 0.2f), suit * 0.7f, collider: false);
            Greybox.Prim(PrimitiveType.Capsule, "LegR", _body, new Vector3(0.13f, 0.42f, 0f), new Vector3(0.2f, 0.42f, 0.2f), suit * 0.7f, collider: false);
            Greybox.Prim(PrimitiveType.Sphere, "Head", _body, new Vector3(0f, 1.68f, 0f), Vector3.one * 0.32f, skin, collider: false);
            var gun = Greybox.Empty("Gun", _body, new Vector3(0.22f, 1.3f, 0.35f));
            Greybox.Box("Receiver", gun, Vector3.zero, new Vector3(0.08f, 0.1f, 0.6f), new Color(0.16f, 0.17f, 0.18f), collider: false);
            _remoteMuzzle = Greybox.Empty("Muzzle", gun, new Vector3(0f, 0f, 0.32f));

            _remoteAudio = gameObject.AddComponent<AudioSource>();
            _remoteAudio.spatialBlend = 1f;
            _remoteAudio.minDistance = 3f;
            _remoteAudio.maxDistance = 60f;
            _remoteAudio.playOnAwake = false;
            ApplyRemotePose(State.Value);
        }

        void ApplyRemotePose(LifeState state)
        {
            if (_body == null) return;
            _body.gameObject.SetActive(state != LifeState.Dead);
            _body.localRotation = state == LifeState.Downed ? Quaternion.Euler(-70f, 0f, 0f) : Quaternion.identity;
            _body.localPosition = state == LifeState.Downed ? new Vector3(0f, 0.25f, -0.6f) : Vector3.zero;
        }

        // ---------------------------------------------------------------- state changes

        void OnStateChanged(LifeState previous, LifeState current)
        {
            ApplyRemotePose(current);
            if (IsOwner && _motor != null)
            {
                _motor.Downed = current == LifeState.Downed;
                _motor.InputLocked = current == LifeState.Dead;
            }
        }

        void OnOwnerHealthChanged(float previous, float current)
        {
            if (current >= previous) return;
            LocalLastDamageTime = Time.time;
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.PlayerHurt, 0.9f);
            if (_look != null) _look.AddShake(0.7f);
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (IsServer) ServerTick(Time.deltaTime);
            if (IsOwner) OwnerTick();
        }

        void OwnerTick()
        {
            if (_pivot == null) return;
            float targetHeight = State.Value == LifeState.Alive ? EyeHeight : State.Value == LifeState.Downed ? 0.55f : 0.3f;
            var p = _pivot.localPosition;
            p.y = Mathf.Lerp(p.y, targetHeight, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _pivot.localPosition = p;

            bool reviveHeld = IsAlive && GameInput.InteractHeld && _look.CursorLocked;
            if (reviveHeld != _sentReviveHeld)
            {
                _sentReviveHeld = reviveHeld;
                SetReviveHeldRpc(reviveHeld);
            }
        }

        void ServerTick(float dt)
        {
            var director = GameDirector.Instance;
            if (director != null && director.GameOver.Value) return;

            switch (State.Value)
            {
                case LifeState.Alive:
                    if (Health.Value < MaxHealth && Time.time - _lastDamageTime >= RegenDelay)
                        Health.Value = Mathf.Min(MaxHealth, Health.Value + RegenPerSecond * dt);
                    if (_reviveHeld)
                    {
                        var target = FindReviveTarget(transform.position);
                        if (target != null) target.ServerReviveTick(dt);
                    }
                    break;

                case LifeState.Downed:
                    bool beingRevived = Time.frameCount - _lastRevivedFrame <= 1;
                    if (!beingRevived)
                    {
                        if (ReviveProgress.Value != 0f) ReviveProgress.Value = 0f;
                        BleedOutRemaining.Value = Mathf.Max(0f, BleedOutRemaining.Value - dt);
                        if (BleedOutRemaining.Value <= 0f) State.Value = LifeState.Dead;
                    }
                    break;
            }
        }

        /// <summary>Nearest downed teammate within revive range of a position, or null.</summary>
        public NetPlayer FindReviveTarget(Vector3 from)
        {
            NetPlayer best = null;
            float bestSqr = ReviveRange * ReviveRange;
            foreach (var p in s_All)
            {
                if (p == this || p.State.Value != LifeState.Downed) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    best = p;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        void ServerReviveTick(float dt)
        {
            if (_lastRevivedFrame == Time.frameCount) return; // Two revivers don't stack.
            _lastRevivedFrame = Time.frameCount;
            ReviveProgress.Value = Mathf.Min(1f, ReviveProgress.Value + dt / ReviveTime);
            if (ReviveProgress.Value >= 1f)
            {
                State.Value = LifeState.Alive;
                Health.Value = MaxHealth * 0.5f;
                ReviveProgress.Value = 0f;
                _lastDamageTime = Time.time;
            }
        }

        // ---------------------------------------------------------------- server API

        /// <summary>Host only. Zombie hits land here.</summary>
        public void ServerApplyDamage(float amount, Vector3 from)
        {
            if (!IsServer || State.Value != LifeState.Alive || amount <= 0f) return;
            _lastDamageTime = Time.time;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            if (Health.Value <= 0f)
            {
                State.Value = LifeState.Downed;
                BleedOutRemaining.Value = BleedOutTime;
                ReviveProgress.Value = 0f;
            }
        }

        /// <summary>Host only. Fresh start after a game over.</summary>
        public void ServerResetForRestart(Vector3 spawn)
        {
            State.Value = LifeState.Alive;
            Health.Value = MaxHealth;
            BleedOutRemaining.Value = 0f;
            ReviveProgress.Value = 0f;
            Points.Value = 0;
            Kills.Value = 0;
            Headshots.Value = 0;
            RespawnRpc(spawn);
        }

        // ---------------------------------------------------------------- RPCs

        [Rpc(SendTo.Server)]
        void SetReviveHeldRpc(bool held)
        {
            _reviveHeld = held;
        }

        /// <summary>Owner fired: the host resolves the hit against its own copy of the world.</summary>
        [Rpc(SendTo.Server)]
        void ShootRpc(Vector3 origin, Vector3 direction)
        {
            if (State.Value != LifeState.Alive) return;
            var gun = GameConfig.Gun;
            // Co-op trust model: accept the client's aim, but keep the origin near the player.
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if ((origin - eye).sqrMagnitude > 2f * 2f) origin = eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            Vector3 end = origin + direction * gun.range;
            Vector3 normal = -direction;
            bool flesh = false;
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, gun.range, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                // Players never block shots (no friendly fire, no blocking your teammates' bullets).
                if (_hits[i].collider.GetComponentInParent<NetPlayer>() != null) continue;
                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex >= 0)
            {
                var hit = _hits[nearestIndex];
                end = hit.point;
                normal = hit.normal;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.Owner is NetZombie zombie && zombie.IsAlive)
                {
                    flesh = true;
                    float mult = hitbox.isHead ? gun.headshotMultiplier : hitbox.multiplier;
                    long damage = System.Math.Max(1L, (long)System.Math.Round(gun.damage * (double)mult));
                    var result = zombie.TakeDamage(new DamageInfo(damage, hit.point, direction, hitbox.isHead, OwnerClientId));
                    AwardPoints(result, hitbox.isHead);
                    HitConfirmRpc(result.Killed, hitbox.isHead);
                }
            }
            ShotFxRpc(end, normal, flesh);
        }

        void AwardPoints(DamageResult result, bool headshot)
        {
            var data = GameConfig.Zombie;
            if (!result.Killed)
            {
                Points.Value += data.pointsPerHit;
                return;
            }
            Points.Value += headshot ? data.pointsPerHeadshotKill : data.pointsPerKill;
            Kills.Value++;
            if (headshot) Headshots.Value++;
        }

        [Rpc(SendTo.Owner)]
        void HitConfirmRpc(bool killed, bool headshot)
        {
            if (_gun != null) _gun.ConfirmHit(killed, headshot);
        }

        /// <summary>Everyone but the shooter sees and hears the shot.</summary>
        [Rpc(SendTo.NotOwner)]
        void ShotFxRpc(Vector3 end, Vector3 normal, bool flesh)
        {
            if (_remoteMuzzle == null) return;
            FxSystem.Instance?.ShowTracer(_remoteMuzzle.position, end);
            FxSystem.Instance?.Impact(end, normal, flesh ? FxSystem.Blood : FxSystem.Dust, flesh ? 5 : 3, 3f, 0.06f);
            _remoteAudio.pitch = Random.Range(0.94f, 1.06f);
            _remoteAudio.PlayOneShot(ProceduralAudio.Gunshot, 0.8f);
        }

        [Rpc(SendTo.Owner)]
        void RespawnRpc(Vector3 position)
        {
            if (_motor != null) _motor.Teleport(position);
            if (_gun != null) _gun.ResetAmmo();
        }
    }
}
