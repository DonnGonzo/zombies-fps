using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The settings screen, shared by the main menu and the in-game pause menu: key bindings, mouse and view,
    /// sound, and accessibility. Everything saves as soon as it changes.
    /// </summary>
    public static class SettingsMenu
    {
        enum Tab { Controls, MouseAndView, Graphics, Sound, Accessibility }

        static readonly string[] TabNames = { "Controls", "Mouse & view", "Graphics", "Sound", "Accessibility" };

        static Tab s_Tab;
        static Vector2 s_Scroll;
        static int s_Capturing = -1;
        static int s_CaptureFrame;

        public static bool IsCapturing => s_Capturing >= 0;

        /// <summary>Draws the screen in a rect. Returns false once the player presses Back.</summary>
        public static bool Draw(Rect area, float u)
        {
            Ui.EnsureStyles(u);
            Ui.PanelBox(area, Ui.Blood, u);
            float pad = 28 * u;
            var inner = new Rect(area.x + pad, area.y + pad, area.width - pad * 2, area.height - pad * 2);
            Ui.Label(new Rect(inner.x, inner.y, inner.width, 50 * u), "SETTINGS", Ui.Header, Ui.Text, TextAnchor.UpperLeft);

            bool capturing = IsCapturing;
            GUI.enabled = !capturing;
            float tw = (inner.width - (TabNames.Length - 1) * 8 * u) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                var r = new Rect(inner.x + i * (tw + 8 * u), inner.y + 58 * u, tw, 44 * u);
                if (GUI.Toggle(r, s_Tab == (Tab)i, TabNames[i], Ui.ButtonStyle) && s_Tab != (Tab)i)
                {
                    s_Tab = (Tab)i;
                    s_Scroll = Vector2.zero;
                }
            }

            var body = new Rect(inner.x, inner.y + 120 * u, inner.width, inner.height - 120 * u - 64 * u);
            switch (s_Tab)
            {
                case Tab.Controls: DrawControls(body, u); break;
                case Tab.MouseAndView: DrawMouseAndView(body, u); break;
                case Tab.Graphics: DrawGraphics(body, u); break;
                case Tab.Sound: DrawSound(body, u); break;
                default: DrawAccessibility(body, u); break;
            }

            bool open = true;
            float bw = 220 * u;
            var bottom = new Rect(inner.x, inner.yMax - 50 * u, bw, 50 * u);
            if (Ui.Button(bottom, "Back", accent: true))
            {
                GameSettings.Save();
                open = false;
            }
            if (Ui.Button(new Rect(inner.xMax - 260 * u, inner.yMax - 50 * u, 260 * u, 50 * u), "Reset this page"))
            {
                if (s_Tab == Tab.Controls) Bindings.ResetAll();
                else ResetPage(s_Tab);
            }
            GUI.enabled = true;

            if (capturing) DrawCapture(area, u);
            return open;
        }

        static void ResetPage(Tab tab)
        {
            switch (tab)
            {
                case Tab.MouseAndView:
                    GameSettings.Sensitivity = 1f;
                    GameSettings.InvertY = false;
                    GameSettings.Fov = 75f;
                    break;
                case Tab.Sound:
                    GameSettings.MasterVolume = 1f;
                    GameSettings.EffectsVolume = 1f;
                    GameSettings.VoiceVolume = 1f;
                    break;
                case Tab.Accessibility:
                    GameSettings.Subtitles = true;
                    GameSettings.ReduceShake = false;
                    GameSettings.Colorblind = false;
                    GameSettings.HudScale = 1f;
                    GameSettings.Tips = true;
                    break;
            }
            GameSettings.Save();
        }

        static void DrawControls(Rect body, float u)
        {
            float rowH = 46 * u;
            Ui.Label(new Rect(body.x, body.y - 14 * u, body.width, 24 * u),
                "Click a key to change it, then press the new key or mouse button. Esc cancels. A key that's already used swaps over.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            var view = new Rect(body.x, body.y + 18 * u, body.width, body.height - 18 * u);
            var content = new Rect(0, 0, view.width - 24 * u, rowH * Bindings.Count + 60 * u);
            s_Scroll = GUI.BeginScrollView(view, s_Scroll, content, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < Bindings.Count; i++)
            {
                var action = (GameAction)i;
                var r = new Rect(0, i * rowH, content.width, rowH - 6 * u);
                if (i % 2 == 0) Ui.RoundRect(r, new Color(1f, 1f, 1f, 0.035f), small: true);
                Ui.Label(new Rect(r.x + 14 * u, r.y, r.width * 0.55f, r.height), Bindings.Label(action), Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
                bool changed = Bindings.Get(action) != Bindings.Default(action);
                var keyRect = new Rect(r.xMax - 250 * u, r.y + 3 * u, 240 * u, r.height - 6 * u);
                string label = s_Capturing == i ? "Press a key..." : Bindings.Name(action) + (changed ? "  *" : "");
                if (GUI.Button(keyRect, label, s_Capturing == i ? Ui.ButtonAccent : Ui.ButtonStyle))
                {
                    s_Capturing = i;
                    s_CaptureFrame = Time.frameCount;
                    GameInput.Suspended = true;
                }
            }
            Ui.Label(new Rect(14 * u, Bindings.Count * rowH + 8 * u, content.width, 40 * u),
                "Fixed keys: Esc menu, 1-9 shop and power-up picks, mouse wheel switches weapon. A gamepad works too.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            GUI.EndScrollView();
        }

        static void DrawCapture(Rect area, float u)
        {
            Ui.Fill(area, new Color(0f, 0f, 0f, 0.55f));
            var box = new Rect(area.center.x - 300 * u, area.center.y - 90 * u, 600 * u, 180 * u);
            Ui.PanelBox(box, Ui.Amber, u);
            Ui.Label(new Rect(box.x, box.y + 30 * u, box.width, 40 * u), Bindings.Label((GameAction)s_Capturing), Ui.Header, Ui.Text, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(box.x, box.y + 90 * u, box.width, 30 * u), "Press the new key or mouse button  ·  Esc to cancel", Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);

            // Poll once per frame, and not on the frame of the click that started it.
            if (Event.current.type != EventType.Repaint || Time.frameCount <= s_CaptureFrame) return;
            if (GameInput.KeyDown(KeyCode.Escape))
            {
                StopCapture();
                return;
            }
            if (GameInput.TryGetPressedKey(out KeyCode key))
            {
                Bindings.Set((GameAction)s_Capturing, key);
                StopCapture();
            }
        }

        static void StopCapture()
        {
            s_Capturing = -1;
            GameInput.Suspended = false;
        }

        /// <summary>Called when the screen is closed from outside (the menu went away).</summary>
        public static void Close()
        {
            if (IsCapturing) StopCapture();
            GameSettings.Save();
        }

        static void DrawMouseAndView(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.Sensitivity = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Mouse sensitivity", GameSettings.Sensitivity,
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity, $"{GameSettings.Sensitivity:0.00}x", u);
            y += rowH;
            GameSettings.Fov = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Field of view", GameSettings.Fov,
                GameSettings.MinFov, GameSettings.MaxFov, $"{GameSettings.Fov:0}°", u);
            y += rowH;
            GameSettings.InvertY = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Invert look up/down", GameSettings.InvertY, u);
            y += rowH + 10 * u;
            Ui.Label(new Rect(body.x, y, body.width, 60 * u), "Aiming down sights slows the mouse a little on top of this, and zooms in by the same amount at any field of view.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        static void DrawGraphics(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            Ui.Label(new Rect(body.x, y, 260 * u, rowH), "Quality", Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
            float bw = 150 * u;
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                var r = new Rect(body.x + 280 * u + i * (bw + 8 * u), y + 8 * u, bw, rowH - 16 * u);
                if (GUI.Toggle(r, GameSettings.Quality == i, GameSettings.QualityNames[i], Ui.ButtonStyle)) GameSettings.Quality = i;
            }
            y += rowH;
            GameSettings.Brightness = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Brightness", GameSettings.Brightness,
                GameSettings.MinBrightness, GameSettings.MaxBrightness, $"{GameSettings.Brightness * 100f:0}%", u);
            y += rowH;
            GameSettings.Bloom = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Bloom", GameSettings.Bloom, u, "glow around lamps, muzzle flashes and eyes");
            y += rowH;
            GameSettings.FilmGrain = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Film grain", GameSettings.FilmGrain, u);
            y += rowH;
            GameSettings.AnimatedZombies = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Animated zombie models", GameSettings.AnimatedZombies, u, "off = the old blocky zombies");
            y += rowH + 10 * u;
            Ui.Label(new Rect(body.x, y, body.width, 80 * u), "Low turns off bloom and shortens shadows for older PCs. Medium adds bloom, soft shadows and smoother edges. High adds sharper, longer shadows, reflections on metal and the smoothest edges.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        static void DrawSound(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.MasterVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Master volume", GameSettings.MasterVolume, 0f, 1f, $"{GameSettings.MasterVolume * 100f:0}%", u);
            y += rowH;
            GameSettings.EffectsVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Effects", GameSettings.EffectsVolume, 0f, 1f, $"{GameSettings.EffectsVolume * 100f:0}%", u);
            y += rowH;
            GameSettings.VoiceVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Voices and radio", GameSettings.VoiceVolume, 0f, 1f, $"{GameSettings.VoiceVolume * 100f:0}%", u);
        }

        static void DrawAccessibility(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.Subtitles = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Subtitles", GameSettings.Subtitles, u, "voice lines and radio as text");
            y += rowH;
            GameSettings.ReduceShake = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Reduce camera shake", GameSettings.ReduceShake, u);
            y += rowH;
            GameSettings.Colorblind = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Colour-blind friendly colours", GameSettings.Colorblind, u, "blue and orange instead of green and red");
            y += rowH;
            GameSettings.Tips = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Show tips", GameSettings.Tips, u, "first-time hints while you play");
            y += rowH;
            GameSettings.HudScale = Ui.Slider(new Rect(body.x, y, body.width, rowH), "HUD size", GameSettings.HudScale,
                GameSettings.MinHudScale, GameSettings.MaxHudScale, $"{GameSettings.HudScale * 100f:0}%", u);
            y += rowH + 6 * u;
            if (Ui.Button(new Rect(body.x, y, 300 * u, 46 * u), "Show all tips again")) Tips.ResetSeen();
        }
    }
}
