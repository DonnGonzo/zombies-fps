using System;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Host-owned buildables. Nine parts are hidden around the map, three for each of the Scrap Cannon, the
    /// Scrap Shield and the Buddy Bot; every game the host picks one of three spots for each part, so they
    /// turn up somewhere different each time. Parts found by anyone count for the whole squad. With all three
    /// parts of something, any build station builds it, and from then on players take it from a station:
    /// the Cannon once per player per game, a Shield whenever theirs has broken (from the next round), and the Bot
    /// whenever it is back online. The Bot follows whoever switched it on and shoots zombies for two minutes,
    /// then breaks and stays offline for five rounds.
    /// </summary>
    public class BuildManager : NetworkBehaviour
    {
        public const int Cannon = 0, Shield = 1, Bot = 2;
        public const float ShieldMaxHealth = 1500f;
        /// <summary>Share of a hit from the front the shield takes instead of you.</summary>
        public const float ShieldBlockShare = 0.9f;
        public const float RobotLifetime = 120f;
        public const int RobotOfflineRounds = 5;
        const float RobotFireInterval = 0.3f;
        const float RobotRange = 28f;
        const float RobotSyncInterval = 0.1f;

        public static readonly string[] BuildableNames = { "Scrap Cannon", "Scrap Shield", "Buddy Bot" };
        public static readonly string[] PartNames =
        {
            "Cannon Barrel", "Cannon Coil", "Cannon Grip",
            "Shield Frame", "Shield Plate", "Shield Strap",
            "Bot Chassis", "Bot Rotor", "Bot Brain",
        };
        public static readonly Color[] BuildableColors =
        {
            new Color(1f, 0.55f, 0.15f), new Color(0.4f, 0.7f, 1f), new Color(0.45f, 1f, 0.5f),
        };

        public static BuildManager Instance { get; private set; }
        /// <summary>Every machine: a part was picked up (part, finder's client id, parts of that buildable found so far).</summary>
        public static event Action<int, ulong, int> PartFound;
        /// <summary>Every machine: something was built for the first time (buildable, builder's client id).</summary>
        public static event Action<int, ulong> ItemBuilt;
        /// <summary>Every machine: the Buddy Bot broke down (the round it's back online).</summary>
        public static event Action<int> RobotBroke;

        /// <summary>Which of its three spots each part is at, two bits per part.</summary>
        public NetworkVariable<int> PartLayout = new NetworkVariable<int>(0);
        /// <summary>A bit per part the squad has picked up.</summary>
        public NetworkVariable<int> Collected = new NetworkVariable<int>(0);
        /// <summary>A bit per buildable that has been built.</summary>
        public NetworkVariable<int> Built = new NetworkVariable<int>(0);
        /// <summary>Client id of the player the Buddy Bot is helping, or -1 while it's off.</summary>
        public NetworkVariable<long> RobotOwner = new NetworkVariable<long>(-1);
        public NetworkVariable<float> RobotTimeLeft = new NetworkVariable<float>(0f);
        /// <summary>The first round the Bot can be switched on again after breaking.</summary>
        public NetworkVariable<int> RobotReadyRound = new NetworkVariable<int>(0);
        public NetworkVariable<Vector3> RobotPosition = new NetworkVariable<Vector3>(Vector3.zero);

        GameObject[] _partProps;
        Transform _robot;
        Transform _robotRotor;
        Light _robotLight;
        AudioSource _robotAudio;
        Vector3 _robotShown;
        bool _robotWasActive;

        // Server.
        Vector3 _robotTarget;
        float _nextShot;
        float _nextSync;
        readonly Collider[] _near = new Collider[96];
        readonly RaycastHit[] _sight = new RaycastHit[8];

        // ---------------------------------------------------------------- queries (every machine)

        public static int ItemOf(int part) => part / ZoneMap.PartsPerBuildable;
        public int SpotOf(int part) => part * ZoneMap.SpotsPerPart + ((PartLayout.Value >> (part * 2)) & 3) % ZoneMap.SpotsPerPart;
        public bool IsCollected(int part) => (Collected.Value & (1 << part)) != 0;
        public bool IsBuilt(int item) => (Built.Value & (1 << item)) != 0;
        public bool RobotActive => RobotOwner.Value >= 0;

        public int PartsFound(int item)
        {
            int n = 0;
            for (int k = 0; k < ZoneMap.PartsPerBuildable; k++)
                if (IsCollected(item * ZoneMap.PartsPerBuildable + k)) n++;
            return n;
        }

        public bool HasAllParts(int item) => PartsFound(item) == ZoneMap.PartsPerBuildable;

        /// <summary>The part lying at a spot right now, or -1.</summary>
        public int PartWaitingAt(int spot)
        {
            int part = spot / ZoneMap.SpotsPerPart;
            return part >= 0 && part < ZoneMap.PartCount && !IsCollected(part) && SpotOf(part) == spot ? part : -1;
        }

        static int CurrentRound => RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;

        /// <summary>
        /// What using a station's place for a buildable would do for a player, for the prompt and the host's check.
        /// Free when it does something, -1 (with the reason in what) when it can't.
        /// </summary>
        public long BenchPrice(NetPlayer player, int item, out string what)
        {
            string name = BuildableNames[item];
            if (!IsBuilt(item))
            {
                int found = PartsFound(item);
                if (found < ZoneMap.PartsPerBuildable)
                {
                    what = $"{name}: {found}/{ZoneMap.PartsPerBuildable} parts found";
                    return -1;
                }
                what = "build the " + name;
                return 0;
            }
            int round = CurrentRound;
            switch (item)
            {
                case Cannon:
                    int id = GameConfig.GunId(ScrapCannon);
                    if (id < 0)
                    {
                        what = null;
                        return -1;
                    }
                    if (player.SlotOf(id) >= 0)
                    {
                        what = "you have the " + name;
                        return -1;
                    }
                    if (player.TookCannon.Value)
                    {
                        what = "one " + name + " each per game";
                        return -1;
                    }
                    what = "take the " + name;
                    return 0;
                case Shield:
                    if (player.HasShield)
                    {
                        what = "you have a " + name;
                        return -1;
                    }
                    if (round < player.ShieldReadyRound.Value)
                    {
                        what = "a new shield next round";
                        return -1;
                    }
                    what = "take a " + name;
                    return 0;
                default:
                    if (RobotActive)
                    {
                        what = "the " + name + " is out helping";
                        return -1;
                    }
                    if (round < RobotReadyRound.Value)
                    {
                        what = $"the {name} is offline until round {RobotReadyRound.Value}";
                        return -1;
                    }
                    what = "switch on the " + name;
                    return 0;
            }
        }

        /// <summary>The Scrap Cannon's gun: the second reward gun after the Ion Lance.</summary>
        public static Data.GunData ScrapCannon => GameConfig.Prices != null ? GameConfig.Prices.RareGun(1) : null;

        // ---------------------------------------------------------------- lifecycle

        public override void OnNetworkSpawn()
        {
            Instance = this;
            BuildProps();
            PartLayout.OnValueChanged += OnPartsChanged;
            Collected.OnValueChanged += OnPartsChanged;
            if (IsServer) Shuffle();
            RefreshParts();
        }

        public override void OnNetworkDespawn()
        {
            PartLayout.OnValueChanged -= OnPartsChanged;
            Collected.OnValueChanged -= OnPartsChanged;
            if (_partProps != null)
                foreach (var p in _partProps)
                    if (p != null) Destroy(p);
            if (_robot != null) Destroy(_robot.gameObject);
            if (Instance == this) Instance = null;
        }

        void OnPartsChanged(int previous, int current) => RefreshParts();

        void RefreshParts()
        {
            if (_partProps == null) return;
            for (int s = 0; s < _partProps.Length; s++) _partProps[s].SetActive(PartWaitingAt(s) >= 0);
        }

        void BuildProps()
        {
            _partProps = new GameObject[ZoneMap.PartSpotCount];
            for (int s = 0; s < _partProps.Length; s++)
            {
                int part = s / ZoneMap.SpotsPerPart;
                int item = ItemOf(part);
                var color = BuildableColors[item];
                var root = Greybox.Empty("Part " + PartNames[part], null, ZoneMap.PartSpot(s));
                var shape = item == Cannon ? PrimitiveType.Cylinder : item == Shield ? PrimitiveType.Cube : PrimitiveType.Sphere;
                var scale = item == Cannon ? new Vector3(0.18f, 0.28f, 0.18f) : item == Shield ? new Vector3(0.45f, 0.5f, 0.08f) : Vector3.one * 0.32f;
                var body = Greybox.Prim(shape, "Part", root, new Vector3(0f, 0.75f, 0f), scale, color, collider: false, unlit: true);
                body.AddComponent<Spin>();
                Greybox.Prim(PrimitiveType.Cylinder, "Glow", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.01f, 0.9f), color * 0.6f, collider: false, unlit: true);
                var light = new GameObject("Part Light").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0f, 1f, 0f);
                light.type = LightType.Point;
                light.color = color;
                light.range = 4f;
                light.intensity = 2f;
                light.shadows = LightShadows.None;
                _partProps[s] = root.gameObject;
            }

            // The Buddy Bot: a hovering drone with a rotor and a glowing eye.
            _robot = Greybox.Empty("Buddy Bot", null, Vector3.zero);
            var botColor = new Color(0.55f, 0.6f, 0.55f);
            Greybox.Prim(PrimitiveType.Sphere, "Body", _robot, Vector3.zero, new Vector3(0.55f, 0.4f, 0.55f), botColor, collider: false);
            Greybox.Prim(PrimitiveType.Cube, "Eye", _robot, new Vector3(0f, 0.02f, 0.26f), new Vector3(0.22f, 0.08f, 0.05f), BuildableColors[Bot], collider: false, unlit: true);
            Greybox.Box("Gun", _robot, new Vector3(0f, -0.2f, 0.22f), new Vector3(0.07f, 0.07f, 0.35f), new Color(0.15f, 0.15f, 0.15f), collider: false);
            _robotRotor = Greybox.Empty("Rotor", _robot, new Vector3(0f, 0.28f, 0f));
            Greybox.Box("Blade", _robotRotor, Vector3.zero, new Vector3(0.9f, 0.02f, 0.08f), new Color(0.2f, 0.2f, 0.2f), collider: false);
            Greybox.Box("Blade", _robotRotor, Vector3.zero, new Vector3(0.08f, 0.02f, 0.9f), new Color(0.2f, 0.2f, 0.2f), collider: false);
            _robotLight = new GameObject("Bot Light").AddComponent<Light>();
            _robotLight.transform.SetParent(_robot, false);
            _robotLight.type = LightType.Point;
            _robotLight.color = BuildableColors[Bot];
            _robotLight.range = 5f;
            _robotLight.intensity = 1.5f;
            _robotLight.shadows = LightShadows.None;
            _robotAudio = GameSettings.Effect(_robot.gameObject.AddComponent<AudioSource>());
            _robotAudio.spatialBlend = 1f;
            _robotAudio.minDistance = 3f;
            _robotAudio.maxDistance = 40f;
            _robotAudio.playOnAwake = false;
            _robot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsServer) ServerTick(Time.deltaTime);
            UpdateRobotVisual();
        }

        void UpdateRobotVisual()
        {
            bool active = RobotActive;
            if (active != _robotWasActive)
            {
                _robotWasActive = active;
                _robot.gameObject.SetActive(active);
                _robotShown = RobotPosition.Value;
            }
            if (!active) return;
            float dt = Time.deltaTime;
            Vector3 target = RobotPosition.Value;
            Vector3 prev = _robotShown;
            _robotShown = Vector3.Lerp(_robotShown, target, 1f - Mathf.Exp(-8f * dt));
            _robot.position = _robotShown + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.08f);
            Vector3 move = _robotShown - prev;
            move.y = 0f;
            if (_facing.sqrMagnitude > 0.01f) _robot.rotation = Quaternion.Slerp(_robot.rotation, Quaternion.LookRotation(_facing, Vector3.up), 1f - Mathf.Exp(-10f * dt));
            else if (move.sqrMagnitude > 0.00001f) _robot.rotation = Quaternion.Slerp(_robot.rotation, Quaternion.LookRotation(move.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
            _robotRotor.localRotation = Quaternion.Euler(0f, Time.time * 1400f, 0f);
            // Flickers in its last ten seconds.
            _robotLight.enabled = RobotTimeLeft.Value > 10f || Mathf.Repeat(Time.time * 4f, 1f) > 0.4f;
            if (Time.time > _facingUntil) _facing = Vector3.zero;
        }

        Vector3 _facing;
        float _facingUntil;

        // ---------------------------------------------------------------- server

        void Shuffle()
        {
            int layout = 0;
            for (int part = 0; part < ZoneMap.PartCount; part++)
                layout |= UnityEngine.Random.Range(0, ZoneMap.SpotsPerPart) << (part * 2);
            PartLayout.Value = layout;
        }

        /// <summary>Host only. A fresh game: new part spots, nothing found or built, the Bot back home.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            Collected.Value = 0;
            Built.Value = 0;
            RobotOwner.Value = -1;
            RobotTimeLeft.Value = 0f;
            RobotReadyRound.Value = 0;
            Shuffle();
        }

        /// <summary>Host only (test console). Every part found.</summary>
        public void DebugCollectAll()
        {
            if (IsServer) Collected.Value = (1 << ZoneMap.PartCount) - 1;
        }

        /// <summary>Host only. A player picked up whatever part is at a spot.</summary>
        public void ServerPickUp(NetPlayer player, int spot)
        {
            int part = PartWaitingAt(spot);
            if (!IsServer || part < 0) return;
            Collected.Value |= 1 << part;
            // The count goes with the message: the variable may reach clients after it.
            PartFoundRpc(part, player.OwnerClientId, PartsFound(ItemOf(part)));
        }

        /// <summary>Host only. A player used a station's place for a buildable: builds it if it's new, then hands it over.</summary>
        public void ServerUseBench(NetPlayer player, int item)
        {
            if (!IsServer || item < 0 || item >= ZoneMap.BuildableCount) return;
            if (BenchPrice(player, item, out _) < 0) return;
            if (!IsBuilt(item))
            {
                Built.Value |= 1 << item;
                ItemBuiltRpc(item, player.OwnerClientId);
            }
            switch (item)
            {
                case Cannon:
                    player.ServerGiveRewardGun(GameConfig.GunId(ScrapCannon));
                    player.TookCannon.Value = true;
                    break;
                case Shield:
                    player.ServerGiveShield();
                    break;
                default:
                    RobotOwner.Value = (long)player.OwnerClientId;
                    RobotTimeLeft.Value = RobotLifetime;
                    _robotTarget = player.transform.position + Vector3.up * 2.2f;
                    RobotPosition.Value = _robotTarget;
                    _nextShot = Time.time + 1f;
                    break;
            }
        }

        NetPlayer RobotOwnerPlayer()
        {
            long id = RobotOwner.Value;
            foreach (var p in NetPlayer.All)
                if ((long)p.OwnerClientId == id) return p;
            return null;
        }

        void ServerTick(float dt)
        {
            if (!RobotActive) return;
            var owner = RobotOwnerPlayer();
            var rounds = RoundManager.Instance;
            if (owner == null || (rounds != null && rounds.IsGameOver))
            {
                ServerBreakRobot();
                return;
            }
            RobotTimeLeft.Value = Mathf.Max(0f, RobotTimeLeft.Value - dt);
            if (RobotTimeLeft.Value <= 0f)
            {
                ServerBreakRobot();
                return;
            }

            // Hovers over the owner's shoulder.
            var t = owner.transform;
            Vector3 goal = t.position - t.right * 1.1f - t.forward * 0.6f + Vector3.up * 2.3f;
            _robotTarget = Vector3.Lerp(_robotTarget, goal, 1f - Mathf.Exp(-4f * dt));
            if (Time.time >= _nextSync)
            {
                _nextSync = Time.time + RobotSyncInterval;
                RobotPosition.Value = _robotTarget;
            }

            if (Time.time < _nextShot || rounds == null || rounds.Phase.Value != GamePhase.RoundActive) return;
            _nextShot = Time.time + RobotFireInterval;
            var target = FindRobotTarget(out Vector3 aim);
            if (target == null) return;
            long damage = Math.Max(150L, (long)(GameConfig.Round.HealthForRound(rounds.DisplayRound) * 0.3));
            Vector3 dir = (aim - _robotTarget).normalized;
            var result = target.TakeDamage(new DamageInfo(damage, aim, dir, false, owner.OwnerClientId));
            if (result.Dealt > 0) owner.ServerCreditRobotHit(target, result);
            RobotShotRpc(_robotTarget, aim);
        }

        /// <summary>The closest living zombie the Bot can see, and where to shoot it.</summary>
        NetZombie FindRobotTarget(out Vector3 aim)
        {
            aim = Vector3.zero;
            NetZombie best = null;
            float bestSqr = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(_robotTarget, RobotRange, _near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hb = _near[i].GetComponent<Hitbox>();
                if (hb == null || hb.isHead || !(hb.Owner is NetZombie z) || !z.IsAlive) continue;
                Vector3 chest = z.transform.position + Vector3.up * 1f;
                float sqr = (chest - _robotTarget).sqrMagnitude;
                if (sqr >= bestSqr || !ClearShot(chest)) continue;
                best = z;
                bestSqr = sqr;
                aim = chest;
            }
            return best;
        }

        bool ClearShot(Vector3 to)
        {
            Vector3 d = to - _robotTarget;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(_robotTarget, d / dist, _sight, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = _sight[i].collider;
                if (c.GetComponentInParent<NetZombie>() != null || c.GetComponentInParent<NetPlayer>() != null) continue;
                return false;
            }
            return true;
        }

        void ServerBreakRobot()
        {
            RobotOwner.Value = -1;
            RobotTimeLeft.Value = 0f;
            RobotReadyRound.Value = CurrentRound + RobotOfflineRounds + 1;
            RobotBrokeRpc(RobotReadyRound.Value);
        }

        // ---------------------------------------------------------------- RPCs

        [Rpc(SendTo.ClientsAndHost)]
        void PartFoundRpc(int part, ulong finder, int found) => PartFound?.Invoke(part, finder, found);

        [Rpc(SendTo.ClientsAndHost)]
        void ItemBuiltRpc(int item, ulong builder) => ItemBuilt?.Invoke(item, builder);

        [Rpc(SendTo.ClientsAndHost)]
        void RobotBrokeRpc(int readyRound)
        {
            if (_robot != null) FxSystem.Instance?.Impact(_robot.position, Vector3.up, FxSystem.Dust, 14, 3f, 0.08f);
            RobotBroke?.Invoke(readyRound);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void RobotShotRpc(Vector3 from, Vector3 to)
        {
            Vector3 muzzle = _robot != null && _robot.gameObject.activeSelf ? _robot.position + _robot.forward * 0.4f + Vector3.down * 0.2f : from;
            Vector3 d = to - muzzle;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
            {
                _facing = d.normalized;
                _facingUntil = Time.time + 0.6f;
            }
            FxSystem.Instance?.ShowTracer(muzzle, to);
            FxSystem.Instance?.Impact(to, -d.normalized, FxSystem.Blood, 4, 3f, 0.06f);
            if (_robotAudio != null && _robotAudio.isActiveAndEnabled)
            {
                _robotAudio.pitch = UnityEngine.Random.Range(1.3f, 1.5f);
                _robotAudio.PlayOneShot(ProceduralAudio.Gunshot, 0.35f);
            }
        }
    }

    /// <summary>Turns a pickup slowly so it catches the eye.</summary>
    public class Spin : MonoBehaviour
    {
        void Update() => transform.localRotation = Quaternion.Euler(15f, Time.time * 90f, 0f);
    }
}
