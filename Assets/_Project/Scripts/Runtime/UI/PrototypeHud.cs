using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Net;
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

        public void Init(NetPlayer player)
        {
            _player = player;
            _gun = player.Gun;
            _gun.Hit += OnHit;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
        }

        void OnDestroy()
        {
            if (_gun != null) _gun.Hit -= OnHit;
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
        }

        void WatchRounds()
        {
            var rounds = RoundManager.Instance;
            if (rounds == null) return;
            var phase = rounds.Phase.Value;
            int round = rounds.Round.Value;
            if (phase == GamePhase.RoundActive && (round != _lastRound || _lastPhase != GamePhase.RoundActive))
            {
                ShowBanner($"ROUND {round}", round == 1 ? "Here they come." : null);
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

            if (_showHelp)
            {
                var r = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                string host = rounds != null && rounds.IsServer ? "\nTest mode (host):  = next round · ] jump 10 rounds · G god mode · P +5,000 pts" : "";
                Shadowed(new Rect(w - 800 * u, 30 * u, 760 * u, 260 * u),
                    "WASD move · Shift sprint · Space jump\nLMB fire · RMB aim · R reload · Q / wheel switch gun\nE buy · hold E revive" + host + "\nEsc menu · H hide help",
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
                Shadowed(new Rect(0, y, w, 36 * u), $"{p.DisplayName}   ·   {p.Points.Value:N0} pts   ·   {p.Kills.Value} kills   ·   {p.Headshots.Value} headshots", _center);
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
            int quarter = _gun.Data.magazineSize / 4;
            bigRight.normal.textColor = _gun.Magazine == 0 ? new Color(1f, 0.3f, 0.25f) : _gun.Magazine <= quarter ? new Color(1f, 0.8f, 0.3f) : Color.white;
            Shadowed(new Rect(w - 420 * u, h - 140 * u, 260 * u, 90 * u), _gun.Magazine.ToString(), bigRight);
            Shadowed(new Rect(w - 160 * u, h - 112 * u, 120 * u, 50 * u), "/ " + _gun.Reserve, right);
            Shadowed(new Rect(w - 520 * u, h - 52 * u, 480 * u, 40 * u), _gun.Data.displayName, right);
            int other = 1 - _player.ActiveSlot.Value;
            var otherGun = GameConfig.GunById(_player.GunInSlot(other));
            if (otherGun != null)
            {
                var dim = new GUIStyle(_small) { alignment = TextAnchor.LowerRight };
                dim.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                Shadowed(new Rect(w - 520 * u, h - 176 * u, 480 * u, 30 * u), $"Q  {otherGun.displayName}", dim);
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
            long price = _player.PriceFor(b, out string what);
            if (price < 0) return;
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
            float n = _player.Health.Value / NetPlayer.MaxHealth;
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
            Shadowed(new Rect(40 * u, 66 * u, 600 * u, 30 * u), $"Kills {_player.Kills.Value}   Headshots {_player.Headshots.Value}", _small);
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
                Shadowed(new Rect(58 * u, y, 600 * u, 26 * u), $"{p.DisplayName}  ·  {status}  ·  {p.Points.Value:N0} pts", style);
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
                string text = p.State.Value == LifeState.Downed ? $"{p.DisplayName} (DOWN)" : p.DisplayName;
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
            float low = _player.IsAlive ? Mathf.Clamp01(1f - _player.Health.Value / NetPlayer.MaxHealth / 0.5f) : 0f;
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
