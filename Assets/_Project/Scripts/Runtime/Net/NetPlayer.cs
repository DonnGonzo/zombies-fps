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
        /// <summary>The survivor picked in the menu (Characters); the owner publishes it on spawn.</summary>
        public static int LocalCharacter;

        static readonly Color[] SuitColors =
        {
            new Color(0.25f, 0.35f, 0.55f),
            new Color(0.55f, 0.3f, 0.22f),
            new Color(0.3f, 0.5f, 0.3f),
            new Color(0.5f, 0.45f, 0.2f),
        };

        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Which survivor this player is (Characters), for their voice lines.</summary>
        public NetworkVariable<int> CharacterId = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
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
        /// <summary>The local player's movement (null on other players' copies).</summary>
        public PlayerMotor Motor => _motor;
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
        Renderer _remoteGun;
        Color _remoteGunColor = RemoteGunColor;
        GameObject _remoteGlow;
        Renderer[] _remoteGlowParts;
        Light _remoteGlowLight;
        int _remoteGlowTier = -1;
        // The held gun's model from the weapon pack, replacing the stand-in blocks when the gun has one.
        Transform _remoteGunRoot;
        Renderer[] _remoteBlocky;
        GameObject _remoteArt;
        int _remoteArtGun = -2;
        Color? _remoteArtCamo;
        int _remoteArtAttachments;
        static readonly Color RemoteGunColor = new Color(0.16f, 0.17f, 0.18f);
        AudioSource _remoteAudio;
        // The remote body's rig and per-character dressing, animated by AnimateRemoteBody.
        Transform _rHips, _rSpine, _rHead, _rLegL, _rLegR, _rKneeL, _rKneeR, _rFootL, _rFootR;
        Renderer[] _rJacket, _rTrousers;
        Renderer _rHair;
        GameObject[] _rHeadgear;
        int _rCharacter = -1;
        Vector3 _rLastPos;
        float _rSpeed, _rPhase, _rForward = 1f;

        // Server-only.
        float _lastDamageTime = -100f;
        bool _reviveHeld;
        int _lastRevivedFrame = -10;
        readonly RaycastHit[] _hits = new RaycastHit[64];

        public string DisplayName => PlayerName.Value.IsEmpty ? "Player " + (OwnerClientId + 1) : PlayerName.Value.ToString();
        public bool IsAlive => State.Value == LifeState.Alive;
        public HitscanGun Gun => _gun;
        public PlayerLook Look => _look;
        /// <summary>Owner-side time of the last health drop, for the damage vignette.</summary>
        public float LocalLastDamageTime { get; private set; } = -100f;
        public Color SuitColor => SuitColors[(int)(OwnerClientId % (ulong)SuitColors.Length)];
        public Character Character => Characters.At(CharacterId.Value);
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
            if (IsServer)
            {
                Points.Value = GameConfig.Round.startingPoints;
                ServerResetEquipment();
            }
            if (IsOwner)
            {
                Local = this;
                PlayerName.Value = new FixedString32Bytes(Truncate(LocalName, 20));
                CharacterId.Value = Mathf.Clamp(LocalCharacter, 0, Characters.Count - 1);
                Health.OnValueChanged += OnOwnerHealthChanged;
                Gun0.OnValueChanged += OnGun0Changed;
                Gun1.OnValueChanged += OnGun1Changed;
                SubscribeProgression();
                SubscribeShop();
                SubscribePerks();
                SubscribeVoice();
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
            if (IsOwner)
            {
                UnsubscribeShop();
                UnsubscribeVoice();
                UnsubscribePerks();
            }
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
            GraphicsSetup.ConfigureCamera(cam);
            camGo.AddComponent<AudioListener>();

            var gunAudio = GameSettings.Effect(camGo.AddComponent<AudioSource>());
            gunAudio.playOnAwake = false;
            _uiAudio = GameSettings.Effect(camGo.AddComponent<AudioSource>());
            _uiAudio.playOnAwake = false;

            var view = Greybox.Empty("Weapon", camGo.transform, Vector3.zero).gameObject.AddComponent<WeaponView>();
            _look.Init(_pivot, camGo.transform);
            _gun.Init(GameConfig.Gun, cam, _look, _motor, () => IsAlive, gunAudio, _uiAudio, view);
            view.Init(_gun, _motor, _look);
            _gun.Fired += (origin, dir, primary) => ShootRpc(origin, dir, primary);
            ResetLoadoutAmmo();

            new GameObject("HUD").AddComponent<PrototypeHud>().Init(this);
        }

        void BuildRemoteBody()
        {
            // A survivor built like the zombies (hip, knee and ankle pivots for the walk), but standing straight,
            // dressed in the character's colours and holding the gun with both hands.
            var skin = Greybox.Lit(new Color(0.78f, 0.62f, 0.5f));
            var boots = Greybox.Lit(new Color(0.2f, 0.14f, 0.1f), Surface.Rubber, true);
            var pack = Greybox.Lit(Color.Lerp(SuitColor, new Color(0.25f, 0.25f, 0.2f), 0.4f), Surface.Fabric, true);
            var strap = Greybox.Lit(new Color(0.12f, 0.11f, 0.1f), Surface.Rubber, true);
            var jacket = new List<Renderer>();
            var trousers = new List<Renderer>();

            _body = Greybox.Empty("Body", transform, Vector3.zero);
            _rHips = Greybox.Empty("Hips", _body, new Vector3(0f, 0.95f, 0f));
            RemotePart(trousers, null, PrimitiveType.Cube, "Pelvis", _rHips, Vector3.zero, new Vector3(0.32f, 0.19f, 0.21f));
            RemotePart(null, strap, PrimitiveType.Cube, "Belt", _rHips, new Vector3(0f, 0.08f, 0f), new Vector3(0.33f, 0.04f, 0.22f), shadow: false);
            BuildRemoteLeg(-1, trousers, boots, out _rLegL, out _rKneeL, out _rFootL);
            BuildRemoteLeg(1, trousers, boots, out _rLegR, out _rKneeR, out _rFootR);

            _rSpine = Greybox.Empty("Spine", _rHips, new Vector3(0f, 0.06f, 0f));
            RemotePart(jacket, null, PrimitiveType.Cube, "Belly", _rSpine, new Vector3(0f, 0.13f, 0f), new Vector3(0.33f, 0.25f, 0.22f));
            RemotePart(jacket, null, PrimitiveType.Cube, "Chest", _rSpine, new Vector3(0f, 0.36f, 0f), new Vector3(0.42f, 0.28f, 0.26f));
            RemotePart(jacket, null, PrimitiveType.Capsule, "Shoulders", _rSpine, new Vector3(0f, 0.46f, -0.01f), new Vector3(0.17f, 0.24f, 0.17f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            RemotePart(jacket, null, PrimitiveType.Cube, "Collar", _rSpine, new Vector3(0f, 0.53f, -0.02f), new Vector3(0.22f, 0.07f, 0.2f));
            RemotePart(null, strap, PrimitiveType.Cube, "Zip", _rSpine, new Vector3(0f, 0.25f, 0.131f), new Vector3(0.02f, 0.44f, 0.01f), shadow: false);
            // Backpack in the player's suit colour, with a bedroll on top and straps over the shoulders.
            RemotePart(null, pack, PrimitiveType.Cube, "Pack", _rSpine, new Vector3(0f, 0.3f, -0.22f), new Vector3(0.32f, 0.4f, 0.18f));
            RemotePart(null, pack, PrimitiveType.Cube, "Pocket", _rSpine, new Vector3(0f, 0.18f, -0.32f), new Vector3(0.24f, 0.14f, 0.05f), shadow: false);
            RemotePart(null, strap, PrimitiveType.Cylinder, "Bedroll", _rSpine, new Vector3(0f, 0.55f, -0.22f), new Vector3(0.12f, 0.17f, 0.12f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            RemotePart(null, strap, PrimitiveType.Cube, "StrapL", _rSpine, new Vector3(-0.12f, 0.36f, 0.133f), new Vector3(0.04f, 0.3f, 0.01f), shadow: false);
            RemotePart(null, strap, PrimitiveType.Cube, "StrapR", _rSpine, new Vector3(0.12f, 0.36f, 0.133f), new Vector3(0.04f, 0.3f, 0.01f), shadow: false);

            // Head, a hair base, and one headgear per character (picked in ApplyRemoteCharacter).
            RemotePart(null, skin, PrimitiveType.Cylinder, "Neck", _rSpine, new Vector3(0f, 0.57f, 0.01f), new Vector3(0.1f, 0.05f, 0.1f));
            _rHead = Greybox.Empty("Head", _rSpine, new Vector3(0f, 0.68f, 0.02f));
            RemotePart(null, skin, PrimitiveType.Sphere, "Skull", _rHead, Vector3.zero, new Vector3(0.24f, 0.28f, 0.26f));
            RemotePart(null, skin, PrimitiveType.Cube, "Nose", _rHead, new Vector3(0f, -0.01f, 0.13f), new Vector3(0.035f, 0.05f, 0.04f), shadow: false);
            var eye = Greybox.Lit(new Color(0.08f, 0.07f, 0.06f));
            RemotePart(null, eye, PrimitiveType.Cube, "EyeL", _rHead, new Vector3(-0.05f, 0.03f, 0.125f), new Vector3(0.03f, 0.018f, 0.01f), shadow: false);
            RemotePart(null, eye, PrimitiveType.Cube, "EyeR", _rHead, new Vector3(0.05f, 0.03f, 0.125f), new Vector3(0.03f, 0.018f, 0.01f), shadow: false);
            _rHair = RemotePart(null, skin, PrimitiveType.Sphere, "Hair", _rHead, new Vector3(0f, 0.035f, -0.015f), new Vector3(0.255f, 0.27f, 0.25f));
            _rHeadgear = new GameObject[4];
            // Marlow: a peaked cop's cap.
            var cap = Greybox.Empty("Cap", _rHead, Vector3.zero);
            RemotePart(jacket, null, PrimitiveType.Cylinder, "Crown", cap, new Vector3(0f, 0.1f, 0f), new Vector3(0.26f, 0.05f, 0.27f));
            RemotePart(null, strap, PrimitiveType.Cube, "Brim", cap, new Vector3(0f, 0.075f, 0.14f), new Vector3(0.2f, 0.015f, 0.1f), shadow: false)
                .transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            _rHeadgear[0] = cap.gameObject;
            // Dee: a knitted beanie.
            var beanie = Greybox.Empty("Beanie", _rHead, Vector3.zero);
            RemotePart(jacket, null, PrimitiveType.Sphere, "Knit", beanie, new Vector3(0f, 0.07f, -0.01f), new Vector3(0.26f, 0.2f, 0.27f));
            RemotePart(jacket, null, PrimitiveType.Cylinder, "Cuff", beanie, new Vector3(0f, 0.045f, -0.01f), new Vector3(0.265f, 0.025f, 0.275f), shadow: false);
            _rHeadgear[1] = beanie.gameObject;
            // Dr. Hale: glasses (hair only, no hat).
            var glasses = Greybox.Empty("Glasses", _rHead, Vector3.zero);
            RemotePart(null, strap, PrimitiveType.Cube, "LensL", glasses, new Vector3(-0.05f, 0.03f, 0.134f), new Vector3(0.06f, 0.035f, 0.01f), shadow: false);
            RemotePart(null, strap, PrimitiveType.Cube, "LensR", glasses, new Vector3(0.05f, 0.03f, 0.134f), new Vector3(0.06f, 0.035f, 0.01f), shadow: false);
            RemotePart(null, strap, PrimitiveType.Cube, "Bridge", glasses, new Vector3(0f, 0.035f, 0.136f), new Vector3(0.05f, 0.01f, 0.01f), shadow: false);
            _rHeadgear[2] = glasses.gameObject;
            // Rook: a cap on backwards.
            var back = Greybox.Empty("BackwardsCap", _rHead, Vector3.zero);
            RemotePart(jacket, null, PrimitiveType.Sphere, "Crown", back, new Vector3(0f, 0.08f, -0.005f), new Vector3(0.26f, 0.17f, 0.27f));
            RemotePart(jacket, null, PrimitiveType.Cube, "Brim", back, new Vector3(0f, 0.07f, -0.15f), new Vector3(0.18f, 0.015f, 0.1f), shadow: false)
                .transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            _rHeadgear[3] = back.gameObject;

            // The gun rides the chest so it sways with the body; the arms are solved once to its grips.
            var gun = Greybox.Empty("Gun", _rSpine, new Vector3(0.14f, 0.27f, 0.3f));
            _remoteGun = Greybox.Box("Receiver", gun, Vector3.zero, new Vector3(0.08f, 0.1f, 0.6f), RemoteGunColor, collider: false).GetComponent<Renderer>();
            var metal = Greybox.Lit(new Color(0.1f, 0.1f, 0.11f), Surface.Metal, true);
            RemotePart(null, metal, PrimitiveType.Cylinder, "Barrel", gun, new Vector3(0f, 0.015f, 0.38f), new Vector3(0.035f, 0.1f, 0.035f), shadow: false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            RemotePart(null, metal, PrimitiveType.Cube, "Grip", gun, new Vector3(0f, -0.09f, -0.1f), new Vector3(0.05f, 0.12f, 0.06f), shadow: false)
                .transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            RemotePart(null, metal, PrimitiveType.Cube, "Mag", gun, new Vector3(0f, -0.11f, 0.07f), new Vector3(0.05f, 0.15f, 0.08f), shadow: false)
                .transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            RemotePart(null, metal, PrimitiveType.Cube, "Stock", gun, new Vector3(0f, -0.02f, -0.38f), new Vector3(0.06f, 0.1f, 0.18f), shadow: false);
            RemotePart(null, metal, PrimitiveType.Cube, "Sight", gun, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.03f, 0.04f, 0.08f), shadow: false);
            _remoteMuzzle = Greybox.Empty("Muzzle", gun, new Vector3(0f, 0.015f, 0.48f));
            _remoteGunRoot = gun;
            _remoteBlocky = gun.GetComponentsInChildren<Renderer>();
            Vector3 g = gun.localPosition;
            BuildRemoteArm(1, jacket, skin, new Vector3(0.24f, 0.46f, 0f), g + new Vector3(0f, -0.08f, -0.1f), new Vector3(0.6f, -1f, -0.3f));
            // The support shoulder rolls forward a little, as in a real rifle stance, so the arm reaches the foregrip.
            BuildRemoteArm(-1, jacket, skin, new Vector3(-0.22f, 0.46f, 0.06f), g + new Vector3(0f, -0.06f, 0.12f), new Vector3(-0.6f, -1f, 0f));
            _rJacket = jacket.ToArray();
            _rTrousers = trousers.ToArray();
            _rCharacter = -1;
            ApplyRemoteCharacter();
            _rLastPos = transform.position;

            // An upgraded gun glows in its tier's colour, so teammates can see it too.
            _remoteGlow = Greybox.Empty("Upgrade Glow", gun, Vector3.zero).gameObject;
            _remoteGlowParts = new[]
            {
                Greybox.Prim(PrimitiveType.Cube, "StripL", _remoteGlow.transform, new Vector3(-0.042f, 0f, 0.05f), new Vector3(0.006f, 0.03f, 0.45f), Color.white, collider: false, unlit: true).GetComponent<Renderer>(),
                Greybox.Prim(PrimitiveType.Cube, "StripR", _remoteGlow.transform, new Vector3(0.042f, 0f, 0.05f), new Vector3(0.006f, 0.03f, 0.45f), Color.white, collider: false, unlit: true).GetComponent<Renderer>(),
                Greybox.Prim(PrimitiveType.Cube, "MuzzleRing", _remoteGlow.transform, new Vector3(0f, 0.015f, 0.46f), new Vector3(0.1f, 0.11f, 0.02f), Color.white, collider: false, unlit: true).GetComponent<Renderer>(),
            };
            _remoteGlowLight = _remoteGlow.AddComponent<Light>();
            _remoteGlowLight.type = LightType.Point;
            _remoteGlowLight.range = 2.5f;
            _remoteGlowLight.intensity = 1.2f;
            _remoteGlowLight.shadows = LightShadows.None;
            _remoteGlow.SetActive(false);

            _remoteAudio = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
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

        void BuildRemoteLeg(int side, List<Renderer> trousers, Material boots, out Transform hip, out Transform knee, out Transform foot)
        {
            string s = side < 0 ? "L" : "R";
            hip = Greybox.Empty("Leg" + s, _rHips, new Vector3(0.1f * side, -0.03f, 0f));
            RemotePart(trousers, null, PrimitiveType.Capsule, "Thigh" + s, hip, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.23f, 0.16f));
            knee = Greybox.Empty("Knee" + s, hip, new Vector3(0f, -0.44f, 0f));
            RemotePart(trousers, null, PrimitiveType.Capsule, "Shin" + s, knee, new Vector3(0f, -0.19f, 0f), new Vector3(0.13f, 0.21f, 0.13f));
            foot = Greybox.Empty("Foot" + s, knee, new Vector3(0f, -0.42f, 0f));
            RemotePart(null, boots, PrimitiveType.Cube, "Boot" + s, foot, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.13f, 0.13f, 0.27f));
        }

        /// <summary>
        /// An arm from the shoulder to a hand on the gun, all in chest space. Solved once here (two-bone IK with
        /// the elbow toward <paramref name="pole"/>): the gun and both shoulders ride the chest, so it stays solved.
        /// </summary>
        void BuildRemoteArm(int side, List<Renderer> jacket, Material skin, Vector3 shoulder, Vector3 hand, Vector3 pole)
        {
            const float upper = 0.3f, lower = 0.29f;
            string s = side < 0 ? "L" : "R";
            Vector3 toHand = hand - shoulder;
            float d = Mathf.Clamp(toHand.magnitude, 0.05f, (upper + lower) * 0.995f);
            Vector3 dir = toHand.normalized;
            hand = shoulder + dir * d;
            // Law of cosines: how far along the shoulder-hand line the elbow sits, and how far off it.
            float along = (upper * upper - lower * lower + d * d) / (2f * d);
            float off = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir).normalized;
            Vector3 elbow = shoulder + dir * along + bend * off;

            var upperT = Greybox.Empty("UpperArm" + s, _rSpine, shoulder);
            upperT.localRotation = Quaternion.LookRotation(elbow - shoulder, Vector3.up);
            RemotePart(jacket, null, PrimitiveType.Capsule, "Sleeve" + s, upperT, new Vector3(0f, 0f, upper * 0.5f), new Vector3(0.12f, upper * 0.55f, 0.12f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lowerT = Greybox.Empty("Forearm" + s, _rSpine, elbow);
            lowerT.localRotation = Quaternion.LookRotation(hand - elbow, Vector3.up);
            RemotePart(jacket, null, PrimitiveType.Capsule, "Cuff" + s, lowerT, new Vector3(0f, 0f, lower * 0.45f), new Vector3(0.105f, lower * 0.5f, 0.105f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            RemotePart(null, skin, PrimitiveType.Cube, "Hand" + s, lowerT, new Vector3(0f, -0.01f, lower + 0.02f), new Vector3(0.06f, 0.08f, 0.09f), shadow: false);
        }

        /// <summary>A part of the remote body: listed in <paramref name="group"/> to be recoloured, or given a fixed material.</summary>
        static Renderer RemotePart(List<Renderer> group, Material material, PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, bool shadow = true)
        {
            var r = Greybox.Prim(type, name, parent, pos, scale, Color.grey, collider: false).GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            group?.Add(r);
            return r;
        }

        // Trousers per character, and hair (shown where no hat covers it).
        static readonly Color[] RemoteTrousers =
        {
            new Color(0.12f, 0.14f, 0.2f),
            new Color(0.28f, 0.22f, 0.16f),
            new Color(0.32f, 0.3f, 0.24f),
            new Color(0.13f, 0.13f, 0.14f),
        };
        static readonly Color[] RemoteHair =
        {
            new Color(0.2f, 0.16f, 0.12f),
            new Color(0.35f, 0.15f, 0.08f),
            new Color(0.55f, 0.53f, 0.5f),
            new Color(0.08f, 0.07f, 0.06f),
        };

        /// <summary>Dresses the body as the picked character. The pick can arrive after spawn, so it's re-checked each frame.</summary>
        void ApplyRemoteCharacter()
        {
            int id = CharacterId.Value;
            if (id == _rCharacter) return;
            _rCharacter = id;
            int look = Mathf.Abs(id) % _rHeadgear.Length;
            // The character's colour, toned down to read as a worn jacket rather than a costume.
            var jacket = Greybox.Lit(Color.Lerp(Character.color, new Color(0.22f, 0.22f, 0.2f), 0.4f) * 0.85f, Surface.Fabric, true);
            foreach (var r in _rJacket) r.sharedMaterial = jacket;
            var trousers = Greybox.Lit(RemoteTrousers[look], Surface.Fabric, true);
            foreach (var r in _rTrousers) r.sharedMaterial = trousers;
            _rHair.sharedMaterial = Greybox.Lit(RemoteHair[look], Surface.Fabric, true);
            for (int i = 0; i < _rHeadgear.Length; i++) _rHeadgear[i].SetActive(i == look);
        }

        /// <summary>Walk cycle for a teammate's body, from how fast it's moving (positions come from the network).</summary>
        void AnimateRemoteBody()
        {
            if (_body == null) return;
            ApplyRemoteCharacter();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 delta = transform.position - _rLastPos;
            _rLastPos = transform.position;
            delta.y = 0f;
            float k = 1f - Mathf.Exp(-10f * dt);
            _rSpeed = Mathf.Lerp(_rSpeed, delta.magnitude / dt, k);
            // Walking backwards runs the cycle backwards; strafing still steps.
            float forward = Vector3.Dot(delta, transform.forward);
            _rForward = Mathf.Lerp(_rForward, forward < -0.001f ? -1f : 1f, k);
            if (State.Value != LifeState.Alive) _rSpeed = 0f;

            float gait = Mathf.Clamp01(_rSpeed / 2f);
            float sprint = Mathf.Clamp01((_rSpeed - 4.5f) / 2f);
            _rPhase += dt * Mathf.Min(_rSpeed / Mathf.Lerp(1.5f, 2.2f, sprint), 3f) * Mathf.PI * 2f * Mathf.Sign(_rForward);
            if (_rPhase > 1000f || _rPhase < -1000f) _rPhase %= Mathf.PI * 2f;
            float p = _rPhase;
            float hipAmp = Mathf.Lerp(24f, 40f, sprint), kneeAmp = Mathf.Lerp(45f, 80f, sprint);
            PoseRemoteLeg(_rLegL, _rKneeL, _rFootL, p, -1, gait, hipAmp, kneeAmp);
            PoseRemoteLeg(_rLegR, _rKneeR, _rFootR, p + Mathf.PI, 1, gait, hipAmp, kneeAmp);
            float sinP = Mathf.Sin(p);
            _rHips.localPosition = new Vector3(0f, 0.95f + 0.035f * gait * (Mathf.Abs(Mathf.Cos(p)) - 1f), 0f);
            _rHips.localRotation = Quaternion.Euler(0f, 6f * gait * sinP, 0f);
            // The chest counter-twists and leans into a sprint; the gun rides it.
            _rSpine.localRotation = Quaternion.Euler(3f + 10f * sprint, -6f * gait * sinP, 2f * gait * Mathf.Sin(p * 2f));
            // A little breathing at rest, and the head keeps level with the gun.
            _rHead.localRotation = Quaternion.Euler(-(3f + 10f * sprint) * 0.7f + Mathf.Sin(Time.time * 1.6f) * 1.5f * (1f - gait), 0f, 0f);
        }

        static void PoseRemoteLeg(Transform hip, Transform knee, Transform foot, float phase, int side, float gait, float hipAmp, float kneeAmp)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float h = -hipAmp * gait * s;
            float bend = 3f + (kneeAmp * Mathf.Max(0f, c) + 6f * Mathf.Max(0f, -c)) * gait;
            hip.localRotation = Quaternion.Euler(h, 0f, 2f * side);
            knee.localRotation = Quaternion.Euler(bend, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-(h + bend) * 0.85f, 0f, 0f);
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
            if (IsOwner)
            {
                OwnerTick();
                OwnerVoiceTick();
            }
            else
            {
                UpdateRemoteGun();
                AnimateRemoteBody();
            }
            VoiceTick();
            UpdateShieldView();
        }

        /// <summary>Teammates see your camo and attachments on the gun in your hands.</summary>
        void UpdateRemoteGun()
        {
            if (_remoteGun == null) return;
            int tier = TierInSlot(ActiveSlot.Value);
            if (tier != _remoteGlowTier && _remoteGlow != null)
            {
                _remoteGlowTier = tier;
                _remoteGlow.SetActive(tier > 0);
                if (tier > 0)
                {
                    var glow = UpgradeData.TierGlow(tier);
                    var mat = Greybox.Unlit(glow);
                    foreach (var r in _remoteGlowParts) r.sharedMaterial = mat;
                    _remoteGlowLight.color = glow;
                }
            }
            int gunId = GunInSlot(ActiveSlot.Value);
            var camo = CamoColorFor(ActiveSlot.Value);
            int attachments = AttachmentsInSlot(ActiveSlot.Value);
            if (gunId != _remoteArtGun || camo != _remoteArtCamo || attachments != _remoteArtAttachments)
            {
                _remoteArtGun = gunId;
                _remoteArtCamo = camo;
                _remoteArtAttachments = attachments;
                if (_remoteArt != null) Destroy(_remoteArt);
                _remoteArt = GunArt.BuildProp(GameConfig.GunById(gunId), _remoteGunRoot, camo, attachments);
                // Its sights sit about where the stand-in's did, the grip under the hand.
                if (_remoteArt != null) _remoteArt.transform.localPosition = new Vector3(0f, -0.04f, -0.02f);
                foreach (var r in _remoteBlocky) r.enabled = _remoteArt == null;
            }
            var color = camo ?? RemoteGunColor;
            if (color == _remoteGunColor) return;
            _remoteGunColor = color;
            _remoteGun.sharedMaterial = Greybox.Lit(color);
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
                int want = picking || ShopOpen ? -1 : GameInput.SlotPressed;
                if (GameInput.SwitchWeaponPressed) want = 1 - ActiveSlot.Value;
                if (want >= 0 && want != ActiveSlot.Value && GunInSlot(want) >= 0) EquipSlot(want, saveCurrent: true);
            }

            OwnerEquipmentTick(canAct && !picking && !ShopOpen);

            bool nearDowned = FindReviveTarget(transform.position) != null;
            FocusedBuyable = canAct && !nearDowned ? FindBuyable() : null;
            OwnerShopTick(canAct, picking);
            if (FocusedBuyable != null && FocusedBuyable.Kind != BuyableKind.Shop && GameInput.InteractPressed)
                BuyRpc(System.Array.IndexOf(GameConfig.Map.Buyables, FocusedBuyable));

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
                    if (Health.Value < max && Time.time - _lastDamageTime >= RegenDelayNow)
                        Health.Value = Mathf.Min(max, Health.Value + RegenPerSecond * dt);
                    if (_reviveHeld)
                    {
                        var target = FindReviveTarget(transform.position);
                        if (target != null) target.ServerReviveTick(dt * (1f + Bonus(PowerStat.ReviveSpeed)) * PerkReviveScale);
                    }
                    break;

                case LifeState.Downed:
                    if (ServerSelfReviveTick()) break;
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
            var data = EffectiveGun(slot);
            if (data == null) return;
            ActiveSlot.Value = slot;
            var tier = GameConfig.Upgrades.Tier(TierInSlot(slot));
            _gun.ExplosiveRounds = (tier != null && tier.explosive) || data.explosiveRounds;
            _gun.UpgradeTier = TierInSlot(slot);
            _gun.Pierce = data.PierceWith(tier);
            _gun.Camo = CamoColorFor(slot);
            _gun.Attachments = AttachmentsInSlot(slot);
            _gun.ReloadSpeed = ReloadSpeedNow;
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
                var data = EffectiveGun(i);
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
                if (b.Kind == BuyableKind.Part && (BuildManager.Instance == null || BuildManager.Instance.PartWaitingAt(b.Index) < 0)) continue;
                Vector3 d = b.Position - here;
                // Up on a ledge or down below doesn't count.
                if (Mathf.Abs(d.y - 1f) > 1.6f) continue;
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
            if (b.Kind == BuyableKind.Perk || b.Kind == BuyableKind.Armor || b.Kind == BuyableKind.Reroll) return PerkStationPrice(b, out what);
            if (b.Kind == BuyableKind.Shop)
            {
                what = "browse the shop";
                return 0;
            }
            if (b.Kind == BuyableKind.Part)
            {
                int part = BuildManager.Instance != null ? BuildManager.Instance.PartWaitingAt(b.Index) : -1;
                if (part < 0) return -1;
                what = "pick up the " + BuildManager.PartNames[part];
                return 0;
            }
            if (b.Kind == BuyableKind.Workbench)
            {
                if (BuildManager.Instance == null) return -1;
                return BuildManager.Instance.BenchPrice(this, b.Index % ZoneMap.BuildableCount, out what);
            }
            if (b.Kind == BuyableKind.Reactor) return EasterEgg.Instance != null ? EasterEgg.Instance.ReactorPrice(this, out what) : -1;
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
        public void ServerApplyDamage(float amount, Vector3 from, bool acid = false)
        {
            if (!IsServer || State.Value != LifeState.Alive || amount <= 0f) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.GodMode.Value) return;
            amount = ServerShieldBlock(amount, from, acid);
            amount = ServerAbsorb(amount, acid);
            _lastDamageTime = Time.time;
            if (amount <= 0f) return;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            if (Health.Value <= 0f)
            {
                if (MissionBoard.Instance != null) MissionBoard.Instance.ServerOnPlayerDowned();
                ServerOnDowned();
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
            if (HasPerk(PerkKind.HazardWard)) return;
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
            Att0.Value = 0;
            Att1.Value = 0;
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
            ServerResetPerks();
            ServerResetEquipment();
            ServerResetBuildables();
            ServerRespawn(spawn);
        }

        // ---------------------------------------------------------------- RPCs

        [Rpc(SendTo.Server)]
        void SetReviveHeldRpc(bool held)
        {
            _reviveHeld = held;
        }

        /// <summary>Owner fired (one call per pellet): the host resolves the hit against its own copy of the world.</summary>
        [Rpc(SendTo.Server)]
        void ShootRpc(Vector3 origin, Vector3 direction, bool primary)
        {
            if (State.Value != LifeState.Alive) return;
            var gun = EffectiveGun(ActiveSlot.Value) ?? GameConfig.Gun;
            // Co-op trust model: accept the client's aim, but keep the origin near the player.
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if ((origin - eye).sqrMagnitude > 2f * 2f) origin = eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            Vector3 end = origin + direction * gun.range;
            Vector3 normal = -direction;
            bool flesh = false;
            NetZombie primaryZombie = null;
            // Where chain and explosive effects go off: the first zombie hit, or where the bullet stops.
            Vector3 impact = end, impactNormal = normal;
            var tier = GameConfig.Upgrades.Tier(TierInSlot(ActiveSlot.Value));
            double baseDamage = gun.damage * (double)GunDamageMultiplier(ActiveSlot.Value);
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, gun.range, ZoneMap.ShotMask, QueryTriggerInteraction.Ignore);
            // Nearest first, so a bullet stops at the first wall and goes through zombies in order.
            System.Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);
            _pierced.Clear();
            // Bullet penetration: the bullet carries on through zombies while it has pierce left, losing some of
            // its damage in each body, until it runs out or meets a wall or prop.
            int pierceLeft = gun.PierceWith(tier);
            double carried = 1.0;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                // Players never block shots (no friendly fire, no blocking your teammates' bullets).
                if (hit.collider.GetComponentInParent<NetPlayer>() != null) continue;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.Owner is NetZombie zombie)
                {
                    if (!zombie.IsAlive || !_pierced.Add(zombie)) continue;
                    flesh = true;
                    end = hit.point;
                    normal = hit.normal;
                    if (primaryZombie == null) { primaryZombie = zombie; impact = hit.point; impactNormal = hit.normal; }
                    float mult = hitbox.isHead ? gun.headshotMultiplier * (1f + Bonus(PowerStat.HeadshotDamage)) : hitbox.multiplier;
                    long damage = System.Math.Max(1L, (long)System.Math.Round(baseDamage * mult * carried));
                    var result = zombie.TakeDamage(new DamageInfo(damage, hit.point, direction, hitbox.isHead, OwnerClientId, _pierced.Count - 1));
                    AwardPoints(zombie, result, hitbox.isHead);
                    OnServerKillHeal(result);
                    HitConfirmRpc(result.Killed, hitbox.isHead);
                    pierceLeft -= zombie.PenetrationCost;
                    if (pierceLeft < 0) break;
                    carried *= gun.pierceDamageKept;
                    // Through and out the other side: unless something else is hit, it flies on to full range.
                    end = origin + direction * gun.range;
                    normal = -direction;
                    flesh = false;
                    continue;
                }
                // A wall or prop: the bullet stops here.
                end = hit.point;
                normal = hit.normal;
                flesh = false;
                break;
            }

            if (primaryZombie == null) { impact = end; impactNormal = normal; }

            if (primary)
            {
                if (tier != null && tier.chain && primaryZombie != null) ServerChain(primaryZombie, impact, baseDamage, direction);
                bool explosive = (tier != null && tier.explosive) || gun.explosiveRounds;
                if (explosive) ServerExplode(primaryZombie, impact, baseDamage, direction);
                ShotFxRpc(end, normal, flesh, explosive, true, _pierced.Count, impact, impactNormal);
            }
            else
            {
                ShotFxRpc(end, normal, flesh, false, false, _pierced.Count, impact, impactNormal);
            }
        }

        readonly HashSet<NetZombie> _pierced = new HashSet<NetZombie>();

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        void AwardPoints(NetZombie zombie, DamageResult result, bool headshot)
        {
            var data = zombie.Data;
            double eventScale = RoundManager.Instance != null ? RoundManager.Instance.PointsMultiplier : 1.0;
            if (!result.Killed)
            {
                Points.Value += (long)System.Math.Round(data.pointsPerHit * eventScale);
                return;
            }
            long reward = headshot ? data.pointsPerHeadshotKill : data.pointsPerKill;
            Points.Value += (long)System.Math.Round(reward * (1.0 + Bonus(PowerStat.KillPoints)) * eventScale);
            if (Shop != null) ServerAddXp(data.IsBoss ? Shop.xpPerBossKill : headshot ? Shop.xpPerHeadshotKill : Shop.xpPerKill);
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
        void ShotFxRpc(Vector3 end, Vector3 normal, bool flesh, bool explosive, bool withSound, int pierced, Vector3 impact, Vector3 impactNormal)
        {
            if (_remoteMuzzle == null) return;
            int tier = TierInSlot(ActiveSlot.Value);
            FxSystem.Instance?.ShowTracer(_remoteMuzzle.position, end, UpgradeData.TierGlow(tier), pierced > 1);
            FxSystem.Instance?.Impact(end, normal, flesh ? FxSystem.Blood : FxSystem.Dust, flesh ? 5 : 3, 3f, 0.06f);
            if (explosive) FxSystem.Instance?.Impact(impact, impactNormal, FxSystem.Blast, 16, 6f, 0.09f);
            if (!withSound) return;
            _remoteAudio.pitch = Random.Range(0.94f, 1.06f);
            _remoteAudio.PlayOneShot(ProceduralAudio.GunshotFor(tier), 0.8f);
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
            if (Mathf.Abs(d.y - 1f) > 2.2f) return;
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
                ServerSay(VoiceEvent.BuyDoor);
            }
            else if (b.Kind == BuyableKind.UpgradeMachine)
            {
                ServerUpgradeGunInHand();
                ServerSay(VoiceEvent.Upgrade);
            }
            else if (b.Kind == BuyableKind.Perk || b.Kind == BuyableKind.Armor || b.Kind == BuyableKind.Reroll)
            {
                ServerBuyFromStation(b);
                if (b.Kind == BuyableKind.Perk) ServerSay(VoiceEvent.BuyPerk);
            }
            else if (b.Kind == BuyableKind.Part)
            {
                BuildManager.Instance.ServerPickUp(this, b.Index);
            }
            else if (b.Kind == BuyableKind.Workbench)
            {
                BuildManager.Instance.ServerUseBench(this, b.Index % ZoneMap.BuildableCount);
            }
            else if (b.Kind == BuyableKind.Reactor)
            {
                EasterEgg.Instance.ServerTouchReactor(this);
            }
            else
            {
                int gunId = GameConfig.GunId(GameConfig.Prices.WallGunAt(b.Index).gun);
                int owned = SlotOf(gunId);
                if (owned >= 0) AmmoBoughtRpc(owned);
                else
                {
                    ServerGiveGun(gunId);
                    ServerSay(VoiceEvent.BuyGun);
                }
            }
            PurchaseResultRpc(true);
        }

        /// <summary>Host only. Puts a gun in the empty slot, or in place of the one in hand (a wall buy or a reward).</summary>
        public void ServerGiveGun(int gunId)
        {
            if (gunId < 0) return;
            int owned = SlotOf(gunId);
            if (owned >= 0)
            {
                AmmoBoughtRpc(owned);
                return;
            }
            int slot = Gun1.Value < 0 ? 1 : Mathf.Clamp(ActiveSlot.Value, 0, SlotCount - 1);
            TierVariable(slot).Value = 0;
            AttVariable(slot).Value = 0;
            SlotVariable(slot).Value = gunId;
        }

        /// <summary>
        /// Host only. A mission's reward gun: goes in an empty slot, or replaces the less invested of the two guns
        /// (fewer upgrade tiers and attachments), so a reward never silently eats an upgraded gun if there's a plainer one.
        /// </summary>
        public void ServerGiveRewardGun(int gunId)
        {
            if (gunId < 0) return;
            int owned = SlotOf(gunId);
            if (owned >= 0)
            {
                AmmoBoughtRpc(owned);
                return;
            }
            int slot = Gun1.Value < 0 ? 1 : Gun0.Value < 0 ? 0 : Invested(0) <= Invested(1) ? 0 : 1;
            TierVariable(slot).Value = 0;
            AttVariable(slot).Value = 0;
            SlotVariable(slot).Value = gunId;
        }

        int Invested(int slot)
        {
            int atts = 0;
            for (int v = AttVariable(slot).Value; v != 0; v &= v - 1) atts++;
            return TierVariable(slot).Value * 4 + atts;
        }

        [Rpc(SendTo.Owner)]
        void PurchaseResultRpc(bool success)
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(success ? ProceduralAudio.Purchase : ProceduralAudio.Denied, 0.8f);
        }

        /// <summary>Host only. Every gun this player owns back to full (a mission reward).</summary>
        public void ServerRefillAllAmmo()
        {
            ServerRefillEquipment(full: true);
            RefillAllRpc();
        }

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
