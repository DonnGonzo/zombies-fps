using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Zombies;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// Throwaway immediate-mode HUD for the prototype: round counter and banners, points, crosshair,
    /// hitmarkers, ammo, health, squad list, name tags, revive prompts, downed/game-over screens and
    /// debug help. The real HUD is UI Toolkit later.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        NetPlayer _player;
        HitscanGun _gun;

        Texture2D _white;
        GUIStyle _label, _big, _small, _center, _button;
        float _hitTime = -100f;
        bool _hitKilled, _hitHead;
        bool _showHelp = true;
        AudioSource _audio;

        // Round banners and the points pop-up, driven by watching the round manager's state.
        GamePhase _lastPhase = GamePhase.Intermission;
        int _lastRound;
        string _banner, _bannerSub;
        float _bannerTime = -100f;
        long _lastPoints = -1;
        long _pointsGain;
        float _pointsGainTime = -100f;
        float _lastPickTime = -100f;
        float _lastLevelUpTime = -100f;

        public void Init(NetPlayer player)
        {
            _player = player;
            _gun = player.Gun;
            _gun.Hit += OnHit;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            RoundManager.BossDefeated += OnBossDefeated;
            MissionBoard.MissionCompleted += OnMissionCompleted;
        }

        void OnDestroy()
        {
            if (_gun != null) _gun.Hit -= OnHit;
            RoundManager.BossDefeated -= OnBossDefeated;
            MissionBoard.MissionCompleted -= OnMissionCompleted;
        }

        void OnMissionCompleted(int id)
        {
            var def = GameConfig.Missions != null ? GameConfig.Missions.At(id) : null;
            var board = MissionBoard.Instance;
            if (def == null || board == null) return;
            ShowBanner("MISSION COMPLETE", $"{def.title}: {RewardText(def, board.SetRound.Value)} for everyone");
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        static string RewardText(MissionDef def, int round)
        {
            string bonus = def.bonus == MissionBonus.MaxAmmo ? " and full ammo" : def.bonus == MissionBonus.PowerUpReroll ? " and a power-up reroll" : "";
            return $"+{def.PointsFor(round):N0} pts{bonus}";
        }

        void OnBossDefeated(long bonus)
        {
            ShowBanner($"{GameConfig.ZombieType(ZombieKind.Boss).displayName.ToUpperInvariant()} DOWN", $"+{bonus:N0} pts for everyone");
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundEnd, 0.9f);
        }

        void OnHit(bool killed, bool headshot)
        {
            _hitTime = Time.time;
            _hitKilled = killed;
            _hitHead = headshot;
        }

        void Update()
        {
            if (_player == null)
            {
                Destroy(gameObject);
                return;
            }
            if (GameInput.DebugPressed(DebugKey.ToggleHelp)) _showHelp = !_showHelp;
            WatchRounds();
            WatchPoints();
            WatchPowerUps();
        }

        void WatchPowerUps()
        {
            if (_player.LevelUpTime > _lastLevelUpTime)
            {
                _lastLevelUpTime = _player.LevelUpTime;
                ShowBanner($"LEVEL {_player.LevelUpLevel}", UnlockLine(_player.LevelUpLevel));
                if (_audio != null) _audio.PlayOneShot(ProceduralAudio.RoundStart, 0.7f);
            }
            if (_player.LastPickTime <= _lastPickTime) return;
            _lastPickTime = _player.LastPickTime;
            ShowBanner("POWER-UP", _player.LastPickText);
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
                string sub = round == 1 ? "Here they come."
                    : GameConfig.Round.IsBossRound(round) ? $"Boss round: shoot the glowing core on the {boss}'s back."
                    : NewTypeLine(round);
                ShowBanner($"ROUND {round}", sub);
                _audio.PlayOneShot(ProceduralAudio.RoundStart, 0.9f);
            }
            else if (phase == GamePhase.Intermission && _lastPhase == GamePhase.RoundActive)
            {
                ShowBanner($"ROUND {round} CLEARED", $"+{GameConfig.Round.RoundBonus(round):N0} pts for everyone");
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
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < shop.Count; i++)
                if (shop.At(i).unlockLevel == level) names.Add(shop.At(i).name);
            return names.Count > 0 ? "Unlocked in the spawn shop: " + string.Join(", ", names) : null;
        }

        void ShowBanner(string title, string sub)
        {
            _banner = title;
            _bannerSub = sub;
            _bannerTime = Time.time;
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

        void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            float scale = Screen.height / 1080f;
            int size = Mathf.Max(12, Mathf.RoundToInt(24 * scale));
            if (_label != null && _label.fontSize == size) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(size * 2.4f), alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(size * 0.75f), fontStyle = FontStyle.Normal };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = size, fixedHeight = 48 * scale };
        }

        void Fill(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), _white);
            GUI.color = old;
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * old.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        void OnGUI()
        {
            if (_player == null || _gun == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height, u = h / 1080f;
            var rounds = RoundManager.Instance;
            var state = _player.State.Value;

            DrawNameTags(u);
            DrawDamageVignette(w, h);

            if (rounds != null && rounds.IsGameOver)
            {
                DrawGameOver(w, h, u, rounds);
                DrawLeaveButton(w, h, u, alwaysShow: true);
                return;
            }

            if (state == LifeState.Alive)
            {
                DrawCrosshair(w, h, u);
                DrawHitmarker(w, h, u);
                DrawAmmo(w, h, u);
                DrawRevivePrompt(w, h, u);
                DrawBuyPrompt(w, h, u);
            }
            else if (state == LifeState.Downed)
            {
                Fill(0, 0, w, h, new Color(0.25f, 0f, 0f, 0.35f));
                Shadowed(new Rect(0, h * 0.3f, w, 100 * u), "DOWNED", _big);
                Shadowed(new Rect(0, h * 0.3f + 100 * u, w, 40 * u),
                    $"Bleeding out in {Mathf.CeilToInt(_player.BleedOutRemaining.Value)}s. A teammate can revive you.", _center);
                DrawProgress(w, h * 0.3f + 150 * u, u, _player.ReviveProgress.Value, "BEING REVIVED");
            }
            else
            {
                Fill(0, 0, w, h, new Color(0f, 0f, 0f, 0.4f));
                Shadowed(new Rect(0, h * 0.3f, w, 100 * u), "BLED OUT", _big);
                Shadowed(new Rect(0, h * 0.3f + 100 * u, w, 40 * u), "You're back when your squad clears this round.", _center);
            }

            DrawRound(w, h, u, rounds);
            DrawBanner(w, h, u);
            DrawHealth(h, u);
            DrawStats(u, rounds);
            DrawSquad(w, u);
            DrawPowerUps(h, u);
            DrawPowerUpOffer(w, h, u);
            DrawMissions(w, u);

            if (_showHelp)
            {
                var r = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                string host = rounds != null && rounds.IsServer ? "\nTest mode (host):  = next round · ] jump 10 rounds · G god mode · P +5,000 pts · O power-up · B boss" : "";
                Shadowed(new Rect(w - 800 * u, 30 * u, 760 * u, 260 * u),
                    "WASD move · Shift sprint · Space jump\nLMB fire · RMB aim · R reload · Q / wheel switch gun\nE buy · hold E revive · 1/2/3 pick power-up · 4 redraw" + host + "\nEsc menu · H hide help",
                    r);
            }

            if (!_player.Look.CursorLocked)
            {
                Fill(0, 0, w, h, new Color(0f, 0f, 0f, 0.45f));
                Shadowed(new Rect(0, h * 0.38f, w, 80 * u), "CLICK TO PLAY", _big);
                DrawLeaveButton(w, h, u, alwaysShow: false);
            }
        }

        void DrawLeaveButton(float w, float h, float u, bool alwaysShow)
        {
            if (!alwaysShow && _player.Look.CursorLocked) return;
            bool hosting = RoundManager.Instance != null && RoundManager.Instance.IsServer;
            float bw = 320 * u;
            if (GUI.Button(new Rect((w - bw) / 2f, h * 0.62f, bw, 48 * u), hosting ? "End game for everyone" : "Leave game", _button))
                PrototypeBootstrap.ReturnToMenu(hosting ? "You ended the game." : "You left the game.");
        }

        void DrawGameOver(float w, float h, float u, RoundManager rounds)
        {
            Fill(0, 0, w, h, new Color(0.15f, 0f, 0f, 0.6f));
            Shadowed(new Rect(0, h * 0.2f, w, 120 * u), rounds.Won.Value ? "YOU MADE IT" : "GAME OVER", _big);
            int round = rounds.DisplayRound;
            string survived = rounds.Won.Value ? $"You survived all {round} rounds."
                : round == 1 ? "You were overrun on round 1." : $"You were overrun on round {round}.";
            Shadowed(new Rect(0, h * 0.2f + 120 * u, w, 40 * u), survived, _center);
            float y = h * 0.2f + 190 * u;
            foreach (var p in NetPlayer.All)
            {
                Shadowed(new Rect(0, y, w, 36 * u), $"{p.TaggedName}   ·   {p.Points.Value:N0} pts   ·   {p.Kills.Value} kills   ·   {p.Headshots.Value} headshots", _center);
                y += 38 * u;
            }
            bool hosting = rounds.IsServer;
            Shadowed(new Rect(0, y + 20 * u, w, 40 * u), hosting ? "Press Enter to restart" : "Waiting for the host to restart", _center);
            PlayerLookUnlock();
        }

        static void PlayerLookUnlock()
        {
            if (Cursor.lockState != CursorLockMode.None) ZombiesFPS.Player.PlayerLook.SetCursorLocked(false);
        }

        void DrawAmmo(float w, float h, float u)
        {
            var right = new GUIStyle(_label) { alignment = TextAnchor.LowerRight };
            var bigRight = new GUIStyle(_big) { alignment = TextAnchor.LowerRight };
            int quarter = _gun.MagazineCapacity / 4;
            bigRight.normal.textColor = _gun.Magazine == 0 ? new Color(1f, 0.3f, 0.25f) : _gun.Magazine <= quarter ? new Color(1f, 0.8f, 0.3f) : Color.white;
            Shadowed(new Rect(w - 420 * u, h - 140 * u, 260 * u, 90 * u), _gun.Magazine.ToString(), bigRight);
            Shadowed(new Rect(w - 160 * u, h - 112 * u, 120 * u, 50 * u), "/ " + _gun.Reserve, right);
            Shadowed(new Rect(w - 520 * u, h - 52 * u, 480 * u, 40 * u), _gun.DisplayName ?? _gun.Data.displayName, right);
            int other = 1 - _player.ActiveSlot.Value;
            var otherGun = GameConfig.GunById(_player.GunInSlot(other));
            if (otherGun != null)
            {
                var dim = new GUIStyle(_small) { alignment = TextAnchor.LowerRight };
                dim.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                Shadowed(new Rect(w - 520 * u, h - 176 * u, 480 * u, 30 * u), $"Q  {_player.GunName(other)}", dim);
            }

            var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            if (_gun.IsReloading) DrawProgress(w, h * 0.58f, u, _gun.ReloadProgress, "RELOADING");
            else if (_gun.Magazine == 0 && _gun.Reserve == 0) Shadowed(new Rect(0, h * 0.58f, w, 30 * u), "NO AMMO", c);
            else if (_gun.Magazine <= quarter) Shadowed(new Rect(0, h * 0.58f, w, 30 * u), "R  RELOAD", c);
        }

        void DrawProgress(float w, float y, float u, float progress, string label)
        {
            float bw = 220 * u;
            Fill(w / 2 - bw / 2, y, bw, 6 * u, new Color(0f, 0f, 0f, 0.5f));
            Fill(w / 2 - bw / 2, y, bw * Mathf.Clamp01(progress), 6 * u, Color.white);
            var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            Shadowed(new Rect(0, y + 8 * u, w, 30 * u), label, c);
        }

        void DrawRevivePrompt(float w, float h, float u)
        {
            var target = _player.FindReviveTarget(_player.transform.position);
            if (target == null) return;
            var c = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            Shadowed(new Rect(0, h * 0.64f, w, 36 * u), $"Hold E to revive {target.DisplayName}", c);
            if (target.ReviveProgress.Value > 0f) DrawProgress(w, h * 0.64f + 40 * u, u, target.ReviveProgress.Value, "REVIVING");
        }

        void DrawBuyPrompt(float w, float h, float u)
        {
            var b = _player.FocusedBuyable;
            if (b == null) return;
            if (b.Kind == ZombiesFPS.World.BuyableKind.Shop)
            {
                if (_player.ShopOpen) DrawShop(w, h, u);
                else Shadowed(new Rect(0, h * 0.64f, w, 36 * u), "Press E to browse the shop", new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            long price = _player.PriceFor(b, out string what);
            if (price < 0)
            {
                // Something there, but not for sale right now (a maxed gun, a recharging machine).
                if (string.IsNullOrEmpty(what)) return;
                var dim = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                dim.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                Shadowed(new Rect(0, h * 0.64f, w, 36 * u), what, dim);
                return;
            }
            bool afford = _player.Points.Value >= price;
            var c = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            if (!afford) c.normal.textColor = new Color(1f, 0.45f, 0.35f);
            Shadowed(new Rect(0, h * 0.64f, w, 36 * u), $"Press E to {what}  [{price:N0}]", c);
            if (!afford)
            {
                var s = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, h * 0.64f + 36 * u, w, 30 * u), $"You need {price - _player.Points.Value:N0} more points", s);
            }
        }

        void DrawHealth(float h, float u)
        {
            float hbw = 320 * u, hbh = 14 * u, hx = 40 * u, hy = h - 60 * u;
            float n = Mathf.Clamp01(_player.Health.Value / _player.MaxHealthNow);
            Fill(hx - 2, hy - 2, hbw + 4, hbh + 4, new Color(0f, 0f, 0f, 0.5f));
            Fill(hx, hy, hbw * n, hbh, Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.85f, 0.85f, 0.85f), n));
            Shadowed(new Rect(hx, hy - 40 * u, hbw, 36 * u), Mathf.CeilToInt(_player.Health.Value).ToString(), _label);
        }

        void DrawStats(float u, RoundManager rounds)
        {
            Shadowed(new Rect(40 * u, 30 * u, 600 * u, 40 * u), $"{_player.Points.Value:N0} pts", _label);
            float gainAge = Time.time - _pointsGainTime;
            if (gainAge < 1.2f)
            {
                var gain = new GUIStyle(_label);
                gain.normal.textColor = new Color(1f, 0.85f, 0.3f, 1f - Mathf.Clamp01((gainAge - 0.6f) / 0.6f));
                Shadowed(new Rect(40 * u + _label.CalcSize(new GUIContent($"{_player.Points.Value:N0} pts")).x + 16 * u, 30 * u - gainAge * 12 * u, 300 * u, 40 * u),
                    $"+{_pointsGain:N0}", gain);
            }
            var shopData = GameConfig.Shop;
            int level = _player.Level;
            string xp = shopData != null && level < shopData.maxLevel ? $" ({shopData.XpIntoLevel(_player.TotalXp):N0}/{shopData.XpToNext(level):N0} XP)" : "";
            Shadowed(new Rect(40 * u, 66 * u, 900 * u, 30 * u), $"Level {level}{xp}   Kills {_player.Kills.Value}   Headshots {_player.Headshots.Value}", _small);
            if (rounds != null && rounds.IsServer)
                Shadowed(new Rect(40 * u, 94 * u, 900 * u, 30 * u),
                    $"Host: zombie HP {rounds.CurrentZombieHealth:N0}  ·  {rounds.CurrentSpeedTier}  ·  alive {rounds.AliveZombies.Value}/{rounds.LiveCap}" + (rounds.GodMode.Value ? "  ·  GOD MODE" : ""),
                    _small);
        }

        void DrawRound(float w, float h, float u, RoundManager rounds)
        {
            if (rounds == null) return;
            var top = new GUIStyle(_label) { alignment = TextAnchor.UpperCenter };
            if (rounds.Phase.Value == GamePhase.Intermission)
            {
                int next = rounds.Round.Value + 1;
                Shadowed(new Rect(0, 30 * u, w, 40 * u), $"Round {next} starts in {Mathf.CeilToInt(rounds.PhaseTimeLeft)}", top);
            }
            else
            {
                Shadowed(new Rect(0, 30 * u, w, 40 * u), rounds.ZombiesLeft.Value == 1 ? "1 zombie left" : $"{rounds.ZombiesLeft.Value} zombies left", top);
            }
            if (rounds.GodMode.Value)
            {
                var god = new GUIStyle(_small) { alignment = TextAnchor.UpperCenter };
                god.normal.textColor = new Color(1f, 0.85f, 0.3f);
                Shadowed(new Rect(0, 68 * u, w, 30 * u), "GOD MODE (test)", god);
            }

            DrawBossBars(w, u);

            if (rounds.Round.Value <= 0) return;
            var big = new GUIStyle(_big) { alignment = TextAnchor.LowerLeft, fontSize = Mathf.RoundToInt(_big.fontSize * 1.3f) };
            big.normal.textColor = new Color(0.75f, 0.08f, 0.05f);
            Shadowed(new Rect(36 * u, h - 230 * u, 400 * u, 130 * u), rounds.Round.Value.ToString(), big);
        }

        void DrawBanner(float w, float h, float u)
        {
            const float Duration = 3.2f;
            float t = Time.time - _bannerTime;
            if (t > Duration || string.IsNullOrEmpty(_banner)) return;
            float a = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((Duration - t) / 0.6f);
            var title = new GUIStyle(_big);
            title.normal.textColor = new Color(0.85f, 0.12f, 0.08f, a);
            Shadowed(new Rect(0, h * 0.2f, w, 110 * u), _banner, title);
            if (string.IsNullOrEmpty(_bannerSub)) return;
            var sub = new GUIStyle(_center);
            sub.normal.textColor = new Color(1f, 1f, 1f, a);
            Shadowed(new Rect(0, h * 0.2f + 110 * u, w, 40 * u), _bannerSub, sub);
        }

        /// <summary>The spawn shop menu: attachments for the gun in hand, unlocked by level, bought with points.</summary>
        void DrawShop(float w, float h, float u)
        {
            var shop = GameConfig.Shop;
            if (shop == null) return;
            float pw = 980 * u, rowH = 30 * u, ph = 96 * u + rowH * shop.Count;
            float x = (w - pw) / 2f, y = h * 0.5f - ph / 2f + 40 * u;
            Fill(x, y, pw, ph, new Color(0.04f, 0.06f, 0.08f, 0.88f));
            Fill(x, y, pw, 4 * u, new Color(0.95f, 0.8f, 0.35f));
            var title = new GUIStyle(_label) { alignment = TextAnchor.UpperLeft };
            Shadowed(new Rect(x + 20 * u, y + 12 * u, pw, 34 * u), $"SPAWN SHOP  ·  {_player.GunName(_player.ActiveSlot.Value)}", title);
            var sub = new GUIStyle(_small) { alignment = TextAnchor.UpperLeft };
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            Shadowed(new Rect(x + 20 * u, y + 46 * u, pw, 26 * u), $"Level {_player.Level}  ·  number key to buy  ·  Q switch gun  ·  E close", sub);
            float ry = y + 80 * u;
            var row = new GUIStyle(_small) { alignment = TextAnchor.UpperLeft };
            var right = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
            for (int i = 0; i < shop.Count; i++)
            {
                var a = shop.At(i);
                var status = _player.StatusOf(i);
                Color c = status == AttachmentStatus.Locked ? new Color(1f, 1f, 1f, 0.35f)
                    : status == AttachmentStatus.Owned ? new Color(0.5f, 1f, 0.55f)
                    : _player.Points.Value >= a.price ? Color.white : new Color(1f, 0.55f, 0.45f);
                row.normal.textColor = c;
                right.normal.textColor = c;
                Shadowed(new Rect(x + 20 * u, ry, pw - 220 * u, rowH), $"{i + 1}  {a.name}  ({a.slot})  ·  {a.description}", row);
                string state = status == AttachmentStatus.Locked ? $"Level {a.unlockLevel}" : status == AttachmentStatus.Owned ? "Fitted" : $"{a.price:N0}";
                Shadowed(new Rect(x + pw - 220 * u, ry, 200 * u, rowH), state, right);
                ry += rowH;
            }
        }

        /// <summary>The current mission set, top right under the help text.</summary>
        void DrawMissions(float w, float u)
        {
            var board = MissionBoard.Instance;
            var data = GameConfig.Missions;
            if (board == null || data == null || board.SetRound.Value <= 0) return;
            float x = w - 640 * u, y = 300 * u, lw = 600 * u;
            var head = new GUIStyle(_small) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            head.normal.textColor = new Color(0.6f, 0.85f, 1f);
            int next = data.NextRefreshRound(board.SetRound.Value);
            Shadowed(new Rect(x, y, lw, 26 * u), $"MISSIONS  ·  new set at round {next}", head);
            y += 28 * u;
            var line = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = board.DefIn(i);
                if (def == null) continue;
                ulong slot = board.SlotValue(i);
                bool done = MissionData.DoneOf(slot);
                int target = MissionData.TargetOf(slot);
                string where = def.objective == MissionObjective.HoldGenerator ? $" in the {ZombiesFPS.World.ZoneMap.ZoneNames[MissionData.ZoneOf(slot)]}" : "";
                string state = done ? "DONE" : $"{MissionData.ProgressOf(slot)}/{target}";
                line.normal.textColor = done ? new Color(0.5f, 1f, 0.55f) : Color.white;
                Shadowed(new Rect(x, y, lw, 26 * u), $"{def.title}: {def.Describe(target)}{where}   {state}", line);
                y += 24 * u;
                if (!done)
                {
                    var reward = new GUIStyle(line);
                    reward.normal.textColor = new Color(1f, 0.85f, 0.4f, 0.85f);
                    Shadowed(new Rect(x, y, lw, 24 * u), RewardText(def, board.SetRound.Value), reward);
                    y += 24 * u;
                }
                y += 4 * u;
            }
        }

        void DrawBossBars(float w, float u)
        {
            float bw = 600 * u, bh = 12 * u, y = 100 * u;
            var label = new GUIStyle(_small) { alignment = TextAnchor.LowerCenter };
            label.normal.textColor = new Color(1f, 0.6f, 0.3f);
            foreach (var boss in NetZombie.Bosses)
            {
                if (boss == null || !boss.IsAlive) continue;
                float n = Mathf.Clamp01(boss.HealthFraction.Value);
                Shadowed(new Rect(0, y, w, 26 * u), boss.Data.displayName.ToUpperInvariant(), label);
                y += 28 * u;
                Fill((w - bw) / 2f - 2, y - 2, bw + 4, bh + 4, new Color(0f, 0f, 0f, 0.6f));
                Fill((w - bw) / 2f, y, bw * n, bh, new Color(0.85f, 0.35f, 0.08f));
                y += bh + 10 * u;
            }
        }

        /// <summary>Owned power-ups and their levels, above the health bar.</summary>
        void DrawPowerUps(float h, float u)
        {
            var upgrades = GameConfig.Upgrades;
            if (upgrades == null || _player.PowerLevels.Value == 0) return;
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < upgrades.PowerUpCount; i++)
            {
                int level = _player.PowerLevel(i);
                if (level > 0) parts.Add($"{upgrades.PowerUpAt(i).name} {NetPlayer.ToRoman(level)}");
            }
            var style = new GUIStyle(_small) { wordWrap = true, alignment = TextAnchor.LowerLeft };
            style.normal.textColor = new Color(0.85f, 0.75f, 1f);
            Shadowed(new Rect(40 * u, h - 300 * u, 700 * u, 60 * u), string.Join("  ·  ", parts), style);
        }

        /// <summary>The three power-up cards to pick from after every few rounds.</summary>
        void DrawPowerUpOffer(float w, float h, float u)
        {
            var upgrades = GameConfig.Upgrades;
            if (upgrades == null || !_player.HasPowerUpOffer) return;
            bool empowered = _player.OfferIsEmpowered;
            int levels = _player.OfferLevels;
            float cw = 300 * u, ch = 130 * u, gap = 20 * u;
            float x0 = (w - (cw * 3 + gap * 2)) / 2f, y = h - 290 * u;

            var head = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            head.normal.textColor = empowered ? new Color(1f, 0.8f, 0.3f) : new Color(0.85f, 0.75f, 1f);
            if (_player.PowerRerolls.Value > 0)
            {
                var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                Shadowed(new Rect(0, y + ch + 6 * u, w, 26 * u), $"Press 4 to redraw the cards ({_player.PowerRerolls.Value} left)", hint);
            }
            string more = _player.QueuedOffers.Value > 0 ? $"   ({_player.QueuedOffers.Value} more waiting)" : "";
            Shadowed(new Rect(0, y - 40 * u, w, 36 * u), (empowered ? $"EMPOWERED: PICK A POWER-UP (+{levels} levels)" : "PICK A POWER-UP") + more, head);

            var title = new GUIStyle(_label) { alignment = TextAnchor.UpperCenter };
            var body = new GUIStyle(_small) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            var key = new GUIStyle(_small) { alignment = TextAnchor.LowerCenter };
            key.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            for (int card = 0; card < 3; card++)
            {
                var p = upgrades.PowerUpAt(_player.OfferedPowerUp(card));
                if (p == null) continue;
                int id = _player.OfferedPowerUp(card);
                int now = _player.PowerLevel(id), next = now + levels;
                float x = x0 + card * (cw + gap);
                Fill(x, y, cw, ch, new Color(0.08f, 0.05f, 0.12f, 0.8f));
                Fill(x, y, cw, 4 * u, head.normal.textColor);
                Shadowed(new Rect(x, y + 12 * u, cw, 34 * u), $"{p.name} {NetPlayer.ToRoman(next)}", title);
                string change = now > 0 ? $"{p.Describe(now)}  ->  {p.Describe(next)}" : p.Describe(next);
                Shadowed(new Rect(x + 12 * u, y + 46 * u, cw - 24 * u, 50 * u), change, body);
                Shadowed(new Rect(x, y + ch - 30 * u, cw, 26 * u), $"Press {card + 1}", key);
            }
        }

        void DrawSquad(float w, float u)
        {
            float y = 140 * u;
            foreach (var p in NetPlayer.All)
            {
                if (p == _player) continue;
                string status = p.State.Value == LifeState.Alive ? $"{Mathf.CeilToInt(p.Health.Value)} hp"
                    : p.State.Value == LifeState.Downed ? $"DOWN {Mathf.CeilToInt(p.BleedOutRemaining.Value)}s" : "OUT";
                var style = new GUIStyle(_small);
                style.normal.textColor = p.State.Value == LifeState.Alive ? Color.white : new Color(1f, 0.4f, 0.3f);
                Fill(40 * u, y + 6 * u, 10 * u, 10 * u, p.SuitColor);
                Shadowed(new Rect(58 * u, y, 600 * u, 26 * u), $"{p.TaggedName} (Lv {p.Level})  ·  {status}  ·  {p.Points.Value:N0} pts", style);
                y += 26 * u;
            }
        }

        void DrawNameTags(float u)
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == _player || p.State.Value == LifeState.Dead) continue;
                Vector3 world = p.transform.position + Vector3.up * (p.State.Value == LifeState.Downed ? 0.9f : 2.1f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = p.State.Value == LifeState.Downed ? new Color(1f, 0.4f, 0.3f) : Color.white;
                string text = p.State.Value == LifeState.Downed ? $"{p.TaggedName} (DOWN)" : p.TaggedName;
                Shadowed(new Rect(screen.x - 150 * u, Screen.height - screen.y - 14 * u, 300 * u, 28 * u), text, style);
            }
        }

        void DrawCrosshair(float w, float h, float u)
        {
            float aim = _gun.AimAmount;
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
                Fill(cx - thick / 2 + o, cy - gap - len + o, thick, len, col);
                Fill(cx - thick / 2 + o, cy + gap + o, thick, len, col);
                Fill(cx - gap - len + o, cy - thick / 2 + o, len, thick, col);
                Fill(cx + gap + o, cy - thick / 2 + o, len, thick, col);
            }
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
                Fill(center.x - 1.5f * u, center.y - inner - size, 3f * u, size, c);
                GUI.matrix = old;
            }
        }

        void DrawDamageVignette(float w, float h)
        {
            float recent = Mathf.Clamp01(1f - (Time.time - _player.LocalLastDamageTime) / 0.6f);
            float low = _player.IsAlive ? Mathf.Clamp01(1f - _player.Health.Value / _player.MaxHealthNow / 0.5f) : 0f;
            float a = Mathf.Max(recent * 0.35f, low * 0.3f * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f)));
            if (a <= 0.001f) return;
            float edge = h * 0.12f;
            var c = new Color(0.6f, 0f, 0f, a);
            Fill(0, 0, w, edge, c);
            Fill(0, h - edge, w, edge, c);
            Fill(0, edge, edge, h - edge * 2, c);
            Fill(w - edge, edge, edge, h - edge * 2, c);
            Fill(0, 0, w, h, new Color(0.5f, 0f, 0f, a * 0.25f));
        }
    }
}
