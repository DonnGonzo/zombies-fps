using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Player;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The in-game HUD, drawn with IMGUI in the shared Ui style: points and level, round and banners, health and
    /// armour, perks and power-ups, ammo and throwables, missions, prompts, tips, subtitles, the pause menu
    /// and the downed and game-over screens. It also tints the world during blackout rounds.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        NetPlayer _player;
        HitscanGun _gun;
        AudioSource _audio;

        float _hitTime = -100f;
        bool _hitKilled, _hitHead;
        bool _showHelp = true;

        // Banners and pop-ups, driven by watching state.
        GamePhase _lastPhase = GamePhase.Intermission;
        int _lastRound;
        string _banner, _bannerSub;
        Color _bannerColor = Ui.Blood;
        float _bannerTime = -100f;
        long _lastPoints = -1;
        long _pointsGain;
        float _pointsGainTime = -100f;
        float _lastPickTime = -100f;
        float _lastLevelUpTime = -100f;
        float _lastPerkTime = -100f;

        // Pause menu.
        enum PauseScreen { Main, Settings }
        PauseScreen _pause;
        bool _wasUnlocked;
        bool _consoleWasOpen;
        bool _wasGameOver;
        bool _mapOpen;

        // Blackout atmosphere.
        float _blackout;
        float _staminaShow;
        bool _haveBaseline;
        Color _baseAmbient, _baseFog;
        float _baseFogDensity, _baseMoon;
        Light _moon;

        public void Init(NetPlayer player)
        {
            _player = player;
            _gun = player.Gun;
            _gun.Hit += OnHit;
            _audio = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
            _audio.playOnAwake = false;
            RoundManager.BossDefeated += OnBossDefeated;
            MissionBoard.MissionCompleted += OnMissionCompleted;
            StoryDirector.StoryCompleted += OnStoryCompleted;
            MissionBoard.MissionFailed += OnMissionFailed;
            BuildManager.PartFound += OnPartFound;
            BuildManager.ItemBuilt += OnItemBuilt;
            BuildManager.RobotBroke += OnRobotBroke;
            EasterEgg.StepStarted += OnEggStarted;
            EasterEgg.StepCompleted += OnEggCompleted;
            // The pause menu has its own Resume button, so a click there mustn't drop straight back into the game.
            PlayerLook.ClickToResume = false;
            gameObject.AddComponent<StoryDirector>().Init(player);
            Tips.Offer("move", $"Move with {Keys(GameAction.MoveForward, GameAction.MoveLeft, GameAction.MoveBack, GameAction.MoveRight)}, sprint with {K(GameAction.Sprint)}. Kills earn points: spend them on doors, wall guns, perks and more.");
        }

        void OnDestroy()
        {
            if (_gun != null) _gun.Hit -= OnHit;
            RoundManager.BossDefeated -= OnBossDefeated;
            MissionBoard.MissionCompleted -= OnMissionCompleted;
            StoryDirector.StoryCompleted -= OnStoryCompleted;
            MissionBoard.MissionFailed -= OnMissionFailed;
            BuildManager.PartFound -= OnPartFound;
            BuildManager.ItemBuilt -= OnItemBuilt;
            BuildManager.RobotBroke -= OnRobotBroke;
            EasterEgg.StepStarted -= OnEggStarted;
            EasterEgg.StepCompleted -= OnEggCompleted;
            PlayerLook.ClickToResume = true;
            SettingsMenu.Close();
            DebugConsole.Close();
            Subtitles.Clear();
            RestoreAtmosphere();
        }

        static string K(GameAction a) => Bindings.Name(a);
        static string Keys(params GameAction[] actions)
        {
            var names = new string[actions.Length];
            for (int i = 0; i < actions.Length; i++) names[i] = Bindings.Name(actions[i]);
            return string.Join(" ", names);
        }

        // ---------------------------------------------------------------- events

        void OnMissionCompleted(int id)
        {
            var def = GameConfig.Missions != null ? GameConfig.Missions.At(id) : null;
            var board = MissionBoard.Instance;
            if (def == null || board == null) return;
            ShowBanner("MISSION COMPLETE", $"{def.title}: {RewardText(def, board.SetRound.Value)} for everyone", Ui.Sky);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        void OnMissionFailed(int id)
        {
            var def = GameConfig.Missions != null ? GameConfig.Missions.At(id) : null;
            if (def == null) return;
            ShowBanner("MISSION FAILED", def.objective == MissionObjective.ProtectSurvivor ? $"{def.title}: the survivor didn't make it." : def.title, Ui.Bad);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.DryFire, 1f);
        }

        static string NameOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All)
                if (p.OwnerClientId == clientId) return p == NetPlayer.Local ? "You" : p.DisplayName;
            return "Someone";
        }

        void OnPartFound(int part, ulong finder, int found)
        {
            int item = BuildManager.ItemOf(part);
            string sub = found >= ZoneMap.PartsFor(item)
                ? $"All parts found. Build the {BuildManager.BuildableNames[item]} at a build station (Garage Roof or Greenhouse Roof)."
                : $"{BuildManager.BuildableNames[item]}: {found}/{ZoneMap.PartsFor(item)} parts";
            ShowBanner($"{NameOf(finder).ToUpperInvariant()} FOUND THE {BuildManager.PartNames[part].ToUpperInvariant()}", sub, BuildManager.BuildableColors[item]);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.Purchase, 0.9f);
        }

        void OnItemBuilt(int item, ulong builder)
        {
            ShowBanner($"{BuildManager.BuildableNames[item].ToUpperInvariant()} BUILT", item == BuildManager.Bot
                ? $"Switched on by {NameOf(builder)}. It helps for two minutes, then needs {BuildManager.RobotOfflineRounds} rounds to come back online."
                : $"{NameOf(builder)} built it. Anyone can take one from a build station.", BuildManager.BuildableColors[item]);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        void OnRobotBroke(int readyRound)
        {
            ShowBanner("BUDDY BOT OFFLINE", $"Back online at round {readyRound}", BuildManager.BuildableColors[BuildManager.Bot]);
        }

        static string RewardText(MissionDef def, int round)
        {
            string bonus = def.bonus switch
            {
                MissionBonus.MaxAmmo => " and full ammo",
                MissionBonus.PowerUpReroll => " and a power-up reroll",
                MissionBonus.RareWeapon => " and a prototype weapon",
                _ => "",
            };
            return $"+{def.PointsFor(round):N0} pts{bonus}";
        }

        void OnBossDefeated(long bonus)
        {
            ShowBanner($"{GameConfig.ZombieType(ZombieKind.Boss).displayName.ToUpperInvariant()} DOWN", $"+{bonus:N0} pts for everyone", Ui.Amber);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        void OnHit(bool killed, bool headshot)
        {
            _hitTime = Time.time;
            _hitKilled = killed;
            _hitHead = headshot;
        }

        void OnStoryCompleted() =>
            ShowBanner("STORY COMPLETE", "The Lantern is dead. Keep going: the Trader's bounties are on the board, and round 500 is the record.", Ui.Amber);

        void OnEggStarted(int step)
        {
            var def = EasterEggData.At(step);
            if (def == null) return;
            ShowBanner(def.title.ToUpperInvariant(), $"The doors are sealed. Kill {def.zombies} of Doctor Morrow's runners. They don't count toward the round.", EasterEggData.ScientistColor);
        }

        void OnEggCompleted(int step)
        {
            var def = EasterEggData.At(step);
            if (def == null) return;
            ShowBanner("THE LANTERN IS AWAKE", $"{def.title}: step {step + 1} of {EasterEggData.Steps.Length} done. {EasterEgg.StepReward:N0} points for everyone.", EasterEggData.ScientistColor);
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        void ShowBanner(string title, string sub, Color color)
        {
            _banner = title;
            _bannerSub = sub;
            _bannerColor = color;
            _bannerTime = Time.time;
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (_player == null)
            {
                Destroy(gameObject);
                return;
            }
            var rounds = RoundManager.Instance;

            if (!SettingsMenu.IsCapturing && GameInput.DebugPressed(DebugKey.Console)) DebugConsole.Toggle();
            else if (DebugConsole.IsOpen && GameInput.PausePressed) DebugConsole.Toggle();
            if (!DebugConsole.IsOpen && GameInput.DebugPressed(DebugKey.ToggleHelp)) _showHelp = !_showHelp;
            if (!DebugConsole.IsOpen && !SettingsMenu.IsCapturing && _player.Look.CursorLocked && GameInput.MapPressed) _mapOpen = !_mapOpen;

            bool gameOver = rounds != null && rounds.IsGameOver;
            if (gameOver) _mapOpen = false;
            if (_wasGameOver && !gameOver) PlayerLook.SetCursorLocked(true);
            _wasGameOver = gameOver;
            UpdatePause(rounds);
            WatchRounds();
            WatchPoints();
            WatchPowerUps();
            OfferTips(rounds);
            Tips.Tick();
            UpdateAtmosphere(rounds);
        }

        void UpdatePause(RoundManager rounds)
        {
            bool unlocked = !_player.Look.CursorLocked;
            bool console = DebugConsole.IsOpen || _consoleWasOpen;
            _consoleWasOpen = DebugConsole.IsOpen;
            if (!unlocked)
            {
                if (_pause == PauseScreen.Settings) SettingsMenu.Close();
                _pause = PauseScreen.Main;
                // Esc closes the map first, then pauses.
                if (!console && GameInput.PausePressed)
                {
                    if (_mapOpen) _mapOpen = false;
                    else PlayerLook.SetCursorLocked(false);
                }
            }
            else if (_wasUnlocked && !console && GameInput.PausePressed && !SettingsMenu.IsCapturing && !(rounds != null && rounds.IsGameOver))
            {
                // Esc again: back out of settings, or resume.
                if (_pause == PauseScreen.Settings)
                {
                    SettingsMenu.Close();
                    _pause = PauseScreen.Main;
                }
                else Resume();
            }
            _wasUnlocked = !_player.Look.CursorLocked;
        }

        void Resume()
        {
            SettingsMenu.Close();
            _pause = PauseScreen.Main;
            PlayerLook.SetCursorLocked(true);
        }

        void WatchPowerUps()
        {
            if (_player.LevelUpTime > _lastLevelUpTime)
            {
                _lastLevelUpTime = _player.LevelUpTime;
                ShowBanner($"LEVEL {_player.LevelUpLevel}", UnlockLine(_player.LevelUpLevel), Ui.Amber);
                if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundStart, 0.7f);
                var shop = GameConfig.Shop;
                if (shop != null && _player.LevelUpLevel >= shop.prestigeLevel)
                    Tips.Offer("prestige", $"Level {shop.prestigeLevel}: you can prestige from the main menu. Your level resets, you keep your camos, and earn a prestige rank and new camos.");
                else Tips.Offer("level", "Levels carry over between games. Each one unlocks attachments in the spawn shop, plus camos and titles in the main menu.");
            }
            if (_player.LastPerkTime > _lastPerkTime)
            {
                _lastPerkTime = _player.LastPerkTime;
                var perk = GameConfig.Perks != null ? GameConfig.Perks.At(_player.LastPerk) : null;
                if (perk != null) ShowBanner(perk.name.ToUpperInvariant(), perk.description, perk.color);
            }
            if (_player.LastPickTime <= _lastPickTime) return;
            _lastPickTime = _player.LastPickTime;
            ShowBanner("POWER-UP", _player.LastPickText, Ui.Violet);
        }

        void WatchRounds()
        {
            var rounds = RoundManager.Instance;
            if (rounds == null) return;
            var phase = rounds.Phase.Value;
            int round = rounds.Round.Value;
            if (phase == GamePhase.RoundActive && (round != _lastRound || _lastPhase != GamePhase.RoundActive))
            {
                string boss = GameConfig.ZombieType(ZombieKind.Boss).displayName;
                bool blackout = GameConfig.Round.IsBlackoutRound(round);
                string sub = round == 1 ? "Here they come."
                    : GameConfig.Round.IsBossRound(round) ? $"Boss round: shoot the glowing core on the {boss}'s back."
                    : blackout ? $"BLACKOUT: the lights are out and runners are everywhere. Points are worth {GameConfig.Round.blackoutPointsMultiplier:0}x."
                    : NewTypeLine(round);
                ShowBanner($"ROUND {round}", sub, blackout ? Ui.Violet : Ui.Blood);
                _audio.PlayOneShot(ProceduralAudio.RoundStart, 0.9f);
            }
            else if (phase == GamePhase.Intermission && _lastPhase == GamePhase.RoundActive)
            {
                ShowBanner($"ROUND {round} CLEARED", $"+{GameConfig.Round.RoundBonus(round):N0} pts for everyone", Ui.Good);
                _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.8f);
            }
            _lastPhase = phase;
            _lastRound = round;
        }

        /// <summary>A heads-up on the round a zombie type first appears.</summary>
        static string NewTypeLine(int round)
        {
            if (GameConfig.ZombieTypes == null) return null;
            foreach (var type in GameConfig.ZombieTypes)
                if (type != null && !type.IsBoss && type.kind != ZombieKind.Walker && type.firstRound == round)
                    return type.kind switch
                    {
                        ZombieKind.Runner => "Runners: fast, but they go down easy.",
                        ZombieKind.Crawler => "Crawlers: low to the ground, and they go for your legs.",
                        ZombieKind.Brute => "Brutes: armoured bodies. Aim for the head.",
                        ZombieKind.Spitter => "Spitters: they lob acid where you stand. Keep moving.",
                        _ => $"{type.displayName}s are coming.",
                    };
            return null;
        }

        static string UnlockLine(int level)
        {
            var shop = GameConfig.Shop;
            if (shop == null) return null;
            var names = new List<string>();
            for (int i = 0; i < shop.Count; i++)
                if (shop.At(i).unlockLevel == level) names.Add(shop.At(i).name);
            return names.Count > 0 ? "Unlocked in the spawn shop: " + string.Join(", ", names) : null;
        }

        void WatchPoints()
        {
            long points = _player.Points.Value;
            if (_lastPoints >= 0 && points > _lastPoints)
            {
                // Gains close together add up into one pop-up.
                if (Time.time - _pointsGainTime > 0.8f) _pointsGain = 0;
                _pointsGain += points - _lastPoints;
                _pointsGainTime = Time.time;
            }
            _lastPoints = points;
        }

        /// <summary>First-time hints, each offered once the moment it applies.</summary>
        void OfferTips(RoundManager rounds)
        {
            if (rounds == null || rounds.IsGameOver) return;
            string interact = K(GameAction.Interact);
            var b = _player.FocusedBuyable;
            if (b != null)
            {
                switch (b.Kind)
                {
                    case BuyableKind.Door: Tips.Offer("door", $"Doors open new areas, with more wall guns, machines and windows for zombies. Press {interact} to buy one."); break;
                    case BuyableKind.WallGun: Tips.Offer("wallgun", $"Wall guns: press {interact} to buy. Buying the gun you already hold refills its ammo for less."); break;
                    case BuyableKind.UpgradeMachine: Tips.Offer("machine", "The upgrade machine makes the gun in your hands much stronger. Each tier costs more and adds a new effect."); break;
                    case BuyableKind.Shop: Tips.Offer("shop", "The spawn shop sells attachments for the gun in hand. Your level unlocks them; points buy them, every game."); break;
                    case BuyableKind.Perk: Tips.Offer("perk", "Perk machines sell lasting upgrades. You keep them until you go down, then they're gone."); break;
                    case BuyableKind.Armor: Tips.Offer("armor", "Armour plates soak up hits before your health. You can carry three and they get pricier each round."); break;
                    case BuyableKind.Reroll: Tips.Offer("reroll", "The shrine sells redraws for power-up cards. Press 4 while picking a power-up to redraw."); break;
                    case BuyableKind.Part: Tips.Offer("part", $"A build part! Press {interact} to pick it up. Find all the parts of the Scrap Cannon, Aegis Shield (four parts) or Buddy Bot, then build it at a build station. Parts are somewhere different every game."); break;
                    case BuyableKind.Workbench: Tips.Offer("bench", "A build station. Build the Scrap Cannon, Aegis Shield or Buddy Bot here once the squad has all of its parts; after that, take one whenever you need it."); break;
                    case BuyableKind.Reactor: Tips.Offer("reactor", "The Lantern reactor. Something in it is listening. With the whole squad in the room, touching it starts the map's secret, and seals you in until it's done."); break;
                }
            }
            var motor = _player.Motor;
            if (motor != null && motor.Exhausted) Tips.Offer("stamina", "Out of breath! Sprinting drains the stamina bar under your health. Stop running for a moment and it refills; Fleet Foot makes it last twice as long.");
            if (motor != null && motor.IsMantling) Tips.Offer("mantle", $"Jump at anything your head can reach and keep pushing {K(GameAction.MoveForward)} to climb up onto it. Zombies climb after you.");
            if (Ladder.Near(_player.transform.position) != null) Tips.Offer("ladder", $"Walk into a ladder facing it to climb, {K(GameAction.MoveBack)} to go down, {K(GameAction.Jump)} to let go. Zombies can climb too.");
            if (rounds.Round.Value >= 3) Tips.Offer("map", $"Press {K(GameAction.Map)} for the map: every zone, where your squad is, the high spots and the build stations.");
            if (_player.HasPowerUpOffer) Tips.Offer("powerup", "Power-up! Press 1, 2 or 3 to pick a card. Power-ups stack and stay for the whole game, even if you go down.");
            if (MissionBoard.Instance != null && MissionBoard.Instance.SetRound.Value > 0 && rounds.Round.Value >= 2)
                Tips.Offer("mission", "Missions (top right) pay the whole squad. A new set of three arrives every 10 rounds.");
            if (rounds.Round.Value >= 2 && rounds.Phase.Value == GamePhase.RoundActive)
                Tips.Offer("grenade", $"{K(GameAction.Grenade)} throws a grenade, {K(GameAction.ThrowingKnife)} a throwing knife, {K(GameAction.Concussion)} a concussion bang that stuns. {K(GameAction.Melee)} is melee. They top up every round.");
            if (_gun.Magazine == 0 && _gun.Reserve > 0) Tips.Offer("reload", $"Press {K(GameAction.Reload)} to reload. {K(GameAction.SwitchWeapon)} or the mouse wheel switches guns.");
            if (_player.State.Value == LifeState.Downed) Tips.Offer("downed", "You're down. A teammate can hold their interact key on you to revive you before the timer runs out.");
            if (_player.FindReviveTarget(_player.transform.position) != null) Tips.Offer("revive", $"Hold {interact} to revive your teammate. Zombies can still hit you while you do.");
            if (NetZombie.Bosses.Count > 0) Tips.Offer("boss", "Boss! Its back has a glowing weak spot that takes extra damage. Spread out so it can't catch everyone.");
            if (rounds.IsBlackout) Tips.Offer("blackout", "Blackout round: dark, full of runners, and every kill is worth double points.");
            if (MissionBoard.Instance != null && MissionBoard.Instance.ActiveSurvivorSlot >= 0)
                Tips.Offer("survivor", "A survivor needs protecting. Zombies go for them, so stand close and keep the horde off. Watch their health in the mission list.");
        }

        // ---------------------------------------------------------------- blackout atmosphere

        void UpdateAtmosphere(RoundManager rounds)
        {
            if (!_haveBaseline)
            {
                _haveBaseline = true;
                _baseAmbient = RenderSettings.ambientLight;
                _baseFog = RenderSettings.fogColor;
                _baseFogDensity = RenderSettings.fogDensity;
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _moon = l; break; }
                if (_moon != null) _baseMoon = _moon.intensity;
            }
            float target = rounds != null && rounds.IsBlackout ? 1f : 0f;
            float next = Mathf.MoveTowards(_blackout, target, Time.deltaTime / 2.5f);
            if (Mathf.Approximately(next, _blackout) && next == 0f) return;
            _blackout = next;
            float t = _blackout;
            RenderSettings.ambientLight = Color.Lerp(_baseAmbient, new Color(0.07f, 0.04f, 0.08f), t);
            RenderSettings.fogColor = Color.Lerp(_baseFog, new Color(0.03f, 0.01f, 0.03f), t);
            RenderSettings.fogDensity = Mathf.Lerp(_baseFogDensity, _baseFogDensity * 1.9f, t);
            if (_moon != null) _moon.intensity = Mathf.Lerp(_baseMoon, _baseMoon * 0.15f, t);
            GraphicsSetup.SetBlackout(t);
        }

        void RestoreAtmosphere()
        {
            if (!_haveBaseline || _blackout <= 0f) return;
            RenderSettings.ambientLight = _baseAmbient;
            RenderSettings.fogColor = _baseFog;
            RenderSettings.fogDensity = _baseFogDensity;
            if (_moon != null) _moon.intensity = _baseMoon;
            GraphicsSetup.SetBlackout(0f);
        }

        // ---------------------------------------------------------------- drawing

        void OnGUI()
        {
            if (_player == null || _gun == null) return;
            float w = Screen.width, h = Screen.height, u = Ui.HudU;
            Ui.EnsureStyles(u);
            var rounds = RoundManager.Instance;
            var state = _player.State.Value;

            DrawNameTags(u);
            DrawSurvivorMarker(u);
            DrawDamageVignette(w, h);
            if (_blackout > 0f) Ui.Fill(new Rect(0, 0, w, h), new Color(0.05f, 0f, 0.08f, 0.18f * _blackout));

            if (rounds != null && rounds.IsGameOver)
            {
                DrawGameOver(w, h, u, rounds);
                DebugConsole.Draw(w, h, u);
                return;
            }

            if (state == LifeState.Alive)
            {
                DrawCrosshair(w, h, u);
                DrawHitmarker(w, h, u);
                DrawCenterPrompts(w, h, u);
            }
            else if (state == LifeState.Downed) DrawDowned(w, h, u);
            else DrawBledOut(w, h, u);

            DrawRound(w, h, u, rounds);
            DrawBanner(w, h, u);
            DrawTopLeft(u, rounds);
            DrawBottomLeft(h, u);
            DrawAmmo(w, h, u);
            DrawMissions(w, u);
            DrawPowerUpOffer(w, h, u);
            DrawTip(w, h, u);
            DrawSubtitles(w, h, u);
            if (_showHelp) DrawHelp(w, h, u, rounds);
            if (_mapOpen && _player.Look.CursorLocked) MapScreen.Draw(w, h, u, _player);

            if (DebugConsole.IsOpen) DebugConsole.Draw(w, h, u);
            else if (!_player.Look.CursorLocked) DrawPause(w, h, u);
        }

        // ---------------------------------------------------------------- pause

        void DrawPause(float w, float h, float u)
        {
            Ui.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.55f));
            if (_pause == PauseScreen.Settings)
            {
                float sw = Mathf.Min(w - 40 * u, 1100 * u), sh = Mathf.Min(h - 40 * u, 760 * u);
                if (!SettingsMenu.Draw(new Rect((w - sw) / 2f, (h - sh) / 2f, sw, sh), u))
                {
                    SettingsMenu.Close();
                    _pause = PauseScreen.Main;
                }
                return;
            }

            float pw = 440 * u, ph = 420 * u;
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            Ui.GlowSpot(new Rect(r.center.x - 400 * u, r.y - 200 * u, 800 * u, 500 * u), Ui.A(Ui.Blood, 0.2f));
            Ui.PanelBox(r, Ui.Blood, u);
            Ui.Label(new Rect(r.x, r.y + 26 * u, pw, 60 * u), "PAUSED", Ui.Title, Ui.Text, TextAnchor.UpperCenter);
            bool solo = NetPlayer.All.Count <= 1;
            Ui.Label(new Rect(r.x, r.y + 96 * u, pw, 30 * u), solo ? "The zombies don't wait, even solo." : "The game keeps going for your squad.", Ui.Tiny, Ui.Muted, TextAnchor.UpperCenter);
            float bx = r.x + 40 * u, bw = pw - 80 * u, by = r.y + 140 * u, bh = 52 * u, gap = 12 * u;
            if (Ui.Button(new Rect(bx, by, bw, bh), "RESUME", accent: true)) Resume();
            by += bh + gap;
            if (Ui.Button(new Rect(bx, by, bw, bh), "SETTINGS")) _pause = PauseScreen.Settings;
            by += bh + gap;
            if (Ui.Button(new Rect(bx, by, bw, bh), _showHelp ? "HIDE CONTROLS" : "SHOW CONTROLS")) _showHelp = !_showHelp;
            by += bh + gap;
            bool hosting = RoundManager.Instance != null && RoundManager.Instance.IsServer;
            if (Ui.Button(new Rect(bx, by, bw, bh), hosting ? "END GAME FOR EVERYONE" : "LEAVE GAME"))
                PrototypeBootstrap.ReturnToMenu(hosting ? "You ended the game." : "You left the game.");
            DrawStoryCard(w, r, u);
        }

        /// <summary>The story so far, beside the pause menu.</summary>
        void DrawStoryCard(float w, Rect pause, float u)
        {
            float cw = 460 * u;
            if (pause.xMax + 24 * u + cw > w - 20 * u) return;
            int act = StoryDirector.CurrentAct;
            var style = Ui.Small;
            bool wrap = style.wordWrap;
            style.wordWrap = true;
            string summary = StoryData.ActSummaries[act];
            string last = StoryDirector.LastBeat;
            float sh = style.CalcHeight(new GUIContent(summary), cw - 48 * u);
            float lh = string.IsNullOrEmpty(last) ? 0f : Ui.Tiny.CalcHeight(new GUIContent($"\"{last}\""), cw - 48 * u) + 40 * u;
            // The easter egg shows up here once the squad has found it.
            var egg = EasterEgg.Instance;
            bool eggFound = egg != null && (egg.Active.Value || egg.StepsDone.Value > 0);
            float eh = eggFound ? 54 * u : 0f;
            var r = new Rect(pause.xMax + 24 * u, pause.y, cw, 90 * u + sh + lh + eh);
            Ui.PanelBox(r, StoryData.RadioColor, u);
            string actLine = act < StoryData.StoryActs ? $"ACT {act + 1} OF {StoryData.StoryActs}  ·  THE STORY ENDS AT ROUND {StoryData.FinalRound}" : "STORY COMPLETE";
            Ui.Label(new Rect(r.x + 24 * u, r.y + 20 * u, cw, 26 * u), actLine, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(r.x + 24 * u, r.y + 44 * u, cw, 34 * u), StoryData.ActNames[act].ToUpperInvariant(), Ui.Bold, StoryData.RadioColor, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(r.x + 24 * u, r.y + 82 * u, cw - 48 * u, sh), summary, style, Ui.Text, TextAnchor.UpperLeft);
            style.wordWrap = wrap;
            if (!string.IsNullOrEmpty(last))
            {
                var tiny = Ui.Tiny;
                bool tw = tiny.wordWrap;
                tiny.wordWrap = true;
                Ui.Label(new Rect(r.x + 24 * u, r.y + 92 * u + sh, cw - 48 * u, 22 * u), "LAST RADIO CALL", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
                Ui.Label(new Rect(r.x + 24 * u, r.y + 116 * u + sh, cw - 48 * u, lh - 40 * u), $"\"{last}\"", tiny, Ui.Muted, TextAnchor.UpperLeft);
                tiny.wordWrap = tw;
            }
            if (eggFound)
            {
                int done = egg.StepsDone.Value, steps = EasterEggData.Steps.Length;
                var next = EasterEggData.At(done);
                string state = egg.Active.Value ? $"Now: {next.title}, {egg.Remaining} runners left"
                    : next != null && next.kind != EggStepKind.Locked ? $"Next: {next.title}" : "Doctor Morrow will have more for you soon";
                float ey = r.yMax - eh;
                Ui.Label(new Rect(r.x + 24 * u, ey, cw, 22 * u), $"DOCTOR MORROW'S WORK  ·  {done} OF {steps} STEPS", Ui.Tiny, EasterEggData.ScientistColor, TextAnchor.UpperLeft);
                Ui.Label(new Rect(r.x + 24 * u, ey + 22 * u, cw - 48 * u, 22 * u), state, Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            }
        }

        // ---------------------------------------------------------------- screens

        void DrawDowned(float w, float h, float u)
        {
            Ui.Fill(new Rect(0, 0, w, h), new Color(0.3f, 0f, 0f, 0.35f));
            Ui.Label(new Rect(0, h * 0.28f, w, 110 * u), "DOWNED", Ui.Huge, Ui.Blood, TextAnchor.MiddleCenter);
            string line = _player.SelfReviving
                ? $"Second Wind: back on your feet in {Mathf.CeilToInt(_player.SelfReviveRemaining.Value)}s"
                : $"Bleeding out in {Mathf.CeilToInt(_player.BleedOutRemaining.Value)}s. A teammate can revive you.";
            Ui.Label(new Rect(0, h * 0.28f + 110 * u, w, 40 * u), line, Ui.Body, Ui.Text, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(0, h * 0.28f + 146 * u, w, 30 * u), "Your perks are lost. Power-ups stay.", Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);
            if (_player.ReviveProgress.Value > 0f) DrawProgress(w, h * 0.28f + 190 * u, u, _player.ReviveProgress.Value, "BEING REVIVED", Ui.Good);
        }

        void DrawBledOut(float w, float h, float u)
        {
            Ui.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.45f));
            Ui.Label(new Rect(0, h * 0.3f, w, 110 * u), "BLED OUT", Ui.Huge, Ui.Muted, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(0, h * 0.3f + 110 * u, w, 40 * u), "You're back when your squad clears this round.", Ui.Body, Ui.Text, TextAnchor.MiddleCenter);
        }

        void DrawGameOver(float w, float h, float u, RoundManager rounds)
        {
            Ui.Fill(new Rect(0, 0, w, h), new Color(0.08f, 0f, 0f, 0.7f));
            bool won = rounds.Won.Value;
            Ui.GlowSpot(new Rect(w / 2f - 600 * u, h * 0.1f, 1200 * u, 400 * u), Ui.A(won ? Ui.Amber : Ui.Blood, 0.25f));
            Ui.Label(new Rect(0, h * 0.16f, w, 120 * u), won ? "YOU MADE IT" : "GAME OVER", Ui.Huge, won ? Ui.Amber : Ui.Blood, TextAnchor.MiddleCenter);
            int round = rounds.DisplayRound;
            string survived = won ? $"You survived all {round} rounds." : round == 1 ? "You were overrun on round 1." : $"You were overrun on round {round}.";
            Ui.Label(new Rect(0, h * 0.16f + 120 * u, w, 40 * u), survived, Ui.Header, Ui.Text, TextAnchor.MiddleCenter);

            float tw = Mathf.Min(w - 40 * u, 900 * u), rowH = 44 * u;
            var players = NetPlayer.All;
            var table = new Rect((w - tw) / 2f, h * 0.16f + 190 * u, tw, 56 * u + rowH * players.Count);
            Ui.PanelBox(table, won ? Ui.Amber : Ui.Blood, u);
            float[] cols = { 0.04f, 0.5f, 0.68f, 0.84f };
            string[] heads = { "PLAYER", "POINTS", "KILLS", "HEADSHOTS" };
            for (int i = 0; i < cols.Length; i++)
                Ui.Label(new Rect(table.x + tw * cols[i], table.y + 14 * u, tw * 0.3f, 30 * u), heads[i], Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            float y = table.y + 50 * u;
            foreach (var p in players)
            {
                Ui.RoundRect(new Rect(table.x + tw * 0.025f, y + 14 * u, 10 * u, 10 * u), p.SuitColor, small: true);
                Ui.Label(new Rect(table.x + tw * cols[0] + 10 * u, y, tw * 0.45f, rowH), p.TaggedName, Ui.Body, p == _player ? Ui.Amber : Ui.Text, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(table.x + tw * cols[1], y, tw * 0.2f, rowH), $"{p.Points.Value:N0}", Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(table.x + tw * cols[2], y, tw * 0.2f, rowH), $"{p.Kills.Value}", Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(table.x + tw * cols[3], y, tw * 0.2f, rowH), $"{p.Headshots.Value}", Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
                y += rowH;
            }
            bool hosting = rounds.IsServer;
            float by = table.yMax + 30 * u;
            if (hosting) Ui.Prompt(w / 2f, by, "Enter", "restart with the same squad", Ui.Body, Ui.Text, u);
            else Ui.Label(new Rect(0, by, w, 40 * u), "Waiting for the host to restart", Ui.Body, Ui.Muted, TextAnchor.MiddleCenter);
            float bw = 340 * u;
            if (Ui.Button(new Rect((w - bw) / 2f, by + 60 * u, bw, 50 * u), hosting ? "END GAME FOR EVERYONE" : "LEAVE GAME"))
                PrototypeBootstrap.ReturnToMenu(hosting ? "You ended the game." : "You left the game.");
            if (Cursor.lockState != CursorLockMode.None) PlayerLook.SetCursorLocked(false);
        }

        // ---------------------------------------------------------------- centre

        void DrawProgress(float w, float y, float u, float progress, string label, Color color)
        {
            float bw = 260 * u;
            Ui.Bar(new Rect(w / 2 - bw / 2, y, bw, 8 * u), progress, color);
            Ui.Label(new Rect(0, y + 12 * u, w, 30 * u), label, Ui.Tiny, Ui.Text, TextAnchor.MiddleCenter);
        }

        void DrawCenterPrompts(float w, float h, float u)
        {
            float y = h * 0.62f;
            int quarter = _gun.MagazineCapacity / 4;
            if (_gun.IsReloading) DrawProgress(w, h * 0.57f, u, _gun.ReloadProgress, "RELOADING", Ui.Text);
            else if (_gun.Magazine == 0 && _gun.Reserve == 0) Ui.Label(new Rect(0, h * 0.57f, w, 30 * u), "NO AMMO", Ui.Bold, Ui.Bad, TextAnchor.MiddleCenter);
            else if (_gun.Magazine <= quarter) Ui.Prompt(w / 2f, h * 0.57f, K(GameAction.Reload), "RELOAD", Ui.Small, _gun.Magazine == 0 ? Ui.Bad : Ui.Amber, u);

            var target = _player.FindReviveTarget(_player.transform.position);
            if (target != null)
            {
                Ui.Prompt(w / 2f, y, K(GameAction.Interact), $"Hold to revive {target.DisplayName}", Ui.Body, Ui.Text, u);
                if (target.ReviveProgress.Value > 0f) DrawProgress(w, y + 44 * u, u, target.ReviveProgress.Value, "REVIVING", Ui.Good);
                return;
            }

            var b = _player.FocusedBuyable;
            if (b == null) return;
            if (b.Kind == BuyableKind.Shop)
            {
                if (_player.ShopOpen) DrawShop(w, h, u);
                else Ui.Prompt(w / 2f, y, K(GameAction.Interact), "Browse the spawn shop", Ui.Body, Ui.Text, u);
                return;
            }
            long price = _player.PriceFor(b, out string what);
            if (price < 0)
            {
                // Something there, but not for sale right now (a maxed gun, a recharging machine, a perk you own).
                if (!string.IsNullOrEmpty(what)) Ui.Label(new Rect(0, y, w, 36 * u), what, Ui.Body, Ui.Muted, TextAnchor.MiddleCenter);
                return;
            }
            bool afford = _player.Points.Value >= price;
            string text = price > 0 ? $"{Capitalise(what)}   {price:N0}" : Capitalise(what);
            Ui.Prompt(w / 2f, y, K(GameAction.Interact), text, Ui.Body, afford ? Ui.Text : Ui.Bad, u);
            if (!afford) Ui.Label(new Rect(0, y + 40 * u, w, 30 * u), $"You need {price - _player.Points.Value:N0} more points", Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        void DrawBanner(float w, float h, float u)
        {
            const float Duration = 3.4f;
            float t = Time.time - _bannerTime;
            if (t > Duration || string.IsNullOrEmpty(_banner)) return;
            float a = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((Duration - t) / 0.6f);
            float slide = (1f - Mathf.Clamp01(t / 0.35f)) * 30 * u;
            float y = h * 0.2f;
            Ui.GlowSpot(new Rect(w / 2f - 500 * u, y - 60 * u, 1000 * u, 260 * u), Ui.A(_bannerColor, 0.22f * a));
            Ui.Label(new Rect(0, y + slide, w, 110 * u), _banner, Ui.Huge, Ui.A(_bannerColor, a), TextAnchor.MiddleCenter);
            float lw = Mathf.Min(w * 0.4f, Ui.Width(_banner, Ui.Huge));
            Ui.Fill(new Rect((w - lw) / 2f, y + 104 * u, lw, 2 * u), Ui.A(_bannerColor, 0.6f * a));
            if (!string.IsNullOrEmpty(_bannerSub))
                Ui.Label(new Rect(0, y + 114 * u, w, 40 * u), _bannerSub, Ui.Body, Ui.A(Ui.Text, a), TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- top

        void DrawRound(float w, float h, float u, RoundManager rounds)
        {
            if (rounds == null) return;
            string text = rounds.Phase.Value == GamePhase.Intermission
                ? $"Round {rounds.Round.Value + 1} in {Mathf.CeilToInt(rounds.PhaseTimeLeft)}"
                : rounds.ZombiesLeft.Value == 1 ? "1 zombie left" : $"{rounds.ZombiesLeft.Value} zombies left";
            float pw = Ui.Width(text, Ui.Bold) + 48 * u, ph = 40 * u;
            var r = new Rect((w - pw) / 2f, 22 * u, pw, ph);
            Ui.RoundRect(r, new Color(0f, 0f, 0f, 0.45f));
            Ui.Label(r, text, Ui.Bold, Ui.Text, TextAnchor.MiddleCenter);
            float y = r.yMax + 6 * u;
            if (rounds.IsBlackout)
            {
                Ui.Label(new Rect(0, y, w, 26 * u), $"BLACKOUT  ·  {GameConfig.Round.blackoutPointsMultiplier:0}x POINTS", Ui.Small, Ui.Violet, TextAnchor.UpperCenter);
                y += 26 * u;
            }
            var egg = EasterEgg.Instance;
            if (egg != null && egg.Active.Value)
            {
                Ui.Label(new Rect(0, y, w, 26 * u), $"LANTERN TRIAL  ·  {egg.Remaining} OF {egg.Total.Value} RUNNERS LEFT  ·  DOORS SEALED", Ui.Small, EasterEggData.ScientistColor, TextAnchor.UpperCenter);
                y += 26 * u;
            }
            if (rounds.GodMode.Value)
            {
                Ui.Label(new Rect(0, y, w, 26 * u), "GOD MODE (test)", Ui.Small, Ui.Amber, TextAnchor.UpperCenter);
                y += 26 * u;
            }
            DrawBossBars(w, y + 8 * u, u);
        }

        void DrawBossBars(float w, float y, float u)
        {
            float bw = 620 * u, bh = 12 * u;
            foreach (var boss in NetZombie.Bosses)
            {
                if (boss == null || !boss.IsAlive) continue;
                Ui.Label(new Rect(0, y, w, 26 * u), boss.Data.displayName.ToUpperInvariant(), Ui.Small, new Color(1f, 0.6f, 0.3f), TextAnchor.LowerCenter);
                y += 28 * u;
                Ui.Bar(new Rect((w - bw) / 2f, y, bw, bh), boss.HealthFraction.Value, new Color(0.9f, 0.38f, 0.08f));
                y += bh + 12 * u;
            }
        }

        void DrawTopLeft(float u, RoundManager rounds)
        {
            float x = 36 * u, y = 24 * u;
            // Points.
            string points = $"{_player.Points.Value:N0}";
            Ui.Label(new Rect(x, y, 600 * u, 50 * u), points, Ui.Header, Ui.Amber, TextAnchor.MiddleLeft);
            float pw = Ui.Width(points, Ui.Header);
            Ui.Label(new Rect(x + pw + 8 * u, y + 6 * u, 100 * u, 44 * u), "pts", Ui.Small, Ui.Muted, TextAnchor.MiddleLeft);
            float gainAge = Time.time - _pointsGainTime;
            if (gainAge < 1.2f)
            {
                float a = 1f - Mathf.Clamp01((gainAge - 0.6f) / 0.6f);
                Ui.Label(new Rect(x + pw + 50 * u, y - gainAge * 14 * u, 300 * u, 50 * u), $"+{_pointsGain:N0}", Ui.Bold, Ui.A(Ui.Amber, a), TextAnchor.MiddleLeft);
            }
            y += 52 * u;

            // Level, prestige and XP.
            var shop = GameConfig.Shop;
            int level = _player.Level, prestige = _player.Prestige.Value;
            float bx = x;
            if (prestige > 0)
            {
                var emblem = new Rect(bx, y, 34 * u, 26 * u);
                Ui.RoundRect(emblem, ShopData.PrestigeColor(prestige), small: true);
                Ui.Label(emblem, $"P{prestige}", Ui.Tiny, new Color(0.08f, 0.06f, 0.05f), TextAnchor.MiddleCenter);
                bx += 42 * u;
            }
            Ui.Label(new Rect(bx, y, 120 * u, 26 * u), $"LV {level}", Ui.Bold, Ui.Text, TextAnchor.MiddleLeft);
            bx += Ui.Width($"LV {level}", Ui.Bold) + 12 * u;
            float barW = 220 * u;
            Ui.Bar(new Rect(bx, y + 9 * u, barW, 8 * u), _player.LevelProgress, Ui.Amber);
            string next = shop != null && level < shop.maxLevel ? $"{_player.XpToNextLevel:N0} XP to level {level + 1}" : "Max level";
            Ui.Label(new Rect(bx + barW + 10 * u, y, 400 * u, 26 * u), next, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            y += 30 * u;
            Ui.Label(new Rect(x, y, 600 * u, 24 * u), $"{_player.Kills.Value} kills  ·  {_player.Headshots.Value} headshots", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            y += 26 * u;
            if (rounds != null && rounds.IsServer && _showHelp)
            {
                Ui.Label(new Rect(x, y, 900 * u, 24 * u), $"Host: zombie HP {rounds.CurrentZombieHealth:N0}  ·  {rounds.CurrentSpeedTier}  ·  alive {rounds.AliveZombies.Value}/{rounds.LiveCap}", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
                y += 26 * u;
            }
            DrawSquad(x, y + 10 * u, u);
        }

        void DrawSquad(float x, float y, float u)
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == _player) continue;
                bool alive = p.State.Value == LifeState.Alive;
                var row = new Rect(x, y, 380 * u, 46 * u);
                Ui.RoundRect(row, new Color(0f, 0f, 0f, 0.35f));
                Ui.RoundRect(new Rect(row.x + 10 * u, row.y + 10 * u, 8 * u, 26 * u), p.SuitColor, small: true);
                Ui.Label(new Rect(row.x + 28 * u, row.y + 2 * u, 260 * u, 24 * u), p.TaggedName, Ui.Small, Ui.Text, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(row.x, row.y + 2 * u, row.width - 10 * u, 24 * u), $"{p.Points.Value:N0}", Ui.Small, Ui.Amber, TextAnchor.MiddleRight);
                if (alive) Ui.Bar(new Rect(row.x + 28 * u, row.y + 30 * u, row.width - 40 * u, 6 * u), p.Health.Value / Mathf.Max(1f, p.MaxHealthNow), Ui.Text);
                else
                {
                    string status = p.State.Value == LifeState.Downed ? $"DOWN  ·  {Mathf.CeilToInt(p.BleedOutRemaining.Value)}s" : "OUT UNTIL NEXT ROUND";
                    Ui.Label(new Rect(row.x + 28 * u, row.y + 24 * u, 300 * u, 20 * u), status, Ui.Tiny, Ui.Bad, TextAnchor.MiddleLeft);
                }
                y += 52 * u;
            }
        }

        /// <summary>The current mission set, top right.</summary>
        /// <summary>Where a placed mission is, for its description: " in the Depot", " on the Water Tower (Pump Station)".</summary>
        static string MissionWhere(MissionDef def, ulong slot)
        {
            int at = MissionData.ZoneOf(slot);
            if (def.objective == MissionObjective.HoldHighGround && at < ZoneMap.HighSpotCount)
                return $" on the {ZoneMap.HighSpotNames[at]} ({ZoneMap.ZoneNames[ZoneMap.HighSpotZones[at]]})";
            if (MissionData.IsPlaced(def.objective) && at < ZoneMap.ZoneCount) return $" in the {ZoneMap.ZoneNames[at]}";
            return "";
        }

        void DrawMissions(float w, float u)
        {
            var board = MissionBoard.Instance;
            var data = GameConfig.Missions;
            if (board == null || data == null || board.SetRound.Value <= 0) return;
            float pw = 520 * u, x = w - pw - 30 * u, y = 24 * u;
            int count = 0;
            for (int i = 0; i < MissionData.SlotCount; i++) if (board.DefIn(i) != null) count++;
            if (count == 0) return;
            float rowH = 74 * u;
            var panel = new Rect(x, y, pw, 46 * u + rowH * count);
            Ui.PanelBox(panel, Ui.Sky, u, 0.85f);
            Ui.Label(new Rect(x + 18 * u, y + 12 * u, pw, 26 * u), "MISSIONS", Ui.Small, Ui.Sky, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y + 12 * u, pw - 18 * u, 26 * u), $"new set at round {data.NextRefreshRound(board.SetRound.Value)}", Ui.Tiny, Ui.Muted, TextAnchor.MiddleRight);
            y += 44 * u;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = board.DefIn(i);
                if (def == null) continue;
                ulong slot = board.SlotValue(i);
                bool done = MissionData.DoneOf(slot), failed = MissionData.FailedOf(slot);
                int target = MissionData.TargetOf(slot), progress = MissionData.ProgressOf(slot);
                string where = MissionWhere(def, slot);
                Color c = done ? Ui.Good : failed ? Ui.Bad : Ui.Text;
                Ui.Label(new Rect(x + 18 * u, y, pw - 36 * u, 24 * u), def.title, Ui.Small, c, TextAnchor.MiddleLeft);
                string state = done ? "DONE" : failed ? "FAILED" : $"{progress}/{target}";
                Ui.Label(new Rect(x, y, pw - 18 * u, 24 * u), state, Ui.Small, c, TextAnchor.MiddleRight);
                Ui.Label(new Rect(x + 18 * u, y + 22 * u, pw - 36 * u, 22 * u), def.Describe(target) + where, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
                float barY = y + 48 * u;
                if (!done && !failed)
                {
                    Ui.Bar(new Rect(x + 18 * u, barY, (pw - 36 * u) * 0.5f, 5 * u), target > 0 ? progress / (float)target : 0f, Ui.Sky);
                    if (def.objective == MissionObjective.ProtectSurvivor && board.ActiveSurvivorSlot == i && data.survivorHealth > 0f)
                    {
                        float hx = x + 18 * u + (pw - 36 * u) * 0.55f;
                        Ui.Bar(new Rect(hx, barY, (pw - 36 * u) * 0.45f, 5 * u), board.SurvivorHealth.Value / data.survivorHealth, Ui.Good);
                    }
                    else Ui.Label(new Rect(x, barY - 8 * u, pw - 18 * u, 20 * u), RewardText(def, board.SetRound.Value), Ui.Tiny, Ui.Amber, TextAnchor.MiddleRight);
                }
                y += rowH;
            }
        }

        void DrawHelp(float w, float h, float u, RoundManager rounds)
        {
            var lines = new List<string>
            {
                $"{Keys(GameAction.MoveForward, GameAction.MoveLeft, GameAction.MoveBack, GameAction.MoveRight)} move  ·  {K(GameAction.Sprint)} sprint  ·  {K(GameAction.Jump)} jump (jump at a ledge to climb it)",
                $"{K(GameAction.Fire)} fire  ·  {K(GameAction.Aim)} aim  ·  {K(GameAction.Reload)} reload  ·  {K(GameAction.SwitchWeapon)} switch gun",
                $"{K(GameAction.Melee)} melee  ·  {K(GameAction.Grenade)} grenade  ·  {K(GameAction.ThrowingKnife)} knife  ·  {K(GameAction.Concussion)} bang",
                $"{K(GameAction.Interact)} buy / hold to revive  ·  1 2 3 pick power-up  ·  4 redraw  ·  {K(GameAction.Map)} map",
            };
            if (rounds != null && rounds.IsServer)
                lines.Add("Test (host): = next round  ·  ] +10 rounds  ·  F1 god  ·  P +5,000  ·  O power-up  ·  B boss  ·  ` console");
            lines.Add("Esc pause  ·  H hide this");
            float lh = 22 * u, pw = 700 * u;
            float y = h - 40 * u - lh * lines.Count - 260 * u;
            for (int i = 0; i < lines.Count; i++)
                Ui.Label(new Rect(w - pw - 36 * u, y + i * lh, pw, lh), lines[i], Ui.Tiny, Ui.A(Ui.Text, 0.6f), TextAnchor.MiddleRight);
        }

        // ---------------------------------------------------------------- bottom left

        void DrawBottomLeft(float h, float u)
        {
            float x = 36 * u;
            var rounds = RoundManager.Instance;

            // Health and armour.
            float bw = 340 * u, y = h - 52 * u;
            float n = Mathf.Clamp01(_player.Health.Value / Mathf.Max(1f, _player.MaxHealthNow));
            Ui.Bar(new Rect(x, y, bw, 14 * u), n, Color.Lerp(Ui.Blood, Ui.Text, n));
            Ui.Label(new Rect(x, y - 34 * u, 200 * u, 30 * u), Mathf.CeilToInt(_player.Health.Value).ToString(), Ui.Bold, Ui.Text, TextAnchor.MiddleLeft);
            var perks = GameConfig.Perks;
            if (perks != null && perks.maxPlates > 0)
            {
                float pw = (120 * u - (perks.maxPlates - 1) * 4 * u) / perks.maxPlates;
                float px = x + bw - 120 * u;
                for (int i = 0; i < perks.maxPlates; i++)
                {
                    float fill = Mathf.Clamp01(_player.Armor.Value / perks.plateArmor - i);
                    Ui.Bar(new Rect(px + i * (pw + 4 * u), y - 14 * u, pw, 8 * u), fill, Ui.Sky);
                }
            }

            // Stamina, just under the health bar while it isn't full. Red while it's run dry and locked.
            var motor = _player.Motor;
            if (motor != null)
            {
                _staminaShow = Mathf.MoveTowards(_staminaShow, motor.Stamina < 0.999f ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
                if (_staminaShow > 0.01f)
                {
                    var sc = motor.Exhausted ? Ui.Blood : Color.Lerp(Ui.Amber, Ui.Good, motor.Stamina);
                    sc.a *= _staminaShow;
                    Ui.Bar(new Rect(x, y + 18 * u, bw * 0.6f, 5 * u), motor.Stamina, sc, new Color(0f, 0f, 0f, 0.55f * _staminaShow));
                }
            }
            float ey = y + 9 * u;

            // Aegis Shield and Buddy Bot, under the health and stamina bars.
            float extraX = x;
            if (_player.HasShield)
            {
                var sc = BuildManager.BuildableColors[BuildManager.Shield];
                Ui.Bar(new Rect(extraX, ey + 22 * u, 150 * u, 6 * u), _player.ShieldHealth.Value / BuildManager.ShieldMaxHealth, sc);
                Ui.Label(new Rect(extraX + 156 * u, ey + 14 * u, 120 * u, 22 * u), "SHIELD", Ui.Tiny, sc, TextAnchor.MiddleLeft);
                extraX += 240 * u;
            }
            var build = BuildManager.Instance;
            if (build != null && build.RobotActive && build.RobotOwner.Value == (long)_player.OwnerClientId)
            {
                var bc = BuildManager.BuildableColors[BuildManager.Bot];
                Ui.Bar(new Rect(extraX, ey + 22 * u, 150 * u, 6 * u), build.RobotTimeLeft.Value / BuildManager.RobotLifetime, bc);
                Ui.Label(new Rect(extraX + 156 * u, ey + 14 * u, 160 * u, 22 * u), $"BUDDY BOT {Mathf.CeilToInt(build.RobotTimeLeft.Value)}s", Ui.Tiny, bc, TextAnchor.MiddleLeft);
            }

            // Big round number.
            if (rounds != null && rounds.Round.Value > 0)
            {
                var big = new Rect(x - 4 * u, y - 170 * u, 400 * u, 130 * u);
                Ui.Label(big, rounds.Round.Value.ToString(), Ui.Huge, rounds.IsBlackout ? Ui.Violet : new Color(0.75f, 0.08f, 0.05f), TextAnchor.LowerLeft);
            }

            // Perks owned.
            float rowY = y - 210 * u;
            if (perks != null && _player.Perks.Value != 0)
            {
                float px = x, s = 30 * u;
                for (int i = 0; i < perks.Count; i++)
                {
                    if (!PerkData.Has(_player.Perks.Value, (PerkKind)i)) continue;
                    var def = perks.At(i);
                    var r = new Rect(px, rowY, s, s);
                    Ui.RoundRect(r, def.color, small: true);
                    Ui.Label(r, Initials(def.name), Ui.Tiny, new Color(0.05f, 0.05f, 0.06f), TextAnchor.MiddleCenter);
                    px += s + 6 * u;
                }
                rowY -= 40 * u;
            }

            // Power-ups.
            var upgrades = GameConfig.Upgrades;
            if (upgrades != null && _player.PowerLevels.Value != 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < upgrades.PowerUpCount; i++)
                {
                    int level = _player.PowerLevel(i);
                    if (level > 0) parts.Add($"{upgrades.PowerUpAt(i).name} {NetPlayer.ToRoman(level)}");
                }
                var style = Ui.Tiny;
                bool wrap = style.wordWrap;
                style.wordWrap = true;
                Ui.Label(new Rect(x, rowY - 30 * u, 640 * u, 56 * u), string.Join("  ·  ", parts), style, Ui.Violet, TextAnchor.LowerLeft);
                style.wordWrap = wrap;
            }
        }

        static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var words = name.Split(' ');
            return words.Length > 1 ? $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}" : name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
        }

        // ---------------------------------------------------------------- bottom right

        void DrawAmmo(float w, float h, float u)
        {
            if (_player.State.Value != LifeState.Alive) return;
            float right = w - 36 * u;
            int quarter = _gun.MagazineCapacity / 4;
            Color magColor = _gun.Magazine == 0 ? Ui.Bad : _gun.Magazine <= quarter ? Ui.Amber : Ui.Text;
            string reserve = $"/ {_gun.Reserve}";
            float rw = Ui.Width(reserve, Ui.Bold);
            Ui.Label(new Rect(right - rw, h - 108 * u, rw + 4, 40 * u), reserve, Ui.Bold, Ui.Muted, TextAnchor.LowerLeft);
            Ui.Label(new Rect(right - rw - 310 * u, h - 140 * u, 300 * u, 80 * u), _gun.Magazine.ToString(), Ui.Title, magColor, TextAnchor.LowerRight);
            Ui.Label(new Rect(right - 600 * u, h - 58 * u, 600 * u, 30 * u), _gun.DisplayName ?? _gun.Data.displayName, Ui.Small, Ui.Text, TextAnchor.MiddleRight);
            int other = 1 - _player.ActiveSlot.Value;
            if (GameConfig.GunById(_player.GunInSlot(other)) != null)
            {
                string name = _player.GunName(other);
                float nw = Ui.Width(name, Ui.Tiny);
                Ui.Label(new Rect(right - nw, h - 176 * u, nw + 4, 26 * u), name, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
                Ui.Key(new Vector2(right - nw - 40 * u, h - 176 * u), K(GameAction.SwitchWeapon), 26 * u);
            }

            // Throwables.
            float y = h - 222 * u, x = right;
            x = DrawEquip(x, y, u, K(GameAction.Concussion), "BANG", _player.Bangs.Value, Ui.Sky);
            x = DrawEquip(x, y, u, K(GameAction.ThrowingKnife), "KNIFE", _player.Knives.Value, Ui.Text);
            DrawEquip(x, y, u, K(GameAction.Grenade), "FRAG", _player.Grenades.Value, Ui.Amber);
        }

        /// <summary>One throwable counter, right-aligned at x. Returns the x for the next one.</summary>
        float DrawEquip(float x, float y, float u, string key, string label, int count, Color color)
        {
            float bw = 96 * u, bh = 34 * u;
            var r = new Rect(x - bw, y, bw, bh);
            Ui.RoundRect(r, new Color(0f, 0f, 0f, count > 0 ? 0.45f : 0.25f), small: true);
            Ui.Key(new Vector2(r.x + 6 * u, r.y + 6 * u), key, 22 * u);
            Ui.Label(new Rect(r.x + 34 * u, r.y, 40 * u, bh), label, Ui.Tiny, count > 0 ? Ui.Muted : Ui.A(Ui.Muted, 0.5f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(r.x, r.y, bw - 8 * u, bh), count.ToString(), Ui.Bold, count > 0 ? color : Ui.A(Ui.Muted, 0.5f), TextAnchor.MiddleRight);
            return x - bw - 8 * u;
        }

        // ---------------------------------------------------------------- shop and power-up cards

        /// <summary>The spawn shop menu: attachments for the gun in hand, unlocked by level, bought with points.</summary>
        void DrawShop(float w, float h, float u)
        {
            var shop = GameConfig.Shop;
            if (shop == null) return;
            float pw = Mathf.Min(w - 40 * u, 1000 * u), rowH = 34 * u, ph = 104 * u + rowH * shop.Count;
            var r = new Rect((w - pw) / 2f, Mathf.Max(20 * u, h * 0.5f - ph / 2f + 20 * u), pw, ph);
            Ui.PanelBox(r, Ui.Amber, u);
            Ui.Label(new Rect(r.x + 24 * u, r.y + 16 * u, pw, 34 * u), $"SPAWN SHOP  ·  {_player.GunName(_player.ActiveSlot.Value)}", Ui.Bold, Ui.Amber, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(r.x + 24 * u, r.y + 50 * u, pw, 26 * u), $"Level {_player.Level}  ·  number key to buy  ·  {K(GameAction.SwitchWeapon)} switch gun  ·  {K(GameAction.Interact)} close", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            float ry = r.y + 86 * u;
            for (int i = 0; i < shop.Count; i++)
            {
                var a = shop.At(i);
                var status = _player.StatusOf(i);
                Color c = status == AttachmentStatus.Locked ? Ui.A(Ui.Text, 0.35f)
                    : status == AttachmentStatus.Owned ? Ui.Good
                    : _player.Points.Value >= a.price ? Ui.Text : Ui.Bad;
                if (i % 2 == 0) Ui.Fill(new Rect(r.x + 12 * u, ry, pw - 24 * u, rowH), new Color(1f, 1f, 1f, 0.025f));
                if (status != AttachmentStatus.Locked) Ui.Key(new Vector2(r.x + 22 * u, ry + 5 * u), (i + 1).ToString(), 24 * u);
                Ui.Label(new Rect(r.x + 60 * u, ry, pw - 280 * u, rowH), $"{a.name}  <color=#8a8f99>({a.slot})  {a.description}</color>", Ui.Small, c, TextAnchor.MiddleLeft);
                string state = status == AttachmentStatus.Locked ? $"Level {a.unlockLevel}" : status == AttachmentStatus.Owned ? "Fitted" : $"{a.price:N0}";
                Ui.Label(new Rect(r.x, ry, pw - 24 * u, rowH), state, Ui.Small, c, TextAnchor.MiddleRight);
                ry += rowH;
            }
        }

        /// <summary>The three power-up cards to pick from after every few rounds.</summary>
        void DrawPowerUpOffer(float w, float h, float u)
        {
            var upgrades = GameConfig.Upgrades;
            if (upgrades == null || !_player.HasPowerUpOffer) return;
            bool empowered = _player.OfferIsEmpowered;
            int levels = _player.OfferLevels;
            Color accent = empowered ? Ui.Amber : Ui.Violet;
            float cw = 310 * u, ch = 150 * u, gap = 20 * u;
            float x0 = (w - (cw * 3 + gap * 2)) / 2f, y = h - 320 * u;

            string more = _player.QueuedOffers.Value > 0 ? $"   ({_player.QueuedOffers.Value} more waiting)" : "";
            Ui.Label(new Rect(0, y - 42 * u, w, 36 * u), (empowered ? $"EMPOWERED: PICK A POWER-UP (+{levels} levels)" : "PICK A POWER-UP") + more, Ui.Bold, accent, TextAnchor.MiddleCenter);
            for (int card = 0; card < 3; card++)
            {
                int id = _player.OfferedPowerUp(card);
                var p = upgrades.PowerUpAt(id);
                if (p == null) continue;
                int now = _player.PowerLevel(id), next = now + levels;
                var r = new Rect(x0 + card * (cw + gap), y, cw, ch);
                Ui.PanelBox(r, accent, u, 0.95f);
                Ui.Label(new Rect(r.x, r.y + 14 * u, cw, 34 * u), $"{p.name} {NetPlayer.ToRoman(next)}", Ui.Bold, Ui.Text, TextAnchor.UpperCenter);
                string change = now > 0 ? $"{p.Describe(now)}  →  {p.Describe(next)}" : p.Describe(next);
                var body = Ui.Small;
                bool wrap = body.wordWrap;
                body.wordWrap = true;
                Ui.Label(new Rect(r.x + 14 * u, r.y + 52 * u, cw - 28 * u, 56 * u), change, body, Ui.Muted, TextAnchor.UpperCenter);
                body.wordWrap = wrap;
                Ui.Key(new Vector2(r.center.x - 13 * u, r.yMax - 38 * u), (card + 1).ToString(), 26 * u);
            }
            if (_player.PowerRerolls.Value > 0)
                Ui.Prompt(w / 2f, y + ch + 10 * u, "4", $"redraw the cards ({_player.PowerRerolls.Value} left)", Ui.Small, Ui.Text, u);
        }

        // ---------------------------------------------------------------- tips and subtitles

        void DrawTip(float w, float h, float u)
        {
            string tip = Tips.Current;
            if (string.IsNullOrEmpty(tip)) return;
            float a = Mathf.Clamp01(Tips.CurrentAge / 0.3f) * Mathf.Clamp01((1f - Tips.CurrentFraction) * 7f / 0.5f);
            float pw = 440 * u;
            var style = Ui.Small;
            bool wrap = style.wordWrap;
            style.wordWrap = true;
            float th = style.CalcHeight(new GUIContent(tip), pw - 40 * u);
            var r = new Rect(36 * u, h * 0.42f, pw, th + 56 * u);
            Ui.PanelBox(r, Ui.Amber, u, 0.9f * a);
            Ui.Label(new Rect(r.x + 20 * u, r.y + 12 * u, pw, 22 * u), "TIP", Ui.Tiny, Ui.A(Ui.Amber, a), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(r.x + 20 * u, r.y + 36 * u, pw - 40 * u, th), tip, style, Ui.A(Ui.Text, a), TextAnchor.UpperLeft);
            style.wordWrap = wrap;
            Ui.Fill(new Rect(r.x + 10 * u, r.yMax - 4 * u, (pw - 20 * u) * (1f - Tips.CurrentFraction), 2 * u), Ui.A(Ui.Amber, 0.6f * a));
        }

        void DrawSubtitles(float w, float h, float u)
        {
            var lines = Subtitles.Lines;
            if (lines.Count == 0) return;
            float y = h - 290 * u;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (!line.radio && !GameSettings.Subtitles) continue;
                // Radio calls always show, so the story is never missed; other lines follow the Subtitles setting.
                float a = line.Alpha;
                string speaker = line.speaker.ToUpperInvariant();
                string text = $"<b>{speaker}</b>  {line.text}";
                var style = Ui.Small;
                float tw = Mathf.Min(w * 0.6f, Ui.Width(text, style) + 40 * u);
                bool wrap = style.wordWrap;
                style.wordWrap = true;
                float th = style.CalcHeight(new GUIContent(text), tw - 40 * u) + 14 * u;
                var r = new Rect((w - tw) / 2f, y - th, tw, th);
                Ui.RoundRect(r, new Color(0f, 0f, 0f, 0.55f * a));
                if (line.radio) Ui.Fill(new Rect(r.x, r.y + 6 * u, 3 * u, r.height - 12 * u), Ui.A(line.color, a));
                Ui.Label(new Rect(r.x + 20 * u, r.y + 7 * u, tw - 40 * u, th - 14 * u), text, style, Ui.A(Color.Lerp(Ui.Text, line.color, 0.35f), a), TextAnchor.MiddleCenter);
                style.wordWrap = wrap;
                y -= th + 6 * u;
            }
        }

        // ---------------------------------------------------------------- world markers

        void DrawNameTags(float u)
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == _player || p.State.Value == LifeState.Dead) continue;
                bool downed = p.State.Value == LifeState.Downed;
                Vector3 world = p.transform.position + Vector3.up * (downed ? 0.9f : 2.1f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                string text = downed ? $"{p.TaggedName}  ·  DOWN" : p.TaggedName;
                Ui.Label(new Rect(screen.x - 200 * u, Screen.height - screen.y - 14 * u, 400 * u, 28 * u), text, Ui.Tiny, downed ? Ui.Bad : Ui.Text, TextAnchor.MiddleCenter);
                if (downed)
                {
                    var icon = new Rect(screen.x - 10 * u, Screen.height - screen.y - 42 * u, 20 * u, 20 * u);
                    Ui.RoundRect(icon, Ui.A(Ui.Bad, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f)), small: true);
                    Ui.Label(icon, "+", Ui.Bold, Color.white, TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>A marker over the survivor while a protect mission is running, visible through walls.</summary>
        void DrawSurvivorMarker(float u)
        {
            var board = MissionBoard.Instance;
            var cam = Camera.main;
            if (board == null || cam == null || !board.TryGetSurvivor(out var at)) return;
            Vector3 screen = cam.WorldToScreenPoint(at + Vector3.up * 1.9f);
            if (screen.z <= 0f) return;
            float dist = Vector3.Distance(cam.transform.position, at);
            float x = Mathf.Clamp(screen.x, 40 * u, Screen.width - 40 * u), y = Mathf.Clamp(Screen.height - screen.y, 40 * u, Screen.height - 40 * u);
            var icon = new Rect(x - 11 * u, y - 30 * u, 22 * u, 22 * u);
            Ui.RoundRect(icon, Ui.A(Ui.Good, 0.85f), small: true);
            Ui.Label(icon, "!", Ui.Bold, new Color(0.05f, 0.05f, 0.06f), TextAnchor.MiddleCenter);
            Ui.Label(new Rect(x - 100 * u, y - 6 * u, 200 * u, 22 * u), $"SURVIVOR  {dist:0}m", Ui.Tiny, Ui.Good, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- crosshair and feedback

        void DrawCrosshair(float w, float h, float u)
        {
            float aim = _gun.AimAmount;
            if (_gun.Scoped) DrawScope(w, h, u, aim);
            if (aim > 0.85f) return; // Iron sights take over.
            float alpha = 1f - aim;
            // Gap tracks real spread: degrees to pixels at the current FOV.
            var cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 75f;
            float gap = Mathf.Max(4f * u, _gun.CurrentSpread * (h / fov));
            float len = 10f * u, thick = Mathf.Max(2f, 2f * u);
            float cx = w / 2f, cy = h / 2f;
            var c = new Color(1f, 1f, 1f, 0.9f * alpha);
            var s = new Color(0f, 0f, 0f, 0.5f * alpha);
            for (int pass = 0; pass < 2; pass++)
            {
                var col = pass == 0 ? s : c;
                float o = pass == 0 ? 1f : 0f;
                Ui.Fill(new Rect(cx - thick / 2 + o, cy - gap - len + o, thick, len), col);
                Ui.Fill(new Rect(cx - thick / 2 + o, cy + gap + o, thick, len), col);
                Ui.Fill(new Rect(cx - gap - len + o, cy - thick / 2 + o, len, thick), col);
                Ui.Fill(new Rect(cx + gap + o, cy - thick / 2 + o, len, thick), col);
            }
            Ui.Fill(new Rect(cx - 1f, cy - 1f, 2f, 2f), c);
        }

        static Texture2D s_ScopeMask;

        /// <summary>A round scope view: black outside a lens with a soft dark rim.</summary>
        static Texture2D ScopeMask
        {
            get
            {
                if (s_ScopeMask != null) return s_ScopeMask;
                const int n = 512;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Clear in the middle, darkening towards the rim, solid black past it.
                    float a = Mathf.Clamp01((r - 0.8f) / 0.17f);
                    a = r >= 0.97f ? 1f : a * a * 0.85f;
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
                t.SetPixels32(px);
                t.Apply();
                return s_ScopeMask = t;
            }
        }

        /// <summary>Looking through a scope: the lens fills the middle of the screen with a fine crosshair and a red dot.</summary>
        void DrawScope(float w, float h, float u, float aim)
        {
            float eased = aim * aim * (3f - 2f * aim);
            if (eased < 0.9f) return; // the gun's own scope tube shows until then (WeaponView)
            float d = h * 0.94f, cx = w / 2f, cy = h / 2f;
            var lens = new Rect(cx - d / 2f, cy - d / 2f, d, d);
            GUI.DrawTexture(lens, ScopeMask);
            var black = Color.black;
            Ui.Fill(new Rect(0f, 0f, lens.xMin + 1f, h), black);
            Ui.Fill(new Rect(lens.xMax - 1f, 0f, w - lens.xMax + 1f, h), black);
            Ui.Fill(new Rect(lens.xMin, 0f, d, lens.yMin + 1f), black);
            Ui.Fill(new Rect(lens.xMin, lens.yMax - 1f, d, h - lens.yMax + 1f), black);
            // Thick posts from the rim, thin hairs across the middle, and a small lit dot.
            var ink = new Color(0.02f, 0.02f, 0.02f, 0.95f);
            float thin = Mathf.Max(1f, 1.2f * u), thick = Mathf.Max(3f, 4f * u), gap = d * 0.16f;
            Ui.Fill(new Rect(lens.xMin, cy - thick / 2f, d / 2f - gap, thick), ink);
            Ui.Fill(new Rect(cx + gap, cy - thick / 2f, d / 2f - gap, thick), ink);
            Ui.Fill(new Rect(cx - thick / 2f, cy + gap, thick, d / 2f - gap), ink);
            Ui.Fill(new Rect(cx - gap, cy - thin / 2f, gap * 2f, thin), ink);
            Ui.Fill(new Rect(cx - thin / 2f, lens.yMin, thin, d / 2f + gap), ink);
            for (int i = 1; i <= 4; i++)
            {
                float y = cy + i * gap * 0.22f;
                Ui.Fill(new Rect(cx - (i % 2 == 0 ? 8f : 5f) * u, y, (i % 2 == 0 ? 16f : 10f) * u, thin), ink);
            }
            float dot = Mathf.Max(3f, 3.5f * u);
            Ui.Fill(new Rect(cx - dot / 2f, cy - dot / 2f, dot, dot), new Color(1f, 0.15f, 0.1f, 0.95f));
        }

        void DrawHitmarker(float w, float h, float u)
        {
            float t = Time.time - _hitTime;
            float duration = _hitKilled ? 0.3f : 0.15f;
            if (t > duration) return;
            float a = 1f - t / duration;
            Color c = _hitKilled ? new Color(1f, 0.2f, 0.15f, a) : _hitHead ? new Color(1f, 0.85f, 0.3f, a) : new Color(1f, 1f, 1f, a);
            float size = (_hitKilled ? 16f : 11f) * u;
            float inner = 6f * u;
            var center = new Vector2(w / 2f, h / 2f);
            var old = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + 90f * i, center);
                Ui.Fill(new Rect(center.x - 1.5f * u, center.y - inner - size, 3f * u, size), c);
                GUI.matrix = old;
            }
        }

        void DrawDamageVignette(float w, float h)
        {
            float recent = Mathf.Clamp01(1f - (Time.time - _player.LocalLastDamageTime) / 0.6f);
            float low = _player.IsAlive ? Mathf.Clamp01(1f - _player.Health.Value / _player.MaxHealthNow / 0.5f) : 0f;
            float a = Mathf.Max(recent * 0.35f, low * 0.3f * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f)));
            if (a <= 0.001f) return;
            var c = new Color(0.6f, 0f, 0f, a);
            float edge = h * 0.12f;
            Ui.Fill(new Rect(0, 0, w, edge), c);
            Ui.Fill(new Rect(0, h - edge, w, edge), c);
            Ui.Fill(new Rect(0, edge, edge, h - edge * 2), c);
            Ui.Fill(new Rect(w - edge, edge, edge, h - edge * 2), c);
            Ui.Fill(new Rect(0, 0, w, h), new Color(0.5f, 0f, 0f, a * 0.25f));
        }
    }
}
