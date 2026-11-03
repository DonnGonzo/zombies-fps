using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Player;
using ZombiesFPS.UI;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Entry point. Builds the world every machine shares (lighting, the zone map + NavMesh, effects),
    /// shows the host/join menu, and owns the NetworkManager. Everything that changes during play lives
    /// on network objects: NetPlayer, NetZombie and RoundManager.
    /// </summary>
    public class PrototypeBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to use built-in defaults.")]
        public GunData gunData;
        public ZombieData zombieData;
        public RoundData roundData;
        public PriceTable priceTable;
        public UpgradeData upgradeData;
        public MissionData missionData;
        public ShopData shopData;
        public EquipmentData equipmentData;
        public PerkData perkData;
        [Tooltip("Runner, crawler, brute, spitter and boss. Any type missing here uses built-in values.")]
        public ZombieData[] zombieTypes;

        const string NameKey = "ZombiesFPS.PlayerName";
        const string AddressKey = "ZombiesFPS.JoinAddress";
        const int ZombiePoolSize = 64;

        /// <summary>Why the last session ended, shown on the menu after a scene reload.</summary>
        static string s_LastMessage;

        NetworkManager _network;
        UnityTransport _transport;
        Camera _menuCamera;
        string _name;
        string _address;
        string _port;
        string _status;
        MenuScreen _screen;
        int[] _camoPicks;
        int _titlePick;
        int _character;
        long _profileXp;
        int _profileLevel = 1;
        int _bestLevel = 1;
        int _prestige;
        bool _connecting;

        void Awake()
        {
            GameConfig.Gun = gunData != null ? gunData : ScriptableObject.CreateInstance<GunData>();
            GameConfig.Zombie = zombieData != null ? zombieData : ScriptableObject.CreateInstance<ZombieData>();
            GameConfig.ZombieTypes = BuildZombieTypes(GameConfig.Zombie, zombieTypes);
            GameConfig.Round = roundData != null ? roundData : ScriptableObject.CreateInstance<RoundData>();
            GameConfig.Prices = priceTable != null ? priceTable : PriceTable.CreateDefault(GameConfig.Gun);
            var guns = GameConfig.Prices.BuildCatalog(GameConfig.Gun, EasterEggData.MakeGodGun());
            GameConfig.Guns = guns;
            GameConfig.Upgrades = upgradeData != null ? upgradeData : ScriptableObject.CreateInstance<UpgradeData>();
            GameConfig.Missions = missionData != null ? missionData : ScriptableObject.CreateInstance<MissionData>();
            GameConfig.Shop = shopData != null ? shopData : ScriptableObject.CreateInstance<ShopData>();
            GameConfig.Equipment = equipmentData != null ? equipmentData : ScriptableObject.CreateInstance<EquipmentData>();
            GameConfig.Perks = perkData != null ? perkData : ScriptableObject.CreateInstance<PerkData>();
            GameConfig.PlayerPrefab = Resources.Load<GameObject>("Net/NetPlayer");
            GameConfig.ZombiePrefab = Resources.Load<GameObject>("Net/NetZombie");
            GameConfig.DirectorPrefab = Resources.Load<GameObject>("Net/NetDirector");

            SetUpEnvironment();
            GameConfig.Map = ZoneMap.Build(GameConfig.Prices);
            var area = ZoneMap.Bounds;
            GraphicsSetup.BakeReflections(new Bounds(new Vector3(area.center.x, 4f, area.center.y), new Vector3(area.width, 12f, area.height)));
            GameConfig.PlayerStart = GameConfig.Map.PlayerStart;
            new GameObject("FX").AddComponent<FxSystem>();
            new GameObject("Music").AddComponent<MusicPlayer>();
            BuildMenuCamera();
            CreateNetworkManager();

            _name = PlayerPrefs.GetString(NameKey, "Survivor");
            _address = PlayerPrefs.GetString(AddressKey, "127.0.0.1");
            _port = GameConfig.DefaultPort.ToString();
            _status = s_LastMessage;
            s_LastMessage = null;
            LoadCosmetics();
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
            GraphicsSetup.SetUpEnvironment();
            SurfaceTextures.Prewarm();
        }

        void BuildMenuCamera()
        {
            var go = new GameObject("Menu Camera");
            go.transform.SetPositionAndRotation(new Vector3(-9f, 8f, -10f), Quaternion.Euler(28f, 38f, 0f));
            _menuCamera = go.AddComponent<Camera>();
            GraphicsSetup.ConfigureCamera(_menuCamera);
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
            SaveCosmetics();
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
            Weapons.Throwable.ClearAll();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        // ---------------------------------------------------------------- per frame + menu UI

        void Update()
        {
            if (_menuCamera != null && NetPlayer.Local != null) _menuCamera.gameObject.SetActive(false);
        }

        enum MenuScreen { Main, Loadout, Prestige, Settings }

        void OnGUI()
        {
            if (_network == null || (_network.IsListening && !_connecting)) return;
            float u = Ui.MenuU;
            Ui.EnsureStyles(u);
            float sw = Screen.width, sh = Screen.height;

            // Backdrop: darken the map behind, with a red glow behind the title.
            Ui.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.55f));
            Ui.GlowSpot(new Rect(sw * 0.5f - 700 * u, -300 * u, 1400 * u, 700 * u), new Color(0.8f, 0.08f, 0.05f, 0.35f));
            Ui.Label(new Rect(0, 46 * u, sw, 110 * u), "ZOMBIES FPS", Ui.Title, Ui.Blood, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(0, 140 * u, sw, 30 * u), "ASHGROVE FACILITY  ·  CO-OP SURVIVAL  ·  1-4 PLAYERS", Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);

            var panel = new Rect((sw - 1180 * u) / 2f, 200 * u, 1180 * u, Mathf.Min(sh - 240 * u, 820 * u));
            switch (_screen)
            {
                case MenuScreen.Settings:
                    if (!SettingsMenu.Draw(panel, u)) _screen = MenuScreen.Main;
                    return;
                case MenuScreen.Loadout:
                    DrawLoadout(panel, u);
                    return;
                case MenuScreen.Prestige:
                    DrawPrestige(panel, u);
                    return;
            }
            DrawMain(panel, u);
        }

        void DrawMain(Rect panel, float u)
        {
            float gap = 24 * u;
            var left = new Rect(panel.x, panel.y, panel.width * 0.5f - gap / 2f, panel.height);
            var right = new Rect(left.xMax + gap, panel.y, panel.width * 0.5f - gap / 2f, panel.height);

            // Profile: name, survivor, level and XP, prestige.
            Ui.PanelBox(left, Ui.Blood, u);
            float pad = 26 * u, x = left.x + pad, w = left.width - pad * 2, y = left.y + pad;
            GUI.enabled = !_connecting;
            Ui.Label(new Rect(x, y, w, 30 * u), "YOUR SURVIVOR", Ui.Small, Ui.Muted, TextAnchor.UpperLeft);
            y += 34 * u;
            _name = GUI.TextField(new Rect(x, y, w, 42 * u), _name ?? "", 20, Ui.Field);
            y += 56 * u;

            var c = Characters.At(_character);
            var card = new Rect(x, y, w, 118 * u);
            Ui.RoundRect(card, new Color(1f, 1f, 1f, 0.04f));
            Ui.Fill(new Rect(card.x, card.y + 12 * u, 4 * u, card.height - 24 * u), c.color);
            if (Ui.Button(new Rect(card.x + 16 * u, card.y + 35 * u, 48 * u, 48 * u), "<")) _character = (_character + Characters.Count - 1) % Characters.Count;
            if (Ui.Button(new Rect(card.xMax - 64 * u, card.y + 35 * u, 48 * u, 48 * u), ">")) _character = (_character + 1) % Characters.Count;
            Ui.Label(new Rect(card.x + 80 * u, card.y + 12 * u, card.width - 160 * u, 34 * u), c.name, Ui.Header, c.color, TextAnchor.UpperLeft);
            Ui.Label(new Rect(card.x + 80 * u + Ui.Width(c.name, Ui.Header) + 12 * u, card.y + 20 * u, 300 * u, 28 * u), c.role.ToUpperInvariant(), Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            var blurb = new GUIStyle(Ui.Small) { wordWrap = true };
            Ui.Label(new Rect(card.x + 80 * u, card.y + 54 * u, card.width - 160 * u, 60 * u), c.blurb, blurb, Ui.Text, TextAnchor.UpperLeft);
            y += card.height + 20 * u;

            DrawProfileLevel(new Rect(x, y, w, 110 * u), u);
            y += 124 * u;

            if (Ui.Button(new Rect(x, y, w, 50 * u), "Camos and title")) _screen = MenuScreen.Loadout;
            y += 58 * u;
            var shop = GameConfig.Shop;
            if (shop != null && shop.saveBetweenRuns && _prestige < shop.MaxPrestige)
            {
                bool can = shop.CanPrestige(_profileLevel, _prestige);
                string label = can ? $"Prestige to P{_prestige + 1}  ·  {shop.PrestigeName(_prestige + 1)}" : $"Prestige unlocks at level {shop.prestigeLevel}";
                GUI.enabled = can && !_connecting;
                if (Ui.Button(new Rect(x, y, w, 50 * u), label, accent: can)) _screen = MenuScreen.Prestige;
                GUI.enabled = !_connecting;
                y += 58 * u;
            }
            if (Ui.Button(new Rect(x, y, w, 50 * u), "Settings")) _screen = MenuScreen.Settings;

            // Play: host, or join a friend.
            Ui.PanelBox(right, Ui.Amber, u);
            x = right.x + pad;
            w = right.width - pad * 2;
            y = right.y + pad;
            Ui.Label(new Rect(x, y, w, 30 * u), "PLAY", Ui.Small, Ui.Muted, TextAnchor.UpperLeft);
            y += 34 * u;
            if (Ui.Button(new Rect(x, y, w, 64 * u), "HOST GAME", accent: true, preferred: true)) Host();
            y += 72 * u;
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };
            Ui.Label(new Rect(x, y, w, 52 * u), $"Play solo, or have friends join you at {LocalAddress()} port {_port} (same network, or forward the port).", wrap, Ui.Muted, TextAnchor.UpperLeft);
            y += 74 * u;
            Ui.Fill(new Rect(x, y, w, 1), Ui.Line);
            y += 18 * u;
            Ui.Label(new Rect(x, y, w, 30 * u), "JOIN A FRIEND", Ui.Small, Ui.Muted, TextAnchor.UpperLeft);
            y += 34 * u;
            Ui.Label(new Rect(x, y, w, 30 * u), "Host address", Ui.Small, Ui.Text, TextAnchor.UpperLeft);
            Ui.Label(new Rect(x + w - 140 * u, y, 140 * u, 30 * u), "Port", Ui.Small, Ui.Text, TextAnchor.UpperLeft);
            y += 30 * u;
            _address = GUI.TextField(new Rect(x, y, w - 156 * u, 42 * u), _address ?? "", 64, Ui.Field);
            _port = GUI.TextField(new Rect(x + w - 140 * u, y, 140 * u, 42 * u), _port ?? "", 5, Ui.Field);
            y += 54 * u;
            if (Ui.Button(new Rect(x, y, w, 56 * u), "JOIN GAME")) Join();
            y += 70 * u;
            GUI.enabled = true;

            if (_connecting && Ui.Button(new Rect(x, y, w, 48 * u), "Cancel", cancel: true, preferred: true)) ReturnToMenu("Cancelled.");
            if (_connecting) y += 58 * u;
            if (!string.IsNullOrEmpty(_status))
                Ui.Label(new Rect(x, y, w, 80 * u), _status, wrap, Ui.Amber, TextAnchor.UpperLeft);

            if (Ui.Button(new Rect(right.xMax - pad - 140 * u, right.yMax - pad - 46 * u, 140 * u, 46 * u), "Quit"))
                Application.Quit();
        }

        /// <summary>Level, prestige emblem and the XP still needed for the next level.</summary>
        void DrawProfileLevel(Rect r, float u)
        {
            var shop = GameConfig.Shop;
            Ui.RoundRect(r, new Color(1f, 1f, 1f, 0.04f));
            float x = r.x + 16 * u, w = r.width - 32 * u;
            if (_prestige > 0 && shop != null)
            {
                var col = ShopData.PrestigeColor(_prestige);
                var emblem = new Rect(x, r.y + 14 * u, 70 * u, 70 * u);
                Ui.RoundRect(emblem, Ui.A(col, 0.2f));
                Ui.Label(emblem, "P" + _prestige, Ui.Header, col, TextAnchor.MiddleCenter);
                Ui.Label(new Rect(emblem.x, emblem.yMax, emblem.width, 22 * u), shop.PrestigeName(_prestige).ToUpperInvariant(), Ui.Tiny, col, TextAnchor.UpperCenter);
                x += 86 * u;
                w -= 86 * u;
            }
            Ui.Label(new Rect(x, r.y + 12 * u, w, 40 * u), $"LEVEL {_profileLevel}", Ui.Header, Ui.Text, TextAnchor.UpperLeft);
            if (shop == null) return;
            long into = shop.XpIntoLevel(_profileXp);
            bool max = _profileLevel >= shop.maxLevel;
            long need = max ? 0 : shop.XpToNext(_profileLevel) - into;
            string next = !shop.saveBetweenRuns ? "Levels reset every run (saving is off)"
                : max ? (shop.CanPrestige(_profileLevel, _prestige) ? "Max level. Prestige to climb again." : "Max level")
                : $"{need:N0} XP to level {_profileLevel + 1}";
            Ui.Label(new Rect(x, r.y + 18 * u, w, 30 * u), next, Ui.Small, Ui.Amber, TextAnchor.UpperRight);
            Ui.Bar(new Rect(x, r.y + 60 * u, w, 12 * u), max ? 1f : (float)into / shop.XpToNext(_profileLevel), Ui.Amber);
            Ui.Label(new Rect(x, r.y + 78 * u, w, 26 * u), max ? $"{_profileXp:N0} XP total" : $"{into:N0} / {shop.XpToNext(_profileLevel):N0} XP", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        // ---------------------------------------------------------------- prestige (menu only)

        void DrawPrestige(Rect panel, float u)
        {
            var shop = GameConfig.Shop;
            var box = new Rect(panel.center.x - 380 * u, panel.y + 40 * u, 760 * u, 520 * u);
            Ui.PanelBox(box, ShopData.PrestigeColor(_prestige + 1), u);
            if (shop == null || !shop.CanPrestige(_profileLevel, _prestige))
            {
                _screen = MenuScreen.Main;
                return;
            }
            float pad = 34 * u, x = box.x + pad, w = box.width - pad * 2, y = box.y + pad;
            var col = ShopData.PrestigeColor(_prestige + 1);
            Ui.Label(new Rect(x, y, w, 50 * u), $"PRESTIGE {_prestige + 1}: {shop.PrestigeName(_prestige + 1).ToUpperInvariant()}", Ui.Header, col, TextAnchor.UpperCenter);
            y += 70 * u;
            var wrap = new GUIStyle(Ui.Body) { wordWrap = true };
            string camo = "";
            for (int i = 0; i < shop.CamoCount; i++)
                if (shop.CamoAt(i).unlockPrestige == _prestige + 1) camo = $"\n•  You unlock the {shop.CamoAt(i).name} camo.";
            Ui.Label(new Rect(x, y, w, 300 * u),
                $"•  Your level goes back to 1 and climbs again from there.\n" +
                $"•  Attachments in the spawn shop lock again until you re-level.\n" +
                $"•  You keep every camo and title you've unlocked.\n" +
                $"•  Your name shows the P{_prestige + 1} {shop.PrestigeName(_prestige + 1)} emblem to your squad." + camo +
                $"\n\nThere are {shop.MaxPrestige} prestige ranks. This can't be undone.", wrap, Ui.Text, TextAnchor.UpperLeft);
            float bw = (w - 20 * u) / 2f;
            if (Ui.Button(new Rect(x, box.yMax - pad - 56 * u, bw, 56 * u), "Cancel", cancel: true, preferred: true)) _screen = MenuScreen.Main;
            if (Ui.Button(new Rect(x + bw + 20 * u, box.yMax - pad - 56 * u, bw, 56 * u), "PRESTIGE", accent: true))
            {
                ProfileSave.SaveBestLevel(_profileLevel);
                ProfileSave.SaveXp(0);
                ProfileSave.SavePrestige(_prestige + 1);
                LoadCosmetics();
                _status = $"Welcome to Prestige {_prestige}: {shop.PrestigeName(_prestige)}.";
                _screen = MenuScreen.Main;
            }
        }

        // ---------------------------------------------------------------- cosmetics (menu only)

        /// <summary>Camos and title come from the save; they're picked here and can't change once a match starts.</summary>
        void LoadCosmetics()
        {
            var shop = GameConfig.Shop;
            var guns = GameConfig.Guns ?? new GunData[0];
            bool saved = shop != null && shop.saveBetweenRuns;
            _profileXp = saved ? ProfileSave.LoadXp() : 0;
            _profileLevel = shop != null ? shop.LevelFor(_profileXp) : 1;
            if (saved) ProfileSave.SaveBestLevel(_profileLevel);
            _bestLevel = saved ? Mathf.Max(_profileLevel, ProfileSave.LoadBestLevel()) : _profileLevel;
            _prestige = saved && shop != null ? Mathf.Min(ProfileSave.LoadPrestige(), shop.MaxPrestige) : 0;
            _camoPicks = new int[guns.Length];
            for (int i = 0; i < guns.Length; i++)
            {
                int camo = ProfileSave.LoadCamo(guns[i].name);
                _camoPicks[i] = shop != null && shop.IsCamoUnlocked(camo, _bestLevel, _prestige) ? camo : 0;
            }
            int title = ProfileSave.LoadTitle();
            _titlePick = shop != null && shop.IsTitleUnlocked(title, _bestLevel) ? title : 0;
            _character = Characters.LoadPick();
            PublishCosmetics();
        }

        void SaveCosmetics()
        {
            var guns = GameConfig.Guns ?? new GunData[0];
            for (int i = 0; i < guns.Length && i < _camoPicks.Length; i++) ProfileSave.SaveCamo(guns[i].name, _camoPicks[i]);
            ProfileSave.SaveTitle(_titlePick);
            Characters.SavePick(_character);
            PlayerPrefs.Save();
            PublishCosmetics();
        }

        void PublishCosmetics()
        {
            ulong picks = 0;
            for (int i = 0; i < _camoPicks.Length; i++) picks = ShopData.WithCamo(picks, i, _camoPicks[i]);
            NetPlayer.LocalCamoPicks = picks;
            NetPlayer.LocalTitle = _titlePick;
            NetPlayer.LocalCharacter = _character;
        }

        /// <summary>The next unlocked entry in a direction, wrapping round; entry 0 is always unlocked.</summary>
        static int Cycle(int current, int dir, int count, System.Func<int, bool> unlocked)
        {
            for (int step = 1; step <= count; step++)
            {
                int i = ((current + dir * step) % count + count) % count;
                if (unlocked(i)) return i;
            }
            return current;
        }

        void DrawLoadout(Rect panel, float u)
        {
            var shop = GameConfig.Shop;
            var guns = GameConfig.Guns ?? new GunData[0];
            Ui.PanelBox(panel, Ui.Violet, u);
            float pad = 28 * u, x = panel.x + pad, w = panel.width - pad * 2, y = panel.y + pad;
            Ui.Label(new Rect(x, y, w, 44 * u), "CAMOS AND TITLE", Ui.Header, Ui.Text, TextAnchor.UpperLeft);
            Ui.Label(new Rect(x, y + 8 * u, w, 30 * u), "Picked here before a match. They can't change during one.", Ui.Small, Ui.Muted, TextAnchor.UpperRight);
            y += 60 * u;
            if (shop == null) return;

            int unlockedTitles = 0, unlockedCamos = 0;
            for (int i = 0; i < shop.TitleCount; i++) if (shop.IsTitleUnlocked(i, _bestLevel)) unlockedTitles++;
            for (int i = 0; i < shop.CamoCount; i++) if (shop.IsCamoUnlocked(i, _bestLevel, _prestige)) unlockedCamos++;

            float rowH = 54 * u;
            DrawCycler(new Rect(x, y, w, rowH), "Title", shop.TitleAt(_titlePick)?.name, Ui.Text, $"{unlockedTitles}/{shop.TitleCount} unlocked", u,
                dir => _titlePick = Cycle(_titlePick, dir, shop.TitleCount, i => shop.IsTitleUnlocked(i, _bestLevel)));
            y += rowH + 14 * u;
            Ui.Label(new Rect(x, y, w, 28 * u), $"GUN CAMOS  ·  {unlockedCamos}/{shop.CamoCount} unlocked", Ui.Small, Ui.Muted, TextAnchor.UpperLeft);
            y += 32 * u;
            float colW = (w - 20 * u) / 2f;
            for (int g = 0; g < guns.Length && g < ShopData.MaxCamoGuns; g++)
            {
                int gun = g;
                var camo = shop.CamoAt(_camoPicks[gun]);
                var r = new Rect(x + (g % 2) * (colW + 20 * u), y + (g / 2) * (rowH + 6 * u), colW, rowH);
                Color swatch = _camoPicks[gun] == 0 ? guns[gun].accent : camo.color;
                DrawCycler(r, guns[gun].displayName, camo?.name, swatch, null, u,
                    dir => _camoPicks[gun] = Cycle(_camoPicks[gun], dir, shop.CamoCount, i => shop.IsCamoUnlocked(i, _bestLevel, _prestige)));
            }
            y += ((guns.Length + 1) / 2) * (rowH + 6 * u) + 10 * u;

            // What's still locked, so there's something to aim for.
            var next = new System.Text.StringBuilder();
            for (int i = 0; i < shop.CamoCount; i++)
            {
                var cm = shop.CamoAt(i);
                if (shop.IsCamoUnlocked(i, _bestLevel, _prestige)) continue;
                if (next.Length > 0) next.Append("  ·  ");
                next.Append(cm.unlockPrestige > 0 ? $"{cm.name} (P{cm.unlockPrestige})" : $"{cm.name} (Lv {cm.unlockLevel})");
            }
            if (next.Length > 0)
            {
                var wrap = new GUIStyle(Ui.Tiny) { wordWrap = true };
                Ui.Label(new Rect(x, y, w, 60 * u), "Still locked: " + next, wrap, Ui.Muted, TextAnchor.UpperLeft);
            }

            if (Ui.Button(new Rect(x, panel.yMax - pad - 52 * u, 240 * u, 52 * u), "Done", accent: true, cancel: true))
            {
                SaveCosmetics();
                _screen = MenuScreen.Main;
            }
        }

        static void DrawCycler(Rect r, string label, string value, Color swatch, string note, float u, System.Action<int> step)
        {
            Ui.RoundRect(r, new Color(1f, 1f, 1f, 0.04f), small: true);
            Ui.Fill(new Rect(r.x + 10 * u, r.y + 12 * u, 6 * u, r.height - 24 * u), swatch);
            Ui.Label(new Rect(r.x + 28 * u, r.y, r.width * 0.45f, r.height), label, Ui.Small, Ui.Muted, TextAnchor.MiddleLeft);
            float bw = 44 * u;
            if (Ui.Button(new Rect(r.xMax - bw * 2 - 14 * u, r.y + (r.height - bw) / 2f, bw, bw), "<")) step(-1);
            if (Ui.Button(new Rect(r.xMax - bw - 8 * u, r.y + (r.height - bw) / 2f, bw, bw), ">")) step(1);
            float vx = r.x + r.width * 0.45f;
            Ui.Label(new Rect(vx, r.y, r.xMax - bw * 2 - 24 * u - vx, r.height), value, Ui.Bold, Ui.Text, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(note))
                Ui.Label(new Rect(r.x + 28 * u + Ui.Width(label, Ui.Small) + 12 * u, r.y, 300 * u, r.height), note, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
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

        /// <summary>One data asset per ZombieKind, walker first; kinds without an asset get the built-in values.</summary>
        static ZombieData[] BuildZombieTypes(ZombieData walker, ZombieData[] assets)
        {
            var types = new ZombieData[ZombieData.KindCount];
            types[0] = walker;
            if (assets != null)
                foreach (var asset in assets)
                    if (asset != null && asset.kind != ZombieKind.Walker && types[(int)asset.kind] == null) types[(int)asset.kind] = asset;
            for (int i = 1; i < types.Length; i++)
                if (types[i] == null) types[i] = ZombieData.CreateDefault((ZombieKind)i);
            return types;
        }
    }
}
