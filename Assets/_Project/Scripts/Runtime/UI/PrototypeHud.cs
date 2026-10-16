using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Player;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// Throwaway immediate-mode HUD for the prototype: crosshair, hitmarkers, ammo, health,
    /// damage vignette, stats and debug help. The real HUD is built in UI Toolkit in Phase 7.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        HitscanGun _gun;
        PlayerHealth _health;
        ZombieSpawner _spawner;
        PlayerLook _look;

        Texture2D _white;
        GUIStyle _label, _big, _small;
        float _hitTime = -100f;
        bool _hitKilled, _hitHead;
        bool _showHelp = true;

        public void Init(HitscanGun gun, PlayerHealth health, ZombieSpawner spawner, PlayerLook look)
        {
            _gun = gun;
            _health = health;
            _spawner = spawner;
            _look = look;
            _gun.Hit += OnHit;
        }

        void OnDestroy()
        {
            if (_gun != null) _gun.Hit -= OnHit;
        }

        void OnHit(DamageResult result, bool headshot)
        {
            _hitTime = Time.time;
            _hitKilled = result.Killed;
            _hitHead = headshot;
        }

        void Update()
        {
            if (GameInput.DebugPressed(DebugKey.ToggleHelp)) _showHelp = !_showHelp;
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
            if (_label == null || _label.fontSize != size)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
                _label.normal.textColor = Color.white;
                _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(size * 2.4f), alignment = TextAnchor.MiddleCenter };
                _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(size * 0.75f), fontStyle = FontStyle.Normal };
            }
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
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        void OnGUI()
        {
            if (_gun == null || _health == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height, u = h / 1080f;

            DrawDamageVignette(w, h);

            if (!_health.IsAlive)
            {
                Fill(0, 0, w, h, new Color(0.15f, 0f, 0f, 0.55f));
                Shadowed(new Rect(0, h * 0.35f, w, 120 * u), "YOU DIED", _big);
                var center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, h * 0.35f + 120 * u, w, 40 * u),
                    $"Kills {_spawner.Kills}   Headshots {_spawner.Headshots}   Points {_spawner.Points:N0}", center);
                Shadowed(new Rect(0, h * 0.35f + 170 * u, w, 40 * u), "Press Enter to restart", center);
                return;
            }

            DrawCrosshair(w, h, u);
            DrawHitmarker(w, h, u);

            // Ammo, bottom right.
            var right = new GUIStyle(_label) { alignment = TextAnchor.LowerRight };
            var bigRight = new GUIStyle(_big) { alignment = TextAnchor.LowerRight };
            Color ammoColor = _gun.Magazine == 0 ? new Color(1f, 0.3f, 0.25f) : _gun.Magazine <= _gun.Data.magazineSize / 4 ? new Color(1f, 0.8f, 0.3f) : Color.white;
            var oldAmmoColor = bigRight.normal.textColor;
            bigRight.normal.textColor = ammoColor;
            Shadowed(new Rect(w - 420 * u, h - 140 * u, 260 * u, 90 * u), _gun.Magazine.ToString(), bigRight);
            bigRight.normal.textColor = oldAmmoColor;
            Shadowed(new Rect(w - 160 * u, h - 112 * u, 120 * u, 50 * u), "/ " + _gun.Reserve, right);
            Shadowed(new Rect(w - 520 * u, h - 52 * u, 480 * u, 40 * u), _gun.Data.displayName, right);
            if (_gun.IsReloading)
            {
                float bw = 160 * u;
                Fill(w / 2 - bw / 2, h * 0.58f, bw, 6 * u, new Color(0f, 0f, 0f, 0.5f));
                Fill(w / 2 - bw / 2, h * 0.58f, bw * _gun.ReloadProgress, 6 * u, Color.white);
                var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, h * 0.58f + 8 * u, w, 30 * u), "RELOADING", c);
            }
            else if (_gun.Magazine == 0 && _gun.Reserve == 0)
            {
                var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, h * 0.58f, w, 30 * u), "NO AMMO", c);
            }
            else if (_gun.Magazine <= _gun.Data.magazineSize / 4)
            {
                var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, h * 0.58f, w, 30 * u), "R  RELOAD", c);
            }

            // Health, bottom left.
            float hbw = 320 * u, hbh = 14 * u, hx = 40 * u, hy = h - 60 * u;
            Fill(hx - 2, hy - 2, hbw + 4, hbh + 4, new Color(0f, 0f, 0f, 0.5f));
            Color hc = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.85f, 0.85f, 0.85f), _health.Normalized);
            Fill(hx, hy, hbw * _health.Normalized, hbh, hc);
            Shadowed(new Rect(hx, hy - 40 * u, hbw, 36 * u), Mathf.CeilToInt(_health.Health).ToString(), _label);

            // Stats, top left.
            Shadowed(new Rect(40 * u, 30 * u, 600 * u, 40 * u), $"{_spawner.Points:N0} pts", _label);
            Shadowed(new Rect(40 * u, 66 * u, 600 * u, 30 * u), $"Kills {_spawner.Kills}   Headshots {_spawner.Headshots}", _small);
            Shadowed(new Rect(40 * u, 94 * u, 900 * u, 30 * u),
                $"Test round {_spawner.TestRound}  ·  zombie HP {_spawner.CurrentZombieHealth:N0}  ·  {_spawner.CurrentSpeedTier}  ·  alive {_spawner.Alive}/{_spawner.TargetAlive}",
                _small);

            if (_showHelp)
            {
                var r = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                Shadowed(new Rect(w - 640 * u, 30 * u, 600 * u, 260 * u),
                    "WASD move · Shift sprint · Space jump\nLMB fire · RMB aim · R reload\n[ / ]  fewer / more zombies\n- / =  test round down / up\nEsc free mouse · H hide help",
                    r);
            }

            if (!_look.CursorLocked)
            {
                Fill(0, 0, w, h, new Color(0f, 0f, 0f, 0.45f));
                Shadowed(new Rect(0, h * 0.42f, w, 80 * u), "CLICK TO PLAY", _big);
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
            float pxPerDeg = h / fov;
            float gap = Mathf.Max(4f * u, _gun.CurrentSpread * pxPerDeg);
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
            float recent = Mathf.Clamp01(1f - (Time.time - _health.LastDamageTime) / 0.6f);
            float low = Mathf.Clamp01(1f - _health.Normalized / 0.5f);
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
