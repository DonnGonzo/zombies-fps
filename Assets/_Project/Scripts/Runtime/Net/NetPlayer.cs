using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Player;
using ZombiesFPS.UI;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
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
    /// One connected player. The owner moves itself (owner-authority NetworkTransform), predicts its own
    /// shots and keeps its own ammo; the host owns everything that costs health or money: damage, downs,
    /// revives, bleed-out, points, kills, purchases and which guns are in the two weapon slots.
    /// Remote players see a greybox body with a gun.
    /// </summary>
    public partial class NetPlayer : NetworkBehaviour
    {
        public const float MaxHealth = 100f;
        public const float RegenDelay = 4f;
        public const float RegenPerSecond = 30f;
        public const float BleedOutTime = 30f;
        public const float ReviveTime = 3f;
        public const float ReviveRange = 2.2f;
        public const int SlotCount = 2;
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
        /// <summary>Gun ids (GameConfig.Guns) in the two weapon slots; -1 is empty. Only the host changes them.</summary>
        public NetworkVariable<int> Gun0 = new NetworkVariable<int>(0);
        public NetworkVariable<int> Gun1 = new NetworkVariable<int>(-1);
        /// <summary>Which slot is in the player's hands. The owner switches; the host reads it for damage.</summary>
        public NetworkVariable<int> ActiveSlot = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Owner-only rig.
        PlayerMotor _motor;
        PlayerLook _look;
        HitscanGun _gun;
        Transform _pivot;
        AudioSource _uiAudio;
        bool _sentReviveHeld;
        readonly int[] _slotMagazine = new int[SlotCount];
        readonly int[] _slotReserve = new int[SlotCount];

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
        /// <summary>Owner only: the door or wall gun the player is standing at, if any.</summary>
        public Buyable FocusedBuyable { get; private set; }

        public int GunInSlot(int slot) => slot == 0 ? Gun0.Value : slot == 1 ? Gun1.Value : -1;
        NetworkVariable<int> SlotVariable(int slot) => slot == 0 ? Gun0 : Gun1;

        public int SlotOf(int gunId)
        {
            if (gunId < 0) return -1;
            for (int i = 0; i < SlotCount; i++)
                if (GunInSlot(i) == gunId) return i;
            return -1;
        }

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
            State.OnValueChanged += OnStateChanged;
            if (IsServer) Points.Value = GameConfig.Round.startingPoints;
            if (IsOwner)
            {
                Local = this;
                PlayerName.Value = new FixedString32Bytes(Truncate(LocalName, 20));
                Health.OnValueChanged += OnOwnerHealthChanged;
                Gun0.OnValueChanged += OnGun0Changed;
                Gun1.OnValueChanged += OnGun1Changed;
                SubscribeProgression();
                BuildLocalRig();
                ApplyOwnerBonuses();
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
            Gun0.OnValueChanged -= OnGun0Changed;
            Gun1.OnValueChanged -= OnGun1Changed;
            UnsubscribeProgression();
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
            ResetLoadoutAmmo();

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

            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.GodMode.Value && _gun.Reserve < _gun.MaxReserve) _gun.RefillAmmo();

            bool canAct = IsAlive && _look.CursorLocked && !(rounds != null && rounds.IsGameOver);
            bool picking = HasPowerUpOffer && _look.CursorLocked && !(rounds != null && rounds.IsGameOver);
            if (picking)
            {
                int choice = GameInput.ChoicePressed;
                if (choice >= 0) PickPowerUpRpc(choice);
                else if (GameInput.RerollPressed && PowerRerolls.Value > 0) RerollPowerUpsRpc();
            }
            if (canAct)
            {
                int want = picking ? -1 : GameInput.SlotPressed;
                if (GameInput.SwitchWeaponPressed) want = 1 - ActiveSlot.Value;
                if (want >= 0 && want != ActiveSlot.Value && GunInSlot(want) >= 0) EquipSlot(want, saveCurrent: true);
            }

            bool nearDowned = FindReviveTarget(transform.position) != null;
            FocusedBuyable = canAct && !nearDowned ? FindBuyable() : null;
            if (FocusedBuyable != null && GameInput.InteractPressed) BuyRpc(System.Array.IndexOf(GameConfig.Map.Buyables, FocusedBuyable));

            bool reviveHeld = IsAlive && GameInput.InteractHeld && _look.CursorLocked;
            if (reviveHeld != _sentReviveHeld)
            {
                _sentReviveHeld = reviveHeld;
                SetReviveHeldRpc(reviveHeld);
            }
        }

        void ServerTick(float dt)
        {
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return;

            switch (State.Value)
            {
                case LifeState.Alive:
                    float max = MaxHealthNow;
                    if (Health.Value < max && Time.time - _lastDamageTime >= RegenDelay)
                        Health.Value = Mathf.Min(max, Health.Value + RegenPerSecond * dt);
                    if (_reviveHeld)
                    {
                        var target = FindReviveTarget(transform.position);
                        if (target != null) target.ServerReviveTick(dt * (1f + Bonus(PowerStat.ReviveSpeed)));
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

        // ---------------------------------------------------------------- weapons (owner)

        /// <summary>Puts a slot's gun in the player's hands, first saving the ammo of the gun being put away.</summary>
        void EquipSlot(int slot, bool saveCurrent)
        {
            if (_gun == null) return;
            if (saveCurrent)
            {
                _slotMagazine[ActiveSlot.Value] = _gun.Magazine;
                _slotReserve[ActiveSlot.Value] = _gun.Reserve;
            }
            var data = GameConfig.GunById(GunInSlot(slot));
            if (data == null) return;
            ActiveSlot.Value = slot;
            var tier = GameConfig.Upgrades.Tier(TierInSlot(slot));
            _gun.ExplosiveRounds = tier != null && tier.explosive;
            _gun.ReloadSpeed = 1f + Bonus(PowerStat.ReloadSpeed);
            _gun.Equip(data, _slotMagazine[slot], _slotReserve[slot], CapacityScale(slot), GunName(slot));
        }

        void OnGun0Changed(int previous, int current) => OnSlotGunChanged(0, current);
        void OnGun1Changed(int previous, int current) => OnSlotGunChanged(1, current);

        /// <summary>The host put a new gun in a slot (a wall buy or a restart): it arrives full and in hand.</summary>
        void OnSlotGunChanged(int slot, int gunId)
        {
            var data = GameConfig.GunById(gunId);
            if (data == null)
            {
                // Slot emptied: if it was in hand, fall back to the other one.
                if (ActiveSlot.Value == slot && GunInSlot(1 - slot) >= 0) EquipSlot(1 - slot, saveCurrent: false);
                return;
            }
            FillSlot(slot);
            EquipSlot(slot, saveCurrent: slot != ActiveSlot.Value);
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.MagIn, 0.9f);
        }

        /// <summary>Every owned gun back to a full magazine and its starting reserve (respawn and restart).</summary>
        void ResetLoadoutAmmo()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var data = GameConfig.GunById(GunInSlot(i));
                float scale = CapacityScale(i);
                _slotMagazine[i] = data != null ? HitscanGun.ScaledMagazine(data, scale) : 0;
                _slotReserve[i] = data != null ? HitscanGun.ScaledReserve(data.startingReserve, scale) : 0;
            }
            int slot = GunInSlot(ActiveSlot.Value) >= 0 ? ActiveSlot.Value : 0;
            EquipSlot(slot, saveCurrent: false);
        }

        // ---------------------------------------------------------------- buying

        /// <summary>The closest door or wall gun in reach, skipping doors that are already open.</summary>
        Buyable FindBuyable()
        {
            var map = GameConfig.Map;
            if (map == null) return null;
            Buyable best = null;
            float bestSqr = float.MaxValue;
            Vector3 here = transform.position;
            foreach (var b in map.Buyables)
            {
                if (b.Kind == BuyableKind.Door && MapState.Instance != null && MapState.Instance.IsDoorOpen(b.Index)) continue;
                Vector3 d = b.Position - here;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= b.Range * b.Range && sqr < bestSqr)
                {
                    best = b;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        /// <summary>What a buyable would cost this player right now, and what they'd get. -1 if there is nothing to buy.</summary>
        public long PriceFor(Buyable b, out string what)
        {
            what = null;
            if (b == null) return -1;
            var map = GameConfig.Map;
            if (b.Kind == BuyableKind.Door)
            {
                if (MapState.Instance == null || MapState.Instance.IsDoorOpen(b.Index)) return -1;
                what = "open the door to " + ZoneMap.ZoneNames[map.OtherSide(b.Index, transform.position)];
                return GameConfig.Prices.DoorCost(b.Index);
            }
            if (b.Kind == BuyableKind.UpgradeMachine) return MachinePrice(out what);
            var entry = GameConfig.Prices.WallGunAt(b.Index);
            if (entry == null || entry.gun == null) return -1;
            bool owned = SlotOf(GameConfig.GunId(entry.gun)) >= 0;
            what = owned ? $"refill {entry.gun.displayName} ammo" : $"buy the {entry.gun.displayName}";
            return owned ? entry.ammoPrice : entry.price;
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
                Health.Value = MaxHealthNow * 0.5f;
                ReviveProgress.Value = 0f;
                _lastDamageTime = Time.time;
            }
        }

        // ---------------------------------------------------------------- server API

        /// <summary>Host only. Zombie hits land here.</summary>
        public void ServerApplyDamage(float amount, Vector3 from)
        {
            if (!IsServer || State.Value != LifeState.Alive || amount <= 0f) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.GodMode.Value) return;
            _lastDamageTime = Time.time;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            if (Health.Value <= 0f)
            {
                if (MissionBoard.Instance != null) MissionBoard.Instance.ServerOnPlayerDowned();
                State.Value = LifeState.Downed;
                BleedOutRemaining.Value = BleedOutTime;
                ReviveProgress.Value = 0f;
            }
        }

        /// <summary>Host only. Slows the player for a while (crawlers hit the legs).</summary>
        public void ServerSlow(float scale, float duration)
        {
            if (!IsServer || State.Value != LifeState.Alive) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.GodMode.Value) return;
            SlowRpc(scale, duration);
        }

        [Rpc(SendTo.Owner)]
        void SlowRpc(float scale, float duration)
        {
            if (_motor != null) _motor.Slow(scale, duration);
        }

        /// <summary>Host only. Back to just the starter gun (a fresh game).</summary>
        public void ServerResetLoadout()
        {
            Gun0.Value = 0;
            Gun1.Value = -1;
            Tier0.Value = 0;
            Tier1.Value = 0;
        }

        /// <summary>Host only. Brings a bled-out player back at the end of a round, keeping their points.</summary>
        public void ServerRespawn(Vector3 spawn)
        {
            State.Value = LifeState.Alive;
            Health.Value = MaxHealthNow;
            BleedOutRemaining.Value = 0f;
            ReviveProgress.Value = 0f;
            _lastDamageTime = Time.time;
            RespawnRpc(spawn);
        }

        /// <summary>Host only. Fresh start after a game over.</summary>
        public void ServerResetForRestart(Vector3 spawn)
        {
            Points.Value = GameConfig.Round.startingPoints;
            Kills.Value = 0;
            Headshots.Value = 0;
            ServerResetLoadout();
            ServerResetProgression();
            ServerRespawn(spawn);
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
            var gun = GameConfig.GunById(GunInSlot(ActiveSlot.Value)) ?? GameConfig.Gun;
            // Co-op trust model: accept the client's aim, but keep the origin near the player.
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if ((origin - eye).sqrMagnitude > 2f * 2f) origin = eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            Vector3 end = origin + direction * gun.range;
            Vector3 normal = -direction;
            bool flesh = false;
            NetZombie primary = null;
            double baseDamage = gun.damage * (double)GunDamageMultiplier(ActiveSlot.Value);
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
                    primary = zombie;
                    float mult = hitbox.isHead ? gun.headshotMultiplier * (1f + Bonus(PowerStat.HeadshotDamage)) : hitbox.multiplier;
                    long damage = System.Math.Max(1L, (long)System.Math.Round(baseDamage * mult));
                    var result = zombie.TakeDamage(new DamageInfo(damage, hit.point, direction, hitbox.isHead, OwnerClientId));
                    AwardPoints(zombie, result, hitbox.isHead);
                    HitConfirmRpc(result.Killed, hitbox.isHead);
                }
            }

            var tier = GameConfig.Upgrades.Tier(TierInSlot(ActiveSlot.Value));
            if (tier != null && tier.chain && primary != null) ServerChain(primary, end, baseDamage, direction);
            if (tier != null && tier.explosive) ServerExplode(primary, end, baseDamage, direction);
            ShotFxRpc(end, normal, flesh, tier != null && tier.explosive);
        }

        void AwardPoints(NetZombie zombie, DamageResult result, bool headshot)
        {
            var data = zombie.Data;
            if (!result.Killed)
            {
                Points.Value += data.pointsPerHit;
                return;
            }
            long reward = headshot ? data.pointsPerHeadshotKill : data.pointsPerKill;
            Points.Value += (long)System.Math.Round(reward * (1.0 + Bonus(PowerStat.KillPoints)));
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
        void ShotFxRpc(Vector3 end, Vector3 normal, bool flesh, bool explosive)
        {
            if (_remoteMuzzle == null) return;
            FxSystem.Instance?.ShowTracer(_remoteMuzzle.position, end);
            FxSystem.Instance?.Impact(end, normal, flesh ? FxSystem.Blood : FxSystem.Dust, flesh ? 5 : 3, 3f, 0.06f);
            if (explosive) FxSystem.Instance?.Impact(end, normal, FxSystem.Blast, 16, 6f, 0.09f);
            _remoteAudio.pitch = Random.Range(0.94f, 1.06f);
            _remoteAudio.PlayOneShot(ProceduralAudio.Gunshot, 0.8f);
        }

        [Rpc(SendTo.Owner)]
        void RespawnRpc(Vector3 position)
        {
            if (_motor != null) _motor.Teleport(position);
            ResetLoadoutAmmo();
        }

        /// <summary>Owner asks to buy the map's buyable at an index; the host checks reach and points.</summary>
        [Rpc(SendTo.Server)]
        void BuyRpc(int buyableIndex)
        {
            var map = GameConfig.Map;
            var rounds = RoundManager.Instance;
            if (map == null || buyableIndex < 0 || buyableIndex >= map.Buyables.Length) return;
            if (State.Value != LifeState.Alive || (rounds != null && rounds.IsGameOver)) return;
            var b = map.Buyables[buyableIndex];
            Vector3 d = b.Position - transform.position;
            d.y = 0f;
            if (d.magnitude > b.Range + 1f) return;

            long price = PriceFor(b, out _);
            if (price < 0) return;
            if (Points.Value < price)
            {
                PurchaseResultRpc(false);
                return;
            }
            Points.Value -= price;

            if (b.Kind == BuyableKind.Door)
            {
                MapState.Instance.ServerOpenDoor(b.Index);
            }
            else if (b.Kind == BuyableKind.UpgradeMachine)
            {
                ServerUpgradeGunInHand();
            }
            else
            {
                int gunId = GameConfig.GunId(GameConfig.Prices.WallGunAt(b.Index).gun);
                int owned = SlotOf(gunId);
                if (owned >= 0) AmmoBoughtRpc(owned);
                else
                {
                    int slot = Gun1.Value < 0 ? 1 : Mathf.Clamp(ActiveSlot.Value, 0, SlotCount - 1);
                    TierVariable(slot).Value = 0;
                    SlotVariable(slot).Value = gunId;
                }
            }
            PurchaseResultRpc(true);
        }

        [Rpc(SendTo.Owner)]
        void PurchaseResultRpc(bool success)
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(success ? ProceduralAudio.Purchase : ProceduralAudio.Denied, 0.8f);
        }

        /// <summary>Host only. Every gun this player owns back to full (a mission reward).</summary>
        public void ServerRefillAllAmmo() => RefillAllRpc();

        [Rpc(SendTo.Owner)]
        void RefillAllRpc()
        {
            for (int slot = 0; slot < SlotCount; slot++) AmmoBoughtRpc(slot);
        }

        [Rpc(SendTo.Owner)]
        void AmmoBoughtRpc(int slot)
        {
            if (_gun == null || slot < 0 || slot >= SlotCount) return;
            if (slot == ActiveSlot.Value)
            {
                _gun.FillAmmo();
                return;
            }
            FillSlot(slot);
        }
    }
}
