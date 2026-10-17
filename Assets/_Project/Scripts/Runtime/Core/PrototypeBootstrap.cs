using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Player;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Entry point. Builds the world every machine shares (lighting, greybox arena + NavMesh, effects),
    /// shows the host/join menu, and owns the NetworkManager. Everything that changes during play lives
    /// on network objects: NetPlayer, NetZombie and RoundManager.
    /// </summary>
    public class PrototypeBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to use built-in defaults.")]
        public GunData gunData;
        public ZombieData zombieData;
        public RoundData roundData;

        const string NameKey = "ZombiesFPS.PlayerName";
        const string AddressKey = "ZombiesFPS.JoinAddress";
        const int ZombiePoolSize = 64;
        static readonly Color FogColor = new Color(0.05f, 0.06f, 0.08f);

        /// <summary>Why the last session ended, shown on the menu after a scene reload.</summary>
        static string s_LastMessage;

        NetworkManager _network;
        UnityTransport _transport;
        Camera _menuCamera;
        string _name;
        string _address;
        string _port;
        string _status;
        bool _connecting;
        GUIStyle _title, _label, _button, _field;

        void Awake()
        {
            GameConfig.Gun = gunData != null ? gunData : ScriptableObject.CreateInstance<GunData>();
            GameConfig.Zombie = zombieData != null ? zombieData : ScriptableObject.CreateInstance<ZombieData>();
            GameConfig.Round = roundData != null ? roundData : ScriptableObject.CreateInstance<RoundData>();
            GameConfig.PlayerPrefab = Resources.Load<GameObject>("Net/NetPlayer");
            GameConfig.ZombiePrefab = Resources.Load<GameObject>("Net/NetZombie");
            GameConfig.DirectorPrefab = Resources.Load<GameObject>("Net/NetDirector");

            SetUpEnvironment();
            var arena = PrototypeArena.Build();
            GameConfig.ZombieSpawnPoints = arena.SpawnPoints;
            GameConfig.PlayerStart = arena.PlayerStart;
            new GameObject("FX").AddComponent<FxSystem>();
            BuildMenuCamera();
            CreateNetworkManager();

            _name = PlayerPrefs.GetString(NameKey, "Survivor");
            _address = PlayerPrefs.GetString(AddressKey, "127.0.0.1");
            _port = GameConfig.DefaultPort.ToString();
            _status = s_LastMessage;
            s_LastMessage = null;
            PlayerLook.SetCursorLocked(false);
        }

        void OnDestroy()
        {
            if (_network != null)
            {
                _network.OnClientDisconnectCallback -= OnClientDisconnect;
                _network.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        static void SetUpEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.skybox = null;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            moon.color = new Color(0.62f, 0.7f, 0.95f);
            moon.intensity = 0.45f;
            moon.shadows = LightShadows.Soft;
        }

        void BuildMenuCamera()
        {
            var go = new GameObject("Menu Camera");
            go.transform.SetPositionAndRotation(new Vector3(-18f, 9f, -20f), Quaternion.Euler(22f, 42f, 0f));
            _menuCamera = go.AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = FogColor;
            go.AddComponent<AudioListener>();
        }

        void CreateNetworkManager()
        {
            // Configure while inactive so NetworkManager.Awake sees the prefab list.
            var go = new GameObject("NetworkManager");
            go.SetActive(false);
            _transport = go.AddComponent<UnityTransport>();
            _network = go.AddComponent<NetworkManager>();
            _network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = _transport,
                PlayerPrefab = GameConfig.PlayerPrefab,
                ConnectionApproval = true,
                // The world is built locally on every machine; there is no networked scene to load.
                EnableSceneManagement = false,
            };
            foreach (var prefab in new[] { GameConfig.PlayerPrefab, GameConfig.ZombiePrefab, GameConfig.DirectorPrefab })
                _network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            go.SetActive(true);

            // Every machine pools zombies; enough for a full live cap plus the corpses still falling.
            _network.PrefabHandler.AddHandler(GameConfig.ZombiePrefab, new ZombiePool(GameConfig.ZombiePrefab, ZombiePoolSize));

            _network.ConnectionApprovalCallback = ApproveConnection;
            _network.OnClientDisconnectCallback += OnClientDisconnect;
            _network.OnClientConnectedCallback += OnClientConnected;
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int slot = _network.ConnectedClientsList.Count;
            bool full = slot >= GameConfig.MaxPlayers;
            response.Approved = !full;
            response.Reason = full ? "The game is full (4 players)." : null;
            response.CreatePlayerObject = !full;
            response.Position = GameConfig.PlayerSpawn(slot);
            response.Rotation = Quaternion.identity;
        }

        // ---------------------------------------------------------------- menu actions

        void Host()
        {
            if (!TryParsePort(out ushort port)) return;
            SaveMenuFields();
            _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            _connecting = true;
            if (!_network.StartHost())
            {
                _connecting = false;
                _status = $"Couldn't host on port {port}. Is another copy of the game already hosting?";
                return;
            }
            var director = Instantiate(GameConfig.DirectorPrefab);
            director.GetComponent<NetworkObject>().Spawn();
        }

        void Join()
        {
            if (!TryParsePort(out ushort port)) return;
            SaveMenuFields();
            _transport.SetConnectionData(_address.Trim(), port);
            _connecting = true;
            _status = $"Connecting to {_address.Trim()}:{port}...";
            if (!_network.StartClient())
            {
                _connecting = false;
                _status = "Couldn't start connecting. Check the address.";
            }
        }

        bool TryParsePort(out ushort port)
        {
            if (ushort.TryParse(_port, out port) && port > 0) return true;
            _status = "The port must be a number like 7777.";
            return false;
        }

        void SaveMenuFields()
        {
            NetPlayer.LocalName = string.IsNullOrWhiteSpace(_name) ? "Survivor" : _name.Trim();
            PlayerPrefs.SetString(NameKey, NetPlayer.LocalName);
            PlayerPrefs.SetString(AddressKey, _address.Trim());
        }

        void OnClientConnected(ulong clientId)
        {
            if (clientId == _network.LocalClientId) _connecting = false;
        }

        void OnClientDisconnect(ulong clientId)
        {
            // On a client, any disconnect callback means we lost the host.
            if (_network.IsServer) return;
            string reason = _network.DisconnectReason;
            ReturnToMenu(string.IsNullOrEmpty(reason) ? "Disconnected from the host." : reason);
        }

        /// <summary>Leaves the session (hosting ends it for everyone) and reloads the scene to the menu.</summary>
        public static void ReturnToMenu(string message)
        {
            s_LastMessage = message;
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.Shutdown();
                Destroy(nm.gameObject);
            }
            NetPlayer.ClearRegistry();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        // ---------------------------------------------------------------- per frame + menu UI

        void Update()
        {
            if (_menuCamera != null && NetPlayer.Local != null) _menuCamera.gameObject.SetActive(false);
        }

        void OnGUI()
        {
            if (_network == null || (_network.IsListening && !_connecting)) return;
            EnsureStyles();
            float u = Screen.height / 1080f;
            float w = 560 * u, h = 640 * u;
            var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(area.x - 30 * u, area.y - 30 * u, area.width + 60 * u, area.height + 60 * u), Texture2D.whiteTexture);
            GUI.color = old;

            GUILayout.BeginArea(area);
            GUILayout.Label("ZOMBIES FPS", _title);
            GUILayout.Label("Co-op prototype · 1-4 players", _label);
            GUILayout.Space(24 * u);

            GUI.enabled = !_connecting;
            GUILayout.Label("Your name", _label);
            _name = GUILayout.TextField(_name ?? "", 20, _field);
            GUILayout.Space(16 * u);
            if (GUILayout.Button("Host game", _button)) Host();
            GUILayout.Label($"Friends join you at {LocalAddress()} port {_port} (same network, or forward the port).", _label);
            GUILayout.Space(16 * u);
            GUILayout.Label("Host address", _label);
            _address = GUILayout.TextField(_address ?? "", 64, _field);
            GUILayout.Label("Port", _label);
            _port = GUILayout.TextField(_port ?? "", 5, _field);
            if (GUILayout.Button("Join game", _button)) Join();
            GUI.enabled = true;

            if (_connecting && GUILayout.Button("Cancel", _button)) ReturnToMenu("Cancelled.");
            GUILayout.Space(12 * u);
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _label);
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            float u = Screen.height / 1080f;
            int size = Mathf.Max(12, Mathf.RoundToInt(22 * u));
            if (_label != null && _label.fontSize == size) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            _label.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _title = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(size * 2.6f), fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.9f, 0.25f, 0.2f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(size * 1.1f), fixedHeight = 52 * u };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = size, fixedHeight = 40 * u };
        }

        static string s_LocalAddress;

        static string LocalAddress()
        {
            if (s_LocalAddress != null) return s_LocalAddress;
            s_LocalAddress = "127.0.0.1";
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        s_LocalAddress = ip.ToString();
                        break;
                    }
                }
            }
            catch (SocketException)
            {
                // Keep the loopback fallback.
            }
            return s_LocalAddress;
        }
    }
}
